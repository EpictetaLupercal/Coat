// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Coat;

public partial class EditorCanvas
{
    internal (int Level, bool Lower, bool Upper, object? Focus) MapVisibility =>
        (Plan.ActiveLevel, ShowLowerLevels, ShowUpperLevels, focusView);
    internal bool MapShowsAnnotation(Annotation note) => IsLevelVisible(note.Level) && FocusIncludesAnnotation(note);
    internal bool MapShowsLevel(int level) => IsLevelVisible(level);

    internal (Rect Bounds, (Point A, Point B)[] Segments) MakeMapGeometry()
    {
        var bounds = Rect.Empty;
        // A single floor-independent outline. Annotation, line style and group data stay with the editor.
        var segments = Plan.Edges.Where(edge => !edge.Dimension).Select(edge =>
        {
            var a = VertexAt(edge.A); var b = VertexAt(edge.B);
            var from = new Point(a.X, -a.Y); var to = new Point(b.X, -b.Y);
            bounds.Union(from); bounds.Union(to);
            return (from, to);
        }).ToArray();
        return (bounds, segments);
    }
}

internal sealed class Minimap : FrameworkElement
{
    internal const double Size = 196;
    internal static readonly Rect MapBounds = new(16, 24, 164, 164);
    private static readonly Point Middle = new(MapBounds.Left + MapBounds.Width / 2, MapBounds.Top + MapBounds.Height / 2);
    private static readonly RectangleGeometry FrameClip = MakeFrameClip();
    private readonly RtsHud hud;
    private EditorCanvas Canvas => hud.Host.Canvas;
    private readonly DrawingVisual overlays = new();
    private (Rect Bounds, (Point A, Point B)[] Segments)? scene;
    private BitmapSource? topology;
    private Matrix? rasterMap;
    private (Matrix Map, Matrix Inverse)? dragFrame;
    private V2 dragOffset;
    private bool pressed, dragged;
    private Point pressAt;
    internal int GeometryBuilds { get; private set; }
    internal int RasterBuilds { get; private set; }

    internal Minimap(RtsHud hud)
    {
        this.hud = hud;
        Width = Height = Size;
        Cursor = Cursors.Cross;
        AddVisualChild(overlays);
        System.Windows.Automation.AutomationProperties.SetName(this, L.T("Minicarte"));
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => index == 0 ? overlays : throw new ArgumentOutOfRangeException(nameof(index));

    internal void InvalidateGeometry()
    {
        scene = null;
        topology = null;
        rasterMap = null;
        InvalidateVisual();
    }

    private static RectangleGeometry MakeFrameClip()
    {
        var clip = new RectangleGeometry(MapBounds);
        clip.Freeze();
        return clip;
    }

    private (Matrix Map, Matrix Inverse) Frame()
    {
        if (scene == null)
        {
            scene = Canvas.MakeMapGeometry();
            GeometryBuilds++;
        }
        if (dragFrame is { } fixedFrame) return fixedFrame;
        var bounds = scene.Value.Bounds;
        bool empty = bounds.IsEmpty;
        if (empty) bounds = new Rect(Canvas.Center.X - 256, -Canvas.Center.Y - 256, 512, 512);
        foreach (var ping in hud.LivePings.Where(p => Canvas.MapShowsLevel(p.Level)))
            bounds.Union(new Point(ping.Position.X, -ping.Position.Y));
        var center = empty ? new Point(Canvas.Center.X, -Canvas.Center.Y) : new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        double half = Math.Max(32, Math.Max(Math.Max(center.X - bounds.Left, bounds.Right - center.X),
            Math.Max(center.Y - bounds.Top, bounds.Bottom - center.Y)));
        double scale = MapBounds.Width / (2 * half * 1.16);
        var matrix = Matrix.Identity;
        matrix.Translate(-center.X, -center.Y);
        matrix.Scale(scale, scale);
        matrix.Translate(Middle.X, Middle.Y);
        var inverse = matrix;
        inverse.Invert();
        return (matrix, inverse);
    }

    internal Point MapPoint(V2 world) => Frame().Map.Transform(new Point(world.X, -world.Y));
    internal V2 PlanPoint(Point local)
    {
        var point = Frame().Inverse.Transform(local);
        return new(point.X, -point.Y);
    }
    internal Rect CameraWorldRect => new(Canvas.Center.X - Canvas.ActualWidth / (2 * Canvas.Zoom),
        -Canvas.Center.Y - Canvas.ActualHeight / (2 * Canvas.Zoom), Canvas.ActualWidth / Canvas.Zoom, Canvas.ActualHeight / Canvas.Zoom);

    protected override HitTestResult? HitTestCore(PointHitTestParameters parameters) =>
        MapBounds.Contains(parameters.HitPoint) ? new PointHitTestResult(this, parameters.HitPoint) : null;

    internal static (Point Position, Vector Direction)? OffMapCamera(Point projectedCenter)
    {
        if (MapBounds.Contains(projectedCenter)) return null;
        var direction = projectedCenter - Middle;
        const double inset = 5;
        double edge = Math.Max(Math.Abs(direction.X) / (MapBounds.Width / 2 - inset),
            Math.Abs(direction.Y) / (MapBounds.Height / 2 - inset));
        var position = Middle + direction / edge;
        direction.Normalize();
        return (position, direction);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var frame = Frame();
        dc.DrawGeometry(hud.Host.ChromeBrush("#151F2B"), new Pen(hud.Host.ChromeBrush("#526678"), 1.5), FrameClip);
        if (!scene!.Value.Bounds.IsEmpty)
        {
            if (rasterMap != frame.Map)
            {
                topology = Rasterize(frame.Map, scene.Value.Segments);
                rasterMap = frame.Map;
                RasterBuilds++;
            }
            dc.DrawImage(topology, MapBounds);
        }
        DrawOverlays(frame.Map);
    }

    internal int CameraIntervalMs { get; set; }
    private long lastCameraFrame;
    private System.Windows.Threading.DispatcherTimer? pendingCamera;

    internal void CameraMoved()
    {
        if (!IsVisible || scene == null) return;
        long remaining = CameraIntervalMs - (Environment.TickCount64 - lastCameraFrame);
        if (remaining <= 0 || pressed) { RefreshCamera(); return; }
        if (pendingCamera != null) return;
        pendingCamera = new() { Interval = TimeSpan.FromMilliseconds(remaining) };
        pendingCamera.Tick += CameraTick;
        pendingCamera.Start();
    }

    private void CameraTick(object? sender, EventArgs e) => RefreshCamera();

    internal void RefreshCamera()
    {
        if (pendingCamera != null)
        {
            pendingCamera.Stop(); pendingCamera.Tick -= CameraTick; pendingCamera = null;
        }
        lastCameraFrame = Environment.TickCount64;
        if (!IsVisible || scene == null) return;
        var frame = Frame();
        if (!scene.Value.Bounds.IsEmpty && rasterMap != frame.Map) InvalidateVisual();
        else DrawOverlays(frame.Map);
    }

    private BitmapSource Rasterize(Matrix projection, (Point A, Point B)[] segments)
    {
        const int resolution = 128;
        var pixels = new uint[resolution * resolution];
        var color = ((SolidColorBrush)hud.Host.ChromeBrush("#78DCC1")).Color;
        uint ink = 0xff000000u | (uint)color.R << 16 | (uint)color.G << 8 | color.B;
        (int X, int Y) Pixel(Point world)
        {
            var point = projection.Transform(world);
            return ((int)((point.X - MapBounds.Left) * resolution / MapBounds.Width),
                (int)((point.Y - MapBounds.Top) * resolution / MapBounds.Height));
        }
        foreach (var (a, b) in segments)
        {
            var (x, y) = Pixel(a);
            var (endX, endY) = Pixel(b);
            int dx = Math.Abs(endX - x), dy = -Math.Abs(endY - y);
            int sx = Math.Sign(endX - x), sy = Math.Sign(endY - y), error = dx + dy;
            while (true)
            {
                if ((uint)x < resolution && (uint)y < resolution) pixels[y * resolution + x] = ink;
                if (x == endX && y == endY) break;
                int distance = 2 * error;
                if (distance >= dy) { error += dy; x += sx; }
                if (distance <= dx) { error += dx; y += sy; }
            }
        }
        var image = BitmapSource.Create(resolution, resolution, 96, 96, PixelFormats.Pbgra32, null, pixels, resolution * 4);
        image.Freeze();
        return image;
    }

    private void DrawOverlays(Matrix projection)
    {
        using var dc = overlays.RenderOpen();
        dc.PushClip(FrameClip);
        var cameraBounds = CameraWorldRect;
        dc.DrawRectangle(EditorCanvas.Brush("#3078DCC1"), new Pen(hud.Host.ChromeBrush("#C0F3DA"), 1.15),
            new Rect(projection.Transform(cameraBounds.TopLeft), projection.Transform(cameraBounds.BottomRight)));
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        foreach (var ping in hud.VisiblePings())
        {
            var point = projection.Transform(new Point(ping.Position.X, -ping.Position.Y));
            RtsHud.DrawPingHalo(dc, point, ping, 24, now);
            RtsHud.DrawPing(dc, point, ping.Color, ping.Kind, 4.5, ping.NoteId != null);
        }
        var camera = projection.Transform(new Point(Canvas.Center.X, -Canvas.Center.Y));
        if (OffMapCamera(camera) is { } indicator)
        {
            var (point, direction) = indicator;
            var perpendicular = new Vector(-direction.Y, direction.X);
            var arrow = new StreamGeometry();
            using (var path = arrow.Open())
            {
                path.BeginFigure(point + direction * 3.5, true, true);
                path.LineTo(point - direction * 3 + perpendicular * 3, true, false);
                path.LineTo(point - direction * 3 - perpendicular * 3, true, false);
            }
            dc.DrawGeometry(hud.Host.ChromeBrush("#FFD480"), new Pen(EditorCanvas.Brush("#151F2B"), 1), arrow);
        }
        dc.Pop();
    }

    internal void BeginPointer(Point point)
    {
        if (hud.UsePingTarget(point, MapPoint, PlanPoint(point), ctrl: false)) return;
        var world = PlanPoint(point);
        if (!CameraWorldRect.Contains(new Point(world.X, -world.Y))) { hud.CenterOn(world); return; }
        dragFrame = Frame();
        pressAt = point;
        dragOffset = Canvas.Center - world;
        pressed = true;
        dragged = false;
    }

    internal void MovePointer(Point point)
    {
        if (!pressed) return;
        if (!dragged && (point - pressAt).Length < 3) return;
        dragged = true;
        hud.CenterOn(PlanPoint(point) + dragOffset);
    }

    internal void EndPointer(Point point)
    {
        if (!pressed) return;
        if (!dragged) hud.CenterOn(PlanPoint(point));
        CancelPointer();
    }

    private void CancelPointer()
    {
        pressed = false;
        dragFrame = null;
        RefreshCamera();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        BeginPointer(e.GetPosition(this));
        if (pressed) CaptureMouse();
        e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e) => MovePointer(e.GetPosition(this));
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        EndPointer(e.GetPosition(this));
        ReleaseMouseCapture();
        e.Handled = true;
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) => CancelPointer();
    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e) =>
        e.Handled = hud.OpenPingMenu(this, e.GetPosition(this), MapPoint);
}

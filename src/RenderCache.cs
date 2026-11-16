// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Media;

namespace Coat;
public partial class EditorCanvas
{
    private DrawingGroup? cachedScene;
    private object? cachedSceneStyle;
    private V2 sceneCenter;
    private DrawingBrush? navigationBrush;
    private Rect navigationBounds;
    private V2 bitmapCenter;
    private const int NavigationMargin = 256;
    private void DrawSceneForNavigation(DrawingContext target)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        double width = ActualWidth + NavigationMargin * 2, height = ActualHeight + NavigationMargin * 2;
        bool cache = !Exporting && !dragging && !extruding && width * dpi.DpiScaleX <= 8192 && height * dpi.DpiScaleY <= 8192 && width * height * dpi.DpiScaleX * dpi.DpiScaleY <= 16000000;
        if (cache && (navigationBrush == null || Math.Abs((bitmapCenter.X - Center.X) * Zoom) > NavigationMargin || Math.Abs((Center.Y - bitmapCenter.Y) * Zoom) > NavigationMargin))
        {
            // Let WPF cache the drawing in its compositor; synchronous RenderTargetBitmap
            // would force slow software rasterization of large transparent scenes.
            navigationBounds = new Rect((Center.X - sceneCenter.X) * Zoom - NavigationMargin, (sceneCenter.Y - Center.Y) * Zoom - NavigationMargin, width, height);
            var drawing = new DrawingGroup { ClipGeometry = new RectangleGeometry(navigationBounds) }; drawing.Children.Add(cachedScene!);
            navigationBrush = new DrawingBrush(drawing) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = navigationBounds, ViewportUnits = BrushMappingMode.Absolute, Viewport = navigationBounds, Stretch = Stretch.Fill };
            RenderOptions.SetCachingHint(navigationBrush, CachingHint.Cache);
            RenderOptions.SetCacheInvalidationThresholdMinimum(navigationBrush, .99); RenderOptions.SetCacheInvalidationThresholdMaximum(navigationBrush, 1.01);
            navigationBrush.Freeze(); bitmapCenter = Center;
        }
        target.PushTransform(new TranslateTransform((sceneCenter.X - Center.X) * Zoom, (Center.Y - sceneCenter.Y) * Zoom));
        if (cache) target.DrawRectangle(navigationBrush, null, navigationBounds); else target.DrawDrawing(cachedScene);
        target.Pop();
    }
    private HashSet<int> sceneVertices = new(), sceneEdges = new();
    private HashSet<string> sceneFaces = new();
    private Dictionary<int, Vertex> renderVertices = new();
    private Dictionary<int, Edge> renderEdges = new();
    private Dictionary<string, Face> renderFaces = new();
    private Dictionary<string, HashSet<int>> faceEdges = new();
    private Dictionary<int, Vertex[]> verticesByLevel = new();
    private Dictionary<int, Edge[]> edgesByLevel = new();
    private Dictionary<int, Face[]> facesByLevel = new();
    private Dictionary<string, (Geometry Geometry, Rect Bounds)> faceGeometry = new();
    internal int RenderCacheBuilds { get; private set; }
    private Vertex VertexAt(int id) => renderVertices[id];
    private HashSet<int> CachedFaceEdges(Face face) => faceEdges[face.Key];
    private void RebuildRenderCache()
    {
        RenderCacheBuilds++;
        renderVertices = Plan.Vertices.ToDictionary(v => v.Id);
        renderEdges = Plan.Edges.ToDictionary(e => e.Id);
        renderFaces = faces.ToDictionary(f => f.Key);
        verticesByLevel = Plan.Vertices.GroupBy(v => v.Level).ToDictionary(g => g.Key, g => g.ToArray());
        edgesByLevel = Plan.Edges.GroupBy(e => VertexAt(e.A).Level).ToDictionary(g => g.Key, g => g.ToArray());
        facesByLevel = faces.GroupBy(f => f.Level).ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.Area).ToArray());
        var endpoints = Plan.Edges.ToLookup(e => (Math.Min(e.A, e.B), Math.Max(e.A, e.B)));
        faceEdges = faces.ToDictionary(f => f.Key, f => Enumerable.Range(0, f.Vertices.Count).SelectMany(i =>
        { int a = f.Vertices[i], b = f.Vertices[(i + 1) % f.Vertices.Count]; return endpoints[(Math.Min(a, b), Math.Max(a, b))].Select(e => e.Id); }).ToHashSet());
        RebuildFaceGeometry();
    }
    private void RebuildFaceGeometry()
    {
        cachedScene = null; navigationBrush = null; faceGeometry.Clear();
        var regions = new ContourRegions(faces, renderVertices.ToDictionary(p => p.Key, p => p.Value.P));
        foreach (var face in faces)
        {
            var geometry = ContourRegions.Geometry(regions.Loops(face.Key));
            faceGeometry[face.Key] = (geometry, geometry.Bounds);
        }
    }
    internal bool FillContains(string key, V2 point) => faceGeometry[key].Geometry.FillContains(new Point(point.X, -point.Y));

    private void DrawFace(DrawingContext dc, Face face, Brush brush, bool outline = false)
    {
        dc.PushTransform(new MatrixTransform(Zoom, 0, 0, Zoom, ActualWidth / 2 - Center.X * Zoom, ActualHeight / 2 + Center.Y * Zoom));
        dc.DrawGeometry(brush, outline ? new Pen(Accent, 3 / Zoom) : null, faceGeometry[face.Key].Geometry); dc.Pop();
    }
    private readonly Dictionary<(string, FillPattern), Brush> patternBrushes = new();
    private Brush WorldFill(string color, FillPattern pattern)
    {
        if (pattern == FillPattern.Solid) return Brush(color);
        if (patternBrushes.TryGetValue((color, pattern), out var cached)) return cached;
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawRectangle(Brush(color), null, new Rect(0, 0, 12, 12)); var dark = new SolidColorBrush(Color.FromArgb(85, 0, 0, 0));
            if (pattern == FillPattern.Stripes) dc.DrawRectangle(dark, null, new Rect(0, 0, 4, 12));
            else dc.DrawEllipse(dark, null, new Point(6, 6), 1.5, 1.5);
        }
        var result = new DrawingBrush(drawing) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 12, 12), ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 12, 12) };
        result.Freeze(); if (patternBrushes.Count > 512) patternBrushes.Clear(); patternBrushes[(color, pattern)] = result; return result;
    }
    private readonly Dictionary<(string, double, Brush, string, double), FormattedText> labels = new();
    private static readonly Dictionary<(string, double, LineStyle), Pen> pens = new();
    private static Pen CachedPen(string color, double width, LineStyle style = LineStyle.Solid)
    {
        if (pens.TryGetValue((color, width, style), out var pen)) return pen;
        pen = new Pen(Brush(color), width) { DashStyle = EdgeDash(style) }; pen.Freeze();
        if (pens.Count > 4096) pens.Clear(); pens[(color, width, style)] = pen; return pen;
    }
    private void DrawEdgeBatch(DrawingContext dc, IEnumerable<Edge> edges, HashSet<int> selectedPoints, int? hover = null, bool upper = false)
    {
        var batches = edges.GroupBy(e =>
        {
            bool selected = !Exporting && (SelectedEdges.Contains(e.Id) || selectedPoints.Contains(e.A) && selectedPoints.Contains(e.B));
            return (Color: selected || hover == e.Id ? "#78DCC1" : upper ? "#FF2020" : e.Color, Width: selected ? 2.8 : upper ? .75 : 2, Style: upper ? LineStyle.Solid : e.Style);
        });
        foreach (var batch in batches)
        {
            var path = new StreamGeometry(); using (var context = path.Open()) foreach (var edge in batch)
            { context.BeginFigure(Screen(VertexAt(edge.A).P), false, false); context.LineTo(Screen(VertexAt(edge.B).P), true, false); }
            path.Freeze(); dc.DrawGeometry(null, CachedPen(batch.Key.Color, batch.Key.Width, batch.Key.Style), path);
        }
    }
    private void DrawVertexBatch(DrawingContext dc, Vertex[] vertices, HashSet<int> selectedPoints)
    {
        double radius = Mode == SelectionMode.Vertex && Tool == EditorTool.Select ? 4 : 2.6;
        foreach (var batch in vertices.GroupBy(v => selectedPoints.Contains(v.Id)))
        {
            var path = new StreamGeometry { FillRule = FillRule.Nonzero }; using (var context = path.Open()) foreach (var vertex in batch)
            {
                var center = Screen(vertex.P); context.BeginFigure(new Point(center.X + radius, center.Y), true, true);
                context.ArcTo(new Point(center.X - radius, center.Y), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                context.ArcTo(new Point(center.X + radius, center.Y), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            }
            path.Freeze(); dc.DrawGeometry(batch.Key ? Accent : Brush("#202A36"), CachedPen(batch.Key ? "#78DCC1" : "#B0BDC9", 1), path);
        }
    }
}

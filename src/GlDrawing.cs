// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Media;
using SkiaSharp;

namespace Coat;

// Translate the editor's retained scene once per revision. Camera pans replay
// the same picture and preserve the ordering and opacity of level groups.
internal static class GlDrawing
{
    internal static SKRect Bounds(Rect r) => new((float)r.Left, (float)r.Top, (float)r.Right, (float)r.Bottom);
    private static SKPoint Point(Point p) => new((float)p.X, (float)p.Y);
    private static SKColor Ink(Color c) => new(c.R, c.G, c.B, c.A);
    private static SKMatrix Matrix(Matrix m) => new((float)m.M11,(float)m.M21,(float)m.OffsetX,
        (float)m.M12,(float)m.M22,(float)m.OffsetY,0,0,1);

    internal static void Draw(SKCanvas canvas, Drawing drawing)
    {
        if (drawing is DrawingGroup group)
        {
            int saved = canvas.Save();
            if (group.Transform != null) canvas.Concat(Matrix(group.Transform.Value));
            if (group.ClipGeometry != null) { using var clip = Path(group.ClipGeometry); canvas.ClipPath(clip); }
            if (group.Opacity < 1)
            {
                using var opacity = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * group.Opacity)) };
                canvas.SaveLayer(opacity);
            }
            foreach (var child in group.Children) Draw(canvas, child);
            canvas.RestoreToCount(saved);
        }
        else if (drawing is GlyphRunDrawing glyph)
            Geometry(canvas, glyph.GlyphRun.BuildGeometry(), glyph.ForegroundBrush, null);
        else if (drawing is GeometryDrawing geometry)
            Geometry(canvas, geometry.Geometry, geometry.Brush, geometry.Pen);
        else throw new NotSupportedException(drawing.GetType().Name);
    }

    private static SKPath Path(Geometry geometry)
    {
        var flat = geometry.GetFlattenedPathGeometry(.12, ToleranceType.Absolute);
        var path = new SKPath { FillType = flat.FillRule == FillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding };
        foreach (var figure in flat.Figures)
        {
            path.MoveTo(Point(figure.StartPoint));
            foreach (var segment in figure.Segments)
            {
                if (segment is PolyLineSegment poly) foreach (var p in poly.Points) path.LineTo(Point(p));
                else if (segment is LineSegment line) path.LineTo(Point(line.Point));
            }
            if (figure.IsClosed) path.Close();
        }
        if (flat.Transform != null) path.Transform(Matrix(flat.Transform.Value));
        return path;
    }

    private static void Geometry(SKCanvas canvas, Geometry geometry, Brush? fill, Pen? pen)
    {
        if (fill != null)
        {
            using var path = Path(geometry);
            using var paint = Paint(fill, geometry.Bounds);
            canvas.DrawPath(path, paint);
        }
        if (pen?.Brush != null)
        {
            // Flattening alone may discard open, unfilled WPF figures. Widen the
            // stroke first to retain those edges, including their dashes and caps.
            using var stroke = Path(geometry.GetWidenedPathGeometry(pen, .12, ToleranceType.Absolute));
            using var paint = Paint(pen.Brush, geometry.Bounds);
            canvas.DrawPath(stroke, paint);
        }
    }

    private static SKPaint Paint(Brush brush, Rect bounds)
    {
        var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
        if (brush is SolidColorBrush solid) paint.Color = Ink(solid.Color);
        else if (brush is LinearGradientBrush gradient)
        {
            SKPoint Position(Point p) => gradient.MappingMode == BrushMappingMode.Absolute ? Point(p) :
                new((float)(bounds.X + p.X * bounds.Width), (float)(bounds.Y + p.Y * bounds.Height));
            using var shader = SKShader.CreateLinearGradient(Position(gradient.StartPoint), Position(gradient.EndPoint),
                gradient.GradientStops.Select(s => Ink(s.Color)).ToArray(), gradient.GradientStops.Select(s => (float)s.Offset).ToArray(), SKShaderTileMode.Clamp);
            paint.Shader = shader;
        }
        else if (brush is DrawingBrush tile)
        {
            using var recorder = new SKPictureRecorder();
            Draw(recorder.BeginRecording(Bounds(tile.Viewbox)), tile.Drawing);
            using var picture = recorder.EndRecording();
            var transform = SKMatrix.CreateScaleTranslation((float)(tile.Viewport.Width / tile.Viewbox.Width),
                (float)(tile.Viewport.Height / tile.Viewbox.Height), (float)tile.Viewport.X, (float)tile.Viewport.Y);
            using var shader = picture.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, transform, Bounds(tile.Viewbox));
            paint.Shader = shader;
        }
        else throw new NotSupportedException(brush.GetType().Name);
        paint.Color = paint.Color.WithAlpha((byte)Math.Round(paint.Color.Alpha * brush.Opacity));
        return paint;
    }
}

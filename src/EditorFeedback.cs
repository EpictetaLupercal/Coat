// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Media;

namespace Coat;

public partial class EditorCanvas
{
    internal InteractionFeedbackLayer Feedback { get; } = new();
    private MoveFeedback? dragFeedback;
    internal sealed record MoveFeedback(Geometry Outline, V2[] Points, int Level);

    private bool CanCue(FeedbackCue cue) => !Exporting && Feedback.CanPlay &&
        (cue is FeedbackCue.Vertex or FeedbackCue.Edge or FeedbackCue.Face or FeedbackCue.Marquee or FeedbackCue.Move
            ? Host.Shortcuts.SelectionFeedback : Host.Shortcuts.ConstructionFeedback);

    private void Cue(FeedbackCue cue, int level, double duration, Action<DrawingContext, double, Brush> draw)
    {
        if (!CanCue(cue)) return;
        var document = Plan;
        var focus = focusView;
        Feedback.Add(cue, duration, () => ReferenceEquals(Plan, document) && ReferenceEquals(focus, focusView) && IsLevelVisible(level) && CanCue(cue),
            (dc, progress) => draw(dc, progress, Host.ChromeBrush("#78DCC1")));
    }

    private void PointFeedback(FeedbackCue cue, V2 point, int level) =>
        Cue(cue, level, 180, (dc, time, brush) =>
        {
            double radius = 4 + 12 * (1 - time);
            dc.PushOpacity(.72 * (1 - time));
            dc.DrawEllipse(null, new Pen(brush, 1.5), Screen(point), radius, radius);
            dc.Pop();
        });

    private void SelectionFeedback(int? vertex, int? edge, Face? face)
    {
        if (!CanCue(FeedbackCue.Vertex)) return;
        if (vertex is int id && renderVertices.TryGetValue(id, out var point))
            PointFeedback(FeedbackCue.Vertex, point.P, point.Level);
        else if (edge is int edgeId && renderEdges.TryGetValue(edgeId, out var line))
        {
            var from = VertexAt(line.A);
            var geometry = new LineGeometry(WorldPoint(from.P), WorldPoint(VertexAt(line.B).P));
            geometry.Freeze();
            OutlineFeedback(FeedbackCue.Edge, from.Level, geometry, 240, 16);
        }
        else if (face != null)
        {
            OutlineFeedback(FeedbackCue.Face, face.Level, faceGeometry[face.Key].Geometry, 300, 24);
        }
    }

    private void OutlineFeedback(FeedbackCue cue, int level, Geometry contour, double duration, double spread)
    {
        Cue(cue, level, duration, (dc, time, brush) =>
        {
            double fade = 1 - time;
            double width = spread * fade;
            PushWorld(dc);
            Stroke(3 + width, .24);
            Stroke(3 + width / 2, .38);
            Stroke(3 + width / 8, .92);
            dc.Pop();

            void Stroke(double pixels, double opacity)
            {
                var pen = new Pen(brush, pixels / Zoom)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                dc.PushOpacity(opacity * fade);
                dc.DrawGeometry(null, pen, contour);
                dc.Pop();
            }
        });
    }

    private void MarqueeFeedback(V2 start, V2 end) => Cue(FeedbackCue.Marquee, Plan.ActiveLevel, 180, (dc, time, brush) =>
    {
        dc.PushOpacity(.56 * (1 - time));
        dc.DrawRectangle(null, new Pen(brush, 1.5), new Rect(Screen(start), Screen(end)));
        dc.Pop();
    });

    internal MoveFeedback? CaptureMoveFeedback()
    {
        if (!CanCue(FeedbackCue.Move)) return null;
        var vertices = SelectionPoints();
        var outline = new StreamGeometry { FillRule = FillRule.EvenOdd };
        var used = new HashSet<int>();
        using (var dc = outline.Open())
        {
            foreach (var edge in Plan.Edges.Where(e => FocusIncludesEdge(e) && IsLevelVisible(VertexAt(e.A).Level) && vertices.Contains(e.A) && vertices.Contains(e.B)))
            {
                dc.BeginFigure(WorldPoint(VertexAt(edge.A).P), false, false);
                dc.LineTo(WorldPoint(VertexAt(edge.B).P), true, false);
                used.Add(edge.A); used.Add(edge.B);
            }
            foreach (var note in Plan.Annotations.Where(n => FocusIncludesAnnotation(n) && IsLevelVisible(n.Level) && SelectedAnnotations.Contains(n.Id)))
            {
                var bounds = note.WorldBounds();
                dc.BeginFigure(bounds.TopLeft, false, true);
                dc.LineTo(bounds.TopRight, true, false);
                dc.LineTo(bounds.BottomRight, true, false);
                dc.LineTo(bounds.BottomLeft, true, false);
            }
        }
        outline.Freeze();
        return new(outline, vertices.Except(used).Select(VertexAt).Where(v => FocusIncludesVertex(v) && IsLevelVisible(v.Level)).Select(v => v.P).ToArray(), Plan.ActiveLevel);
    }

    internal void FinishMoveFeedback(MoveFeedback? origin)
    {
        if (origin == null) return;
        Cue(FeedbackCue.Move, origin.Level, 200, (dc, time, brush) =>
        {
            double opacity = .38 * (1 - time);
            DrawOutline(dc, origin.Outline, brush, opacity, 1.3);
            dc.PushOpacity(opacity);
            foreach (var point in origin.Points) dc.DrawEllipse(null, new Pen(brush, 1), Screen(point), 3, 3);
            dc.Pop();
        });
    }

    private Face[]? SnapshotFeedbackFaces() => CanCue(FeedbackCue.Rectangle)
        ? facesByLevel.GetValueOrDefault(Plan.ActiveLevel, Array.Empty<Face>()) : null;

    private IEnumerable<Face> NewFeedbackFaces(Face[] previous)
    {
        var keys = previous.Select(f => f.Key).ToHashSet();
        return facesByLevel.GetValueOrDefault(Plan.ActiveLevel, Array.Empty<Face>()).Where(f => !keys.Contains(f.Key) && FocusIncludesFace(f));
    }

    private Action? CaptureExtrusionFeedback(int[] selected, V2? click)
    {
        if (!CanCue(FeedbackCue.Extrusion)) return null;
        var previousFaces = renderFaces;
        var levels = selected.Select(id => renderEdges[id]).GroupBy(edge => VertexAt(edge.A).Level)
            .Where(group => IsLevelVisible(group.Key)).Select(group =>
            {
                var edge = group.First();
                var origin = click ?? (VertexAt(edge.A).P + VertexAt(edge.B).P) * .5;
                var regions = facesByLevel.GetValueOrDefault(group.Key, Array.Empty<Face>())
                    .Select(face => faceGeometry[face.Key]).ToLookup(region => region.Bounds, region => region.Geometry);
                return (Level: group.Key, Origin: origin, Regions: regions);
            }).ToArray();

        return () =>
        {
            foreach (var (level, origin, regions) in levels)
            {
                var closed = facesByLevel.GetValueOrDefault(level, Array.Empty<Face>())
                    .Where(face => !previousFaces.ContainsKey(face.Key) && FocusIncludesFace(face));
                // Normalization can split an existing boundary without enclosing a new region.
                var shape = CombineFeedbackFaces(closed.Where(face =>
                {
                    var (geometry, bounds) = faceGeometry[face.Key];
                    return !regions[bounds].Any(old => Geometry.Combine(old, geometry, GeometryCombineMode.Xor,
                        null, 1e-6, ToleranceType.Absolute).GetArea() < 1e-6);
                }));
                if (!shape.Bounds.IsEmpty) ConstructionWave(FeedbackCue.Extrusion, shape, origin, null, level);
            }
        };
    }

    private bool ClosedDrawingFeedback(Face[]? previous, V2 start, V2 end)
    {
        if (previous == null || facesByLevel.GetValueOrDefault(Plan.ActiveLevel, Array.Empty<Face>()).Length <= previous.Length) return false;
        var geometry = CombineFeedbackFaces(NewFeedbackFaces(previous).Where(f => CachedFaceEdges(f).Select(id => renderEdges[id]).Any(edge =>
            Plan.Distance(VertexAt(edge.A).P, start, end) < 1e-5 && Plan.Distance(VertexAt(edge.B).P, start, end) < 1e-5)));
        if (geometry.Bounds.IsEmpty) return false;
        ConstructionWave(FeedbackCue.DrawClosed, geometry, end, null, Plan.ActiveLevel);
        return true;
    }

    private void CompletedShapeFeedback(ShapeKind shape, Face[]? previous, List<V2> contour, V2 start)
    {
        if (previous == null) return;
        var boundary = ContourRegions.Geometry(new[] { contour });
        var regions = CombineFeedbackFaces(NewFeedbackFaces(previous).Where(f => faceGeometry[f.Key].Bounds.IntersectsWith(boundary.Bounds)));
        if (regions.Bounds.IsEmpty) return;
        var geometry = Geometry.Combine(regions, boundary, GeometryCombineMode.Intersect, null, 1e-6, ToleranceType.Absolute);
        geometry.Freeze();
        if (geometry.Bounds.IsEmpty) return;
        ConstructionWave(shape == ShapeKind.Circle ? FeedbackCue.Circle : FeedbackCue.Rectangle, geometry, start,
            shape == ShapeKind.Rectangle ? contour[2] : null, Plan.ActiveLevel);
    }

    private Geometry CombineFeedbackFaces(IEnumerable<Face> source)
    {
        var result = new System.Windows.Media.GeometryGroup { FillRule = FillRule.EvenOdd };
        foreach (var face in source) result.Children.Add(faceGeometry[face.Key].Geometry);
        result.Freeze();
        return result;
    }

    private void ConstructionWave(FeedbackCue cue, Geometry shape, V2 start, V2? end, int level)
    {
        var origin = WorldPoint(start);
        var bounds = shape.Bounds;
        double reach = Math.Max((bounds.TopLeft - origin).Length, (bounds.BottomRight - origin).Length);
        reach = Math.Max(reach, Math.Max((bounds.TopRight - origin).Length, (bounds.BottomLeft - origin).Length));
        if (reach < 1e-7) return;
        Cue(cue, level, 320, (dc, time, brush) =>
        {
            var color = ((SolidColorBrush)brush).Color;
            double front = time * 1.18;
            var pulse = new GradientStopCollection
            {
                new(Colors.Transparent, front - .34), new(color, front - .14), new(Colors.Transparent, front + .11)
            };
            GradientBrush band = end is { } destination
                ? new LinearGradientBrush(pulse, origin, WorldPoint(destination))
                : new RadialGradientBrush(pulse) { Center = origin, GradientOrigin = origin, RadiusX = reach, RadiusY = reach };
            band.MappingMode = BrushMappingMode.Absolute;
            double fade = 1 - time;
            PushWorld(dc);
            dc.PushOpacity(.24 * fade);
            dc.DrawGeometry(band, null, shape);
            dc.Pop();
            dc.PushOpacity(.75 * fade);
            dc.DrawGeometry(null, new Pen(band, 2 / Zoom), shape);
            dc.Pop(); dc.Pop();
        });
    }

    private void StairFeedback(Face[]? previous, IReadOnlyList<StairLine> source, GeometryGroup group)
    {
        if (previous == null) return;
        var edgeIds = group.EdgeIds.ToHashSet();
        var footprint = CombineFeedbackFaces(NewFeedbackFaces(previous).Where(f => CachedFaceEdges(f).IsSubsetOf(edgeIds)));
        // The geometry builder emits the tread lines in ascent order, including at turns.
        var treads = source.Where(line => line.Style == LineStyle.LongDash).ToArray();
        var borders = new StreamGeometry();
        using (var path = borders.Open())
            foreach (var line in source.Where(line => line.Style == LineStyle.Solid))
            {
                path.BeginFigure(WorldPoint(line.A), false, false);
                path.LineTo(WorldPoint(line.B), true, false);
            }
        borders.Freeze();
        Cue(FeedbackCue.Stairs, Plan.ActiveLevel, 320, (dc, time, brush) =>
        {
            PushWorld(dc);
            dc.PushOpacity(.09 * (1 - time));
            dc.DrawGeometry(brush, null, footprint);
            dc.DrawGeometry(null, new Pen(brush, 1.3 / Zoom), borders);
            dc.Pop();
            double front = time * (treads.Length + 3) - 1;
            int first = Math.Max(0, (int)Math.Floor(front) - 3);
            int last = Math.Min(treads.Length - 1, (int)Math.Ceiling(front));
            var pen = new Pen(brush, 2 / Zoom);
            for (int i = first; i <= last; i++)
            {
                dc.PushOpacity(.8 * (1 - time) * Math.Max(0, 1 - Math.Abs(front - i) / 3));
                dc.DrawLine(pen, WorldPoint(treads[i].A), WorldPoint(treads[i].B));
                dc.Pop();
            }
            dc.Pop();
        });
    }

    private static Point WorldPoint(V2 point) => new(point.X, -point.Y);

    private void PushWorld(DrawingContext dc) => dc.PushTransform(new MatrixTransform(Zoom, 0, 0, Zoom,
        ActualWidth / 2 - Center.X * Zoom, ActualHeight / 2 + Center.Y * Zoom));

    private void DrawOutline(DrawingContext dc, Geometry shape, Brush brush, double opacity, double width)
    {
        PushWorld(dc);
        dc.PushOpacity(opacity);
        dc.DrawGeometry(null, new Pen(brush, width / Zoom), shape);
        dc.Pop(); dc.Pop();
    }
}

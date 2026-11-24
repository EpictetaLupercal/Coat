// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Media;

namespace Coat;

internal sealed class BooleanRegions
{
    private const double Tolerance = .000001;
    private readonly Face target;
    private readonly Face? operand;
    private readonly IReadOnlyList<Face> faces;
    private readonly ContourRegions regions;
    private readonly List<List<V2>> outlines;
    internal Geometry Result { get; }
    internal Geometry A { get; }
    internal Geometry B { get; }
    internal bool IsEmpty => outlines.Count == 0;

    internal BooleanRegions(Face target, Face? operand, List<V2>? cutter, GeometryCombineMode operation,
        IReadOnlyList<Face> faces, IReadOnlyDictionary<int, V2> positions)
    {
        this.target = target;
        this.operand = operand;
        this.faces = faces;
        regions = new(faces, positions);
        A = ContourRegions.Geometry(regions.Loops(target.Key));
        B = operand != null ? ContourRegions.Geometry(regions.Loops(operand.Key)) : ContourRegions.Geometry(new[] { cutter! });
        Result = Geometry.Combine(A, B, operation, null, Tolerance, ToleranceType.Absolute);
        Result.Freeze();
        outlines = ReadOutlines(Result);
    }

    internal bool Apply(Plan plan)
    {
        var consumed = new HashSet<string> { target.Key };
        if (operand != null) consumed.Add(operand.Key);
        if (outlines.SelectMany(points => points).Any(p => !SelectionTransform.Valid(p))) return false;
        var byEndpoints = plan.Edges.ToLookup(e => (Math.Min(e.A, e.B), Math.Max(e.A, e.B)), e => e.Id);
        var boundaries = faces.ToDictionary(f => f.Key, f => Enumerable.Range(0, f.Vertices.Count).SelectMany(i =>
        {
            int a = f.Vertices[i], b = f.Vertices[(i + 1) % f.Vertices.Count];
            return byEndpoints[(Math.Min(a, b), Math.Max(a, b))];
        }).ToArray());
        IEnumerable<int> Boundary(Face face) => boundaries[face.Key].Concat(regions.Holes[face.Key].SelectMany(hole => boundaries[hole]));
        var removed = faces.Where(f => consumed.Contains(f.Key)).SelectMany(Boundary).ToHashSet();
        // Boundary edges shared with other rooms remain available to those rooms.
        removed.ExceptWith(faces.Where(f => !consumed.Contains(f.Key)).SelectMany(Boundary));
        bool painted = plan.Fills.TryGetValue(target.Key, out var color);
        var pattern = plan.FillPatterns.GetValueOrDefault(target.Key);
        var gradient = plan.Gradients.GetValueOrDefault(target.Key);
        foreach (var key in consumed) { plan.Fills.Remove(key); plan.FillPatterns.Remove(key); plan.Gradients.Remove(key); }
        plan.Edges.RemoveAll(edge => removed.Contains(edge.Id));
        foreach (var outline in outlines)
        {
            var ids = outline.Select(p => plan.Point(p, target.Level)).ToArray();
            for (int i = 0; i < ids.Length; i++) plan.AddEdge(ids[i], ids[(i + 1) % ids.Length]);
        }
        plan.Normalize();
        if (plan.Vertices.Count > 20000 || plan.Edges.Count > 30000) return false;

        if (!painted || outlines.Count == 0) return true;
        var updated = plan.Faces().Where(f => f.Level == target.Level).ToArray();
        var updatedRegions = new ContourRegions(updated, plan.Vertices.ToDictionary(v => v.Id, v => v.P));
        var retained = faces.Where(f => f.Level == target.Level && !consumed.Contains(f.Key)).Select(f => f.Key).ToHashSet();
        foreach (var face in updated.Where(f => !retained.Contains(f.Key) && !plan.Fills.ContainsKey(f.Key)))
        {
            var shape = ContourRegions.Geometry(updatedRegions.Loops(face.Key));
            if (!Result.Bounds.IntersectsWith(shape.Bounds)) continue;
            double area = shape.GetArea(Tolerance, ToleranceType.Absolute);
            double shared = Geometry.Combine(shape, Result, GeometryCombineMode.Intersect, null, Tolerance, ToleranceType.Absolute).GetArea(Tolerance, ToleranceType.Absolute);
            if (shared <= Tolerance || area - shared > Math.Max(Tolerance, area * 1e-8)) continue;
            plan.Fills[face.Key] = color!;
            if (pattern != FillPattern.Solid) plan.FillPatterns[face.Key] = pattern;
            if (gradient != null) plan.Gradients[face.Key] = gradient;
        }
        return true;
    }

    private static List<List<V2>> ReadOutlines(Geometry result)
    {
        var outlines = new List<List<V2>>();
        foreach (var figure in result.GetFlattenedPathGeometry(Tolerance, ToleranceType.Absolute).Figures)
        {
            if (!figure.IsFilled || !figure.IsClosed) continue;
            var points = new List<V2>();
            Add(figure.StartPoint);
            foreach (var segment in figure.Segments)
            {
                if (segment is LineSegment line) Add(line.Point);
                else if (segment is PolyLineSegment polyline) foreach (var point in polyline.Points) Add(point);
            }
            if (points.Count > 1 && (points[0] - points[^1]).Length <= Tolerance) points.RemoveAt(points.Count - 1);
            if (points.Count >= 3) outlines.Add(points);

            void Add(Point p)
            {
                var point = new V2(p.X, -p.Y);
                if (points.Count == 0 || (point - points[^1]).Length > Tolerance) points.Add(point);
            }
        }
        return outlines;
    }
}

// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Media;
namespace Coat;
public partial class Plan
{
    private record DissolveFill(string Key, int Level, Geometry Shape, string Color, FillPattern Pattern, GradientFill? Gradient);
    private Geometry FaceShape(Face face, IReadOnlyDictionary<int, V2> vertices)
    {
        var geometry = new StreamGeometry(); using (var c = geometry.Open())
        { var points = face.Vertices.Select(id => vertices[id]).Select(p => new Point(p.X, p.Y)).ToArray(); c.BeginFigure(points[0], true, true); c.PolyLineTo(points.Skip(1).ToArray(), true, false); }
        geometry.Freeze(); return geometry;
    }
    private List<DissolveFill> CaptureDissolveFills()
    {
        var vertices = Vertices.ToDictionary(v => v.Id, v => v.P);
        return Faces().Where(f => Fills.ContainsKey(f.Key)).Select(f => new DissolveFill(f.Key, f.Level, FaceShape(f, vertices), Fills[f.Key], FillPatterns.GetValueOrDefault(f.Key), Gradients.GetValueOrDefault(f.Key))).ToList();
    }
    private void RestoreDissolveFills(List<DissolveFill> old)
    {
        Fills.Clear(); FillPatterns.Clear(); Gradients.Clear();
        var byKey = old.ToDictionary(f => f.Key); var vertices = Vertices.ToDictionary(v => v.Id, v => v.P);
        foreach (var face in Faces())
        {
            var source = byKey.GetValueOrDefault(face.Key);
            if (source == null)
            {
                var shape = FaceShape(face, vertices);
                // Only affected faces require overlap computation. A merged face inherits the
                // filled source with greatest overlap, with a stable key tie-breaker.
                source = old.Where(f => f.Level == face.Level && f.Shape.Bounds.IntersectsWith(shape.Bounds))
                    .Select(f => (Fill: f, Area: Geometry.Combine(shape, f.Shape, GeometryCombineMode.Intersect, null).GetArea()))
                    .Where(p => p.Area > 1e-7).OrderByDescending(p => p.Area).ThenBy(p => p.Fill.Key, StringComparer.Ordinal).Select(p => p.Fill).FirstOrDefault();
            }
            if (source == null) continue;
            Fills[face.Key] = source.Color; FillPatterns[face.Key] = source.Pattern; if (source.Gradient != null) Gradients[face.Key] = source.Gradient;
        }
        Normalize();
    }
    public int DissolveVertices(IEnumerable<int> ids)
    {
        var fills = CaptureDissolveFills(); int changed = 0;
        foreach (int id in ids.Distinct().Order())
        {
            var vertex = Vertices.FirstOrDefault(v => v.Id == id); if (vertex == null) continue;
            var incident = Edges.Where(e => e.A == id || e.B == id).ToList(); if (incident.Count != 2) continue;
            int a = incident[0].A == id ? incident[0].B : incident[0].A, b = incident[1].A == id ? incident[1].B : incident[1].A;
            if (a == b || Edges.Any(e => e.A == a && e.B == b || e.A == b && e.B == a)) continue;
            var pa = Get(a).P; var pb = Get(b).P;
            // Refuse a replacement that intersects unrelated geometry or collapses a junction.
            if (Vertices.Any(v => v.Level == vertex.Level && v.Id != id && v.Id != a && v.Id != b && Distance(v.P, pa, pb) < 1e-6)) continue;
            bool crosses = Edges.Except(incident).Where(e => LevelOf(e) == vertex.Level && e.A != a && e.B != a && e.A != b && e.B != b).Any(e =>
            {
                var c = Get(e.A).P; var r = pb - pa; var d = Get(e.B).P - c; double cross = V2.Cross(r, d); if (Math.Abs(cross) < 1e-9) return false;
                double t = V2.Cross(c - pa, d) / cross, u = V2.Cross(c - pa, r) / cross; return t > 0 && t < 1 && u >= 0 && u <= 1;
            });
            if (crosses) continue;
            var first = incident[0]; var last = incident[1];
            var merged = new Edge { Id = first.Id, A = a, B = b, Color = first.Color, Style = first.Style, ArrowStyle = first.ArrowStyle, ArrowSize = first.ArrowSize,
                ArrowStart = first.A == a ? first.ArrowStart : first.ArrowEnd, ArrowEnd = last.B == b ? last.ArrowEnd : last.ArrowStart };
            Edges.RemoveAll(e => incident.Contains(e)); Edges.Add(merged); Vertices.Remove(vertex);
            foreach (var group in Groups)
            {
                if (group.EdgeIds.Any(e => incident.Any(i => i.Id == e)) || group.VertexIds.Contains(id)) group.EdgeIds.Add(merged.Id);
                group.EdgeIds.RemoveAll(e => e == last.Id); group.VertexIds.Remove(id);
            }
            changed++;
        }
        if (changed > 0) RestoreDissolveFills(fills); return changed;
    }
    public int DissolveEdges(IEnumerable<int> ids)
    {
        var requested = ids.ToHashSet(); var faces = Faces();
        var edges = Edges.ToDictionary(e => (Math.Min(e.A, e.B), Math.Max(e.A, e.B)), e => e.Id);
        var usage = new Dictionary<int, int>();
        foreach (var face in faces)
        {
            var boundary = new HashSet<int>();
            for (int i = 0; i < face.Vertices.Count; i++)
            { int a = face.Vertices[i], b = face.Vertices[(i + 1) % face.Vertices.Count]; if (edges.TryGetValue((Math.Min(a, b), Math.Max(a, b)), out int id)) boundary.Add(id); }
            foreach (int id in boundary) usage[id] = usage.GetValueOrDefault(id) + 1;
        }
        var removable = Edges.Where(e => requested.Contains(e.Id) && usage.GetValueOrDefault(e.Id) == 2).Select(e => e.Id).ToHashSet();
        if (removable.Count == 0) return 0;
        var fills = CaptureDissolveFills(); Edges.RemoveAll(e => removable.Contains(e.Id));
        RestoreDissolveFills(fills); return removable.Count;
    }
}

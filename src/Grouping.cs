// Coat — Epicteta Lupercal
namespace Coat;

public class GeometryGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = L.T("Groupe");
    public List<int> VertexIds { get; set; } = new();
    public List<int> EdgeIds { get; set; } = new();
}

public partial class Plan
{
    public List<GeometryGroup> Groups { get; set; } = new();
    public void RemapGroups(IReadOnlyDictionary<int, List<int>> edgeMap, IReadOnlyDictionary<int, int> vertexMap)
    {
        var vertices = Vertices.Select(v => v.Id).ToHashSet(); var edges = Edges.Select(e => e.Id).ToHashSet();
        foreach (var group in Groups)
        {
            group.VertexIds = group.VertexIds.Select(id => vertexMap.TryGetValue(id, out var next) ? next : id).Where(vertices.Contains).Distinct().ToList();
            group.EdgeIds = group.EdgeIds.SelectMany(id => edgeMap.TryGetValue(id, out var next) ? next : new List<int> { id }).Where(edges.Contains).Distinct().ToList();
        }
        Groups.RemoveAll(g => g.VertexIds.Count + g.EdgeIds.Count == 0);
    }
    public HashSet<int> GroupPoints(GeometryGroup group) => group.VertexIds.Concat(Edges.Where(e => group.EdgeIds.Contains(e.Id)).SelectMany(e => new[] { e.A, e.B })).ToHashSet();
    public List<GeometryGroup> MatchingGroups(IEnumerable<int> vertices, IEnumerable<int> edges)
    {
        var v = vertices.ToHashSet(); var e = edges.ToHashSet();
        return Groups.Where(g => GroupPoints(g).Overlaps(v) || g.EdgeIds.Any(e.Contains)).ToList();
    }
    public GeometryGroup? GroupSelection(IEnumerable<int> vertices, IEnumerable<int> edges)
    {
        var v = vertices.ToHashSet(); var e = edges.ToHashSet();
        if (v.Count + e.Count == 0) return null;
        var merged = MatchingGroups(v, e);
        foreach (var old in merged) { v.UnionWith(old.VertexIds); e.UnionWith(old.EdgeIds); }
        if (merged.Count == 1 && v.SetEquals(merged[0].VertexIds) && e.SetEquals(merged[0].EdgeIds)) return merged[0];
        Groups.RemoveAll(g => merged.Contains(g));
        var group = new GeometryGroup { Id = NextId++, Name = L.T("Groupe ") + (Groups.Count + 1), VertexIds = v.ToList(), EdgeIds = e.ToList() }; Groups.Add(group); return group;
    }
    public void CopyGroups(IReadOnlyDictionary<int, int> vertexMap, IReadOnlyDictionary<int, int> edgeMap)
    {
        foreach (var group in Groups.ToList())
        {
            if (!group.VertexIds.All(vertexMap.ContainsKey) || !group.EdgeIds.All(edgeMap.ContainsKey)) continue;
            Groups.Add(new GeometryGroup { Id = NextId++, Name = group.Name + L.T(" (copie)"), VertexIds = group.VertexIds.Select(id => vertexMap[id]).ToList(), EdgeIds = group.EdgeIds.Select(id => edgeMap[id]).ToList() });
        }
    }
}

public enum ShapeKind { Rectangle, Circle, Stairs }
public static class Shapes
{
    public static List<V2> Contour(ShapeKind kind, V2 start, V2 end, int count, int grid)
    {
        start = start.Snap(grid); end = end.Snap(grid);
        if (kind == ShapeKind.Rectangle)
        {
            if (Math.Abs(end.X - start.X) < 1e-6 || Math.Abs(end.Y - start.Y) < 1e-6) return new();
            return new() { start, new(end.X, start.Y), end, new(start.X, end.Y) };
        }
        if (kind != ShapeKind.Circle) throw new ArgumentOutOfRangeException(nameof(kind));
        if (count < 3 || count > 128) throw new ArgumentOutOfRangeException(nameof(count));
        double radius = Math.Round((end - start).Length / grid, MidpointRounding.AwayFromZero) * grid;
        if (radius <= 0) return new();
        // Snap the centre and radius; keep the polygon regular instead of distorting each vertex to the grid.
        return Enumerable.Range(0, count).Select(i => start + new V2(Math.Cos(2 * Math.PI * i / count), Math.Sin(2 * Math.PI * i / count)) * radius).ToList();
    }
}

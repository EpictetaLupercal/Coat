// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Media;
namespace Coat;
// Face topology stays unchanged in .coat files. This derived view adds immediate
// enclosed contours as holes; each nested face keeps its own independent fill.
internal sealed class ContourRegions
{
    internal Dictionary<string, List<V2>> Points { get; }
    internal Dictionary<string, List<string>> Holes { get; }
    internal ContourRegions(IReadOnlyList<Face> faces, IReadOnlyDictionary<int,V2> vertices)
    {
        Points = faces.ToDictionary(f => f.Key, f => f.Vertices.Select(id => vertices[id]).ToList());
        Holes = faces.ToDictionary(f => f.Key, _ => new List<string>());
        var shapes = Points.ToDictionary(p => p.Key, p => Geometry(new[] { p.Value }));
        var parent = new Dictionary<string, Face>();
        foreach (var level in faces.GroupBy(f => f.Level))
        {
            var ordered = level.OrderBy(f => shapes[f.Key].Bounds.Left).ToArray();
            foreach (var outer in level)
            {
                var bounds = shapes[outer.Key].Bounds; int lo = 0, hi = ordered.Length;
                while (lo < hi) { int mid = (lo + hi) / 2; if (shapes[ordered[mid].Key].Bounds.Left < bounds.Left) lo = mid + 1; else hi = mid; }
                for (int i = lo; i < ordered.Length && shapes[ordered[i].Key].Bounds.Left <= bounds.Right; i++)
                {
                    var inner = ordered[i]; if (inner.Area >= outer.Area || !bounds.Contains(shapes[inner.Key].Bounds)) continue;
                    if (parent.TryGetValue(inner.Key, out var previous) && previous.Area <= outer.Area) continue;
                    if (shapes[outer.Key].FillContainsWithDetail(shapes[inner.Key]) == IntersectionDetail.FullyContains) parent[inner.Key] = outer;
                }
            }
        }
        foreach (var pair in parent) Holes[pair.Value.Key].Add(pair.Key);
    }
    internal IEnumerable<List<V2>> Loops(string key) => new[] { Points[key] }.Concat(Holes[key].Select(h => Points[h]));
    internal static StreamGeometry Geometry(IEnumerable<List<V2>> loops)
    {
        var shape = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var path = shape.Open()) foreach (var loop in loops)
        { path.BeginFigure(new Point(loop[0].X, -loop[0].Y), true, true); path.PolyLineTo(loop.Skip(1).Select(p => new Point(p.X, -p.Y)).ToArray(), true, false); }
        shape.Freeze(); return shape;
    }
}
internal static class AppVersion
{
    internal static string Number => typeof(AppVersion).Assembly.GetName().Version!.ToString(3);
    internal static string Title => "Coat — v" + Number;
}

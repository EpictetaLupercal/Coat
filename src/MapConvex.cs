// Coat — Epicteta Lupercal
namespace Coat;
public static partial class MapExporter
{
    internal static double Area(IReadOnlyList<V2> p)
    { if (p.Count < 3) return 0; double area = 0; var origin = p[0]; for (int i = 1; i + 1 < p.Count; i++) area += V2.Cross(p[i] - origin, p[i + 1] - origin); return area / 2; }
    internal static List<V2> Clean(List<V2> points)
    {
        bool changed = true;
        while (changed && points.Count >= 3)
        {
            changed = false;
            for (int i = 0; i < points.Count; i++)
            {
                var a = points[(i + points.Count - 1) % points.Count]; var b = points[i]; var c = points[(i + 1) % points.Count];
                if ((b - a).Length < 1e-8 || Plan.Distance(b, a, c) < 1e-9)
                { points.RemoveAt(i); changed = true; break; }
            }
        }
        if (Area(points) < 0) points.Reverse(); return points;
    }
    internal static bool Convex(IReadOnlyList<V2> p)
    {
        if (p.Count < 3 || Area(p) <= 1e-8 || p.Distinct().Count() != p.Count) return false;
        for (int i = 0; i < p.Count; i++)
            if (V2.Cross(p[(i + 1) % p.Count] - p[i], p[(i + 2) % p.Count] - p[(i + 1) % p.Count]) <= 0) return false;
        return true;
    }
    private static List<V2> Hull(IEnumerable<V2> source)
    {
        var points = source.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        var lower = new List<V2>(); var upper = new List<V2>();
        void Add(List<V2> side, V2 p)
        { while (side.Count >= 2 && V2.Cross(side[^1] - side[^2], p - side[^1]) <= 0) side.RemoveAt(side.Count - 1); side.Add(p); }
        foreach (var p in points) Add(lower, p); foreach (var p in points.AsEnumerable().Reverse()) Add(upper, p);
        lower.RemoveAt(lower.Count - 1); upper.RemoveAt(upper.Count - 1); lower.AddRange(upper); return Clean(lower);
    }
    private static bool SharedBoundary(List<V2> a, List<V2> b)
    {
        for (int i = 0; i < a.Count; i++) for (int j = 0; j < b.Count; j++)
        {
            var x = a[i]; var d = a[(i + 1) % a.Count] - x; double length = d.Length;
            var u = b[j]; var v = b[(j + 1) % b.Count];
            if (Math.Abs(V2.Cross(d, u - x)) > length * 1e-7 || Math.Abs(V2.Cross(d, v - x)) > length * 1e-7) continue;
            double p = V2.Dot(u - x, d) / length, q = V2.Dot(v - x, d) / length;
            if (Math.Min(length, Math.Max(p, q)) - Math.Max(0, Math.Min(p, q)) > 1e-7) return true;
        }
        return false;
    }
    private static List<MapBrush> MergeConvex(List<MapBrush> source)
    {
        var result = new List<MapBrush>();
        foreach (var group in source.GroupBy(b => (b.Level, b.Bottom, b.Top, b.Surface)))
        {
            var pieces = group.ToList(); bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < pieces.Count && !changed; i++) for (int j = i + 1; j < pieces.Count; j++)
                {
                    var a = pieces[i].Outline; var b = pieces[j].Outline;
                    if (a.Max(p => p.X) < b.Min(p => p.X) - 1e-7 || b.Max(p => p.X) < a.Min(p => p.X) - 1e-7 || a.Max(p => p.Y) < b.Min(p => p.Y) - 1e-7 || b.Max(p => p.Y) < a.Min(p => p.Y) - 1e-7) continue;
                    if (!SharedBoundary(a, b)) continue;
                    var hull = Hull(a.Concat(b)); double area = Area(a) + Area(b);
                    // Equality of area is essential: a convex hull alone would fill concavities or holes.
                    if (!Convex(hull) || Math.Abs(Area(hull) - area) > Math.Max(1e-7, area * 1e-11)) continue;
                    pieces[i] = pieces[i] with { Outline = hull }; pieces.RemoveAt(j); changed = true; break;
                }
            }
            result.AddRange(pieces);
        }
        return result;
    }
}

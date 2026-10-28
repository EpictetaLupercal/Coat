// Coat — Epicteta Lupercal
using System.IO;
using System.Text.Json;

namespace Coat;

public record struct V2(double X, double Y)
{
    public static V2 operator +(V2 a, V2 b) => new(a.X + b.X, a.Y + b.Y);
    public static V2 operator -(V2 a, V2 b) => new(a.X - b.X, a.Y - b.Y);
    public static V2 operator *(V2 a, double f) => new(a.X * f, a.Y * f);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public static double Cross(V2 a, V2 b) => a.X * b.Y - a.Y * b.X;
    public static double Dot(V2 a, V2 b) => a.X * b.X + a.Y * b.Y;
    public V2 Snap(double step) => new(Math.Round(X / step, MidpointRounding.AwayFromZero) * step, Math.Round(Y / step, MidpointRounding.AwayFromZero) * step);
}
public class Vertex
{
    public int Id { get; set; }
    public bool KeepPoint { get; set; }
    public int Level { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public V2 P { get => new(X, Y); set { X = value.X; Y = value.Y; } }
}
public class Edge
{
    public int Id { get; set; }
    public int A { get; set; }
    public int B { get; set; }
    public string Color { get; set; } = "#CCD5DF";
    public LineStyle Style { get; set; }
}
public class ReferenceImage
{
    public string Data { get; set; } = "";
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 1024;
    public double Height { get; set; } = 768;
    public double Opacity { get; set; } = 0.45;
    public bool Visible { get; set; } = true;
}
public record Face(List<int> Vertices, string Key, double Area, int Level);
public record LevelTransfer(HashSet<int> Points, HashSet<int> Edges, HashSet<string> Faces);
public partial class Plan
{
    public int Version { get; set; } = 6;
    public List<int> Levels { get; set; } = new() { 0 };
    public int ActiveLevel { get; set; }
    public List<Vertex> Vertices { get; set; } = new();
    public List<Edge> Edges { get; set; } = new();
    public Dictionary<string, string> Fills { get; set; } = new();
    public ReferenceImage? Background { get; set; }
    public int Grid { get; set; } = 32;
    public int NextId { get; set; } = 1;
    public double ViewX { get; set; }
    public double ViewY { get; set; }
    public double Zoom { get; set; } = 1;
    public Vertex Get(int id) => Vertices.First(v => v.Id == id);
    public int LevelOf(Edge edge) => Get(edge.A).Level;
    public double LevelOpacity(int level) => level > ActiveLevel ? 0 : level == ActiveLevel ? 1 : .12 + .48 / (ActiveLevel - level);
    public double FillOpacity(int level) => level == ActiveLevel ? .88 : .65;
    public void EnsureLevel(int level)
    {
        if (level < -999 || level > 999) throw new ArgumentOutOfRangeException(nameof(level), L.T("Le niveau doit être compris entre -999 et 999."));
        if (!Levels.Contains(level))
        {
            // New floors inherit the nearest floor's elevation with the default 192-unit spacing.
            if (Levels.Count > 0)
            {
                int neighbour = Levels.OrderBy(l => Math.Abs(l - level)).ThenBy(l => l).First();
                double z = LevelZ(neighbour) + (level - neighbour) * 192.0;
                if (z != level * 192.0) LevelHeights[level] = z;
            }
            Levels.Add(level); Levels.Sort();
        }
    }
    public int Point(V2 p, int? level = null)
    {
        int target = level ?? ActiveLevel; EnsureLevel(target);
        var existing = Vertices.FirstOrDefault(v => v.Level == target && (v.P - p).Length < 0.00001);
        if (existing != null) return existing.Id;
        var v = new Vertex { Id = NextId++, P = p, Level = target }; Vertices.Add(v); return v.Id;
    }
    public int AddEdge(int a, int b, string color = "#CCD5DF")
    {
        if (a == b) return -1;
        if (Get(a).Level != Get(b).Level) throw new InvalidOperationException(L.T("Une arête doit appartenir à un seul niveau."));
        var old = Edges.FirstOrDefault(e => (e.A == a && e.B == b) || (e.A == b && e.B == a));
        if (old != null) return old.Id;
        var e = new Edge { Id = NextId++, A = a, B = b, Color = color }; Edges.Add(e); return e.Id;
    }
    public void Segment(V2 a, V2 b, string color = "#CCD5DF")
    {
        if ((a - b).Length < 0.00001) return;
        AddEdge(Point(a), Point(b), color); Normalize();
    }
    public void Polygon(IReadOnlyList<V2> points)
    {
        if (points.Count < 3) return;
        var ids = points.Select(p => Point(p)).ToList();
        for (int i = 0; i < ids.Count; i++) AddEdge(ids[i], ids[(i + 1) % ids.Count]);
        Normalize();
    }
    public void DeleteLevel(int level)
    {
        if (!Levels.Contains(level)) return;
        Edges.RemoveAll(e => LevelOf(e) == level); Vertices.RemoveAll(v => v.Level == level);
        Annotations.RemoveAll(a => a.Level == level);
        Levels.Remove(level); LevelHeights.Remove(level);
        if (Levels.Count == 0) EnsureLevel(0);
        if (ActiveLevel == level) ActiveLevel = Levels.OrderBy(l => Math.Abs((long)l - level)).ThenBy(l => l).First();
        Normalize();
    }
    public static double Distance(V2 p, V2 a, V2 b)
    {
        var d = b - a; var len2 = V2.Dot(d, d);
        if (len2 < 1e-12) return (p - a).Length;
        var t = Math.Clamp(V2.Dot(p - a, d) / len2, 0, 1);
        return (p - (a + d * t)).Length;
    }
    public static bool Inside(V2 p, IReadOnlyList<V2> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if (Distance(p, a, b) < 1e-7) return true;
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }
    // Split crossings, T junctions and overlapping segments; merge coincident vertices.
    public void Normalize()
    {
        // These indexes are local to this operation: coordinates and lists are freely editable elsewhere.
        var verticesById = Vertices.ToDictionary(v => v.Id);
        var oldFaces = (Fills.Count == 0 ? Enumerable.Empty<Face>() : Faces()).Where(f => Fills.ContainsKey(f.Key)).Select(f => (Poly: f.Vertices.Select(id => verticesById[id].P).ToList(), Color: Fills[f.Key], Pattern: FillPatterns.GetValueOrDefault(f.Key), f.Level)).ToList();
        var aliases = new Dictionary<int, int>(); var unique = new List<Vertex>();
        foreach (var v in Vertices)
        {
            var same = unique.FirstOrDefault(x => x.Level == v.Level && (x.P - v.P).Length < 1e-6);
            if (same == null) unique.Add(v); else { aliases[v.Id] = same.Id; same.KeepPoint |= v.KeepPoint; }
        }
        Vertices = unique;
        verticesById = Vertices.ToDictionary(v => v.Id);
        foreach (var e in Edges) { if (aliases.ContainsKey(e.A)) e.A = aliases[e.A]; if (aliases.ContainsKey(e.B)) e.B = aliases[e.B]; }
        Edges.RemoveAll(e => e.A == e.B);
        var segments = Edges.ToList();
        // Resolve endpoints once, then reject disjoint bounding boxes before testing intersections.
        // Padding includes the existing parameter tolerance, including very long segments.
        var geometry = segments.Select(e =>
        {
            var a = verticesById[e.A]; var b = verticesById[e.B]; var d = b.P - a.P;
            double padding = 1e-6 + Math.Max(Math.Abs(d.X), Math.Abs(d.Y)) * 1e-8;
            return (A: a.P, B: b.P, D: d, a.Level, MinX: Math.Min(a.X, b.X) - padding,
                MaxX: Math.Max(a.X, b.X) + padding, MinY: Math.Min(a.Y, b.Y) - padding, MaxY: Math.Max(a.Y, b.Y) + padding);
        }).ToArray();
        var verticesByLevel = Vertices.GroupBy(v => v.Level).ToDictionary(g => g.Key, g => g.ToList());
        for (int i = 0; i < segments.Count; i++)
        for (int j = i + 1; j < segments.Count; j++)
        {
            var first = geometry[i]; var second = geometry[j];
            if (first.Level != second.Level || first.MaxX < second.MinX || second.MaxX < first.MinX || first.MaxY < second.MinY || second.MaxY < first.MinY) continue;
            var a = first.A; var r = first.D; var b = second.A; var s = second.D;
            double cross = V2.Cross(r, s);
            if (Math.Abs(cross) < 1e-9) continue;
            double t = V2.Cross(b - a, s) / cross, u = V2.Cross(b - a, r) / cross;
            if (t >= -1e-8 && t <= 1 + 1e-8 && u >= -1e-8 && u <= 1 + 1e-8)
            {
                var point = a + r * Math.Clamp(t, 0, 1); var candidates = verticesByLevel[first.Level];
                if (!candidates.Any(v => (v.P - point).Length < .00001))
                {
                    var vertex = new Vertex { Id = NextId++, P = point, Level = first.Level };
                    Vertices.Add(vertex); candidates.Add(vertex); verticesById.Add(vertex.Id, vertex);
                }
            }
        }
        Edges.Clear();
        var edgeMap = segments.ToDictionary(e => e.Id, _ => new List<int>());
        var edgesByEndpoints = new Dictionary<(int, int), Edge>();
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            var edge = segments[segmentIndex]; var segment = geometry[segmentIndex];
            var a = segment.A; var b = segment.B;
            var on = verticesByLevel[segment.Level].Where(v => v.X >= segment.MinX && v.X <= segment.MaxX && v.Y >= segment.MinY && v.Y <= segment.MaxY && Distance(v.P, a, b) < 1e-6).OrderBy(v => V2.Dot(v.P - a, b - a)).ToList();
            for (int i = 0; i + 1 < on.Count; i++)
            {
                var key = (Math.Min(on[i].Id, on[i + 1].Id), Math.Max(on[i].Id, on[i + 1].Id));
                if (!edgesByEndpoints.TryGetValue(key, out var existing)) { existing = new Edge { Id = i == 0 ? edge.Id : NextId++, A = on[i].Id, B = on[i + 1].Id, Color = edge.Color, Style = edge.Style }; Edges.Add(existing); edgesByEndpoints.Add(key, existing); }
                edgeMap[edge.Id].Add(existing.Id);
            }
        }
        var used = Edges.SelectMany(e => new[] { e.A, e.B }).ToHashSet();
        Vertices.RemoveAll(v => !v.KeepPoint && !used.Contains(v.Id));
        RemapGroups(edgeMap, aliases);
        var fresh = new Dictionary<string, string>();
        var patterns = new Dictionary<string, FillPattern>();
        foreach (var f in Faces())
        {
            if (Fills.TryGetValue(f.Key, out var c)) { fresh[f.Key] = c; patterns[f.Key] = FillPatterns.GetValueOrDefault(f.Key); }
            else
            {
                var a = verticesById[f.Vertices[0]].P; var b = verticesById[f.Vertices[1]].P; var d = b - a;
                var sample = (a + b) * 0.5 + new V2(-d.Y, d.X) * (0.0001 / Math.Max(d.Length, 1));
                var match = oldFaces.LastOrDefault(x => x.Level == f.Level && Inside(sample, x.Poly));
                if (match.Poly != null) { fresh[f.Key] = match.Color; patterns[f.Key] = match.Pattern; }
            }
        }
        Fills = fresh; FillPatterns = patterns;
    }
    // Walk the left side of each directed half edge. Positive loops are bounded faces.
    public List<Face> Faces()
    {
        var result = new List<Face>(); var adjacency = Vertices.ToDictionary(v => v.Id, _ => new List<int>());
        foreach (var e in Edges)
        {
            if (!adjacency.ContainsKey(e.A) || !adjacency.ContainsKey(e.B)) continue;
            adjacency[e.A].Add(e.B); adjacency[e.B].Add(e.A);
        }
        var points = Vertices.ToDictionary(v => v.Id, v => v.P);
        foreach (var pair in adjacency) pair.Value.Sort((a, b) => Math.Atan2(points[a].Y - points[pair.Key].Y, points[a].X - points[pair.Key].X).CompareTo(Math.Atan2(points[b].Y - points[pair.Key].Y, points[b].X - points[pair.Key].X)));
        var seen = new HashSet<(int, int)>();
        foreach (var e in Edges) foreach (var first in new[] { (e.A, e.B), (e.B, e.A) })
        {
            if (seen.Contains(first)) continue;
            var loop = new List<int>(); var cur = first; bool closed = false;
            for (int guard = 0; guard <= Edges.Count * 2; guard++)
            {
                if (!seen.Add(cur)) { closed = cur == first; break; }
                loop.Add(cur.Item1);
                if (!adjacency.TryGetValue(cur.Item2, out var neighbours) || neighbours.Count == 0) break;
                int back = neighbours.IndexOf(cur.Item1);
                cur = (cur.Item2, neighbours[(back + neighbours.Count - 1) % neighbours.Count]);
            }
            if (!closed || loop.Count < 3) continue;
            double area = 0;
            for (int i = 0; i < loop.Count; i++) area += V2.Cross(points[loop[i]], points[loop[(i + 1) % loop.Count]]);
            if (area > 1e-6) result.Add(new Face(loop, string.Join("-", loop.Order()), area / 2, Get(loop[0]).Level));
        }
        return result;
    }
    public Face? HitFace(V2 p) => Faces().Where(f => f.Level == ActiveLevel && Inside(p, f.Vertices.Select(id => Get(id).P).ToList())).OrderBy(f => f.Area).FirstOrDefault();
    public HashSet<int> Extrude(IEnumerable<int> edgeIds, V2 delta)
    {
        var source = Edges.Where(e => edgeIds.Contains(e.Id)).ToList();
        if (delta.Length < 1e-6) return new();
        var map = source.SelectMany(e => new[] { e.A, e.B }).Distinct().ToDictionary(id => id, id => Point(Get(id).P + delta, Get(id).Level));
        var added = new HashSet<int>();
        foreach (var e in source)
        {
            int top = AddEdge(map[e.A], map[e.B], e.Color), sideA = AddEdge(e.A, map[e.A], e.Color), sideB = AddEdge(e.B, map[e.B], e.Color);
            foreach (var id in new[] { top, sideA, sideB }) if (id >= 0) Edges.First(x => x.Id == id).Style = e.Style;
            added.Add(top);
            foreach (var group in Groups.Where(g => g.EdgeIds.Contains(e.Id))) group.EdgeIds = group.EdgeIds.Concat(new[] { top, sideA, sideB }).Where(id => id >= 0).Distinct().ToList();
        }
        Normalize(); return added.Where(id => Edges.Any(e => e.Id == id)).ToHashSet();
    }
    public HashSet<int> FaceEdges(Face face)
    {
        var result = new HashSet<int>();
        for (int i = 0; i < face.Vertices.Count; i++)
        {
            int a = face.Vertices[i], b = face.Vertices[(i + 1) % face.Vertices.Count];
            foreach (var e in Edges.Where(e => (e.A == a && e.B == b) || (e.A == b && e.B == a))) result.Add(e.Id);
        }
        return result;
    }
    // Detach shared endpoints so transferring an edge never pulls neighbouring geometry to another floor.
    public LevelTransfer TransferToLevel(IEnumerable<int> vertexIds, IEnumerable<int> edgeIds, IEnumerable<string> faceKeys, int target)
    {
        EnsureLevel(target);
        var selectedPoints = vertexIds.ToHashSet(); var selectedEdges = edgeIds.ToHashSet(); var selectedFaces = faceKeys.ToHashSet();
        var allFaces = Faces();
        foreach (var e in Edges.Where(e => selectedPoints.Contains(e.A) || selectedPoints.Contains(e.B))) selectedEdges.Add(e.Id);
        foreach (var f in allFaces.Where(f => selectedFaces.Contains(f.Key))) selectedEdges.UnionWith(FaceEdges(f));
        var source = Edges.Where(e => selectedEdges.Contains(e.Id)).ToList();
        var sourcePaths = source.Select(e => (A: Get(e.A).P, B: Get(e.B).P)).ToList();
        var transferringFaces = allFaces.Where(f => FaceEdges(f).IsSubsetOf(selectedEdges)).ToList();
        var colored = transferringFaces.Where(f => Fills.ContainsKey(f.Key)).Select(f => (Poly: f.Vertices.Select(id => Get(id).P).ToList(), Color: Fills[f.Key], Pattern: FillPatterns.GetValueOrDefault(f.Key))).ToList();
        var selectedPolygons = transferringFaces.Where(f => selectedFaces.Contains(f.Key)).Select(f => f.Vertices.Select(id => Get(id).P).ToList()).ToList();
        var ids = source.SelectMany(e => new[] { e.A, e.B }).Concat(selectedPoints).Distinct().ToList();
        var map = ids.ToDictionary(id => id, id => Point(Get(id).P, target));
        foreach (int id in ids) Get(map[id]).KeepPoint |= Get(id).KeepPoint;
        Edges.RemoveAll(e => selectedEdges.Contains(e.Id));
        var newEdges = new HashSet<int>();
        var transferredEdges = new Dictionary<int, List<int>>();
        foreach (var e in source)
        {
            var id = AddEdge(map[e.A], map[e.B], e.Color); newEdges.Add(id);
            transferredEdges[e.Id] = new() { id };
            Edges.First(x => x.Id == id).Color = e.Color; Edges.First(x => x.Id == id).Style = e.Style;
        }
        Vertices.RemoveAll(v => map.TryGetValue(v.Id, out int mapped) && mapped != v.Id && !Edges.Any(e => e.A == v.Id || e.B == v.Id));
        RemapGroups(transferredEdges, map);
        Normalize();
        // A target-level crossing may split a transferred edge into several selectable pieces.
        newEdges = Edges.Where(e => LevelOf(e) == target && sourcePaths.Any(path => Distance(Get(e.A).P, path.A, path.B) < 1e-6 && Distance(Get(e.B).P, path.A, path.B) < 1e-6)).Select(e => e.Id).ToHashSet();
        var newFaces = new HashSet<string>();
        foreach (var f in Faces().Where(f => f.Level == target))
        {
            var a = Get(f.Vertices[0]).P; var b = Get(f.Vertices[1]).P; var d = b - a;
            var sample = (a + b) * .5 + new V2(-d.Y, d.X) * (.0001 / Math.Max(d.Length, 1));
            var match = colored.LastOrDefault(c => Inside(sample, c.Poly));
            if (match.Poly != null) { Fills[f.Key] = match.Color; FillPatterns[f.Key] = match.Pattern; }
            if (selectedPolygons.Any(poly => Inside(sample, poly))) newFaces.Add(f.Key);
        }
        return new LevelTransfer(selectedPoints.Select(id => map[id]).Where(id => Vertices.Any(v => v.Id == id)).ToHashSet(), newEdges.Where(id => Edges.Any(e => e.Id == id)).ToHashSet(), newFaces);
    }
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);
    public static Plan Parse(string json)
    {
        var p = JsonSerializer.Deserialize<Plan>(json) ?? throw new InvalidDataException(L.T("Le fichier est vide."));
        if (p.Version < 1 || p.Version > 6) throw new InvalidDataException(L.T("Version du fichier non prise en charge."));
        if (p.Version == 1) { p.ActiveLevel = 0; p.Levels = new() { 0 }; foreach (var v in p.Vertices ?? new()) v.Level = 0; }
        if (p.Version < 3) p.Groups = new();
        p.Version = 6;
        if (p.Vertices == null || p.Edges == null || p.Fills == null || p.Vertices.Count > 20000 || p.Edges.Count > 30000) throw new InvalidDataException(L.T("Géométrie absente ou trop volumineuse."));
        if (p.Vertices.Select(v => v.Id).Distinct().Count() != p.Vertices.Count || p.Edges.Select(e => e.Id).Distinct().Count() != p.Edges.Count) throw new InvalidDataException(L.T("Identifiants dupliqués."));
        var ids = p.Vertices.Select(v => v.Id).ToHashSet();
        if (p.Vertices.Any(v => !double.IsFinite(v.X) || !double.IsFinite(v.Y) || Math.Abs(v.X) > 1e8 || Math.Abs(v.Y) > 1e8) || p.Edges.Any(e => !ids.Contains(e.A) || !ids.Contains(e.B) || e.A == e.B)) throw new InvalidDataException(L.T("Coordonnées ou arêtes invalides."));
        if (p.Levels == null || p.Levels.Any(l => l < -999 || l > 999) || p.ActiveLevel < -999 || p.ActiveLevel > 999 || p.Vertices.Any(v => v.Level < -999 || v.Level > 999) || p.Edges.Any(e => p.Get(e.A).Level != p.Get(e.B).Level)) throw new InvalidDataException(L.T("Niveaux ou arêtes entre niveaux invalides."));
        p.Levels = p.Levels.Append(p.ActiveLevel).Concat(p.Vertices.Select(v => v.Level)).Distinct().Order().ToList();
        if (!double.IsFinite(p.Zoom) || !double.IsFinite(p.ViewX) || !double.IsFinite(p.ViewY)) throw new InvalidDataException(L.T("Vue invalide."));
        p.Zoom = Math.Clamp(p.Zoom, .05, 20); p.Grid = new[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512 }.Contains(p.Grid) ? p.Grid : 32;
        if (p.Groups == null || p.Groups.Any(g => g == null || g.VertexIds == null || g.EdgeIds == null || g.VertexIds.Any(id => !ids.Contains(id)) || g.EdgeIds.Any(id => !p.Edges.Any(e => e.Id == id))) || p.Groups.Select(g => g.Id).Distinct().Count() != p.Groups.Count) throw new InvalidDataException(L.T("Groupes invalides."));
        if (p.LevelHeights == null) throw new InvalidDataException(L.T("Hauteurs Z ou couleur des murs invalides."));
        p.ValidateDecorations();
        p.ValidateMapSettings();
        p.NextId = Math.Max(p.NextId, p.Vertices.Select(v => v.Id).Concat(p.Edges.Select(e => e.Id)).Concat(p.Groups.Select(g => g.Id)).Concat(p.Annotations.Select(a => a.Id)).DefaultIfEmpty(0).Max() + 1);
        if (p.Background != null)
        {
            var b = p.Background;
            if (!double.IsFinite(b.Width) || !double.IsFinite(b.Height) || !double.IsFinite(b.X) || !double.IsFinite(b.Y) || !double.IsFinite(b.Opacity) || b.Width <= 0 || b.Height <= 0) throw new InvalidDataException(L.T("Image de référence invalide."));
            b.Opacity = Math.Clamp(b.Opacity, 0, 1);
        }
        return p;
    }
    public static Plan Demo()
    {
        var p = new Plan();
        void Rect(double x, double y, double w, double h, string color)
        {
            var points = new[] { new V2(x, y), new V2(x + w, y), new V2(x + w, y + h), new V2(x, y + h) };
            for (int i = 0; i < 4; i++) p.Segment(points[i], points[(i + 1) % 4]);
            var f = p.HitFace(new V2(x + w / 2, y + h / 2)); if (f != null) p.Fills[f.Key] = color;
        }
        Rect(-384, -192, 256, 384, "#50757C"); Rect(-128, -64, 256, 128, "#A28555"); Rect(128, -256, 320, 512, "#596E97");
        Rect(224, -64, 128, 128, "#8B756B"); Rect(-320, 192, 128, 128, "#718663");
        return p;
    }
}

public class History
{
    private readonly List<string> undo = new();
    private readonly List<string> redo = new();
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public void Push(string before) { undo.Add(before); if (undo.Count > 100) undo.RemoveAt(0); redo.Clear(); }
    public Plan? Undo(Plan current) { if (!CanUndo) return null; redo.Add(current.Serialize()); var s = undo[^1]; undo.RemoveAt(undo.Count - 1); return Plan.Parse(s); }
    public Plan? Redo(Plan current) { if (!CanRedo) return null; undo.Add(current.Serialize()); var s = redo[^1]; redo.RemoveAt(redo.Count - 1); return Plan.Parse(s); }
    public void Clear() { undo.Clear(); redo.Clear(); }
}

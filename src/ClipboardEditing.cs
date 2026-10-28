// Coat — Epicteta Lupercal
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace Coat;

internal interface IPlanClipboard
{
    void Write(string data);
    string? Read();
}
internal sealed class WindowsPlanClipboard : IPlanClipboard
{
    private const string Format = "Coat.Selection.v1";
    public void Write(string data)
    {
        var contents = new DataObject(); contents.SetData(Format, data); Clipboard.SetDataObject(contents, true);
    }
    public string? Read() => Clipboard.GetData(Format) as string;
}

public partial class MainWindow
{
    internal IPlanClipboard PlanClipboard { get; set; } = new WindowsPlanClipboard();
    private string? lastPastedData;
    private V2 lastPasteCenter;
    private Plan? lastPasteDocument;
    private int pasteCount, lastPasteLevel;
    public bool CopySelection()
    {
        Canvas.CancelGesture();
        var fragment = CaptureSelection();
        if (fragment.Vertices.Count + fragment.Annotations.Count == 0) { Notify(L.T("Sélectionnez les éléments à copier.")); return false; }
        try
        {
            PlanClipboard.Write(fragment.Serialize()); lastPastedData = null;
            Notify(L.T("Sélection copiée. Ctrl+V colle au centre de la vue, sur le niveau actif.")); return true;
        }
        catch (Exception ex) when (ex is ExternalException or System.Threading.ThreadStateException)
        { Notify(L.T("Presse-papiers indisponible. Réessayez dans un instant.")); return false; }
    }
    public void CutSelection()
    {
        if (!CopySelection()) return;
        Canvas.DeleteSelection(); Notify(L.T("Sélection coupée. Ctrl+Z permet de la restaurer."));
    }
    internal Plan CaptureSelection()
    {
        var fragment = new Plan { Grid = Document.Grid };
        if (Canvas.SelectedAnnotation is int noteId)
        {
            var note = Document.Annotations.First(a => a.Id == noteId);
            fragment.Annotations.Add(CloneNote(note, fragment.NextId++, new V2(), 0)); return fragment;
        }
        var points = Canvas.SelectionPoints();
        // In point mode, include edges joining selected points. In edge/face mode, copy only selected edges.
        var edges = Canvas.SelectionEdges();
        if (Canvas.Mode == SelectionMode.Vertex)
            edges.UnionWith(Document.Edges.Where(e => points.Contains(e.A) && points.Contains(e.B)).Select(e => e.Id));
        var vertexMap = points.ToDictionary(id => id, id => fragment.Point(Document.Get(id).P));
        var edgeMap = new Dictionary<int, int>();
        foreach (var edge in Document.Edges.Where(e => edges.Contains(e.Id)))
        {
            int id = fragment.AddEdge(vertexMap[edge.A], vertexMap[edge.B], edge.Color);
            fragment.Edges.First(e => e.Id == id).Style = edge.Style; edgeMap[edge.Id] = id;
        }
        foreach (var v in fragment.Vertices) v.KeepPoint = !fragment.Edges.Any(e => e.A == v.Id || e.B == v.Id);
        CopyFaceStyles(Document, fragment, vertexMap, edges);
        CopyFragmentGroups(Document, fragment, vertexMap, edgeMap);
        return fragment;
    }
    private static Annotation CloneNote(Annotation note, int id, V2 offset, int level) => new()
    { Id = id, X = note.X + offset.X, Y = note.Y + offset.Y, Text = note.Text, Size = note.Size, Color = note.Color, Level = level };
    private static void CopyFaceStyles(Plan source, Plan target, IReadOnlyDictionary<int, int> map, HashSet<int> edges)
    {
        foreach (var face in source.Faces().Where(f => source.FaceEdges(f).IsSubsetOf(edges) && f.Vertices.All(map.ContainsKey)))
        {
            var key = string.Join("-", face.Vertices.Select(id => map[id]).Order());
            if (source.Fills.TryGetValue(face.Key, out var color)) target.Fills[key] = color;
            if (source.FillPatterns.TryGetValue(face.Key, out var pattern)) target.FillPatterns[key] = pattern;
        }
    }
    private static void CopyFragmentGroups(Plan source, Plan target, IReadOnlyDictionary<int, int> vertices, IReadOnlyDictionary<int, int> edges)
    {
        foreach (var group in source.Groups.Where(g => g.VertexIds.All(vertices.ContainsKey) && g.EdgeIds.All(edges.ContainsKey)))
            target.Groups.Add(new GeometryGroup { Id = target.NextId++, Name = group.Name, VertexIds = group.VertexIds.Select(id => vertices[id]).ToList(), EdgeIds = group.EdgeIds.Select(id => edges[id]).ToList() });
    }
    public void PasteSelection()
    {
        string? data;
        try { data = PlanClipboard.Read(); }
        catch (Exception ex) when (ex is ExternalException or System.Threading.ThreadStateException) { Notify(L.T("Presse-papiers indisponible. Réessayez dans un instant.")); return; }
        if (string.IsNullOrWhiteSpace(data)) { Notify(L.T("Le presse-papiers ne contient pas de sélection Coat.")); return; }
        Plan fragment;
        try
        {
            if (data.Length > 16 * 1024 * 1024) throw new InvalidDataException();
            fragment = Plan.Parse(data);
            if (fragment.Background != null || fragment.Vertices.Select(v => v.Level).Concat(fragment.Annotations.Select(a => a.Level)).Distinct().Count() > 1) throw new InvalidDataException();
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or ArgumentException or NullReferenceException)
        { Notify(L.T("La sélection du presse-papiers est invalide.")); return; }
        if (fragment.Vertices.Count + fragment.Annotations.Count == 0) return;
        Canvas.CancelGesture();
        var center = Canvas.Center.Snap(Document.Grid);
        int repeat = data == lastPastedData && Document == lastPasteDocument && center == lastPasteCenter && Document.ActiveLevel == lastPasteLevel ? pasteCount + 1 : 0;
        var positions = fragment.Vertices.Select(v => v.P).Concat(fragment.Annotations.Select(a => new V2(a.X, a.Y))).ToList();
        var origin = new V2((positions.Min(p => p.X) + positions.Max(p => p.X)) / 2, (positions.Min(p => p.Y) + positions.Max(p => p.Y)) / 2);
        var offset = (center - origin).Snap(Document.Grid) + new V2(repeat * Document.Grid, -repeat * Document.Grid);
        if (positions.Any(p => Math.Abs(p.X + offset.X) > 1e8 || Math.Abs(p.Y + offset.Y) > 1e8) || Document.Vertices.Count + fragment.Vertices.Count > 20000 || Document.Edges.Count + fragment.Edges.Count > 30000 || Document.Annotations.Count + fragment.Annotations.Count > 5000)
        { Notify(L.T("Le collage dépasse les limites du plan.")); return; }
        SetTool(EditorTool.Select);
        var previousMode = Canvas.Mode;
        if (fragment.Vertices.Count > 0 && fragment.Edges.Count == 0) Canvas.Mode = previousMode = SelectionMode.Vertex;
        else if (previousMode == SelectionMode.Face && fragment.Faces().Count == 0) Canvas.Mode = previousMode = SelectionMode.Edge;
        Mutate(() =>
        {
            var map = fragment.Vertices.ToDictionary(v => v.Id, v => Document.Point(v.P + offset));
            foreach (var v in fragment.Vertices.Where(v => v.KeepPoint)) Document.Get(map[v.Id]).KeepPoint = true;
            var edgeMap = new Dictionary<int, int>();
            foreach (var edge in fragment.Edges)
            {
                int id = Document.AddEdge(map[edge.A], map[edge.B], edge.Color);
                var added = Document.Edges.First(e => e.Id == id); added.Color = edge.Color; added.Style = edge.Style; edgeMap[edge.Id] = id;
            }
            CopyFaceStyles(fragment, Document, map, fragment.Edges.Select(e => e.Id).ToHashSet());
            CopyFragmentGroups(fragment, Document, map, edgeMap);
            // Temporary group tracks the newly pasted topology through splitting/merging at intersections.
            var tracking = new GeometryGroup { Id = Document.NextId++, VertexIds = map.Values.ToList(), EdgeIds = edgeMap.Values.ToList() };
            Document.Groups.Add(tracking); Document.Normalize(); Document.Groups.Remove(tracking);
            Canvas.ClearSelection();
            if (previousMode == SelectionMode.Vertex || edgeMap.Count == 0) Canvas.SelectedVertices.UnionWith(tracking.VertexIds.Concat(Document.Edges.Where(e => tracking.EdgeIds.Contains(e.Id)).SelectMany(e => new[] { e.A, e.B })));
            else Canvas.SelectedEdges.UnionWith(tracking.EdgeIds);
            foreach (var note in fragment.Annotations)
            {
                var pasted = CloneNote(note, Document.NextId++, offset, Document.ActiveLevel); Document.Annotations.Add(pasted); Canvas.SelectedAnnotation = pasted.Id;
            }
            Canvas.RefreshGeometry(); Canvas.ExpandGroups();
        });
        lastPastedData = data; lastPasteDocument = Document; lastPasteCenter = center; lastPasteLevel = Document.ActiveLevel; pasteCount = repeat;
        Canvas.Focus(); Notify(L.T("Sélection collée sur le niveau ") + Document.ActiveLevel + L.T(". Glissez pour la placer."));
    }
    public void ToggleUpperLevels()
    {
        Canvas.ShowUpperLevels = !Canvas.ShowUpperLevels; upperLevelsMenu.IsChecked = Canvas.ShowUpperLevels; Canvas.InvalidateVisual();
        Notify(Canvas.ShowUpperLevels ? L.T("Niveaux supérieurs visibles en traits rouges.") : L.T("Niveaux supérieurs masqués."));
    }
    private MenuItem upperLevelsMenu = null!;
    private MenuItem lowerLevelsMenu = null!;
    public void ToggleLowerLevels()
    {
        Canvas.ShowLowerLevels = !Canvas.ShowLowerLevels; lowerLevelsMenu.IsChecked = Canvas.ShowLowerLevels; Canvas.InvalidateVisual();
        Notify(Canvas.ShowLowerLevels ? L.T("Niveaux inférieurs visibles.") : L.T("Niveaux inférieurs masqués."));
    }
}

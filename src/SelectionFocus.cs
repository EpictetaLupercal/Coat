// Coat — Epicteta Lupercal
using System.Windows.Controls;
using System.Windows.Input;

namespace Coat;

public partial class EditorCanvas
{
    private sealed record FocusView(V2 Center, double Zoom, int FirstNewId, HashSet<int> Vertices,
        HashSet<int> Edges, HashSet<string> Faces, HashSet<int> Annotations);

    private FocusView? focusView;
    internal bool IsSelectionFocused => focusView != null;
    internal (V2 Center, double Zoom) PersistentView => focusView is { } focus ? (focus.Center, focus.Zoom) : (Center, Zoom);

    internal void ToggleSelectionFocus()
    {
        if (focusView != null) { ClearSelectionFocus(restoreView: true); return; }
        if (SelectionCount == 0) { Host.UpdateStatus(); return; }
        CancelGesture();
        var vertices = SelectionPoints();
        var edges = SelectionEdges();
        if (Mode == SelectionMode.Vertex)
            edges.UnionWith(Plan.Edges.Where(e => vertices.Contains(e.A) && vertices.Contains(e.B)).Select(e => e.Id));
        var visibleFaces = faces.Where(f => SelectedFaces.Contains(f.Key) || CachedFaceEdges(f).IsSubsetOf(edges)).ToArray();
        foreach (var hole in visibleFaces.SelectMany(f => faceHoles[f.Key]))
        {
            edges.UnionWith(CachedFaceEdges(renderFaces[hole]));
            vertices.UnionWith(renderFaces[hole].Vertices);
        }
        focusView = new(Center, Zoom, Plan.NextId, vertices, edges,
            visibleFaces.Select(f => f.Key).ToHashSet(), SelectedAnnotations.ToHashSet());
        Feedback.Clear();
        if (!FocusPoints().Any()) { focusView = null; Host.UpdateStatus(); return; }
        InvalidateColors();
        Fit();
    }

    internal void ClearSelectionFocus(bool restoreView)
    {
        if (focusView is not { } focus) return;
        Feedback.Clear();
        focusView = null;
        if (restoreView) { Center = focus.Center; Zoom = focus.Zoom; }
        InvalidateColors();
        Host.UpdateStatus();
        InvalidateVisual();
    }

    private bool FocusIncludesVertex(Vertex vertex) => focusView is not { } focus ||
        vertex.Id >= focus.FirstNewId || focus.Vertices.Contains(vertex.Id);

    private bool FocusIncludesEdge(Edge edge) => focusView is not { } focus || focus.Edges.Contains(edge.Id) ||
        (edge.Id >= focus.FirstNewId && FocusIncludesVertex(VertexAt(edge.A)) && FocusIncludesVertex(VertexAt(edge.B)));

    private bool FocusIncludesFace(Face face) => focusView is not { } focus || focus.Faces.Contains(face.Key) ||
        (CachedFaceEdges(face).Any(id => id >= focus.FirstNewId) && CachedFaceEdges(face).All(id => FocusIncludesEdge(renderEdges[id])));

    private bool FocusIncludesAnnotation(Annotation note) => focusView is not { } focus ||
        note.Id >= focus.FirstNewId || focus.Annotations.Contains(note.Id);

    private IEnumerable<V2> FocusPoints()
    {
        foreach (var vertex in Plan.Vertices.Where(FocusIncludesVertex)) yield return vertex.P;
        foreach (var note in Plan.Annotations.Where(FocusIncludesAnnotation))
        {
            var bounds = note.WorldBounds();
            yield return new(bounds.Left, -bounds.Top);
            yield return new(bounds.Right, -bounds.Bottom);
        }
    }
}

public partial class MainWindow
{
    private MenuItem? focusMenu;
    private bool focusTextRepeating;
    private bool UsesDefaultFocusShortcut => Shortcuts.Bindings.GetValueOrDefault(ShortcutSettings.FocusAction) == ShortcutSettings.FocusDefault;

    // On layouts such as AZERTY, the key that types '/' may differ from Oem2.
    private void OnFocusTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text != "/" || !UsesDefaultFocusShortcut || e.OriginalSource is not EditorCanvas) return;
        if (!focusTextRepeating) Canvas.ToggleSelectionFocus();
        e.Handled = true;
    }
}

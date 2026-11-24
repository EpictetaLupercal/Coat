// Coat — Epicteta Lupercal
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Coat;

public enum EditorTool { Select, Draw, Erase, Paint, None, Shapes, Annotation, Dimension, Token, Mirror, Modifiers }
public enum SelectionMode { Vertex, Edge, Face }

public partial class EditorCanvas : FrameworkElement
{
    public MainWindow Host { get; }
    public Plan Plan => Host.Document;
    public EditorTool Tool { get; set; } = EditorTool.None;
    public SelectionMode Mode { get; set; } = SelectionMode.Edge;
    public HashSet<int> SelectedVertices { get; } = new();
    public HashSet<int> SelectedEdges { get; } = new();
    public HashSet<string> SelectedFaces { get; } = new();
    public string PaintColor { get; set; } = "#66C5AD";
    public bool PaintLines { get; set; }
    public ShapeKind Shape { get; set; } = ShapeKind.Rectangle;
    public int CirclePoints { get; set; } = 32;
    private V2? shapeStart;
    private bool shaping;
    public V2 Center { get; set; }
    public double Zoom { get; set; } = 1;
    public bool ShowGrid { get; set; } = true;
    public bool ShowLengths { get; set; }
    public bool ShowUpperLevels { get; set; }
    public bool ShowLowerLevels { get; set; } = true;
    public bool Exporting { get; set; }
    public bool ExportGuides { get; set; }
    public V2? Anchor { get; set; }
    private V2? chainStart;
    private V2 mouseWorld;
    private Point mouseScreen;
    private Point downScreen;
    private V2 downWorld;
    private V2 downCenter;
    private bool panning, dragging, marquee, extruding, moved;
    private PlanSnapshot? before;
    private Dictionary<int, V2> original = new();
    private List<(V2 A, V2 B)> extrusionEdges = new();
    private V2 extrusionDelta;
    private BitmapImage? reference;
    private string? referenceData;
    private List<Face> faces = new();
    private readonly DrawingGroup viewportDrawing = new();
    private bool panFrameQueued;
    // Keep the retained scene during a burst of wheel events, then restore screen-space detail.
    private readonly DispatcherTimer wheelPreview = new(DispatcherPriority.Input)
    {
        Interval = TimeSpan.FromMilliseconds(140)
    };
    private double sceneZoom = 1;
    internal int ClosedFaceCount => faces.Count;
    private static readonly Dictionary<string, SolidColorBrush> brushes = new();
    private static readonly Brush GridMinor = Brush("#242D3A"), GridMajor = Brush("#323D4C"), Accent = Brush("#78DCC1"), Text = Brush("#99A7BA");
    public static SolidColorBrush Brush(string color)
    {
        if (brushes.TryGetValue(color, out var cached)) return cached;
        try { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); b.Freeze(); if (brushes.Count > 4096) brushes.Clear(); brushes[color] = b; return b; }
        catch { return new SolidColorBrush(Colors.LightGray); }
    }
    public EditorCanvas(MainWindow host)
    {
        Host = host; GlPlan = new OpenGlPlan(this); Focusable = true; ClipToBounds = true; Cursor = Cursors.Arrow;
        MouseDown += OnDown; MouseMove += OnMove; MouseUp += OnUp; MouseWheel += OnWheel;
        LostMouseCapture += (_, _) =>
        {
            if (IsColorSampling) CancelColorSampling();
            if (dragging || extruding || shaping) CancelGesture();
            if (panning) { SetPanning(false); InvalidateVisual(); }
            marquee = false;
        };
        wheelPreview.Tick += (_, _) =>
        {
            wheelPreview.Stop();
            InvalidateVisual();
        };
        Unloaded += (_, _) =>
        {
            wheelPreview.Stop();
            CancelPanFrame();
        };
        MouseLeave += (_, _) => { if (!IsMouseCaptured) { mouseScreen = new Point(-100, -100); InvalidateVisual(); } };
    }
    public Point Screen(V2 p) => new(ActualWidth / 2 + (p.X - Center.X) * Zoom, ActualHeight / 2 - (p.Y - Center.Y) * Zoom);
    public V2 World(Point p) => new(Center.X + (p.X - ActualWidth / 2) / Zoom, Center.Y - (p.Y - ActualHeight / 2) / Zoom);
    public void RefreshGeometry() => RefreshGeometry(null);

    internal void RefreshGeometry(BitmapImage? preparedReference)
    {
        faces = Plan.Faces(); RebuildRenderCache();
        SelectedAnnotations.IntersectWith(Plan.Annotations.Select(a => a.Id));
        if (referenceData != Plan.Background?.Data)
        {
            reference = preparedReference ?? ReadReference(Plan.Background?.Data);
            referenceData = Plan.Background?.Data;
        }
        SelectedVertices.RemoveWhere(id => !renderVertices.ContainsKey(id));
        SelectedEdges.RemoveWhere(id => !renderEdges.ContainsKey(id));
        SelectedFaces.RemoveWhere(key => !renderFaces.ContainsKey(key));
        InvalidateVisual();
    }
    internal BitmapImage? ReadReference(string? data) =>
        data == referenceData ? reference : string.IsNullOrEmpty(data) ? null : DecodeImage(data);

    public static BitmapImage DecodeImage(string data)
    {
        using var stream = new MemoryStream(Convert.FromBase64String(data));
        var bmp = new BitmapImage(); bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.StreamSource = stream; bmp.EndInit(); bmp.Freeze(); return bmp;
    }
    internal bool IndividualSelection { get; private set; }
    internal bool IgnoreGroups { get; private set; }
    internal bool GroupsIgnored => IgnoreGroups || IndividualSelection;
    internal void ToggleIgnoreGroups()
    {
        CancelGesture();
        if(GroupsIgnored){IgnoreGroups=false;IndividualSelection=false;ExpandGroups();}
        else {IgnoreGroups=true;ClearSelection();}
        Host.UpdateInspector();InvalidateVisual();Focus();
    }
    internal void ResetGroupSelectionMode(){IgnoreGroups=false;IndividualSelection=false;}
    public void ClearSelection() { IndividualSelection = false; SelectedVertices.Clear(); SelectedEdges.Clear(); SelectedFaces.Clear(); SelectedAnnotation = null; }
    public HashSet<int> SelectionEdges()
    {
        var edges = new HashSet<int>(SelectedEdges);
        foreach (string key in SelectedFaces) if (renderFaces.TryGetValue(key, out var face)) edges.UnionWith(CachedFaceEdges(face));
        return edges;
    }
    public void ExpandGroups()
    {
        if (GroupsIgnored) return;
        // Group membership traversal is a work queue, avoiding repeated scans of every group.
        var byVertex = new Dictionary<int, List<GeometryGroup>>(); var byEdge = new Dictionary<int, List<GeometryGroup>>();
        void Add(Dictionary<int, List<GeometryGroup>> index, int id, GeometryGroup group) { if (!index.TryGetValue(id, out var members)) index[id] = members = new(); members.Add(group); }
        foreach (var group in Plan.Groups)
        {
            foreach (int id in group.VertexIds) Add(byVertex, id, group);
            foreach (int id in group.EdgeIds) { Add(byEdge, id, group); if (renderEdges.TryGetValue(id, out var edge)) { Add(byVertex, edge.A, group); Add(byVertex, edge.B, group); } }
        }
        var pending = new Queue<GeometryGroup>(); var seen = new HashSet<int>();
        void Enqueue(Dictionary<int, List<GeometryGroup>> index, IEnumerable<int> ids) { foreach (int id in ids) if (index.TryGetValue(id, out var groups)) foreach (var g in groups) if (seen.Add(g.Id)) pending.Enqueue(g); }
        Enqueue(byVertex, SelectedVertices); Enqueue(byEdge, SelectionEdges());
        while (pending.TryDequeue(out var group))
        {
            var vertices = IsSelectionFocused ? group.VertexIds.Where(id => renderVertices.TryGetValue(id, out var vertex) && FocusIncludesVertex(vertex)) : group.VertexIds;
            var edges = IsSelectionFocused ? group.EdgeIds.Where(id => renderEdges.TryGetValue(id, out var edge) && FocusIncludesEdge(edge)) : group.EdgeIds;
            SelectedVertices.UnionWith(vertices); SelectedEdges.UnionWith(edges);
            Enqueue(byVertex, vertices); Enqueue(byEdge, edges);
        }
        foreach (var face in faces.Where(f => FocusIncludesFace(f) && CachedFaceEdges(f).IsSubsetOf(SelectedEdges))) SelectedFaces.Add(face.Key);
    }
    private void SelectHit(int? vertex, int? edge, Face? face, bool ctrl, bool individual = false)
    {
        bool precise = IndividualSelection || individual;
        if (individual) ctrl = false;
        var oldAnnotations = SelectedAnnotations.ToHashSet();
        var oldV = SelectedVertices.ToHashSet(); var oldE = SelectedEdges.ToHashSet(); var oldF = SelectedFaces.ToHashSet();
        ClearSelection(); IndividualSelection = precise; if (vertex.HasValue) SelectedVertices.Add(vertex.Value); if (edge.HasValue) SelectedEdges.Add(edge.Value); if (face != null) SelectedFaces.Add(face.Key);
        ExpandGroups();
        bool already = SelectedVertices.IsSubsetOf(oldV) && SelectedEdges.IsSubsetOf(oldE) && SelectedFaces.IsSubsetOf(oldF);
        if (ctrl && already) { oldV.ExceptWith(SelectedVertices); oldE.ExceptWith(SelectedEdges); oldF.ExceptWith(SelectedFaces); ClearSelection(); IndividualSelection = precise; }
        if (!individual && ((ctrl && !already) || already)) { SelectedVertices.UnionWith(oldV); SelectedEdges.UnionWith(oldE); SelectedFaces.UnionWith(oldF); SelectedAnnotations.UnionWith(oldAnnotations); }
        if (!already || individual && (oldV.Count != SelectedVertices.Count || oldE.Count != SelectedEdges.Count ||
            oldF.Count != SelectedFaces.Count || oldAnnotations.Count != SelectedAnnotations.Count))
        {
            Host.PlayEditorSound(EditorSoundCue.Selection);
            SelectionFeedback(vertex, edge, face);
        }
    }
    public HashSet<int> SelectionPoints()
    {
        var ids = new HashSet<int>(SelectedVertices);
        foreach (int id in SelectedEdges) if (renderEdges.TryGetValue(id, out var e)) { ids.Add(e.A); ids.Add(e.B); }
        foreach (string key in SelectedFaces) if (renderFaces.TryGetValue(key, out var f)) ids.UnionWith(f.Vertices);
        return ids;
    }
    public void SelectAll()
    {
        if (Tool != EditorTool.Select) return;
        ClearSelection();
        if (Mode == SelectionMode.Vertex) SelectedVertices.UnionWith(Plan.Vertices.Where(v => CanSelectLevel(v.Level) && FocusIncludesVertex(v)).Select(v => v.Id));
        else if (Mode == SelectionMode.Edge) SelectedEdges.UnionWith(Plan.Edges.Where(e => CanSelectLevel(VertexAt(e.A).Level) && FocusIncludesEdge(e)).Select(e => e.Id));
        else SelectedFaces.UnionWith(faces.Where(f => CanSelectLevel(f.Level) && FocusIncludesFace(f)).Select(f => f.Key));
        SelectedAnnotations.UnionWith(Plan.Annotations.Where(CanSelectAnnotation).Select(a => a.Id));
        if(SelectedAnnotations.Count>0)Transform=TransformKind.Translation;
        ExpandGroups(); Host.UpdateInspector(); InvalidateVisual();
        if (SelectionCount > 0) Host.PlayEditorSound(EditorSoundCue.Selection);
    }
    private int SelectionCount => SelectedVertices.Count + SelectedEdges.Count + SelectedFaces.Count + SelectedAnnotations.Count;
    public void DeleteSelection()
    {
        var annotations = SelectedAnnotations.ToHashSet();
        var vertices = new HashSet<int>(SelectedVertices); var edges = new HashSet<int>(SelectedEdges);
        foreach (var f in faces.Where(f => SelectedFaces.Contains(f.Key)))
            for (int i = 0; i < f.Vertices.Count; i++)
            { int a = f.Vertices[i], b = f.Vertices[(i + 1) % f.Vertices.Count]; foreach (var e in Plan.Edges.Where(e => (e.A == a && e.B == b) || (e.A == b && e.B == a))) edges.Add(e.Id); }
        if (vertices.Count + edges.Count + annotations.Count == 0) return;
        Host.Mutate(() => { Plan.Annotations.RemoveAll(a => annotations.Contains(a.Id)); Plan.Edges.RemoveAll(e => edges.Contains(e.Id) || vertices.Contains(e.A) || vertices.Contains(e.B)); Plan.Vertices.RemoveAll(v => vertices.Contains(v.Id)); Plan.Normalize(); });
        ClearSelection(); Host.UpdateInspector();
    }
    private Edge? HitEdge(V2 p) => Plan.Edges.Where(e => FocusIncludesEdge(e) && CanSelectLevel(VertexAt(e.A).Level) && Plan.Distance(p, VertexAt(e.A).P, VertexAt(e.B).P) <= 9 / Zoom).OrderBy(e => Plan.Distance(p, VertexAt(e.A).P, VertexAt(e.B).P)).FirstOrDefault();
    private Vertex? HitVertex(V2 p) => Plan.Vertices.Where(v => FocusIncludesVertex(v) && CanSelectLevel(v.Level) && (p - v.P).Length <= 10 / Zoom).OrderBy(v => (p - v.P).Length).FirstOrDefault();
    private Face? HitFace(V2 p) => faces.Where(f => FocusIncludesFace(f) && CanSelectLevel(f.Level) && faceGeometry[f.Key].Bounds.Contains(new Point(p.X, -p.Y)) &&
        (IsSelectionFocused ? FillContains(f.Key, p) : Plan.Inside(p, f.Vertices.Select(id => VertexAt(id).P).ToList()))).OrderBy(f => Math.Abs(f.Level - Plan.ActiveLevel)).ThenBy(f => f.Area).FirstOrDefault();
    public bool HasSelectionOrGesture => SelectedAnnotations.Count > 0 || SelectedVertices.Count + SelectedEdges.Count + SelectedFaces.Count > 0 || Anchor != null || shapeStart != null || dragging || extruding || marquee;
    private static bool Shift => (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
    private static bool Ctrl => (Keyboard.Modifiers & ModifierKeys.Control) != 0;
    public void CancelGesture()
    {
        SetPanning(false);
        if (IsColorSampling) CancelColorSampling();
        if (before != null && (dragging || extruding)) Host.Document = before.Value.Restore();
        before = null; dragFeedback = null; dragging = extruding = marquee = shaping = false; Anchor = chainStart = shapeStart = null;
        ReleaseMouseCapture(); RefreshGeometry(); Host.UpdateInspector(); Host.UpdateStatus();
    }
    private Cursor ToolCursor => Tool == EditorTool.Select || Tool == EditorTool.None ? Cursors.Arrow : Cursors.Cross;
    public void SetTool(EditorTool t) { CancelGesture(); Tool = t; if (t is EditorTool.Token or EditorTool.Annotation && Plan.Annotations.FirstOrDefault(a => a.Id == SelectedAnnotation) is { } note && note.IsToken != (t == EditorTool.Token)) SelectedAnnotation = null; if (t == EditorTool.Draw) { InitializeDrawAppearance(); DrawAppearance.Dimension = false; } if (t == EditorTool.Dimension) InitializeDimensionAppearance(); if (t == EditorTool.None) ClearSelection(); Cursor = ToolCursor; Host.UpdateInspector(); InvalidateVisual(); }
    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        PointerDown(e.GetPosition(this), e.ChangedButton, e.ClickCount, Shift, Ctrl, Keyboard.IsKeyDown(Key.Space));
    }
    // Both real WPF events and the regression suite use the same gesture controller.
    internal void PointerDown(Point position, MouseButton button, int clickCount = 1, bool shift = false, bool ctrl = false, bool space = false)
    {
        if (IsColorSampling) { if (button == MouseButton.Left) SampleColorAt(PointToScreen(position)); else if (button == MouseButton.Right) CancelColorSampling(); return; }
        Focus(); downScreen = mouseScreen = position; downWorld = mouseWorld = World(downScreen); downCenter = Center; moved = false;
        if (button == MouseButton.Middle || button == MouseButton.Right || (button == MouseButton.Left && space))
        { SetPanning(true); CaptureMouse(); Cursor = Cursors.Hand; return; }
        if (button != MouseButton.Left) return;
        if (Host.Rts?.HandleSceneClick(downWorld, position, ctrl) == true) return;
        if (Tool == EditorTool.None) return;
        if (Tool == EditorTool.Mirror) { Host.PickMirrorAxis(downWorld.Snap(Plan.Grid)); return; }
        if (Tool == EditorTool.Modifiers)
        {
            Host.PickBooleanOperand(downWorld, () => faces.FirstOrDefault(f => f.Level == Plan.ActiveLevel && FocusIncludesFace(f) && FillContains(f.Key, downWorld)));
            return;
        }
        if (clickCount < 2 && TryBeginScaleHandle()) return;
        if (AnnotationDown(downWorld, ctrl)) return;
        if (Tool == EditorTool.Shapes)
        {
            if (shapeStart != null) FinishShape(downWorld);
            else { shapeStart = downWorld.Snap(Plan.Grid); shaping = true; CaptureMouse(); }
            InvalidateVisual(); return;
        }
        if (Tool is EditorTool.Draw or EditorTool.Dimension)
        {
            if (clickCount > 1) { Anchor = chainStart = null; InvalidateVisual(); return; }
            var p = downWorld.Snap(Plan.Grid);
            if (Anchor == null)
            {
                Anchor = chainStart = p;
                Host.PlayEditorSound(EditorSoundCue.Drawing);
                if (Tool == EditorTool.Draw) PointFeedback(FeedbackCue.DrawPoint, p, Plan.ActiveLevel);
            }
            else if ((p - Anchor.Value).Length > 1e-6)
            {
                var previous = Tool == EditorTool.Draw ? SnapshotFeedbackFaces() : null;
                var start = Anchor.Value; var appearance = ActiveStroke; Host.Mutate(() => { int id = Plan.AddEdge(Plan.Point(start), Plan.Point(p), appearance.Color); var edge = Plan.Edges.First(e => e.Id == id); edge.Style = appearance.Style; edge.CopyArrows(appearance); Plan.Normalize(); }, createsFaces: Tool == EditorTool.Draw, createsObjects: Tool == EditorTool.Dimension);
                if (Tool == EditorTool.Dimension || (chainStart != null && (p - chainStart.Value).Length < 1e-6)) Anchor = chainStart = null;
                else Anchor = p;
                Host.PlayEditorSound(EditorSoundCue.Drawing);
                if (Tool == EditorTool.Draw && !ClosedDrawingFeedback(previous, start, p)) PointFeedback(FeedbackCue.DrawPoint, p, Plan.ActiveLevel);
            }
            InvalidateVisual(); Host.UpdateStatus(); return;
        }
        if (Tool == EditorTool.Erase)
        {
            var edge = HitEdge(downWorld);
            if (edge != null) Host.Mutate(() => { Plan.Edges.Remove(edge); Plan.Normalize(); });
            else { var face = HitFace(downWorld); if (face != null) Host.Mutate(() => Plan.Fills.Remove(face.Key)); }
            return;
        }
        if (Tool == EditorTool.Paint)
        {
            if (PaintLines) { var edge = HitEdge(downWorld); if (edge != null) Host.Mutate(() => { edge.Color = PaintColor; edge.Style = PaintLineStyle; }); }
            else { var face = HitFace(downWorld); if (face != null) Host.Mutate(() => { PaintFace(face.Key); }); else Host.Notify(L.T("Fermez le contour pour pouvoir le remplir.")); }
            return;
        }
        bool hit = false;
        if (Mode == SelectionMode.Vertex)
        {
            var v = HitVertex(downWorld); if (v != null) { hit = true; SelectHit(v.Id, null, null, ctrl, clickCount > 1); }
        }
        else if (Mode == SelectionMode.Edge)
        {
            var edge = HitEdge(downWorld); if (edge != null) { hit = true; SelectHit(null, edge.Id, null, ctrl, clickCount > 1); }
        }
        else
        {
            var face = HitFace(downWorld); if (face != null) { hit = true; SelectHit(null, null, face, ctrl, clickCount > 1); }
        }
        // A closed shape already selected as vertices or edges can also be grabbed by its interior.
        // Keep precise vertex/edge hits first, and reserve Shift extrusion for actual edge hits.
        bool interior = false;
        if (!hit && !ctrl && Mode != SelectionMode.Face)
        {
            var points = SelectionPoints(); var edges = SelectionEdges();
            interior = faces.Any(f => CanSelectLevel(f.Level) &&
                (Mode == SelectionMode.Vertex ? f.Vertices.All(points.Contains) : CachedFaceEdges(f).IsSubsetOf(edges)) &&
                FillContains(f.Key, downWorld));
            hit = interior;
        }
        if (hit && !ctrl)
        {
            before = PlanSnapshot.Capture(Plan);
            if (shift && Mode == SelectionMode.Edge && !interior && Transform == TransformKind.Translation)
            {
                extruding = true; extrusionDelta = new V2();
                extrusionEdges = Plan.Edges.Where(e => SelectedEdges.Contains(e.Id)).Select(e => (VertexAt(e.A).P, VertexAt(e.B).P)).ToList();
            }
            else BeginTransformDrag();
            CaptureMouse();
        }
        else if (!hit)
        { if (!ctrl) ClearSelection(); marquee = true; CaptureMouse(); }
        Host.UpdateInspector(); InvalidateVisual();
    }
    private void FinishShape(V2 end)
    {
        if (shapeStart == null) return;
        if (Shape == ShapeKind.Stairs) { FinishStair(end); return; }
        var start = shapeStart.Value;
        var shape = Shape;
        var contour = Shapes.Contour(shape, start, end, CirclePoints, Plan.Grid);
        if (contour.Count < 3) return;
        var previous = SnapshotFeedbackFaces();
        shapeStart = null; shaping = false; ReleaseMouseCapture();
        Host.Mutate(() => Plan.Polygon(contour), createsFaces: true);
        Host.PlayEditorSound(EditorSoundCue.Drawing);
        CompletedShapeFeedback(shape, previous, contour, start);
    }
    private void OnMove(object sender, MouseEventArgs e)
    {
        PointerMove(e.GetPosition(this), Shift);
    }
    internal void PointerMove(Point position, bool shift = false)
    {
        mouseScreen = position; mouseWorld = World(mouseScreen);
        if ((mouseScreen - downScreen).Length > 3) moved = true;
        if (!dragging && !panning) UpdateScaleCursor();
        if (panning)
        {
            var delta = mouseScreen - downScreen;
            var center = downCenter + new V2(-delta.X / Zoom, delta.Y / Zoom);
            if (Center == center) return;
            Center = center;
            if (!panFrameQueued)
            {
                panFrameQueued = true;
                CompositionTarget.Rendering += DrawPanFrame;
            }
            return;
        }
        if (dragging && moved)
        {
            if (Tool == EditorTool.Select && Transform == TransformKind.Translation) dragFeedback ??= CaptureMoveFeedback();
            var delta = (mouseWorld - downWorld).Snap(Plan.Grid);
            if (Transform != TransformKind.Translation) UpdateTransformDrag(shift);
            else
            {
                foreach (var pair in original) VertexAt(pair.Key).P = pair.Value + delta;
                foreach (var note in Plan.Annotations.Where(a => originalAnnotations.ContainsKey(a.Id)))
                {
                    var target = originalAnnotations[note.Id].Position + delta;
                    if (original.Count == 0 && originalAnnotations.Count == 1) target = SnapAnnotation(originalAnnotations[note.Id].Position + mouseWorld - downWorld);
                    note.X = target.X; note.Y = target.Y;
                }
            }
        }
        else if (extruding && moved) extrusionDelta = (mouseWorld - downWorld).Snap(Plan.Grid);
        if (dragging && moved) { if(original.Count > 0)RebuildFaceGeometry();else InvalidateColors(); }
        Host.UpdateStatus(mouseWorld.Snap(Plan.Grid)); InvalidateVisual();
    }
    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        PointerUp(e.ChangedButton);
    }
    internal void PointerUp(MouseButton button)
    {
        if (panning)
        {
            if (button == MouseButton.Right && !moved) { Anchor = chainStart = shapeStart = null; }
            SetPanning(false); ReleaseMouseCapture(); Cursor = ToolCursor; InvalidateVisual();
            if (moved) Host.UpdateStatus(World(mouseScreen).Snap(Plan.Grid));
            if (button == MouseButton.Right && !moved && Host.Rts?.OpenScenePingMenu(downScreen) != true && Tool == EditorTool.Select) OpenSelectionMenu(downWorld);
            return;
        }
        if (button != MouseButton.Left) return;
        if (Tool is EditorTool.Mirror or EditorTool.Modifiers) return;
        if (shaping)
        {
            shaping = false; ReleaseMouseCapture();
            if (moved) FinishShape(mouseWorld);
            InvalidateVisual(); return;
        }
        if (extruding && moved && extrusionDelta.Length > 0)
        {
            extruding = false;
            var previous = before;
            before = null;
            ReleaseMouseCapture();
            ExtrudeSelection(extrusionDelta, previous, downWorld);
            return;
        }
        if (dragging || extruding)
        {
            bool movedSelection = dragFeedback != null && dragging && moved &&
                (original.Any(pair => (VertexAt(pair.Key).P - pair.Value).Length > 1e-9) ||
                    Plan.Annotations.Any(note => originalAnnotations.TryGetValue(note.Id, out var pose) && (note.Position - pose.Position).Length > 1e-9));
            if (dragging && moved && SelectedAnnotation == null && original.Any(pair => (VertexAt(pair.Key).P - pair.Value).Length > 1e-9)) NormalizeSelection();
            dragging = extruding = false;
            if (before != null) Host.Commit(before.Value); before = null;
            if (movedSelection) FinishMoveFeedback(dragFeedback);
            dragFeedback = null;
        }
        else if (marquee && moved)
        {
            int previousCount = SelectionCount;
            var rect = new Rect(downScreen, mouseScreen);
            bool Contains(int id) => rect.Contains(Screen(VertexAt(id).P));
            if (Mode == SelectionMode.Vertex) SelectedVertices.UnionWith(Plan.Vertices.Where(v => FocusIncludesVertex(v) && CanSelectLevel(v.Level) && Contains(v.Id)).Select(v => v.Id));
            else if (Mode == SelectionMode.Edge) SelectedEdges.UnionWith(Plan.Edges.Where(e => FocusIncludesEdge(e) && CanSelectLevel(VertexAt(e.A).Level) && Contains(e.A) && Contains(e.B)).Select(e => e.Id));
            else SelectedFaces.UnionWith(faces.Where(f => FocusIncludesFace(f) && CanSelectLevel(f.Level) && f.Vertices.All(Contains)).Select(f => f.Key));
            SelectedAnnotations.UnionWith(Plan.Annotations.Where(a => CanSelectAnnotation(a) && rect.Contains(AnnotationScreenBounds(a))).Select(a => a.Id));
            if (SelectedAnnotations.Count > 0) Transform = TransformKind.Translation;
            ExpandGroups();
            if (SelectionCount > previousCount)
            {
                Host.PlayEditorSound(EditorSoundCue.Selection);
                MarqueeFeedback(downWorld, mouseWorld);
            }
        }
        marquee = false; ReleaseMouseCapture(); RefreshGeometry(); Host.UpdateInspector();
    }
    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomAt(e.GetPosition(this), e.Delta);
        e.Handled = true;
    }

    internal void ZoomAt(Point position, int delta)
    {
        double next = Math.Clamp(Zoom * Math.Pow(1.15, delta / 120.0), .01, 20);
        if (next == Zoom) return;
        var anchored = World(position);
        Zoom = next;
        Center += anchored - World(position);
        mouseWorld = World(mouseScreen);
        if (GlPlan?.Active == true)
        {
            wheelPreview.Stop();
            wheelPreview.Start();
        }
        Host.UpdateStatus();
        InvalidateVisual();
    }
    public void Fit()
    {
        var pts = IsSelectionFocused ? FocusPoints().ToList() : Plan.Vertices.Where(v => IsLevelVisible(v.Level)).Select(v => v.P).Concat(Plan.AnnotationBounds(ShowLowerLevels)).ToList();
        if (!IsSelectionFocused && Plan.Background is { Visible: true } b) { pts.Add(new V2(b.X, b.Y)); pts.Add(new V2(b.X + b.Width, b.Y - b.Height)); }
        if (pts.Count == 0) { Center = new V2(); Zoom = 1; }
        else
        {
            double x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y);
            Center = new V2((x0 + x1) / 2, (y0 + y1) / 2);
            Zoom = Math.Clamp(Math.Min(Math.Max(ActualWidth - 160, 100) / Math.Max(x1 - x0, 64), Math.Max(ActualHeight - 160, 100) / Math.Max(y1 - y0, 64)), .01, 20);
        }
        Host.UpdateStatus(); InvalidateVisual();
    }
    private void Label(DrawingContext dc, string text, Point point, double size = 11, Brush? brush = null)
    {
        var key = (text, size, brush ?? Text, L.Language, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if (!labels.TryGetValue(key, out var ft)) { ft = new FormattedText(text, L.Culture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush ?? Text, key.Item5); if (labels.Count > 2048) labels.Clear(); labels[key] = ft; }
        dc.DrawText(ft, point);
    }
    internal OpenGlPlan GlPlan { get; }
    private void PrepareScene(bool preview)
    {
        Edge? hover = !Exporting && Tool == EditorTool.Select && Mode == SelectionMode.Edge && Shift ? HitEdge(mouseWorld) : null;
        var style = (Plan.ActiveLevel, ShowLowerLevels, ShowUpperLevels, ShowLengths, ActualWidth, ActualHeight, Tool, Mode, Exporting, L.Language, VisualTreeHelper.GetDpi(this).PixelsPerDip, hover?.Id, SelectedAnnotation);
        if (cachedScene == null || (!preview && sceneZoom != Zoom) || !Equals(cachedSceneStyle, style) || !sceneVertices.SetEquals(SelectedVertices) || !sceneEdges.SetEquals(SelectedEdges) || !sceneFaces.SetEquals(SelectedFaces) || !sceneAnnotations.SetEquals(SelectedAnnotations))
        {
        navigationBrush = null; cachedScene = new DrawingGroup(); cachedSceneStyle = style; sceneCenter = Center; sceneZoom = Zoom;
        sceneVertices = SelectedVertices.ToHashSet(); sceneEdges = SelectedEdges.ToHashSet(); sceneFaces = SelectedFaces.ToHashSet(); sceneAnnotations = SelectedAnnotations.ToHashSet();
        using (var dc = cachedScene.Open())
        {
        var selectedPoints = Exporting ? new HashSet<int>() : SelectionPoints();
        int selectedEdgeCount = selectedPoints.Count == 0 ? 0 : Plan.Edges.Count(e => SelectedEdges.Contains(e.Id) || (selectedPoints.Contains(e.A) && selectedPoints.Contains(e.B)));
        foreach (int level in Plan.Levels.Where(l => l == Plan.ActiveLevel || (ShowLowerLevels && l < Plan.ActiveLevel)).Order())
        {
        dc.PushOpacity(Plan.LevelOpacity(level));
        foreach (var face in facesByLevel.GetValueOrDefault(level, Array.Empty<Face>()).Where(FocusIncludesFace))
        {
            if (Plan.Fills.TryGetValue(face.Key, out var color)) { dc.PushOpacity(Plan.FillOpacity(level)); DrawFace(dc, face, FaceBrush(face, color)); dc.Pop(); }
            if (!Exporting && SelectedFaces.Contains(face.Key)) { dc.PushOpacity(.24); DrawFace(dc, face, Accent, outline: true); dc.Pop(); }
        }
        var allLevelEdges = edgesByLevel.GetValueOrDefault(level, Array.Empty<Edge>());
        var levelEdges = IsSelectionFocused ? allLevelEdges.Where(FocusIncludesEdge).ToArray() : allLevelEdges;
        if (hover != null && VertexAt(hover.A).Level == level) dc.DrawLine(CachedPen("#426A62", 9), Screen(VertexAt(hover.A).P), Screen(VertexAt(hover.B).P));
        DrawEdgeBatch(dc, levelEdges, selectedPoints, hover?.Id);
        DrawArrows(dc, levelEdges);
        if (ShowLengths || selectedPoints.Count > 0) foreach (var edge in levelEdges)
        {
            bool selected = !Exporting && (SelectedEdges.Contains(edge.Id) || (selectedPoints.Contains(edge.A) && selectedPoints.Contains(edge.B)));
            var a = Screen(VertexAt(edge.A).P); var b = Screen(VertexAt(edge.B).P);
            if (!edge.Dimension && (ShowLengths || (selected && selectedEdgeCount <= 8)))
            {
                string length = (VertexAt(edge.A).P - VertexAt(edge.B).P).Length.ToString("0.#", CultureInfo.InvariantCulture) + " u";
                var mid = new Point((a.X + b.X) / 2 + 7, (a.Y + b.Y) / 2 - 19);
                dc.DrawRoundedRectangle(Brush("#181F2A"), null, new Rect(mid.X - 4, mid.Y - 2, length.Length * 7 + 8, 19), 3, 3); Label(dc, length, mid, 11, selected ? Accent : Text);
            }
        }
        if (!Exporting) DrawVertexBatch(dc, verticesByLevel.GetValueOrDefault(level, Array.Empty<Vertex>()).Where(FocusIncludesVertex), selectedPoints);
        DrawAnnotations(dc, level);
        dc.Pop();
        }
        if (ShowUpperLevels)
        {
            DrawEdgeBatch(dc, Plan.Edges.Where(e => VertexAt(e.A).Level > Plan.ActiveLevel && FocusIncludesEdge(e)), selectedPoints, upper: true);
            if (!Exporting) foreach (int id in selectedPoints.Where(id => VertexAt(id).Level > Plan.ActiveLevel && FocusIncludesVertex(VertexAt(id)))) dc.DrawEllipse(Accent, null, Screen(VertexAt(id).P), 4, 4);
        }
        }
        cachedScene.Freeze();
        }

    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Exporting) { DrawViewport(dc); return; }
        using (var viewport = viewportDrawing.Open()) DrawViewport(viewport);
        dc.DrawDrawing(viewportDrawing);
    }

    private void DrawPanFrame(object? sender, EventArgs args)
    {
        CancelPanFrame();
        if (!panning || !IsLoaded || Exporting) return;
        mouseWorld = World(mouseScreen);
        using var viewport = viewportDrawing.Open();
        DrawViewport(viewport);
    }

    private void CancelPanFrame()
    {
        if (!panFrameQueued) return;
        panFrameQueued = false;
        CompositionTarget.Rendering -= DrawPanFrame;
    }

    private void SetPanning(bool value)
    {
        if (panning == value) return;
        panning = value;
        if (!value) CancelPanFrame();
        Host.SetMenuPanPaused(value);
    }

    internal void DrawViewport(DrawingContext dc, bool snapshot = false)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight); bool gpu = !snapshot && !Exporting && GlPlan?.Active == true;
        dc.DrawRectangle(gpu ? Brushes.Transparent : Brush("#181F2A"), null, bounds);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        if (!Exporting) Host.Rts?.ObserveCamera();
        if (!gpu && !IsSelectionFocused && reference != null && Plan.Background is { Visible: true } bg)
        { dc.PushOpacity(bg.Opacity); dc.DrawImage(reference, new Rect(Screen(new V2(bg.X, bg.Y)), new Size(bg.Width * Zoom, bg.Height * Zoom))); dc.Pop(); }
        var lo = World(new Point(0, ActualHeight)); var hi = World(new Point(ActualWidth, 0));
        double visibleStep = Plan.Grid; while (visibleStep * Zoom < 10) visibleStep *= 2;
        if (!gpu && ShowGrid)
        {
            for (double x = Math.Ceiling(lo.X / visibleStep) * visibleStep; x <= hi.X; x += visibleStep)
            { double sx = Math.Round(Screen(new V2(x, 0)).X) + .5; dc.DrawLine(new Pen(Math.Abs(x % (visibleStep * 8)) < .01 ? GridMajor : GridMinor, 1), new Point(sx, 0), new Point(sx, ActualHeight)); }
            for (double y = Math.Ceiling(lo.Y / visibleStep) * visibleStep; y <= hi.Y; y += visibleStep)
            { double sy = Math.Round(Screen(new V2(0, y)).Y) + .5; dc.DrawLine(new Pen(Math.Abs(y % (visibleStep * 8)) < .01 ? GridMajor : GridMinor, 1), new Point(0, sy), new Point(ActualWidth, sy)); }
            var origin = Screen(new V2());
            dc.DrawLine(new Pen(Brush("#41564F"), 1), new Point(origin.X, 0), new Point(origin.X, ActualHeight));
            dc.DrawLine(new Pen(Brush("#5C4548"), 1), new Point(0, origin.Y), new Point(ActualWidth, origin.Y));
        }
        PrepareScene(gpu && wheelPreview.IsEnabled);
        if (gpu)
        {
            var image = Plan.Background;
            bool show = !IsSelectionFocused && reference != null && image?.Visible == true;
            var area = show ? new Rect(Screen(new V2(image!.X, image.Y)), new Size(image.Width * Zoom, image.Height * Zoom)) : Rect.Empty;
            GlPlan!.Present(new(cachedScene!, sceneCenter, sceneZoom, Center, Zoom, RenderSize, ShowGrid, visibleStep,
                show ? reference : null, area, image?.Opacity ?? 1));
        }
        else DrawSceneForNavigation(dc);
        if (!Exporting)
        {
            Host.DrawGeometryToolGuide(dc, mouseWorld);
            if (Tool == EditorTool.Shapes && Shape != ShapeKind.Stairs && shapeStart != null)
            {
                var contour = Shapes.Contour(Shape, shapeStart.Value, mouseWorld, CirclePoints, Plan.Grid);
                if (contour.Count > 2)
                {
                    var geometry = new StreamGeometry(); using (var ctx = geometry.Open()) { ctx.BeginFigure(Screen(contour[0]), true, true); ctx.PolyLineTo(contour.Skip(1).Select(Screen).ToArray(), true, false); }
                    dc.PushOpacity(.12); dc.DrawGeometry(Accent, null, geometry); dc.Pop(); dc.DrawGeometry(null, new Pen(Accent, 1.8) { DashStyle = DashStyles.Dash }, geometry);
                    foreach (var point in contour) dc.DrawEllipse(Accent, null, Screen(point), 2.5, 2.5);
                    string size = Shape == ShapeKind.Rectangle ? L.F($"{Math.Abs(contour[2].X - contour[0].X):0.#} × {Math.Abs(contour[2].Y - contour[0].Y):0.#} u") : L.F($"Rayon {(contour[0] - shapeStart.Value).Length:0.#} u · {CirclePoints} points");
                    Label(dc, size, new Point(mouseScreen.X + 14, mouseScreen.Y - 24), 12, Accent);
                }
                dc.DrawEllipse(Accent, null, Screen(shapeStart.Value), 4, 4);
            }
            if (Tool == EditorTool.Shapes && Shape == ShapeKind.Stairs) DrawStairPreview(dc);
            DrawTransformGuide(dc);
            if (Tool is EditorTool.Draw or EditorTool.Dimension && IsMouseOver)
            {
                var snap = mouseWorld.Snap(Plan.Grid); var point = Screen(snap);
                if (Anchor != null)
                {
                    DrawStrokePreview(dc, Anchor.Value, snap);
                    if (Tool != EditorTool.Dimension) Label(dc, (snap - Anchor.Value).Length.ToString("0.#", L.Culture) + " u", new Point(point.X + 12, point.Y - 24), 12, Accent);
                    dc.DrawEllipse(Accent, null, Screen(Anchor.Value), 4, 4);
                }
                dc.DrawRectangle(null, new Pen(Accent, 1.5), new Rect(point.X - 5, point.Y - 5, 10, 10));
                dc.DrawLine(new Pen(Accent, 1), new Point(point.X - 10, point.Y), new Point(point.X + 10, point.Y));
                dc.DrawLine(new Pen(Accent, 1), new Point(point.X, point.Y - 10), new Point(point.X, point.Y + 10));
            }
            if (extruding)
            {
                var pen = new Pen(Accent, 2) { DashStyle = DashStyles.Dash };
                foreach (var pair in extrusionEdges)
                {
                    dc.DrawLine(pen, Screen(pair.A + extrusionDelta), Screen(pair.B + extrusionDelta));
                    dc.DrawLine(pen, Screen(pair.A), Screen(pair.A + extrusionDelta)); dc.DrawLine(pen, Screen(pair.B), Screen(pair.B + extrusionDelta));
                }
                Label(dc, L.F($"Extrusion   ΔX {extrusionDelta.X:0.#}   ΔY {extrusionDelta.Y:0.#}"), new Point(mouseScreen.X + 14, mouseScreen.Y - 25), 12, Accent);
            }
            if (marquee && moved) { dc.PushOpacity(.15); dc.DrawRectangle(Accent, null, new Rect(downScreen, mouseScreen)); dc.Pop(); dc.DrawRectangle(null, new Pen(Accent, 1), new Rect(downScreen, mouseScreen)); }
        }
        if (!Exporting || ExportGuides)
        {
            dc.DrawRectangle(Brush("#141B25"), null, new Rect(0, 0, ActualWidth, 24)); dc.DrawRectangle(Brush("#141B25"), null, new Rect(0, 24, 28, Math.Max(0, ActualHeight - 24)));
            double ruler = visibleStep; while (ruler * Zoom < 75) ruler *= 2;
            for (double x = Math.Ceiling(lo.X / ruler) * ruler; x <= hi.X; x += ruler) { var sx = Screen(new V2(x, 0)).X; Label(dc, x.ToString("0"), new Point(sx + 4, 4), 10); dc.DrawLine(new Pen(GridMajor, 1), new Point(sx, 19), new Point(sx, 24)); }
            for (double y = Math.Ceiling(lo.Y / ruler) * ruler; y <= hi.Y; y += ruler) { var sy = Screen(new V2(0, y)).Y; dc.PushTransform(new RotateTransform(-90, 8, sy - 3)); Label(dc, y.ToString("0"), new Point(8, sy - 3), 10); dc.Pop(); }
            if (!Exporting)
            {
                dc.DrawRoundedRectangle(Brush("#202A36"), new Pen(GridMajor, 1), new Rect(46, 42, 168, 31), 5, 5);
                Label(dc, L.T("DESSUS  /  XY"), new Point(59, 50), 10, Brush("#CAD5E1")); Label(dc, "2D", new Point(185, 50), 10, Accent);
            }
            double bar = visibleStep; while (bar * Zoom < 75) bar *= 2;
            double bottom = ActualHeight - 26;
            dc.DrawLine(new Pen(Text, 1), new Point(48, bottom), new Point(48 + bar * Zoom, bottom));
            dc.DrawLine(new Pen(Text, 1), new Point(48, bottom - 4), new Point(48, bottom + 4));
            dc.DrawLine(new Pen(Text, 1), new Point(48 + bar * Zoom, bottom - 4), new Point(48 + bar * Zoom, bottom + 4));
            Label(dc, L.F($"{bar:0} unités"), new Point(48, bottom - 22), 10);
            if (visibleStep != Plan.Grid) Label(dc, L.F($"Affichage allégé : 1 ligne / {visibleStep / Plan.Grid:0} · aimantation {Plan.Grid} u"), new Point(48, ActualHeight - 70), 11);
        }
    }
}

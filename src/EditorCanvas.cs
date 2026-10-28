// Coat — Epicteta Lupercal
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Coat;

public enum EditorTool { Select, Draw, Erase, Paint, None, Shapes, Annotation }
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
    private string? before;
    private Dictionary<int, V2> original = new();
    private List<(V2 A, V2 B)> extrusionEdges = new();
    private V2 extrusionDelta;
    private BitmapImage? reference;
    private string? referenceData;
    private List<Face> faces = new();
    private static readonly Brush GridMinor = Brush("#242D3A"), GridMajor = Brush("#323D4C"), Accent = Brush("#78DCC1"), Text = Brush("#99A7BA");
    public static SolidColorBrush Brush(string color)
    {
        try { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); b.Freeze(); return b; }
        catch { return new SolidColorBrush(Colors.LightGray); }
    }
    public EditorCanvas(MainWindow host)
    {
        Host = host; Focusable = true; ClipToBounds = true; Cursor = Cursors.Arrow;
        MouseDown += OnDown; MouseMove += OnMove; MouseUp += OnUp; MouseWheel += OnWheel;
        LostMouseCapture += (_, _) => { if (dragging || extruding || shaping) CancelGesture(); panning = false; marquee = false; };
        MouseLeave += (_, _) => { if (!IsMouseCaptured) { mouseScreen = new Point(-100, -100); InvalidateVisual(); } };
    }
    public Point Screen(V2 p) => new(ActualWidth / 2 + (p.X - Center.X) * Zoom, ActualHeight / 2 - (p.Y - Center.Y) * Zoom);
    public V2 World(Point p) => new(Center.X + (p.X - ActualWidth / 2) / Zoom, Center.Y - (p.Y - ActualHeight / 2) / Zoom);
    public void RefreshGeometry()
    {
        faces = Plan.Faces();
        if (!Plan.Annotations.Any(a => a.Id == SelectedAnnotation && a.Level == Plan.ActiveLevel)) SelectedAnnotation = null;
        if (referenceData != Plan.Background?.Data)
        {
            reference = null; referenceData = Plan.Background?.Data;
            if (!string.IsNullOrEmpty(referenceData)) reference = DecodeImage(referenceData);
        }
        SelectedVertices.RemoveWhere(id => !Plan.Vertices.Any(v => v.Id == id && v.Level == Plan.ActiveLevel));
        SelectedEdges.RemoveWhere(id => !Plan.Edges.Any(e => e.Id == id && Plan.LevelOf(e) == Plan.ActiveLevel));
        SelectedFaces.RemoveWhere(key => !faces.Any(f => f.Key == key && f.Level == Plan.ActiveLevel));
        InvalidateVisual();
    }
    public static BitmapImage DecodeImage(string data)
    {
        using var stream = new MemoryStream(Convert.FromBase64String(data));
        var bmp = new BitmapImage(); bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.StreamSource = stream; bmp.EndInit(); bmp.Freeze(); return bmp;
    }
    public void ClearSelection() { SelectedVertices.Clear(); SelectedEdges.Clear(); SelectedFaces.Clear(); SelectedAnnotation = null; }
    public HashSet<int> SelectionEdges()
    {
        var edges = new HashSet<int>(SelectedEdges);
        foreach (var face in faces.Where(f => SelectedFaces.Contains(f.Key))) edges.UnionWith(Plan.FaceEdges(face));
        return edges;
    }
    public void ExpandGroups()
    {
        var groups = new HashSet<int>(); bool changed;
        do
        {
            changed = false;
            foreach (var group in Plan.MatchingGroups(SelectedVertices, SelectionEdges()))
            {
                if (!groups.Add(group.Id)) continue;
                SelectedVertices.UnionWith(group.VertexIds.Where(id => Plan.Get(id).Level == Plan.ActiveLevel));
                SelectedEdges.UnionWith(Plan.Edges.Where(e => group.EdgeIds.Contains(e.Id) && Plan.LevelOf(e) == Plan.ActiveLevel).Select(e => e.Id)); changed = true;
            }
        } while (changed);
        foreach (var face in faces.Where(f => f.Level == Plan.ActiveLevel && Plan.FaceEdges(f).IsSubsetOf(SelectedEdges))) SelectedFaces.Add(face.Key);
    }
    private void SelectHit(int? vertex, int? edge, Face? face, bool ctrl)
    {
        var oldV = SelectedVertices.ToHashSet(); var oldE = SelectedEdges.ToHashSet(); var oldF = SelectedFaces.ToHashSet();
        ClearSelection(); if (vertex.HasValue) SelectedVertices.Add(vertex.Value); if (edge.HasValue) SelectedEdges.Add(edge.Value); if (face != null) SelectedFaces.Add(face.Key);
        ExpandGroups();
        bool already = SelectedVertices.IsSubsetOf(oldV) && SelectedEdges.IsSubsetOf(oldE) && SelectedFaces.IsSubsetOf(oldF);
        if (ctrl && already) { oldV.ExceptWith(SelectedVertices); oldE.ExceptWith(SelectedEdges); oldF.ExceptWith(SelectedFaces); ClearSelection(); }
        if ((ctrl && !already) || already) { SelectedVertices.UnionWith(oldV); SelectedEdges.UnionWith(oldE); SelectedFaces.UnionWith(oldF); }
    }
    public HashSet<int> SelectionPoints()
    {
        var ids = new HashSet<int>(SelectedVertices);
        foreach (var e in Plan.Edges.Where(e => SelectedEdges.Contains(e.Id))) { ids.Add(e.A); ids.Add(e.B); }
        foreach (var f in faces.Where(f => SelectedFaces.Contains(f.Key))) ids.UnionWith(f.Vertices);
        return ids;
    }
    public void SelectAll()
    {
        if (Tool != EditorTool.Select) return;
        ClearSelection();
        if (Mode == SelectionMode.Vertex) SelectedVertices.UnionWith(Plan.Vertices.Where(v => v.Level == Plan.ActiveLevel).Select(v => v.Id));
        else if (Mode == SelectionMode.Edge) SelectedEdges.UnionWith(Plan.Edges.Where(e => Plan.LevelOf(e) == Plan.ActiveLevel).Select(e => e.Id));
        else SelectedFaces.UnionWith(faces.Where(f => f.Level == Plan.ActiveLevel).Select(f => f.Key));
        ExpandGroups(); Host.UpdateInspector(); InvalidateVisual();
    }
    public void DeleteSelection()
    {
        if (SelectedAnnotation is int annotation) { Host.Mutate(() => Plan.Annotations.RemoveAll(a => a.Id == annotation)); ClearSelection(); Host.UpdateInspector(); return; }
        var vertices = new HashSet<int>(SelectedVertices); var edges = new HashSet<int>(SelectedEdges);
        foreach (var f in faces.Where(f => SelectedFaces.Contains(f.Key)))
            for (int i = 0; i < f.Vertices.Count; i++)
            { int a = f.Vertices[i], b = f.Vertices[(i + 1) % f.Vertices.Count]; foreach (var e in Plan.Edges.Where(e => (e.A == a && e.B == b) || (e.A == b && e.B == a))) edges.Add(e.Id); }
        if (vertices.Count + edges.Count == 0) return;
        Host.Mutate(() => { Plan.Edges.RemoveAll(e => edges.Contains(e.Id) || vertices.Contains(e.A) || vertices.Contains(e.B)); Plan.Vertices.RemoveAll(v => vertices.Contains(v.Id)); Plan.Normalize(); });
        ClearSelection(); Host.UpdateInspector();
    }
    private Edge? HitEdge(V2 p) => Plan.Edges.Where(e => Plan.LevelOf(e) == Plan.ActiveLevel && Plan.Distance(p, Plan.Get(e.A).P, Plan.Get(e.B).P) <= 9 / Zoom).OrderBy(e => Plan.Distance(p, Plan.Get(e.A).P, Plan.Get(e.B).P)).FirstOrDefault();
    private Vertex? HitVertex(V2 p) => Plan.Vertices.Where(v => v.Level == Plan.ActiveLevel && (p - v.P).Length <= 10 / Zoom).OrderBy(v => (p - v.P).Length).FirstOrDefault();
    private Face? HitFace(V2 p) => faces.Where(f => f.Level == Plan.ActiveLevel && Plan.Inside(p, f.Vertices.Select(id => Plan.Get(id).P).ToList())).OrderBy(f => f.Area).FirstOrDefault();
    public bool HasSelectionOrGesture => SelectedAnnotation != null || SelectedVertices.Count + SelectedEdges.Count + SelectedFaces.Count > 0 || Anchor != null || shapeStart != null || dragging || extruding || marquee;
    private static bool Shift => (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
    private static bool Ctrl => (Keyboard.Modifiers & ModifierKeys.Control) != 0;
    public void CancelGesture()
    {
        if (before != null && (dragging || extruding)) Host.Document = Plan.Parse(before);
        before = null; dragging = extruding = marquee = panning = shaping = false; Anchor = chainStart = shapeStart = null;
        ReleaseMouseCapture(); RefreshGeometry(); Host.UpdateInspector(); Host.UpdateStatus();
    }
    private Cursor ToolCursor => Tool == EditorTool.Select || Tool == EditorTool.None ? Cursors.Arrow : Cursors.Cross;
    public void SetTool(EditorTool t) { CancelGesture(); Tool = t; if (t == EditorTool.None) ClearSelection(); Cursor = ToolCursor; Host.UpdateInspector(); InvalidateVisual(); }
    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        PointerDown(e.GetPosition(this), e.ChangedButton, e.ClickCount, Shift, Ctrl, Keyboard.IsKeyDown(Key.Space));
    }
    // Both real WPF events and the regression suite use the same gesture controller.
    internal void PointerDown(Point position, MouseButton button, int clickCount = 1, bool shift = false, bool ctrl = false, bool space = false)
    {
        Focus(); downScreen = mouseScreen = position; downWorld = mouseWorld = World(downScreen); downCenter = Center; moved = false;
        if (button == MouseButton.Middle || button == MouseButton.Right || (button == MouseButton.Left && space))
        { panning = true; CaptureMouse(); Cursor = Cursors.Hand; return; }
        if (button != MouseButton.Left) return;
        if (Tool == EditorTool.None) return;
        if (AnnotationDown(downWorld)) return;
        if (Tool == EditorTool.Shapes)
        {
            if (shapeStart != null) FinishShape(downWorld);
            else { shapeStart = downWorld.Snap(Plan.Grid); shaping = true; CaptureMouse(); }
            InvalidateVisual(); return;
        }
        if (Tool == EditorTool.Draw)
        {
            if (clickCount > 1) { Anchor = chainStart = null; InvalidateVisual(); return; }
            var p = downWorld.Snap(Plan.Grid);
            if (Anchor == null) { Anchor = chainStart = p; }
            else if ((p - Anchor.Value).Length > 1e-6)
            {
                var start = Anchor.Value; Host.Mutate(() => Plan.Segment(start, p));
                if (chainStart != null && (p - chainStart.Value).Length < 1e-6) Anchor = chainStart = null;
                else Anchor = p;
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
            else { var face = HitFace(downWorld); if (face != null) Host.Mutate(() => { Plan.Fills[face.Key] = PaintColor; Plan.FillPatterns[face.Key] = PaintPattern; }); else Host.Notify(L.T("Fermez le contour pour pouvoir le remplir.")); }
            return;
        }
        bool hit = false;
        if (Mode == SelectionMode.Vertex)
        {
            var v = HitVertex(downWorld); if (v != null) { hit = true; SelectHit(v.Id, null, null, ctrl); }
        }
        else if (Mode == SelectionMode.Edge)
        {
            var edge = HitEdge(downWorld); if (edge != null) { hit = true; SelectHit(null, edge.Id, null, ctrl); }
        }
        else
        {
            var face = HitFace(downWorld); if (face != null) { hit = true; SelectHit(null, null, face, ctrl); }
        }
        // A closed shape already selected as vertices or edges can also be grabbed by its interior.
        // Keep precise vertex/edge hits first, and reserve Shift extrusion for actual edge hits.
        bool interior = false;
        if (!hit && !ctrl && Mode != SelectionMode.Face)
        {
            var points = SelectionPoints(); var edges = SelectionEdges();
            interior = faces.Any(f => f.Level == Plan.ActiveLevel &&
                (Mode == SelectionMode.Vertex ? f.Vertices.All(points.Contains) : Plan.FaceEdges(f).IsSubsetOf(edges)) &&
                Plan.Inside(downWorld, f.Vertices.Select(id => Plan.Get(id).P).ToList()));
            hit = interior;
        }
        if (hit && !ctrl)
        {
            before = Plan.Serialize();
            if (shift && Mode == SelectionMode.Edge && !interior && Transform == TransformKind.Translation)
            {
                extruding = true; extrusionDelta = new V2();
                extrusionEdges = Plan.Edges.Where(e => SelectedEdges.Contains(e.Id)).Select(e => (Plan.Get(e.A).P, Plan.Get(e.B).P)).ToList();
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
        var contour = Shapes.Contour(Shape, shapeStart.Value, end, CirclePoints, Plan.Grid);
        if (contour.Count < 3) return;
        shapeStart = null; shaping = false; ReleaseMouseCapture();
        Host.Mutate(() => Plan.Polygon(contour));
    }
    private void OnMove(object sender, MouseEventArgs e)
    {
        PointerMove(e.GetPosition(this), Shift);
    }
    internal void PointerMove(Point position, bool shift = false)
    {
        mouseScreen = position; mouseWorld = World(mouseScreen);
        if ((mouseScreen - downScreen).Length > 3) moved = true;
        if (panning) { var d = mouseScreen - downScreen; Center = downCenter + new V2(-d.X / Zoom, d.Y / Zoom); }
        else if (dragging && moved)
        {
            var delta = (mouseWorld - downWorld).Snap(Plan.Grid);
            if (SelectedAnnotation is int noteId) { var note = Plan.Annotations.First(a => a.Id == noteId); note.X = annotationOrigin.X + delta.X; note.Y = annotationOrigin.Y + delta.Y; }
            else if (Transform != TransformKind.Translation) UpdateTransformDrag(shift);
            else foreach (var pair in original) Plan.Get(pair.Key).P = pair.Value + delta;
        }
        else if (extruding && moved) extrusionDelta = (mouseWorld - downWorld).Snap(Plan.Grid);
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
            panning = false; ReleaseMouseCapture(); Cursor = ToolCursor; InvalidateVisual(); return;
        }
        if (button != MouseButton.Left) return;
        if (shaping)
        {
            shaping = false; ReleaseMouseCapture();
            if (moved) FinishShape(mouseWorld);
            InvalidateVisual(); return;
        }
        if (dragging || extruding)
        {
            if (extruding && moved && extrusionDelta.Length > 0)
            {
                var next = Plan.Extrude(SelectedEdges.ToArray(), extrusionDelta); SelectedEdges.Clear(); SelectedEdges.UnionWith(next);
            }
            else if (dragging && moved && SelectedAnnotation == null && original.Any(pair => (Plan.Get(pair.Key).P - pair.Value).Length > 1e-9)) NormalizeSelection();
            dragging = extruding = false;
            if (before != null) Host.Commit(before); before = null;
        }
        else if (marquee && moved)
        {
            var rect = new Rect(downScreen, mouseScreen);
            bool Contains(int id) => rect.Contains(Screen(Plan.Get(id).P));
            if (Mode == SelectionMode.Vertex) SelectedVertices.UnionWith(Plan.Vertices.Where(v => v.Level == Plan.ActiveLevel && Contains(v.Id)).Select(v => v.Id));
            else if (Mode == SelectionMode.Edge) SelectedEdges.UnionWith(Plan.Edges.Where(e => Plan.LevelOf(e) == Plan.ActiveLevel && Contains(e.A) && Contains(e.B)).Select(e => e.Id));
            else SelectedFaces.UnionWith(faces.Where(f => f.Level == Plan.ActiveLevel && f.Vertices.All(Contains)).Select(f => f.Key));
            ExpandGroups();
        }
        marquee = false; ReleaseMouseCapture(); RefreshGeometry(); Host.UpdateInspector();
    }
    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        var pos = e.GetPosition(this); var beforeZoom = World(pos);
        Zoom = Math.Clamp(Zoom * Math.Pow(1.15, e.Delta / 120.0), .05, 20);
        Center += beforeZoom - World(pos); Host.UpdateStatus(); InvalidateVisual(); e.Handled = true;
    }
    public void Fit()
    {
        var pts = Plan.Vertices.Where(v => v.Level == Plan.ActiveLevel || (v.Level < Plan.ActiveLevel ? ShowLowerLevels : ShowUpperLevels)).Select(v => v.P).Concat(Plan.AnnotationBounds(ShowLowerLevels)).ToList();
        if (Plan.Background is { Visible: true } b) { pts.Add(new V2(b.X, b.Y)); pts.Add(new V2(b.X + b.Width, b.Y - b.Height)); }
        if (pts.Count == 0) { Center = new V2(); Zoom = 1; }
        else
        {
            double x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y);
            Center = new V2((x0 + x1) / 2, (y0 + y1) / 2);
            Zoom = Math.Clamp(Math.Min(Math.Max(ActualWidth - 160, 100) / Math.Max(x1 - x0, 64), Math.Max(ActualHeight - 160, 100) / Math.Max(y1 - y0, 64)), .05, 20);
        }
        Host.UpdateStatus(); InvalidateVisual();
    }
    private void Label(DrawingContext dc, string text, Point point, double size = 11, Brush? brush = null)
    {
        var ft = new FormattedText(text, L.Culture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush ?? Text, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, point);
    }
    private Geometry Polygon(Face face)
    {
        var g = new StreamGeometry(); using (var ctx = g.Open()) { ctx.BeginFigure(Screen(Plan.Get(face.Vertices[0]).P), true, true); ctx.PolyLineTo(face.Vertices.Skip(1).Select(id => Screen(Plan.Get(id).P)).ToList(), true, false); } g.Freeze(); return g;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); var bounds = new Rect(0, 0, ActualWidth, ActualHeight); dc.DrawRectangle(Brush("#181F2A"), null, bounds);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        if (reference != null && Plan.Background is { Visible: true } bg)
        { dc.PushOpacity(bg.Opacity); dc.DrawImage(reference, new Rect(Screen(new V2(bg.X, bg.Y)), new Size(bg.Width * Zoom, bg.Height * Zoom))); dc.Pop(); }
        var lo = World(new Point(0, ActualHeight)); var hi = World(new Point(ActualWidth, 0));
        double visibleStep = Plan.Grid; while (visibleStep * Zoom < 10) visibleStep *= 2;
        if (ShowGrid)
        {
            for (double x = Math.Ceiling(lo.X / visibleStep) * visibleStep; x <= hi.X; x += visibleStep)
            { double sx = Math.Round(Screen(new V2(x, 0)).X) + .5; dc.DrawLine(new Pen(Math.Abs(x % (visibleStep * 8)) < .01 ? GridMajor : GridMinor, 1), new Point(sx, 0), new Point(sx, ActualHeight)); }
            for (double y = Math.Ceiling(lo.Y / visibleStep) * visibleStep; y <= hi.Y; y += visibleStep)
            { double sy = Math.Round(Screen(new V2(0, y)).Y) + .5; dc.DrawLine(new Pen(Math.Abs(y % (visibleStep * 8)) < .01 ? GridMajor : GridMinor, 1), new Point(0, sy), new Point(ActualWidth, sy)); }
            var origin = Screen(new V2());
            dc.DrawLine(new Pen(Brush("#41564F"), 1), new Point(origin.X, 0), new Point(origin.X, ActualHeight));
            dc.DrawLine(new Pen(Brush("#5C4548"), 1), new Point(0, origin.Y), new Point(ActualWidth, origin.Y));
        }
        Edge? hover = !Exporting && Tool == EditorTool.Select && Mode == SelectionMode.Edge && Shift ? HitEdge(mouseWorld) : null;
        var selectedPoints = Exporting ? new HashSet<int>() : SelectionPoints();
        int selectedEdgeCount = Plan.Edges.Count(e => SelectedEdges.Contains(e.Id) || (selectedPoints.Contains(e.A) && selectedPoints.Contains(e.B)));
        foreach (int level in Plan.Levels.Where(l => l == Plan.ActiveLevel || (ShowLowerLevels && l < Plan.ActiveLevel)).Order())
        {
        dc.PushOpacity(Plan.LevelOpacity(level));
        foreach (var face in faces.Where(f => f.Level == level).OrderByDescending(f => f.Area))
        {
            if (Plan.Fills.TryGetValue(face.Key, out var color)) { dc.PushOpacity(Plan.FillOpacity(level)); dc.DrawGeometry(DecorationBrush(color, Plan.FillPatterns.GetValueOrDefault(face.Key)), null, Polygon(face)); dc.Pop(); }
            if (!Exporting && SelectedFaces.Contains(face.Key)) { dc.PushOpacity(.24); dc.DrawGeometry(Accent, new Pen(Accent, 3), Polygon(face)); dc.Pop(); }
        }
        foreach (var edge in Plan.Edges.Where(e => Plan.LevelOf(e) == level))
        {
            bool selected = !Exporting && (SelectedEdges.Contains(edge.Id) || (selectedPoints.Contains(edge.A) && selectedPoints.Contains(edge.B)));
            var a = Screen(Plan.Get(edge.A).P); var b = Screen(Plan.Get(edge.B).P);
            if (hover?.Id == edge.Id) dc.DrawLine(new Pen(Brush("#426A62"), 9), a, b);
            dc.DrawLine(new Pen(selected || hover?.Id == edge.Id ? Accent : Brush(edge.Color), selected ? 2.8 : 2) { DashStyle = EdgeDash(edge.Style) }, a, b);
            if (ShowLengths || (selected && selectedEdgeCount <= 8))
            {
                string length = (Plan.Get(edge.A).P - Plan.Get(edge.B).P).Length.ToString("0.#", CultureInfo.InvariantCulture) + " u";
                var mid = new Point((a.X + b.X) / 2 + 7, (a.Y + b.Y) / 2 - 19);
                dc.DrawRoundedRectangle(Brush("#181F2A"), null, new Rect(mid.X - 4, mid.Y - 2, length.Length * 7 + 8, 19), 3, 3); Label(dc, length, mid, 11, selected ? Accent : Text);
            }
        }
        if (!Exporting)
        {
            foreach (var v in Plan.Vertices.Where(v => v.Level == level))
            {
                bool selected = selectedPoints.Contains(v.Id); double r = Mode == SelectionMode.Vertex && Tool == EditorTool.Select ? 4 : 2.6;
                dc.DrawEllipse(selected ? Accent : Brush("#202A36"), new Pen(selected ? Accent : Brush("#B0BDC9"), 1), Screen(v.P), r, r);
            }
        }
        DrawAnnotations(dc, level);
        dc.Pop();
        }
        if (ShowUpperLevels)
        {
            var upperPen = new Pen(Brush("#FF2020"), .75);
            foreach (var edge in Plan.Edges.Where(e => Plan.LevelOf(e) > Plan.ActiveLevel))
                dc.DrawLine(upperPen, Screen(Plan.Get(edge.A).P), Screen(Plan.Get(edge.B).P));
        }
        if (!Exporting)
        {
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
            if (Tool == EditorTool.Draw && IsMouseOver)
            {
                var snap = mouseWorld.Snap(Plan.Grid); var point = Screen(snap);
                if (Anchor != null)
                {
                    var pen = new Pen(Accent, 1.8) { DashStyle = DashStyles.Dash };
                    dc.DrawLine(pen, Screen(Anchor.Value), point);
                    Label(dc, (snap - Anchor.Value).Length.ToString("0.#", L.Culture) + " u", new Point(point.X + 12, point.Y - 24), 12, Accent);
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

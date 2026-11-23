// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace Coat;

public partial class MainWindow
{
    private (TextBox X, TextBox Y) AxisFields(string title, string x, string y, string xName, string yName)
    {
        Heading(title);
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition());
        TextBox Add(string axis, string value, string name, int column)
        {
            var cell = new DockPanel { Margin = new Thickness(column == 0 ? 0 : 8, 0, 0, 0) };
            cell.Children.Add(new TextBlock { Text = axis, Width = 20, VerticalAlignment = VerticalAlignment.Center, Foreground = B("#A7B6C9") });
            var input = new TextBox { Text = value, MinWidth = 48, Padding = new Thickness(5, 6, 5, 6) };
            System.Windows.Automation.AutomationProperties.SetName(input, name); NumericInput(input); cell.Children.Add(input);
            Grid.SetColumn(cell, column); row.Children.Add(cell); return input;
        }
        var a = Add("X", x, xName, 0); var b = Add("Y", y, yName, 1); inspector.Children.Add(row); return (a, b);
    }
    private record LineChoice(string Title, ImageSource Image);
    private static DataTemplate StrokeChoiceTemplate() => (DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><StackPanel Orientation=\"Horizontal\"><Image Source=\"{Binding Image}\" Width=\"68\" Height=\"20\" Margin=\"0,0,8,0\"/><TextBlock Text=\"{Binding Title}\" VerticalAlignment=\"Center\"/></StackPanel></DataTemplate>");
    private void DrawInspector()
    {
        Heading(L.T("STYLE DES LIGNES"));
        var ids = Document.Edges.Where(e => Canvas.SelectionEdges().Contains(e.Id) && !e.Dimension).Select(e => e.Id).ToHashSet();
        var choice = new ComboBox { Height = 34, Margin = new Thickness(0, 0, 0, 10) };
        choice.ItemTemplate = StrokeChoiceTemplate();
        string[] names = { L.T("Continu"), L.T("Long"), L.T("Court") };
        for (int i = 0; i < names.Length; i++)
        {
            var drawing = new DrawingGroup(); using (var dc = drawing.Open())
            { dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 100, 18)); dc.DrawLine(new Pen(B("#DFE8F2"), 2) { DashStyle = EditorCanvas.EdgeDash((LineStyle)i) }, new Point(0, 9), new Point(100, 9)); }
            drawing.Freeze(); choice.Items.Add(new LineChoice(names[i], new DrawingImage(drawing)));
        }
        System.Windows.Automation.AutomationProperties.SetName(choice, L.T("STYLE DES LIGNES"));
        choice.SelectedIndex = (int)Canvas.DrawAppearance.Style;
        choice.SelectionChanged += (_, _) => { bool dimension = false; var style = (LineStyle)choice.SelectedIndex; void Apply() { Canvas.DrawAppearance.Dimension = dimension; Canvas.DrawAppearance.Style = style; foreach (var edge in Document.Edges.Where(e => ids.Contains(e.Id))) { edge.Style = style; edge.Dimension = dimension; } } if (ids.Count > 0) Mutate(Apply); else { Apply(); UpdateInspector(); } };
        inspector.Children.Add(choice); ArrowControls(ids);
        ToolHint(L.T("Ces réglages s’appliquent aux nouveaux traits et aux arêtes sélectionnées. Sélectionnez une arête puis ouvrez Draw pour modifier ses flèches."));
    }
}
public partial class EditorCanvas
{
    public Edge DrawAppearance { get; } = new();
    public Edge DimensionAppearance { get; } = new() { Dimension = true };
    internal Edge ActiveStroke => Tool == EditorTool.Dimension ? DimensionAppearance : DrawAppearance;
    private void InitializeDimensionAppearance()
    {
        var ids = SelectionEdges(); var source = Plan.Edges.FirstOrDefault(e => e.Dimension && ids.Contains(e.Id));
        if (source != null) { DimensionAppearance.Color = source.Color; DimensionAppearance.CopyArrows(source); }
        DimensionAppearance.Dimension = true; DimensionAppearance.Style = LineStyle.Solid;
    }
    private void InitializeDrawAppearance()
    {
        var ids = SelectionEdges(); var source = Plan.Edges.FirstOrDefault(e => ids.Contains(e.Id));
        if (source != null) { DrawAppearance.Style = source.Style; DrawAppearance.Color = source.Color; DrawAppearance.CopyArrows(source); }
    }
    private void DrawStrokePreview(DrawingContext dc, V2 a, V2 b)
    {
        var appearance = ActiveStroke;
        dc.DrawLine(new Pen(Accent, 1.8) { DashStyle = EdgeDash(appearance.Style) }, Screen(a), Screen(b));
        StrokeDecoration.Draw(dc, Screen(a), Screen(b), a, b, appearance, Zoom, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private (int X, int Y)? scaleHandle;
    private V2 scaleReference;
    private double scaleFactorX = 1, scaleFactorY = 1;
    internal List<(int X, int Y, Point Position)> ScaleHandles()
    {
        var points = SelectionPoints().Select(id => Screen(VertexAt(id).P)).ToList();
        var result = new List<(int, int, Point)>(); if (points.Count < 2) return result;
        double l = points.Min(p => p.X), r = points.Max(p => p.X), t = points.Min(p => p.Y), b = points.Max(p => p.Y);
        // Keep handles separated and hittable even for a tiny selection or a single straight line.
        bool wide = r - l > 1e-6, tall = b - t > 1e-6;
        double cx = (l + r) / 2, cy = (t + b) / 2; l = Math.Min(l, cx - 22); r = Math.Max(r, cx + 22); t = Math.Min(t, cy - 22); b = Math.Max(b, cy + 22);
        foreach (int x in new[] { -1, 0, 1 }) foreach (int y in new[] { -1, 0, 1 })
            if ((x != 0 || y != 0) && (x == 0 || wide) && (y == 0 || tall)) result.Add((x, y, new Point(x < 0 ? l : x > 0 ? r : cx, y > 0 ? t : y < 0 ? b : cy)));
        return result;
    }
    private bool ScaleReady => Tool == EditorTool.Select && Transform == TransformKind.Scale && SelectedAnnotations.Count == 0;
    private (int X, int Y, Point Position)? HitScaleHandle() => ScaleReady ? ScaleHandles().Where(h => Math.Abs(h.Position.X - mouseScreen.X) <= 10 && Math.Abs(h.Position.Y - mouseScreen.Y) <= 10).Select(h => ((int X, int Y, Point Position)?)h).FirstOrDefault() : null;
    private void UpdateScaleCursor()
    {
        var hit = HitScaleHandle(); Cursor = hit == null ? ToolCursor : hit.Value.X == 0 ? Cursors.SizeNS : hit.Value.Y == 0 ? Cursors.SizeWE : hit.Value.X == hit.Value.Y ? Cursors.SizeNESW : Cursors.SizeNWSE;
    }
    private bool TryBeginScaleHandle()
    {
        var handle = HitScaleHandle(); if (handle == null) return false;
        before = Plan.Serialize(); BeginTransformDrag(); scaleHandle = (handle.Value.X, handle.Value.Y);
        var points = original.Values; double minX = points.Min(p => p.X), maxX = points.Max(p => p.X), minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
        transformPivot = new(handle.Value.X < 0 ? maxX : handle.Value.X > 0 ? minX : (minX + maxX) / 2, handle.Value.Y < 0 ? maxY : handle.Value.Y > 0 ? minY : (minY + maxY) / 2);
        scaleReference = new(handle.Value.X * Math.Max(maxX - minX, 44 / Zoom), handle.Value.Y * Math.Max(maxY - minY, 44 / Zoom)); scaleFactorX = scaleFactorY = 1;
        CaptureMouse(); InvalidateVisual(); return true;
    }
    private void UpdateScaleHandle(bool uniform)
    {
        var delta = mouseWorld - downWorld; var handle = scaleHandle!.Value;
        double x = handle.X == 0 ? 1 : 1 + delta.X / scaleReference.X, y = handle.Y == 0 ? 1 : 1 + delta.Y / scaleReference.Y;
        if (uniform && handle.X != 0 && handle.Y != 0) x = y = Math.Abs(x - 1) >= Math.Abs(y - 1) ? x : y;
        scaleFactorX = Math.Clamp(x, .01, 1000); scaleFactorY = Math.Clamp(y, .01, 1000);
        var next = original.ToDictionary(p => p.Key, p => SelectionTransform.Scale(p.Value, transformPivot, scaleFactorX, scaleFactorY));
        if (next.Values.All(SelectionTransform.Valid)) foreach (var pair in next) VertexAt(pair.Key).P = pair.Value;
    }
    private void DrawScaleHandles(DrawingContext dc)
    {
        var handles = ScaleHandles(); if (handles.Count == 0) return;
        double l = handles.Min(h => h.Position.X), r = handles.Max(h => h.Position.X), t = handles.Min(h => h.Position.Y), b = handles.Max(h => h.Position.Y);
        dc.DrawRectangle(null, new Pen(Accent, 1) { DashStyle = DashStyles.Dash }, new Rect(new Point(l, t), new Point(r, b)));
        foreach (var h in handles)
        {
            var color = h.Y == 0 ? Brush("#F39A9A") : h.X == 0 ? Brush("#91C5FF") : Accent;
            dc.DrawRectangle(Brush("#18242D"), new Pen(color, 2), new Rect(h.Position.X - 6, h.Position.Y - 6, 12, 12));
        }
        if (dragging && moved) Label(dc, $"X ×{scaleFactorX:0.##}   Y ×{scaleFactorY:0.##}", new Point(mouseScreen.X + 16, mouseScreen.Y - 26), 12, Accent);
    }
    internal ContextMenu BuildSelectionMenu(V2 position)
    {
        if (Tool == EditorTool.Select && !HasSelectionOrGesture)
        {
            if (Mode == SelectionMode.Vertex && HitVertex(position) is { } vertex) SelectHit(vertex.Id, null, null, false);
            else if (Mode == SelectionMode.Edge && HitEdge(position) is { } edge) SelectHit(null, edge.Id, null, false);
            else if (Mode == SelectionMode.Face && HitFace(position) is { } face) SelectHit(null, null, face, false);
            Host.UpdateInspector(); InvalidateVisual();
        }
        var menu = new ContextMenu { Resources = Host.Resources, BorderBrush = Brushes.LightGray, Background = Brushes.White, Foreground = Brushes.Black };
        void Item(string name, Action action, bool enabled)
        { var item = new MenuItem { Header = L.T(name), IsEnabled = enabled }; if (Host.Shortcuts.Bindings.TryGetValue(name, out var key) && key.Key != Key.None) item.InputGestureText = key.ToString(); item.Click += (_, _) => action(); menu.Items.Add(item); }
        bool selected = SelectionPoints().Count > 0 || SelectedAnnotations.Count > 0;
        Item("Dissoudre les sommets", () => DissolveSelection(true), Tool == EditorTool.Select && SelectedVertices.Count > 0 && SelectedAnnotations.Count == 0);
        Item("Dissoudre les arêtes", () => DissolveSelection(false), Tool == EditorTool.Select && SelectionEdges().Count > 0 && SelectedAnnotations.Count == 0);
        menu.Items.Add(new Separator()); Item("Supprimer", DeleteSelection, selected); Item("Dupliquer", Host.Duplicate, selected);
        return menu;
    }
    private void OpenSelectionMenu(V2 position) { var menu = BuildSelectionMenu(position); menu.PlacementTarget = this; menu.IsOpen = true; }
    internal void DissolveSelection(bool vertices)
    {
        var ids = vertices ? SelectedVertices.ToHashSet() : SelectionEdges(); if (ids.Count == 0) return;
        CancelGesture(); int changed = 0;
        Host.Mutate(() => { changed = vertices ? Plan.DissolveVertices(ids) : Plan.DissolveEdges(ids); if (changed > 0) ClearSelection(); });
        if (changed < ids.Count) Host.Notify(L.T("Les éléments qui ne peuvent pas être dissous sans ouvrir un contour ou supprimer une jonction sont conservés."));
    }
}

// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Coat;

internal static class Update03Tests
{
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        Plan Rooms()
        {
            var p = new Plan();
            p.Polygon(Shapes.Contour(ShapeKind.Rectangle, new(0, 0), new(128, 128), 32, 32));
            p.Polygon(Shapes.Contour(ShapeKind.Rectangle, new(320, 0), new(448, 128), 32, 32));
            foreach (var face in p.Faces()) p.Fills[face.Key] = "#66C5AD";
            return p;
        }
        var p = new Plan { ActiveLevel = 2 };
        check(p.LevelOpacity(2) == 1 && p.LevelOpacity(1) > p.LevelOpacity(0) && p.LevelOpacity(0) > p.LevelOpacity(-1), "Level opacity progressively decreases with floor distance");
        check(Enumerable.Range(-999, 1002).All(l => p.LevelOpacity(l) > 0) && p.LevelOpacity(3) == 0, "Every lower floor retains nonzero opacity while higher floors stay hidden");
        check(p.FillOpacity(2) == .88 && p.FillOpacity(1) == .65, "Active-level fills have increased opacity");
        var corners = Shapes.Contour(ShapeKind.Rectangle, new(130, 150), new(-1, -2), 32, 32);
        check(corners.Count == 4 && corners.Distinct().Count() == 4 && corners.All(v => v.X % 32 == 0 && v.Y % 32 == 0), "Rectangle has four grid-snapped corners in any drag direction");
        check(Shapes.Contour(ShapeKind.Rectangle, new(0, 0), new(0, 128), 32, 32).Count == 0, "Zero-width rectangles are rejected");
        foreach (int n in new[] { 3, 8, 32, 128 })
        {
            var circle = Shapes.Contour(ShapeKind.Circle, new(0, 0), new(128, 0), n, 32);
            p = new Plan(); p.Polygon(circle);
            check(p.Vertices.Count == n && p.Edges.Count == n && p.Faces().Count == 1 && circle.All(v => Math.Abs(v.Length - 128) < 1e-6), $"Circle with {n} points is regular, closed and fillable");
        }
        bool invalid = false; try { Shapes.Contour(ShapeKind.Circle, new(), new(128, 0), 2, 32); } catch (ArgumentOutOfRangeException) { invalid = true; }
        check(invalid, "Circle precision rejects fewer than three points");
        p = Rooms(); var group = p.GroupSelection(Array.Empty<int>(), p.Edges.Select(e => e.Id))!;
        check(p.Groups.Count == 1 && group.EdgeIds.Count == 8 && p.GroupPoints(group).Count == 8, "Grouping disconnected filled contours preserves all members");
        check(Plan.Parse(p.Serialize()).Serialize() == p.Serialize(), "Groups survive project save and reload");
        p = new Plan(); p.Segment(new(0, 0), new(128, 0)); group = p.GroupSelection(Array.Empty<int>(), p.Edges.Select(e => e.Id))!; p.Segment(new(64, -64), new(64, 64));
        check(group.EdgeIds.Count == 2 && group.EdgeIds.All(id => p.Edges.Any(e => e.Id == id)) && p.Edges.Count == 4, "Splitting a grouped edge retains both fragments without grouping the crossing line");
        p = Rooms(); group = p.GroupSelection(Array.Empty<int>(), p.Edges.Select(e => e.Id))!; int groupId = group.Id;
        p.TransferToLevel(Array.Empty<int>(), group.EdgeIds.ToArray(), Array.Empty<string>(), 1); p.ActiveLevel = 1;
        check(p.Groups.Single().Id == groupId && p.Groups.Single().EdgeIds.Count == 8 && p.Edges.All(e => p.LevelOf(e) == 1) && p.Fills.Count == 2, "Assigning a group to another level preserves members and fills");
        p.DeleteLevel(1); check(p.Groups.Count == 0 && p.Edges.Count == 0 && p.Fills.Count == 0 && p.Levels.SequenceEqual(new[] { 0 }), "Deleting a populated level removes its geometry, fills and empty groups");
        p.DeleteLevel(0); check(p.Levels.SequenceEqual(new[] { 0 }) && p.ActiveLevel == 0, "Deleting the final level leaves a usable empty level zero");
        p = Rooms(); p.EnsureLevel(2); p.EnsureLevel(7); p.ActiveLevel = 2; p.DeleteLevel(2);
        check(p.Levels.SequenceEqual(new[] { 0, 7 }) && p.ActiveLevel == 0 && p.Edges.Count == 8, "Deleting an empty level preserves other level numbers and geometry");
        var old = Rooms(); old.Version = 2; var migrated = Plan.Parse(old.Serialize());
        check(migrated.Version == 6 && migrated.Groups.Count == 0 && migrated.Fills.Count == 2, "Version 0.2 projects migrate without losing levels or colors");
        p = Rooms(); p.Groups.Add(new GeometryGroup { Id = 100, EdgeIds = new() { 999999 } }); invalid = false;
        try { Plan.Parse(p.Serialize()); } catch (InvalidDataException) { invalid = true; }
        check(invalid, "Reject group references to missing geometry");

        var canvas = window.Canvas; window.Width = 1440; window.Height = 900; window.UpdateLayout();
        void Click(V2 point, bool ctrl = false) { canvas.PointerDown(canvas.Screen(point), MouseButton.Left, ctrl: ctrl); canvas.PointerUp(MouseButton.Left); }
        void Drag(V2 a, V2 b) { canvas.PointerDown(canvas.Screen(a), MouseButton.Left); canvas.PointerMove(canvas.Screen(b)); canvas.PointerUp(MouseButton.Left); }
        void Load(Plan document) { canvas.CancelGesture(); window.LoadDocument(document, ""); canvas.Zoom = 1; canvas.Center = new(); }
        void Press(Key key) => window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Load(new Plan()); Press(Key.R); canvas.Shape = ShapeKind.Rectangle;
        Click(new(0, 0)); check(window.Document.Edges.Count == 0 && canvas.HasSelectionOrGesture, "First shape click starts a preview without changing the project");
        Click(new(128, 128)); check(window.Document.Vertices.Count == 4 && window.Document.Faces().Count == 1, "Two-click rectangle gesture creates four editable vertices");
        window.Undo(); check(window.Document.Edges.Count == 0 && !window.History.CanUndo, "A complete shape is undone in one operation"); window.Redo();
        check(window.Document.Edges.Count == 4, "Redo restores the entire shape");
        Drag(new(320, 128), new(448, 0)); check(window.Document.Edges.Count == 8, "Drag gesture also creates a rectangle");
        Click(new(640, 0)); window.Escape(); check(!canvas.HasSelectionOrGesture && canvas.Tool == EditorTool.Select && window.Document.Edges.Count == 8, "Escape cancels an unfinished shape and returns to Select");
        window.SetTool(EditorTool.Shapes); canvas.Shape = ShapeKind.Circle; canvas.CirclePoints = 12; Drag(new(-256, 0), new(-128, 0));
        check(window.Document.Vertices.Count == 20 && window.Document.Faces().Count == 3, "Circle gesture uses the configured point count");
        window.UpdateInspector(); window.UpdateLayout(); Pump();
        var countBox = Controls<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Points"); countBox.Text = "24";
        check(canvas.CirclePoints == 24, "Circle precision can be changed from the inspector");
        window.ChangeLevel(1); Click(new(0, 0)); Click(new(128, 0));
        check(window.Document.Vertices.Count(v => v.Level == 1) == 24 && window.Document.Faces().Single(f => f.Level == 1).Vertices.Count == 24, "Two-click circles are drawn on the active level");

        Load(Rooms()); window.SetTool(EditorTool.Select); canvas.Mode = SelectionMode.Face; Click(new(64, 64)); Click(new(384, 64), ctrl: true); window.GroupSelection();
        check(window.Document.Groups.Count == 1 && window.Document.Groups[0].EdgeIds.Count == 8, "Editor groups a multi-face selection");
        window.Undo(); check(window.Document.Groups.Count == 0, "Creating a group is undoable"); window.Redo();
        canvas.ClearSelection(); canvas.Mode = SelectionMode.Edge; Click(new(64, 0));
        check(canvas.SelectionEdges().Count == 8 && canvas.SelectionPoints().Count == 8, "Clicking a grouped edge selects the entire group");
        var beforeMove = window.Document.Vertices.ToDictionary(v => v.Id, v => v.P); Drag(new(64, 0), new(96, -32));
        check(window.Document.Vertices.All(v => v.P == beforeMove[v.Id] + new V2(32, -32)) && window.Document.Fills.Count == 2, "Dragging one group member moves all contours and their colors");
        Click(new(96, -32), ctrl: true); check(canvas.SelectionPoints().Count == 0, "Ctrl-click toggles a whole group off");
        Click(new(96, -32)); window.UngroupSelection(); check(window.Document.Groups.Count == 0, "Ungroup removes grouping without deleting geometry");
        canvas.ClearSelection(); Click(new(96, -32)); check(canvas.SelectedEdges.Count == 1, "Ungrouped members can be selected individually");
        window.Undo(); canvas.ClearSelection(); Click(new(96, -32)); check(canvas.SelectionEdges().Count == 8, "Undo restores group selection behaviour");
        window.UpdateInspector(); window.UpdateLayout(); Pump();
        TextBox MoveBox(string name) => Controls<TextBox>(window).Single(t => AutomationProperties.GetName(t) == name);
        Button ActionButton(string name) => Controls<Button>(window).Single(b => b.Content as string == name);
        MoveBox("Décalage X").Text = "64"; MoveBox("Décalage Y").Text = "-32";
        var moveStart = window.Document.Vertices.ToDictionary(v => v.Id, v => v.P);
        ActionButton("Déplacer la sélection").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); Pump();
        check(MoveBox("Décalage X").Text == "64" && MoveBox("Décalage Y").Text == "-32", "Move inspector retains the entered X and Y values after applying");
        ActionButton("Déplacer la sélection").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); Pump();
        check(window.Document.Vertices.All(v => v.P == moveStart[v.Id] + new V2(128, -64)), "The same retained displacement can be applied repeatedly");
        window.SetTool(EditorTool.Paint); window.SetTool(EditorTool.Select); window.UpdateLayout(); Pump();
        check(MoveBox("Décalage X").Text == "64" && MoveBox("Décalage Y").Text == "-32", "Move values also survive inspector rebuilds and tool switches");
        window.Duplicate(); check(window.Document.Groups.Count == 2 && window.Document.Groups.Select(g => g.Id).Distinct().Count() == 2, "Duplicating a group creates an independent group");
        check(window.Document.Groups.All(g => g.EdgeIds.All(id => window.Document.Edges.Any(e => e.Id == id))), "Duplicated groups retain valid members after intersections are normalized");
        window.Undo(); canvas.Mode = SelectionMode.Edge; canvas.SelectAll(); window.AssignSelectionToLevel(3);
        check(window.Document.Groups.Count == 1 && window.Document.Groups[0].EdgeIds.All(id => window.Document.LevelOf(window.Document.Edges.First(e => e.Id == id)) == 3), "Level assignment carries the selected group intact");
        string beforeDelete = window.Document.Serialize(); window.DeleteActiveLevel();
        check(!window.Document.Levels.Contains(3) && window.Document.Groups.Count == 0 && window.Document.Edges.Count == 0, "Delete-level command clears the selected level and its group");
        window.Undo(); check(window.Document.Serialize() == beforeDelete, "Undo level deletion restores groups, colors, geometry and active level");
        window.Redo(); check(!window.Document.Levels.Contains(3), "Redo repeats level deletion");
        var sparse = Rooms(); sparse.EnsureLevel(1); sparse.EnsureLevel(2); sparse.ActiveLevel = 1; Load(sparse); window.DeleteActiveLevel(); window.ChangeAdjacentLevel(1);
        check(window.Document.ActiveLevel == 2 && !window.Document.Levels.Contains(1), "Level navigation skips a deleted interior level instead of recreating it");

        var gradient = new Plan();
        for (int level = 0; level <= 2; level++)
        {
            gradient.ActiveLevel = level; double x = -320 + level * 224; gradient.Polygon(Shapes.Contour(ShapeKind.Rectangle, new(x, 0), new(x + 128, 128), 32, 32)); gradient.Fills[gradient.HitFace(new(x + 64, 64))!.Key] = "#66C5AD";
        }
        Load(gradient); window.SetTool(EditorTool.None); canvas.ShowGrid = false; canvas.Exporting = true; canvas.InvalidateVisual(); Pump();
        byte[] Pixel(V2 p)
        {
            var image = new RenderTargetBitmap((int)canvas.ActualWidth, (int)canvas.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(canvas); var at = canvas.Screen(p); var pixel = new byte[4]; image.CopyPixels(new Int32Rect((int)at.X, (int)at.Y, 1, 1), pixel, 4, 0); return pixel;
        }
        int far = Pixel(new(-256, 64))[1], near = Pixel(new(-32, 64))[1], active = Pixel(new(192, 64))[1];
        check(active > near && near > far && far > 31 && active > 170, "Rendered lower levels remain distinguishable and the active fill is denser");
        var svg = MainWindow.Svg(gradient);
        check(svg.Contains("opacity=\"0.36\"") && svg.Contains("opacity=\"0.6\"") && svg.Contains("fill-opacity=\"0.88\""), "SVG export matches progressive opacity and the increased active fill");
        canvas.Exporting = false; canvas.ShowGrid = true;

        var demo = new Plan(); demo.ActiveLevel = 0; demo.Polygon(Shapes.Contour(ShapeKind.Rectangle, new(-384, -192), new(384, 192), 32, 32)); demo.Fills[demo.Faces().Single().Key] = "#596E97";
        demo.ActiveLevel = 1; demo.Polygon(Shapes.Contour(ShapeKind.Rectangle, new(-320, -128), new(-64, 128), 32, 32)); demo.Fills[demo.HitFace(new(-192, 0))!.Key] = "#D9AC6E"; demo.Polygon(Shapes.Contour(ShapeKind.Circle, new(192, 0), new(352, 0), 16, 32)); demo.Fills[demo.HitFace(new(192, 0))!.Key] = "#AD91D6";
        demo.ActiveLevel = 2; demo.Polygon(Shapes.Contour(ShapeKind.Rectangle, new(-288, -64), new(-96, 96), 32, 32)); demo.Fills[demo.HitFace(new(-192, 0))!.Key] = "#66C5AD"; demo.Polygon(Shapes.Contour(ShapeKind.Circle, new(192, 0), new(320, 0), 32, 32)); demo.Fills[demo.HitFace(new(192, 0))!.Key] = "#D38D9E"; demo.GroupSelection(Array.Empty<int>(), demo.Edges.Where(e => demo.LevelOf(e) == 2).Select(e => e.Id));
        Load(demo); window.SetTool(EditorTool.Shapes); canvas.Shape = ShapeKind.Circle; canvas.CirclePoints = 32; canvas.ShowLengths = false; canvas.Fit(); window.UpdateInspector(); window.UpdateLayout(); Pump();
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-03-shapes.png")); MainWindow.WriteAtomic(Path.Combine(output, "Exemple-shapes-groupes.coat"), demo.Serialize());
        window.SetTool(EditorTool.Select); canvas.Mode = SelectionMode.Edge; Click(new(-192, -64)); window.UpdateLayout(); Pump(); MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-03-groupes.png"));
        window.Width = 1050; window.Height = 680; window.UpdateLayout(); canvas.Fit(); Pump(); MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-03-compact.png"));
    }
    private static IEnumerable<T> Controls<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T control) yield return control;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var item in Controls<T>(VisualTreeHelper.GetChild(root, i))) yield return item;
    }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
}

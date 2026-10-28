// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Coat;
internal static class Update07Tests
{
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        var canvas = window.Canvas;
        bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;
        Plan Rectangle()
        {
            var p = new Plan(); p.Polygon(new V2[] { new(0, 0), new(128, 0), new(128, 64), new(0, 64) });
            var face = p.Faces().Single(); p.Fills[face.Key] = "#66C5AD"; p.FillPatterns[face.Key] = FillPattern.Dots; p.GroupSelection(Array.Empty<int>(), p.Edges.Select(e => e.Id)); return p;
        }
        void Load(Plan p)
        {
            canvas.CancelGesture(); window.LoadDocument(p, ""); canvas.Transform = TransformKind.Translation; canvas.Mode = SelectionMode.Edge;
            canvas.Center = new(); canvas.Zoom = 1; canvas.ShowUpperLevels = false; window.SetTool(EditorTool.Select); window.UpdateLayout(); Pump();
        }
        void Drag(V2 from, V2 to, bool shift = false)
        { canvas.PointerDown(canvas.Screen(from), MouseButton.Left, shift: shift); canvas.PointerMove(canvas.Screen(to), shift); canvas.PointerUp(MouseButton.Left); }
        void Click(V2 p) { canvas.PointerDown(canvas.Screen(p), MouseButton.Left); canvas.PointerUp(MouseButton.Left); }
        TextBox Field(string name) => Desc<TextBox>(window).Single(t => AutomationProperties.GetName(t) == name);
        Button ActionButton(string name) => Desc<Button>(window).Single(b => b.Content as string == name);
        Load(Rectangle()); canvas.SelectAll(); var before = window.Document.Serialize(); window.RotateSelection(90);
        check(Near(window.Document.Vertices.Min(v => v.X), 32) && Near(window.Document.Vertices.Max(v => v.Y), 96) && Near(window.Document.Faces().Single().Area, 8192), "Numeric rotation uses the selection center and preserves area");
        check(window.Document.Groups.Count == 1 && window.Document.Fills.Values.Single() == "#66C5AD" && window.Document.FillPatterns.Values.Single() == FillPattern.Dots && canvas.SelectionPoints().Count == 4, "Rotation retains grouping, appearance and selection");
        window.Undo(); check(window.Document.Serialize() == before, "Numeric rotation is undone in one step"); window.Redo(); check(Near(window.Document.Vertices.Min(v => v.X), 32), "Numeric rotation redo restores the rotated geometry");
        Load(Rectangle()); canvas.SelectAll(); window.ScaleSelection(2, .5);
        check(Near(window.Document.Vertices.Max(v => v.X) - window.Document.Vertices.Min(v => v.X), 256) && Near(window.Document.Vertices.Max(v => v.Y) - window.Document.Vertices.Min(v => v.Y), 32) && canvas.SelectionCenter == new V2(64, 32), "Independent X/Y scale factors preserve the center");
        window.ScaleSelection(-1, 1); check(window.Document.Fills.Count == 1 && window.Document.FillPatterns.Values.Single() == FillPattern.Dots, "Mirroring retains the colored face and motif");
        before = window.Document.Serialize(); window.ScaleSelection(0, 1); window.ScaleSelection(double.NaN, 1); window.ScaleSelection(1001, 1); window.RotateSelection(double.PositiveInfinity);
        check(window.Document.Serialize() == before, "Zero and invalid transforms cannot corrupt the document");
        Load(Rectangle()); canvas.SelectAll(); before = window.Document.Serialize(); canvas.SetTransform(TransformKind.Rotation); Drag(new(128, 64), new(32, 96));
        check(Near(window.Document.Vertices.Min(v => v.X), 32) && Near(window.Document.Vertices.Max(v => v.Y), 96) && window.Document.Edges.Count == 4, "Rotation mouse gesture rotates the full selection without extrusion");
        window.Undo(); check(window.Document.Serialize() == before, "Mouse rotation is a single undo transaction");
        canvas.SelectAll(); canvas.SetTransform(TransformKind.Rotation); var snapPivot = canvas.SelectionCenter;
        Drag(new(128, 64), SelectionTransform.Rotate(new(128, 64), snapPivot, 37), shift: true);
        var expected = SelectionTransform.Rotate(new(128, 64), snapPivot, 30);
        check(window.Document.Edges.Count == 4 && window.Document.Vertices.Any(v => (v.P - expected).Length < 1e-6), "Shift rotation snaps to 15 degrees without triggering edge extrusion");
        window.Undo();
        canvas.SelectAll(); canvas.SetTransform(TransformKind.Scale); Drag(new(128, 64), new(192, 96));
        check(Near(window.Document.Vertices.Max(v => v.X) - window.Document.Vertices.Min(v => v.X), 256) && Near(window.Document.Vertices.Max(v => v.Y) - window.Document.Vertices.Min(v => v.Y), 128), "Mouse scale grows uniformly around the selection center");
        before = window.Document.Serialize(); canvas.PointerDown(canvas.Screen(new(192, 96)), MouseButton.Left); canvas.PointerMove(canvas.Screen(new(256, 128))); window.Escape();
        check(window.Document.Serialize() == before && !canvas.HasSelectionOrGesture, "Escape cancels an in-progress scale without leaving partial changes");
        canvas.SelectAll(); canvas.SetTransform(TransformKind.Rotation); window.UpdateLayout(); Pump();
        Field("Angle (°)").Text = "45"; ActionButton("Appliquer la rotation").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); Pump();
        check(Field("Angle (°)").Text == "45" && window.Document.Groups.Count == 1, "Rotation inspector applies and retains the entered angle");
        Desc<Button>(window).Single(b => AutomationProperties.GetName(b) == "Échelle").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); Pump();
        Field("Facteur").Text = "0,5"; ActionButton("Appliquer l’échelle").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); Pump();
        check(canvas.Transform == TransformKind.Scale && Field("Facteur").Text == "0,5", "Scale mode button and inspector accept and retain a French decimal factor");
        check(Desc<Image>(window).Count(i => i.Source is DrawingImage) >= 3, "Transform menu displays vector translation, rotation and cube-scale icons");
        foreach (var kind in Enum.GetValues<StairKind>())
        {
            var lines = Stairs.Build(kind, new(), new(384, 0), 12, 96, 270, true, 32); var p = new Plan(); var group = p.AddStair(lines, kind);
            check(p.Faces().Count == 12 + (kind is StairKind.L or StairKind.U ? 1 : 0), $"{kind}: generated geometry has twelve treads and the expected landing");
            check(group.EdgeIds.Count == p.Edges.Count && group.EdgeIds.All(id => p.Edges.Any(e => e.Id == id)) && Plan.Parse(p.Serialize()).Groups.Count == 1, $"{kind}: complete stair geometry forms a valid saved group");
        }
        var right = Stairs.Build(StairKind.Straight, new(), new(320, 0), 10, 96, 270, true, 32);
        var left = Stairs.Build(StairKind.Straight, new(), new(-320, 0), 10, 96, 270, true, 32);
        var up = Stairs.Build(StairKind.Straight, new(), new(0, 320), 10, 96, 270, true, 32);
        var down = Stairs.Build(StairKind.Straight, new(), new(0, -320), 10, 96, 270, true, 32);
        check(right.SelectMany(l => new[] { l.A.X, l.B.X }).Max() == 320 && left.SelectMany(l => new[] { l.A.X, l.B.X }).Min() == -320 && up.SelectMany(l => new[] { l.A.Y, l.B.Y }).Max() == 320 && down.SelectMany(l => new[] { l.A.Y, l.B.Y }).Min() == -320, "Stair drag direction supports right, left, up and down");
        var diagonal = Stairs.Build(StairKind.Straight, new(), new(300, 70), 10, 96, 270, true, 32);
        check(diagonal.SelectMany(l => new[] { l.A.Y, l.B.Y }).Max() == 48 && diagonal.SelectMany(l => new[] { l.A.X, l.B.X }).Max() == 288, "Diagonal mouse movement chooses the dominant snapped axis");
        var lRight = Stairs.Build(StairKind.L, new(), new(384, 0), 9, 96, 270, true, 32); var lLeft = Stairs.Build(StairKind.L, new(), new(384, 0), 9, 96, 270, false, 32);
        check(lRight.Zip(lLeft).All(pair => pair.First.A.X == pair.Second.A.X && pair.First.A.Y == -pair.Second.A.Y), "Left/right turn options mirror the stair consistently");
        var odd = new Plan(); odd.AddStair(lRight, StairKind.L); check(odd.Faces().Count == 10, "Odd step counts distribute across the two flights without losing a step");
        var spiral = new Plan(); spiral.AddStair(Stairs.Build(StairKind.Spiral, new(), new(192, 0), 24, 96, 360, true, 32), StairKind.Spiral);
        check(spiral.Faces().Count == 25 && spiral.Edges.All(e => (spiral.Get(e.A).P - spiral.Get(e.B).P).Length > 1e-6), "Full-turn spiral has 24 treads, central opening, and no zero-length edges");
        foreach (var (count, angle) in new[] { (2, 15), (128, 360) })
        {
            var limit = new Plan(); limit.AddStair(Stairs.Build(StairKind.Spiral, new(), new(192, 0), count, 96, angle, false, 32), StairKind.Spiral);
            check(limit.Faces().Count == count + (angle == 360 ? 1 : 0), $"Spiral limits remain valid at {count} steps and {angle} degrees");
        }
        bool rejected = false; try { Stairs.Build(StairKind.Spiral, new(), new(192, 0), 1, 96, 0, true, 32); } catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected && Stairs.Build(StairKind.Straight, new(), new(), 12, 96, 270, true, 32).Count == 0, "Invalid parameters and zero-size stairs are rejected");
        Load(new Plan { ActiveLevel = 2, Levels = new() { 0, 2 } }); window.SetTool(EditorTool.Shapes); canvas.Shape = ShapeKind.Stairs; canvas.Stair = StairKind.Straight; canvas.StairSteps = 8;
        Drag(new(0, 0), new(256, 0));
        check(window.Document.Faces().Count == 8 && window.Document.Vertices.All(v => v.Level == 2) && window.Document.Groups.Count == 1, "Stair drag creates the requested treads on the active level");
        window.Undo(); check(window.Document.Edges.Count == 0, "A complete staircase is undone in one step"); window.Redo(); check(window.Document.Groups.Count == 1, "Redo restores a complete grouped staircase");
        canvas.Stair = StairKind.U; canvas.StairSteps = 10; Click(new(640, 0)); Click(new(960, 0));
        check(window.Document.Groups.Count == 2 && window.Document.Faces().Count == 19, "Two-click staircase creation supports U stairs and their landing");
        before = window.Document.Serialize(); Click(new(-512, 0)); canvas.PointerMove(canvas.Screen(new(-256, 0))); window.Escape(); check(window.Document.Serialize() == before, "Escape cancels an unfinished staircase preview"); window.SetTool(EditorTool.Shapes);
        window.UpdateInspector(); window.UpdateLayout(); Pump();
        Desc<ComboBox>(window).Single(c => AutomationProperties.GetName(c) == "Type d’escalier").SelectedIndex = (int)StairKind.Spiral; window.UpdateLayout(); Pump();
        Field("Marches").Text = "18"; Field("Angle (°)").Text = "180";
        check(canvas.Stair == StairKind.Spiral && canvas.StairSteps == 18 && canvas.StairAngle == 180, "Stair inspector exposes type, step count and spiral angle");
        var demo = new Plan();
        void Add(StairKind kind, V2 start, V2 end, string title, V2 label)
        {
            demo.AddStair(Stairs.Build(kind, start, end, 12, 96, 270, true, 32), kind);
            demo.Annotations.Add(new Annotation { Id = demo.NextId++, Text = title, X = label.X, Y = label.Y, Size = 22 });
        }
        Add(StairKind.Straight, new(-640, 288), new(-256, 288), "DROIT · 12 marches", new(-640, 400));
        Add(StairKind.L, new(0, 320), new(384, 320), "EN L · palier", new(0, 432));
        Add(StairKind.U, new(-640, -96), new(-256, -96), "EN U · demi-tour", new(-640, 16));
        Add(StairKind.Spiral, new(160, -320), new(352, -320), "COLIMAÇON · 270°", new(-16, -64));
        Load(demo); window.Width = 1440; window.Height = 1000; canvas.Fit(); window.SetTool(EditorTool.Shapes); canvas.Shape = ShapeKind.Stairs; window.UpdateInspector(); window.UpdateLayout(); Pump();
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-07-escaliers.png")); window.ExportPngTo(Path.Combine(output, "coat-07-plan.png"));
        demo.ViewX = canvas.Center.X; demo.ViewY = canvas.Center.Y; demo.Zoom = canvas.Zoom; MainWindow.WriteAtomic(Path.Combine(output, "Exemple-escaliers.coat"), demo.Serialize());
        window.SetTool(EditorTool.Select); canvas.SelectedEdges.UnionWith(demo.Groups[0].EdgeIds); canvas.ExpandGroups(); canvas.SetTransform(TransformKind.Rotation); window.UpdateLayout(); Pump();
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-07-transformations.png"));
        before = demo.Serialize(); window.RotateSelection(90); check(demo.Groups.Count == 4 && demo.Groups[0].EdgeIds.All(id => demo.Edges.Any(e => e.Id == id)), "Generated staircase can be rotated as a complete group"); window.Undo(); check(window.Document.Serialize() == before, "Undo restores the original stair geometry after rotation");
    }
    private static IEnumerable<T> Desc<T>(DependencyObject parent) where T : DependencyObject { if (parent is T item) yield return item; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var child in Desc<T>(VisualTreeHelper.GetChild(parent, i))) yield return child; }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
}

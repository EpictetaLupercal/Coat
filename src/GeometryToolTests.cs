// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Coat;

internal static class GeometryToolTests
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var results = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            results.Add("PASS: " + name);
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow { TestMode = true, ShowInTaskbar = false, Width = 1050, Height = 680 };
        window.Shortcuts.AnimatedMenus = false;
        window.ChangeLanguage("en");
        window.Show();
        var canvas = window.Canvas;
        void Layout() => InterfaceLayoutTests.Layout(window);
        Button Command(string name) => InterfaceLayoutTests.Children<Button>(window).Single(b =>
            AutomationProperties.GetName(b) == name || Equals(b.Content, name));
        void Invoke(string name)
        {
            Layout();
            Command(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        void Click(double x, double y)
        {
            canvas.PointerDown(canvas.Screen(new(x, y)), MouseButton.Left);
            canvas.PointerUp(MouseButton.Left);
        }
        Plan Fixture()
        {
            var plan = new Plan();
            Rectangle(plan, 0, 0, 128, 128);
            var face = plan.Faces().Single();
            plan.Fills[face.Key] = "#906533";
            plan.FillPatterns[face.Key] = FillPattern.Dots;
            return plan;
        }
        void Load(Plan plan)
        {
            window.SetTool(EditorTool.Select);
            window.LoadDocument(plan, "");
            canvas.Zoom = 1;
            canvas.Center = new V2();
            Layout();
        }

        var plan = Fixture();
        plan.Gradients[plan.Faces().Single().Key] = new("#AABBCC", 30);
        plan.Edges[0].Style = LineStyle.LongDash;
        Load(plan);
        canvas.Mode = SelectionMode.Face;
        canvas.SelectAll();
        Invoke("Mirror");
        var before = plan.Serialize();
        Check(canvas.Tool == EditorTool.Mirror && Command("Mirror").IsVisible, "Mirror opens from the compact top toolbar");
        Click(0, 0);
        Invoke("Apply Mirror");
        Check(window.Document.Vertices.Max(v => v.X) == 0 && window.Document.Vertices.Min(v => v.X) == -128 &&
            window.Document.Gradients.Values.Single().Angle == 150, "Vertical mirroring reflects positions and gradient direction");
        window.Undo();
        Check(window.Document.Serialize() == before, "Mirror is a single undoable edit");
        window.SetTool(EditorTool.Select);
        canvas.SelectAll();
        Invoke("Mirror");
        Layout();
        InterfaceLayoutTests.Children<CheckBox>(window).Single(c => Equals(c.Content, "Keep original")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(window.ApplyMirror(true, -32, true) && window.Document.Faces().Count == 2 && window.Document.Fills.Count == 2 &&
            window.Document.Edges.Count(e => e.Style == LineStyle.LongDash) == 2, "Horizontal reflected copies retain the original, fills, and line styles");

        foreach (var (index, name, expected) in new[] { (0, "Union", 22528d), (1, "Difference", 10240d), (2, "Intersection", 6144d) })
        {
            var scene = Fixture();
            scene.EnsureLevel(1); scene.ActiveLevel = 1;
            Rectangle(scene, 0, 0, 128, 128); scene.ActiveLevel = 0;
            Load(scene);
            string untouched = scene.Serialize();
            Invoke("Modifiers");
            Click(32, 32);
            Invoke("Draw rectangle B");
            Click(32, 64);
            Click(224, 128);
            Layout();
            InterfaceLayoutTests.Children<ComboBox>(window).Single(c => AutomationProperties.GetName(c) == "Boolean operation").SelectedIndex = index;
            Check(window.Document.Serialize() == untouched, $"{name} preview leaves the plan untouched");
            Invoke("Apply Boolean");
            var result = window.Document;
            var faces = result.Faces();
            Check(Math.Abs(faces.Single(f => f.Level == 0).Area - expected) < .01 && faces.Single(f => f.Level == 1).Area == 16384,
                $"{name} applies the expected area and preserves the other level");
            Check(result.Fills.Values.Single() == "#906533" && result.FillPatterns.Values.Single() == FillPattern.Dots,
                $"{name} preserves the target fill and pattern");
            window.Undo();
            Check(window.Document.Serialize() == untouched, $"{name} is a single undoable edit");
        }

        Load(Fixture()); Invoke("Modifiers"); Click(24, 24); Invoke("Draw rectangle B"); Click(32, 32); Click(96, 96);
        Layout(); InterfaceLayoutTests.Children<ComboBox>(window).Single(c => AutomationProperties.GetName(c) == "Boolean operation").SelectedIndex = 1;
        Invoke("Apply Boolean");
        var hole = window.Document.HitFace(new(64, 64))!;
        var outside = window.Document.HitFace(new(16, 16))!;
        Check(!window.Document.Fills.ContainsKey(hole.Key) && window.Document.Fills.ContainsKey(outside.Key) && !canvas.FillContains(outside.Key, new(64, 64)),
            "A rectangle inside the target cuts an unpainted hole");

        var adjacent = Fixture(); Rectangle(adjacent, 128, 0, 256, 128); Rectangle(adjacent, 256, 0, 384, 128);
        adjacent.Fills[adjacent.HitFace(new(320, 64))!.Key] = "#224466";
        Load(adjacent); Invoke("Modifiers"); Click(64, 64); Click(192, 64); Layout();
        InterfaceLayoutTests.Children<ComboBox>(window).Single(c => AutomationProperties.GetName(c) == "Boolean operation").SelectedIndex = 0;
        Invoke("Apply Boolean");
        Check(window.Document.HitFace(new(64, 64))!.Area == 32768 && window.Document.Edges.All(e =>
            window.Document.Get(e.A).X != 128 || window.Document.Get(e.B).X != 128), "Union of existing adjacent areas removes the shared seam");
        Check(window.Document.HitFace(new(320, 64)) is { Area: 16384 } neighbour && window.Document.Fills[neighbour.Key] == "#224466",
            "Union preserves a third neighbouring room and its fill");
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-modifiers.png"));
        Check(L.Missing.Count == 0, "The new tools and controls have English labels");
        window.Close(); app.Shutdown();
        File.WriteAllLines(Path.Combine(output, "results.txt"), results.Append($"\n{results.Count} checks passed."));
    }

    private static void Rectangle(Plan plan, double left, double bottom, double right, double top) =>
        plan.Polygon(new V2[] { new(left, bottom), new(right, bottom), new(right, top), new(left, top) });
}

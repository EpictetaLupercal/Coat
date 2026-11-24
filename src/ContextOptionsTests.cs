// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Coat;

internal static class ContextOptionsTests
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
        window.Shortcuts.Bindings["Rotation"] = new(Key.F8, ModifierKeys.None);
        window.Shortcuts.Bindings["Cercle"] = new(Key.F9, ModifierKeys.None);
        window.Shortcuts.Bindings["Sélection de points"] = new(Key.F7, ModifierKeys.None);
        window.ChangeLanguage("en");
        window.Show();
        var canvas = window.Canvas;
        var plan = new Plan();
        plan.Polygon(new V2[] { new(0, 0), new(128, 0), new(128, 64), new(0, 64) });
        window.LoadDocument(plan, "");
        void Layout() => InterfaceLayoutTests.Layout(window);
        static IEnumerable<T> Children<T>(DependencyObject node) where T : DependencyObject => InterfaceLayoutTests.Children<T>(node);
        Border Bar() => Children<Border>(window).Single(b => AutomationProperties.GetName(b) == L.T("Options de l’outil"));
        Button ButtonInBar(string title) => Children<Button>(Bar()).Single(b => AutomationProperties.GetName(b) == L.T(title));
        TextBox Field(string label) => Children<TextBox>(Bar()).Single(b => AutomationProperties.GetName(b) == L.T(label));
        static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void Press(UIElement target, Key key) => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        void Capture(string name) => MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, name));

        Layout();
        var noToolY = canvas.TranslatePoint(new Point(), window).Y;
        Check(Bar().Visibility == Visibility.Collapsed, "The contextual row is collapsed with no tool");
        window.SetTool(EditorTool.Select);
        canvas.Mode = SelectionMode.Edge;
        canvas.SelectAll();
        Layout();
        Check(Bar().IsVisible && Bar().ActualHeight > 0 && canvas.TranslatePoint(new Point(), window).Y > noToolY,
            "Selection shows a separate row between the main toolbar and the canvas");
        var dx = Field("Décalage X"); var dy = Field("Décalage Y");
        Check(dx.TranslatePoint(new Point(), window).Y == dy.TranslatePoint(new Point(), window).Y &&
            Children<TextBox>(window).Count(b => AutomationProperties.GetName(b) == L.T("Décalage X")) == 1,
            "Move X and Y share one row with one copy of each field");
        dx.Text = "10+6";
        Press(dx, Key.Enter); Layout();
        Check(window.Document.Vertices.Min(v => v.X) == 16 && Field("Décalage X").Text == "16", "Enter evaluates and applies the move in the contextual bar");
        Press(canvas, Key.F8); Layout();
        var angle = Field("Angle (°)");
        Check(canvas.Transform == TransformKind.Rotation && ButtonInBar("Rotation").Background == window.ChromeBrush("#304E48"),
            "The custom Rotate shortcut updates the active contextual control");
        angle.Text = "90";
        Press(angle, Key.Enter); Layout();
        Check(window.Document.Vertices.Max(v => v.Y) - window.Document.Vertices.Min(v => v.Y) == 128, "The angle field applies rotation");
        Press(canvas, Key.S); Layout();
        var x = Field("Facteur X"); var y = Field("Facteur Y");
        x.Text = "2";
        Check(!y.IsEnabled && y.Text == "2", "Uniform scaling displays both axes and keeps the second synchronized");
        Click(ButtonInBar("Échelle uniforme")); Layout();
        y = Field("Facteur Y");
        Check(y.IsEnabled && y.Text == "1", "Unlocking restores the independent Y factor");
        y.Text = "0.5";
        Press(y, Key.Enter); Layout();
        Check(window.Document.Vertices.Max(v => v.X) - window.Document.Vertices.Min(v => v.X) == 128 &&
            window.Document.Vertices.Max(v => v.Y) - window.Document.Vertices.Min(v => v.Y) == 64, "X and Y scale the selection independently");
        Capture("coat-context-selection.png");

        foreach (string language in new[] { "en", "fr" })
        {
            window.ChangeLanguage(language); Layout();
            var modes = new[] { "Sélection de points", "Sélection d’arêtes", "Sélection de zones" };
            var buttons = modes.Select(ButtonInBar).ToArray();
            var move = ButtonInBar("Translation");
            Check(buttons.All(b => b.IsVisible && b.TranslatePoint(new Point(b.ActualWidth, 0), window).X < move.TranslatePoint(new Point(), window).X) &&
                modes.All(mode => Children<Button>(window).Count(b => AutomationProperties.GetName(b) == L.T(mode)) == 1),
                $"Only the contextual bar contains the three selection mode buttons, before the transforms, in {language}");
            Press(canvas, Key.F7); Layout();
            Check(canvas.Mode == SelectionMode.Vertex && ButtonInBar("Sélection de points").Background == window.ChromeBrush("#304E48"),
                $"The configured vertex shortcut updates the contextual highlight in {language}");
            Click(ButtonInBar("Sélection de zones")); Layout();
            Check(canvas.Mode == SelectionMode.Face && ButtonInBar("Sélection de zones").Background == window.ChromeBrush("#304E48"),
                $"The contextual face button changes and highlights the mode in {language}");
            Click(ButtonInBar("Sélection d’arêtes")); Layout();
            Check(canvas.Mode == SelectionMode.Edge && ButtonInBar("Sélection d’arêtes").Background == window.ChromeBrush("#304E48"),
                $"The contextual edge button changes and highlights the mode in {language}");
        }
        window.ChangeLanguage("en");

        window.SetTool(EditorTool.Draw); Layout();
        Check(!Bar().IsVisible && canvas.TranslatePoint(new Point(), window).Y == noToolY, "Draw collapses the row and returns its space to the canvas");
        canvas.ClearSelection();
        Press(canvas, Key.F9); Layout();
        Check(canvas.Tool == EditorTool.Shapes && canvas.Shape == ShapeKind.Circle && ButtonInBar("Cercle").Background == window.ChromeBrush("#304E48"),
            "The custom Circle shortcut changes the active shape icon");
        Check(!Children<Button>(Bar()).Any(b => AutomationProperties.GetName(b) == L.T("Sélection de points")), "Shape options do not include selection mode buttons");
        var vertices = Field("Points");
        vertices.Text = "16+8";
        Press(vertices, Key.Enter);
        Check(canvas.CirclePoints == 24, "Circle count accepts arithmetic and Enter");
        Click(Children<Button>(Bar()).Single(b => AutomationProperties.GetName(b) == L.F($"Augmenter {L.T("Points")}")));
        Check(canvas.CirclePoints == 25, "The point stepper increments the count");
        window.Activate(); vertices.Focus(); Layout();
        vertices.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
        Check(canvas.CirclePoints == 24, "Mouse wheel changes a focused contextual numeric field");
        Click(ButtonInBar("Rectangle")); Layout();
        Check(canvas.Shape == ShapeKind.Rectangle && !Children<TextBox>(Bar()).Any(), "Rectangle has no irrelevant numeric options");
        Click(ButtonInBar("Escaliers")); Layout();
        var choice = Children<ComboBox>(Bar()).Single();
        choice.SelectedIndex = (int)StairKind.U; Layout();
        Check(canvas.Stair == StairKind.U && Children<TextBox>(Bar()).Count() == 1 &&
            Children<TextBox>(window).Any(b => AutomationProperties.GetName(b) == L.T("Largeur (u)")), "Stairs expose the type and count at the top and retain width in the inspector");
        var steps = Field("Marches"); steps.Text = "4*3"; Press(steps, Key.Enter);
        Check(canvas.StairSteps == 12, "Stair count accepts arithmetic and Enter");
        Children<ComboBox>(Bar()).Single().SelectedIndex = (int)StairKind.Spiral; Layout();
        angle = Field("Angle (°)"); angle.Text = "90*2"; Press(angle, Key.Enter);
        Check(canvas.Stair == StairKind.Spiral && canvas.StairAngle == 180 && canvas.StairSteps == 12, "Spiral adds its angle alongside the preserved count");

        foreach (string language in new[] { "en", "fr" })
        {
            window.ChangeLanguage(language); Layout();
            var row = (Panel)Bar().Child;
            Check(row.Children.Cast<FrameworkElement>().All(element => element.IsVisible && element.TranslatePoint(new Point(element.ActualWidth, 0), window).X <= window.ActualWidth),
                $"All contextual stair controls fit at minimum width in {language}");
            Check(new[] { L.T("Plan"), L.T("Sélection"), L.T("Modificateurs") }.All(name => Children<Button>(window).Any(b => AutomationProperties.GetName(b) == name)),
                $"All inspector tabs remain available in {language}");
            Capture("coat-context-shapes-" + language + ".png");
        }
        Check(L.Missing.Count == 0, "All new contextual controls are localized");

        window.Close(); app.Shutdown();
        File.WriteAllLines(Path.Combine(output, "results.txt"), results.Append($"\n{results.Count} checks passed."));
    }
}

// Coat — Epicteta Lupercal
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Coat;
internal static class Update105Tests
{
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        var defaults = new ShortcutSettings(); ShortcutSettings.Validate(defaults.Bindings);
        bool Resolves(ShortcutSettings settings, Key key, Key command, ModifierKeys mods = ModifierKeys.None)
            => settings.Translate(ref key, ref mods) && key == command;
        check(Resolves(defaults, Key.Z, Key.P) && Resolves(defaults, Key.X, Key.L) && Resolves(defaults, Key.A, Key.Home), "Ergonomic keys resolve to stable vertex, edge and frame commands");
        check(Resolves(defaults, Key.S, Key.S, ModifierKeys.Control) && Resolves(defaults, Key.Z, Key.Z, ModifierKeys.Control), "Save and Undo keep their standard shortcuts");
        var classic = ShortcutSettings.ClassicBindings();
        foreach (var action in new[] { "Niveaux inférieurs visibles", "Translation", "Rotation", "Échelle", "Préférences" }) classic.Remove(action);
        classic["Dessin"] = new(Key.Z, ModifierKeys.None);
        string path = Path.Combine(output, "preferences-105.json");
        File.WriteAllText(path, JsonSerializer.Serialize(classic)); var migrated = ShortcutSettings.Load(path);
        check(migrated.Bindings["Dessin"].Key == Key.Z && migrated.Bindings["Sélection de points"].Key == Key.P && migrated.Bindings["Sélection d’arêtes"].Key == Key.X, "Migration preserves a custom key that conflicts with a new default");
        check(migrated.Bindings.Count == ShortcutSettings.Actions.Count && migrated.Bindings.Values.Distinct().Count() == migrated.Bindings.Count, "Migration adds new actions without duplicate bindings");
        classic["Dessin"] = new(Key.D, ModifierKeys.None);
        File.WriteAllText(path, JsonSerializer.Serialize(new { Bindings = classic, Language = "fr" })); migrated = ShortcutSettings.Load(path);
        check(migrated.Bindings["Sélection de points"].Key == Key.Z && migrated.Bindings["Cadrer"].Key == Key.A && migrated.Language == "fr", "Legacy defaults migrate to ergonomic keys while retaining French");
        new ShortcutSettings { Bindings = ShortcutSettings.ClassicBindings() }.Save(path);
        check(ShortcutSettings.Load(path).Bindings["Sélection de points"].Key == Key.P, "Explicit Classic preset survives a save and reload without remigration");

        window.Shortcuts = new(); window.ChangeLanguage("en"); window.Width = 1440; window.Height = 900;
        var canvas = window.Canvas; window.LoadDocument(new Plan(), ""); window.UpdateLayout(); Pump();
        void Press(UIElement target, Key key) => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        foreach (var tool in Enum.GetValues<EditorTool>())
        {
            window.SetTool(tool); Press(window, Key.Escape);
            check(canvas.Tool == EditorTool.Select && !canvas.HasSelectionOrGesture, $"Escape returns from {tool} to an empty selection");
        }
        Press(window, Key.Z); check(canvas.Mode == SelectionMode.Vertex, "Z activates vertex selection through the window handler");
        Press(window, Key.X); check(canvas.Mode == SelectionMode.Edge, "X activates edge selection through the window handler");
        foreach (var (label, transform) in new[] { ("Translation", TransformKind.Translation), ("Rotation", TransformKind.Rotation), ("Échelle", TransformKind.Scale) })
        {
            window.Shortcuts = new(); window.Shortcuts.Bindings[label] = new(Key.F7, ModifierKeys.None); Press(window, Key.F7);
            check(canvas.Tool == EditorTool.Select && canvas.Transform == transform, $"Remapped {label} selects the requested transform");
        }
        window.Shortcuts = new(); window.Shortcuts.Bindings["Niveaux inférieurs visibles"] = new(Key.F8, ModifierKeys.None);
        bool lower = canvas.ShowLowerLevels; Press(window, Key.F8); check(canvas.ShowLowerLevels != lower, "Remapped lower-level visibility toggles independently"); canvas.ShowLowerLevels = lower;
        window.Shortcuts = new(); window.ChangeLanguage("en"); window.UpdateLayout(); Pump();
        var view = Desc<Menu>(window).Single().Items.OfType<MenuItem>().Single(m => (string)m.Header == "View");
        check(view.Items.OfType<MenuItem>().Single(m => (string)m.Header == "Show Lower Levels").InputGestureText == "Shift+H", "View menu displays the lower-level shortcut");

        window.SetTool(EditorTool.Shapes); canvas.Shape = ShapeKind.Circle; window.UpdateInspector(); window.UpdateLayout(); Pump();
        TextBox Field(string name) => Desc<TextBox>(window).Single(t => AutomationProperties.GetName(t) == name);
        Button Arrow(string name) => Desc<Button>(window).Single(b => AutomationProperties.GetName(b) == name);
        void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var points = Field("Vertices"); points.Text = "24"; Click(Arrow("Increase Vertices"));
        check(canvas.CirclePoints == 25 && points.Text == "25", "Circle stepper increments a manually entered vertex count");
        var inputBounds = points.TransformToAncestor(window).TransformBounds(new Rect(points.RenderSize));
        var arrowBounds = Arrow("Increase Vertices").TransformToAncestor(window).TransformBounds(new Rect(Arrow("Increase Vertices").RenderSize));
        check(inputBounds.Right <= arrowBounds.Left && inputBounds.Width >= 60 && arrowBounds.Width >= 18 && arrowBounds.Height >= 12, "Stepper arrows remain visible beside the editable field without overlap");
        points.Text = "3"; Click(Arrow("Decrease Vertices")); check(canvas.CirclePoints == 3, "Circle stepper respects its minimum");
        points.Text = "128"; Click(Arrow("Increase Vertices")); check(canvas.CirclePoints == 128, "Circle stepper respects its maximum");
        points.Text = "invalid"; Click(Arrow("Decrease Vertices")); check(canvas.CirclePoints == 127, "Invalid text falls back to the last valid count when stepping");
        points.Text = "5.5"; Click(Arrow("Increase Vertices")); check(canvas.CirclePoints == 7, "Vertex count stepping always produces an integer");
        points.Text = "24"; var center = canvas.Center; Press(points, Key.Up); Press(points, Key.Down);
        check(canvas.CirclePoints == 24 && canvas.Center == center && points.Text == "24", "Up and Down adjust the focused numeric field without panning");
        canvas.PointerDown(canvas.Screen(new(0, 0)), MouseButton.Left); canvas.PointerUp(MouseButton.Left);
        Click(Arrow("Increase Vertices")); canvas.PointerDown(canvas.Screen(new(128, 0)), MouseButton.Left); canvas.PointerUp(MouseButton.Left);
        check(window.Document.Vertices.Count == 25, "Changing the count during a circle preview affects the committed geometry");
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-105-circle.png"));
        canvas.Shape = ShapeKind.Stairs; canvas.Stair = StairKind.Spiral; window.UpdateInspector(); window.UpdateLayout(); Pump();
        Field("Steps").Text = "2"; Click(Arrow("Decrease Steps")); check(canvas.StairSteps == 2, "Stair stepper respects the two-step minimum");
        Field("Steps").Text = "128"; Click(Arrow("Increase Steps")); check(canvas.StairSteps == 128, "Stair stepper respects the maximum count");
        Field("Steps").Text = "18"; Click(Arrow("Decrease Steps")); check(canvas.StairSteps == 17, "Stair count accepts manual input followed by decrement");
        Field("Angle (°)").Text = "180,5"; Click(Arrow("Increase Angle (°)")); check(canvas.StairAngle == 181.5, "Angle stepping preserves manually entered fractional values");
        Field("Angle (°)").Text = "360"; Click(Arrow("Increase Angle (°)")); Field("Angle (°)").Text = "15"; Click(Arrow("Decrease Angle (°)"));
        check(canvas.StairAngle == 15, "Spiral angle arrows clamp to valid limits");
        Field("Angle (°)").Text = "270"; window.UpdateLayout(); Pump();
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-105-stairs-en.png"));
        canvas.Stair = StairKind.Straight; window.UpdateInspector(); window.UpdateLayout(); Pump();
        Field("Width (units)").Text = "96.5"; Click(Arrow("Decrease Width (units)")); check(canvas.StairWidth == 95.5, "Stair width remains manually editable with arrow adjustments");
        window.ChangeLanguage("fr"); window.SetTool(EditorTool.Shapes); canvas.Stair = StairKind.Spiral; window.UpdateInspector(); window.UpdateLayout(); Pump();
        check(Arrow("Augmenter Marches").IsVisible && Field("Marches").Text == "17", "French controls retain their values and translated arrow labels");
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-105-stairs-fr.png"));

        window.ChangeLanguage("en"); string planState = window.Document.Serialize();
        var prefs = window.CreatePreferencesWindow(path); prefs.Show(); prefs.UpdateLayout(); Pump();
        Button Capture(string name) => Desc<Button>(prefs).Single(b => AutomationProperties.GetName(b) == name);
        Button Action(string name) => Desc<Button>(prefs).Single(b => b.Content as string == name && AutomationProperties.GetName(b) == "");
        var search = Desc<TextBox>(prefs).Single(); search.Text = "vertex"; prefs.UpdateLayout(); Pump();
        check(Desc<Button>(prefs).Count(b => AutomationProperties.GetName(b) != "") == 1, "Shortcut search filters actions by their translated label");
        search.Text = ""; prefs.UpdateLayout(); Pump();
        check(Desc<TextBlock>(prefs).Any(t => t.Text == "Selection and Transforms") && Desc<TextBlock>(prefs).Any(t => t.Text == "Navigation and View"), "Preferences group shortcuts into readable categories");
        Press(Capture("Draw"), Key.F6); Click(Action("Save"));
        check(ShortcutSettings.Load(path).Bindings["Dessin"].Key == Key.D, "Focusing a shortcut without arming capture does not rebind it");
        prefs = window.CreatePreferencesWindow(path); prefs.Show(); prefs.UpdateLayout(); Pump();
        Click(Capture("Draw")); Press(Capture("Draw"), Key.F6); Click(Action("Save"));
        check(ShortcutSettings.Load(path).Bindings["Dessin"].Key == Key.F6 && window.Shortcuts.Bindings["Dessin"].Key == Key.F6, "Explicitly captured shortcut persists and applies to the active window");
        prefs = window.CreatePreferencesWindow(path); prefs.Show(); prefs.UpdateLayout(); Pump();
        Click(Action("Classic")); Click(Action("Save")); check(window.Shortcuts.Bindings["Cadrer"].Key == Key.Home, "Classic preset restores previous navigation bindings");
        prefs = window.CreatePreferencesWindow(path); prefs.Show(); prefs.UpdateLayout(); Pump();
        Click(Action("Ergonomic")); MainWindow.SaveVisual(prefs, Path.Combine(output, "coat-105-preferences-en.png"));
        Click(Action("Cancel")); check(window.Shortcuts.Bindings["Cadrer"].Key == Key.Home, "Cancelling a preset leaves saved shortcuts unchanged");
        prefs = window.CreatePreferencesWindow(path); prefs.Show(); prefs.UpdateLayout(); Pump();
        Click(Action("Ergonomic")); Click(Action("Save"));
        check(window.Shortcuts.Bindings["Cadrer"].Key == Key.A && window.Document.Serialize() == planState, "Saving the ergonomic preset changes keys without modifying the plan");
        window.ChangeLanguage("fr"); prefs = window.CreatePreferencesWindow(path); prefs.Width = 560; prefs.Height = 650; prefs.Show(); prefs.UpdateLayout(); Pump();
        var save = Action("Enregistrer"); var bounds = save.TransformToAncestor(prefs).TransformBounds(new Rect(save.RenderSize));
        check(bounds.Right <= prefs.ActualWidth && bounds.Bottom <= prefs.ActualHeight, "Preferences footer wraps within a narrow French window");
        MainWindow.SaveVisual(prefs, Path.Combine(output, "coat-105-preferences-fr.png")); prefs.Close(); window.ChangeLanguage("en");
        check(L.Missing.Count == 0, "All new shortcut and stepper controls are localized: " + string.Join(", ", L.Missing));
    }
    private static IEnumerable<T> Desc<T>(DependencyObject parent) where T : DependencyObject { if (parent is T item) yield return item; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var child in Desc<T>(VisualTreeHelper.GetChild(parent, i))) yield return child; }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
}

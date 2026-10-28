// Coat — Epicteta Lupercal
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Coat;
internal static class Update10Tests
{
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        string settingsPath = Path.Combine(output, "preferences-10.json");
        check(new ShortcutSettings().Language == "en" && ShortcutSettings.Load(Path.Combine(output, "missing-10.json")).Language == "en", "New installations use English regardless of the operating system language");
        var legacy = new ShortcutSettings(); legacy.Bindings["Dessin"] = new(Key.F6, ModifierKeys.Control);
        string legacyPath = Path.Combine(output, "preferences-10-legacy.json"); File.WriteAllText(legacyPath, JsonSerializer.Serialize(legacy.Bindings));
        var migrated = ShortcutSettings.Load(legacyPath);
        check(migrated.Language == "en" && migrated.Bindings["Dessin"] == new Shortcut(Key.F6, ModifierKeys.Control), "Legacy preferences default to English without resetting custom shortcuts");
        migrated.Language = "fr"; migrated.Save(settingsPath); var loaded = ShortcutSettings.Load(settingsPath);
        check(loaded.Language == "fr" && loaded.Bindings.OrderBy(p => p.Key).SequenceEqual(migrated.Bindings.OrderBy(p => p.Key)), "Language and shortcut bindings persist together and reload without loss");
        string unsupported = Path.Combine(output, "preferences-10-unsupported.json"); File.WriteAllText(unsupported, File.ReadAllText(settingsPath).Replace("\"fr\"", "\"unknown\""));
        check(ShortcutSettings.Load(unsupported).Language == "en", "Unsupported language identifiers fall back to English");
        bool formatsValid = L.English.All(pair =>
        {
            var before = CompositeFormat.Parse(pair.Key); var after = CompositeFormat.Parse(pair.Value);
            string Indices(string text) => string.Join(",", Regex.Matches(text, @"\{(\d+)(?:[:,][^}]*)?\}").Select(m => m.Groups[1].Value).Order());
            return before.MinimumArgumentCount == after.MinimumArgumentCount && Indices(pair.Key) == Indices(pair.Value);
        });
        check(formatsValid, "Every English translation preserves the source format arguments");

        var plan = new Plan { Grid = 64 }; plan.Polygon(new V2[] { new(0, 0), new(256, 0), new(256, 128), new(0, 128) });
        plan.GroupSelection(Array.Empty<int>(), plan.Edges.Select(e => e.Id)); plan.Groups[0].Name = "Groupe français — Points";
        plan.Annotations.Add(new Annotation { Id = plan.NextId++, Text = "Annuler\nPorte à gauche", X = 16, Y = 80 });
        var face = plan.Faces().Single(); plan.Fills[face.Key] = "#448855"; plan.FillPatterns[face.Key] = FillPattern.Dots;
        window.LoadDocument(plan, ""); window.Shortcuts = migrated; window.SetTool(EditorTool.Select); window.Canvas.Mode = SelectionMode.Edge; window.Canvas.SelectAll();
        window.Canvas.Center = new(128, 64); window.Canvas.Zoom = .9; window.Canvas.ShowUpperLevels = true;
        var savedColors = window.SavedColors.Colors.ToArray(); string state = plan.Serialize(); var selected = window.Canvas.SelectedEdges.ToArray();
        window.ChangeLanguage("en"); window.UpdateLayout(); Pump();
        var menus = Desc<Menu>(window).Single().Items.OfType<MenuItem>().ToList();
        check(menus.Select(m => (string)m.Header).SequenceEqual(new[] { "File", "Edit", "View", "Help" }), "Switching to English updates all top-level menus immediately");
        check((string)((MenuItem)menus[1].Items[0]).Header == "Undo" && L.T("Annuler") == "Cancel", "Undo history and Cancel dialog actions use distinct English terms");
        check(window.Document.Serialize() == state && window.Canvas.SelectedEdges.SetEquals(selected) && window.Canvas.Center == new V2(128, 64) && window.Canvas.Zoom == .9 && window.Canvas.ShowUpperLevels, "Changing language preserves the open plan, selection, view, and display options");
        check(window.SavedColors.Colors.SequenceEqual(savedColors) && window.Shortcuts.Bindings["Dessin"] == new Shortcut(Key.F6, ModifierKeys.Control), "Changing language preserves the palette and custom key bindings");
        check(plan.Annotations[0].Text == "Annuler\nPorte à gauche" && plan.Groups[0].Name == "Groupe français — Points", "User-authored annotations and group names are never translated");
        var texts = Desc<TextBlock>(window).Select(t => t.Text).ToHashSet();
        check(texts.Contains("•  Vertices") && texts.Contains("╱  Edges") && texts.Contains("▱  Faces") && new[] { "Move", "Rotate", "Scale" }.All(texts.Contains), "Geometry and transform controls use standard editor terminology");
        check(menus[0].Items.OfType<MenuItem>().Single(m => (string)m.Header == "Save As…").InputGestureText == "Ctrl+Shift+S", "English menu hints use Shift instead of the French Maj label");
        Key key = Key.F6; var modifiers = ModifierKeys.Control;
        check(window.Shortcuts.Translate(ref key, ref modifiers) && key == Key.D, "Custom key bindings keep the same action after a language change");
        check(L.F($"Surface : {12.5:0.##} u²") == "Area: 12.5 units²", "English formatted measurements use a decimal point");
        window.Canvas.SetTransform(TransformKind.Rotation); window.UpdateLayout(); Pump();
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-10-selection-en.png"));

        var preferences = window.CreatePreferencesWindow(settingsPath); preferences.Show(); preferences.UpdateLayout(); Pump();
        var language = Desc<ComboBox>(preferences).Single();
        check(language.Items.Cast<string>().SequenceEqual(new[] { "English", "Français" }) && language.SelectedIndex == 0, "Preferences expose English and Français with the active language selected");
        var labels = Desc<TextBlock>(preferences).Select(t => t.Text).ToHashSet();
        check(ShortcutSettings.Actions.All(a => labels.Contains(L.ShortcutLabel(a.Label))) && labels.Contains("Vertex Selection") && labels.Contains("Grid 32 units (numpad)"), "Every shortcut action, including grid and numpad actions, has an English display name");
        MainWindow.SaveVisual(preferences, Path.Combine(output, "coat-10-preferences-en.png"));
        language.SelectedIndex = 1; ActionButton(preferences, "Save").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); Pump();
        check(L.Language == "fr" && ShortcutSettings.Load(settingsPath).Language == "fr" && (string)Desc<Menu>(window).Single().Items.OfType<MenuItem>().First().Header == "Fichier", "Saving French preferences changes the existing interface without restarting");
        check(window.Document.Serialize() == state && window.Canvas.SelectedEdges.SetEquals(selected), "Saving a language preference does not modify the plan or clear the selection");
        preferences = window.CreatePreferencesWindow(settingsPath); preferences.Show(); preferences.UpdateLayout(); Pump();
        MainWindow.SaveVisual(preferences, Path.Combine(output, "coat-10-preferences-fr.png"));
        string savedPreferences = File.ReadAllText(settingsPath);
        Desc<ComboBox>(preferences).Single().SelectedIndex = 0; ActionButton(preferences, "Annuler").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(L.Language == "fr" && File.ReadAllText(settingsPath) == savedPreferences, "Cancelling a language choice leaves the active and saved preferences unchanged");
        preferences = window.CreatePreferencesWindow(settingsPath); preferences.Show(); preferences.UpdateLayout(); Pump();
        Desc<ComboBox>(preferences).Single().SelectedIndex = 0; ActionButton(preferences, "Enregistrer").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); Pump();
        check(L.Language == "en" && ShortcutSettings.Load(settingsPath).Language == "en" && window.Shortcuts.Bindings["Dessin"] == new Shortcut(Key.F6, ModifierKeys.Control), "Returning to English persists the choice and retains customized shortcuts");
        window.Mutate(() => window.Document.Segment(new(384, 0), new(512, 0))); window.ChangeLanguage("fr"); window.ChangeLanguage("en"); window.Undo();
        check(window.Document.Serialize() == state, "Undo history survives repeated language changes");

        window.SetTool(EditorTool.Paint); window.Canvas.PaintLines = false; window.UpdateInspector(); window.UpdateLayout(); Pump();
        check(Desc<Button>(window).Any(b => b.Content as string == "Dots") && Desc<Button>(window).Any(b => b.Content as string == "Fill faces"), "Fill pattern dots are distinguished from geometric vertices");
        window.Canvas.PaintLines = true; window.UpdateInspector(); window.UpdateLayout(); Pump();
        check(Desc<Button>(window).Any(b => b.Content as string == "Long dashes") && Desc<Button>(window).Any(b => b.Content as string == "Short dashes"), "Line styles use precise dashed-line terminology");
        var picker = new ColorPicker(window, "#448855"); picker.Show(); picker.UpdateLayout(); Pump();
        check(picker.Title == "Color Picker — Coat" && Desc<Button>(picker).Any(b => b.Content as string == "Eyedropper…") && Desc<TextBlock>(picker).Any(t => t.Text == "Saved colors") && Desc<TextBlock>(picker).Any(t => t.Text.Contains("G 136")), "Color picker, saved colors, eyedropper, and RGB labels are localized");
        MainWindow.SaveVisual(picker, Path.Combine(output, "coat-10-color-picker-en.png")); picker.Close();
        var confirmation = AppDialog.Create(window, "Delete level 0?", "Delete Level", MessageBoxButton.YesNoCancel, MessageBoxResult.No, _ => { }); confirmation.Show(); confirmation.UpdateLayout(); Pump();
        check(Desc<Button>(confirmation).Select(b => b.Content as string).SequenceEqual(new[] { "Yes", "No", "Cancel" }), "Application confirmations use English buttons independently of Windows language"); confirmation.Close();
        window.SetTool(EditorTool.Shapes); window.Canvas.Shape = ShapeKind.Stairs; window.Canvas.Stair = StairKind.Spiral; window.UpdateInspector(); window.UpdateLayout(); Pump();
        var types = Desc<ComboBox>(window).Single(b => AutomationProperties.GetName(b) == "Stair type");
        check(types.Items.Cast<string>().SequenceEqual(new[] { "Straight", "L-shaped", "U-shaped", "Spiral" }), "Stair types use professional English names");
        check(Desc<TextBox>(window).Any(t => AutomationProperties.GetName(t) == "Steps") && Desc<TextBox>(window).Any(t => AutomationProperties.GetName(t) == "Angle (°)"), "Stair step count and spiral angle remain editable in English");
        foreach (var tool in Enum.GetValues<EditorTool>()) { window.SetTool(tool); window.UpdateLayout(); Pump(); }
        window.SetTool(EditorTool.Select); window.Canvas.SelectAll(); window.ScaleSelection(0, 1);
        check(Desc<TextBlock>(window).Any(t => t.Text.StartsWith("The absolute value of each scale factor")), "Validation feedback follows the selected language");
        check(L.Missing.Count == 0, "All localized UI paths and shortcut labels have English catalog entries: " + string.Join(", ", L.Missing));

        var englishDemo = new Plan();
        foreach (var (kind, start, end, label) in new[] {
            (StairKind.Straight, new V2(-640, 288), new V2(-256, 288), "STRAIGHT · 12 steps"),
            (StairKind.L, new V2(0, 320), new V2(384, 320), "L-SHAPED · landing"),
            (StairKind.U, new V2(-640, -96), new V2(-256, -96), "U-SHAPED · return flight"),
            (StairKind.Spiral, new V2(160, -320), new V2(352, -320), "SPIRAL · 270° sweep") })
        {
            englishDemo.AddStair(Stairs.Build(kind, start, end, 12, 96, 270, true, 32), kind);
            var labelPosition = kind == StairKind.Spiral ? new V2(-16, -64) : start + new V2(0, 112);
            englishDemo.Annotations.Add(new Annotation { Id = englishDemo.NextId++, Text = label, X = labelPosition.X, Y = labelPosition.Y, Size = 22 });
        }
        window.LoadDocument(englishDemo, ""); window.Canvas.Fit(); window.SetTool(EditorTool.Shapes); window.Canvas.Shape = ShapeKind.Stairs; window.UpdateInspector(); window.UpdateLayout(); Pump();
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-10-stairs-en.png")); window.ExportPngTo(Path.Combine(output, "coat-10-plan-en.png"));
        englishDemo.ViewX = window.Canvas.Center.X; englishDemo.ViewY = window.Canvas.Center.Y; englishDemo.Zoom = window.Canvas.Zoom;
        MainWindow.WriteAtomic(Path.Combine(output, "Example-Stairs.coat"), englishDemo.Serialize());
        window.Width = 1050; window.Height = 680; window.UpdateLayout(); Pump(); MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-10-compact-en.png"));
    }
    private static Button ActionButton(DependencyObject root, string title) => Desc<Button>(root).Single(b => b.Content as string == title);
    private static IEnumerable<T> Desc<T>(DependencyObject parent) where T : DependencyObject { if (parent is T item) yield return item; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var child in Desc<T>(VisualTreeHelper.GetChild(parent, i))) yield return child; }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
}

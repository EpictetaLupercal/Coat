// Coat — Epicteta Lupercal
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Coat;
internal static class Update106Tests
{
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        L.SetLanguage("en"); window.ChangeLanguage("en"); var canvas = window.Canvas;
        foreach (var (text, value) in new[] { ("10+5", 15d), ("10-5", 5d), ("10*2", 20d), ("10/2", 5d), ("2+3*4", 14d), ("(2+3)*4", 20d), ("-10/-2", 5d), ("1,5+2.5", 4d), ("1e2/4", 25d), (" 2 * -3 + 1 ", -5d) })
            check(NumericExpression.TryEvaluate(text, out double actual) && actual == value, "Arithmetic evaluates " + text);
        foreach (string text in new[] { "1/0", "0/0", "1+", "()", "abc", "1e309", "2(3)", new string('(', 80) + "1" + new string(')', 80), "1;2", "NaN", "1..2" })
            check(!NumericExpression.TryEvaluate(text, out _), "Arithmetic rejects unsafe or invalid input: " + text);
        check(!NumericExpression.TryInteger("5/2", out _) && NumericExpression.TryInteger("12/3", out int count) && count == 4, "Integer fields reject fractional results");
        var settings = new ShortcutSettings(); ShortcutSettings.Validate(settings.Bindings);
        check(settings.Bindings.All(p => p.Value.Key == (p.Key == "Confirmer" ? Key.Enter : Key.None)), "New installations assign only the explicitly requested Enter confirmation");
        var path = Path.Combine(output, "preferences-106.json");
        settings.Bindings["Dessin"] = new(Key.F8, ModifierKeys.None); settings.Save(path);
        var before = new Dictionary<string, Shortcut>(settings.Bindings); before.Remove("Organisation"); before.Remove("Supprimer ce niveau");
        File.WriteAllText(path, JsonSerializer.Serialize(new { Bindings = before, Language = "fr", ShortcutVersion = 1 }));
        var loaded = ShortcutSettings.Load(path);
        check(loaded.Bindings["Dessin"].Key == Key.F8 && loaded.Bindings["Organisation"].Key == Key.None && loaded.Bindings["Supprimer ce niveau"].Key == Key.None, "Preference migration preserves existing bindings and leaves new actions unassigned");
        loaded.Bindings["Shapes"] = loaded.Bindings["Dessin"]; bool conflict = false;
        try { ShortcutSettings.Validate(loaded.Bindings); } catch (InvalidDataException ex) { conflict = ex.Message.Contains("Draw") && ex.Message.Contains("Shapes"); }
        check(conflict, "Conflict error identifies both affected commands");
        window.Shortcuts = new(); window.LoadDocument(new Plan(), ""); window.SetTool(EditorTool.Select);
        void Press(UIElement target, Key key) => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        TextBox Field(string name) => Desc<TextBox>(window).Single(t => AutomationProperties.GetName(t) == name);
        Press(window, Key.D); check(canvas.Tool == EditorTool.Select, "An unassigned key never activates an old default action");
        window.Shortcuts.Bindings["Cercle"] = new(Key.F8, ModifierKeys.None); Press(window, Key.F8); window.UpdateLayout(); Pump();
        check(canvas.Tool == EditorTool.Shapes && canvas.Shape == ShapeKind.Circle, "A newly bound feature executes through the normal keyboard handler");
        Field("Vertices").Text = "4*6"; Press(Field("Vertices"), Key.Enter);
        check(canvas.CirclePoints == 24 && Field("Vertices").Text == "24", "Enter evaluates and commits the circle vertex expression");
        Field("Vertices").Text = "1/0"; Press(Field("Vertices"), Key.Enter); check(canvas.CirclePoints == 24, "Invalid arithmetic cannot change shape precision");
        window.ExecuteFeature("Escaliers en colimaçon"); window.UpdateLayout(); Pump();
        Field("Steps").Text = "4*4"; Press(Field("Steps"), Key.Enter); Field("Angle (°)").Text = "360/2"; Press(Field("Angle (°)"), Key.Enter);
        check(canvas.StairSteps == 16 && canvas.StairAngle == 180, "Stair count and angle accept arithmetic with Enter");
        Field("Height").Text = "96*2"; Press(Field("Height"), Key.Enter);
        check(window.Document.LevelZ(0) == 192, "Enter applies a level height expression");
        canvas.Shape = ShapeKind.Rectangle; canvas.PointerDown(canvas.Screen(new(0, 0)), MouseButton.Left); canvas.PointerUp(MouseButton.Left); canvas.PointerMove(canvas.Screen(new(128, 128))); Press(window, Key.Enter);
        check(window.Document.Faces().Count == 1, "Enter confirms the pending two-click shape");
        window.SetTool(EditorTool.Select); canvas.SetTransform(TransformKind.Translation); canvas.Mode = SelectionMode.Edge; canvas.SelectAll(); window.UpdateLayout(); Pump();
        Field("Offset X").Text = "8*4"; Field("Offset Y").Text = "10-5"; Press(Field("Offset X"), Key.Enter);
        check(window.Document.Vertices.Min(v => v.X) == 32 && window.Document.Vertices.Min(v => v.Y) == 5, "Enter applies a full translation with both expressions");
        check(Field("Offset X").Text == "32", "Evaluated translation value is retained after applying it");
        window.Undo(); check(window.Document.Vertices.Min(v => v.X) == 0, "Confirmed arithmetic transformation is one undo step");
        var plan = new Plan();
        foreach (int level in new[] { -1, 0, 2 }) { plan.ActiveLevel = level; double x = level * 256; plan.Polygon(new V2[] { new(x, 0), new(x + 128, 0), new(x + 128, 128), new(x, 128) }); }
        plan.ActiveLevel = 0; window.LoadDocument(plan, ""); window.SetTool(EditorTool.Select); canvas.Mode = SelectionMode.Edge; canvas.ShowUpperLevels = true; canvas.SelectAcrossLevels = true; canvas.SelectAll();
        check(canvas.SelectionPoints().Count == 12, "Select All spans visible lower, active and upper levels when enabled");
        window.GroupSelection(); var group = plan.Groups.Single(); check(plan.GroupPoints(group).Count == 12, "One group can span three levels");
        window.RenameGroup(group.Id, "Main circulation"); check(plan.Groups.Single().Name == "Main circulation", "Group names are editable");
        canvas.ClearSelection(); window.SelectGroup(group); window.MoveSelection(32, 0);
        check(window.Document.Vertices.All(v => v.X == v.Level * 256 + 32 || v.X == v.Level * 256 + 160), "Moving a multi-level group transforms every member and preserves levels");
        string grouped = window.Document.Serialize(); window.Duplicate();
        check(window.Document.Vertices.Select(v => v.Level).Distinct().Order().SequenceEqual(new[] { -1, 0, 2 }) && window.Document.Vertices.GroupBy(v => v.Level).All(g => g.Count() >= 8) && window.Document.Groups.Count == 2, "Duplicating a multi-level group preserves all level memberships");
        window.Undo(); check(window.Document.Serialize() == grouped, "Undo restores the multi-level group exactly");
        window.SelectGroup(window.Document.Groups.Single()); var fragment = window.CaptureSelection();
        check(fragment.Vertices.Select(v => v.Level - fragment.ActiveLevel).Distinct().Order().SequenceEqual(new[] { 0, 1, 3 }), "Clipboard stores relative level offsets without collapsing geometry");
        var clipboard = new MemoryClipboard(); window.PlanClipboard = clipboard; window.CopySelection(); canvas.Center = new(2000, 0); window.PasteSelection();
        check(window.Document.Vertices.Any(v => v.Level == 3) && window.Document.Groups.Count == 2, "Paste preserves a multi-level group's spacing from the active level");
        window.Undo(); window.SelectGroup(window.Document.Groups.Single()); canvas.Fit(); window.UpdateLayout(); Pump();
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-106-selection.png"));
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var outliner = Application.Current.Windows.OfType<Window>().Single(w => w.Title == "Outliner");
            try
            {
                var tree = Desc<TreeView>(outliner).Single(); foreach (TreeViewItem level in tree.Items) level.IsExpanded = true;
                outliner.UpdateLayout(); Pump();
                check(Desc<TreeViewItem>(outliner).Count(n => n.Header as string == "Main circulation") == 3, "Outliner shows multi-level group membership beneath every containing level");
                MainWindow.SaveVisual(outliner, Path.Combine(output, "coat-106-outliner.png"));
            }
            finally { outliner.Close(); }
        }));
        window.ExecuteFeature("Organisation");
        int builds = canvas.RenderCacheBuilds;
        for (int i = 0; i < 4; i++) { canvas.Center += new V2(16, -8); canvas.Zoom *= 1.05; canvas.InvalidateVisual(); Pump(); }
        check(canvas.RenderCacheBuilds == builds, "Panning and zooming never rebuild topology or render geometry");
        var renderPlan = new Plan(); renderPlan.Polygon(new V2[] { new(0, 0), new(128, 0), new(128, 128), new(0, 128) }); renderPlan.Fills[renderPlan.Faces().Single().Key] = "#22CC66";
        window.LoadDocument(renderPlan, ""); canvas.ShowGrid = false; canvas.Center = new(64, 64); canvas.Zoom = 1; canvas.InvalidateVisual(); Pump();
        byte[] Pixel(V2 point)
        {
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)canvas.ActualWidth, (int)canvas.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas);
            var pixel = canvas.Screen(point); var bytes = new byte[4]; bitmap.CopyPixels(new Int32Rect((int)pixel.X, (int)pixel.Y, 1, 1), bytes, 4, 0); return bytes;
        }
        byte[] expected = Pixel(new(64, 64));
        canvas.Center += new V2(32, 0); canvas.InvalidateVisual(); Pump(); check(Pixel(new(64, 64)).SequenceEqual(expected), "Navigation image preserves rendered fill colors");
        canvas.Center += new V2(300, 0); canvas.InvalidateVisual(); Pump(); check(Pixel(new(64, 64)).SequenceEqual(expected), "Navigation cache renews past its margin without losing geometry");
        window.Mutate(() => renderPlan.Fills[renderPlan.Faces().Single().Key] = "#CC2266"); Pump(); check(!Pixel(new(64, 64)).SequenceEqual(expected), "Painting invalidates the navigation image immediately");
        renderPlan.Annotations.Add(new Annotation { Id = renderPlan.NextId++, Text = "First\nSecond", Level = 0 }); canvas.RefreshGeometry(); canvas.SelectedAnnotation = renderPlan.Annotations.Single().Id; window.SetTool(EditorTool.Annotation); window.UpdateLayout(); Pump();
        var multiline = Desc<TextBox>(window).Single(t => t.AcceptsReturn); string noteBefore = renderPlan.Annotations.Single().Text;
        var enter = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(multiline), 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent }; multiline.RaiseEvent(enter);
        check(!enter.Handled && renderPlan.Annotations.Single().Text == noteBefore, "Multiline Enter is left to the text editor and never applies the annotation");
        window.LoadDocument(new Plan(), Path.Combine(output, "view-入口-é.coat")); canvas.Center = new(123.5, -456.25); canvas.Zoom = .75; canvas.ShowGrid = false; canvas.ShowLengths = true; canvas.ShowLowerLevels = false; canvas.ShowUpperLevels = true; canvas.SelectAcrossLevels = true;
        check(window.Save(), "Save captures the complete project view");
        window.LoadDocument(new Plan(), ""); window.OpenPath(Path.Combine(output, "view-入口-é.coat"));
        check(canvas.Center == new V2(123.5, -456.25) && canvas.Zoom == .75 && !canvas.ShowGrid && canvas.ShowLengths && !canvas.ShowLowerLevels && canvas.ShowUpperLevels && canvas.SelectAcrossLevels, "Reopening restores framing, grid, lengths, visibility and selection scope");
        window.LoadDocument(Plan.Parse("{\"Version\":1}"), "");
        check(canvas.ShowGrid && canvas.ShowLowerLevels && !canvas.ShowUpperLevels && !canvas.ShowLengths && !canvas.SelectAcrossLevels, "Older files without View settings receive compatible defaults");
        window.RecentFilesPathOverride = Path.Combine(output, "recent-106.json");
        for (int i = 0; i < 12; i++) window.RememberRecent(Path.Combine(output, $"recent-{i}.coat"));
        window.RememberRecent(Path.Combine(output, "recent-5.coat"));
        check(window.RecentFiles.Count == 10 && Path.GetFileName(window.RecentFiles[0]) == "recent-5.coat" && window.RecentFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 10, "Recent files are bounded, deduplicated and sorted by most recent use");
        window.OpenRecent(Path.Combine(output, "view-入口-é.coat"));
        check(canvas.Zoom == .75 && Path.GetFileName(window.RecentFiles[0]) == "view-入口-é.coat", "Open Recent reopens a Unicode project and updates its position");
        string unchanged = window.Document.Serialize(); window.OpenRecent(Path.Combine(output, "missing-file-106.coat"));
        check(window.Document.Serialize() == unchanged, "Missing recent files leave the current document intact");
        window.RecentFilesPathOverride = null;
        window.Shortcuts = new(); var prefs = window.CreatePreferencesWindow(path); prefs.Show(); prefs.UpdateLayout(); Pump();
        MainWindow.SaveVisual(prefs, Path.Combine(output, "coat-106-preferences.png")); prefs.Close();
        check(L.Missing.Count == 0, "New commands and controls have English translations: " + string.Join(", ", L.Missing));
    }
    private sealed class MemoryClipboard : IPlanClipboard { private string? data; public void Write(string text) => data = text; public string? Read() => data; }
    private static IEnumerable<T> Desc<T>(DependencyObject parent) where T : DependencyObject { if (parent is T value) yield return value; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var child in Desc<T>(VisualTreeHelper.GetChild(parent, i))) yield return child; }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
}

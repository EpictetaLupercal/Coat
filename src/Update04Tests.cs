// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace Coat;
internal static class Update04Tests
{
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        var p = new Plan(); p.Polygon(new V2[] { new(0, 0), new(256, 0), new(256, 256), new(0, 256) });
        var face = p.Faces().Single(); p.Fills[face.Key] = "#66C5AD"; p.FillPatterns[face.Key] = FillPattern.Stripes;
        foreach (var e in p.Edges) e.Style = LineStyle.LongDash;
        p.Segment(new(128, 0), new(128, 256));
        check(p.FillPatterns.Count == 2 && p.FillPatterns.Values.All(v => v == FillPattern.Stripes), "Splitting a patterned fill preserves its pattern on both faces");
        check(p.Edges.Count(e => e.Style == LineStyle.LongDash) == 6, "Splitting dashed edges preserves their style");
        p.Annotations.Add(new Annotation { Id = p.NextId++, X = 0, Y = 320, Text = "Entrée <A> & sortie\nNiveau 0" });
        check(Plan.Parse(p.Serialize()).Serialize() == p.Serialize(), "Annotations and styles survive project roundtrip");
        var svg = XDocument.Parse(MainWindow.Svg(p)); var ns = svg.Root!.Name.Namespace;
        check(svg.Descendants(ns + "pattern").Count() == 2 && svg.Descendants(ns + "line").Count(e => (string?)e.Attribute("stroke-dasharray") == "16 8") == 6, "SVG defines motifs and exports long dashes");
        check(svg.Descendants(ns + "tspan").First().Value == "Entrée <A> & sortie" && svg.Descendants(ns + "tspan").Count() == 2, "SVG safely exports multiline Unicode annotations");
        var copy = Plan.Parse(p.Serialize()); copy.DeleteLevel(0); check(copy.Annotations.Count == 0 && copy.FillPatterns.Count == 0, "Deleting a level removes annotations and motifs");
        var settings = new ShortcutSettings(); settings.Bindings["Dessin"] = new(Key.F6, ModifierKeys.Control);
        var path = Path.Combine(output, "preferences-test.json"); settings.Save(path); settings = ShortcutSettings.Load(path);
        Key key = Key.F6; var mods = ModifierKeys.Control;
        check(settings.Translate(ref key, ref mods) && key == Key.D && mods == ModifierKeys.None, "Saved custom shortcut resolves to the original action");
        key = Key.D; check(!settings.Translate(ref key, ref mods), "Reassigned default shortcut stops activating its old action");
        var duplicate = new Dictionary<string, Shortcut>(settings.Bindings); duplicate["Shapes"] = duplicate["Dessin"];
        bool rejected = false; try { ShortcutSettings.Validate(duplicate); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "Duplicate shortcuts are rejected without saving");
        window.Shortcuts = settings; window.LoadDocument(p, ""); window.SetTool(EditorTool.None);
        var source = PresentationSource.FromVisual(window); var keyboard = new KeyEventArgs(Keyboard.PrimaryDevice, source!, 0, Key.R) { RoutedEvent = Keyboard.PreviewKeyDownEvent }; window.RaiseEvent(keyboard);
        check(window.Canvas.Tool == EditorTool.Shapes, "Unchanged shortcuts still dispatch through the real keyboard handler");
        window.Shortcuts = new(); var canvas = window.Canvas; canvas.Zoom = 1; canvas.Center = new(128, 128);
        window.SetTool(EditorTool.Annotation);
        canvas.PointerDown(canvas.Screen(new(320, 64)), MouseButton.Left); canvas.PointerUp(MouseButton.Left);
        check(p.Annotations.Count == 2 && canvas.SelectedAnnotation == p.Annotations.Last().Id, "Annotation tool places and selects a snapped text");
        var id = canvas.SelectedAnnotation; var original = p.Serialize();
        canvas.PointerDown(canvas.Screen(new(328, 60)), MouseButton.Left); canvas.PointerMove(canvas.Screen(new(360, 92))); canvas.PointerUp(MouseButton.Left);
        check(p.Annotations.Last().X == 352 && p.Annotations.Last().Y == 96, "Dragging an annotation snaps its displacement");
        window.Undo(); check(window.Document.Serialize() == original, "Annotation drag is undone as one operation"); window.Redo();
        canvas.SelectedAnnotation = id; window.Duplicate(); check(window.Document.Annotations.Count == 3, "Annotation duplication creates an independent text");
        window.AssignSelectionToLevel(2); check(window.Document.Annotations.Last().Level == 2 && window.Document.ActiveLevel == 2, "Annotation can be assigned to another level");
        canvas.DeleteSelection(); check(window.Document.Annotations.Count == 2, "Delete removes the selected annotation"); window.Undo(); check(window.Document.Annotations.Count == 3, "Annotation deletion is undoable");
        window.LoadDocument(p, ""); window.SetTool(EditorTool.Select); canvas.Mode = SelectionMode.Face; canvas.SelectedFaces.Add(p.Faces()[0].Key); window.UpdateInspector();
        IEnumerable<T> Desc<T>(DependencyObject d) where T : DependencyObject { if (d is T t) yield return t; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) foreach (var child in Desc<T>(VisualTreeHelper.GetChild(d, i))) yield return child; }
        window.UpdateLayout();
        var dots = Desc<Button>(window).First(b => (b.Content as string) == "Points"); dots.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(p.FillPatterns[p.Faces()[0].Key] == FillPattern.Dots, "Selection inspector applies the dots motif via its button");
        var shortDash = Desc<Button>(window).First(b => (b.Content as string) == "Court"); shortDash.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(p.FaceEdges(p.Faces()[0]).All(e => p.Edges.First(x => x.Id == e).Style == LineStyle.ShortDash), "Selection inspector applies short dashes to selected zone edges");
        canvas.ClearSelection(); window.SetTool(EditorTool.None); canvas.Fit(); window.UpdateLayout(); Pump();
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-04-styles.png"));
        canvas.Exporting = true; canvas.InvalidateVisual(); Pump(); MainWindow.SaveVisual(canvas, Path.Combine(output, "coat-04-export.png")); canvas.Exporting = false;
        File.WriteAllText(Path.Combine(output, "coat-04-export.svg"), MainWindow.Svg(p));
        File.WriteAllText(Path.Combine(output, "Exemple-annotations-styles.coat"), p.Serialize());
        var preferences = window.CreatePreferencesWindow(); preferences.Show(); preferences.UpdateLayout(); Pump();
        check(Desc<Button>(preferences).Count() >= ShortcutSettings.Actions.Count, "Preferences window renders all configurable shortcut actions");
        MainWindow.SaveVisual((FrameworkElement)preferences.Content, Path.Combine(output, "coat-04-preferences.png")); preferences.Close();
        var menu = Desc<Menu>(window).Single(); var file = (MenuItem)menu.Items[0]; file.IsSubmenuOpen = true; Pump();
        check(((SolidColorBrush)file.Background).Color == Colors.White && ((SolidColorBrush)file.Foreground).Color == Colors.Black, "File menu uses black text on white background");
        var popup = PresentationSource.FromVisual((MenuItem)file.Items[0])?.RootVisual as FrameworkElement;
        if (popup != null) MainWindow.SaveVisual(popup, Path.Combine(output, "coat-04-menu.png")); file.IsSubmenuOpen = false;
        window.SetTool(EditorTool.Annotation); canvas.SelectedAnnotation = p.Annotations.First().Id; window.UpdateInspector(); window.UpdateLayout();
        var editor = Desc<TextBox>(window).Single(t => t.AcceptsReturn); editor.Text = "Entrée principale\nAccès réservé";
        Desc<Button>(window).Single(b => b.Content as string == "Appliquer le texte").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(p.Annotations.First().Text == editor.Text, "Annotation editor applies multiline content through its button");
        window.Undo(); check(window.Document.Annotations.First().Text == "Entrée <A> & sortie\nNiveau 0", "Annotation text edits can be undone");
    }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
}


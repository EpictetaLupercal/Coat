// Coat — Epicteta Lupercal
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

namespace Coat;
internal static class Update06Tests
{
    private sealed class TestClipboard : IPlanClipboard
    {
        public string? Data;
        public bool Busy;
        public void Write(string data) { if (Busy) throw new ExternalException(); Data = data; }
        public string? Read() => Busy ? throw new ExternalException() : Data;
    }
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        var clipboard = new TestClipboard(); window.PlanClipboard = clipboard;
        var canvas = window.Canvas;
        Plan Square()
        {
            var p = new Plan(); p.Polygon(new V2[] { new(0, 0), new(128, 0), new(128, 128), new(0, 128) });
            p.Fills[p.Faces().Single().Key] = "#66C5AD"; p.FillPatterns[p.Faces().Single().Key] = FillPattern.Stripes;
            foreach (var edge in p.Edges) { edge.Color = "#CC8844"; edge.Style = LineStyle.LongDash; } return p;
        }
        void Load(Plan p, SelectionMode mode = SelectionMode.Edge)
        { window.LoadDocument(p, ""); window.SetTool(EditorTool.Select); canvas.Mode = mode; canvas.Zoom = 1; canvas.Center = new(64, 64); canvas.ShowUpperLevels = false; window.UpdateLayout(); }
        void Drag(V2 from, V2 to, bool shift = false)
        { canvas.PointerDown(canvas.Screen(from), MouseButton.Left, shift: shift); canvas.PointerMove(canvas.Screen(to)); canvas.PointerUp(MouseButton.Left); }
        foreach (var mode in new[] { SelectionMode.Vertex, SelectionMode.Edge })
        {
            var p = Square(); Load(p, mode); canvas.SelectAll(); var before = p.Serialize();
            Drag(new(64, 64), new(96, 96), shift: true);
            check(p.Vertices.Min(v => v.X) == 32 && p.Vertices.Min(v => v.Y) == 32 && p.Edges.Count == 4 && p.Fills.Count == 1, $"{mode}: dragging selected interior moves the entire shape without extrusion");
            window.Undo(); check(window.Document.Serialize() == before, $"{mode}: interior movement is undone in one step");
            Load(Square(), mode); if (mode == SelectionMode.Vertex) canvas.SelectedVertices.Add(window.Document.Vertices[0].Id); else canvas.SelectedEdges.Add(window.Document.Edges[0].Id);
            before = window.Document.Serialize(); Drag(new(64, 64), new(96, 96));
            check(window.Document.Serialize() == before, $"{mode}: an incompletely selected face cannot be dragged by its interior");
        }
        var source = Square(); source.GroupSelection(Array.Empty<int>(), source.Edges.Select(e => e.Id)); Load(source); canvas.SelectAll();
        string sourceState = source.Serialize(); check(window.CopySelection() && source.Serialize() == sourceState && !window.History.CanUndo, "Copy preserves the source document and undo history");
        var fragment = Plan.Parse(clipboard.Data!);
        check(fragment.Edges.Count == 4 && fragment.Groups.Count == 1 && fragment.Fills.Values.Single() == "#66C5AD" && fragment.FillPatterns.Values.Single() == FillPattern.Stripes && fragment.Edges.All(e => e.Style == LineStyle.LongDash), "Clipboard includes geometry, group, colors, dashes and fill motifs");
        var target = new Plan { ActiveLevel = 3 }; target.EnsureLevel(3); Load(target); canvas.Center = new(512, 256);
        window.PasteSelection();
        check(target.Vertices.All(v => v.Level == 3) && target.Vertices.Min(v => v.X) == 448 && target.Groups.Count == 1 && target.Fills.Values.Single() == "#66C5AD" && target.FillPatterns.Values.Single() == FillPattern.Stripes, "Paste into another project uses active level and view center while preserving appearance");
        check(canvas.SelectionEdges().Count == 4, "Pasted geometry is selected for immediate movement");
        string once = target.Serialize(); window.PasteSelection();
        check(target.Vertices.Any(v => v.X == 608) && target.Groups.Count == 2, "Repeated paste advances by one grid step and creates a separate group");
        window.Undo(); check(window.Document.Serialize() == once, "Repeated paste can be undone in one step"); window.Redo(); check(window.Document.Groups.Count == 2, "Redo restores a pasted group");
        Load(Square()); canvas.SelectAll(); string beforeCut = window.Document.Serialize();
        clipboard.Busy = true; window.CutSelection(); check(window.Document.Serialize() == beforeCut, "Busy clipboard never deletes the selection during cut"); clipboard.Busy = false;
        window.CutSelection(); check(window.Document.Edges.Count == 0 && window.Document.Fills.Count == 0 && clipboard.Data != null, "Cut copies first then removes the selected geometry");
        window.Undo(); check(window.Document.Serialize() == beforeCut, "Undo cut restores geometry and fill styles");
        clipboard.Data = "broken JSON"; string beforeInvalid = window.Document.Serialize(); window.PasteSelection(); check(window.Document.Serialize() == beforeInvalid, "Malformed clipboard data cannot change the plan");
        clipboard.Data = null; window.PasteSelection(); check(window.Document.Serialize() == beforeInvalid, "Non-Coat clipboard content is ignored");
        Load(Square(), SelectionMode.Vertex); canvas.SelectedVertices.Add(window.Document.Vertices[0].Id); window.CopySelection();
        Load(new Plan()); window.PasteSelection(); var single = window.Document;
        check(single.Vertices.Count == 1 && single.Edges.Count == 0 && canvas.Mode == SelectionMode.Vertex, "A single copied point can be pasted and selected independently");
        Drag(single.Vertices[0].P, single.Vertices[0].P + new V2(32, 32)); single.Normalize();
        check(single.Vertices.Count == 1 && Plan.Parse(single.Serialize()).Vertices.Count == 1, "Standalone pasted points survive moving, normalization and saving");
        canvas.DeleteSelection(); check(single.Vertices.Count == 0, "A standalone pasted point can be deleted"); window.Undo(); check(window.Document.Vertices.Count == 1, "Deleting a standalone point is undoable");
        canvas.SelectedVertices.Add(window.Document.Vertices.Single().Id); window.AssignSelectionToLevel(2);
        check(window.Document.Vertices.Count == 1 && window.Document.Vertices.Single().Level == 2 && canvas.SelectedVertices.Count == 1, "Standalone points transfer between levels without leaving a copy or disappearing");
        window.Duplicate(); check(window.Document.Vertices.Count == 2 && canvas.SelectedVertices.Count == 1, "Standalone points can also be duplicated");
        var notes = new Plan(); notes.Annotations.Add(new Annotation { Id = notes.NextId++, Text = "Objectif\nA", Size = 24, Color = "#CC4499", Level = 0 }); Load(notes); canvas.SelectedAnnotation = notes.Annotations[0].Id; window.CopySelection();
        Load(new Plan { ActiveLevel = -1, Levels = new() { -1 } }); window.PasteSelection();
        check(window.Document.Annotations.Single().Text == "Objectif\nA" && window.Document.Annotations.Single().Level == -1 && window.Document.Annotations.Single().Size == 24 && canvas.SelectedAnnotation != null, "Clipboard preserves annotation text and style on the destination level");
        // Legacy preferences keep custom actions even when their key conflicts with a new default.
        var settings = new ShortcutSettings(); foreach (var label in new[] { "Copier", "Couper", "Coller", "Niveaux supérieurs visibles" }) settings.Bindings.Remove(label);
        settings.Bindings["Dessin"] = new(Key.H, ModifierKeys.None);
        string prefs = Path.Combine(output, "legacy-preferences-06.json"); File.WriteAllText(prefs, JsonSerializer.Serialize(settings.Bindings));
        settings = ShortcutSettings.Load(prefs);
        check(settings.Bindings["Dessin"] == new Shortcut(Key.H, ModifierKeys.None) && settings.Bindings["Niveaux supérieurs visibles"] != settings.Bindings["Dessin"] && settings.Bindings.ContainsKey("Coller"), "Preference upgrade preserves custom bindings and resolves new shortcut conflicts");
        window.Shortcuts = new(); Load(Square()); canvas.SelectAll(); window.Shortcuts.Bindings["Copier"] = new(Key.F6, ModifierKeys.None);
        var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.F6) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        clipboard.Data = null; window.RaiseEvent(key); check(clipboard.Data != null && key.Handled, "Remapped Copy dispatches through the normal keyboard handler"); window.Shortcuts = new();
        var layered = Square(); layered.ActiveLevel = 1;
        layered.Polygon(new V2[] { new(192, 0), new(320, 0), new(320, 128), new(192, 128) }); layered.Fills[layered.Faces().Single(f => f.Level == 1).Key] = "#FFFFFF";
        layered.Annotations.Add(new Annotation { Id = layered.NextId++, Level = 1, X = 208, Y = 100, Text = "Hidden text" }); layered.ActiveLevel = 0;
        Load(layered); canvas.ShowGrid = false; canvas.Center = new(160, 64); canvas.Zoom = 1; canvas.ClearSelection();
        var viewMenu = Desc<Menu>(window).Single().Items.OfType<MenuItem>().Single(m => m.Header as string == "Affichage");
        var upper = viewMenu.Items.OfType<MenuItem>().Single(m => m.Header as string == "Afficher les niveaux supérieurs");
        check(!canvas.ShowUpperLevels && !upper.IsChecked, "Upper geometry is hidden by default");
        upper.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); check(canvas.ShowUpperLevels && upper.IsChecked, "Display menu toggles upper geometry and its checked state");
        canvas.InvalidateVisual(); Pump();
        byte[] Pixel(V2 world)
        {
            var bitmap = new RenderTargetBitmap((int)canvas.ActualWidth, (int)canvas.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas); var screen = canvas.Screen(world); var pixels = new byte[4]; bitmap.CopyPixels(new Int32Rect((int)screen.X, (int)screen.Y, 1, 1), pixels, 4, 0); return pixels;
        }
        var red = Pixel(new(240, 0)); var nextRow = Pixel(new(240, 3)); var upperInside = Pixel(new(250, 50));
        check(red[2] > red[1] * 2 && red[2] > red[0] * 2 && nextRow[2] == 24, "Upper outlines render bright red and thin without filling their surfaces");
        check(upperInside[2] == 24 && upperInside[1] == 31, "Upper fills and annotations remain hidden");
        string protectedState = layered.Serialize(); Drag(new(240, 0), new(272, 32)); check(layered.Serialize() == protectedState && canvas.SelectionPoints().Count == 0, "Visible upper outlines cannot be selected or moved from a lower level");
        window.SetTool(EditorTool.Paint); canvas.PaintLines = true; canvas.PointerDown(canvas.Screen(new(240, 0)), MouseButton.Left); canvas.PointerUp(MouseButton.Left); check(layered.Serialize() == protectedState, "Painting cannot modify visible upper geometry");
        var xml = XDocument.Parse(MainWindow.Svg(layered, true)); var ns = xml.Root!.Name.Namespace; var outline = xml.Descendants(ns + "g").Single(g => g.Attribute("data-upper-levels") != null);
        check((string?)outline.Attribute("stroke") == "#FF2020" && outline.Elements(ns + "line").Count() == 4 && !outline.Descendants(ns + "polygon").Any(), "SVG follows upper-level display using thin red outlines only");
        check(!MainWindow.Svg(layered).Contains("data-upper-levels"), "SVG omits upper outlines when the option is disabled");
        window.ExportPngTo(Path.Combine(output, "coat-06-niveaux.png"));
        window.SetTool(EditorTool.None); canvas.ShowGrid = true; canvas.InvalidateVisual(); window.UpdateLayout(); Pump(); MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-06-editor.png"));
        key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.H) { RoutedEvent = Keyboard.PreviewKeyDownEvent }; window.RaiseEvent(key);
        check(!canvas.ShowUpperLevels && !upper.IsChecked && key.Handled, "H toggles upper levels and keeps the menu synchronized");
        var legacy = Square(); legacy.Version = 4; check(Plan.Parse(legacy.Serialize()).Version == 6, "Previous project format migrates to the standalone-point-capable format");
    }
    private static IEnumerable<T> Desc<T>(DependencyObject parent) where T : DependencyObject { if (parent is T t) yield return t; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var child in Desc<T>(VisualTreeHelper.GetChild(parent, i))) yield return child; }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
}

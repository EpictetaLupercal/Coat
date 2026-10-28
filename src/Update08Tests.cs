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

internal static class Update08Tests
{
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        string path = Path.Combine(output, "palette-08.json");
        var palette = new ColorPalette(path);
        check(palette.Colors.Count == 12 && palette.Colors.All(c => c == null), "New palettes contain twelve empty slots and no predefined colors");
        palette.Add("#aabbcc"); palette.Add("#112233"); palette.Add("#445566");
        check(ColorPalette.Load(path).Colors.Take(3).SequenceEqual(new[] { "#AABBCC", "#112233", "#445566" }), "Added colors persist in their slots with normalized hex values");
        palette.Delete(1);
        check(ColorPalette.Load(path).Colors[1] == null && palette.Colors[2] == "#445566", "Deleting a color leaves a persistent empty slot without shifting other colors");
        check(palette.Add("#102030") == 1 && palette.Colors[2] == "#445566", "Adding reuses the first free slot");
        check(palette.Add("#aabbcc") == 0 && palette.Colors.Count(c => c != null) == 3, "Adding an already saved color does not waste another slot");
        bool invalid = false; try { palette.Add("#broken"); } catch (InvalidDataException) { invalid = true; }
        check(invalid && palette.Colors.Count(c => c != null) == 3, "Invalid color input cannot enter the saved palette");
        for (int i = 3; i < 12; i++) palette.Add($"#{i:X6}");
        var full = palette.Colors.ToArray(); bool rejected = false;
        try { palette.Add("#ABCDEF"); } catch (InvalidOperationException) { rejected = true; }
        check(rejected && palette.Colors.SequenceEqual(full), "A full palette rejects additional colors without replacing a saved color");
        string badPath = Path.Combine(output, "palette-08-corrupt.json"); File.WriteAllText(badPath, "[\"invalid\"]");
        rejected = false; try { ColorPalette.Load(badPath); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "Malformed saved palettes are rejected explicitly");
        var failing = new ColorPalette(output); rejected = false;
        try { failing.Add("#123456"); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { rejected = true; }
        check(rejected && failing.Colors.All(c => c == null), "A failed palette save does not pretend that a color has been saved");

        window.SetTool(EditorTool.Paint); window.UpdateLayout(); Pump();
        var sidebar = Desc<PaletteView>(window).Single();
        check(sidebar.Children.Count == 12 && sidebar.Children.OfType<Button>().All(b => !b.IsEnabled && b.ContextMenu == null), "Paint sidebar displays twelve empty squares without preset colors");
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-08-palette-vide.png"));
        var picker = new ColorPicker(window, "#1267AB"); picker.Show(); picker.UpdateLayout(); Pump();
        Button Add() => Desc<Button>(picker).Single(b => AutomationProperties.GetName(b) == "Enregistrer la couleur dans la palette");
        Add().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.SavedColors.Colors[0] == "#1267AB" && sidebar.Children.OfType<Button>().First().IsEnabled, "Wheel plus button saves the chosen color and refreshes the sidebar immediately");
        picker.HexInput.Text = "#"; Add().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.SavedColors.Colors.Count(c => c != null) == 1, "Wheel plus does not silently save a stale color when hex is invalid");
        picker.HexInput.Text = "#F1C080"; Add().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var squares = Desc<PaletteView>(picker).Single();
        squares.Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(picker.SelectedHex == "#1267AB" && picker.HexInput.Text == "#1267AB", "Clicking a saved swatch restores the wheel and hex color");
        var delete = (MenuItem)squares.Children.OfType<Button>().First().ContextMenu.Items[0];
        delete.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(delete.Header as string == "Supprimer" && window.SavedColors.Colors[0] == null && window.SavedColors.Colors[1] == "#F1C080", "Saved swatches provide a Delete context action that frees the chosen slot");
        picker.HexInput.Text = "#448855"; Add().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.SavedColors.Colors[0] == "#448855", "Wheel plus reuses a slot freed through the context menu");
        picker.UpdateLayout(); Pump(); MainWindow.SaveVisual(picker, Path.Combine(output, "coat-08-roue.png"));

        // Exercise the actual canvas rendering and modal pipette path without moving the user's cursor.
        var point = window.Canvas.PointToScreen(new Point(20, 50));
        var screen = ColorSampling.FromVisual(window.Canvas);
        check(screen.Bitmap.PixelWidth > 0 && screen.TryColor(point, out _), "Pipette renders the visible canvas at its screen DPI and can address a plan pixel");
        screen.TryColor(point, out var expected);
        bool timedOut = false;
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        watchdog.Tick += (_, _) => { timedOut = true; foreach (var overlay in Application.Current.Windows.OfType<CanvasColorPicker>().ToArray()) overlay.Close(); };
        watchdog.Start();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => Application.Current.Windows.OfType<CanvasColorPicker>().Single().PickAt(point)));
        Desc<Button>(picker).Single(b => AutomationProperties.GetName(b) == "Pipette").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(picker.SelectedHex == ColorMath.Hex(expected) && !Application.Current.Windows.OfType<CanvasColorPicker>().Any(), "Pipette selects an actual rendered plan pixel and returns to the wheel");
        string previous = picker.SelectedHex;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var overlay = Application.Current.Windows.OfType<CanvasColorPicker>().Single();
            overlay.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(overlay), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        }));
        Desc<Button>(picker).Single(b => AutomationProperties.GetName(b) == "Pipette").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(picker.SelectedHex == previous && !Application.Current.Windows.OfType<CanvasColorPicker>().Any(), "Escape cancels pipette sampling without changing the chosen color");
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var overlay = Application.Current.Windows.OfType<CanvasColorPicker>().Single();
            overlay.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.MouseRightButtonDownEvent });
        }));
        Desc<Button>(picker).Single(b => AutomationProperties.GetName(b) == "Pipette").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(picker.SelectedHex == previous && !Application.Current.Windows.OfType<CanvasColorPicker>().Any(), "Right-click cancels the pipette and closes its overlay");
        picker.Close();
        check(window.SavedColors.Colors[0] == "#448855", "Explicitly saved palette changes survive cancelling the color dialog");
        sidebar.Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.Canvas.PaintColor == "#448855", "Saved sidebar colors can be selected for painting");
        sidebar = Desc<PaletteView>(window).Single();
        ((MenuItem)sidebar.Children.OfType<Button>().First().ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(window.Canvas.PaintColor == "#448855" && window.SavedColors.Colors[0] == null, "Deleting a saved swatch leaves the active paint color unchanged");
        var modal = new ColorPicker(window, "#123456");
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var overlay = Application.Current.Windows.OfType<CanvasColorPicker>().Single();
                MainWindow.SaveVisual((FrameworkElement)overlay.Content, Path.Combine(output, "coat-08-pipette.png"));
                overlay.PickAt(point);
            }));
            Desc<Button>(modal).Single(b => AutomationProperties.GetName(b) == "Pipette").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Desc<Button>(modal).Single(b => b.Content as string == "Utiliser cette couleur").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        check(modal.ShowDialog() == true && modal.SelectedHex == ColorMath.Hex(expected), "Pipette works inside the modal wheel and returns its chosen color for application");
        watchdog.Stop();
        check(!timedOut && window.IsEnabled, "Pipette selection and cancellation finish promptly and restore the editor input");
        var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgr32, null, new byte[] { 3, 2, 1, 0, 6, 5, 4, 0 }, 8);
        var negative = new ScreenFrame(bitmap, -1920, -200);
        check(negative.TryColor(new(-1919, -200), out var sampled) && sampled == Color.FromRgb(4, 5, 6) && !negative.TryColor(new(-1918, -200), out _) && !negative.TryColor(new(-1921, -200), out _), "Pipette sampling handles negative desktop coordinates and rejects pixels outside the frame");

        foreach (var kind in Enum.GetValues<StairKind>())
        {
            var lines = Stairs.Build(kind, new(), new(384, 0), 12, 96, 270, true, 32);
            int outlines = kind switch { StairKind.Straight => 4, StairKind.L => 6, StairKind.U => 8, _ => 98 };
            check(lines.Count(l => l.Style == LineStyle.Solid) == outlines && lines.Count(l => l.Style == LineStyle.LongDash) == (kind is StairKind.L or StairKind.U ? 12 : 11), $"{kind}: continuous contours and native long-dashed treads are generated separately");
            var plan = new Plan(); plan.AddStair(lines, kind);
            check(plan.Edges.All(e => lines.Any(l => l.Style == e.Style && Plan.Distance(plan.Get(e.A).P, l.A, l.B) < 1e-6 && Plan.Distance(plan.Get(e.B).P, l.A, l.B) < 1e-6)), $"{kind}: normalization preserves contour and tread styles after splitting");
            var restored = Plan.Parse(plan.Serialize());
            check(restored.Edges.Select(e => e.Style).SequenceEqual(plan.Edges.Select(e => e.Style)) && MainWindow.Svg(plan).Contains("stroke-dasharray=\"16 8\""), $"{kind}: native tread styles survive project saving and SVG export");
        }
        var circle = Stairs.Build(StairKind.Spiral, new(), new(192, 0), 24, 96, 360, true, 32);
        check(circle.Count(l => l.Style == LineStyle.LongDash) == 24, "Full-turn spiral keeps both circular contours solid and every radial tread dashed");
        var crossing = new Plan(); crossing.AddStair(Stairs.Build(StairKind.Straight, new(), new(384, 0), 12, 96, 270, true, 32), StairKind.Straight);
        crossing.Segment(new(-32, 0), new(416, 0));
        check(crossing.Edges.Count(e => e.Style == LineStyle.LongDash) == 22, "New intersections split stair treads without losing long dashes");
        window.LoadDocument(new Plan(), ""); window.SetTool(EditorTool.Shapes); var canvas = window.Canvas; canvas.Shape = ShapeKind.Stairs; canvas.Stair = StairKind.Straight; canvas.StairSteps = 12;
        canvas.PointerDown(canvas.Screen(new(0, 0)), MouseButton.Left); canvas.PointerMove(canvas.Screen(new(384, 0))); canvas.PointerUp(MouseButton.Left);
        check(window.Document.Edges.Count(e => e.Style == LineStyle.LongDash) == 11, "Stair drawing gesture applies native long dashes without a separate styling step");
        string before = window.Document.Serialize(); window.Undo(); window.Redo();
        check(window.Document.Serialize() == before, "Undo and redo retain the generated stair line styles");
        window.SetTool(EditorTool.Select); canvas.SelectAll();
        check(window.CaptureSelection().Edges.Count(e => e.Style == LineStyle.LongDash) == 11, "Copied staircase fragments preserve the long-dashed treads");
        window.RotateSelection(90); window.ScaleSelection(2, 2);
        check(window.Document.Edges.Count(e => e.Style == LineStyle.LongDash) == 11, "Rotation and scaling preserve the staircase contour and tread styles");
        window.LoadDocument(Plan.Parse(File.ReadAllText(Path.Combine(output, "Exemple-escaliers.coat"))), "");
        window.SetTool(EditorTool.None); canvas.Fit(); window.UpdateLayout(); Pump();
        window.ExportPngTo(Path.Combine(output, "coat-08-escaliers.png"));
    }
    private static IEnumerable<T> Desc<T>(DependencyObject parent) where T : DependencyObject { if (parent is T item) yield return item; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var child in Desc<T>(VisualTreeHelper.GetChild(parent, i))) yield return child; }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
}

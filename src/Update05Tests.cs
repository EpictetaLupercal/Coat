// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Coat;
internal static class Update05Tests
{
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        check(ColorMath.FromHsv(0, 1, 1) == Colors.Red && ColorMath.FromHsv(120, 1, 1) == Colors.Lime && ColorMath.FromHsv(240, 1, 1) == Colors.Blue, "Color wheel produces accurate primary colors");
        check(ColorMath.FromHsv(200, 0, 1) == Colors.White && ColorMath.FromHsv(200, 1, 0) == Colors.Black, "Wheel saturation and brightness reach white and black");
        var colors = new[] { "#66C5AD", "#A28555", "#121314", "#FFFFFF", "#000000", "#8A007E" };
        check(colors.All(s => { ColorMath.TryHex(s, out var c); var (h, sat, v) = ColorMath.ToHsv(c); return ColorMath.Hex(ColorMath.FromHsv(h, sat, v)) == s; }), "Opening a palette preserves exact RGB colors through HSV conversion");
        check(!ColorMath.TryHex("#GG1122", out _) && !ColorMath.TryHex("#12", out _) && ColorMath.TryHex(" 66c5ad ", out var parsed) && ColorMath.Hex(parsed) == "#66C5AD", "Hex field accepts valid colors and rejects incomplete input");
        var picker = new ColorPicker(window, "#66C5AD"); picker.Show(); picker.UpdateLayout(); Pump();
        check(picker.SelectedHex == "#66C5AD", "Palette starts with the active paint color");
        picker.Wheel.Value = 1; picker.Wheel.Pick(new Point(picker.Wheel.ActualWidth, picker.Wheel.ActualHeight / 2));
        check(picker.SelectedHex == "#FF0000" && picker.HexInput.Text == "#FF0000", "Dragging to the wheel boundary selects fully saturated red and updates hex");
        picker.Wheel.Pick(new Point(picker.Wheel.ActualWidth / 2, picker.Wheel.ActualHeight / 2));
        check(picker.SelectedHex == "#FFFFFF", "Clicking the wheel center produces white at full brightness");
        picker.HexInput.Text = "#3282CF"; check(picker.SelectedHex == "#3282CF", "Editing hex updates the wheel color");
        picker.HexInput.Text = "#wrong"; check(picker.SelectedHex == "#3282CF", "Invalid hex leaves the last valid color intact");
        picker.SetColor(parsed); picker.UpdateLayout(); Pump(); MainWindow.SaveVisual(picker, Path.Combine(output, "coat-05-palette.png")); picker.Close();
        picker = new ColorPicker(window, "#66C5AD");
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => { picker.HexInput.Text = "#AA4499"; Desc<Button>(picker).Single(b => b.Content as string == "Utiliser cette couleur").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }));
        check(picker.ShowDialog() == true && picker.SelectedHex == "#AA4499", "Palette confirmation returns the chosen color through the dialog");
        picker = new ColorPicker(window, "#66C5AD");
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => { picker.HexInput.Text = "#AA4499"; Desc<Button>(picker).Single(b => b.Content as string == "Annuler").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }));
        check(picker.ShowDialog() != true, "Cancelling the palette returns no applied change");
        var plan = Plan.Demo(); window.LoadDocument(plan, ""); window.Width = 1200; window.Height = 820; window.SetTool(EditorTool.Paint); window.UpdateLayout(); Pump();
        var button = Desc<Button>(window).Single(b => b.Content as string == "◉  Roue de couleurs…");
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<ColorPicker>().Single(); dialog.HexInput.Text = "#2288AA";
            Desc<Button>(dialog).Single(b => b.Content as string == "Utiliser cette couleur").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); check(window.Canvas.PaintColor == "#2288AA", "Paint inspector button applies the wheel color to the active palette");
        window.Canvas.PaintLines = false; window.Canvas.PointerDown(window.Canvas.Screen(new(-256, 0)), MouseButton.Left); window.Canvas.PointerUp(MouseButton.Left);
        check(plan.Fills[plan.HitFace(new(-256, 0))!.Key] == "#2288AA", "Color chosen in wheel is used by the fill gesture");
        window.SetTool(EditorTool.Select); var canvas = window.Canvas; canvas.Mode = SelectionMode.Edge; canvas.SelectedEdges.Add(plan.Edges[0].Id);
        canvas.Center = new(90, -70); canvas.Zoom = .67; canvas.ShowGrid = true; canvas.ShowLengths = false; canvas.InvalidateVisual(); window.UpdateInspector(); window.UpdateLayout(); Pump();
        var center = canvas.Center; double zoom = canvas.Zoom; string state = plan.Serialize(); var selection = canvas.SelectedEdges.ToArray();
        var planPath = Path.Combine(output, "coat-051-grille.png");
        window.ExportPngTo(planPath);
        var view = ReadBitmap(planPath);
        check(view.PixelWidth == (int)(canvas.ActualWidth * 2) && view.PixelHeight == (int)(canvas.ActualHeight * 2), "PNG exports only the visible canvas, excluding menus and sidebar");
        check(canvas.Center == center && canvas.Zoom == zoom && plan.Serialize() == state && canvas.SelectedEdges.SetEquals(selection) && !canvas.Exporting && !canvas.ExportGuides, "PNG keeps the displayed framing, document and selection unchanged");
        // The scale is the same pixel strip as the live view, including its units and both end ticks.
        canvas.InvalidateVisual(); canvas.UpdateLayout(); Pump();
        var livePath = Path.Combine(output, "coat-05-live.png"); MainWindow.SaveVisual(canvas, livePath, 2); var live = ReadBitmap(livePath);
        int sx = 84, sy = (int)((canvas.ActualHeight - 55) * 2), sw = 380, sh = 70;
        var scalePixels = Pixels(view, new Int32Rect(sx, sy, sw, sh)); var livePixels = Pixels(live, new Int32Rect(sx, sy, sw, sh));
        check(scalePixels.SequenceEqual(livePixels), "PNG reproduces the visible scale bar and unit label exactly");
        check(Pixels(view, new Int32Rect(0, 0, view.PixelWidth, 42)).SequenceEqual(Pixels(live, new Int32Rect(0, 0, live.PixelWidth, 42))), "PNG includes the top coordinate ruler");
        bool failed = false; try { window.ExportPngTo(Path.Combine(output, "absent-directory", "fail.png")); } catch (DirectoryNotFoundException) { failed = true; }
        check(failed && !canvas.Exporting && !canvas.ExportGuides && canvas.Zoom == zoom && canvas.Center == center, "Failed export restores normal display without changing the view");
        window.SetTool(EditorTool.Paint); window.UpdateLayout(); Pump(); MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-05-editor.png"));
    }
    private static BitmapSource ReadBitmap(string path) { using var stream = File.OpenRead(path); return BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad); }
    private static byte[] Pixels(BitmapSource bitmap, Int32Rect rectangle) { var data = new byte[rectangle.Width * rectangle.Height * 4]; bitmap.CopyPixels(rectangle, data, rectangle.Width * 4, 0); return data; }
    private static IEnumerable<T> Desc<T>(DependencyObject parent) where T : DependencyObject { if (parent is T t) yield return t; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var child in Desc<T>(VisualTreeHelper.GetChild(parent, i))) yield return child; }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
}

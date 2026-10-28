// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

namespace Coat;
internal static class Update11Tests
{
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        window.Width = 1440; window.Height = 900;
        var plan = new Plan();
        void Room(int level, double x)
        {
            plan.ActiveLevel = level;
            plan.Polygon(new V2[] { new(x, 0), new(x + 128, 0), new(x + 128, 128), new(x, 128) });
            plan.Fills[plan.Faces().Single(f => f.Level == level).Key] = "#FFFFFF";
        }
        Room(-2, -300); Room(0, 0); Room(2, 300); plan.ActiveLevel = 0;
        plan.Annotations.Add(new Annotation { Id = plan.NextId++, Level = -2, X = -300, Y = -32, Text = "Lower note" });
        plan.Annotations.Add(new Annotation { Id = plan.NextId++, Level = -2, X = -8000, Y = 0, Text = "Distant lower note" });
        window.LoadDocument(plan, ""); window.SetTool(EditorTool.Select);
        var canvas = window.Canvas; canvas.ShowGrid = false; canvas.ShowUpperLevels = true;
        canvas.Center = new(64, 64); canvas.Zoom = 1; window.UpdateLayout(); Pump();
        MenuItem LowerMenu() => Desc<Menu>(window).Single().Items.OfType<MenuItem>().Single(m => (string)m.Header == L.T("Affichage"))
            .Items.OfType<MenuItem>().Single(m => (string)m.Header == L.T("Afficher les niveaux inférieurs"));
        check(canvas.ShowLowerLevels && LowerMenu().IsChecked, "Lower levels are shown by default with a checked View menu item");
        string state = plan.Serialize(); bool undo = window.History.CanUndo;
        byte[] Pixel(V2 world)
        {
            canvas.InvalidateVisual(); Pump();
            var bitmap = new RenderTargetBitmap((int)canvas.ActualWidth, (int)canvas.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas);
            var screen = canvas.Screen(world); var bytes = new byte[4]; bitmap.CopyPixels(new Int32Rect((int)screen.X, (int)screen.Y, 1, 1), bytes, 4, 0); return bytes;
        }
        var visibleFill = Pixel(new(-240, 64)); var activeFill = Pixel(new(64, 64)); var upperLine = Pixel(new(340, 0));
        LowerMenu().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(!canvas.ShowLowerLevels && !LowerMenu().IsChecked && canvas.ShowUpperLevels, "Lower visibility toggle is independent of upper outlines");
        var hiddenFill = Pixel(new(-240, 64)); var hiddenEdge = Pixel(new(-240, 0)); var hiddenPoint = Pixel(new(-300, 0));
        check(visibleFill[2] > hiddenFill[2] && hiddenFill[2] == 24 && hiddenEdge[2] == 24 && hiddenPoint[2] == 24, "Hidden lower levels render neither fills, edges, nor vertices");
        check(activeFill.SequenceEqual(Pixel(new(64, 64))) && upperLine.SequenceEqual(Pixel(new(340, 0))), "Active geometry and upper outlines retain their appearance");
        check(plan.Serialize() == state && window.History.CanUndo == undo, "Visibility changes leave document geometry and undo history intact");
        var xml = XDocument.Parse(MainWindow.Svg(plan, true, false)); var ns = xml.Root!.Name.Namespace;
        check(xml.Descendants(ns + "g").Where(g => g.Attribute("data-level") != null).All(g => (string?)g.Attribute("data-level") == "0") && xml.Descendants(ns + "text").Count() == 0 && !xml.ToString().Contains("Distant lower note"), "SVG omits hidden lower geometry and annotations");
        check(xml.Descendants(ns + "g").Any(g => g.Attribute("data-upper-levels") != null), "SVG can show upper outlines while hiding lower levels");
        check(MainWindow.Svg(plan).Contains("Lower note"), "Default SVG behavior still includes lower levels");
        string png = Path.Combine(output, "coat-11-lower-hidden.png"); window.ExportPngTo(png);
        var exported = new BitmapImage(new Uri(png)); var position = canvas.Screen(new(-240, 64)); var pixel = new byte[4];
        exported.CopyPixels(new Int32Rect((int)(position.X * 2), (int)(position.Y * 2), 1, 1), pixel, 4, 0);
        check(pixel[2] == 24 && pixel[1] == 31, "PNG export respects lower-level visibility");
        window.ChangeLanguage("fr"); window.UpdateLayout(); Pump(); check(!LowerMenu().IsChecked && !canvas.ShowLowerLevels, "French menu retains hidden lower-level state");
        window.ChangeLanguage("en"); window.UpdateLayout(); Pump(); check(!LowerMenu().IsChecked && !canvas.ShowLowerLevels, "English menu retains hidden lower-level state");
        canvas.ShowUpperLevels = false; canvas.Fit();
        check(canvas.Center == new V2(64, 64), "Frame All ignores hidden lower vertices and distant annotations");
        LowerMenu().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); canvas.Fit();
        check(canvas.ShowLowerLevels && LowerMenu().IsChecked && canvas.Center.X < -3000, "Showing lower levels restores their geometry and annotation framing");
        canvas.ShowGrid = true;
        // Regression cases for the broad-phase bounds used by normalization.
        var crossing = new Plan();
        crossing.Segment(new(-1e8, 0), new(0, 0)); crossing.Segment(new(.25, -1), new(.25, 1));
        check(crossing.Edges.Count == 2 && crossing.Vertices.Count == 4, "Long-segment endpoint tolerance does not create a false crossing");
        var near = new Plan(); near.AddEdge(near.Point(new(0, 0)), near.Point(new(128, 0)));
        int kept = near.Point(new(64, .0000005)); near.Get(kept).KeepPoint = true; near.Normalize();
        check(near.Edges.Count == 2 && near.Get(kept).KeepPoint, "Near-collinear standalone points still split edges within the original tolerance");
        var distant = new Plan();
        for (int level = -1; level <= 1; level++)
        {
            distant.ActiveLevel = level;
            distant.Polygon(new V2[] { new(-128, -128), new(0, -128), new(0, 0), new(-128, 0) });
            var f = distant.Faces().Single(f => f.Level == level); distant.Fills[f.Key] = "#FF0000"; distant.FillPatterns[f.Key] = FillPattern.Dots;
        }
        var serialized = distant.Serialize(); distant.Normalize();
        check(serialized == distant.Serialize(), "Normalization is stable across coincident negative-coordinate levels with patterned fills");
        check(L.Missing.Count == 0, "All new level visibility messages have English translations");
    }
    private static IEnumerable<T> Desc<T>(DependencyObject parent) where T : DependencyObject { if (parent is T value) yield return value; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var child in Desc<T>(VisualTreeHelper.GetChild(parent, i))) yield return child; }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
}

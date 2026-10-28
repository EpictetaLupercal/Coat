// Coat — Epicteta Lupercal
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace Coat;
internal static class InternationalTests
{
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            window.ChangeLanguage("en");
            var plan = new Plan();
            plan.Polygon(new V2[] { new(-16.5, -8.5), new(128.5, -8.5), new(128.5, 64.5), new(-16.5, 64.5) });
            plan.Fills[plan.Faces().Single().Key] = "#FFFFFF"; plan.LevelHeights[0] = 12.5;
            const string note = "Entrance — 入口 — مدخل — Entrée";
            plan.Annotations.Add(new Annotation { Id = plan.NextId++, Text = note, X = 16.5, Y = 24.5, Size = 18.5 });
            string json = plan.Serialize(), map = MapExporter.Export(plan).Text, svg = MainWindow.Svg(plan);
            foreach (string culture in new[] { "en-US", "de-DE", "ja-JP", "ar-EG" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                window.ChangeLanguage("en"); window.LoadDocument(Plan.Parse(json), ""); window.SetTool(EditorTool.Select); window.UpdateLayout(); Pump();
                check(window.Title.Contains("Untitled") && Desc<Menu>(window).Single().Items.OfType<MenuItem>().First().Header as string == "File", $"English UI remains English with {culture} numeric settings");
                check(window.Document.Serialize() == json && window.Document.Annotations.Single().Text == note, $"Fractional coordinates and Unicode project content round-trip under {culture}");
                var exported = XDocument.Parse(MainWindow.Svg(window.Document));
                check(MapExporter.Export(window.Document).Text == map && MainWindow.Svg(window.Document) == svg && exported.Root!.Element(exported.Root.Name.Namespace + "title")!.Value == "Coat plan", $"Map and SVG exports retain portable decimal syntax under {culture}");
                string path = Path.Combine(output, "Plan-入口-é-مدخل.coat"); MainWindow.WriteAtomic(path, json);
                check(Plan.Parse(File.ReadAllText(path)).Serialize() == json, $"Unicode project filenames can be saved and reopened under {culture}");
                window.UpdateStatus(new(12.5, 34.5));
                check(Desc<TextBlock>(window).Any(t => t.Text.StartsWith("X 12.5   Y 34.5", StringComparison.Ordinal)), $"English coordinate labels use English decimals under {culture}");
                void EnterHeight(string value)
                {
                    Desc<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Height").Text = value;
                    Desc<Button>(window).Single(b => b.Content as string == "Apply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    window.UpdateLayout(); Pump();
                }
                EnterHeight("24,5"); check(window.Document.LevelZ(0) == 24.5, $"Height accepts a decimal comma under {culture}");
                EnterHeight("36.5"); check(window.Document.LevelZ(0) == 36.5, $"Height accepts a decimal point under {culture}");
            }
            check(L.Missing.Count == 0, "International workflow has no missing English translations");
        }
        finally { CultureInfo.CurrentCulture = previousCulture; CultureInfo.CurrentUICulture = previousUiCulture; }
    }
    private static IEnumerable<T> Desc<T>(DependencyObject parent) where T : DependencyObject { if (parent is T item) yield return item; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var child in Desc<T>(VisualTreeHelper.GetChild(parent, i))) yield return child; }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
}

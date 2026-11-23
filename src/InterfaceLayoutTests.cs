// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace Coat;

internal static partial class InterfaceLayoutTests
{
    internal static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T match) yield return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var item in Children<T>(VisualTreeHelper.GetChild(parent,i))) yield return item;
    }
    internal static void Layout(Window window)
    {
        window.UpdateLayout();
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);
    }
    internal static void Measure(MainWindow window, string output)
    {
        window.ChangeLanguage("en");
        window.LoadDocument(Plan.Demo(),"");
        window.Width=1050; window.Height=680;
        var measurements = new List<string>();
        foreach (var tool in new[] { EditorTool.Select, EditorTool.Paint })
        {
            window.SetTool(tool);
            if(tool == EditorTool.Select) window.Canvas.SelectAll();
            window.UpdateInspector(); Layout(window);
            var scroll = Children<ScrollViewer>(window).Single(v=>v.Content is StackPanel);
            measurements.Add(FormattableString.Invariant($"{tool} {scroll.ExtentHeight:0} {scroll.ViewportHeight:0} {scroll.ScrollableHeight:0}"));
        }
        File.WriteAllLines(Path.Combine(output,"panel-measurements.txt"),measurements);
    }
}

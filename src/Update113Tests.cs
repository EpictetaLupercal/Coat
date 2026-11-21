// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Threading;
using System.Xml.Linq;
namespace Coat;
internal static class Update113Tests
{
    internal static void Run(MainWindow w,Action<bool,string> check,string output)
    {
        w.LoadDocument(new Plan(),""); w.SetTool(EditorTool.Draw); w.UpdateLayout();Pump();
        var draw=Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="Draw");var dimension=Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="Dimension");var erase=Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="Erase");
        check(draw.TranslatePoint(new(),w).X<dimension.TranslatePoint(new(),w).X && dimension.TranslatePoint(new(),w).X<erase.TranslatePoint(new(),w).X,"Dimension has its own toolbar button between Draw and Erase");
        var lines=Desc<ComboBox>(w).Single(b=>AutomationProperties.GetName(b)=="LINE STYLE"); check(lines.Items.Count==3,"Draw line-style dropdown no longer contains Dimension");
        dimension.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var c=w.Canvas; check(c.Tool==EditorTool.Dimension,"Dimension toolbar button activates the dedicated tool");
        void Click(V2 p){c.PointerDown(c.Screen(p),MouseButton.Left);c.PointerUp(MouseButton.Left);}
        Click(new(-256,0));Click(new(256,0));check(w.Document.Edges.Single().Dimension&&c.Anchor==null,"Two clicks finish a dimension without starting a continuous outline");
        w.SetTool(EditorTool.Select);c.Mode=SelectionMode.Edge;c.SelectAll();w.UpdateLayout();Pump();
        var font=Desc<TextBox>(w).Single(t=>AutomationProperties.GetName(t)=="Font Size");
        check(font.TranslatePoint(new(),w).Y+font.ActualHeight<w.ActualHeight-30,"Dimension font control is visible immediately after selection");font.Text="18*2";
        font.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});
        check(w.Document.Edges.Single().DimensionFontSize==36,"Selected dimension font size accepts an expression confirmed with Enter");
        var svg=XDocument.Parse(MainWindow.Svg(w.Document));check(svg.Descendants().Any(e=>e.Name.LocalName=="text"&&(string?)e.Attribute("font-size")=="36"),"SVG exports the chosen dimension font size");
        check(Plan.Parse(w.Document.Serialize()).Edges.Single().DimensionFontSize==36,"Dimension font size survives saving and reopening");
        check(w.CaptureSelection().Edges.Single().DimensionFontSize==36,"Clipboard capture preserves dimension font size");
        w.Undo();check(w.Document.Edges.Single().DimensionFontSize==12,"Font-size changes undo in one step");w.Redo();check(w.Document.Edges.Single().DimensionFontSize==36,"Redo restores dimension text size");
        c.SelectAll();w.Duplicate();check(w.Document.Edges.All(e=>e.DimensionFontSize==36),"Duplicate retains dimension font size");w.Undo();
        c.ClearSelection();w.SetTool(EditorTool.Draw);Click(new(-256,-128));Click(new(256,-128));c.Anchor=null;
        check(w.Document.Edges.Count(e=>e.Dimension)==1&&w.Document.Edges.Count(e=>!e.Dimension)==1,"Switching back to Draw creates ordinary lines after dimension drawing");
        c.ClearSelection();w.SetTool(EditorTool.Dimension);w.UpdateInspector();font=Desc<TextBox>(w).Single(t=>AutomationProperties.GetName(t)=="Font Size");font.Text="24";Desc<Button>(w).Single(b=>Equals(b.Content,"Apply Dimension")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Click(new(-256,128));Click(new(256,128));check(w.Document.Edges.Last().Dimension&&w.Document.Edges.Last().DimensionFontSize==24,"Dimension tool font setting applies to newly created dimensions");
        w.SetTool(EditorTool.Select);c.SelectedEdges.Add(w.Document.Edges.First(e=>e.Dimension).Id);w.UpdateInspector();c.Fit();w.UpdateLayout();Pump();
        MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-113-selected.png"));w.ExportPngTo(Path.Combine(output,"coat-113-fonts.png"));
        w.SetTool(EditorTool.Dimension);c.ClearSelection();w.UpdateInspector();w.UpdateLayout();Pump();MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-113-dimension.png"));
        w.Width=1050;w.Height=680;w.UpdateLayout();Pump();check(draw.ActualWidth>0&&erase.ActualWidth>0,"Toolbar remains laid out at the minimum supported window size");MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-113-compact.png"));w.Width=1440;w.Height=900;
        check(new ShortcutSettings().Bindings["Cote"]==ShortcutSettings.Unassigned,"Dimension tool shortcut remains unassigned by default");
        check(L.Missing.Count==0,"Dimension tool and font controls are localized");
    }
    private static IEnumerable<T> Desc<T>(DependencyObject p)where T:DependencyObject{if(p is T t)yield return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++)foreach(var c in Desc<T>(VisualTreeHelper.GetChild(p,i)))yield return c;}
    private static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
}

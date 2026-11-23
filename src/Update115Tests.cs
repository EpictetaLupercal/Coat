// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Threading;
namespace Coat;
internal static class Update115Tests
{
    internal static void Run(MainWindow w,Action<bool,string> check,string output)
    {
        w.LoadDocument(new Plan(),"");w.SetTool(EditorTool.Token);var c=w.Canvas;c.TokenShape=TokenKind.Skull;c.PaintColor="#FFBB00";c.Zoom=1;
        void Click(V2 p){c.PointerDown(c.Screen(p),MouseButton.Left);c.PointerUp(MouseButton.Left);}
        Click(new(-128,0));int first=w.Document.Annotations.Single().Id;
        check(c.SelectedAnnotation==null&&!c.HasSelectionOrGesture,"New token is deselected immediately after placement");
        Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="Heart").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(w.Document.Annotations.Single().TokenShape==TokenKind.Skull&&c.TokenShape==TokenKind.Heart,"Choosing another icon leaves the previously placed token unchanged");
        Click(new(128,0));check(w.Document.Annotations.Select(a=>a.TokenShape).SequenceEqual(new[]{TokenKind.Skull,TokenKind.Heart})&&c.SelectedAnnotation==null,"Different tokens can be placed in sequence without deselecting manually");
        w.SetTool(EditorTool.Select);Click(new(-128,0));check(c.SelectedAnnotation==first,"Existing tokens remain selectable for editing");w.UpdateLayout();Pump();
        var wheel=Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="◉  Color wheel…");
        check(wheel.Content is Image,"Color button contains a wheel icon and active-color swatch");
        w.History.Clear();int windows=Application.Current.Windows.Count;
        wheel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var picker=w.ActiveColorPicker ?? throw new InvalidOperationException("Color popup did not open.");picker.UpdateLayout();Pump();
        check(Application.Current.Windows.Count==windows&&w.IsEnabled,"Color opens as a nonmodal popup without a new application window");
        check(!Desc<Button>(picker).Any(b=>Equals(b.Content,"Apply")||Equals(b.Content,"Use This Color")||Equals(b.Content,"Cancel")),"Live color popup has no confirmation buttons");
        picker.HexInput.Text="#112233";check(w.Document.Annotations.Single(a=>a.Id==first).Color=="#112233"&&w.ActiveColorPicker==picker,"Hex applies directly to the selected token while popup stays open");
        picker.HexInput.Text="#12";check(w.Document.Annotations.Single(a=>a.Id==first).Color=="#112233","Incomplete hex retains the last valid color");
        picker.HexInput.Text="#448855";Desc<Slider>(picker).Single(s=>AutomationProperties.GetName(s)=="Brightness").Value=90;
        check(w.Document.Annotations.Single(a=>a.Id==first).Color==picker.SelectedHex&&!w.History.CanUndo,"HSV changes are live and wait until popup closure to create undo history");
        string last=picker.SelectedHex;MainWindow.SaveVisual(picker,Path.Combine(output,"coat-115-color-popup.png"));
        w.CloseColorPicker();check(w.ActiveColorPicker==null&&w.Document.Annotations.Single(a=>a.Id==first).Color==last&&w.History.CanUndo,"Closing popup retains the color and commits its edit");
        w.Undo();check(w.Document.Annotations.Single(a=>a.Id==first).Color=="#FFBB00"&&!w.History.CanUndo,"One Undo reverses the entire live token color session");w.Redo();check(w.Document.Annotations.Single(a=>a.Id==first).Color==last,"Redo restores the final live color");
        w.SetTool(EditorTool.Token);c.ClearSelection();w.UpdateInspector();wheel=Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="◉  Color wheel…");wheel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));picker=w.ActiveColorPicker!;picker.HexInput.Text="#2255FF";
        check(c.PaintColor=="#2255FF","Default token color changes directly from the popup");
        var swatches=Desc<PaletteView>(picker).Single();var saved=swatches.Children.OfType<Button>().FirstOrDefault(b=>b.ContextMenu!=null);
        if(saved!=null){saved.ContextMenu.IsOpen=true;Pump();check(w.ActiveColorPicker==picker,"Saved-color context menu keeps the live popup open");saved.ContextMenu.IsOpen=false;}
        w.ColorPopup!.IsOpen=false;Pump();check(w.ActiveColorPicker==null&&c.PaintColor=="#2255FF","External popup dismissal retains the live color");Click(new(0,128));check(w.Document.Annotations.Last().Color=="#2255FF"&&c.SelectedAnnotation==null,"New token uses the live color and remains deselected");
        w.SetTool(EditorTool.Paint);wheel=Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="◉  Color wheel…");wheel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));picker=w.ActiveColorPicker!;picker.HexInput.Text="#AA2255";
        var samplePoint=c.PointToScreen(new Point(100,100));var frame=ColorSampling.FromVisual(c);frame.TryColor(samplePoint,out var expected);var oldCount=w.Document.Annotations.Count;
        Desc<Button>(picker).Single(b=>AutomationProperties.GetName(b)=="Eyedropper").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(c.IsColorSampling&&w.IsEnabled&&Application.Current.Windows.Count==windows,"Eyedropper samples on the canvas without disabling the editor or creating a window");c.SampleColorAt(samplePoint);
        check(c.PaintColor==ColorMath.Hex(expected)&&w.ActiveColorPicker==picker&&!c.IsColorSampling&&w.Document.Annotations.Count==oldCount,"Eyedropper applies a plan pixel live, returns to the popup and creates no geometry");
        Desc<Button>(picker).Single(b=>AutomationProperties.GetName(b)=="Eyedropper").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));w.CloseColorPicker();
        check(!c.IsColorSampling&&w.ActiveColorPicker==null,"Closing while sampling releases mouse capture and finalizes color editing");
        var p=new Plan();p.Polygon(new V2[]{new(-128,-128),new(128,-128),new(128,128),new(-128,128)});string key=p.Faces().Single().Key;p.Fills[key]="#112233";p.Gradients[key]=new("#FFFFFF",0);
        w.LoadDocument(p,"");w.SetTool(EditorTool.Select);c.Mode=SelectionMode.Face;c.SelectAll();w.SetTool(EditorTool.Paint);c.PaintLines=false;c.GradientMode=true;w.UpdateInspector();wheel=Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="End Color…");wheel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));w.ActiveColorPicker!.HexInput.Text="#ABCDEF";
        check(p.Gradients[key].EndColor=="#ABCDEF","Gradient end color changes directly on selected gradient faces");w.CloseColorPicker();
        w.UpdateLayout(); Desc<Expander>(w).Single(e=>Equals(e.Header,"MAP EXPORT")).IsExpanded=true; w.UpdateLayout();
        wheel=Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="Wall Color…");wheel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));w.ActiveColorPicker!.HexInput.Text="#334455";check(p.WallColor=="#334455","Wall classification color changes live");w.CloseColorPicker();
        c.ClearSelection();c.GradientMode=false;w.UpdateInspector();w.UpdateLayout();Pump();wheel=Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="◉  Color wheel…");wheel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));picker=w.ActiveColorPicker!;picker.UpdateLayout();Pump();MainWindow.SaveVisual(picker,Path.Combine(output,"coat-115-popup-full.png"));MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-115-toolbar.png"));w.CloseColorPicker();
        check(L.Missing.Count==0,"Live color and token workflow remain localized");
    }
    private static IEnumerable<T> Desc<T>(DependencyObject p)where T:DependencyObject{if(p is T t)yield return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++)foreach(var c in Desc<T>(VisualTreeHelper.GetChild(p,i)))yield return c;}
    private static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
}

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
internal static class Update112Tests
{
    internal static void Run(MainWindow w,Action<bool,string> check,string output)
    {
        var p=new Plan(); p.Segment(new(0,0),new(256,0)); var edge=p.Edges.Single(); edge.ArrowStart=edge.ArrowEnd=true; edge.ArrowStyle=ArrowKind.Open;
        var legacy=Plan.Parse(p.Serialize()); check(legacy.Edges[0].HeadStyle(true)==ArrowKind.Open && legacy.Edges[0].HeadStyle(false)==ArrowKind.Open,"Legacy common arrow styles remain compatible");
        w.LoadDocument(p,""); w.SetTool(EditorTool.Select); var c=w.Canvas; c.Mode=SelectionMode.Edge; c.SelectAll(); w.SetTool(EditorTool.Draw); w.UpdateLayout(); Pump();
        ComboBox Combo(string name)=>Desc<ComboBox>(w).Single(b=>AutomationProperties.GetName(b)==name);
        Combo("Arrow Start").SelectedIndex=3; Combo("Arrow End").SelectedIndex=2;
        check(w.Document.Edges[0].HeadStyle(true)==ArrowKind.Round && w.Document.Edges[0].HeadStyle(false)==ArrowKind.Diamond,"Start and End dropdowns configure independent round and diamond caps");
        check(!Desc<Button>(w).Any(b=>Equals(b.Content,"Double Arrow")||Equals(b.Content,"Apply Arrows")) && !Desc<CheckBox>(w).Any(b=>Equals(b.Content,"Arrow Start")),"Draw replaces arrow checkboxes and buttons with two visual dropdowns");
        var parsed=Plan.Parse(w.Document.Serialize()); check(parsed.Edges[0].StartArrowStyle==ArrowKind.Round && parsed.Edges[0].EndArrowStyle==ArrowKind.Diamond,"Independent caps survive project serialization");
        string svg=MainWindow.Svg(parsed); XDocument.Parse(svg); check(svg.Contains("<circle")&&svg.Contains("<polygon"),"SVG exports both independent cap shapes");
        Combo("Arrow Start").SelectedIndex=0; check(!w.Document.Edges[0].ArrowStart && w.Document.Edges[0].ArrowEnd,"None removes only the chosen endpoint cap"); w.Undo();
        c.SelectedEdges.Add(w.Document.Edges[0].Id); w.SetTool(EditorTool.Draw); w.UpdateInspector();
        var input=Desc<TextBox>(w).Single(t=>AutomationProperties.GetName(t)=="Arrow Size"); input.Text="8*3"; input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});
        check(w.Document.Edges[0].ArrowSize==24,"Arrow size validates arithmetic with Enter without an Apply button");
        w.Duplicate(); check(w.Document.Edges.Last().HeadStyle(true)==ArrowKind.Round&&w.Document.Edges.Last().HeadStyle(false)==ArrowKind.Diamond,"Duplicate preserves independent endpoint styles");
        var split=Plan.Parse(parsed.Serialize()); split.Segment(new(128,-32),new(128,32)); check(split.Edges.Count(e=>e.ArrowStart)==1&&split.Edges.Count(e=>e.ArrowEnd)==1&&split.Edges.Single(e=>e.ArrowStart).HeadStyle(true)==ArrowKind.Round,"Splitting retains independent heads only at the original endpoints");
        w.LoadDocument(new Plan(),""); w.SetTool(EditorTool.Draw); c.DrawAppearance.Dimension=false; c.ClearSelection(); w.UpdateInspector(); w.SetTool(EditorTool.Dimension);
        check(c.Tool==EditorTool.Dimension&&c.DimensionAppearance.Dimension,"Dimension tool presets two arrowheads and endpoint bars");
        void Click(V2 a){c.PointerDown(c.Screen(a),MouseButton.Left);c.PointerUp(MouseButton.Left);}
        Click(new(-128,0)); Click(new(128,0)); c.Anchor=null;
        edge=w.Document.Edges.Single(); check(edge.Dimension&&StrokeDecoration.Distance(w.Document.Get(edge.A).P,w.Document.Get(edge.B).P)=="256 u","Drawing a dimension records its endpoints and exact distance");
        svg=MainWindow.Svg(w.Document); check(svg.Contains("data-dimension=\"true\"")&&svg.Contains("256 u")&&svg.Split("<polyline").Length>=3,"Dimension SVG contains distance text and both perpendicular endpoint bars");
        check(Plan.Parse(w.Document.Serialize()).Edges[0].Dimension,"Dimension style survives saving and reopening");
        w.SetTool(EditorTool.Select); c.SelectAll(); w.ScaleSelection(2,1); svg=MainWindow.Svg(w.Document); check(svg.Contains("512 u"),"Dimension distance updates automatically after scaling");
        w.Duplicate(); check(w.Document.Edges.All(e=>e.Dimension),"Dimension duplication preserves its style"); w.Undo();
        // Save a comparison of ordinary independent caps and several dimensions.
        var demo=new Plan();
        void Add(V2 a,V2 b,bool dimension,ArrowKind start=ArrowKind.Triangle,ArrowKind end=ArrowKind.Triangle)
        { demo.Segment(a,b); var e=demo.Edges.Last(); e.Dimension=dimension; e.ArrowStart=e.ArrowEnd=true; e.StartArrowStyle=start;e.EndArrowStyle=end;e.ArrowSize=12; }
        Add(new(-160,128),new(160,128),false,ArrowKind.Round,ArrowKind.Diamond);
        Add(new(-160,32),new(160,32),true); Add(new(-160,-64),new(96,-192),true);
        w.LoadDocument(demo,""); w.SetTool(EditorTool.Draw); c.DrawAppearance.Dimension=false;c.DrawAppearance.StartArrowStyle=ArrowKind.Round;c.DrawAppearance.EndArrowStyle=ArrowKind.Diamond;c.DrawAppearance.ArrowStart=c.DrawAppearance.ArrowEnd=true;
        c.ShowLengths=false;c.Fit();w.UpdateInspector();w.UpdateLayout();Pump();MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-112-draw.png"));w.ExportPngTo(Path.Combine(output,"coat-112-dimensions.png"));
        File.WriteAllText(Path.Combine(output,"coat-112-dimensions.svg"),MainWindow.Svg(demo));
        check(L.Missing.Count==0,"Dimension and endpoint dropdown labels are localized");
    }
    private static IEnumerable<T> Desc<T>(DependencyObject p)where T:DependencyObject {if(p is T t)yield return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++)foreach(var c in Desc<T>(VisualTreeHelper.GetChild(p,i)))yield return c;}
    private static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
}

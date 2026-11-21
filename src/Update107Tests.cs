// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Threading;
namespace Coat;
internal static class Update107Tests
{
    internal static void Run(MainWindow w, Action<bool,string> check, string output)
    {
        var p = new Plan(); p.Polygon(new V2[] { new(13,17), new(205,17), new(205,209), new(13,209) });
        var key = p.Faces().Single().Key; p.Fills[key] = "#33AAFF"; p.Gradients[key] = new("#CC2255", 35);
        var edge = p.Edges[0]; edge.ArrowStart = edge.ArrowEnd = true; edge.ArrowSize = 24; edge.ArrowStyle = ArrowKind.Diamond;
        p.Annotations.Add(new Annotation { Id = p.NextId++, IsToken = true, X = 96, Y = 96, Color = "#FFCC00", Size = 32 });
        string original = p.Serialize(); var copy = Plan.Parse(original);
        check(copy.Annotations.Single().IsToken && copy.Gradients[key].Angle == 35 && copy.Edges[0].ArrowEnd, "Tokens, gradients and arrow settings round-trip through project JSON");
        string svg = MainWindow.Svg(p); System.Xml.Linq.XDocument.Parse(svg);
        check(svg.Contains("linearGradient") && svg.Contains("<circle") && svg.Contains("stop-color=\"#CC2255\""), "SVG exports gradient colors and tokens");
        check(ArrowGeometry.Svg(p,edge).Split("<polygon").Length == 3, "Double arrow exports both diamond heads");
        var split = Plan.Parse(p.Serialize()); split.Segment(new(109,-20),new(109,240));
        check(split.Gradients.Count == 2 && split.Gradients.Values.All(g => g.EndColor == "#CC2255"), "Splitting a gradient-filled face preserves its appearance settings");
        check(split.Edges.Count(e => e.ArrowStart) == 1 && split.Edges.Count(e => e.ArrowEnd) == 1, "Splitting an arrowed edge keeps arrows only at original endpoints");
        w.LoadDocument(p, ""); w.SetTool(EditorTool.Select); var c = w.Canvas; c.Zoom = 1;
        check(c.SnapAnnotation(new(15,18)) == new V2(13,17), "Annotation snapping finds off-grid vertices");
        check(c.SnapAnnotation(new(110,19)) == new V2(109,17), "Annotation snapping finds edge midpoints");
        c.SnapAnnotations = false; check(c.SnapAnnotation(new(15,18)) == new V2(15,18), "Disabling annotation snapping preserves exact coordinates"); c.SnapAnnotations = true;
        c.Mode = SelectionMode.Face; c.SelectAll(); w.Duplicate();
        check(w.Document.Gradients.Count >= 2 && w.Document.Edges.Any(e => e.ArrowEnd), "Duplicating geometry retains gradients and arrow appearance"); w.Undo();
        w.SetTool(EditorTool.Token); c.PointerDown(c.Screen(new(320,96)),MouseButton.Left); c.PointerUp(MouseButton.Left);
        check(w.Document.Annotations.Count(a => a.IsToken) == 2, "Token mode places a selectable colored marker");
        c.SelectedAnnotation=w.Document.Annotations.Last().Id; w.Duplicate(); check(w.Document.Annotations.Last().IsToken, "Token duplication retains its type");
        w.LoadDocument(Plan.Parse(original), ""); w.SetTool(EditorTool.Paint); c.GradientMode = true; c.GradientEnd = "#00FF88"; c.GradientAngle = 90; c.PaintColor = "#2255FF"; c.PaintLines = false;
        c.PointerDown(c.Screen(new(160,130)), MouseButton.Left); c.PointerUp(MouseButton.Left);
        check(w.Document.Gradients[key].EndColor == "#00FF88", "Gradient painting applies the configured colors to the clicked face");
        c.GradientMode = false; c.PointerDown(c.Screen(new(160,130)), MouseButton.Left); c.PointerUp(MouseButton.Left);
        check(!w.Document.Gradients.ContainsKey(key), "Solid fill replaces a previous gradient"); w.Undo();
        w.SetTool(EditorTool.Shapes); c.Shape = ShapeKind.Circle; w.UpdateInspector(); w.UpdateLayout(); Pump();
        var input = Desc<TextBox>(w).Single(t => AutomationProperties.GetName(t) == "Vertices"); input.Focus(); input.Text = "20+4";
        input.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
        check(c.CirclePoints == 25 && input.Text == "25", "Wheel increments a focused numeric expression and updates the tool");
        input.Text = "128"; input.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120) { RoutedEvent = Mouse.PreviewMouseWheelEvent }); check(c.CirclePoints == 128, "Wheel respects numeric field bounds");
        w.SetTool(EditorTool.Paint); c.GradientMode = false; w.UpdateInspector(); w.UpdateLayout(); Pump();
        check(Desc<Button>(w).Any(b => b.Content is Image && AutomationProperties.GetName(b) == "Stripes"), "Fill patterns use accessible visual icons");
        w.SetTool(EditorTool.Select); c.Mode = SelectionMode.Edge; c.SelectedEdges.Add(w.Document.Edges[0].Id); w.UpdateInspector(); c.Fit(); w.UpdateLayout(); Pump();
        MainWindow.SaveVisual((FrameworkElement)w.Content, Path.Combine(output,"coat-107-tools.png"));
        int selected = w.Document.Edges[0].Id; w.SetTool(EditorTool.Draw);
        check(w.ExecuteFeature("Flèche au début") && !w.Document.Edges.Single(e => e.Id == selected).ArrowStart, "Configurable arrow command toggles the selected edge"); w.Undo();
        c.SelectedEdges.Add(selected); w.UpdateInspector();
        check(w.ExecuteFeature("Triangle") && w.Document.Edges.Single(e => e.Id == selected).ArrowStyle == ArrowKind.Triangle, "Configurable arrow style command updates the selection"); w.Undo();
        w.ExportPngTo(Path.Combine(output,"coat-107-export.png"));
        check(new FileInfo(Path.Combine(output,"coat-107-export.png")).Length > 1000, "PNG export renders markers, gradients and arrows");
        check(L.Missing.Count == 0, "New visual tools are fully localized");
    }
    private static IEnumerable<T> Desc<T>(DependencyObject p) where T:DependencyObject { if(p is T t) yield return t; for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++) foreach(var c in Desc<T>(VisualTreeHelper.GetChild(p,i))) yield return c; }
    private static void Pump() { var f = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false)); Dispatcher.PushFrame(f); }
}

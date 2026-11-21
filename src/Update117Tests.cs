// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Threading;
namespace Coat;
internal static class Update117Tests
{
    internal static void Run(MainWindow w,Action<bool,string> check,string output)
    {
        var p=new Plan();p.Polygon(new V2[]{new(0,0),new(128,0),new(128,128),new(0,128)});p.Polygon(new V2[]{new(256,0),new(384,0),new(384,128),new(256,128)});
        p.GroupSelection(p.Vertices.Select(v=>v.Id),p.Edges.Select(e=>e.Id));string original=p.Serialize();
        w.LoadDocument(p,"");w.SetTool(EditorTool.Select);var c=w.Canvas;c.Mode=SelectionMode.Edge;c.Zoom=1;c.Transform=TransformKind.Translation;
        Button Toggle()=>Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="Ignore Groups");
        void Click(V2 a,int count=1){c.PointerDown(c.Screen(a),MouseButton.Left,count);c.PointerUp(MouseButton.Left);}
        check(Toggle()!=null&&!c.GroupsIgnored,"Ignore Groups button is available before selecting anything");
        Toggle().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(c.IgnoreGroups&&c.GroupsIgnored&&w.Document.Serialize()==original&&!w.History.CanUndo,"Group-selection toggle changes interaction only, preserving document and history");
        Click(new(64,0));check(c.SelectedEdges.Count==1,"Ignore Groups allows normal single-edge selection");
        w.Escape();check(c.IgnoreGroups&&!c.HasSelectionOrGesture,"Ignore Groups stays active after Escape and deselection");
        c.Mode=SelectionMode.Vertex;Click(new());check(c.SelectedVertices.Count==1&&c.SelectedEdges.Count==0,"Ignore Groups allows normal single-vertex selection");
        c.ClearSelection();c.Mode=SelectionMode.Face;Click(new(64,64));check(c.SelectedFaces.Count==1&&c.SelectionPoints().Count==4,"Ignore Groups allows normal single-face selection");
        c.ClearSelection();c.Mode=SelectionMode.Edge;
        c.PointerDown(c.Screen(new(-32,16)),MouseButton.Left);c.PointerMove(c.Screen(new(160,-16)));c.PointerUp(MouseButton.Left);
        check(c.SelectedEdges.Count==1&&c.IgnoreGroups,"Marquee selection ignores group expansion while the toggle is active");
        c.PointerDown(c.Screen(new(64,0)),MouseButton.Left);c.PointerMove(c.Screen(new(64,-32)));c.PointerUp(MouseButton.Left);
        check(c.IgnoreGroups&&c.SelectedEdges.Count==1&&w.Document.Groups.Count==1&&w.Document.Groups[0].EdgeIds.Count==8,"Dragging and normalization preserve both the group and the persistent individual-selection mode");
        Toggle().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!c.GroupsIgnored&&c.SelectedEdges.Count==8,"Clicking the button again immediately restores full-group selection");
        w.Undo();c.Mode=SelectionMode.Edge;Click(new(64,0));Click(new(64,0),2);check(c.IndividualSelection&&AutomationProperties.GetItemStatus(Toggle())=="On","Button reflects the temporary individual selection entered by double-click");
        Toggle().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!c.GroupsIgnored&&c.SelectedEdges.Count==8,"Button exits double-click individual selection and selects the complete group");
        w.ExecuteFeature("Ignorer les groupes");check(c.IgnoreGroups,"Configurable Ignore Groups command activates the toggle");
        c.ClearSelection();Click(new(64,0));w.UpdateLayout();Pump();Toggle().BringIntoView();Pump();MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-117-ignore-groups.png"));
        w.LoadDocument(Plan.Parse(original),"");check(!c.GroupsIgnored,"Opening a project starts with normal group selection");
        check(new ShortcutSettings().Bindings["Ignorer les groupes"]==ShortcutSettings.Unassigned,"Ignore Groups receives no automatic shortcut");
        check(L.Missing.Count==0,"Group-selection toggle and its state are localized");
    }
    private static IEnumerable<T> Desc<T>(DependencyObject p)where T:DependencyObject{if(p is T t)yield return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++)foreach(var c in Desc<T>(VisualTreeHelper.GetChild(p,i)))yield return c;}
    private static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
}

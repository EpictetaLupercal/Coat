// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Threading;
namespace Coat;
internal static class Update108Tests
{
    internal static void Run(MainWindow w, Action<bool,string> check, string output)
    {
        Plan Rectangle() { var p = new Plan(); p.Polygon(new V2[] { new(0,0), new(128,0), new(128,96), new(0,96) }); return p; }
        var p = Rectangle(); var face = p.Faces().Single(); p.Fills[face.Key] = "#66AAFF"; p.FillPatterns[face.Key] = FillPattern.Dots; p.Gradients[face.Key] = new("#FFCC00",45);
        var bottom = p.Edges.First(); bottom.ArrowStart = bottom.ArrowEnd = true;
        p.Point(new(64,0)); p.Normalize(); int midpoint = p.Vertices.Single(v=>v.P==new V2(64,0)).Id;
        p.GroupSelection(p.Vertices.Select(v=>v.Id), p.Edges.Select(e=>e.Id));
        check(p.DissolveVertices(new[]{midpoint}) == 1 && p.Vertices.Count == 4 && p.Edges.Count == 4, "Dissolve vertex reconnects a split boundary instead of opening it");
        check(p.Fills.Values.Single()=="#66AAFF" && p.FillPatterns.Values.Single()==FillPattern.Dots && p.Gradients.Values.Single().Angle==45, "Vertex dissolve preserves face color, pattern and gradient");
        check(p.Edges.Count(e=>e.ArrowStart)==1 && p.Edges.Count(e=>e.ArrowEnd)==1, "Dissolve preserves arrows at the surviving endpoints");
        check(p.Groups.Count==1 && p.Groups[0].EdgeIds.All(id=>p.Edges.Any(e=>e.Id==id)), "Dissolve remaps group membership");
        var corner = p.Vertices.First().Id; check(p.DissolveVertices(new[]{corner})==1 && p.Faces().Single().Vertices.Count==3, "Dissolving a corner produces a closed triangular face");
        check(p.DissolveVertices(p.Vertices.Select(v=>v.Id).ToArray())==0 && p.Faces().Count==1, "Dissolve refuses to collapse a triangle");
        p=Rectangle(); p.Segment(new(64,0),new(64,96)); var shared=p.Edges.Single(e=>p.Get(e.A).X==64 && p.Get(e.B).X==64);
        var halves=p.Faces(); foreach(var f in halves) p.Fills[f.Key]="#CCEE88";
        check(p.DissolveEdges(new[]{shared.Id})==1 && p.Faces().Count==1 && Math.Abs(p.Faces()[0].Area-12288)<.001, "Dissolve edge merges adjacent faces while preserving the outer contour");
        check(p.Fills.Values.Single()=="#CCEE88", "Merged face retains its fill");
        string unchanged=p.Serialize(); check(p.DissolveEdges(p.Edges.Select(e=>e.Id).ToArray())==0 && p.Serialize()==unchanged, "Boundary edges are retained instead of being deleted");
        p=new Plan(); p.Segment(new(-64,0),new(64,0)); p.Segment(new(0,0),new(0,64)); int junction=p.Vertices.Single(v=>v.P==new V2(0,0)).Id;
        check(p.DissolveVertices(new[]{junction})==0 && p.Edges.Count==3, "Dissolve preserves branching junctions");
        p=Rectangle(); p.Point(new(64,0)); p.Normalize(); midpoint=p.Vertices.Single(v=>v.P==new V2(64,0)).Id;
        w.LoadDocument(p, ""); w.SetTool(EditorTool.Select); var c=w.Canvas; c.Mode=SelectionMode.Vertex; c.SelectedVertices.Add(midpoint); w.UpdateInspector();
        var menu=c.BuildSelectionMenu(new(64,0));
        check(menu.Background==Brushes.White && menu.Items.OfType<MenuItem>().First().IsEnabled, "Context menu provides enabled dissolve actions with a classic white background");
        menu.Items.OfType<MenuItem>().First().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(w.Document.Vertices.Count==4,"Context menu executes vertex dissolve"); w.Undo(); check(w.Document.Vertices.Count==5,"Dissolve is undoable in one step");
        w.LoadDocument(Rectangle(), ""); w.SetTool(EditorTool.Select); c.Mode=SelectionMode.Edge; c.SelectAll(); c.Transform=TransformKind.Translation; w.UpdateInspector(); w.UpdateLayout(); Pump();
        var dx=Desc<TextBox>(w).Single(t=>AutomationProperties.GetName(t)=="Offset X"); var dy=Desc<TextBox>(w).Single(t=>AutomationProperties.GetName(t)=="Offset Y");
        check(Math.Abs(dx.TranslatePoint(new Point(),w).Y-dy.TranslatePoint(new Point(),w).Y)<1 && dx.TranslatePoint(new Point(),w).X<dy.TranslatePoint(new Point(),w).X, "Move offset X and Y share one compact row");
        check(!Desc<CheckBox>(w).Any(b=>Equals(b.Content,"Arrow Start")), "Select no longer contains arrow options");
        MainWindow.SaveVisual((FrameworkElement)w.Content, Path.Combine(output,"coat-108-select.png"));
        c.SetTransform(TransformKind.Scale); c.Zoom=2; c.Center=new(64,48); w.UpdateLayout(); Pump();
        check(c.ScaleHandles().Count==8,"Scale gizmo exposes side and corner handles");
        var h=c.ScaleHandles().Single(h=>h.X==1 && h.Y==0); c.PointerDown(h.Position,MouseButton.Left); c.PointerMove(new Point(h.Position.X+128*c.Zoom,h.Position.Y)); c.PointerUp(MouseButton.Left);
        check(w.Document.Vertices.Min(v=>v.X)==0 && w.Document.Vertices.Max(v=>v.X)==256 && w.Document.Vertices.Max(v=>v.Y)==96, "X handle scales only X and anchors the opposite side"); w.Undo(); c.SelectAll();
        h=c.ScaleHandles().Single(h=>h.X==0 && h.Y==1); c.PointerDown(h.Position,MouseButton.Left); c.PointerMove(new Point(h.Position.X,h.Position.Y-96*c.Zoom)); c.PointerUp(MouseButton.Left);
        check(w.Document.Vertices.Max(v=>v.X)==128 && w.Document.Vertices.Max(v=>v.Y)==192, "Y handle scales only Y"); w.Undo(); c.SelectAll();
        h=c.ScaleHandles().Single(h=>h.X==1 && h.Y==1); c.PointerDown(h.Position,MouseButton.Left); c.PointerMove(new Point(h.Position.X+128*c.Zoom,h.Position.Y-48*c.Zoom)); c.PointerUp(MouseButton.Left);
        check(w.Document.Vertices.Max(v=>v.X)==256 && w.Document.Vertices.Max(v=>v.Y)==144,"Corner handle scales X and Y independently"); w.Undo(); c.SelectAll();
        h=c.ScaleHandles().Single(h=>h.X==1 && h.Y==1); c.PointerDown(h.Position,MouseButton.Left); c.PointerMove(new Point(h.Position.X+128*c.Zoom,h.Position.Y-48*c.Zoom),shift:true); c.PointerUp(MouseButton.Left);
        check(w.Document.Vertices.Max(v=>v.X)==256 && w.Document.Vertices.Max(v=>v.Y)==192,"Shift corner drag preserves proportions"); w.Undo(); c.SelectAll();
        h=c.ScaleHandles().Single(h=>h.X==1 && h.Y==0); c.PointerDown(h.Position,MouseButton.Left); c.PointerMove(new Point(h.Position.X+100,h.Position.Y)); c.CancelGesture();
        check(w.Document.Vertices.Max(v=>v.X)==128,"Cancelling a gizmo drag restores the geometry");
        c.SelectAll(); c.Zoom=.05; h=c.ScaleHandles().Single(h=>h.X==1 && h.Y==0);
        c.PointerDown(h.Position,MouseButton.Left); c.PointerMove(new Point(h.Position.X+22,h.Position.Y)); c.PointerUp(MouseButton.Left);
        check(Math.Abs(w.Document.Vertices.Max(v=>v.X)-192)<.001,"Tiny selections use the visible handle extent for predictable scaling"); w.Undo(); c.Zoom=2;
        c.SelectAll(); c.SetTransform(TransformKind.Scale); w.ExecuteFeature("Échelle uniforme"); w.UpdateLayout(); Pump();
        var sx=Desc<TextBox>(w).Single(t=>AutomationProperties.GetName(t)=="Factor X"); var sy=Desc<TextBox>(w).Single(t=>AutomationProperties.GetName(t)=="Factor Y");
        check(Math.Abs(sx.TranslatePoint(new Point(),w).Y-sy.TranslatePoint(new Point(),w).Y)<1,"Independent scale factors share one compact row");
        MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-108-scale.png"));
        w.SetTool(EditorTool.Draw); w.UpdateLayout(); Pump();
        check(Desc<CheckBox>(w).Any(b=>Equals(b.Content,"Arrow Start")) && Desc<ComboBox>(w).Any(b=>AutomationProperties.GetName(b)=="LINE STYLE"),"Draw contains arrow controls and the visual line-style dropdown");
        w.ExecuteFeature("Double flèche"); check(w.Document.Edges.All(e=>e.ArrowStart && e.ArrowEnd),"Draw arrow settings also edit previously selected edges");
        c.ClearSelection(); w.UpdateInspector(); c.DrawAppearance.Style=LineStyle.LongDash; c.DrawAppearance.ArrowStyle=ArrowKind.Open; c.DrawAppearance.ArrowSize=20;
        c.PointerDown(c.Screen(new(-128,-64)),MouseButton.Left); c.PointerUp(MouseButton.Left); c.PointerDown(c.Screen(new(128,-64)),MouseButton.Left); c.PointerUp(MouseButton.Left);
        var stroke=w.Document.Edges.Single(e=>w.Document.Get(e.A).Y==-64 && w.Document.Get(e.B).Y==-64);
        check(stroke.Style==LineStyle.LongDash && stroke.ArrowStart && stroke.ArrowEnd && stroke.ArrowStyle==ArrowKind.Open && stroke.ArrowSize==20,"New Draw segments inherit line style and arrow parameters");
        c.Anchor=null; c.Fit(); w.UpdateInspector(); w.UpdateLayout(); Pump();
        var styleChoice=Desc<ComboBox>(w).Single(b=>AutomationProperties.GetName(b)=="LINE STYLE");
        check(Desc<TextBlock>(styleChoice).Any(t=>t.Text=="Long dashes"),"Line dropdown displays its selected visual style when closed");
        MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-108-draw.png"));
        w.SetTool(EditorTool.Select); c.Mode=SelectionMode.Edge; c.SelectAll();
        var popup=c.BuildSelectionMenu(new()); popup.PlacementTarget=c; popup.IsOpen=true; w.UpdateLayout(); Pump();
        MainWindow.SaveVisual(popup,Path.Combine(output,"coat-108-context.png")); popup.IsOpen=false;
        check(new ShortcutSettings().Bindings["Dissoudre les sommets"]==ShortcutSettings.Unassigned,"New dissolve shortcuts remain unassigned");
        check(L.Missing.Count==0,"Selection update is fully localized");
    }
    private static IEnumerable<T> Desc<T>(DependencyObject p) where T:DependencyObject { if(p is T t) yield return t; for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++) foreach(var c in Desc<T>(VisualTreeHelper.GetChild(p,i))) yield return c; }
    private static void Pump() { var f=new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false)); Dispatcher.PushFrame(f); }
}

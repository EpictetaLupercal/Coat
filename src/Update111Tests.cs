// Coat — Epicteta Lupercal
using System.Windows.Input;
namespace Coat;
internal static class Update111Tests
{
    internal static void Run(MainWindow w, Action<bool,string> check)
    {
        var p=new Plan(); p.Polygon(new V2[]{new(0,0),new(256,0),new(256,256),new(0,256)});
        p.Polygon(new V2[]{new(384,0),new(640,0),new(640,256),new(384,256)});
        var group=p.GroupSelection(p.Vertices.Select(v=>v.Id),p.Edges.Select(e=>e.Id))!;
        var originalMembers=group.EdgeIds.Order().ToArray(); var originalVertices=group.VertexIds.Order().ToArray();
        w.LoadDocument(p,""); w.SetTool(EditorTool.Select); var c=w.Canvas; c.Zoom=2; c.Transform=TransformKind.Translation; c.Mode=SelectionMode.Edge;
        void Click(V2 point,int count=1,bool ctrl=false){c.PointerDown(c.Screen(point),MouseButton.Left,count,ctrl:ctrl);c.PointerUp(MouseButton.Left);}
        Click(new(128,0)); check(c.SelectedEdges.Count==8,"Single click still selects the complete group");
        Click(new(128,0),2); int edge=c.SelectedEdges.Single();
        check(c.IndividualSelection && c.SelectionPoints().Count==2 && c.SelectedFaces.Count==0,"Double-click isolates one edge inside the group");
        Click(new(128,0)); check(c.SelectedEdges.SetEquals(new[]{edge}),"A following single click retains precise edge selection");
        c.PointerDown(c.Screen(new(128,0)),MouseButton.Left); c.PointerMove(c.Screen(new(128,-32))); c.PointerUp(MouseButton.Left);
        check(c.SelectedEdges.Count==1 && c.IndividualSelection && w.Document.Vertices.Count(v=>v.Y==-32)==2 && w.Document.Vertices.Count(v=>v.X>=384 && v.Y==0)==2,"Dragging an isolated edge moves only its endpoints and keeps precise selection after normalization");
        check(w.Document.Groups.Count==1 && w.Document.Groups[0].EdgeIds.Order().SequenceEqual(originalMembers) && w.Document.Groups[0].VertexIds.Order().SequenceEqual(originalVertices),"Editing an isolated member preserves the original group and membership");
        w.MoveSelection(0,-32); check(c.SelectedEdges.Count==1 && w.Document.Vertices.Count(v=>v.Y==-64)==2,"Numeric move also operates on the isolated edge");
        w.Undo(); check(w.Document.Vertices.Count(v=>v.Y==-32)==2 && w.Document.Groups.Count==1,"Undo restores an individual edit while retaining the group");
        c.Mode=SelectionMode.Vertex; Click(new(0,-32)); Click(new(0,-32),2);
        check(c.SelectedVertices.Count==1 && c.SelectedEdges.Count==0 && c.SelectionPoints().Count==1,"Vertex double-click isolates one grouped vertex");
        Click(new(256,-32),ctrl:true); check(c.SelectedVertices.Count==2 && c.SelectedEdges.Count==0,"Ctrl-click adds individual vertices without expanding the group");
        Click(new(0,-32),ctrl:true); check(c.SelectedVertices.Count==1 && c.IndividualSelection,"Ctrl-click can deselect a precise member without restoring the group");
        w.Escape(); check(!c.IndividualSelection && !c.HasSelectionOrGesture,"Escape clears individual selection mode");
        c.Mode=SelectionMode.Face; Click(new(128,128)); check(c.SelectedEdges.Count==8,"Normal group selection resumes after deselection");
        Click(new(128,128),2); check(c.SelectedFaces.Count==1 && c.SelectionPoints().Count==4 && c.SelectedEdges.Count==0,"Face double-click isolates one shape within a multi-shape group");
        c.PointerDown(c.Screen(new(128,128)),MouseButton.Left); c.PointerMove(c.Screen(new(96,128))); c.PointerUp(MouseButton.Left);
        check(c.SelectedFaces.Count==1 && c.SelectionPoints().Count==4 && w.Document.Groups.Count==1 && w.Document.Vertices.Where(v=>v.X>=384).All(v=>v.X==384||v.X==640),"Isolated face can be dragged without moving the other grouped shape");
        Click(new(-256,-256)); check(!c.IndividualSelection && !c.HasSelectionOrGesture,"Clicking empty canvas exits individual selection mode");
        c.Mode=SelectionMode.Edge; Click(new(512,0)); check(c.SelectedEdges.Count==8 && !c.IndividualSelection,"Clearing selection restores full-group edge picking");
        check(L.Missing.Count==0,"Individual group selection help is localized");
    }
}

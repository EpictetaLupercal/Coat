// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
namespace Coat;
internal static class Update110Tests
{
    internal static void Run(MainWindow w, Action<bool,string> check, string output)
    {
        var fixtures = Path.Combine(output,"map-110"); Directory.CreateDirectory(fixtures);
        Plan Polygon(IEnumerable<V2> points) { var p = new Plan(); p.Polygon(points.ToArray()); p.Fills[p.Faces().Single().Key]="#FFFFFF"; return p; }
        V2[] Rect(double x,double y,double width,double height) => new V2[] {new(x,y),new(x+width,y),new(x+width,y+height),new(x,y+height)};
        IEnumerable<V2> Circle(int n) => Shapes.Contour(ShapeKind.Circle,new(),new(128,0),n,32);
        V2 Transform(V2 p) => SelectionTransform.Rotate(new V2(p.X*1.7,p.Y*.6),new(),37)+new V2(512.25,-384.5);
        var cases = new (string Name, IEnumerable<V2> Points, int Sides)[] {
            ("rectangle",Rect(0,0,256,128),4),
            ("rectangle-45",Rect(0,0,256,128).Select(p=>SelectionTransform.Rotate(p,new(),45)),4),
            ("circle-12",Circle(12),12), ("circle-24",Circle(24),24),
            ("ellipse",Circle(24).Select(Transform),24),
            ("convex-rotated",new V2[]{new(0,0),new(160,0),new(200,80),new(80,160),new(-32,64)}.Select(Transform),5)
        };
        foreach (var item in cases)
        {
            var p=Polygon(item.Points); string original=p.Serialize(); var b=MapExporter.Build(p); var map=MapExporter.Export(p);
            check(b.Count==1 && b[0].Outline.Count==item.Sides && map.Text.Split('\n').Count(l=>l.StartsWith("("))==item.Sides+2,item.Name+" exports one brush with N+2 planes");
            check(Math.Abs(MapExporter.Area(b[0].Outline)-p.Faces()[0].Area)<1e-5 && b[0].Outline.All(v=>p.Vertices.Any(p=>(p.P-v).Length<1e-7)),item.Name+" preserves the original transformed contour");
            Update12Tests.CheckPlanes(map.Text,check); check(p.Serialize()==original,item.Name+" export is read-only"); File.WriteAllText(Path.Combine(fixtures,item.Name+".map"),map.Text);
            // Extra disconnected shapes must not introduce cuts at unrelated vertex heights.
            p.Polygon(Rect(2048,13,64,37)); p.Fills[p.Faces().Single(f=>f.Vertices.Any(id=>p.Get(id).X==2048)).Key]="#FF0000";
            check(MapExporter.Build(p).Count==2,item.Name+" stays intact alongside unrelated geometry");
        }
        var concave=Polygon(new V2[]{new(0,0),new(192,0),new(192,64),new(96,64),new(96,160),new(0,160)}.Select(p=>SelectionTransform.Rotate(p,new(),31)));
        var parts=MapExporter.Build(concave); check(parts.Count<=3 && parts.All(b=>MapExporter.Convex(b.Outline)),"Rotated concave polygon merges into a small convex partition");
        check(Math.Abs(parts.Sum(b=>MapExporter.Area(b.Outline))-concave.Faces().Single().Area)<1e-5,"Concave partition preserves area without filling the recess");
        File.WriteAllText(Path.Combine(fixtures,"concave.map"),MapExporter.Export(concave).Text); Update12Tests.CheckPlanes(MapExporter.Export(concave).Text,check);
        var hole=Polygon(Rect(0,0,512,512)); string outer=hole.Faces().Single().Key;
        hole.Polygon(Rect(128,128,256,256)); string inner=hole.Faces().OrderBy(f=>f.Area).First().Key; hole.Fills.Remove(inner);
        parts=MapExporter.Build(hole); check(parts.Count==4 && parts.Sum(b=>MapExporter.Area(b.Outline))==196608 && parts.All(b=>!Plan.Inside(new(256,256),b.Outline)),"Hole exports four convex walls with an empty center");
        File.WriteAllText(Path.Combine(fixtures,"hole.map"),MapExporter.Export(hole).Text); Update12Tests.CheckPlanes(MapExporter.Export(hole).Text,check);
        w.LoadDocument(hole,""); w.SetTool(EditorTool.Paint); w.Canvas.ShowGrid=false; w.Canvas.Fit(); w.UpdateLayout(); Pump();
        check(w.Title.StartsWith(AppVersion.Title),"Title takes version from assembly project metadata");
        check(w.Canvas.FillContains(outer,new(64,64)) && !w.Canvas.FillContains(outer,new(256,256)),"Outer canvas fill excludes an enclosed unfilled shape");
        var svg=XDocument.Parse(MainWindow.Svg(hole)); check(svg.Descendants().Any(e=>e.Name.LocalName=="path" && (string?)e.Attribute("fill-rule")=="evenodd" && e.Attribute("d")!.Value.Split('M').Length==3),"SVG uses an even-odd compound clipping contour");
        w.ExportPngTo(Path.Combine(output,"coat-110-hole.png")); MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-110-fill.png"));
        hole.Fills[inner]="#FF0000"; hole.Polygon(Rect(192,192,128,128)); string nested=hole.Faces().OrderBy(f=>f.Area).First().Key; hole.Fills.Remove(nested); w.Canvas.RefreshGeometry();
        check(!w.Canvas.FillContains(outer,new(160,160)) && w.Canvas.FillContains(inner,new(160,160)) && !w.Canvas.FillContains(inner,new(256,256)),"Nested regions retain independent fills and holes");
        hole.EnsureLevel(1); hole.ActiveLevel=1; hole.Polygon(Rect(32,32,32,32)); w.Canvas.RefreshGeometry(); check(w.Canvas.FillContains(outer,new(48,48)),"Contours on a different level cannot cut holes in a fill");
        var bridge=Polygon(Rect(0,0,512,512)); bridge.Polygon(Rect(128,128,256,256)); bridge.Segment(new(0,256),new(128,256)); var ring=bridge.Faces().OrderByDescending(f=>f.Area).First(); bridge.Fills.Clear(); bridge.Fills[ring.Key]="#FFFFFF"; w.LoadDocument(bridge,"");
        check(w.Canvas.FillContains(ring.Key,new(64,64)) && !w.Canvas.FillContains(ring.Key,new(256,256)),"A bridged contour does not double-toggle its existing hole");
        check(MapExporter.Build(bridge).All(b=>!Plan.Inside(new(256,256),b.Outline)),"Bridge edges do not close an exported hole");
        var redundant=Polygon(new V2[]{new(0,0),new(64,0),new(128,0),new(128,64),new(0,64)});
        check(MapExporter.Build(redundant).Single().Outline.Count==4,"Export removes redundant collinear vertices");
        var near=Polygon(Circle(24).Select(p=>p+new V2(1000000.125,-1000000.25))); check(MapExporter.Build(near).Single().Outline.Count==24,"Translated circles retain precision away from the origin");
        w.LoadDocument(Polygon(Rect(0,0,512,512)),""); w.SetTool(EditorTool.Select); w.Canvas.Zoom=.05;
        for(int i=0;i<20;i++) w.ExecuteFeature("Zoom arrière"); check(w.Canvas.Zoom==.01,"Keyboard zoom reaches and clamps at one percent");
        w.Document.Zoom=.01; check(Plan.Parse(w.Document.Serialize()).Zoom==.01,"One-percent zoom survives project serialization");
        w.Canvas.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=Mouse.MouseWheelEvent}); check(w.Canvas.Zoom>.01 && w.Canvas.Zoom<.02,"Wheel zoom remains progressive from one percent");
        w.Canvas.Zoom=.01; w.Canvas.ShowGrid=true; w.UpdateLayout(); Pump(); MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-110-zoom.png"));
        var vertex=w.Document.Vertices[0]; w.Canvas.Mode=SelectionMode.Vertex; w.Canvas.PointerDown(w.Canvas.Screen(vertex.P),MouseButton.Left); w.Canvas.PointerUp(MouseButton.Left); check(w.Canvas.SelectedVertices.Contains(vertex.Id),"Vertex hit testing works at one-percent zoom");
        check(L.Missing.Count==0,"Version and export updates are fully localized");
    }
    private static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
}

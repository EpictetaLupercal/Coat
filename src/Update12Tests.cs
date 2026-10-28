// Coat — Epicteta Lupercal
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Coat;
internal static class Update12Tests
{
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        Plan Polygon(params V2[] points) { var p = new Plan(); p.Polygon(points); p.Fills[p.Faces().Single().Key] = "#FFFFFF"; return p; }
        Plan Rectangle() => Polygon(new(0, 0), new(256, 0), new(256, 32), new(0, 32));
        double Area(List<MapBrush> brushes) => brushes.Sum(b => MapExporter.Area(b.Outline));
        bool Rectangular(MapBrush b) => b.Outline.Count == 4 && Enumerable.Range(0, 4).All(i => { var d = b.Outline[(i + 1) % 4] - b.Outline[i]; return d.X == 0 || d.Y == 0; });
        void Invalid(Action action, string name) { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } check(rejected, name); }
        var p = Rectangle(); var brushes = MapExporter.Build(p);
        check(brushes.Count == 1 && Rectangular(brushes[0]) && Area(brushes) == 8192 && brushes[0].Bottom == 0 && brushes[0].Top == 192, "Default white wall exports as one rectangular brush from Z 0 to 192");
        var result = MapExporter.Export(p); string boxPath = Path.Combine(output, "coat-12-box.map"); File.WriteAllText(boxPath, result.Text);
        check(result.Text.Contains("\"classname\" \"worldspawn\"") && result.Text.Contains("// Format: Valve") && result.Text.Split('\n').Count(l => l.StartsWith("(")) == 6, "Valve 220 export has worldspawn and six planes for a rectangular wall");
        CheckPlanes(result.Text, check);
        p.EnsureLevel(2); p.LevelHeights[0] = -64; p.LevelHeights[2] = 256;
        brushes = MapExporter.Build(p); check(brushes.Single().Bottom == -64 && brushes.Single().Top == 256, "Wall top uses the next existing numbered level's custom elevation, including an empty upper level");
        p.ActiveLevel = 2; p.Polygon(new V2[] { new(0, 0), new(32, 0), new(32, 128), new(0, 128) }); p.Fills[p.Faces().Single(f => f.Level == 2).Key] = "#ffffff";
        brushes = MapExporter.Build(p); check(brushes.Count == 2 && brushes.Single(b => b.Level == 2).Top == 448, "Highest-level walls default to 192 units above their base; color matching ignores hex case");
        string state = p.Serialize(); MapExporter.Export(p); check(p.Serialize() == state, "Export does not change the document");
        check(Plan.Parse(state).Serialize() == state, "Level elevations and wall color round-trip in project format 6");
        var legacy = Plan.Parse("{\"Version\":5,\"Levels\":[-1,0,2],\"ActiveLevel\":0}");
        check(legacy.Version == 6 && legacy.LevelZ(-1) == -192 && legacy.LevelZ(2) == 384 && legacy.WallColor == "#FFFFFF", "Legacy plans receive default elevations and white wall color");
        var elevated = new Plan(); elevated.LevelHeights[0] = 500; elevated.EnsureLevel(1); elevated.EnsureLevel(-1);
        check(elevated.LevelZ(1) == 692 && elevated.LevelZ(-1) == 308, "New neighbouring levels inherit 192-unit spacing from a custom elevation");
        p.LevelHeights[2] = -64; Invalid(() => MapExporter.Export(p), "Equal wall base and top elevations are rejected before export");
        p.LevelHeights[2] = -100; Invalid(() => MapExporter.Export(p), "Inverted wall heights are rejected instead of producing invalid brushes");
        Invalid(() => Plan.Parse("{\"Version\":6,\"LevelHeights\":{\"99\":192}}"), "Reject elevations belonging to nonexistent levels");
        Invalid(() => Plan.Parse("{\"Version\":6,\"WallColor\":\"invalid\"}"), "Reject invalid saved wall colors");
        Invalid(() => Plan.Parse("{\"Version\":6,\"LevelHeights\":null}"), "Reject missing elevation dictionaries without a null-reference error");
        var custom = Rectangle(); custom.WallColor = "#00FF00"; check(MapExporter.Build(custom).Single().Surface == MapSurface.Floor, "Non-wall fills export as floors");
        custom.Fills[custom.Faces().Single().Key] = "#00ff00"; custom.FillPatterns[custom.Faces().Single().Key] = FillPattern.Dots;
        check(MapExporter.Build(custom).Count == 1, "Custom wall colors and patterned fills export solid brushes");
        custom.Fills.Clear(); foreach (var edge in custom.Edges) edge.Color = "#00FF00";
        Invalid(() => MapExporter.Export(custom), "Colored outlines alone never become walls");
        var l = Polygon(new(0, 0), new(128, 0), new(128, 32), new(32, 32), new(32, 128), new(0, 128));
        brushes = MapExporter.Build(l);
        check(brushes.Count == 2 && brushes.All(Rectangular) && Area(brushes) == 7168, "Concave orthogonal L decomposes into two rectangular brushes with exact area");
        var ring = Polygon(new(0, 0), new(256, 0), new(256, 256), new(0, 256));
        ring.Polygon(new V2[] { new(32, 32), new(224, 32), new(224, 224), new(32, 224) });
        ring.Fills.Remove(ring.Faces().OrderBy(f => f.Area).First().Key);
        brushes = MapExporter.Build(ring);
        check(brushes.Count == 4 && brushes.All(Rectangular) && Area(brushes) == 256*256-192*192 && brushes.All(b => !Plan.Inside(new(128,128), b.Outline)), "Enclosed unfilled room remains an opening inside a white outline");
        ring.Fills[ring.Faces().OrderBy(f => f.Area).First().Key] = "#FF0000";
        check(Area(MapExporter.Build(ring).Where(b => b.Surface == MapSurface.Wall).ToList()) == Area(brushes) && MapExporter.Build(ring).Single(b => b.Surface == MapSurface.Floor).Top == 0, "Nested non-wall fill creates a floor inside the surrounding walls");
        ring.Fills[ring.Faces().OrderBy(f => f.Area).First().Key] = "#FFFFFF";
        check(MapExporter.Build(ring).Count == 1 && Area(MapExporter.Build(ring)) == 256*256, "Adjacent white areas merge into a single rectangular wall without internal duplicate brushes");
        var diagonal = Polygon(new(-96, 0), new(0, -128), new(96, 0), new(0, 128));
        brushes = MapExporter.Build(diagonal); check(Math.Abs(Area(brushes) - 24576) < 1e-6 && brushes.All(b => b.Outline.Count >= 3), "Diagonal contour retains its exact footprint");
        var circle = Polygon(Enumerable.Range(0,32).Select(i => new V2(128*Math.Cos(i*Math.PI/16),128*Math.Sin(i*Math.PI/16))).ToArray());
        brushes = MapExporter.Build(circle); check(Math.Abs(Area(brushes) - circle.Faces().Single().Area) < 1e-4, "Polygonal circle keeps its original area without a rectangular approximation");
        CheckPlanes(MapExporter.Export(circle).Text, check);
        File.WriteAllText(Path.Combine(output,"coat-12-circle.map"),MapExporter.Export(circle).Text);
        // Sampling compares exported footprints against the editor's face ownership, away from boundaries.
        foreach (var shape in new[] { l, diagonal, circle })
        {
            brushes = MapExporter.Build(shape); bool same = true;
            for (double y = -140.37; y < 280; y += 7) for (double x = -140.13; x < 280; x += 7)
            { var point = new V2(x,y); var face = shape.HitFace(point); bool wall = face != null && shape.Fills.ContainsKey(face.Key); if (brushes.Any(b => Plan.Inside(point,b.Outline)) != wall) same = false; }
            check(same, "Export footprint agrees with face hit testing for " + (shape == l ? "concave walls" : shape == diagonal ? "diagonal walls" : "curved walls"));
        }
        var doorway = Polygon(new(0,0),new(96,0),new(96,32),new(0,32));
        doorway.Polygon(new V2[] {new(160,0),new(256,0),new(256,32),new(160,32)}); foreach(var f in doorway.Faces()) doorway.Fills[f.Key] = "#FFFFFF";
        check(MapExporter.Build(doorway).Count == 2 && MapExporter.Build(doorway).All(b => !Plan.Inside(new(128,16),b.Outline)), "A 64-unit doorway stays open with clearance for the 40-unit-wide player");
        // Complete editable fixture for independent map editor parsing/compiler checks.
        var example = l; example.LevelHeights[0] = -64; example.EnsureLevel(1); example.LevelHeights[1] = 192; example.ActiveLevel = 1;
        example.Polygon(new V2[] {new(256,0),new(352,128),new(448,0),new(448,192),new(256,192)});
        foreach (var f in example.Faces()) example.Fills[f.Key] = "#FFFFFF";
        result = MapExporter.Export(example); CheckPlanes(result.Text, check);
        File.WriteAllText(Path.Combine(output,"coat-12-walls.map"),result.Text); File.WriteAllText(Path.Combine(output,"Example-Map.coat"),example.Serialize());
        window.LoadDocument(Rectangle(), ""); window.SetTool(EditorTool.Paint); window.UpdateLayout(); Pump();
        var zbox = Desc<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Height"); zbox.Text = "-96";
        Desc<Button>(window).Single(b => b.Content as string == "Apply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.Document.LevelZ(0) == -96, "Level panel Apply Z changes the current level elevation");
        window.Undo(); check(window.Document.LevelZ(0) == 0, "Elevation change supports Undo"); window.Redo(); check(window.Document.LevelZ(0) == -96, "Elevation change supports Redo");
        window.Canvas.PaintColor = "#224466"; window.UpdateInspector(); window.UpdateLayout(); Pump();
        Desc<Button>(window).Single(b => b.Content as string == "Use Current Color for Walls").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.Document.WallColor == "#224466", "Color panel assigns the current paint color to exported walls"); window.Undo(); check(window.Document.WallColor == "#FFFFFF", "Changing the wall color supports Undo");
        window.SetLevelHeight(48); window.DeleteActiveLevel(); check(window.Document.LevelHeights.Count == 0 && window.Document.LevelZ(0) == 0, "Deleting a level removes its elevation and recreates the default level when needed"); window.Undo(); check(window.Document.LevelZ(0) == 48, "Undo restores a deleted level's elevation");
        window.SetLevelHeight(double.NaN); check(window.Document.LevelZ(0) == 48, "Nonfinite elevation input does not change the document");
        window.ChangeLanguage("fr"); window.UpdateLayout(); Pump();
        check(Desc<TextBox>(window).Any(t => AutomationProperties.GetName(t) == "Hauteur du niveau"), "Level elevation control is localized in French");
        window.ChangeLanguage("en"); window.UpdateLayout(); Pump();
        check(Desc<Menu>(window).Single().Items.OfType<MenuItem>().SelectMany(m => m.Items.OfType<MenuItem>()).Any(m => m.Header as string == "Export to .map…"), "File menu exposes the localized map export command");
        window.LoadDocument(example, ""); window.Canvas.Fit(); window.SetTool(EditorTool.Paint); window.Width = 1440; window.Height = 900; window.UpdateLayout(); Pump();
        MainWindow.SaveVisual((FrameworkElement)window.Content,Path.Combine(output,"coat-12-map-settings.png"));
        var sidebar = Desc<ScrollViewer>(window).Single(v => v.Content is StackPanel panel && panel.Children.OfType<TextBlock>().Any(t => t.Text == "MAP EXPORT"));
        sidebar.ScrollToEnd(); Pump(); MainWindow.SaveVisual((FrameworkElement)window.Content,Path.Combine(output,"coat-12-wall-color.png"));
        window.Width = 1050; window.Height = 680; window.UpdateLayout(); Pump(); MainWindow.SaveVisual((FrameworkElement)window.Content,Path.Combine(output,"coat-12-compact.png"));
        check(L.Missing.Count == 0, "Map export controls and validation messages have English translations: " + string.Join(", ",L.Missing));
    }
    private record struct V3(double X,double Y,double Z)
    {
        public static V3 operator -(V3 a,V3 b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static V3 Cross(V3 a,V3 b)=>new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        public static double Dot(V3 a,V3 b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    }
    internal static void CheckPlanes(string map, Action<bool,string> check)
    {
        var planes = new List<V3[]>(); bool valid = true; int count = 0;
        foreach (string line in map.Split('\n'))
        {
            if(line.StartsWith("(")) planes.Add(Regex.Matches(line,@"\( ([^)]*) \)").Select(m=>m.Groups[1].Value.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray()).Select(v=>new V3(v[0],v[1],v[2])).ToArray());
            if(line.Trim()=="}" && planes.Count>0)
            {
                count++; var points=planes.SelectMany(p=>p).ToArray(); var center=new V3(points.Average(p=>p.X),points.Average(p=>p.Y),points.Average(p=>p.Z));
                foreach(var p in planes) {var n=V3.Cross(p[1]-p[0],p[2]-p[0]); if(V3.Dot(n,n)<1e-12 || V3.Dot(n,center-p[0])<=0 || points.Any(q=>V3.Dot(n,q-p[0]) < -1e-3)) valid=false;}
                planes.Clear();
            }
        }
        check(valid && count>0,"Serialized map planes bound nondegenerate convex brushes with consistent winding");
    }
    private static IEnumerable<T> Desc<T>(DependencyObject parent) where T : DependencyObject { if(parent is T value)yield return value; for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) foreach(var child in Desc<T>(VisualTreeHelper.GetChild(parent,i)))yield return child; }
    private static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
}

// Coat — Epicteta Lupercal
using System.IO;
using System.Windows.Media;

namespace Coat;

internal static class Update120Tests
{
    internal static void Run(MainWindow window, Action<bool, string> check, string output)
    {
        var fixtures = Path.Combine(output, "map-120"); Directory.CreateDirectory(fixtures);
        const string orange = "#FFA500", blue = "#2288CC", white = "#FFFFFF", green = "#33AA66";
        void Polygon(Plan p, IEnumerable<V2> points, string? color)
        {
            var contour = points.ToArray(); p.Polygon(contour);
            var face = p.Faces().Single(f => f.Level == p.ActiveLevel &&
                f.Vertices.All(id => contour.Any(v => (p.Get(id).P - v).Length < 1e-6)));
            if (color == null) p.Fills.Remove(face.Key); else p.Fills[face.Key] = color;
        }
        void Rect(Plan p, double x, double y, double width, double height, string? color) =>
            Polygon(p, new V2[] { new(x,y), new(x+width,y), new(x+width,y+height), new(x,y+height) }, color);
        double Area(IEnumerable<MapBrush> brushes, string color) => brushes.Where(b => b.Color == color).Sum(b => MapExporter.Area(b.Outline));
        bool Near(double a, double b) => Math.Abs(a-b) < 1e-5;
        List<MapBrush> Verify(Plan p, string name)
        {
            string saved = p.Serialize(); var brushes = MapExporter.Build(p); var map = MapExporter.Export(p);
            check(saved == p.Serialize() && MapExporter.Export(Plan.Parse(saved)).Text == map.Text, name + ": export is read-only and existing project format round-trips");
            check(map.BrushCount == brushes.Count && brushes.All(b => MapExporter.Convex(b.Outline)) &&
                brushes.Sum(b => b.Outline.Count+2) == map.Text.Split('\n').Count(l => l.StartsWith("(")), name + ": convex brushes have exactly N+2 planes");
            Update12Tests.CheckPlanes(map.Text, check);
            var shapes = brushes.Select(b => ContourRegions.Geometry(new[] { b.Outline })).ToArray();
            bool overlap = false;
            for (int i = 0; i < brushes.Count; i++) for (int j = i+1; j < brushes.Count; j++)
            {
                if (Math.Min(brushes[i].Top, brushes[j].Top) <= Math.Max(brushes[i].Bottom, brushes[j].Bottom)) continue;
                if (Geometry.Combine(shapes[i], shapes[j], GeometryCombineMode.Intersect, null).GetArea() > 1e-5) overlap = true;
            }
            check(!overlap, name + ": no positive-volume overlap between exported solids");
            check(brushes.All(b => map.Text.Contains("- color " + b.Color)), name + ": normalized source colors remain in map brush comments");
            File.WriteAllText(Path.Combine(fixtures, name + ".map"), map.Text);
            File.WriteAllText(Path.Combine(fixtures, name + ".coat"), saved);
            return brushes;
        }

        var p = new Plan(); Rect(p,0,0,512,512,orange); Rect(p,128,128,256,256,"#ffa500");
        var b = Verify(p,"same-color-floors");
        check(b.Count == 1 && b[0].Outline.Count == 4 && Near(Area(b,orange),512*512), "Nested equal RGB colors merge into one six-plane floor, ignoring hex case");
        foreach (var key in p.Fills.Keys.ToArray()) p.Fills[key] = white;
        b = Verify(p,"same-color-walls");
        check(b.Count == 1 && b[0].Surface == MapSurface.Wall && b[0].Bottom == 0 && b[0].Top == 192, "Nested white walls still merge into one rectangular wall");

        p = new Plan(); Rect(p,0,0,512,512,orange); Rect(p,128,128,256,256,blue);
        b = Verify(p,"different-color-floors");
        check(b.Count == 5 && Near(Area(b,orange),196608) && Near(Area(b,blue),65536) &&
            b.Single(v => v.Color == blue).Outline.Count == 4, "Nested different-color floors remain a four-rectangle ring and an independent rectangular center");
        check(b.All(v => v.Surface == MapSurface.Floor && v.Bottom == -16 && v.Top == 0), "Color separation keeps floor thickness and elevation unchanged");

        p = new Plan(); Rect(p,0,0,512,384,orange);
        var pillars = new[] { new V2(64,64), new V2(224,64), new V2(384,224) };
        foreach (var at in pillars) Rect(p,at.X,at.Y,64,64,white);
        p.LevelHeights[0] = 48; p.EnsureLevel(1); p.LevelHeights[1] = 304;
        b = Verify(p,"orange-floor-white-pillars");
        check(b.Count(v => v.Surface == MapSurface.Wall) == 3 && pillars.All(at =>
            b.Count(v => v.Surface == MapSurface.Wall && v.Outline.Count == 4 &&
                Plan.Inside(at+new V2(32,32),v.Outline) && Near(MapExporter.Area(v.Outline),4096)) == 1), "Every white pillar remains one independent six-plane brush at its original position and size");
        check(Near(Area(b,orange),512*384-3*64*64) && Near(Area(b,white),3*64*64) &&
            b.Where(v => v.Surface == MapSurface.Floor).All(v => v.Outline.Count == 4 && v.Bottom == 32 && v.Top == 48) &&
            b.Where(v => v.Surface == MapSurface.Wall).All(v => v.Bottom == 48 && v.Top == 304), "Orange floor is partitioned around pillars; custom base, next-level top and rectangular brushes are preserved");

        p = new Plan(); Rect(p,0,0,512,512,orange);
        Polygon(p,new V2[] { new(96,96),new(288,96),new(288,160),new(160,160),new(160,288),new(96,288) },blue);
        b = Verify(p,"concave-inner-floor");
        check(Near(Area(b,blue),20480) && Near(Area(b,orange),512*512-20480) && b.Count(v => v.Color == blue) == 2,
            "Scanline color transitions preserve a concave inner floor as two rectangles without absorbing its color");

        p = new Plan(); Rect(p,0,0,256,128,orange); Rect(p,32,32,32,32,null);
        Rect(p,0,128,256,128,blue); Rect(p,32,160,32,32,null);
        // Later drawing may inherit the outer fill into an earlier empty face.
        // Finalize both explicit holes after construction, as a user can do with Erase Fill.
        foreach (var face in p.Faces().Where(f => f.Area == 1024)) p.Fills.Remove(face.Key);
        b = Verify(p,"adjacent-colors-with-holes");
        check(Near(Area(b,orange),31744) && Near(Area(b,blue),31744) &&
            b.Where(v => v.Color == orange).All(v => v.Outline.All(at => at.Y <= 128)) &&
            b.Where(v => v.Color == blue).All(v => v.Outline.All(at => at.Y >= 128)), "Vertical rectangle coalescing and convex merging both stop at different colors");

        p = new Plan(); Rect(p,0,0,512,512,orange); Rect(p,64,64,384,384,blue);
        Rect(p,128,128,256,256,white); Rect(p,192,192,128,128,green); Rect(p,224,224,64,64,null);
        b = Verify(p,"multiple-nested-colors");
        check(Near(Area(b,orange),512*512-384*384) && Near(Area(b,blue),384*384-256*256) &&
            Near(Area(b,white),256*256-128*128) && Near(Area(b,green),128*128-64*64) &&
            b.All(v => !Plan.Inside(new(256,256),v.Outline)), "Four nested colors keep their own areas and the innermost empty shape remains a hole");

        p = new Plan();
        Polygon(p,new V2[] { new(-256,-256),new(256,-256),new(256,256),new(-256,256) }.Select(at => SelectionTransform.Rotate(at,new(),37)),orange);
        Polygon(p,Shapes.Contour(ShapeKind.Circle,new(),new(64,0),12,32).Select(at => SelectionTransform.Rotate(new V2(at.X*1.5,at.Y*.75),new(),23)),blue);
        var inner = p.Faces().OrderBy(f => f.Area).First();
        b = Verify(p,"rotated-floor-ellipse");
        check(b.Single(v => v.Color == blue).Outline.Count == 12 && Near(Area(b,blue),inner.Area) && Near(Area(b,orange),512*512-inner.Area),
            "A nested transformed ellipse stays one 14-plane brush and retains its distinct floor color");

        p = new Plan(); Rect(p,0,0,512,512,orange); Rect(p,128,128,256,256,white); p.WallColor = "#ffa500";
        p.LevelHeights[0] = -96.5;
        b = Verify(p,"custom-wall-color");
        check(b.Where(v => v.Color == orange).All(v => v.Surface == MapSurface.Wall && v.Bottom == -96.5 && v.Top == 95.5) &&
            b.Single(v => v.Color == white).Surface == MapSurface.Floor && b.Single(v => v.Color == white).Top == -96.5,
            "Custom wall classification and fractional negative heights are preserved for different nested colors");

        p = new Plan(); Rect(p,0,0,128,128,orange); Rect(p,32,32,64,64,blue);
        p.EnsureLevel(1); p.ActiveLevel = 1; Rect(p,0,0,128,128,orange); Rect(p,32,32,64,64,white);
        b = Verify(p,"separate-levels");
        check(b.Select(v => v.Level).Distinct().Count() == 2 &&
            Near(b.Where(v => v.Level == 0).Sum(v => MapExporter.Area(v.Outline)),128*128) &&
            Near(b.Where(v => v.Level == 1).Sum(v => MapExporter.Area(v.Outline)),128*128), "Nested color ownership and merging remain isolated per level");
        check(AppVersion.Number == "1.2", "Release name is 1.2 from project version metadata");
        window.UpdateInspector();
        check(window.Title.StartsWith("Coat — v1.2 —"), "Application title displays Coat — v1.2 with the document name");
    }
}

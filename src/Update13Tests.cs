// Coat — Epicteta Lupercal
using System.IO;

namespace Coat;
internal static class Update13Tests
{
    internal static void Run(Action<bool, string> check, string output)
    {
        void Rect(Plan p, double x, double y, double w, double h, string? color)
        {
            p.Polygon(new V2[] {new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)});
            var f = p.HitFace(new(x+w/2,y+h/2))!;
            if (color == null) p.Fills.Remove(f.Key); else p.Fills[f.Key] = color;
        }
        double Area(IEnumerable<MapBrush> brushes) => brushes.Sum(b => MapExporter.Area(b.Outline));
        var p = new Plan(); Rect(p,0,0,256,256,"#778899");
        var brushes = MapExporter.Build(p);
        check(brushes.Count == 1 && brushes[0].Surface == MapSurface.Floor && brushes[0].Bottom == -16 && brushes[0].Top == 0 && Area(brushes) == 65536, "A colored rectangle exports as one floor slab with its top at level Z");
        p.LevelHeights[0] = -96.5; brushes = MapExporter.Build(p);
        check(brushes.Single().Bottom == -112.5 && brushes.Single().Top == -96.5, "Floor elevations preserve negative and fractional Z coordinates");
        Rect(p,64,64,128,128,null); brushes = MapExporter.Build(p);
        check(brushes.Count == 4 && brushes.All(b => b.Surface == MapSurface.Floor && b.Outline.Count == 4) && Area(brushes) == 49152 && brushes.All(b => !Plan.Inside(new(128,128),b.Outline)), "An unfilled nested shape cuts a hole in a floor and produces no brush");
        Rect(p,300,0,128,128,null);
        check(MapExporter.Build(p).All(b => !Plan.Inside(new(350,64),b.Outline)), "A separate unfilled shape creates neither a floor nor a wall");
        p = new Plan(); Rect(p,0,0,32,256,"#FFFFFF"); Rect(p,32,0,128,256,"#445566"); Rect(p,160,0,32,256,"#FFFFFF");
        brushes = MapExporter.Build(p);
        check(brushes.Count(b => b.Surface == MapSurface.Wall) == 2 && brushes.Count(b => b.Surface == MapSurface.Floor) == 1 && Area(brushes.Where(b => b.Surface == MapSurface.Floor)) == 32768, "Adjacent wall-floor-wall cells are split by surface type rather than merged");
        check(brushes.Where(b => b.Surface == MapSurface.Wall).All(b => b.Bottom == 0 && b.Top == 192) && brushes.Single(b => b.Surface == MapSurface.Floor).Top == 0, "Wall heights are unchanged and meet the floor's top surface");
        var serial = p.Serialize(); var result = MapExporter.Export(p); Update12Tests.CheckPlanes(result.Text,check);
        check(serial == p.Serialize() && result.BrushCount == 3, "Mixed export is read-only and counts both wall and floor brushes");
        File.WriteAllText(Path.Combine(output,"coat-13-walls-floors.map"),result.Text);
        File.WriteAllText(Path.Combine(output,"Example-Map.map"),result.Text);
        File.WriteAllText(Path.Combine(output,"Example-Map.coat"),p.Serialize());
        p = new Plan(); Rect(p,0,0,64,64,"#112233"); Rect(p,64,0,64,64,"#667788");
        check(MapExporter.Build(p).Count == 1 && MapExporter.Build(p).Single().Surface == MapSurface.Floor && Area(MapExporter.Build(p)) == 8192, "Adjacent differently colored floors merge into a rectangular slab");
        p.WallColor = "#112233"; brushes = MapExporter.Build(p);
        check(brushes.Count == 2 && brushes.Count(b => b.Surface == MapSurface.Wall) == 1 && brushes.Count(b => b.Surface == MapSurface.Floor) == 1, "Custom wall color determines classification while all other fills remain floors");
        p = new Plan(); Rect(p,0,0,256,256,"#445566"); Rect(p,64,64,128,128,"#FFFFFF");
        brushes = MapExporter.Build(p);
        check(brushes.Count == 5 && Area(brushes.Where(b => b.Surface == MapSurface.Floor)) == 49152 && Area(brushes.Where(b => b.Surface == MapSurface.Wall)) == 16384, "A wall nested inside a floor owns its footprint without overlapping floor brushes");
        p = new Plan(); p.ActiveLevel = -1; Rect(p,0,0,64,64,"#112233"); p.ActiveLevel = 2; Rect(p,0,0,64,64,"#334455");
        brushes = MapExporter.Build(p);
        check(brushes.Count == 2 && brushes.Single(b => b.Level == -1).Top == -192 && brushes.Single(b => b.Level == 2).Top == 384, "Floors are exported on every level at their own Z");
        p.LevelHeights[2] = -300;
        check(MapExporter.Build(p).Count == 2, "Floor-only exports do not require a valid wall height to an upper level");
        p = new Plan(); p.Polygon(new V2[] {new(0,0),new(128,0),new(128,32),new(32,32),new(32,128),new(0,128)});
        p.Fills[p.Faces().Single().Key] = "#445566"; p.FillPatterns[p.Faces().Single().Key] = FillPattern.Stripes;
        brushes = MapExporter.Build(p);
        check(brushes.Count == 2 && brushes.All(b => b.Surface == MapSurface.Floor && b.Outline.Count == 4) && Area(brushes) == 7168, "Patterned concave floor exports as rectangles with its exact footprint");
        p = new Plan(); p.Polygon(Enumerable.Range(0,32).Select(i => new V2(128*Math.Cos(i*Math.PI/16),128*Math.Sin(i*Math.PI/16))).ToArray()); p.Fills[p.Faces().Single().Key] = "#334455";
        brushes = MapExporter.Build(p); check(brushes.All(b => b.Surface == MapSurface.Floor) && Math.Abs(Area(brushes)-p.Faces().Single().Area)<1e-4, "Curved floors preserve their polygonal footprint"); Update12Tests.CheckPlanes(MapExporter.Export(p).Text,check);
        p.Fills.Clear(); bool rejected = false; try {MapExporter.Export(p);} catch(InvalidDataException){rejected=true;}
        check(rejected,"An entirely unfilled plan exports no objects and reports no volumes");
        check(L.Missing.Count==0,"Floor export messages have English translations");
    }
}

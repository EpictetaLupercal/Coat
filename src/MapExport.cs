// Coat — Epicteta Lupercal
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Coat;

public partial class Plan
{
    public Dictionary<int, double> LevelHeights { get; set; } = new();
    public string WallColor { get; set; } = "#FFFFFF";
    public double LevelZ(int level) => LevelHeights.GetValueOrDefault(level, level * 192.0);
    public double WallTop(int level)
    {
        var next = Levels.Where(l => l > level).Order().Cast<int?>().FirstOrDefault();
        return next.HasValue ? LevelZ(next.Value) : LevelZ(level) + 192;
    }
    internal void ValidateMapSettings()
    {
        if (LevelHeights == null || LevelHeights.Any(p => !Levels.Contains(p.Key) || !double.IsFinite(p.Value) || Math.Abs(p.Value) > 1e8) ||
            WallColor == null || !ColorMath.TryHex(WallColor, out _))
            throw new InvalidDataException(L.T("Hauteurs Z ou couleur des murs invalides."));
    }
}

public enum MapSurface { Empty, Wall, Floor }
public record MapBrush(int Level, double Bottom, double Top, List<V2> Outline, MapSurface Surface = MapSurface.Wall);
public sealed record MapExportResult(string Text, int BrushCount);
public static class MapExporter
{
    public const double FloorThickness = 16;
    private record Region(Face Face, List<V2> Points, MapSurface Surface);
    private record Crossing(double X, int Region, V2 A, V2 B)
    {
        public double At(double y) => A.X + (B.X - A.X) * ((y - A.Y) / (B.Y - A.Y));
    }
    public static List<MapBrush> Build(Plan plan)
    {
        plan.ValidateMapSettings();
        var vertices = plan.Vertices.ToDictionary(v => v.Id, v => v.P);
        var faces = plan.Faces(); var brushes = new List<MapBrush>();
        var wallColor = ColorMath.Hex(ParseColor(plan.WallColor));
        foreach (var level in plan.Levels.Order())
        {
            var regions = faces.Where(f => f.Level == level).Select(f => new Region(f, f.Vertices.Select(id => vertices[id]).ToList(),
                !plan.Fills.TryGetValue(f.Key, out var color) ? MapSurface.Empty :
                ColorMath.TryHex(color, out var rgb) && ColorMath.Hex(rgb) == wallColor ? MapSurface.Wall : MapSurface.Floor)).ToList();
            if (!regions.Any(r => r.Surface != MapSurface.Empty)) continue;
            double bottom = plan.LevelZ(level), top = plan.WallTop(level);
            if (regions.Any(r => r.Surface == MapSurface.Wall) && (!double.IsFinite(top) || top <= bottom + 1e-6))
                throw new InvalidDataException(L.F($"Niveau {level} : le haut du mur ({top:0.###}) doit être supérieur à sa base ({bottom:0.###}). Corrigez les hauteurs Z des niveaux."));
            // Horizontal slabs yield rectangles for orthogonal contours and convex trapezoids for diagonals.
            // Even/odd crossings also handle repeated bridge edges, concavity and enclosed openings.
            var ys = regions.SelectMany(r => r.Points.Select(p => p.Y)).Distinct().Order().ToArray();
            var previousRectangles = new Dictionary<(double, double, MapSurface), MapBrush>();
            for (int row = 0; row + 1 < ys.Length; row++)
            {
                double y0 = ys[row], y1 = ys[row + 1], mid = (y0 + y1) / 2;
                if (y1 - y0 < 1e-8) continue;
                var crossings = new List<Crossing>();
                for (int r = 0; r < regions.Count; r++)
                {
                    var points = regions[r].Points;
                    for (int i = 0; i < points.Count; i++)
                    {
                        var a = points[i]; var b = points[(i + 1) % points.Count];
                        if ((a.Y > mid) == (b.Y > mid)) continue;
                        crossings.Add(new(a.X + (b.X - a.X) * ((mid - a.Y) / (b.Y - a.Y)), r, a, b));
                    }
                }
                crossings.Sort((a, b) => a.X.CompareTo(b.X));
                var inside = new HashSet<int>(); Crossing? start = null; var current = MapSurface.Empty;
                var nextRectangles = new Dictionary<(double, double, MapSurface), MapBrush>();
                for (int i = 0; i < crossings.Count;)
                {
                    var boundary = crossings[i]; int j = i;
                    do { if (!inside.Add(crossings[j].Region)) inside.Remove(crossings[j].Region); j++; }
                    while (j < crossings.Count && Math.Abs(crossings[j].X - boundary.X) < 1e-8);
                    // The innermost face owns the cell: empty faces cut holes; color chooses wall or floor.
                    int owner = inside.OrderBy(r => regions[r].Face.Area).ThenBy(r => r).DefaultIfEmpty(-1).First();
                    var next = owner >= 0 && j < crossings.Count ? regions[owner].Surface : MapSurface.Empty;
                    if (next != current && start != null)
                    {
                        var outline = Clean(new() { new(start.At(y0), y0), new(boundary.At(y0), y0), new(boundary.At(y1), y1), new(start.At(y1), y1) });
                        if (outline.Count >= 3 && Area(outline) > 1e-8)
                        {
                            var brush = current == MapSurface.Wall ? new MapBrush(level, bottom, top, outline, current) :
                                new MapBrush(level, bottom - FloorThickness, bottom, outline, current);
                            bool rectangle = outline.Count == 4 && outline[0].X == outline[3].X && outline[1].X == outline[2].X;
                            if (rectangle)
                            {
                                var key = (outline[0].X, outline[1].X, current);
                                if (previousRectangles.TryGetValue(key, out var previous) && previous.Outline[2].Y == y0)
                                { previous.Outline[2] = outline[2]; previous.Outline[3] = outline[3]; brush = previous; }
                                else brushes.Add(brush);
                                nextRectangles[key] = brush;
                            }
                            else brushes.Add(brush);
                        }
                        start = null;
                    }
                    if (next != MapSurface.Empty && start == null) start = boundary;
                    current = next;
                    i = j;
                }
                previousRectangles = nextRectangles;
            }
        }
        if (brushes.Count == 0) throw new InvalidDataException(L.T("Aucun volume à exporter. Remplissez une zone fermée : couleur des murs pour un mur, autre couleur pour un sol. Les zones sans remplissage ne créent aucun objet."));
        return brushes;
    }
    private static System.Windows.Media.Color ParseColor(string value)
    { ColorMath.TryHex(value, out var color); return color; }
    internal static double Area(IReadOnlyList<V2> points)
    { double area = 0; for (int i = 0; i < points.Count; i++) area += V2.Cross(points[i], points[(i + 1) % points.Count]); return area / 2; }
    private static List<V2> Clean(List<V2> points)
    {
        for (int i = points.Count - 1; i >= 0 && points.Count > 1; i--)
            if ((points[i] - points[(i + 1) % points.Count]).Length < 1e-8) points.RemoveAt(i);
        return points;
    }
    public static MapExportResult Export(Plan plan)
    {
        var brushes = Build(plan);
        var text = new StringBuilder("// Game: Quake\n// Format: Valve\n// Coat — Epicteta Lupercal\n{\n\"classname\" \"worldspawn\"\n\"mapversion\" \"220\"\n");
        string F(double n) => (Math.Abs(n) < 5e-10 ? 0 : n).ToString("0.#########", CultureInfo.InvariantCulture);
        string P(V2 p, double z) => $"( {F(p.X)} {F(p.Y)} {F(z)} )";
        void Plane(string a, string b, string c, string u, string v) => text.AppendLine($"{a} {b} {c} __TB_empty [ {u} 0 ] [ {v} 0 ] 0 1 1");
        foreach (var brush in brushes)
        {
            var p = brush.Outline; double z = brush.Bottom, top = brush.Top;
            text.AppendLine($"// Coat {brush.Surface.ToString().ToLowerInvariant()} - level {brush.Level}: Z {F(z)} to {F(top)}").AppendLine("{");
            // .map point order faces inward (the format uses the opposite cross-product convention).
            Plane(P(p[0], z), P(p[1], z), P(p[2], z), "1 0 0", "0 -1 0");
            Plane(P(p[2], top), P(p[1], top), P(p[0], top), "1 0 0", "0 -1 0");
            for (int i = 0; i < p.Count; i++)
            {
                var a = p[i]; var b = p[(i + 1) % p.Count]; var d = b - a;
                Plane(P(a, z), P(a, top), P(b, z), $"{F(d.X / d.Length)} {F(d.Y / d.Length)} 0", "0 0 -1");
            }
            text.AppendLine("}");
        }
        text.AppendLine("}"); return new(text.ToString(), brushes.Count);
    }
}

public partial class MainWindow
{
    private void LevelHeightControls()
    {
        var row = new DockPanel { Margin = new Thickness(0, 7, 0, 0) };
        var label = new TextBlock { Text = L.T("Hauteur"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), FontSize = 11 };
        DockPanel.SetDock(label, Dock.Left); row.Children.Add(label);
        var input = new TextBox { Text = Document.LevelZ(Document.ActiveLevel).ToString("0.#########", CultureInfo.InvariantCulture), MinWidth = 55 };
        AutomationProperties.SetName(input, L.T("Hauteur du niveau"));
        var apply = Button(L.T("Appliquer la hauteur"), () =>
        {
            if (double.TryParse(input.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) SetLevelHeight(z);
            else { Notify(L.T("Saisissez une coordonnée numérique valide.")); input.Focus(); input.SelectAll(); }
        });
        apply.FontSize = 11; apply.Padding = new Thickness(5); apply.Margin = new Thickness(5, 0, 0, 0); DockPanel.SetDock(apply, Dock.Right); row.Children.Add(apply); row.Children.Add(input); levelsPanel.Children.Add(row);
        levelsPanel.Children.Add(new TextBlock { Text = L.F($"Haut des murs : {Document.WallTop(Document.ActiveLevel):0.###} u"), FontSize = 10, Foreground = B("#96A7BC"), Margin = new Thickness(0, 5, 0, 0) });
    }
    public void SetLevelHeight(double z)
    {
        if (!double.IsFinite(z) || Math.Abs(z) > 1e8) { Notify(L.T("La hauteur Z doit être un nombre compris entre -100000000 et 100000000.")); return; }
        Canvas.CancelGesture(); Mutate(() => Document.LevelHeights[Document.ActiveLevel] = z); Canvas.Focus();
    }
    public void SetWallColor(string color)
    {
        if (!ColorMath.TryHex(color, out var rgb)) { Notify(L.T("Couleur invalide : utilisez le format #RRGGBB.")); return; }
        Canvas.CancelGesture(); Mutate(() => Document.WallColor = ColorMath.Hex(rgb));
    }
    private void WallColorControls()
    {
        Divider(); Heading(L.T("EXPORT .MAP"));
        var color = Button(L.T("Couleur des murs…"), () => ChooseColor(Document.WallColor, SetWallColor));
        color.ToolTip = Document.WallColor; inspector.Children.Add(color);
        var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 7) };
        line.Children.Add(new Border { Width = 18, Height = 18, Background = B(Document.WallColor), BorderBrush = B("#96A7BC"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0) });
        line.Children.Add(new TextBlock { Text = Document.WallColor }); inspector.Children.Add(line);
        inspector.Children.Add(Button(L.T("Définir la couleur actuelle comme mur"), () => SetWallColor(Canvas.PaintColor)));
        ToolHint(L.T("Seules les zones remplies avec cette couleur deviennent des murs à l’export .map. Base : Z du niveau. Haut : Z du niveau suivant, ou +192 unités au dernier niveau."));
        ToolHint(L.T("Autres couleurs : sols de 16 unités d’épaisseur, surface au Z du niveau. Sans remplissage : aucun objet exporté."));
    }
    private void ExportMap()
    {
        Canvas.CancelGesture();
        try
        {
            var result = MapExporter.Export(Document);
            var dialog = new SaveFileDialog { Filter = L.T("Carte .map (*.map)|*.map"), DefaultExt = ".map", FileName = string.IsNullOrEmpty(file) ? "Plan.map" : Path.GetFileNameWithoutExtension(file) + ".map" };
            if (dialog.ShowDialog(this) != true) return;
            WriteAtomic(dialog.FileName, result.Text); Notify(L.F($"Carte .map exportée : {result.BrushCount} brushes de murs et de sols."));
        }
        catch (Exception ex) { Error(L.T("Impossible d’exporter la carte .map"), ex); }
    }
}

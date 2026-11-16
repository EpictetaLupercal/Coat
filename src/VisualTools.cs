// Coat — Epicteta Lupercal
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Coat;
public enum ArrowKind { Triangle, Open, Diamond }
public record GradientFill(string EndColor, double Angle);
public partial class Plan
{
    public Dictionary<string, GradientFill> Gradients { get; set; } = new();
}
public static class ArrowGeometry
{
    public static List<V2> Points(V2 tip, V2 other, double size, ArrowKind kind)
    {
        var d = other - tip; if (d.Length < 1e-8) return new(); d *= Math.Min(size, d.Length * .45) / d.Length;
        var n = new V2(-d.Y, d.X) * .45;
        return kind == ArrowKind.Diamond ? new() { tip, tip + d * .5 + n, tip + d, tip + d * .5 - n } : new() { tip + d + n, tip, tip + d - n };
    }
    public static string Svg(Plan plan, Edge edge)
    {
        string result = "";
        foreach (bool start in new[] { true, false })
        {
            if (!(start ? edge.ArrowStart : edge.ArrowEnd)) continue;
            var points = Points(plan.Get(start ? edge.A : edge.B).P, plan.Get(start ? edge.B : edge.A).P, edge.ArrowSize, edge.ArrowStyle);
            string coords = string.Join(" ", points.Select(p => p.X.ToString("G17", CultureInfo.InvariantCulture) + "," + (-p.Y).ToString("G17", CultureInfo.InvariantCulture)));
            string color = System.Security.SecurityElement.Escape(edge.Color) ?? "#CCD5DF";
            result += $"<{(edge.ArrowStyle == ArrowKind.Open ? "polyline" : "polygon")} points=\"{coords}\" fill=\"{(edge.ArrowStyle == ArrowKind.Open ? "none" : color)}\" stroke=\"{color}\" stroke-width=\"2\"/>";
        }
        return result;
    }
}
public partial class EditorCanvas
{
    public bool PlaceTokens { get; set; }
    public bool SnapAnnotations { get; set; } = true;
    public bool GradientMode { get; set; }
    public string GradientEnd { get; set; } = "#FFFFFF";
    public double GradientAngle { get; set; }
    internal V2 SnapAnnotation(V2 p)
    {
        if (!SnapAnnotations) return p;
        var vertices = Plan.Vertices.Where(v => v.Level == Plan.ActiveLevel).ToDictionary(v => v.Id, v => v.P);
        var candidates = vertices.Values.Concat(Plan.Edges.Where(e => vertices.ContainsKey(e.A) && vertices.ContainsKey(e.B)).Select(e => (vertices[e.A] + vertices[e.B]) * .5));
        var nearest = candidates.Where(v => (v - p).Length * Zoom <= 10).OrderBy(v => (v - p).Length).Cast<V2?>().FirstOrDefault();
        return nearest ?? p.Snap(Plan.Grid);
    }
    internal void PaintFace(string key)
    {
        Plan.Fills[key] = PaintColor; Plan.FillPatterns[key] = PaintPattern;
        if (GradientMode) { Plan.Gradients[key] = new(GradientEnd, GradientAngle); Plan.FillPatterns[key] = FillPattern.Solid; }
        else Plan.Gradients.Remove(key);
    }
    private Brush FaceBrush(Face face, string color)
    {
        if (!Plan.Gradients.TryGetValue(face.Key, out var gradient)) return WorldFill(color, Plan.FillPatterns.GetValueOrDefault(face.Key));
        double a = gradient.Angle * Math.PI / 180;
        return new LinearGradientBrush((Color)ColorConverter.ConvertFromString(color), (Color)ColorConverter.ConvertFromString(gradient.EndColor), new Point(.5 - .5 * Math.Cos(a), .5 + .5 * Math.Sin(a)), new Point(.5 + .5 * Math.Cos(a), .5 - .5 * Math.Sin(a)));
    }
    private void DrawArrows(DrawingContext dc, IEnumerable<Edge> edges)
    {
        foreach (var edge in edges.Where(e => e.ArrowStart || e.ArrowEnd)) foreach (bool start in new[] { true, false })
        {
            if (!(start ? edge.ArrowStart : edge.ArrowEnd)) continue;
            var points = ArrowGeometry.Points(VertexAt(start ? edge.A : edge.B).P, VertexAt(start ? edge.B : edge.A).P, edge.ArrowSize, edge.ArrowStyle);
            if (points.Count == 0) continue;
            var path = new StreamGeometry(); using (var c = path.Open()) { c.BeginFigure(Screen(points[0]), edge.ArrowStyle != ArrowKind.Open, edge.ArrowStyle != ArrowKind.Open); c.PolyLineTo(points.Skip(1).Select(Screen).ToArray(), true, false); }
            dc.DrawGeometry(edge.ArrowStyle == ArrowKind.Open ? null : Brush(edge.Color), new Pen(Brush(edge.Color), 2), path);
        }
    }
}
public partial class MainWindow
{
    private void AnnotationOptions()
    {
        ModeButtons(new[] { L.T("Texte"), L.T("Jeton") }, (Document.Annotations.FirstOrDefault(a => a.Id == Canvas.SelectedAnnotation)?.IsToken ?? Canvas.PlaceTokens) ? 1 : 0, i => { Canvas.PlaceTokens = i == 1; Canvas.ClearSelection(); UpdateInspector(); });
        var snap = new CheckBox { Content = L.T("Accrochage : sommets, milieux et grille"), IsChecked = Canvas.SnapAnnotations };
        snap.Click += (_, _) => Canvas.SnapAnnotations = snap.IsChecked == true; inspector.Children.Add(snap);
        RegisterFeature(L.T("Accrochage : sommets, milieux et grille"), () => { Canvas.SnapAnnotations = !Canvas.SnapAnnotations; UpdateInspector(); });
    }
    private void GradientControls()
    {
        if (Canvas.PaintLines) return;
        ModeButtons(new[] { L.T("Remplissage"), L.T("Dégradé") }, Canvas.GradientMode ? 1 : 0, i => { Canvas.GradientMode = i == 1; UpdateInspector(); });
        if (!Canvas.GradientMode) return;
        inspector.Children.Add(Button(L.T("Couleur de fin…"), () => ChooseColor(Canvas.GradientEnd, value => { Canvas.GradientEnd = value; UpdateInspector(); })));
        var angle = Field("Angle (°)", F(Canvas.GradientAngle));
        inspector.Children.Add(ConfirmButton(L.T("Appliquer le dégradé"), () => { if (!Number(angle, out double value)) return; Canvas.GradientAngle = value % 360; if (Canvas.SelectedFaces.Count > 0) Mutate(() => { foreach (string key in Canvas.SelectedFaces) Canvas.PaintFace(key); }); }, angle));
    }
    private void ArrowControls(HashSet<int> ids)
    {
        var edges = Document.Edges.Where(e => ids.Contains(e.Id)).ToList();
        var display = edges.Count > 0 ? edges : new List<Edge> { Canvas.DrawAppearance };
        var targets = edges.Append(Canvas.DrawAppearance).ToList();
        void Change(Action action) { if (edges.Count > 0) Mutate(action); else { action(); UpdateInspector(); Canvas.InvalidateVisual(); } }
        var flags = new WrapPanel(); inspector.Children.Add(flags);
        void Flag(string label, Func<Edge, bool> get, Action<Edge, bool> set)
        {
            var box = new CheckBox { Content = label, IsThreeState = false, IsChecked = display.All(get) ? true : display.All(e => !get(e)) ? false : null, Margin = new Thickness(0, 4, 10, 8) };
            box.Click += (_, _) => Change(() => { foreach (var edge in targets) set(edge, box.IsChecked == true); }); flags.Children.Add(box);
            RegisterFeature(label, () => { bool value = !display.All(get); Change(() => { foreach (var edge in targets) set(edge, value); }); });
        }
        Flag(L.T("Flèche au début"), e => e.ArrowStart, (e, b) => e.ArrowStart = b);
        Flag(L.T("Flèche à la fin"), e => e.ArrowEnd, (e, b) => e.ArrowEnd = b);
        inspector.Children.Add(Button(L.T("Double flèche"), () => Change(() => { foreach (var e in targets) e.ArrowStart = e.ArrowEnd = true; })));
        ModeButtons(new[] { L.T("Triangle"), L.T("Ouverte"), L.T("Diamant") }, display.All(e => e.ArrowStyle == display[0].ArrowStyle) ? (int)display[0].ArrowStyle : -1, i => Change(() => { foreach (var e in targets) e.ArrowStyle = (ArrowKind)i; }));
        foreach (var (label, style) in new[] { ("Triangle", ArrowKind.Triangle), ("Ouverte", ArrowKind.Open), ("Diamant", ArrowKind.Diamond) })
            RegisterFeature(L.T(label), () => Change(() => { foreach (var edge in targets) edge.ArrowStyle = style; }));
        var size = Field(L.T("Taille des flèches"), F(display[0].ArrowSize)); size.Tag = new NumericRange(1, 1024, false);
        inspector.Children.Add(ConfirmButton(L.T("Appliquer les flèches"), () => { if (Number(size, out var n) && n >= 1 && n <= 1024) Change(() => { foreach (var e in targets) e.ArrowSize = n; }); }, size));
    }
    private Image PatternIcon(int pattern)
    {
        var g = new DrawingGroup(); using (var c = g.Open())
        {
            c.DrawRectangle(B("#78DCC1"), null, new Rect(0, 0, 32, 22));
            if (pattern == 1) for (int x = 3; x < 32; x += 7) c.DrawRectangle(B("#233D38"), null, new Rect(x, 0, 3, 22));
            if (pattern == 2) for (int x = 4; x < 32; x += 8) for (int y = 4; y < 22; y += 8) c.DrawEllipse(B("#233D38"), null, new Point(x, y), 1.8, 1.8);
        }
        g.Freeze(); return new Image { Source = new DrawingImage(g), Width = 32, Height = 22 };
    }
}

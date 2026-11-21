// Coat — Epicteta Lupercal
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Coat;
public enum ArrowKind { Triangle, Open, Diamond, Round }
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
    public static string Svg(Plan plan, Edge edge) => StrokeDecoration.Svg(plan.Get(edge.A).P, plan.Get(edge.B).P, edge);

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
        foreach (var edge in edges.Where(e => e.ArrowStart || e.ArrowEnd || e.Dimension))
            StrokeDecoration.Draw(dc, Screen(VertexAt(edge.A).P), Screen(VertexAt(edge.B).P), VertexAt(edge.A).P, VertexAt(edge.B).P, edge, Zoom, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

}
public partial class MainWindow
{
    private void AnnotationOptions()
    {
        var snap = new CheckBox { Content = L.T("Accrochage : sommets, milieux et grille"), IsChecked = Canvas.SnapAnnotations };
        snap.Click += (_, _) => Canvas.SnapAnnotations = snap.IsChecked == true; inspector.Children.Add(snap);
        RegisterFeature(L.T("Accrochage : sommets, milieux et grille"), () => { Canvas.SnapAnnotations = !Canvas.SnapAnnotations; UpdateInspector(); });
    }
    private void GradientControls()
    {
        if (Canvas.PaintLines) return;
        ModeButtons(new[] { L.T("Remplissage"), L.T("Dégradé") }, Canvas.GradientMode ? 1 : 0, i => { Canvas.GradientMode = i == 1; UpdateInspector(); });
        if (!Canvas.GradientMode) return;
        inspector.Children.Add(ColorButton(() => Canvas.GradientEnd, value => { Canvas.GradientEnd = value; foreach (string key in Canvas.SelectedFaces) if (Document.Gradients.TryGetValue(key,out var gradient)) Document.Gradients[key] = gradient with { EndColor = value }; }, "Couleur de fin…"));
        var angle = Field("Angle (°)", F(Canvas.GradientAngle));
        inspector.Children.Add(ConfirmButton(L.T("Appliquer le dégradé"), () => { if (!Number(angle, out double value)) return; Canvas.GradientAngle = value % 360; if (Canvas.SelectedFaces.Count > 0) Mutate(() => { foreach (string key in Canvas.SelectedFaces) Canvas.PaintFace(key); }); }, angle));
    }
    private void ArrowControls(HashSet<int> ids)
    {
        var edges = Document.Edges.Where(e => ids.Contains(e.Id)).ToList();
        var display = edges.Count > 0 ? edges : new List<Edge> { Canvas.DrawAppearance };
        var targets = edges.Append(Canvas.DrawAppearance).ToList();
        void Change(Action action) { if (edges.Count > 0) Mutate(action); else { action(); UpdateInspector(); Canvas.InvalidateVisual(); } }
        foreach (bool start in new[] { true, false })
        {
            string label = L.T(start ? "Flèche au début" : "Flèche à la fin");
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) }); row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new TextBlock { Text = L.T(start ? "Début" : "Fin"), VerticalAlignment = VerticalAlignment.Center, Foreground = B("#A7B6C9") });
            var combo = new ComboBox { Height = 34, ItemTemplate = StrokeChoiceTemplate(), IsEnabled = !display.All(e => e.Dimension) };
            var kinds = new ArrowKind?[] { null, ArrowKind.Triangle, ArrowKind.Diamond, ArrowKind.Round, ArrowKind.Open };
            string[] names = { L.T("Aucune"), L.T("Triangle"), L.T("Diamant"), L.T("Ronde"), L.T("Ouverte") };
            for (int i = 0; i < kinds.Length; i++) combo.Items.Add(new LineChoice(names[i], StrokeDecoration.Icon(kinds[i], start)));
            ArrowKind? Value(Edge e) => e.Dimension ? ArrowKind.Triangle : (start ? e.ArrowStart : e.ArrowEnd) ? e.HeadStyle(start) : null;
            combo.SelectedIndex = display.All(e => Value(e) == Value(display[0])) ? Array.IndexOf(kinds, Value(display[0])) : -1;
            System.Windows.Automation.AutomationProperties.SetName(combo, label); combo.ToolTip = label;
            combo.SelectionChanged += (_, _) => { if (combo.SelectedIndex < 0) return; var kind = kinds[combo.SelectedIndex]; Change(() => { foreach (var edge in targets) { if (start) { edge.ArrowStart = kind.HasValue; edge.StartArrowStyle = kind; } else { edge.ArrowEnd = kind.HasValue; edge.EndArrowStyle = kind; } } }); };
            Grid.SetColumn(combo, 1); row.Children.Add(combo); inspector.Children.Add(row);
            RegisterFeature(label, () => Change(() => { bool value = !display.All(e => start ? e.ArrowStart : e.ArrowEnd); foreach (var edge in targets) { if (start) edge.ArrowStart = value; else edge.ArrowEnd = value; } }));
        }
        RegisterFeature(L.T("Double flèche"), () => Change(() => { foreach (var edge in targets) edge.ArrowStart = edge.ArrowEnd = true; }));
        foreach (var (label, style) in new[] { ("Triangle", ArrowKind.Triangle), ("Ouverte", ArrowKind.Open), ("Diamant", ArrowKind.Diamond), ("Ronde", ArrowKind.Round) })
            RegisterFeature(L.T(label), () => Change(() => { foreach (var edge in targets) { edge.ArrowStyle = style; edge.StartArrowStyle = edge.EndArrowStyle = null; } }));
        var size = Field(L.T("Taille des flèches"), F(display[0].ArrowSize)); size.Tag = new NumericRange(1, 1024, false);
        void Apply() { if (Number(size, out var n) && n >= 1 && n <= 1024 && targets.Any(e => e.ArrowSize != n)) Change(() => { foreach (var e in targets) e.ArrowSize = n; }); }
        confirmations[size] = Apply; size.LostKeyboardFocus += (_, _) => { if (NumericExpression.TryEvaluate(size.Text, out var n) && n >= 1 && n <= 1024) Apply(); };
        RegisterFeature(L.T("Appliquer les flèches"), Apply);
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

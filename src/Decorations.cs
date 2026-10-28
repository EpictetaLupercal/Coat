// Coat — Epicteta Lupercal
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Coat;

public enum LineStyle { Solid, LongDash, ShortDash }
public enum FillPattern { Solid, Stripes, Dots }
public class Annotation
{
    public int Id { get; set; }
    public int Level { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Size { get; set; } = 18;
    public string Text { get; set; } = "Annotation";
    public string Color { get; set; } = "#FFFFFF";
    public FormattedText Layout(double dpi = 1) => new(Text, L.Culture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), Size, EditorCanvas.Brush(Color), dpi);
}
public partial class Plan
{
    public List<Annotation> Annotations { get; set; } = new();
    public Dictionary<string, FillPattern> FillPatterns { get; set; } = new();
    public IEnumerable<V2> AnnotationBounds(bool showLowerLevels = true) => Annotations.Where(a => a.Level == ActiveLevel || (showLowerLevels && a.Level < ActiveLevel)).SelectMany(a => { var t = a.Layout(); return new[] { new V2(a.X, a.Y), new V2(a.X + t.WidthIncludingTrailingWhitespace, a.Y - t.Height) }; });
    private void ValidateDecorations()
    {
        if (Annotations == null || FillPatterns == null || Annotations.Count > 5000 || Annotations.Any(a => a == null || a.Text == null || a.Text.Length > 4000 || !double.IsFinite(a.X) || !double.IsFinite(a.Y) || Math.Abs(a.X) > 1e8 || Math.Abs(a.Y) > 1e8 || !double.IsFinite(a.Size) || a.Size < 4 || a.Size > 256 || a.Level < -999 || a.Level > 999) || Annotations.Select(a => a.Id).Distinct().Count() != Annotations.Count || Edges.Any(e => !Enum.IsDefined(e.Style)) || FillPatterns.Values.Any(v => !Enum.IsDefined(v))) throw new InvalidDataException(L.T("Annotations ou styles invalides."));
        foreach (var a in Annotations) EnsureLevel(a.Level);
    }
}
public partial class EditorCanvas
{
    public int? SelectedAnnotation { get; set; }
    public FillPattern PaintPattern { get; set; }
    public LineStyle PaintLineStyle { get; set; }
    private V2 annotationOrigin;
    public static DashStyle EdgeDash(LineStyle style) => style == LineStyle.LongDash ? new DashStyle(new double[] { 8, 4 }, 0) : style == LineStyle.ShortDash ? new DashStyle(new double[] { 2, 3 }, 0) : DashStyles.Solid;
    private bool AnnotationDown(V2 point)
    {
        var hit = Plan.Annotations.LastOrDefault(a => a.Level == Plan.ActiveLevel && new Rect(a.X, -a.Y, Math.Max(a.Layout().Width, 12), a.Layout().Height).Contains(new Point(point.X, -point.Y)));
        if (Tool == EditorTool.Erase && hit != null) { Host.Mutate(() => Plan.Annotations.Remove(hit)); return true; }
        if (Tool != EditorTool.Annotation && (Tool != EditorTool.Select || hit == null)) return false;
        ClearSelection();
        if (hit == null)
        {
            var p = point.Snap(Plan.Grid);
            Host.Mutate(() => { hit = new Annotation { Id = Plan.NextId++, X = p.X, Y = p.Y, Level = Plan.ActiveLevel }; Plan.Annotations.Add(hit); });
        }
        SelectedAnnotation = hit!.Id; Transform = TransformKind.Translation; annotationOrigin = new(hit.X, hit.Y); before = Plan.Serialize(); dragging = true; CaptureMouse(); Host.UpdateInspector(); InvalidateVisual(); return true;
    }
    public Brush DecorationBrush(string color, FillPattern pattern)
    {
        if (pattern == FillPattern.Solid) return Brush(color);
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawRectangle(Brush(color), null, new Rect(0, 0, 12, 12));
            var dark = new SolidColorBrush(Color.FromArgb(85, 0, 0, 0));
            if (pattern == FillPattern.Stripes) dc.DrawRectangle(dark, null, new Rect(0, 0, 4, 12));
            else dc.DrawEllipse(dark, null, new Point(6, 6), 1.5, 1.5);
        }
        var origin = Screen(new V2());
        return new DrawingBrush(group) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(origin.X, origin.Y, 12 * Zoom, 12 * Zoom), ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 12, 12), Stretch = Stretch.Fill };
    }
    private void DrawAnnotations(DrawingContext dc, int level)
    {
        foreach (var note in Plan.Annotations.Where(a => a.Level == level))
        {
            var p = Screen(new(note.X, note.Y)); var text = note.Layout(VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.PushTransform(new TranslateTransform(p.X, p.Y)); dc.PushTransform(new ScaleTransform(Zoom, Zoom));
            dc.DrawText(text, new Point());
            if (!Exporting && SelectedAnnotation == note.Id) dc.DrawRectangle(null, new Pen(Accent, 1 / Zoom), new Rect(-3, -3, text.Width + 6, text.Height + 6));
            dc.Pop(); dc.Pop();
        }
    }
}
public partial class MainWindow
{
    private void StyleInspector()
    {
        var edges = Canvas.SelectionEdges();
        if (edges.Count > 0)
        {
            Heading(L.T("STYLE DES LIGNES")); var styles = Document.Edges.Where(e => edges.Contains(e.Id)).Select(e => e.Style).Distinct().ToList();
            ModeButtons(new[] { L.T("Continu"), L.T("Long"), L.T("Court") }, styles.Count == 1 ? (int)styles[0] : -1, i => Mutate(() => { foreach (var e in Document.Edges.Where(e => edges.Contains(e.Id))) e.Style = (LineStyle)i; }));
        }
        if (Canvas.SelectedFaces.Count > 0)
        {
            Heading(L.T("MOTIF DU REMPLISSAGE")); var patterns = Canvas.SelectedFaces.Select(f => Document.FillPatterns.GetValueOrDefault(f)).Distinct().ToList();
            ModeButtons(new[] { L.T("Uni"), L.T("Bandes"), L.Dots }, patterns.Count == 1 ? (int)patterns[0] : -1, i => Mutate(() => { foreach (var key in Canvas.SelectedFaces) { Document.Fills.TryAdd(key, Canvas.PaintColor); Document.FillPatterns[key] = (FillPattern)i; } }));
        }
    }
    private void PaintStyleInspector()
    {
        if (Canvas.PaintLines) ModeButtons(new[] { L.T("Continu"), L.T("Long"), L.T("Court") }, (int)Canvas.PaintLineStyle, i => { Canvas.PaintLineStyle = (LineStyle)i; UpdateInspector(); });
        else ModeButtons(new[] { L.T("Uni"), L.T("Bandes"), L.Dots }, (int)Canvas.PaintPattern, i => { Canvas.PaintPattern = (FillPattern)i; UpdateInspector(); });
    }
    private void AnnotationInspector()
    {
        ToolHint(L.T("Cliquez dans le plan pour placer un texte. Cliquez sur un texte pour le modifier, glissez pour le déplacer. Les annotations suivent leur niveau et sont incluses dans les exports."));
        var note = Document.Annotations.FirstOrDefault(a => a.Id == Canvas.SelectedAnnotation);
        if (note == null) return;
        var text = new TextBox { Text = note.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 90, MaxLength = 4000, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 10) }; inspector.Children.Add(text);
        var size = Field(L.T("Taille (u)"), F(note.Size)); var color = Field(L.T("Couleur"), note.Color); var x = Field("X", F(note.X)); var y = Field("Y", F(note.Y));
        var wheel = Button(L.T("◉  Roue de couleurs…"), () => ChooseColor(color.Text, value => color.Text = value)); wheel.Margin = new Thickness(0, 0, 0, 10); inspector.Children.Add(wheel);
        inspector.Children.Add(Button(L.T("Appliquer le texte"), () =>
        {
            if (string.IsNullOrWhiteSpace(text.Text)) { Notify(L.T("Saisissez un texte pour l’annotation.")); return; }
            if (!Number(size, out var n) || !Number(x, out var nx) || !Number(y, out var ny)) return;
            if (n < 4 || n > 256) { Notify(L.T("La taille doit être comprise entre 4 et 256 unités.")); return; }
            Color c; try { c = (Color)ColorConverter.ConvertFromString(color.Text.Trim()); } catch { Notify(L.T("Couleur invalide. Exemple : #FFFFFF")); return; }
            Mutate(() => { note.Text = text.Text; note.Size = n; note.X = nx; note.Y = ny; note.Color = $"#{c.R:X2}{c.G:X2}{c.B:X2}"; }); Canvas.Focus();
        }));
        var remove = Button(L.T("Supprimer le texte"), Canvas.DeleteSelection); remove.Margin = new Thickness(0, 8, 0, 0); inspector.Children.Add(remove);
    }
}
public static class DecorationSvg
{
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string E(string value) => System.Security.SecurityElement.Escape(value) ?? "";
    public static string Dash(LineStyle style) => style == LineStyle.LongDash ? " stroke-dasharray=\"16 8\"" : style == LineStyle.ShortDash ? " stroke-dasharray=\"4 6\"" : "";
    public static string PatternDefinitions() => "<defs><pattern id=\"coat-stripes\" patternUnits=\"userSpaceOnUse\" width=\"12\" height=\"12\"><rect width=\"4\" height=\"12\" fill=\"black\" fill-opacity=\"0.333333\"/></pattern><pattern id=\"coat-dots\" patternUnits=\"userSpaceOnUse\" width=\"12\" height=\"12\"><circle cx=\"6\" cy=\"6\" r=\"1.5\" fill=\"black\" fill-opacity=\"0.333333\"/></pattern></defs>";
    public static string Polygon(string points, string color, double opacity, FillPattern pattern)
    {
        var polygon = $"<polygon points=\"{points}\" fill=\"{E(color)}\" fill-opacity=\"{F(opacity)}\"/>";
        if (pattern == FillPattern.Solid) return polygon;
        string id = pattern == FillPattern.Stripes ? "coat-stripes" : "coat-dots";
        return $"<g opacity=\"{F(opacity)}\"><polygon points=\"{points}\" fill=\"{E(color)}\"/><polygon points=\"{points}\" fill=\"url(#{id})\"/></g>";
    }
    public static string Text(Annotation note)
    {
        var s = new StringBuilder($"<text font-family=\"Segoe UI, sans-serif\" font-size=\"{F(note.Size)}\" fill=\"{E(note.Color)}\" xml:space=\"preserve\">");
        var lines = note.Text.Replace("\r", "").Split('\n'); double lineHeight = note.Layout().Height / lines.Length;
        for (int i = 0; i < lines.Length; i++) s.Append($"<tspan x=\"{F(note.X)}\" y=\"{F(-note.Y + note.Layout().Baseline + i * lineHeight)}\">{E(lines[i])}</tspan>");
        return s.Append("</text>").ToString();
    }
}

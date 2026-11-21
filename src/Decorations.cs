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
    public bool IsToken { get; set; }
    public TokenKind TokenShape { get; set; }
    public string Text { get; set; } = "Annotation";
    public string Color { get; set; } = "#FFFFFF";
    public FormattedText Layout(double dpi = 1) => new(Text, L.Culture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), Size, EditorCanvas.Brush(Color), dpi);
}
public partial class Plan
{
    public List<Annotation> Annotations { get; set; } = new();
    public Dictionary<string, FillPattern> FillPatterns { get; set; } = new();
    public IEnumerable<V2> AnnotationBounds(bool showLowerLevels = true) => Annotations.Where(a => a.Level == ActiveLevel || (showLowerLevels && a.Level < ActiveLevel)).SelectMany(a => { if (a.IsToken) return new[] { new V2(a.X - a.Size / 2, a.Y + a.Size / 2), new V2(a.X + a.Size / 2, a.Y - a.Size / 2) }; var t = a.Layout(); return new[] { new V2(a.X, a.Y), new V2(a.X + t.WidthIncludingTrailingWhitespace, a.Y - t.Height) }; });
    private void ValidateDecorations()
    {
        if (Annotations == null || FillPatterns == null || Annotations.Count > 5000 || Annotations.Any(a => a == null || a.Text == null || a.Text.Length > 4000 || !Enum.IsDefined(a.TokenShape) || !double.IsFinite(a.X) || !double.IsFinite(a.Y) || Math.Abs(a.X) > 1e8 || Math.Abs(a.Y) > 1e8 || !double.IsFinite(a.Size) || a.Size < 4 || a.Size > 256 || a.Level < -999 || a.Level > 999) || Annotations.Select(a => a.Id).Distinct().Count() != Annotations.Count || Edges.Any(e => !Enum.IsDefined(e.Style)) || FillPatterns.Values.Any(v => !Enum.IsDefined(v))) throw new InvalidDataException(L.T("Annotations ou styles invalides."));
        if (Gradients == null || Gradients.Values.Any(g => g == null || !ColorMath.TryHex(g.EndColor, out _) || !double.IsFinite(g.Angle)) || Edges.Any(e => !Enum.IsDefined(e.ArrowStyle) || (e.StartArrowStyle.HasValue && !Enum.IsDefined(e.StartArrowStyle.Value)) || (e.EndArrowStyle.HasValue && !Enum.IsDefined(e.EndArrowStyle.Value)) || !double.IsFinite(e.ArrowSize) || e.ArrowSize < 1 || e.ArrowSize > 1024 || !double.IsFinite(e.DimensionFontSize) || e.DimensionFontSize < 4 || e.DimensionFontSize > 256)) throw new InvalidDataException(L.T("Annotations ou styles invalides."));
        foreach (var a in Annotations) EnsureLevel(a.Level);
    }
}
public partial class EditorCanvas
{
    public HashSet<int> SelectedAnnotations { get; } = new();
    // Single-item inspectors are only shown for a selection containing exactly one note.
    public int? SelectedAnnotation { get => SelectedAnnotations.Count == 1 && SelectedVertices.Count + SelectedEdges.Count + SelectedFaces.Count == 0 ? SelectedAnnotations.First() : null; set { SelectedAnnotations.Clear(); if(value.HasValue){SelectedVertices.Clear();SelectedEdges.Clear();SelectedFaces.Clear();SelectedAnnotations.Add(value.Value);} } }
    public FillPattern PaintPattern { get; set; }
    public LineStyle PaintLineStyle { get; set; }
    public static DashStyle EdgeDash(LineStyle style) => style == LineStyle.LongDash ? new DashStyle(new double[] { 8, 4 }, 0) : style == LineStyle.ShortDash ? new DashStyle(new double[] { 2, 3 }, 0) : DashStyles.Solid;
    private bool AnnotationDown(V2 point, bool ctrl)
    {
        var hit = Plan.Annotations.LastOrDefault(a => (Tool == EditorTool.Select ? CanSelectAnnotation(a) : a.Level == Plan.ActiveLevel) && (a.IsToken ? new Rect(a.X - a.Size / 2, -a.Y - a.Size / 2, a.Size, a.Size) : new Rect(a.X, -a.Y, Math.Max(a.Layout().Width, 12), a.Layout().Height)).Contains(new Point(point.X, -point.Y)));
        if (Tool == EditorTool.Erase && hit != null) { Host.Mutate(() => Plan.Annotations.Remove(hit)); return true; }
        if (Tool is EditorTool.Annotation or EditorTool.Token && hit != null && hit.IsToken != (Tool == EditorTool.Token)) hit = null;
        if (Tool is not (EditorTool.Annotation or EditorTool.Token) && (Tool != EditorTool.Select || hit == null)) return false;
        if (Tool == EditorTool.Select && ctrl && hit != null)
        {
            if(!SelectedAnnotations.Add(hit.Id))SelectedAnnotations.Remove(hit.Id);
            Host.UpdateInspector();InvalidateVisual();return true;
        }
        if(Tool != EditorTool.Select || hit == null || !SelectedAnnotations.Contains(hit.Id))ClearSelection();
        if (hit == null)
        {
            var p = SnapAnnotation(point);
            Host.Mutate(() => { hit = new Annotation { Id = Plan.NextId++, X = p.X, Y = p.Y, Level = Plan.ActiveLevel, IsToken = Tool == EditorTool.Token, TokenShape = TokenShape, Color = PaintColor, Size = Tool == EditorTool.Token ? TokenSize : 18 }; Plan.Annotations.Add(hit); });
            if (hit!.IsToken) { Host.UpdateInspector(); InvalidateVisual(); return true; }
        }
        SelectedAnnotations.Add(hit!.Id); Transform = TransformKind.Translation; before = Plan.Serialize(); BeginTransformDrag(); CaptureMouse(); Host.UpdateInspector(); InvalidateVisual(); return true;
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
            if (note.IsToken) TokenShapes.Draw(dc, note, Zoom); else dc.DrawText(text, new Point());
            if (!Exporting && SelectedAnnotations.Contains(note.Id)) dc.DrawRectangle(null, new Pen(Accent, 1 / Zoom), (note.IsToken ? new Rect(-note.Size / 2 - 3, -note.Size / 2 - 3, note.Size + 6, note.Size + 6) : new Rect(-3, -3, text.Width + 6, text.Height + 6)));
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
            ModeButtons(new[] { L.T("Uni"), L.T("Bandes"), L.Dots }, patterns.Count == 1 ? (int)patterns[0] : -1, i => Mutate(() => { foreach (var key in Canvas.SelectedFaces) { Document.Fills.TryAdd(key, Canvas.PaintColor); Document.FillPatterns[key] = (FillPattern)i; Document.Gradients.Remove(key); } }));
        }
    }
    private void PaintStyleInspector()
    {
        GradientControls(); if (!Canvas.PaintLines && Canvas.GradientMode) return;
        if (Canvas.PaintLines) ModeButtons(new[] { L.T("Continu"), L.T("Long"), L.T("Court") }, (int)Canvas.PaintLineStyle, i => { Canvas.PaintLineStyle = (LineStyle)i; UpdateInspector(); });
        else ModeButtons(new[] { L.T("Uni"), L.T("Bandes"), L.Dots }, (int)Canvas.PaintPattern, i => { Canvas.PaintPattern = (FillPattern)i; UpdateInspector(); });
    }
    private void AnnotationInspector()
    {
        var selectedNote = Document.Annotations.FirstOrDefault(a => a.Id == Canvas.SelectedAnnotation);
        bool tokens = selectedNote?.IsToken ?? Canvas.Tool == EditorTool.Token;
        if (tokens) TokenPalette(selectedNote);
        AnnotationOptions();
        if (!tokens) ToolHint(L.T("Entrée valide le texte ; Maj+Entrée insère un retour à la ligne."));
        if (!tokens) ToolHint(L.T("Cliquez dans le plan pour placer un texte. Cliquez sur un texte pour le modifier, glissez pour le déplacer. Les annotations suivent leur niveau et sont incluses dans les exports."));
        var note = Document.Annotations.FirstOrDefault(a => a.Id == Canvas.SelectedAnnotation);
        if (note == null) { if (tokens) TokenDefaults(); return; }
        var text = new TextBox { Text = note.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 90, MaxLength = 4000, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 10) }; System.Windows.Automation.AutomationProperties.SetName(text,L.T("Texte de l’annotation")); if (!note.IsToken) inspector.Children.Add(text);
        var size = Field(L.T("Taille (u)"), F(note.Size)); var color = Field(L.T("Couleur"), note.Color, numeric: false); var x = Field("X", F(note.X)); var y = Field("Y", F(note.Y));
        var wheel = ColorButton(() => note.Color, value => { note.Color = value; color.Text = value; }); wheel.Margin = new Thickness(0, 0, 0, 10); inspector.Children.Add(wheel);
        Action applyText = () =>
        {
            if (!note.IsToken && string.IsNullOrWhiteSpace(text.Text)) { Notify(L.T("Saisissez un texte pour l’annotation.")); return; }
            if (!Number(size, out var n) || !Number(x, out var nx) || !Number(y, out var ny)) return;
            if (n < 4 || n > 256) { Notify(L.T("La taille doit être comprise entre 4 et 256 unités.")); return; }
            Color c; try { c = (Color)ColorConverter.ConvertFromString(color.Text.Trim()); } catch { Notify(L.T("Couleur invalide. Exemple : #FFFFFF")); return; }
            Mutate(() => { note.Text = text.Text; note.Size = n; note.X = nx; note.Y = ny; note.Color = $"#{c.R:X2}{c.G:X2}{c.B:X2}"; }); Canvas.Focus();
        };
        text.PreviewKeyDown += (_,e) => { if(HandleAnnotationKey(text,e.Key,Keyboard.Modifiers,applyText))e.Handled=true; };
        inspector.Children.Add(ConfirmButton(L.T(note.IsToken ? "Appliquer le jeton" : "Appliquer le texte"), applyText, size, color, x, y));
        var remove = Button(L.T(note.IsToken ? "Supprimer le jeton" : "Supprimer le texte"), Canvas.DeleteSelection); remove.Margin = new Thickness(0, 8, 0, 0); inspector.Children.Add(remove);
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
        if (note.IsToken) return TokenShapes.Svg(note);
        var s = new StringBuilder($"<text font-family=\"Segoe UI, sans-serif\" font-size=\"{F(note.Size)}\" fill=\"{E(note.Color)}\" xml:space=\"preserve\">");
        var lines = note.Text.Replace("\r", "").Split('\n'); double lineHeight = note.Layout().Height / lines.Length;
        for (int i = 0; i < lines.Length; i++) s.Append($"<tspan x=\"{F(note.X)}\" y=\"{F(-note.Y + note.Layout().Baseline + i * lineHeight)}\">{E(lines[i])}</tspan>");
        return s.Append("</text>").ToString();
    }
}

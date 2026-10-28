// Coat — Epicteta Lupercal
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Coat;

public enum StairKind { Straight, L, U, Spiral }
public record StairLine(V2 A, V2 B, LineStyle Style = LineStyle.Solid);
public static class Stairs
{
    // Lines are shared by the live preview and final geometry; divisions keep their exact spacing.
    public static List<StairLine> Build(StairKind kind, V2 start, V2 end, int steps, double width, double angle, bool right, int grid)
    {
        if (!Enum.IsDefined(kind) || steps < 2 || steps > 128 || !double.IsFinite(width) || width < 1 || width > 100000 || !double.IsFinite(angle) || angle < 15 || angle > 360 || grid <= 0) throw new ArgumentOutOfRangeException(nameof(steps), L.T("Paramètres d’escalier invalides."));
        start = start.Snap(grid); end = end.Snap(grid); var delta = end - start;
        var lines = new List<StairLine>();
        void Line(V2 a, V2 b, LineStyle style = LineStyle.Solid) { if ((a - b).Length > 1e-7) lines.Add(new(a, b, style)); }
        if (kind == StairKind.Spiral)
        {
            double radius = Math.Round(delta.Length / grid, MidpointRounding.AwayFromZero) * grid;
            if (radius < grid) return lines;
            double inner = radius * .22, initial = Math.Atan2(delta.Y, delta.X), sweep = angle * Math.PI / 180 * (right ? -1 : 1);
            V2 At(double r, double a) => start + new V2(Math.Cos(a), Math.Sin(a)) * r;
            int subdivisions = Math.Max(1, (int)Math.Ceiling(angle / steps / 6));
            int total = steps * subdivisions;
            for (int i = 0; i < total; i++)
            {
                double a = initial + sweep * i / total, b = initial + sweep * (i + 1) / total;
                Line(At(inner, a), At(inner, b)); Line(At(radius, a), At(radius, b));
            }
            // A complete turn has no entrance/exit boundary: every radial is a tread.
            bool closed = Math.Abs(angle - 360) < 1e-8;
            for (int i = 0; i < (closed ? steps : steps + 1); i++)
            {
                double a = initial + sweep * i / steps;
                Line(At(inner, a), At(radius, a), !closed && (i == 0 || i == steps) ? LineStyle.Solid : LineStyle.LongDash);
            }
            return lines;
        }
        var forward = Math.Abs(delta.X) >= Math.Abs(delta.Y) ? new V2(Math.Sign(delta.X), 0) : new V2(0, Math.Sign(delta.Y));
        double length = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
        if (length < grid) return lines;
        var side = new V2(-forward.Y, forward.X) * (right ? -1 : 1);
        V2 P(double x, double y) => start + forward * x + side * y;
        void Local(double x0, double y0, double x1, double y1, LineStyle style = LineStyle.LongDash) => Line(P(x0, y0), P(x1, y1), style);
        void Outline(params V2[] points) { for (int i = 0; i < points.Length; i++) { var a = points[i]; var b = points[(i + 1) % points.Length]; Local(a.X, a.Y, b.X, b.Y, LineStyle.Solid); } }
        double half = width / 2;
        if (kind == StairKind.Straight)
        {
            Outline(new(0, -half), new(length, -half), new(length, half), new(0, half));
            for (int i = 1; i < steps; i++) Local(length * i / steps, -half, length * i / steps, half);
            return lines;
        }
        int first = (steps + 1) / 2, second = steps - first;
        double run1 = length * first / steps, run2 = length * second / steps;
        if (kind == StairKind.L)
        {
            Outline(new(0, -half), new(run1 + width, -half), new(run1 + width, half + run2), new(run1, half + run2), new(run1, half), new(0, half));
            for (int i = 1; i <= first; i++) Local(run1 * i / first, -half, run1 * i / first, half);
            for (int i = 0; i < second; i++) Local(run1, half + run2 * i / second, run1 + width, half + run2 * i / second);
        }
        else
        {
            double gap = width / 2, near = half + gap, far = near + width;
            Outline(new(0, -half), new(run1 + width, -half), new(run1 + width, far), new(run1 - run2, far), new(run1 - run2, near), new(run1, near), new(run1, half), new(0, half));
            for (int i = 1; i <= first; i++) Local(run1 * i / first, -half, run1 * i / first, half);
            for (int i = 0; i < second; i++) Local(run1 - run2 * i / second, near, run1 - run2 * i / second, far);
        }
        return lines;
    }
}
public partial class Plan
{
    public GeometryGroup AddStair(IReadOnlyList<StairLine> lines, StairKind kind)
    {
        if (lines.Count == 0) throw new ArgumentException(L.T("L’escalier est vide."));
        var ids = new HashSet<int>();
        foreach (var line in lines)
        {
            int a = Point(line.A), b = Point(line.B);
            bool existing = Edges.Any(e => e.A == a && e.B == b || e.A == b && e.B == a);
            int id = AddEdge(a, b);
            if (id >= 0) { ids.Add(id); if (!existing) Edges.First(e => e.Id == id).Style = line.Style; }
        }
        string[] names = { L.T("Escalier droit"), L.T("Escalier en L"), L.T("Escalier en U"), L.T("Escalier en colimaçon") };
        var group = new GeometryGroup { Id = NextId++, Name = names[(int)kind], EdgeIds = ids.ToList() }; Groups.Add(group);
        Normalize(); return group;
    }
}
public partial class EditorCanvas
{
    public StairKind Stair { get; set; }
    public int StairSteps { get; set; } = 12;
    public double StairWidth { get; set; } = 96;
    public double StairAngle { get; set; } = 270;
    public bool StairRight { get; set; } = true;
    private List<StairLine> StairPreview(V2 start, V2 end) => Stairs.Build(Stair, start, end, StairSteps, StairWidth, StairAngle, StairRight, Plan.Grid);
    private void FinishStair(V2 end)
    {
        if (shapeStart == null) return;
        var lines = StairPreview(shapeStart.Value, end); if (lines.Count == 0) return;
        if (lines.Any(l => Math.Abs(l.A.X) > 1e8 || Math.Abs(l.A.Y) > 1e8 || Math.Abs(l.B.X) > 1e8 || Math.Abs(l.B.Y) > 1e8)) { Host.Notify(L.T("L’escalier dépasse les limites du plan.")); return; }
        shapeStart = null; shaping = false; ReleaseMouseCapture();
        Host.Mutate(() => { var group = Plan.AddStair(lines, Stair); ClearSelection(); SelectedEdges.UnionWith(group.EdgeIds); });
    }
    private void DrawStairPreview(DrawingContext dc)
    {
        if (shapeStart == null) return;
        var lines = StairPreview(shapeStart.Value, mouseWorld);
        foreach (var line in lines) dc.DrawLine(new Pen(Accent, 1.6) { DashStyle = EdgeDash(line.Style) }, Screen(line.A), Screen(line.B));
        Label(dc, L.F($"{StairSteps} marches") + (Stair == StairKind.Spiral ? $" · {StairAngle:0.#}°" : L.F($" · largeur {StairWidth:0.#} u")), new Point(mouseScreen.X + 14, mouseScreen.Y - 24), 12, Accent);
        dc.DrawEllipse(Accent, null, Screen(shapeStart.Value), 4, 4);
    }
}
public partial class MainWindow
{
    private void StairInspector()
    {
        var choice = new ComboBox { Margin = new Thickness(0, 0, 0, 12), SelectedIndex = (int)Canvas.Stair };
        foreach (string name in new[] { L.T("Droits"), L.T("En L"), L.T("En U"), L.T("En colimaçon") }) choice.Items.Add(name);
        choice.SelectedIndex = (int)Canvas.Stair;
        System.Windows.Automation.AutomationProperties.SetName(choice, L.T("Type d’escalier"));
        choice.SelectionChanged += (_, _) => { if (choice.SelectedIndex >= 0) { Canvas.CancelGesture(); Canvas.Stair = (StairKind)choice.SelectedIndex; UpdateInspector(); Canvas.Focus(); } }; inspector.Children.Add(choice);
        var steps = Field(L.T("Marches"), Canvas.StairSteps.ToString(CultureInfo.InvariantCulture));
        steps.TextChanged += (_, _) => { if (int.TryParse(steps.Text, out int n) && n >= 2 && n <= 128) { Canvas.StairSteps = n; Canvas.InvalidateVisual(); } };
        steps.LostKeyboardFocus += (_, _) => { if (!int.TryParse(steps.Text, out int n) || n < 2 || n > 128) { steps.Text = Canvas.StairSteps.ToString(CultureInfo.InvariantCulture); Notify(L.T("Choisissez entre 2 et 128 marches au total.")); } };
        AddStepper(steps, L.T("Marches"), 2, 128, () => Canvas.StairSteps, integer: true);
        if (Canvas.Stair == StairKind.Spiral)
        {
            StairField("Angle (°)", Canvas.StairAngle, 15, 360, value => Canvas.StairAngle = value);
            ToolHint(L.T("Placez le centre, puis glissez vers le rayon extérieur. L’angle définit l’ouverture de 15° à 360°. Les marches tournent autour d’un vide central."));
        }
        else
        {
            StairField(L.T("Largeur (u)"), Canvas.StairWidth, 1, 100000, value => Canvas.StairWidth = value);
            ToolHint(L.T("Placez le milieu de l’entrée, puis glissez dans le sens de montée. L’axe dominant de la souris oriente l’escalier. La distance règle la longueur cumulée des volées, hors palier."));
        }
        if (Canvas.Stair != StairKind.Straight) ModeButtons(new[] { L.T("Tourne à gauche"), L.T("Tourne à droite") }, Canvas.StairRight ? 1 : 0, i => { Canvas.StairRight = i == 1; Canvas.InvalidateVisual(); UpdateInspector(); });
        ToolHint(L.T("2 à 128 marches au total ; les paliers ne comptent pas comme marches. Contours continus, marches en tirets longs. L’ensemble est regroupé pour le déplacer et le transformer."));
    }
    private void StairField(string title, double current, double minimum, double maximum, Action<double> assign)
    {
        var field = Field(title, F(current)); double last = current;
        bool Valid(out double value) => double.TryParse(field.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && value >= minimum && value <= maximum;
        field.TextChanged += (_, _) => { if (Valid(out double value)) { last = value; assign(value); Canvas.InvalidateVisual(); } };
        field.LostKeyboardFocus += (_, _) => { if (!Valid(out _)) { field.Text = F(last); Notify(L.F($"{title} : entre {minimum} et {maximum}.")); } };
        AddStepper(field, title, minimum, maximum, () => last);
    }
}

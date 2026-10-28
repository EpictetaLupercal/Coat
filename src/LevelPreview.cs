// Coat — Epicteta Lupercal
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Coat;

// An axonometric projection of stacked horizontal planes, intentionally display-only.
public sealed class LevelPreview : FrameworkElement
{
    private readonly int[] levels;
    private readonly int current;
    public LevelPreview(IEnumerable<int> levels, int current)
    {
        this.levels = levels.Order().ToArray(); this.current = current;
        Height = 150; IsHitTestVisible = false; Focusable = false;
        System.Windows.Automation.AutomationProperties.SetName(this, L.T("Aperçu des niveaux en trois quarts, niveau actif en rouge"));
    }
    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        dc.DrawRoundedRectangle(Brushes.Black, new Pen(EditorCanvas.Brush("#303D4C"), 1), new Rect(0, 0, width, ActualHeight), 6, 6);
        var shown = levels.OrderBy(l => Math.Abs((long)l - current)).Take(7).Order().ToArray();
        double gap = shown.Length > 5 ? 13 : 19;
        double verticalScale = Math.Min(1, (ActualHeight - 30) / (66 + (shown.Length - 1) * gap)); gap *= verticalScale;
        double cx = width * .43, cy = (ActualHeight - 16) / 2 + (shown.Length - 1) * gap / 2;
        double sx = Math.Min(1, (width - 65) / 156);
        Point Project(double x, double y, int i) => new(cx + (x * .78 - y * .55) * sx, cy + (x * .26 + y * .36) * sx * verticalScale - i * gap);
        for (int i = 0; i < shown.Length; i++)
        {
            var points = new[] { Project(-52, -52, i), Project(52, -52, i), Project(52, 52, i), Project(-52, 52, i) };
            var geo = new StreamGeometry(); using (var ctx = geo.Open()) { ctx.BeginFigure(points[0], true, true); ctx.PolyLineTo(points.Skip(1).ToArray(), true, false); }
            bool active = shown[i] == current;
            dc.DrawGeometry(EditorCanvas.Brush(active ? "#A52F3E" : "#34383F"), new Pen(EditorCanvas.Brush(active ? "#FF6675" : "#727A85"), active ? 1.8 : 1), geo);
            for (int n = -1; n <= 1; n++)
            {
                var pen = new Pen(EditorCanvas.Brush(active ? "#CC4655" : "#50565F"), .5);
                dc.DrawLine(pen, Project(n * 26, -52, i), Project(n * 26, 52, i)); dc.DrawLine(pen, Project(-52, n * 26, i), Project(52, n * 26, i));
            }
            var label = new FormattedText((shown[i] > 0 ? "+" : "") + shown[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, EditorCanvas.Brush(active ? "#FF7C87" : "#8D96A2"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(label, new Point(width - 34, cy - i * gap - 6));
        }
        var note = new FormattedText(levels.Length > shown.Length ? L.F($"{levels.Length} niveaux · vue centrée sur {current}") : L.T("VUE 3/4"), L.Culture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9, EditorCanvas.Brush("#737D8C"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(note, new Point(10, ActualHeight - 18));
    }
}

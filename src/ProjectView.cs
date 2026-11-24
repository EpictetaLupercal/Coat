// Coat — Epicteta Lupercal
namespace Coat;
public partial class Plan
{
    public bool ShowGrid { get; set; } = true;
    public bool ShowLengths { get; set; }
    public bool ShowUpperLevels { get; set; }
    public bool ShowLowerLevels { get; set; } = true;
    public bool SelectAcrossLevels { get; set; }
}
public partial class MainWindow
{
    private void SaveView()
    {
        var (center, zoom) = Canvas.PersistentView;
        Document.ViewX = center.X; Document.ViewY = center.Y; Document.Zoom = zoom;
        Document.ShowGrid = Canvas.ShowGrid; Document.ShowLengths = Canvas.ShowLengths;
        Document.ShowUpperLevels = Canvas.ShowUpperLevels; Document.ShowLowerLevels = Canvas.ShowLowerLevels;
        Document.SelectAcrossLevels = Canvas.SelectAcrossLevels;
    }
    private void RestoreView()
    {
        Canvas.ShowGrid = Document.ShowGrid; Canvas.ShowLengths = Document.ShowLengths;
        Canvas.ShowUpperLevels = Document.ShowUpperLevels; Canvas.ShowLowerLevels = Document.ShowLowerLevels;
        Canvas.SelectAcrossLevels = Document.SelectAcrossLevels;
        upperLevelsMenu.IsChecked = Canvas.ShowUpperLevels; lowerLevelsMenu.IsChecked = Canvas.ShowLowerLevels;
    }
}

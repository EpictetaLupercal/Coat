// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace Coat;
public partial class EditorCanvas
{
    public TokenKind TokenShape { get; set; }
    public double TokenSize { get; set; } = 32;
}
public partial class MainWindow
{
    private void TokenPalette(Annotation? note)
    {
        var grid = new UniformGrid { Columns = 6, Margin = new Thickness(-2,0,-2,8) };
        var active = note?.TokenShape ?? Canvas.TokenShape;
        foreach (var kind in Enum.GetValues<TokenKind>())
        {
            var button = Button("", () => { Canvas.TokenShape = kind; if (note != null) Mutate(() => note.TokenShape = kind); else { UpdateInspector(); Canvas.InvalidateVisual(); } });
            button.Content = new Image { Source = TokenShapes.Icon(kind), Width = 24, Height = 24 };
            button.Height = 36; button.Padding = new Thickness(4); button.Margin = new Thickness(2,0,2,5);
            button.ToolTip = TokenShapes.Name(kind); System.Windows.Automation.AutomationProperties.SetName(button,TokenShapes.Name(kind));
            if (kind == active) { button.Background = B("#304E48"); button.BorderBrush = B("#78DCC1"); }
            grid.Children.Add(button);
        }
        inspector.Children.Add(grid);
        ToolHint(L.T("Choisissez un symbole puis cliquez pour placer un jeton. La couleur et la taille sont personnalisables. Sélectionnez un jeton pour modifier sa forme."));
    }
    private void TokenDefaults()
    {
        var size = Field(L.T("Taille (u)"), F(Canvas.TokenSize)); size.Tag = new NumericRange(4,256,false);
        inspector.Children.Add(ConfirmButton(L.T("Utiliser les réglages du jeton"), () => { if (Number(size,out var value) && value >= 4 && value <= 256) { Canvas.TokenSize = value; Canvas.Focus(); } },size));
        var color = ColorButton(() => Canvas.PaintColor, value => Canvas.PaintColor = value);
        color.Margin = new Thickness(0,8,0,0); inspector.Children.Add(color);
    }
}

// Coat — Epicteta Lupercal
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Coat;
public partial class MainWindow
{
    // Keep the original TextBox and its validation; arrows update it through the same TextChanged path.
    private void AddStepper(TextBox input, string title, double minimum, double maximum, Func<double> current, bool integer = false)
    {
        var parent = (Grid)input.Parent; int column = Grid.GetColumn(input); parent.Children.Remove(input);
        var control = new Grid(); control.ColumnDefinitions.Add(new()); control.ColumnDefinitions.Add(new() { Width = new GridLength(22) });
        Grid.SetColumn(input, 0); control.Children.Add(input);
        var arrows = new Grid { Margin = new Thickness(2, 0, 0, 0) }; arrows.RowDefinitions.Add(new()); arrows.RowDefinitions.Add(new()); Grid.SetColumn(arrows, 1); control.Children.Add(arrows);
        void Adjust(int direction)
        {
            bool valid = double.TryParse(input.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value);
            value = valid ? value : current();
            if (integer) value = Math.Round(value);
            input.Text = Math.Clamp(value + direction, minimum, maximum).ToString("0.###", CultureInfo.InvariantCulture);
        }
        var up = Button("▴", () => Adjust(1)); var down = Button("▾", () => Adjust(-1));
        foreach (var b in new[] { up, down }) { b.Padding = new Thickness(0); b.FontSize = 10; b.Focusable = false; }
        AutomationProperties.SetName(up, L.F($"Augmenter {title}")); AutomationProperties.SetName(down, L.F($"Diminuer {title}"));
        up.ToolTip = AutomationProperties.GetName(up); down.ToolTip = AutomationProperties.GetName(down);
        arrows.Children.Add(up); Grid.SetRow(down, 1); arrows.Children.Add(down);
        input.PreviewKeyDown += (_, e) => { if (e.Key is Key.Up or Key.Down) { Adjust(e.Key == Key.Up ? 1 : -1); e.Handled = true; } };
        Grid.SetColumn(control, column); parent.Children.Add(control);
    }
}

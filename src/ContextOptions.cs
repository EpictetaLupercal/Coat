// Coat — Epicteta Lupercal
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Coat;

public partial class MainWindow
{
    private Border contextBar = null!;
    private StackPanel contextOptions = null!;
    private (TextBox X, TextBox Y)? contextOffset;

    private Border CreateContextBar()
    {
        contextOptions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 4, 12, 4) };
        contextBar = new Border
        {
            Child = contextOptions, Background = B("#1B2430"), BorderBrush = B("#2B3645"),
            BorderThickness = new Thickness(0, 0, 0, 1), Visibility = Visibility.Collapsed
        };
        AutomationProperties.SetName(contextBar, L.T("Options de l’outil"));
        return contextBar;
    }

    private void UpdateContextBar()
    {
        contextOptions.Children.Clear();
        selectionModeButtons.Clear();
        contextOffset = null;
        contextBar.Visibility = Canvas.Tool is EditorTool.Select or EditorTool.Shapes ? Visibility.Visible : Visibility.Collapsed;
        if (Canvas.Tool == EditorTool.Select)
        {
            AddSelectionModeButtons(contextOptions);
            AddToolbarDivider(contextOptions);
            TransformOptions(contextOptions);
        }
        else if (Canvas.Tool == EditorTool.Shapes) ShapeOptions(contextOptions);
    }

    private void ChooseShape(ShapeKind shape, StairKind? stair = null)
    {
        Canvas.Shape = shape;
        if (stair.HasValue) Canvas.Stair = stair.Value;
        SetTool(EditorTool.Shapes);
    }

    private void ShapeOptions(Panel row)
    {
        foreach (var (shape, name, glyph, help, shortcut) in new[]
        {
            (ShapeKind.Rectangle, "Rectangle", "M3,5 H21 V19 H3 Z", "Tracez un rectangle par deux coins opposés.", "Rectangle"),
            (ShapeKind.Circle, "Cercle", "M12,3 A9,9 0 1 1 11.99,3 Z", "Tracez un cercle à partir de son centre et de son rayon.", "Cercle"),
            (ShapeKind.Stairs, "Escaliers", "M3,20 H21 V4 H15 V9 H10 V14 H3 Z M10,14 H21 M15,9 H21", "Tracez un escalier et choisissez son type.", (string?)null)
        })
        {
            var button = Button("", () => ChooseShape(shape), shortcut);
            IconButton(button, L.T(name), glyph, L.T(help));
            SetActive(button, Canvas.Shape == shape);
            row.Children.Add(button);
        }

        if (Canvas.Shape == ShapeKind.Circle)
            AddLiveNumber(L.T("Points"), Canvas.CirclePoints, 3, 128, value => Canvas.CirclePoints = (int)value, integer: true);
        else if (Canvas.Shape == ShapeKind.Stairs)
        {
            var choice = new ComboBox
            {
                ItemsSource = new[] { L.T("Droits"), L.T("En L"), L.T("En U"), L.T("En colimaçon") },
                SelectedIndex = (int)Canvas.Stair, Width = 136, Height = 32, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 2, 0)
            };
            AutomationProperties.SetName(choice, L.T("Type d’escalier"));
            choice.SelectionChanged += (_, _) =>
            {
                if (choice.SelectedIndex >= 0) ChooseShape(ShapeKind.Stairs, (StairKind)choice.SelectedIndex);
            };
            row.Children.Add(choice);
            AddLiveNumber(L.T("Marches"), Canvas.StairSteps, 2, 128, value => Canvas.StairSteps = (int)value, integer: true);
            if (Canvas.Stair == StairKind.Spiral)
                AddLiveNumber("Angle (°)", Canvas.StairAngle, 15, 360, value => Canvas.StairAngle = value);
        }

        void AddLiveNumber(string title, double value, double minimum, double maximum, Action<double> change, bool integer = false)
        {
            var field = ContextNumber(row, title, title, F(value));
            ConfigureLiveNumber(field, title, value, minimum, maximum, change, integer);
        }
    }

    private TextBox ContextNumber(Panel row, string label, string title, string text)
    {
        var field = new Grid { Margin = new Thickness(9, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        field.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        field.ColumnDefinitions.Add(new ColumnDefinition());
        field.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), Foreground = B("#A7B6C9") });
        var input = new TextBox { Text = text, Width = 64, Height = 32, Padding = new Thickness(5, 3, 5, 3), VerticalContentAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(input, title);
        Grid.SetColumn(input, 1);
        field.Children.Add(input);
        row.Children.Add(field);
        NumericInput(input);
        return input;
    }

    private TextBox TransformNumber(Panel row, string label, string title, string text, double minimum = -1e8, double maximum = 1e8)
    {
        var input = ContextNumber(row, label, title, text);
        AddStepper(input, title, minimum, maximum, () => 0);
        return input;
    }

    private void ConfigureLiveNumber(TextBox field, string title, double current, double minimum, double maximum, Action<double> assign, bool integer = false)
    {
        double last = current;
        bool Valid(out double value) => double.TryParse(field.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            double.IsFinite(value) && value >= minimum && value <= maximum && (!integer || value == Math.Truncate(value));
        field.TextChanged += (_, _) =>
        {
            if (!Valid(out var value)) return;
            last = value;
            assign(value);
            Canvas.InvalidateVisual();
        };
        field.LostKeyboardFocus += (_, _) => { if (!Valid(out _)) field.Text = F(last); };
        AddStepper(field, title, minimum, maximum, () => last, integer);
    }
}

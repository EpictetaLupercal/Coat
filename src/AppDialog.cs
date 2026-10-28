// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Coat;

internal static class AppDialog
{
    // Application dialogs follow Coat's language, even when Windows uses another language.
    internal static Window Create(Window? owner, string message, string title, MessageBoxButton buttons, MessageBoxResult defaultResult, Action<MessageBoxResult> selected)
    {
        var dialog = new Window
        {
            Title = title, Width = 640, SizeToContent = SizeToContent.Height, MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 40),
            ResizeMode = ResizeMode.NoResize, Background = Brushes.White, Foreground = Brushes.Black, FontFamily = new FontFamily("Segoe UI"), FontSize = 13,
            ShowInTaskbar = false, WindowStartupLocation = owner?.IsVisible == true ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
        };
        if (owner?.IsVisible == true) dialog.Owner = owner;
        var root = new DockPanel { Margin = new Thickness(20) }; dialog.Content = root;
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { MaxHeight = Math.Max(180, Math.Min(600, SystemParameters.WorkArea.Height - 170)), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, LineHeight = 20 } });
        var results = buttons switch
        {
            MessageBoxButton.YesNo => new[] { MessageBoxResult.Yes, MessageBoxResult.No },
            MessageBoxButton.YesNoCancel => new[] { MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel },
            MessageBoxButton.OKCancel => new[] { MessageBoxResult.OK, MessageBoxResult.Cancel },
            _ => new[] { MessageBoxResult.OK }
        };
        if (defaultResult == MessageBoxResult.None) defaultResult = results[0];
        foreach (var result in results)
        {
            string label = result switch { MessageBoxResult.Yes => L.T("Oui"), MessageBoxResult.No => L.T("Non"), MessageBoxResult.Cancel => L.T("Annuler"), _ => "OK" };
            var button = new Button { Content = label, MinWidth = 80, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(8, 0, 0, 0), IsDefault = result == defaultResult, IsCancel = result == MessageBoxResult.Cancel || results.Length == 1 };
            button.Click += (_, _) => { selected(result); dialog.Close(); }; footer.Children.Add(button);
        }
        return dialog;
    }
    public static MessageBoxResult Show(Window owner, string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None) => ShowCore(owner, message, title, buttons, defaultResult);
    public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None) => ShowCore(null, message, title, buttons, MessageBoxResult.None);
    private static MessageBoxResult ShowCore(Window? owner, string message, string title, MessageBoxButton buttons, MessageBoxResult defaultResult)
    {
        var result = MessageBoxResult.Cancel;
        var dialog = Create(owner, message, title, buttons, defaultResult, chosen => result = chosen);
        dialog.ShowDialog(); return result;
    }
}

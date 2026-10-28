// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Coat;

internal sealed record ScreenFrame(BitmapSource Bitmap, int Left, int Top, double ScaleX = 1, double ScaleY = 1)
{
    private readonly byte[] pixels = ReadPixels(Bitmap);
    private static byte[] ReadPixels(BitmapSource bitmap)
    {
        var data = new byte[checked(bitmap.PixelWidth * bitmap.PixelHeight * 4)];
        bitmap.CopyPixels(data, bitmap.PixelWidth * 4, 0); return data;
    }
    public bool TryColor(Point screenPoint, out Color color)
    {
        int x = (int)Math.Floor(screenPoint.X) - Left, y = (int)Math.Floor(screenPoint.Y) - Top;
        color = Colors.Black;
        if (x < 0 || y < 0 || x >= Bitmap.PixelWidth || y >= Bitmap.PixelHeight) return false;
        int index = (y * Bitmap.PixelWidth + x) * 4;
        color = Color.FromRgb(pixels[index + 2], pixels[index + 1], pixels[index]); return true;
    }
}

internal static class ColorSampling
{
    // Render only Coat's canvas, including its reference image. No desktop capture is needed.
    public static ScreenFrame FromVisual(FrameworkElement canvas)
    {
        canvas.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(canvas);
        int width = (int)Math.Ceiling(canvas.ActualWidth * dpi.DpiScaleX), height = (int)Math.Ceiling(canvas.ActualHeight * dpi.DpiScaleY);
        if (width <= 0 || height <= 0) throw new InvalidOperationException(L.T("Le plan n’est pas visible."));
        var bitmap = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        var source = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr32, null, 0); source.Freeze();
        var origin = canvas.PointToScreen(new Point());
        return new ScreenFrame(source, (int)Math.Round(origin.X), (int)Math.Round(origin.Y), dpi.DpiScaleX, dpi.DpiScaleY);
    }

}

internal sealed class CanvasColorPicker : Window
{
    private readonly ScreenFrame frame;
    private readonly TextBlock hint = new() { Foreground = Brushes.White, FontSize = 13, Text = L.T("Cliquez sur le plan pour prélever\nÉchap ou clic droit : annuler") };
    private readonly Border sample = new() { Width = 24, Height = 24, Margin = new Thickness(0, 0, 9, 0), BorderBrush = Brushes.White, BorderThickness = new Thickness(1) };
    private readonly Border label;
    public Color? PickedColor { get; private set; }
    public CanvasColorPicker(Window owner, ScreenFrame frame)
    {
        this.frame = frame; Owner = owner; Title = L.T("Pipette — Coat"); WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Cursor = Cursors.Cross; WindowStartupLocation = WindowStartupLocation.Manual;
        Left = frame.Left / frame.ScaleX; Top = frame.Top / frame.ScaleY;
        Width = frame.Bitmap.PixelWidth / frame.ScaleX; Height = frame.Bitmap.PixelHeight / frame.ScaleY;
        var root = new Grid(); Content = root;
        var image = new Image { Source = frame.Bitmap, Stretch = Stretch.Fill }; RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor); root.Children.Add(image);
        var overlay = new Canvas { IsHitTestVisible = false }; root.Children.Add(overlay);
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; row.Children.Add(sample); row.Children.Add(hint);
        label = new Border { Child = row, Background = EditorCanvas.Brush("#17212B"), BorderBrush = Brushes.White, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(10), MaxWidth = 420 };
        overlay.Children.Add(label);
        Canvas.SetLeft(label, 12); Canvas.SetTop(label, 12);
        Loaded += (_, _) =>
        {
            var origin = PointToScreen(new Point()); var dpi = VisualTreeHelper.GetDpi(this);
            Left += (frame.Left - origin.X) / dpi.DpiScaleX; Top += (frame.Top - origin.Y) / dpi.DpiScaleY;
            Width = frame.Bitmap.PixelWidth / dpi.DpiScaleX; Height = frame.Bitmap.PixelHeight / dpi.DpiScaleY;
            Focus(); UpdateSample();
        };
        MouseMove += (_, _) => UpdateSample();
        MouseLeftButtonDown += (_, e) => { PickAt(ScreenPoint(Mouse.GetPosition(this))); e.Handled = true; };
        MouseRightButtonDown += (_, e) => { Close(); e.Handled = true; };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }
    internal void PickAt(Point screenPoint)
    {
        if (frame.TryColor(screenPoint, out var color)) { PickedColor = color; DialogResult = true; }
    }
    private Point ScreenPoint(Point point) => new(frame.Left + point.X * frame.Bitmap.PixelWidth / Math.Max(1, ActualWidth), frame.Top + point.Y * frame.Bitmap.PixelHeight / Math.Max(1, ActualHeight));
    private void UpdateSample()
    {
        var local = Mouse.GetPosition(this); var screenPoint = ScreenPoint(local);
        if (!frame.TryColor(screenPoint, out var color)) return;
        sample.Background = new SolidColorBrush(color); hint.Text = ColorMath.Hex(color) + L.T("\nClic sur le plan : prélever · Échap / clic droit : annuler");
        var point = local;
        Canvas.SetLeft(label, Math.Clamp(point.X + 20, 0, Math.Max(0, ActualWidth - Math.Max(label.ActualWidth, 320))));
        Canvas.SetTop(label, Math.Clamp(point.Y + 24, 0, Math.Max(0, ActualHeight - Math.Max(label.ActualHeight, 65))));
    }
}

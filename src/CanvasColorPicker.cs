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
        if (canvas is EditorCanvas editor)
        {
            var snapshot = new DrawingVisual();
            using (var dc = snapshot.RenderOpen()) editor.DrawViewport(dc, snapshot: true);
            bitmap.Render(snapshot);
        }
        else bitmap.Render(canvas);
        var source = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr32, null, 0); source.Freeze();
        var origin = canvas.PointToScreen(new Point());
        return new ScreenFrame(source, (int)Math.Round(origin.X), (int)Math.Round(origin.Y), dpi.DpiScaleX, dpi.DpiScaleY);
    }

}

public partial class EditorCanvas
{
    private ScreenFrame? colorFrame;
    private Action<Color?>? colorSampleCompleted;
    internal bool IsColorSampling => colorSampleCompleted != null;
    internal void BeginColorSampling(ScreenFrame frame,Action<Color?> completed)
    { colorFrame=frame;colorSampleCompleted=completed;Focus();CaptureMouse();Cursor=Cursors.Cross;Host.Notify(L.T("Cliquez sur le plan pour prélever\nÉchap ou clic droit : annuler")); }
    internal void SampleColorAt(Point physicalPoint)
    {
        if(colorFrame==null)return;
        FinishColorSampling(colorFrame.TryColor(physicalPoint,out var color)?color:null);
    }
    internal void CancelColorSampling() => FinishColorSampling(null);
    private void FinishColorSampling(Color? color)
    {
        var completed=colorSampleCompleted;colorSampleCompleted=null;colorFrame=null;ReleaseMouseCapture();Cursor=ToolCursor;completed?.Invoke(color);
    }
}

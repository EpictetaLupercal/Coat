// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Wpf;
using SkiaSharp;

namespace Coat;

// The control presents an OpenGL framebuffer through WPF's compositor, so the HUD
// and input surface can still overlap it. Only camera or scene changes request frames.
internal sealed class OpenGlPlan : Border
{
    private readonly EditorCanvas editor;
    private GLWpfControl? gl;
    private GRContext? context;
    private DrawingGroup? recorded;
    private SKPicture? picture;
    private SKImage? layer;
    private SKPoint layerOffset;
    private float layerScale;
    private int layerWidth, layerHeight;
    private BitmapSource? source;
    private SKBitmap? background;
    private Frame? frame;
    internal string? Device { get; private set; }
    internal bool Active => gl != null;
    internal int Frames { get; private set; }
    internal int SceneRecordings { get; private set; }
    internal string? Failure { get; private set; }
    internal sealed record Frame(DrawingGroup Scene, V2 Origin, double SceneZoom, V2 Center, double Zoom, Size Size,
        bool Grid, double Step, BitmapSource? Image, Rect ImageRect, double ImageOpacity);

    internal OpenGlPlan(EditorCanvas canvas)
    {
        editor = canvas;
        IsHitTestVisible = Focusable = false;
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
            new Action(() => { if (!IsLoaded) Stop(); }));
    }

    internal void Close() => Stop();

    private void Start()
    {
        if (Active) return;
        try
        {
            var control = new GLWpfControl { Focusable = false };
            gl = control;
            control.Start(new GLWpfControlSettings { MajorVersion = 3, MinorVersion = 3, RenderContinuously = false });
            control.Render += Render;
            Child = control;
            editor.InvalidateVisual();
        }
        catch (Exception error) { Fail(error); }
    }

    private void Fail(Exception error)
    {
        Failure = error.Message;
        System.Diagnostics.Trace.WriteLine($"Coat OpenGL: {error}");
        Stop();
        editor.InvalidateVisual();
    }

    private void Stop()
    {
        var control = gl;
        gl = null;
        if (control != null)
        {
            control.Render -= Render;
            if (context != null) { control.Context?.MakeCurrent(); layer?.Dispose(); layer = null; context.Dispose(); context = null; }
            Child = null;
            control.Dispose();
        }
        layer?.Dispose(); layer = null;
        picture?.Dispose(); picture = null; recorded = null;
        background?.Dispose(); background = null; source = null; frame = null;
    }

    internal void Present(Frame next)
    {
        if (next == frame) return;
        frame = next;
        gl?.InvalidateVisual();
    }

    private void Render(TimeSpan elapsed)
    {
        if (gl == null || frame is not { } f || f.Size.Width <= 0 || f.Size.Height <= 0) return;
        try
        {
            Device ??= GL.GetString(StringName.Renderer);
            context ??= GRContext.CreateGl() ?? throw new InvalidOperationException("OpenGL context unavailable");
            context.ResetContext();
            int w = gl.FrameBufferWidth, h = gl.FrameBufferHeight;
            using var target = new GRBackendRenderTarget(w, h, 0, 0, new GRGlFramebufferInfo((uint)gl.Framebuffer, (uint)SizedInternalFormat.Rgba8));
            using var surface = SKSurface.Create(context, target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888)
                ?? throw new InvalidOperationException("OpenGL framebuffer unavailable");
            var canvas = surface.Canvas;
            canvas.Clear(new SKColor(24, 31, 42));
            canvas.Scale((float)(w / f.Size.Width), (float)(h / f.Size.Height));
            if (!ReferenceEquals(source, f.Image))
            {
                background?.Dispose(); background = f.Image == null ? null : Bitmap(f.Image); source = f.Image;
            }
            if (background != null)
            {
                using var tint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(255 * f.ImageOpacity)) };
                canvas.DrawBitmap(background, GlDrawing.Bounds(f.ImageRect), tint);
            }
            if (f.Grid) DrawGrid(canvas, f);
            if (!ReferenceEquals(f.Scene, recorded))
            {
                using var recorder = new SKPictureRecorder();
                Rect bounds = f.Scene.Bounds;
                var contents = recorder.BeginRecording(bounds.IsEmpty ? SKRect.Empty : GlDrawing.Bounds(bounds));
                GlDrawing.Draw(contents, f.Scene);
                picture?.Dispose(); picture = recorder.EndRecording(); recorded = f.Scene;
                SceneRecordings++;
                layer?.Dispose(); layer = null;
            }
            double scale = f.Zoom / f.SceneZoom;
            var offset = new SKPoint(
                (float)(f.Size.Width / 2 * (1 - scale) + (f.Origin.X - f.Center.X) * f.Zoom),
                (float)(f.Size.Height / 2 * (1 - scale) + (f.Center.Y - f.Origin.Y) * f.Zoom));
            DrawLayer(canvas, offset, (float)scale, w, h, f.Size);
            surface.Flush();
            context.Flush();
            Frames++;
        }
        catch (Exception error)
        {
            // Leave the WGL interop lock before tearing down the framebuffer.
            Dispatcher.BeginInvoke(new Action(() => Fail(error)));
        }
    }

    private void DrawLayer(SKCanvas canvas, SKPoint offset, float scale, int width, int height, Size size)
    {
        const int pad = 192;
        float sx = (float)(width / size.Width), sy = (float)(height / size.Height);
        if (layer != null && layerWidth == width && layerHeight == height)
        {
            float ratio = scale / layerScale;
            var destination = SKRect.Create(
                offset.X - (layerOffset.X + pad / sx) * ratio,
                offset.Y - (layerOffset.Y + pad / sy) * ratio,
                layer.Width / sx * ratio, layer.Height / sy * ratio);
            if (destination.Left <= 0 && destination.Top <= 0 &&
                destination.Right >= size.Width && destination.Bottom >= size.Height)
            {
                canvas.DrawImage(layer, destination, new SKSamplingOptions(SKFilterMode.Linear));
                return;
            }
        }

        // A zoom out may expose new areas. Replay the existing vector picture
        // at the new scale to cover them without rebuilding the WPF drawing.
        layer?.Dispose();
        layer = null;
        if ((long)(width + 2 * pad) * (height + 2 * pad) <= 12_000_000)
        {
            using var cache = SKSurface.Create(context!, false, new SKImageInfo(width + 2 * pad, height + 2 * pad));
            if (cache != null)
            {
                cache.Canvas.Clear(SKColors.Transparent);
                cache.Canvas.Translate(pad, pad);
                cache.Canvas.Scale(sx, sy);
                DrawPicture(cache.Canvas, offset, scale);
                layer = cache.Snapshot();
                layerOffset = offset;
                layerScale = scale;
                layerWidth = width;
                layerHeight = height;
                var destination = SKRect.Create(-pad / sx, -pad / sy, layer.Width / sx, layer.Height / sy);
                canvas.DrawImage(layer, destination, new SKSamplingOptions(SKFilterMode.Linear));
                return;
            }
        }
        DrawPicture(canvas, offset, scale);
    }

    private void DrawPicture(SKCanvas canvas, SKPoint offset, float scale)
    {
        canvas.Translate(offset.X, offset.Y);
        canvas.Scale(scale);
        canvas.DrawPicture(picture!);
    }

    internal static SKBitmap Bitmap(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        var bitmap = new SKBitmap(converted.PixelWidth, converted.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        converted.CopyPixels(Int32Rect.Empty, bitmap.GetPixels(), bitmap.ByteCount, bitmap.RowBytes);
        bitmap.SetImmutable();
        return bitmap;
    }

    private static void DrawGrid(SKCanvas canvas, Frame f)
    {
        using var paint = new SKPaint { StrokeWidth = 1 };
        void Axis(double size, double origin, bool vertical)
        {
            double step = f.Step * f.Zoom;
            double start = Math.Ceiling(-origin / step);
            for (int i = 0; i <= Math.Ceiling(size / step); i++)
            {
                double index = start + i;
                float pos = (float)(Math.Round(origin + index * step) + .5);
                paint.Color = index == 0 ? (vertical ? new SKColor(65,86,79) : new SKColor(92,69,72)) :
                    index % 8 == 0 ? new SKColor(50,61,76) : new SKColor(36,45,58);
                if (vertical) canvas.DrawLine(pos, 0, pos, (float)f.Size.Height, paint);
                else canvas.DrawLine(0, pos, (float)f.Size.Width, pos, paint);
            }
        }
        Axis(f.Size.Width, f.Size.Width / 2 - f.Center.X * f.Zoom, true);
        Axis(f.Size.Height, f.Size.Height / 2 + f.Center.Y * f.Zoom, false);
    }
}

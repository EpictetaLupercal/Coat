// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Coat;

internal enum MenuSurface { Top, Inspector }

internal sealed class MenuDecoration : Grid
{
    internal static readonly DependencyProperty MotionAllowedProperty = DependencyProperty.Register(
        "MotionAllowed", typeof(bool), typeof(MenuDecoration),
        new PropertyMetadata(false, (element, _) => ((MenuDecoration)element).RefreshMotion()));

    private const int TileWidth = 320;
    private const int TileHeight = 480;
    private static readonly Lazy<BitmapSource> ClassicFacets = new(() => RenderFacets(false));
    private static readonly Lazy<BitmapSource> BordeauxFacets = new(() => RenderFacets(true));
    private readonly TranslateTransform drift = new();
    private readonly DependencyProperty axis;
    private readonly DoubleAnimation animation;
    private AnimationClock? motionClock;
    private Window? owner;
    internal MenuSurface Surface { get; }
    internal double Offset => (double)drift.GetValue(axis);
    internal bool IsAnimating => drift.HasAnimatedProperties;

    internal MenuDecoration(MenuSurface surface, bool bordeaux)
    {
        Surface = surface;
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = true;
        bool inspector = surface == MenuSurface.Inspector;
        axis = inspector ? TranslateTransform.YProperty : TranslateTransform.XProperty;
        animation = new DoubleAnimation(0, inspector ? -TileHeight : 24, TimeSpan.FromSeconds(inspector ? 76 : 7))
        {
            AutoReverse = !inspector, RepeatBehavior = RepeatBehavior.Forever,
            AccelerationRatio = inspector ? 0 : .25, DecelerationRatio = inspector ? 0 : .25
        };
        animation.Freeze();
        if (inspector) BuildFacets(bordeaux);
        else BuildCircuits(bordeaux);

        Loaded += (_, _) =>
        {
            owner = Window.GetWindow(this);
            if (owner != null) owner.StateChanged += WindowStateChanged;
            RefreshMotion();
        };
        Unloaded += (_, _) =>
        {
            if (owner != null) owner.StateChanged -= WindowStateChanged;
            owner = null;
            SetMotion(false);
        };
        IsVisibleChanged += (_, _) => RefreshMotion();
        SetResourceReference(MotionAllowedProperty, SystemParameters.ClientAreaAnimationKey);
    }

    private void BuildCircuits(bool bordeaux)
    {
        var trace = EditorCanvas.Brush(bordeaux ? "#987366" : "#53778F");
        var signal = EditorCanvas.Brush(bordeaux ? "#BA8B84" : "#6E9DB6");
        var board = new Canvas { Width = 1200, Height = 86 };
        Children.Add(new Viewbox { Stretch = Stretch.Fill, Child = board });
        void Trace(string data, Brush brush, double opacity, bool moves = false)
        {
            var path = new Path { Data = Geometry.Parse(data), Stroke = brush, StrokeThickness = 1, Opacity = opacity };
            path.Data.Freeze();
            if (moves) path.RenderTransform = drift;
            board.Children.Add(path);
        }

        Trace("M0,63 H309 L339,33 H538 L560,11 H711 M164,72 H444 L471,45 H735 L763,17 H1014 M638,70 H886 L928,28 H1200", trace, .42);
        Trace("M186,63 H212 M501,45 H526 M598,11 l3,-3 3,3 -3,3 Z M665,70 H692 M807,17 H831 M1049,28 H1077", signal, .46, moves: true);
    }

    private void BuildFacets(bool bordeaux)
    {
        var tile = bordeaux ? BordeauxFacets.Value : ClassicFacets.Value;
        var viewport = new Canvas { Width = TileWidth, Height = TileHeight, ClipToBounds = true };
        var strip = new Canvas { Width = TileWidth, Height = TileHeight * 2, RenderTransform = drift };
        // Both copies share one texture. Moving the strip by a tile yields the same frame at the loop boundary.
        var first = new Image { Source = tile, Width = TileWidth, Height = TileHeight };
        var second = new Image { Source = tile, Width = TileWidth, Height = TileHeight };
        Canvas.SetTop(second, TileHeight);
        strip.Children.Add(first);
        strip.Children.Add(second);
        viewport.Children.Add(strip);
        Children.Add(new Viewbox { Stretch = Stretch.Fill, Child = viewport });
    }

    private static BitmapSource RenderFacets(bool bordeaux)
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var lip = EditorCanvas.Brush(bordeaux ? "#25B78EA8" : "#2695B5D0");
            var side = EditorCanvas.Brush(bordeaux ? "#1D846481" : "#1D6187A9");
            var shade = EditorCanvas.Brush(bordeaux ? "#360E101C" : "#36101723");
            var edge = new Pen(EditorCanvas.Brush(bordeaux ? "#1BCEA2B9" : "#1BACC8E0"), .8);
            // Each prism shares a raised corner across three faces, with light from the upper left.
            drawing.PushOpacity(.82);
            drawing.DrawGeometry(lip, edge, Geometry.Parse("M112,63 L237,22 320,101 217,68 Z M24,182 L135,146 224,217 116,190 Z M140,346 L257,303 320,379 232,347 Z M8,396 L68,372 112,412 56,400 Z"));
            drawing.DrawGeometry(side, null, Geometry.Parse("M112,63 L217,68 239,151 Z M24,182 L116,190 140,270 Z M140,346 L232,347 254,438 Z M8,396 L56,400 69,445 Z"));
            drawing.DrawGeometry(shade, null, Geometry.Parse("M217,68 L320,101 239,151 Z M116,190 L224,217 140,270 Z M232,347 L320,379 254,438 Z M56,400 L112,412 69,445 Z"));
            drawing.Pop();
        }
        // Rasterize fixed geometry once per theme. Resizing and scrolling only transform the cached image.
        var tile = new RenderTargetBitmap(TileWidth * 3 / 2, TileHeight * 3 / 2, 144, 144, PixelFormats.Pbgra32);
        tile.Render(visual);
        tile.Freeze();
        return tile;
    }

    private void WindowStateChanged(object? sender, EventArgs e) => RefreshMotion();
    private void RefreshMotion() => SetMotion(IsLoaded && IsVisible && owner?.WindowState != WindowState.Minimized &&
        (bool)GetValue(MotionAllowedProperty));

    private void SetMotion(bool animate)
    {
        if (animate == IsAnimating) return;
        motionClock = animate ? (AnimationClock)animation.CreateClock(true) : null;
        drift.ApplyAnimationClock(axis, motionClock);
    }

    internal void PauseForPan(bool paused)
    {
        if (motionClock?.Controller is not { } controller) return;
        if (paused) controller.Pause();
        else controller.Resume();
    }
}

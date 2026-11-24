// Coat — Epicteta Lupercal
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Coat;

internal enum FeedbackCue { Vertex, Edge, Face, Marquee, Move, DrawPoint, DrawClosed, Rectangle, Circle, Stairs, Extrusion }

internal sealed class InteractionFeedbackLayer : FrameworkElement
{
    internal static readonly DependencyProperty MotionAllowedProperty = DependencyProperty.Register(
        "MotionAllowed", typeof(bool), typeof(InteractionFeedbackLayer),
        new PropertyMetadata(false, (element, args) => { if (!(bool)args.NewValue) ((InteractionFeedbackLayer)element).Clear(); }));

    private const int MaximumEffects = 12;
    private readonly DrawingVisual visual = new();
    private readonly List<Pulse> effects = new();
    private readonly DispatcherTimer expiryTimer = new(DispatcherPriority.Background);
    private sealed record Pulse(FeedbackCue Cue, long Start, double Duration, Func<bool> Visible, Action<DrawingContext, double> Draw);

    internal InteractionFeedbackLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        AddVisualChild(visual);
        expiryTimer.Tick += ExpireEffects;
        SetResourceReference(MotionAllowedProperty, SystemParameters.ClientAreaAnimationKey);
        IsVisibleChanged += (_, _) => { if (!IsVisible) Clear(); };
        Unloaded += (_, _) => Clear();
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => index == 0 ? visual : throw new ArgumentOutOfRangeException(nameof(index));
    internal bool CanPlay => IsVisible && (bool)GetValue(MotionAllowedProperty);
    internal bool IsAnimating { get; private set; }
    internal int RenderPasses { get; private set; }
    internal IEnumerable<FeedbackCue> ActiveCues => effects.Select(effect => effect.Cue);

    internal void Add(FeedbackCue cue, double duration, Func<bool> visible, Action<DrawingContext, double> draw)
    {
        if (!CanPlay) return;
        if (effects.Count == MaximumEffects) effects.RemoveAt(0);
        long now = Stopwatch.GetTimestamp();
        effects.Add(new(cue, now, duration, visible, draw));
        ScheduleExpiry(now);
        if (IsAnimating) return;
        IsAnimating = true;
        CompositionTarget.Rendering += OnFrame;
    }

    internal void Clear()
    {
        if (!IsAnimating) return;
        effects.Clear();
        Stop();
        visual.RenderOpen().Close();
    }

    private void Stop()
    {
        expiryTimer.Stop();
        CompositionTarget.Rendering -= OnFrame;
        IsAnimating = false;
    }

    private void ScheduleExpiry(long now)
    {
        expiryTimer.Stop();
        // WPF can pause Rendering when its window is occluded or the desktop is idle.
        double remaining = effects.Max(effect => effect.Duration - Stopwatch.GetElapsedTime(effect.Start, now).TotalMilliseconds);
        expiryTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, remaining));
        expiryTimer.Start();
    }

    private void ExpireEffects(object? sender, EventArgs args)
    {
        long now = Stopwatch.GetTimestamp();
        DrawFrame(now);
        if (IsAnimating) ScheduleExpiry(now);
    }

    private void OnFrame(object? sender, EventArgs e) => DrawFrame(Stopwatch.GetTimestamp());

    internal void DrawFrame(long now)
    {
        effects.RemoveAll(effect => Stopwatch.GetElapsedTime(effect.Start, now).TotalMilliseconds >= effect.Duration || !effect.Visible());
        using var drawing = visual.RenderOpen();
        RenderPasses++;
        if (effects.Count == 0) { Stop(); return; }
        drawing.PushClip(new RectangleGeometry(new Rect(28, 24, Math.Max(0, ActualWidth - 28), Math.Max(0, ActualHeight - 24))));
        foreach (var effect in effects) effect.Draw(drawing, Stopwatch.GetElapsedTime(effect.Start, now).TotalMilliseconds / effect.Duration);
        drawing.Pop();
    }
}

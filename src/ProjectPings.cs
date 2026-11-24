// Coat — Epicteta Lupercal
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Coat;

public enum PingKind { Landmark, Review, Test }

public partial class MainWindow
{
    internal bool ArmProjectPing(PingKind kind)
    {
        if (rts == null) return false;
        rts.ArmPing(kind);
        return true;
    }
}

internal sealed partial class RtsHud
{
    internal sealed record Ping(PingKind Kind, V2 Position, int Level, string Color, string Label, DateTimeOffset Expiry,
        int? NoteId = null, long? PulseStarted = null);
    private const double PulseMilliseconds = 720;
    private static readonly DependencyProperty PingPulseClockProperty = DependencyProperty.Register("PingPulseClock", typeof(double), typeof(RtsHud),
        new PropertyMetadata(0d, (element, _) => ((RtsHud)element).RefreshPings()));
    private readonly List<Ping> live = new();
    private Annotation[] pinned = Array.Empty<Annotation>();
    private bool drewScenePings;
    private readonly DispatcherTimer pingExpiry = new(DispatcherPriority.Background);
    private readonly Dictionary<PingKind, Button> pingButtons = new();
    internal IReadOnlyList<Ping> LivePings => live;
    internal PingKind? PendingPing { get; private set; }
    internal static readonly TimeSpan PingLifetime = TimeSpan.FromSeconds(10);

    private static (string Label, string Glyph, string Color) PingStyle(PingKind kind) => kind switch
    {
        PingKind.Review => ("À revoir", "!", "#FFB767"),
        PingKind.Test => ("À tester", "✓", "#F28FCC"),
        _ => ("Repère", "◆", "#70EBD6")
    };

    private void InitializePings()
    {
        CachePinnedPings();
        pingExpiry.Tick += (_, _) => ExpirePings(DateTimeOffset.UtcNow);
        Unloaded += (_, _) => { pingExpiry.Stop(); BeginAnimation(PingPulseClockProperty, null); };
    }

    private FrameworkElement PingButtons()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var kind in Enum.GetValues<PingKind>())
        {
            var (label, glyph, color) = PingStyle(kind);
            var button = HudButton(glyph, label, (_, _) => ArmPing(kind));
            ((TextBlock)button.Content).Foreground = EditorCanvas.Brush(color);
            ToolTipService.SetShowDuration(button, 6000);
            button.ToolTip = L.T(label) + " · " + L.T("Cliquez dans le plan ou la minicarte.");
            pingButtons.Add(kind, button);
            row.Children.Add(button);
        }
        return row;
    }

    internal void ArmPing(PingKind kind)
    {
        Host.Canvas.CancelGesture();
        PendingPing = kind;
        UpdatePingButtons();
        Host.UpdateStatus();
        Host.Canvas.Focus();
    }

    internal bool CancelPing()
    {
        if (PendingPing == null) return false;
        PendingPing = null;
        UpdatePingButtons();
        Host.UpdateStatus();
        return true;
    }

    private void UpdatePingButtons()
    {
        foreach (var (kind, button) in pingButtons)
            button.Background = Host.ChromeBrush(kind == PendingPing ? "#39595C" : "#263241");
    }

    internal void RefreshPings(bool cameraOnly = false)
    {
        bool hasScenePings = live.Count + pinned.Length != 0;
        if (hasScenePings || drewScenePings) scenePings.InvalidateVisual();
        drewScenePings = hasScenePings;
        if (cameraOnly) Map.CameraMoved(); else Map.RefreshCamera();
    }

    private void CachePinnedPings() => pinned = Host.Document.Annotations.Where(note => note.Ping.HasValue).ToArray();

    private void ResetPings()
    {
        live.Clear();
        BeginAnimation(PingPulseClockProperty, null);
        pingExpiry.Stop();
        CancelPing();
        RefreshPings();
    }

    internal void ExpirePings(DateTimeOffset now)
    {
        pingExpiry.Stop();
        if (live.RemoveAll(p => p.Expiry <= now) > 0) RefreshPings();
        if (live.Count == 0) return;
        pingExpiry.Interval = TimeSpan.FromMilliseconds(Math.Max(1, (live.Min(p => p.Expiry) - now).TotalMilliseconds));
        pingExpiry.Start();
    }

    internal IEnumerable<Ping> VisiblePings()
    {
        foreach (var ping in live)
            if (Host.Canvas.MapShowsLevel(ping.Level)) yield return ping;
        foreach (var note in pinned.Where(Host.Canvas.MapShowsAnnotation))
            yield return new Ping(note.Ping!.Value, note.Position, note.Level, note.Color, note.Text, DateTimeOffset.MaxValue, note.Id);
    }

    internal bool HandleSceneClick(V2 world, Point screen, bool ctrl) => UsePingTarget(screen, Host.Canvas.Screen, world, ctrl);

    internal bool UsePingTarget(Point screen, Func<V2, Point> project, V2 world, bool ctrl)
    {
        if (PendingPing is { } kind)
        {
            CancelPing();
            if (!SelectionTransform.Valid(world)) return true;
            var (label, _, color) = PingStyle(kind);
            var now = DateTimeOffset.UtcNow;
            if (live.Count == 24) live.RemoveAt(0);
            bool animate = SystemParameters.ClientAreaAnimation;
            live.Add(new Ping(kind, world, Host.Document.ActiveLevel, color, L.T(label), now + PingLifetime,
                PulseStarted: animate ? Stopwatch.GetTimestamp() : null));
            if (animate)
                BeginAnimation(PingPulseClockProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(PulseMilliseconds)) { FillBehavior = FillBehavior.Stop });
            ExpirePings(now);
            RefreshPings();
            Host.PlayEditorSound(EditorSoundCue.Ping);
            return true;
        }
        if (ctrl || FindPing(screen, project) is not { } hit) return false;
        CenterOn(hit.Position);
        return true;
    }

    private Ping? FindPing(Point screen, Func<V2, Point> project) => VisiblePings()
        .Reverse().FirstOrDefault(ping => (project(ping.Position) - screen).Length <= 10);

    internal bool OpenScenePingMenu(Point point) => OpenPingMenu(Host.Canvas, point, Host.Canvas.Screen);

    internal bool OpenPingMenu(FrameworkElement target, Point point, Func<V2, Point> project)
    {
        if (FindPing(point, project) is not { } ping) return false;
        var menu = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.MousePoint };
        void Add(string label, Action run)
        {
            var item = new MenuItem { Header = L.T(label) };
            item.Click += (_, _) => run();
            menu.Items.Add(item);
        }
        Add("Centrer sur le ping", () => CenterOn(ping.Position));
        if (ping.NoteId == null) Add("Épingler", () => Pin(ping));
        else
        {
            Add("Modifier le marqueur…", () => EditPin(ping.NoteId.Value));
            Add("Supprimer le marqueur", () => Host.Mutate(() => Host.Document.Annotations.RemoveAll(n => n.Id == ping.NoteId)));
        }
        menu.IsOpen = true;
        return true;
    }

    internal Annotation? Pin(Ping ping)
    {
        if (Host.Document.Annotations.Count >= 5000 || !live.Remove(ping)) return null;
        var note = new Annotation
        {
            Id = Host.Document.NextId, Level = ping.Level, X = ping.Position.X, Y = ping.Position.Y,
            Size = 14, Text = ping.Label, Color = ping.Color, Ping = ping.Kind
        };
        Host.Mutate(() => { Host.Document.NextId++; Host.Document.Annotations.Add(note); }, createsObjects: true);
        ExpirePings(DateTimeOffset.UtcNow);
        RefreshPings();
        return note;
    }

    private void EditPin(int id)
    {
        if (Host.Document.Annotations.FirstOrDefault(n => n.Id == id) is not { } note) return;
        var dialog = new Window
        {
            Owner = Host, Title = L.T("Modifier le marqueur…"), Width = 325, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Host.ChromeBrush("#151F2B"), Foreground = Host.ChromeBrush("#DCE4ED")
        };
        var content = new StackPanel { Margin = new Thickness(14) };
        dialog.Content = content;
        var label = new TextBox { Text = note.Text, MaxLength = 120, Margin = new Thickness(0, 3, 0, 10) };
        var color = new TextBox { Text = note.Color, MaxLength = 9, Margin = new Thickness(0, 3, 0, 10) };
        content.Children.Add(new TextBlock { Text = L.T("Libellé") }); content.Children.Add(label);
        content.Children.Add(new TextBlock { Text = L.T("Couleur (Hex)") }); content.Children.Add(color);
        var confirm = new Button { Content = L.T("Confirmer"), IsDefault = true };
        confirm.Click += (_, _) =>
        {
            if (!ColorMath.TryHex(color.Text, out var parsed)) { color.Focus(); color.SelectAll(); return; }
            Host.Mutate(() => { note.Text = label.Text.Trim(); note.Color = ColorMath.Hex(parsed); });
            dialog.Close();
        };
        content.Children.Add(confirm);
        dialog.ShowDialog();
    }

    internal static void DrawPing(DrawingContext dc, Point center, string color, PingKind kind, double radius, bool pinned)
    {
        Brush ink = EditorCanvas.Brush(color);
        dc.DrawEllipse(EditorCanvas.Brush("#182534"), new Pen(ink, pinned ? 2 : 1), center, radius + 2, radius + 2);
        var pen = new Pen(ink, Math.Max(1, radius / 3));
        switch (kind)
        {
            case PingKind.Review:
                dc.DrawLine(pen, center + new Vector(0, -radius * .65), center);
                dc.DrawEllipse(ink, null, center + new Vector(0, radius * .48), 1, 1);
                break;
            case PingKind.Test:
                var corner = center + new Vector(-radius * .2, radius * .45);
                dc.DrawLine(pen, center + new Vector(-radius * .58, 0), corner);
                dc.DrawLine(pen, corner, center + new Vector(radius * .6, -radius * .55));
                break;
            default:
                dc.DrawEllipse(ink, null, center, radius * .55, radius * .55);
                break;
        }
    }

    internal static void DrawPingHalo(DrawingContext dc, Point center, Ping ping, double maximumRadius, long now)
    {
        if (ping.PulseStarted is not { } start) return;
        double progress = Stopwatch.GetElapsedTime(start, now).TotalMilliseconds / PulseMilliseconds;
        if (progress is < 0 or >= 1) return;
        double radius = maximumRadius * (.26 + .74 * (1 - Math.Pow(1 - progress, 2)));
        double opacity = 1 - Math.Pow(progress, 1.35);
        Brush ink = EditorCanvas.Brush(ping.Color);
        dc.PushOpacity(.24 * opacity);
        dc.DrawEllipse(null, new Pen(ink, maximumRadius * .09), center, radius, radius);
        dc.Pop();
        dc.PushOpacity(.91 * opacity);
        dc.DrawEllipse(null, new Pen(ink, maximumRadius * .033), center, radius, radius);
        dc.Pop();
    }

    private sealed class ScenePingLayer(RtsHud hud) : FrameworkElement
    {
        protected override HitTestResult? HitTestCore(PointHitTestParameters parameters) => null;
        protected override void OnRender(DrawingContext dc)
        {
            long now = Stopwatch.GetTimestamp();
            foreach (var ping in hud.VisiblePings())
            {
                var center = hud.Host.Canvas.Screen(ping.Position);
                DrawPingHalo(dc, center, ping, 70, now);
                DrawPing(dc, center, ping.Color, ping.Kind, 8, ping.NoteId != null);
            }
        }
    }
}

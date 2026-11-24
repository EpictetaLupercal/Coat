// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Coat;

public partial class MainWindow
{
    private RtsHud? rts;
    internal RtsHud? Rts => rts;

    internal void ShowLevelCommands(FrameworkElement anchor)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = System.Windows.Controls.Primitives.PlacementMode.Top };
        foreach (int level in Document.Levels)
        {
            var item = new MenuItem { Header = L.F($"Niveau {level}") };
            item.Click += (_, _) => AssignSelectionToLevel(level);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }
}

internal sealed partial class RtsHud : Grid
{
    internal MainWindow Host { get; }
    internal Minimap Map { get; }
    private readonly ProgressGauge gauge;
    private readonly TextBlock reward;
    private readonly StackPanel commandBar;
    private readonly Grid mapArea;
    private readonly Button mapToggle;
    private readonly Button duplicate, layers, focus, confirm, fit;
    private readonly Button quest;
    private readonly ScenePingLayer scenePings;
    private (V2 Center, double Zoom, Size Size, (int, bool, bool, object?) Visibility)? cameraState;
    internal bool IsMapCollapsed { get; private set; }
    internal FrameworkElement GaugeElement => gauge;

    internal RtsHud(MainWindow host, bool collapsed)
    {
        Host = host;
        ClipToBounds = true;
        scenePings = new ScenePingLayer(this);
        Children.Add(scenePings);
        InitializePings();

        Map = new Minimap(this) { VerticalAlignment = VerticalAlignment.Top };
        var mapPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(30, 0, 0, 84)
        };
        Children.Add(mapPanel);
        mapArea = new Grid { Width = Minimap.Size, Height = Minimap.Size, ClipToBounds = true };
        mapArea.Children.Add(Map);
        mapToggle = HudButton("◆", "Réduire la minicarte", (_, _) => SetMapCollapsed(!IsMapCollapsed));
        mapToggle.Width = 24;
        mapToggle.Height = 24;
        mapToggle.HorizontalAlignment = HorizontalAlignment.Center;
        mapToggle.VerticalAlignment = VerticalAlignment.Top;
        mapToggle.Margin = new Thickness(0, 0, 0, 0);
        var toggleText = (TextBlock)mapToggle.Content;
        mapToggle.MouseEnter += (_, _) => toggleText.Foreground = EditorCanvas.Brush("#FF626E");
        mapToggle.MouseLeave += (_, _) => toggleText.Foreground = Host.ChromeBrush("#78DCC1");
        mapArea.Children.Add(mapToggle);
        mapPanel.Children.Add(mapArea);
        mapPanel.Children.Add(PingButtons());
        SetMapCollapsed(collapsed);
        SizeChanged += (_, _) => ObserveCamera();

        var center = new StackPanel
        {
            Width = 182, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 12)
        };
        Children.Add(center);
        reward = new TextBlock
        {
            Height = 26, Foreground = EditorCanvas.Brush("#D8FFAC"), FontWeight = FontWeights.Bold, Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false
        };
        center.Children.Add(reward);
        gauge = new ProgressGauge(host) { Height = 38 };
        center.Children.Add(gauge);
        commandBar = new StackPanel { Height = 32, Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        center.Children.Add(commandBar);
        duplicate = HudButton("▣", "Dupliquer", (_, _) => Host.Duplicate());
        layers = HudButton("≡", "Déplacer vers un niveau", (sender, _) => Host.ShowLevelCommands((FrameworkElement)sender));
        focus = HudButton("◎", "Mode Focus", (_, _) => Host.Canvas.ToggleSelectionFocus());
        confirm = HudButton("✓", "Confirmer", (_, _) => Host.Canvas.ConfirmGesture());
        fit = HudButton("⌖", "Cadrer le plan", (_, _) => Host.Canvas.Fit());
        quest = HudButton("⚑", "Quêtes du projet", ShowQuests);
        foreach (var button in new[] { duplicate, layers, focus, confirm, fit, quest }) commandBar.Children.Add(button);
        RefreshCommands();
        RefreshProgress();
    }

    private Button HudButton(string glyph, string title, RoutedEventHandler action)
    {
        var caption = new TextBlock { Text = glyph, FontSize = 15, Foreground = Host.ChromeBrush("#78DCC1") };
        var button = new Button { Content = caption, MinWidth = 30, Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(2), ToolTip = L.T(title) };
        AutomationProperties.SetName(button, L.T(title));
        button.Click += action;
        return button;
    }

    internal void SetMapCollapsed(bool collapsed)
    {
        IsMapCollapsed = collapsed;
        string name = L.T(collapsed ? "Rouvrir la minicarte" : "Réduire la minicarte");
        mapToggle.ToolTip = name;
        AutomationProperties.SetName(mapToggle, name);
        ((TextBlock)mapToggle.Content).Text = collapsed ? "◇" : "◆";

        double from = mapArea.Height;
        double target = collapsed ? mapToggle.Height : Minimap.Size;
        mapArea.BeginAnimation(HeightProperty, null);
        mapArea.Height = target;
        if (!IsLoaded || !SystemParameters.ClientAreaAnimation || from == target)
        {
            Map.Visibility = collapsed ? Visibility.Hidden : Visibility.Visible;
            Map.IsHitTestVisible = !collapsed;
            return;
        }

        Map.Visibility = Visibility.Visible;
        Map.IsHitTestVisible = false;
        var slide = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        slide.Completed += (_, _) =>
        {
            if (IsMapCollapsed != collapsed || mapArea.Height != target) return;
            Map.Visibility = collapsed ? Visibility.Hidden : Visibility.Visible;
            Map.IsHitTestVisible = !collapsed;
        };
        mapArea.BeginAnimation(HeightProperty, slide);
    }

    internal void RefreshCommands()
    {
        var canvas = Host.Canvas;
        bool selected = canvas.Tool == EditorTool.Select && (canvas.SelectedVertices.Count + canvas.SelectedEdges.Count +
            canvas.SelectedFaces.Count + canvas.SelectedAnnotations.Count > 0);
        duplicate.Visibility = layers.Visibility = focus.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        confirm.Visibility = canvas.Tool is EditorTool.Draw or EditorTool.Dimension or EditorTool.Shapes ? Visibility.Visible : Visibility.Collapsed;
        fit.Visibility = selected ? Visibility.Collapsed : Visibility.Visible;
    }

    internal void ObserveCamera()
    {
        var canvas = Host.Canvas;
        var current = (canvas.Center, canvas.Zoom, canvas.RenderSize, canvas.MapVisibility);
        if (cameraState == current) return;
        cameraState = current;
        RefreshPings(cameraOnly: true);
    }

    internal void InvalidateMapGeometry()
    {
        CachePinnedPings();
        Map.InvalidateGeometry();
        RefreshPings();
    }

    internal void ResetProject()
    {
        ResetPings();
        InvalidateMapGeometry();
        reward.BeginAnimation(OpacityProperty, null);
        reward.Opacity = 0;
        RefreshProgress();
    }

    internal void CenterOn(V2 world)
    {
        Host.Canvas.Center = world;
        Host.Canvas.InvalidateVisual();
        Host.UpdateStatus();
        ObserveCamera();
    }

    private void RefreshProgress()
    {
        var (level, earned, target) = Host.Progress.Gauge();
        gauge.ToolTip = L.F($"Niveau {level} · {earned / 100.0:0.##} / {target / 100.0:0.##} XP · {Host.Progress.TotalXp} XP au total");
        AutomationProperties.SetName(gauge, (string)gauge.ToolTip);
        gauge.InvalidateVisual();
    }

    internal void ShowReward(ProjectProgress previous, ProjectProgress next, bool milestone)
    {
        RefreshProgress();
        int gained = next.TotalXp - previous.TotalXp;
        if (gained <= 0) return;
        int level = next.Gauge().Level;
        bool levelUp = level > previous.Gauge().Level;
        reward.Text = levelUp ? L.F($"Niveau {level}  ·  +{gained} XP") : L.F($"+{gained} XP");
        reward.FontSize = levelUp || milestone ? 17 : 13;
        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milestone || levelUp ? 960 : 420))));
        var duration = TimeSpan.FromMilliseconds(milestone || levelUp ? 1500 : 760);
        if (SystemParameters.ClientAreaAnimation) fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(duration)));
        else fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(duration)));
        reward.BeginAnimation(OpacityProperty, fade);
    }

    private void ShowQuests(object sender, RoutedEventArgs args)
    {
        var progress = Host.Progress;
        var menu = new ContextMenu { PlacementTarget = quest, Placement = System.Windows.Controls.Primitives.PlacementMode.Top };
        menu.Items.Add(new MenuItem { Header = L.T("Première itération") + (progress.FirstIteration ? "  ✓" : "  ·  +40 XP"), IsEnabled = false });
        var test = new MenuItem
        {
            Header = L.T("Tester la circulation") + (progress.Playtest ? "  ✓" : "  ·  +100 XP"),
            IsEnabled = progress.FirstMap != null && !progress.Playtest
        };
        test.Click += (_, _) =>
        {
            if (AppDialog.Show(Host, L.T("Confirmer que vous avez testé un parcours dans la carte exportée ?"),
                L.T("Tester la circulation"), MessageBoxButton.YesNo) == MessageBoxResult.Yes) Host.ConfirmPlaytest();
        };
        menu.Items.Add(test);
        menu.IsOpen = true;
    }

    private sealed class ProgressGauge(MainWindow host) : FrameworkElement
    {
        private static readonly string[] Digits =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111"
        };

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
            var (level, earned, target) = host.Progress.Gauge();
            string text = level.ToString(System.Globalization.CultureInfo.InvariantCulture);
            const double pixel = 3;
            double left = Math.Round((ActualWidth - (4 * text.Length - 1) * pixel) / 2);
            var ink = EditorCanvas.Brush("#99E579");
            var outline = new Pen(EditorCanvas.Brush("#122116"), 2);
            for (int i = 0; i < text.Length; i++)
                for (int j = 0; j < 15; j++)
                    if (Digits[text[i] - '0'][j] == '1')
                        dc.DrawRectangle(null, outline, Pixel(i, j));
            for (int i = 0; i < text.Length; i++)
                for (int j = 0; j < 15; j++)
                    if (Digits[text[i] - '0'][j] == '1') dc.DrawRectangle(ink, null, Pixel(i, j));
            Rect Pixel(int i, int j) => new(left + (i * 4 + j % 3) * pixel, 7 + j / 3 * pixel, pixel, pixel);
            var gauge = new Rect(5, 28, Math.Max(0, ActualWidth - 10), 7);
            dc.PushOpacity(.60);
            dc.DrawRectangle(EditorCanvas.Brush("#15251C"), new Pen(host.ChromeBrush("#526678"), 1), gauge);
            dc.Pop();
            dc.PushOpacity(.76);
            if (earned > 0) dc.DrawRectangle(ink, null, new Rect(gauge.X, gauge.Y, gauge.Width * earned / target, gauge.Height));
            dc.Pop();
        }
    }
}

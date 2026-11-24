// Coat — Epicteta Lupercal
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Coat;

internal static class RtsTests
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var results = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            results.Add("PASS: " + name);
        }
        static bool Near(V2 a, V2 b) => (a - b).Length < 1e-6;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow { TestMode = true, ShowInTaskbar = false, Width = 1170, Height = 790 };
        window.Shortcuts.AnimatedMenus = false;
        window.ChangeLanguage("en");
        window.Show();
        var canvas = window.Canvas;
        var hud = window.Rts!;
        var map = hud.Map;
        var sounds = new List<EditorSoundCue>();
        window.EditorSoundRequested += sounds.Add;
        void Layout() => InterfaceLayoutTests.Layout(window);
        void Click(V2 point)
        {
            canvas.PointerDown(canvas.Screen(point), MouseButton.Left);
            canvas.PointerUp(MouseButton.Left);
        }
        void MapClick(V2 point)
        {
            var at = map.MapPoint(point);
            map.BeginPointer(at);
            map.EndPointer(at);
        }
        Point ProbeGauge() => hud.GaugeElement.TranslatePoint(new Point(), canvas);
        void TryFailure(Action action)
        {
            try { action(); }
            catch (IOException) { return; }
            throw new InvalidOperationException("Expected a write failure");
        }

        window.LoadDocument(new Plan(), "");
        Layout();
        Check(window.Progress.Gauge() == (1, 0, 7475) && ProjectProgress.NextLevelCost(0) == 5000,
            "A new project starts at level 1 with 0 XP and uses the specified level curve");
        byte AlphaAt(int x, int y)
        {
            var gauge = hud.GaugeElement;
            var rendered = new RenderTargetBitmap((int)gauge.ActualWidth, (int)gauge.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            var standalone = new DrawingVisual();
            using (var dc = standalone.RenderOpen()) dc.DrawDrawing(VisualTreeHelper.GetDrawing(gauge));
            rendered.Render(standalone);
            var pixel = new byte[4];
            rendered.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            return pixel[3];
        }
        int trackAlpha = AlphaAt(90, 31), digitAlpha = AlphaAt(90, 8);
        Check(trackAlpha is > 0 and < 220 && digitAlpha >= 245,
            $"The XP track is translucent and the level digit stays opaque (track {trackAlpha}, digit {digitAlpha})");
        hud.CenterOn(new V2(960, -640));
        var limits = Minimap.MapBounds;
        var mapMiddle = new Point(limits.Left + limits.Width / 2, limits.Top + limits.Height / 2);
        Check(Near(map.PlanPoint(mapMiddle), canvas.Center) && Minimap.OffMapCamera(map.MapPoint(canvas.Center)) == null,
            "An empty minimap stays centered on the camera without an off-map marker");
        var gaugeAt = ProbeGauge();
        var viewport = map.CameraWorldRect;
        int builds = map.GeometryBuilds;
        canvas.Zoom = 2;
        canvas.InvalidateVisual();
        Layout();
        Check(Math.Abs(map.CameraWorldRect.Width * 2 - viewport.Width) < 1e-6 && ProbeGauge() == gaugeAt &&
            map.GeometryBuilds == builds, "Zoom resizes the true viewport and keeps the HUD and cached geometry stable");

        const string legacy = """{"Version":6,"Grid":32,"Vertices":[],"Edges":[],"Fills":{}}""";
        var plan = Plan.Parse(legacy);
        plan.Polygon(new V2[] { new(-320,-160), new(320,-160), new(320,160), new(-320,160) });
        plan.Fills[plan.Faces().Single().Key] = "#A28555";
        plan.EnsureLevel(1);
        plan.ActiveLevel = 1;
        plan.Segment(new V2(140, 90), new V2(150, 90));
        int dimension = plan.AddEdge(plan.Point(new(2000, 0)), plan.Point(new(2200, 0)));
        plan.Edges.Single(edge => edge.Id == dimension).Dimension = true;
        plan.ActiveLevel = 0;
        // The fixtures mimic an existing project created before project progression.
        window.LoadDocument(plan, "");
        Layout();
        var centerMap = map.MapPoint(new(0, 0));
        var rightMap = map.MapPoint(new(320, 0));
        var mapOrigin = map.TranslatePoint(new Point(), canvas);
        Check(mapOrigin.X == 30 && mapOrigin.Y + map.ActualHeight < canvas.ActualHeight - 70,
            "The minimap anchors at the lower left above the grid scale and density indicator");
        var northMap = map.MapPoint(new(0, 160));
        Check(window.Progress.TotalXp == 0 && (centerMap - mapMiddle).Length < 1e-6 &&
            rightMap.Y == centerMap.Y && rightMap.X > centerMap.X && northMap.X == centerMap.X && northMap.Y < centerMap.Y,
            "Legacy project starts clean and the flat minimap follows the plan axes without framing the distant dimension");
        canvas.Zoom = 2;
        int topologyBuilds = map.GeometryBuilds, rasters = map.RasterBuilds;
        window.ToggleUpperLevels();
        Layout();
        Check(map.MapPoint(new(0, 0)) == centerMap && map.GeometryBuilds == topologyBuilds && map.RasterBuilds == rasters,
            "The flat topology keeps its extent and cached bitmap when editor level visibility changes");
        window.ToggleUpperLevels();
        Layout();
        double zoom = canvas.Zoom;
        hud.CenterOn(new(1500, 0));
        var rightMarker = Minimap.OffMapCamera(map.MapPoint(canvas.Center));
        Check(rightMarker is { } east && east.Position.X == limits.Right - 5 && east.Direction.X == 1 && east.Direction.Y == 0,
            "An off-map camera to the east is indicated at the right edge");
        hud.CenterOn(new(-1500, -500));
        var bottomLeftMarker = Minimap.OffMapCamera(map.MapPoint(canvas.Center));
        Check(bottomLeftMarker is { } southwest && southwest.Position.X == limits.Left + 5 &&
            southwest.Direction.X < 0 && southwest.Direction.Y > 0, "The edge indicator also points towards a diagonal off-map camera");
        hud.CenterOn(new(0, 1500));
        var topMarker = Minimap.OffMapCamera(map.MapPoint(canvas.Center));
        Check(topMarker is { } north && north.Position.Y == limits.Top + 5 && north.Direction.Y == -1,
            "The north indicator appears below the fold handle at the top edge");
        MapClick(new(225, 45));
        Check(Near(canvas.Center, new(225, 45)) && canvas.Zoom == zoom && Minimap.OffMapCamera(map.MapPoint(canvas.Center)) == null,
            "Clicking the minimap recenters the camera, removes the edge indicator and preserves zoom");
        var beforeDrag = canvas.Center;
        var grab = map.MapPoint(beforeDrag + new V2(35, 25));
        var destination = map.MapPoint(beforeDrag + new V2(155, -45));
        map.BeginPointer(grab);
        Check(canvas.Center == beforeDrag, "Pressing the camera frame preserves its grab offset");
        map.MovePointer(destination);
        map.EndPointer(destination);
        Check(Near(canvas.Center, beforeDrag + new V2(120, -70)) && canvas.Zoom == zoom && ProbeGauge() == gaugeAt &&
            map.GeometryBuilds == topologyBuilds && map.RasterBuilds == rasters,
            "Dragging keeps its offset and moves only the overlay over the cached topology");
        var beforePan = canvas.Center;
        var grabPoint = new Point(440, 330);
        int planCache = canvas.RenderCacheBuilds;
        canvas.PointerDown(grabPoint, MouseButton.Right);
        canvas.PointerMove(grabPoint + new Vector(30, -18));
        canvas.PointerMove(grabPoint + new Vector(72, -36));
        Layout();
        canvas.PointerUp(MouseButton.Right);
        Check(Near(canvas.Center, beforePan + new V2(-72 / zoom, -36 / zoom)) &&
            canvas.RenderCacheBuilds == planCache && map.RasterBuilds == rasters,
            "The right-button pan applies the latest pointer position without rebuilding either scene topology");
        var toggle = InterfaceLayoutTests.Children<Button>(hud).Single(b => AutomationProperties.GetName(b) == "Collapse minimap");
        var reviewButton = InterfaceLayoutTests.Children<Button>(hud).Single(b => AutomationProperties.GetName(b) == "Review");
        var openHandle = toggle.TranslatePoint(new Point(), canvas);
        var pingRow = reviewButton.TranslatePoint(new Point(), canvas);
        void FinishMapSlide()
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
            Layout();
        }
        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        FinishMapSlide();
        var closedHandle = toggle.TranslatePoint(new Point(), canvas);
        Check(hud.IsMapCollapsed && toggle.IsVisible && map.Visibility == Visibility.Hidden && !map.IsHitTestVisible,
            "The minimap folds with its handle accessible and the map excluded from pointer input");
        Check(closedHandle.X == openHandle.X && closedHandle.Y > openHandle.Y + 150 &&
            pingRow == reviewButton.TranslatePoint(new Point(), canvas) && pingRow.Y - closedHandle.Y < 32,
            "The handle slides down to the stationary ping row when the map folds");
        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        FinishMapSlide();
        Check(!hud.IsMapCollapsed && map.IsHitTestVisible && toggle.TranslatePoint(new Point(), canvas) == openHandle,
            "The same handle restores the minimap and its original position");

        window.SetTool(EditorTool.Draw);
        window.ExecuteFeature("Ping : à revoir");
        var pingPosition = canvas.Center + new V2(-64, 32);
        var camera = canvas.Center;
        Click(pingPosition);
        var review = hud.LivePings.Single();
        Check(Near(review.Position, pingPosition) && Near(map.PlanPoint(map.MapPoint(review.Position)), review.Position) &&
            camera == canvas.Center && canvas.Tool == EditorTool.Draw && canvas.Anchor == null && hud.PendingPing == null &&
            review.Kind == PingKind.Review && RtsHud.PingLifetime.TotalSeconds == 10,
            "Scene ping is aligned in both views and does not pan or consume the drawing tool");
        Check(sounds.SequenceEqual(new[] { EditorSoundCue.Ping }), "Placing a ping requests the ping sound once");
        Check(review.PulseStarted.HasValue == SystemParameters.ClientAreaAnimation, "Placed pings follow the Windows animation preference");
        long pulseStart = Stopwatch.GetTimestamp();
        var pulse = review with { PulseStarted = pulseStart };
        Rect HaloBounds(double radius, int milliseconds)
        {
            var drawing = new DrawingGroup();
            using (var dc = drawing.Open())
                RtsHud.DrawPingHalo(dc, new Point(0, 0), pulse, radius, pulseStart + milliseconds * Stopwatch.Frequency / 1000);
            return drawing.Bounds;
        }
        Check(HaloBounds(70, 420).Width > 110 && HaloBounds(70, 420).Width > HaloBounds(70, 80).Width + 45 &&
            HaloBounds(24, 420).Width > 36 && HaloBounds(70, 760).IsEmpty,
            "The ping halo expands on both display scales then disappears while the marker is still active");
        MapClick(review.Position);
        Check(Near(canvas.Center, review.Position), "Clicking a placed ping centers the camera");
        hud.ArmPing(PingKind.Test);
        var secondPosition = review.Position + new V2(84, 16);
        MapClick(secondPosition);
        var testPing = hud.LivePings.Single(p => p.Kind == PingKind.Test);
        Check(Near(testPing.Position, secondPosition) && Near(canvas.Center, review.Position), "Placing a ping on the minimap preserves the camera");
        hud.ArmPing(PingKind.Landmark);
        window.Escape();
        Check(hud.PendingPing == null && canvas.Tool == EditorTool.Draw && hud.LivePings.Count == 2, "Escape cancels only ping placement");
        var marker = hud.Pin(review)!;
        marker.Text = "West approach";
        marker.Color = "#BDE973";
        hud.ExpirePings(testPing.Expiry);
        Check(hud.LivePings.Count == 0 && hud.VisiblePings().Single().NoteId == marker.Id &&
            Plan.Parse(window.Document.Serialize()).Annotations.Single().Text == marker.Text,
            "Transient pings time out and a pinned marker persists as an editable Annotation");
        Check(File.Exists(EditorSoundLibrary.DefaultFile("ping.wav")) &&
            new ShortcutSettings().Bindings.Where(p => p.Key.StartsWith("Ping :", StringComparison.Ordinal)).Count(p => p.Value.Key == Key.None) == 3,
            "A bundled ping sound and three unassigned shortcut commands are available");

        window.LoadDocument(new Plan(), "");
        window.SetTool(EditorTool.Shapes);
        canvas.Shape = ShapeKind.Rectangle;
        Click(new(-160, -96));
        Check(window.Progress.TotalXp == 0, "A shape preview awards no XP");
        Click(new(160, 96));
        Check(window.Progress.CreationXp == 1 && window.Document.Faces().Count == 1, "One completed rectangle awards one XP");
        window.Undo();
        window.Redo();
        Check(window.Progress.CreationXp == 1 && window.Document.Faces().Count == 1, "Undo and redo preserve XP without replaying rewards");
        window.SetTool(EditorTool.Paint);
        canvas.PaintLines = false;
        canvas.PaintColor = "#80808A";
        Click(new(0, 0));
        var project = Path.Combine(output, "progress.coat");
        var missing = Path.Combine(output, "missing-folder", "failed.coat");
        TryFailure(() => window.SaveProjectTo(missing));
        Check(!window.Progress.FirstSave && window.Progress.TotalXp == 1, "A failed first save does not award XP");
        window.SaveProjectTo(project);
        Check(window.Progress.TotalXp == 21, "First successful save awards twenty XP");
        Layout();
        Check(AlphaAt(25, 31) is > 180 and < 255, "The filled XP gauge also lets the plan show through");
        var result = MapExporter.Export(window.Document);
        TryFailure(() => window.WriteProjectMap(Path.ChangeExtension(missing, ".map"), result));
        Check(window.Progress.TotalXp == 21 && window.Progress.FirstMap == null, "A failed map write does not award XP");
        window.WriteProjectMap(Path.Combine(output, "first.map"), result);
        Check(window.Progress.TotalXp == 101 && window.Progress.Gauge().Level >= 2 && Plan.Parse(File.ReadAllText(project)).Progression == window.Progress,
            "A successful first map reaches level 2 or higher and persists the milestone");
        window.LoadDocument(Plan.Parse(File.ReadAllText(project)), project);
        window.WriteProjectMap(Path.Combine(output, "repeat.map"), MapExporter.Export(window.Document));
        Check(window.Progress.TotalXp == 101, "Reopening and exporting the same geometry to a different file awards no XP");
        window.Mutate(() => window.Document.Fills[window.Document.Faces().Single().Key] = "#A010A0");
        var recolored = MapExporter.Export(window.Document);
        Check(result.GeometryFingerprint == recolored.GeometryFingerprint && result.Text != recolored.Text,
            "Geometry fingerprints ignore color metadata when brush geometry is identical");
        window.WriteProjectMap(Path.Combine(output, "recolored.map"), recolored);
        Check(window.Progress.TotalXp == 101, "A metadata-only export does not complete the iteration quest");
        window.Mutate(() => { foreach (var vertex in window.Document.Vertices) vertex.X += 32; });
        window.WriteProjectMap(Path.Combine(output, "iteration.map"), MapExporter.Export(window.Document));
        Check(window.Progress.TotalXp == 141 && window.Progress.FirstIteration,
            "A changed geometry completes the iteration quest");
        Check(window.ConfirmPlaytest(), "A manually confirmed playtest becomes eligible after export");
        Check(window.Progress.TotalXp == 241 && !window.ConfirmPlaytest() &&
            Plan.Parse(File.ReadAllText(project)).Progression == window.Progress &&
            Plan.Parse(File.ReadAllText(project)).Vertices.Min(v => v.X) == -160,
            "Playtest pays once and saving milestones leaves unsaved geometry out of the project file");
        for (int i = 0; i < 22; i++)
            window.Mutate(() => window.Document.Annotations.Add(new Annotation { Id = window.Document.NextId++, X = i }), createsObjects: true);
        Check(window.Progress.CreationXp == 20 && window.Progress.TotalXp == 260, "Creation rewards stop at the per-project twenty XP cap");

        window.LoadDocument(plan, "");
        window.SetTool(EditorTool.Select);
        canvas.Center = new(0, 0);
        canvas.Zoom = 1.5;
        hud.ArmPing(PingKind.Landmark);
        MapClick(new(155, 60));
        Layout();
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-1.6-hud.png"));
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        window.ChangeLanguage("fr");
        Layout();
        hud = window.Rts!;
        map = hud.Map;
        var gaugeBounds = hud.GaugeElement.TransformToVisual(canvas).TransformBounds(new Rect(hud.GaugeElement.RenderSize));
        var mapBounds = map.TransformToVisual(canvas).TransformBounds(new Rect(map.RenderSize));
        Check(!gaugeBounds.IntersectsWith(mapBounds) && InterfaceLayoutTests.Children<Button>(hud).Any(b => AutomationProperties.GetName(b) == "Réduire la minicarte"),
            "At the minimum window size the localized minimap and fixed gauge remain separate");
        window.Shortcuts.AnimatedMenus = true;
        window.ChangeLanguage("en");
        Layout();
        var menus = InterfaceLayoutTests.Children<MenuDecoration>(window).ToArray();
        foreach (var menu in menus) menu.SetValue(MenuDecoration.MotionAllowedProperty, true);
        FinishMapSlide();
        canvas.PointerDown(grabPoint, MouseButton.Right);
        canvas.PointerMove(grabPoint + new Vector(80, 20));
        FinishMapSlide();
        var pausedOffsets = menus.Select(menu => menu.Offset).ToArray();
        FinishMapSlide();
        Check(menus.Length == 2 && menus.Select(menu => menu.Offset).SequenceEqual(pausedOffsets),
            "Right-button panning holds both decorative animations at their current positions");
        canvas.PointerUp(MouseButton.Right);
        FinishMapSlide();
        Check(menus.Where((menu, index) => Math.Abs(menu.Offset - pausedOffsets[index]) > .001).Count() == 2,
            "Both decorative animations resume when panning ends");
        window.Close();
        app.Shutdown();
        File.WriteAllLines(Path.Combine(output, "results.txt"), results.Append($"\n{results.Count} checks passed."));
    }
}

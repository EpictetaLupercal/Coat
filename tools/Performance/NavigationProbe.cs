// Coat — Epicteta Lupercal
using Coat;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

internal static class NavigationProbe
{
    private sealed record Sample(bool Map, bool Pointer, double Start, double End, int Updates, double Median, double P95, int BrushRebuilds)
    {
        public string? Renderer { get; set; }
        public int IntervalMs { get; set; }
        public int GlFrames { get; set; }
        public int SceneRecordings { get; set; }
        public int TopologyRebuilds { get; set; }
        public double? DisplayedFps { get; set; }
    }

    internal static void Run(Plan plan, string output, string? presentMon, bool staticMenus, bool imagesOnly, bool zoom)
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow { TestMode = true, ShowInTaskbar = false, ShowActivated = false, Width = 1440, Height = 900 };
        if (staticMenus)
        {
            window.Shortcuts.AnimatedMenus = false;
            typeof(MainWindow).GetMethod("ChangeLanguage", Private)!.Invoke(window, new object[] { "en" });
        }
        window.Show();
        window.LoadDocument(plan, "");
        window.SetTool(EditorTool.Select);
        var canvas = window.Canvas;
        var root = typeof(MainWindow).GetProperty("Rts", Private)!.GetValue(window)!;
        var map = (UIElement)root.GetType().GetProperty("Map", Private)!.GetValue(root)!;
        var move = typeof(EditorCanvas).GetMethod("PointerMove", Private)!.CreateDelegate<Action<Point, bool>>(canvas);
        var down = typeof(EditorCanvas).GetMethod("PointerDown", Private)!.CreateDelegate<Action<Point, MouseButton, int, bool, bool, bool>>(canvas);
        var up = typeof(EditorCanvas).GetMethod("PointerUp", Private)!.CreateDelegate<Action<MouseButton>>(canvas);
        var brush = typeof(EditorCanvas).GetField("navigationBrush", Private)!;
        var wheel = typeof(EditorCanvas).GetMethod("ZoomAt", Private)!.CreateDelegate<Action<Point, int>>(canvas);
        var center = canvas.Center;
        double initialZoom = canvas.Zoom;
        var origin = new Point(410, 310);
        var samples = new List<Sample>();
        string session = $"Coat-Navigation-{Environment.ProcessId}";
        string trace = Path.Combine(output, "displayed.csv");
        Process? capture = presentMon == null ? null : Start(presentMon, "--session_name", session, "--process_id",
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture), "--output_file", trace, "--no_console_stats",
            "--exclude_dropped", "--no_track_input", "--no_track_gpu", "--qpc_time_ms");
        try
        {
            foreach (int interval in imagesOnly ? Array.Empty<int>() : (zoom ? new[] { 0, -1, 0 } : new[] { 0, -1, 5000, 0, 5000, -1 }))
            {
                bool showMap = interval >= 0, rightButton = !zoom;
                map.GetType().GetProperty("CameraIntervalMs", Private)!.SetValue(map, Math.Max(0, interval));
                map.Visibility = showMap ? Visibility.Visible : Visibility.Hidden;
                canvas.Center = center;
                canvas.Zoom = initialZoom;
                canvas.InvalidateVisual();
                Pause(350);
                if (rightButton) down(origin, MouseButton.Right, 1, false, false, false);
                var gpu = typeof(EditorCanvas).GetProperty("GlPlan", Private)!.GetValue(canvas)!;
                int Counter(object target, string property) => (int)target.GetType().GetProperty(property, Private)!.GetValue(target)!;
                int initialFrames = Counter(gpu, "Frames"), initialRasters = Counter(map, "RasterBuilds");
                int initialRecordings = Counter(gpu, "SceneRecordings"), wheelPosition = 0;
                var times = new List<double>();
                var lastBrush = brush.GetValue(canvas);
                int rebuilds = 0;
                double start = Now();
                var frame = new DispatcherFrame();
                var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(4) };
                timer.Tick += (_, _) =>
                {
                    double now = Now(), elapsed = now - start;
                    if (elapsed > 7000) { timer.Stop(); frame.Continue = false; return; }
                    var delta = new Vector(Math.Sin(elapsed / 70) * 480, Math.Sin(elapsed / 100) * 245);
                    if (rightButton) move(origin + delta, false);
                    else
                    {
                        int tick = (int)(elapsed / 40) % 24;
                        int target = tick <= 12 ? tick - 6 : 18 - tick;
                        if (target == wheelPosition) return;
                        var anchor = new Point(canvas.ActualWidth * .58, canvas.ActualHeight * .45);
                        var anchored = canvas.World(anchor);
                        wheel(anchor, (target - wheelPosition) * 120);
                        if ((canvas.World(anchor) - anchored).Length > 1e-5)
                            throw new InvalidOperationException("Wheel zoom moved its anchor.");
                        wheelPosition = target;
                    }
                    Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                    var currentBrush = brush.GetValue(canvas);
                    if (!ReferenceEquals(currentBrush, lastBrush)) rebuilds++;
                    lastBrush = currentBrush;
                    times.Add(Now() - now);
                };
                timer.Start();
                Dispatcher.PushFrame(frame);
                if (rightButton) up(MouseButton.Right);
                times.Sort();
                samples.Add(new Sample(showMap, rightButton, start + 1200, start + 6800, times.Count,
                    times[times.Count / 2], times[(int)((times.Count - 1) * .95)], rebuilds)
                {
                    IntervalMs = interval,
                    SceneRecordings = Counter(gpu, "SceneRecordings") - initialRecordings,
                    GlFrames = Counter(gpu, "Frames") - initialFrames,
                    TopologyRebuilds = Counter(map, "RasterBuilds") - initialRasters,
                    Renderer = (string?)gpu.GetType().GetProperty("Device", Private)!.GetValue(gpu)
                });
                if (samples[^1].GlFrames == 0)
                    throw new InvalidOperationException("No OpenGL frames: " + gpu.GetType().GetProperty("Failure", Private)!.GetValue(gpu));
            }
            if (zoom)
            {
                Pause(450);
                var anchor = new Point(canvas.ActualWidth * .58, canvas.ActualHeight * .45);
                var finalAnchor = canvas.World(anchor);
                var sceneScale = typeof(EditorCanvas).GetField("sceneZoom", Private)!;
                var gpu = typeof(EditorCanvas).GetProperty("GlPlan", Private)!.GetValue(canvas)!;
                var recordings = gpu.GetType().GetProperty("SceneRecordings", Private)!;
                int before = (int)recordings.GetValue(gpu)!;
                wheel(anchor, 360);
                Pause(70);
                if ((double)sceneScale.GetValue(canvas)! == canvas.Zoom || (int)recordings.GetValue(gpu)! != before)
                    throw new InvalidOperationException("Wheel preview rebuilt the scene before settling.");
                Pause(400);
                if ((double)sceneScale.GetValue(canvas)! != canvas.Zoom || (int)recordings.GetValue(gpu)! != before + 1 ||
                    (canvas.World(anchor) - finalAnchor).Length > 1e-5)
                    throw new InvalidOperationException("The final zoom did not settle precisely and rebuild once.");
                Pause(300);
                if ((int)recordings.GetValue(gpu)! != before + 1)
                    throw new InvalidOperationException("The settled zoom rebuilt again at rest.");
                Console.WriteLine("Zoom preview, cursor anchor, final scene and idle cache checks passed.");
            }
            else
            {
                canvas.Center = center;
                canvas.Zoom = initialZoom;
                canvas.InvalidateVisual();
                Pause(250);
            }
            var glPlan = (FrameworkElement)typeof(EditorCanvas).GetProperty("GlPlan", Private)!.GetValue(canvas)!;
            MainWindow.SaveVisual(glPlan, Path.Combine(output, "opengl-plan.png"));
            var vector = new System.Windows.Media.DrawingGroup();
            using (var dc = vector.Open())
                typeof(EditorCanvas).GetMethod("DrawViewport", Private)!.Invoke(canvas, new object[] { dc, true });
            var reference = new System.Windows.Controls.Image
            {
                Source = new System.Windows.Media.DrawingImage(vector),
                Width = canvas.ActualWidth, Height = canvas.ActualHeight,
                Stretch = System.Windows.Media.Stretch.None,
                ClipToBounds = true
            };
            // Give the reference the exact same viewport as the GPU image.
            vector.ClipGeometry = new System.Windows.Media.RectangleGeometry(new Rect(canvas.RenderSize));
            reference.Measure(canvas.RenderSize);
            reference.Arrange(new Rect(canvas.RenderSize));
            MainWindow.SaveVisual(reference, Path.Combine(output, "reference-plan.png"));
        }
        finally
        {
            if (capture is { HasExited: false })
            {
                using var stop = Start(presentMon!, "--session_name", session, "--terminate_existing_session");
                capture.WaitForExit(3500);
            }
            capture?.Dispose();
            window.Close();
            app.Shutdown();
        }

        if (File.Exists(trace))
        {
            using var stream = File.ReadLines(trace).GetEnumerator();
            if (stream.MoveNext())
            {
                var header = stream.Current.Split(',');
                int timeColumn = Array.IndexOf(header, "CPUStartQPCTimeInMs"), chainColumn = Array.IndexOf(header, "SwapChainAddress");
                var rows = new List<(double Time, string Chain)>();
                if (timeColumn >= 0 && chainColumn >= 0)
                    while (stream.MoveNext())
                    {
                        var row = stream.Current.Split(',');
                        if (row.Length > Math.Max(timeColumn, chainColumn) &&
                            double.TryParse(row[timeColumn], NumberStyles.Float, CultureInfo.InvariantCulture, out double time))
                            rows.Add((time, row[chainColumn]));
                    }
                foreach (var sample in samples)
                {
                    int displayed = rows.Where(row => row.Time >= sample.Start && row.Time < sample.End).GroupBy(row => row.Chain)
                        .Select(group => group.Count()).DefaultIfEmpty(0).Max();
                    sample.DisplayedFps = displayed / ((sample.End - sample.Start) / 1000);
                }
            }
        }
        File.WriteAllText(Path.Combine(output, zoom ? "zoom-ab.json" : "navigation-ab.json"), JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
        foreach (var s in samples)
            Console.WriteLine($"map={s.Map}; pointer={s.Pointer}; updates={s.Updates}; median={s.Median:.00} ms; p95={s.P95:.00} ms; scene recordings={s.SceneRecordings}; displayed={s.DisplayedFps:F1} FPS");
    }

    private static double Now() => Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency;

    private static void Pause(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static Process? Start(string tool, params string[] args)
    {
        var settings = new ProcessStartInfo(tool) { UseShellExecute = false, CreateNoWindow = true };
        foreach (string arg in args) settings.ArgumentList.Add(arg);
        return Process.Start(settings);
    }
}


// Coat — Epicteta Lupercal
using Coat;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

internal static class LifecycleProbe
{
    private sealed record Sample(string Phase, int Step, double OpenMilliseconds, long AllocatedBytes,
        long ManagedBytes, long PrivateBytes, long GraphicsCacheBytes, long GraphicsCacheLimit,
        bool ReusedPlan, bool ReusedSurface, bool ReusedContext, int Windows);

    internal static void Run(string path, string alternate, string output)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow { TestMode = true, Width = 1440, Height = 900 };
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var surface = typeof(EditorCanvas).GetProperty("GlPlan", privateInstance)!;
        var context = surface.PropertyType.GetField("context", privateInstance)!;
        var language = typeof(MainWindow).GetMethod("ChangeLanguage", privateInstance)!;
        var samples = new List<Sample>();
        window.Show();
        window.OpenPath(path);
        Pause(450);
        Measure("warm", 0, () => { });
        for (int i = 0; i < 8; i++) Measure("same-file", i, () => window.OpenPath(path));
        for (int i = 0; i < 8; i++) Measure("alternate", i, () => window.OpenPath(i % 2 == 0 ? alternate : path));
        for (int i = 0; i < 6; i++) Measure("interface", i, () => language.Invoke(window, new object[] { i % 2 == 0 ? "fr" : "en" }));
        var project = window.Document;
        var reopened = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, ".", Path.GetFileName(path)));
        window.OpenPath(reopened);
        if (!ReferenceEquals(project, window.Document)) throw new InvalidOperationException("The open plan was duplicated.");
        if (samples.Any(sample => sample.Windows != 1) ||
            samples.Where(sample => sample.Phase == "interface").Any(sample => !sample.ReusedSurface || !sample.ReusedContext))
            throw new InvalidOperationException("The editor duplicated a window or recreated the OpenGL context.");

        string fixture = Path.Combine(output, "reopen-fixture.coat");
        var plan = new Plan();
        File.WriteAllText(fixture, plan.Serialize());
        window.OpenPath(fixture);
        plan.EnsureLevel(2);
        File.WriteAllText(fixture, plan.Serialize());
        window.OpenPath(fixture);
        if (!window.Document.Levels.Contains(2)) throw new InvalidOperationException("A changed project did not reload.");

        CheckHistory();
        window.Close();
        app.Shutdown();
        Console.WriteLine("File identity, changed file, shared image history and OpenGL lifecycle checks passed.");
        File.WriteAllText(Path.Combine(output, "lifecycle.json"), JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));

        void Measure(string phase, int index, Action action)
        {
            var previous = window.Document;
            var previousSurface = surface.GetValue(window.Canvas);
            var previousContext = context.GetValue(previousSurface);
            long allocated = GC.GetTotalAllocatedBytes(true);
            var watch = Stopwatch.StartNew();
            action();
            double milliseconds = watch.Elapsed.TotalMilliseconds;
            Pause(160);
            long delta = GC.GetTotalAllocatedBytes(true) - allocated;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            var graphics = (SkiaSharp.GRContext?)context.GetValue(surface.GetValue(window.Canvas));
            long graphicsBytes = 0;
            graphics?.GetResourceCacheUsage(out _, out graphicsBytes);
            samples.Add(new(phase, index, milliseconds, delta, GC.GetTotalMemory(false), process.PrivateMemorySize64, graphicsBytes, graphics?.GetResourceCacheLimit() ?? 0,
                ReferenceEquals(previous, window.Document), ReferenceEquals(previousSurface, surface.GetValue(window.Canvas)), ReferenceEquals(previousContext, graphics), app.Windows.Count));
        }
    }

    private static void CheckHistory()
    {
        string image = new('A', 2 * 1024 * 1024);
        var plan = new Plan { Background = new ReferenceImage { Data = image } };
        var snapshot = typeof(Plan).Assembly.GetType("Coat.PlanSnapshot")!;
        const BindingFlags methods = BindingFlags.NonPublic | BindingFlags.Static;
        var capture = snapshot.GetMethod("Capture", methods)!;
        var push = typeof(History).GetMethod("Push", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var history = new History();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int step = 0; step < 12; step++)
        {
            push.Invoke(history, new[] { capture.Invoke(null, new object[] { plan }) });
            plan.Background!.X++;
        }
        if (GC.GetAllocatedBytesForCurrentThread() - allocated >= image.Length)
            throw new InvalidOperationException("Image data was allocated again in the undo records.");
        var undone = history.Undo(plan)!;
        var redone = history.Redo(undone)!;
        if (undone.Background?.X != 11 || redone.Background?.X != 12 ||
            !ReferenceEquals(undone.Background.Data, image) || !ReferenceEquals(redone.Background.Data, image))
            throw new InvalidOperationException("Undo or redo did not preserve image data and editable properties.");
    }

    private static void Pause(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}

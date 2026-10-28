// Coat — Epicteta Lupercal
using Coat;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        string source = File.ReadAllText(args[0]);
        string output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        var input = Plan.Parse(source);
        var measurements = new Dictionary<string, double[]>();
        var operations = new Dictionary<string, Action<Plan>>
        {
            ["normalize"] = p => p.Normalize(),
            ["segment"] = p => p.Segment(new(-32, -32), new(256, 256)),
            ["rectangle"] = p => p.Polygon(new V2[] { new(0, 0), new(256, 0), new(256, 256), new(0, 256) }),
            ["circle64"] = p => p.Polygon(Enumerable.Range(0,64).Select(i => new V2(256*Math.Cos(i*Math.PI/32),256*Math.Sin(i*Math.PI/32))).ToArray()),
            ["move"] = p => { foreach (var v in p.Vertices.Where(v => v.Level == p.ActiveLevel)) v.P += new V2(32, 32); p.Normalize(); }
        };
        foreach (var (name, operation) in operations)
        {
            var times = new List<double>();
            for (int i = 0; i < 8; i++)
            {
                var p = Plan.Parse(source); var timer = Stopwatch.StartNew(); operation(p); timer.Stop();
                if (i > 0) times.Add(timer.Elapsed.TotalMilliseconds);
                if (i == 7) File.WriteAllText(Path.Combine(output,name+".coat"), p.Serialize());
            }
            measurements[name] = times.ToArray();
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow { TestMode = true, ShowInTaskbar = false, Width = 1440, Height = 900 };
        window.Show(); window.UpdateLayout();
        var uiTimes = new List<double>();
        for (int i = 0; i < 8; i++)
        {
            window.LoadDocument(Plan.Parse(source), ""); window.SetTool(EditorTool.Draw); Pump();
            var timer = Stopwatch.StartNew();
            window.Mutate(() => operations["segment"](window.Document)); Pump(); timer.Stop();
            if (i > 0) uiTimes.Add(timer.Elapsed.TotalMilliseconds);
        }
        measurements["segment_with_ui"] = uiTimes.ToArray();
        window.LoadDocument(Plan.Parse(source), ""); window.Canvas.Fit(); Pump();
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "plan.png"));
        window.Close(); app.Shutdown();
        File.WriteAllText(Path.Combine(output,"timings.json"), JsonSerializer.Serialize(measurements, new JsonSerializerOptions { WriteIndented = true }));
        foreach (var (name,times) in measurements) Console.WriteLine($"{name}: median {times.Order().ElementAt(times.Length/2):F2} ms");
        Console.WriteLine($"Vertices: {input.Vertices.Count}; edges: {input.Edges.Count}; levels: {string.Join(",",input.Levels)}");
    }
    static void Pump() { var f = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => f.Continue = false)); Dispatcher.PushFrame(f); }
}

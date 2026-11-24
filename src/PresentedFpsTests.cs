// Coat — Epicteta Lupercal
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Coat;

internal static class PresentedFpsTests
{
    internal static void Run(string output, bool expectUnavailable)
    {
        Directory.CreateDirectory(output);
        var results = new List<string>();
        void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            results.Add("PASS: " + description);
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow { Width = 1050, Height = 680, ShowInTaskbar = false, ShowActivated = false };
        window.Shortcuts.AnimatedMenus = false;
        window.ChangeLanguage("en");
        window.Show();
        InterfaceLayoutTests.Layout(window);
        window.TestMode = true;
        try
        {
            var fps = InterfaceLayoutTests.Children<PresentedFps>(window).Single();
            Check(fps.TranslatePoint(new Point(), window).Y > window.ActualHeight - 65, "The FPS counter is in the status bar");
            var rates = new List<int>();
            var frame = new DispatcherFrame();
            var dispatch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            long start = Stopwatch.GetTimestamp();
            dispatch.Tick += (_, _) =>
            {
                double elapsed = Stopwatch.GetElapsedTime(start).TotalSeconds;
                if (elapsed < 3)
                {
                    window.Canvas.Center = new V2(Math.Sin(elapsed * 4) * 550, 0);
                    window.Canvas.InvalidateVisual();
                    if (int.TryParse(fps.Text.Split(' ')[0], NumberStyles.None, CultureInfo.InvariantCulture, out int rate))
                        rates.Add(rate);
                }
                if (elapsed < 6) return;
                dispatch.Stop();
                frame.Continue = false;
            };
            dispatch.Start();
            Dispatcher.PushFrame(frame);
            if (expectUnavailable)
                Check(rates.Count == 0 && fps.Text == "— FPS", "Denied graphics tracing displays an unavailable reading even when Coat redraws");
            else
            {
                Check(rates.Any(value => value > 5), "PresentMon reports displayed frames from the Coat process during panning");
                results.Add("Observed positive FPS values: " + string.Join(", ", rates.Where(rate => rate > 0).Distinct().Order()));
                Check(int.TryParse(fps.Text.Split(' ')[0], out int idle) && idle < rates.Max(),
                    "The measured rate drops after panning stops (" + fps.Text + ")");
            }
            File.WriteAllLines(Path.Combine(output, expectUnavailable ? "unavailable.txt" : "results.txt"), results);
        }
        finally
        {
            window.Close();
            app.Shutdown();
        }
    }
}

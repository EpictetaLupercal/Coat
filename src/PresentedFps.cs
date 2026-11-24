// Coat — Epicteta Lupercal
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Coat;

internal sealed class PresentedFps(PresentationMonitor monitor) : TextBlock
{
    private readonly DispatcherTimer refresh = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(500)
    };

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Text = "— FPS";
        ToolTip = L.T("Images de Coat réellement affichées par seconde.");
        refresh.Tick += (_, _) =>
        {
            int? fps = monitor.Read();
            string reading = fps.HasValue ? $"{fps.Value} FPS" : "— FPS";
            if (Text != reading) Text = reading;
            if (monitor.IsUnavailable) ToolTip = L.T("Mesure indisponible. Vérifiez que Windows autorise l’accès à la trace graphique.");
        };
        Loaded += (_, _) => refresh.Start();
        Unloaded += (_, _) => refresh.Stop();
    }
}

internal sealed class PresentationMonitor : IDisposable
{
    private static readonly string Executable = Path.Combine(AppContext.BaseDirectory, "third-party", "PresentMon", "PresentMon.exe");
    private readonly string session = $"Coat-{Environment.ProcessId}-{Guid.NewGuid():N}";
    private readonly object gate = new();
    private readonly Dictionary<string, HashSet<double>> chains = new(StringComparer.Ordinal);
    private Process? capture;
    private double latestDisplay;
    private long lastArrival;
    private bool parsedHeader;
    private int pidColumn, chainColumn, displayColumn;
    internal bool IsUnavailable { get; private set; }

    internal void Start()
    {
        if (capture != null || IsUnavailable) return;
        var command = Command("--process_id", Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            "--session_name", session, "--output_stdout", "--no_console_stats", "--exclude_dropped",
            "--no_track_input", "--no_track_gpu", "--qpc_time_ms", "--write_display_time", "--terminate_on_proc_exit");
        command.RedirectStandardOutput = true;
        command.RedirectStandardError = true;
        try
        {
            capture = Process.Start(command);
            if (capture == null) { IsUnavailable = true; return; }
            capture.OutputDataReceived += (_, e) => { if (e.Data != null) Receive(e.Data); };
            capture.BeginOutputReadLine();
            capture.BeginErrorReadLine();
        }
        catch (Win32Exception)
        {
            IsUnavailable = true;
        }
    }

    private void Receive(string line)
    {
        var fields = line.Split(',');
        if (!parsedHeader)
        {
            pidColumn = Array.IndexOf(fields, "ProcessID");
            chainColumn = Array.IndexOf(fields, "SwapChainAddress");
            displayColumn = Array.IndexOf(fields, "DisplayTimeAbs");
            parsedHeader = pidColumn >= 0 && chainColumn >= 0 && displayColumn >= 0;
            return;
        }
        if (fields.Length <= Math.Max(pidColumn, Math.Max(chainColumn, displayColumn)) ||
            !int.TryParse(fields[pidColumn], NumberStyles.None, CultureInfo.InvariantCulture, out int pid) || pid != Environment.ProcessId ||
            !double.TryParse(fields[displayColumn], NumberStyles.Float, CultureInfo.InvariantCulture, out double displayed) ||
            !double.IsFinite(displayed) || displayed < 0) return;

        lock (gate)
        {
            latestDisplay = Math.Max(latestDisplay, displayed);
            if (!chains.TryGetValue(fields[chainColumn], out var times)) chains[fields[chainColumn]] = times = new();
            if (times.Add(displayed)) lastArrival = Stopwatch.GetTimestamp();
        }
    }

    internal int? Read()
    {
        if (capture == null || capture.HasExited)
        {
            IsUnavailable |= capture != null;
            return null;
        }
        lock (gate)
        {
            if (lastArrival == 0) return null;
            if (Stopwatch.GetElapsedTime(lastArrival).TotalSeconds > 1.25) return 0;
            int count = 1;
            double span = 0;
            // Measure the active swap chain with the most displayed frames. A floating
            // window may have its own chain and must not be added to the editor's rate.
            foreach (var times in chains.Values)
            {
                times.RemoveWhere(time => time < latestDisplay - 1000);
                if (times.Count <= count) continue;
                count = times.Count;
                span = times.Max() - times.Min();
            }
            return span > 0 ? (int)Math.Round(1000 * (count - 1) / span) : 0;
        }
    }

    private static ProcessStartInfo Command(params string[] args)
    {
        var command = new ProcessStartInfo(Executable) { UseShellExecute = false, CreateNoWindow = true };
        foreach (string arg in args) command.ArgumentList.Add(arg);
        return command;
    }

    public void Dispose()
    {
        if (capture == null) return;
        if (!capture.HasExited)
        {
            try
            {
                using var stop = Process.Start(Command("--session_name", session, "--terminate_existing_session"));
            }
            catch (Win32Exception) { /* PresentMon also exits when Coat's process ends. */ }
        }
        capture.Dispose();
        capture = null;
    }
}

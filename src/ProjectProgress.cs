// Coat — Epicteta Lupercal
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Coat;

public sealed record ProjectProgress
{
    public bool FirstSave { get; init; }
    public int CreationXp { get; init; }
    public string? FirstMap { get; init; }
    public bool FirstIteration { get; init; }
    public bool Playtest { get; init; }

    [JsonIgnore]
    public int TotalXp => (FirstSave ? 20 : 0) + Math.Clamp(CreationXp, 0, 20) +
        (FirstMap == null ? 0 : 80) + (FirstIteration ? 40 : 0) + (Playtest ? 100 : 0);

    internal static int NextLevelCost(int level) => level == 0 ? 5000 : (50 + 15 * level) * 115;

    internal (int Level, int Earned, int Target) Gauge()
    {
        int level = 1, earned = TotalXp * 100;
        while (earned >= NextLevelCost(level)) earned -= NextLevelCost(level++);
        return (level, earned, NextLevelCost(level));
    }
}

public partial class Plan
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectProgress? Progression { get; set; }
}

public partial class MainWindow
{
    internal ProjectProgress Progress => Document.Progression ?? new();

    private void ApplyProgress(ProjectProgress next, bool persist = false, bool milestone = false)
    {
        var previous = Progress;
        if (previous == next) return;
        Document.Progression = next;
        if (persist && !string.IsNullOrEmpty(file))
        {
            try
            {
                string WithProgress(string json)
                {
                    var root = JsonNode.Parse(json)!.AsObject();
                    root[nameof(Plan.Progression)] = JsonSerializer.SerializeToNode(next);
                    return root.ToJsonString(Plan.JsonOptions);
                }
                // Keep the last saved geometry and camera if the working plan has unsaved edits.
                var snapshot = saved.Restore();
                snapshot.Progression = next;
                WriteAtomic(file, WithProgress(File.ReadAllText(file)));
                saved = PlanSnapshot.Capture(snapshot);
                RememberFileVersion();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                Notify(L.T("La progression sera enregistrée à la prochaine sauvegarde."));
            }
        }
        rts?.ShowReward(previous, next, milestone);
        if (persist) UpdateTitle();
    }

    internal bool ConfirmPlaytest()
    {
        if (Progress.FirstMap == null || Progress.Playtest) return false;
        ApplyProgress(Progress with { Playtest = true }, persist: true, milestone: true);
        return true;
    }

    internal void SaveProjectTo(string path)
    {
        SaveView();
        var previous = Progress;
        var stored = Document.Progression;
        Document.Progression = previous with { FirstSave = true };
        try
        {
            string contents = Document.Serialize();
            WriteAtomic(path, contents);
            file = Path.GetFullPath(path);
            saved = PlanSnapshot.Capture(Document);
            RememberFileVersion();
        }
        catch
        {
            Document.Progression = stored;
            throw;
        }
        rts?.ShowReward(previous, Progress, milestone: false);
        RememberRecent(path);
        UpdateInspector();
        Notify(L.T("Plan enregistré : ") + Path.GetFileName(path));
    }

    internal void WriteProjectMap(string path, MapExportResult result)
    {
        WriteAtomic(path, result.Text);
        Notify(L.F($"Carte .map exportée : {result.BrushCount} brushes de murs et de sols."));
        if (result.BrushCount == 0) return;
        var previous = Progress;
        if (previous.FirstMap == null)
            ApplyProgress(previous with { FirstMap = result.GeometryFingerprint }, persist: true, milestone: true);
        else if (!previous.FirstIteration && previous.FirstMap != result.GeometryFingerprint)
            ApplyProgress(previous with { FirstIteration = true }, persist: true, milestone: true);
    }
}

// Coat — Epicteta Lupercal
using System.IO;
using System.Text.Json;
using System.Windows.Controls;

namespace Coat;
public partial class MainWindow
{
    internal string? RecentFilesPathOverride { get; set; }
    private string RecentFilesPath => RecentFilesPathOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Coat", "recent-files.json");
    internal List<string> RecentFiles { get; private set; } = new();
    private MenuItem recentMenu = new();
    private void LoadRecent()
    {
        try { if (File.Exists(RecentFilesPath)) RecentFiles = (JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentFilesPath)) ?? new()).Where(p => !string.IsNullOrWhiteSpace(p) && Path.IsPathFullyQualified(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException) { RecentFiles.Clear(); }
        UpdateRecentMenu();
    }
    internal void RememberRecent(string path)
    {
        if (TestMode && RecentFilesPathOverride == null) return;
        path = Path.GetFullPath(path); RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)); RecentFiles.Insert(0, path);
        if (RecentFiles.Count > 10) RecentFiles.RemoveRange(10, RecentFiles.Count - 10);
        PersistRecent(); UpdateRecentMenu();
    }
    private void PersistRecent()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(RecentFilesPath)!); WriteAtomic(RecentFilesPath, JsonSerializer.Serialize(RecentFiles)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Notify(L.T("Impossible d’enregistrer la liste des fichiers récents.")); }
    }
    private void ClearRecent() { RecentFiles.Clear(); if (!TestMode || RecentFilesPathOverride != null) PersistRecent(); UpdateRecentMenu(); }
    private void UpdateRecentMenu()
    {
        recentMenu.Items.Clear();
        foreach (string path in RecentFiles)
        {
            var item = DropDownItem(path.Replace("_", "__")); item.ToolTip = path;
            item.Click += (_, _) => OpenRecent(path); recentMenu.Items.Add(item);
        }
        if (RecentFiles.Count == 0) { var empty = DropDownItem(L.T("Aucun projet récent")); empty.IsEnabled = false; recentMenu.Items.Add(empty); }
        else { recentMenu.Items.Add(new Separator()); var clear = DropDownItem(L.T("Effacer la liste")); clear.Click += (_, _) => ClearRecent(); recentMenu.Items.Add(clear); }
    }
    internal void OpenRecent(string path)
    {
        if (!File.Exists(path)) { Notify(L.T("Ce fichier a été déplacé ou supprimé : ") + path); return; }
        Canvas.CancelGesture(); if (!TestMode && !CheckSave()) return; OpenPath(path);
    }
}

// Coat — Epicteta Lupercal
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Coat;

internal sealed class ColorPalette
{
    public const int Capacity = 12;
    private string?[] colors = new string?[Capacity];
    private readonly string? path;
    public IReadOnlyList<string?> Colors => Array.AsReadOnly(colors);
    public event Action? Changed;
    public ColorPalette(string? path = null) => this.path = path;
    public static ColorPalette Load(string path)
    {
        var palette = new ColorPalette(path);
        if (!File.Exists(path)) return palette;
        var saved = JsonSerializer.Deserialize<string?[]>(File.ReadAllText(path));
        if (saved == null || saved.Length != Capacity) throw new InvalidDataException(L.T("La palette doit contenir 12 emplacements."));
        palette.colors = saved.Select(c => c == null ? null : Normalize(c)).ToArray();
        return palette;
    }
    private static string Normalize(string color) => ColorMath.TryHex(color, out var parsed) ? ColorMath.Hex(parsed) : throw new InvalidDataException(L.T("Couleur invalide : utilisez le format #RRGGBB."));
    public int Add(string color)
    {
        color = Normalize(color);
        int index = Array.IndexOf(colors, color);
        if (index >= 0) return index;
        index = Array.IndexOf(colors, null);
        if (index < 0) throw new InvalidOperationException(L.T("Les 12 emplacements sont occupés. Clic droit → Supprimer pour libérer une place."));
        var next = (string?[])colors.Clone(); next[index] = color; Commit(next); return index;
    }
    public void Delete(int index)
    {
        if (index < 0 || index >= Capacity) throw new ArgumentOutOfRangeException(nameof(index));
        if (colors[index] == null) return;
        var next = (string?[])colors.Clone(); next[index] = null; Commit(next);
    }
    private void Commit(string?[] next)
    {
        // Persist before updating the UI: a failed save must not silently lose a user's palette.
        if (path != null) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); MainWindow.WriteAtomic(path, JsonSerializer.Serialize(next, Plan.JsonOptions)); }
        colors = next; Changed?.Invoke();
    }
}

internal sealed class PaletteView : UniformGrid
{
    private readonly ColorPalette palette;
    private readonly Action<string> choose, report;
    private readonly Func<string> current;
    public PaletteView(ColorPalette palette, Action<string> choose, Func<string> current, Action<string> report)
    {
        this.palette = palette; this.choose = choose; this.current = current; this.report = report;
        Columns = 6; Rows = 2;
        Loaded += (_, _) => { palette.Changed += Refresh; Refresh(); };
        Unloaded += (_, _) => palette.Changed -= Refresh;
        Refresh();
    }
    public void Refresh()
    {
        Children.Clear();
        for (int i = 0; i < ColorPalette.Capacity; i++)
        {
            int index = i; string? color = palette.Colors[i];
            bool active = color != null && color.Equals(current(), StringComparison.OrdinalIgnoreCase);
            var swatch = new Button
            {
                Width = 32, Height = 32, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(0),
                Background = color == null ? Brushes.Transparent : EditorCanvas.Brush(color),
                BorderBrush = EditorCanvas.Brush(active ? "#FFFFFF" : "#647387"), BorderThickness = new Thickness(active ? 2 : 1),
                ToolTip = color == null ? L.F($"Emplacement {i + 1} vide — ajouter avec + dans la roue de couleurs") : L.F($"{color} — clic droit pour supprimer"),
                IsEnabled = color != null
            };
            AutomationProperties.SetName(swatch, L.F($"Couleur enregistrée {i + 1}") + (color == null ? L.T(" : vide") : " : " + color));
            if (color != null)
            {
                swatch.Click += (_, _) => { choose(color); Refresh(); };
                var menu = new ContextMenu { Background = Brushes.White, Foreground = Brushes.Black };
                var delete = new MenuItem { Header = L.T("Supprimer"), Background = Brushes.White, Foreground = Brushes.Black };
                delete.Click += (_, _) => { try { palette.Delete(index); } catch (Exception ex) { report(L.T("Palette non enregistrée : ") + ex.Message); } };
                menu.Items.Add(delete); swatch.ContextMenu = menu;
            }
            Children.Add(swatch);
        }
    }
}

public partial class MainWindow
{
    internal ColorPalette SavedColors { get; private set; } = new();
    internal void LoadPalette()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Coat", "palette.json");
        try { SavedColors = ColorPalette.Load(path); UpdateInspector(); }
        catch (Exception ex) { SavedColors = new ColorPalette(path); UpdateInspector(); Notify(L.T("Palette non chargée : ") + ex.Message); }
    }
}

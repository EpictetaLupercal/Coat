// Coat — Epicteta Lupercal
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace Coat;

internal static class L
{
    private static readonly IReadOnlyDictionary<string, string> english = LoadEnglish();
    public static string Language { get; private set; } = "en";
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language == "fr" ? "fr-FR" : "en-US");
    internal static IReadOnlyDictionary<string, string> English => english;
    internal static readonly HashSet<string> Missing = new();
    public static string Normalize(string? language) => language == "fr" ? "fr" : "en";
    public static void SetLanguage(string? language)
    {
        Language = Normalize(language);
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
    }
    private static IReadOnlyDictionary<string, string> LoadEnglish()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Coat.Locales.en.json") ?? throw new InvalidOperationException("The English language catalog is missing.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? throw new InvalidOperationException("The English language catalog is empty.");
    }
    public static string T(string french)
    {
        if (Language == "fr") return french;
        if (english.TryGetValue(french, out var translated)) return translated;
        Missing.Add(french); return french;
    }
    public static string F(FormattableString text) => string.Format(Culture, T(text.Format), text.GetArguments());
    // "Points" means vertices in geometry controls, but dots in a fill pattern.
    public static string Dots => Language == "fr" ? "Points" : "Dots";
    public static string Undo => Language == "fr" ? "Annuler" : "Undo";
    public static string ShortcutLabel(string id)
    {
        if (id == "Annuler") return Undo;
        if (id == "Désélectionner / annuler le geste") return T("Annuler / Revenir à Sélection");
        var grid = Regex.Match(id, @"^Grille (\d+) u( \(pavé\))?$");
        if (grid.Success) return string.Format(Culture, T(grid.Groups[2].Success ? "Grille {0} u (pavé)" : "Grille {0} u"), grid.Groups[1].Value);
        const string fast = " (rapide)";
        if (id.EndsWith(fast, StringComparison.Ordinal)) return T(id[..^fast.Length]) + T(fast);
        return T(id);
    }
    public static string KeyName(Key key)
    {
        if (key >= Key.D0 && key <= Key.D9) return ((int)key - (int)Key.D0).ToString(CultureInfo.InvariantCulture);
        if (key >= Key.NumPad0 && key <= Key.NumPad9) return T("Pavé ") + ((int)key - (int)Key.NumPad0).ToString(CultureInfo.InvariantCulture);
        return key switch
        {
            Key.Escape => T("Échap"), Key.Delete => T("Suppr"), Key.Back => T("Retour arrière"), Key.Return => T("Entrée"),
            Key.Home => T("Origine"), Key.End => T("Fin"), Key.Space => T("Espace"), Key.PageUp => "Page Up", Key.PageDown => "Page Down",
            Key.Left => T("Gauche"), Key.Right => T("Droite"), Key.Up => T("Haut"), Key.Down => T("Bas"), Key.OemComma => ",", _ => key.ToString()
        };
    }
}

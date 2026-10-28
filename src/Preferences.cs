// Coat — Epicteta Lupercal
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Coat;

public record Shortcut(Key Key, ModifierKeys Modifiers)
{
    public override string ToString() => (Modifiers.HasFlag(ModifierKeys.Control) ? "Ctrl+" : "") + (Modifiers.HasFlag(ModifierKeys.Shift) ? L.T("Maj+") : "") + (Modifiers.HasFlag(ModifierKeys.Alt) ? "Alt+" : "") + L.KeyName(Key);
}
public record ShortcutAction(string Label, Shortcut Command, Shortcut Default)
{
    public string Category => Label.StartsWith("Grille ") ? "Grille" : Label switch
    {
        "Nouveau" or "Ouvrir" or "Enregistrer" or "Enregistrer sous" or "Importer une image" => "Fichiers",
        "Sélection" or "Dessin" or "Gomme" or "Coloriage" or "Shapes" or "Annotations" => "Outils",
        "Sélection de points" or "Sélection d’arêtes" or "Sélection de zones" or "Translation" or "Rotation" or "Échelle" => "Sélection et transformations",
        "Aide" or "Préférences" => "Interface",
        _ when Label.StartsWith("Vue ") || Label.StartsWith("Niveau") || Label is "Cadrer" or "Grille visible" or "Longueurs visibles" => "Navigation et affichage",
        _ => "Édition"
    };
}
public class ShortcutSettings
{
    public static readonly List<ShortcutAction> Actions = CreateActions();
    public Dictionary<string, Shortcut> Bindings { get; set; } = Actions.ToDictionary(a => a.Label, a => a.Default);
    public string Language { get; set; } = "en";
    public int ShortcutVersion { get; set; }
    public static Dictionary<string, Shortcut> ClassicBindings() => Actions.ToDictionary(a => a.Label, a => a.Command);
    private static List<ShortcutAction> CreateActions()
    {
        var list = new List<ShortcutAction>();
        void Add(string label, Key key, ModifierKeys mods = ModifierKeys.None) => list.Add(new(label, new(key, mods), new(key, mods)));
        Add("Nouveau", Key.N, ModifierKeys.Control); Add("Ouvrir", Key.O, ModifierKeys.Control); Add("Enregistrer", Key.S, ModifierKeys.Control); Add("Enregistrer sous", Key.S, ModifierKeys.Control | ModifierKeys.Shift);
        Add("Annuler", Key.Z, ModifierKeys.Control); Add("Rétablir", Key.Y, ModifierKeys.Control); Add("Rétablir (alternatif)", Key.Z, ModifierKeys.Control | ModifierKeys.Shift);
        Add("Importer une image", Key.I, ModifierKeys.Control); Add("Tout sélectionner", Key.A, ModifierKeys.Control); Add("Dupliquer", Key.D, ModifierKeys.Control); Add("Grouper", Key.G, ModifierKeys.Control); Add("Dissocier", Key.G, ModifierKeys.Control | ModifierKeys.Shift);
        Add("Copier", Key.C, ModifierKeys.Control); Add("Couper", Key.X, ModifierKeys.Control); Add("Coller", Key.V, ModifierKeys.Control); Add("Niveaux supérieurs visibles", Key.H);
        Add("Sélection", Key.V); Add("Dessin", Key.D); Add("Gomme", Key.E); Add("Coloriage", Key.C); Add("Shapes", Key.R); Add("Annotations", Key.T);
        Add("Sélection de points", Key.P); Add("Sélection d’arêtes", Key.L); Add("Sélection de zones", Key.F);
        Add("Désélectionner / annuler le geste", Key.Escape); Add("Niveau supérieur", Key.PageUp); Add("Niveau inférieur", Key.PageDown); Add("Supprimer", Key.Delete); Add("Supprimer (alternatif)", Key.Back);
        Add("Cadrer", Key.Home); Add("Grille visible", Key.G); Add("Longueurs visibles", Key.M); Add("Aide", Key.F1);
        Add("Niveaux inférieurs visibles", Key.H, ModifierKeys.Shift);
        Add("Translation", Key.W); Add("Rotation", Key.R, ModifierKeys.Shift); Add("Échelle", Key.S, ModifierKeys.Shift);
        Add("Préférences", Key.OemComma, ModifierKeys.Control);
        foreach (var (label, key) in new[] { ("Vue à gauche", Key.Left), ("Vue à droite", Key.Right), ("Vue en haut", Key.Up), ("Vue en bas", Key.Down) }) { Add(label, key); Add(label + " (rapide)", key, ModifierKeys.Shift); }
        int[] steps = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512 };
        for (int i = 0; i < 10; i++) { Add("Grille " + steps[i] + " u", i == 9 ? Key.D0 : Key.D1 + i); Add("Grille " + steps[i] + " u (pavé)", i == 9 ? Key.NumPad0 : Key.NumPad1 + i); }
        return list.Select(a => a with { Default = a.Label switch
        {
            "Sélection de points" => new(Key.Z, ModifierKeys.None),
            "Sélection d’arêtes" => new(Key.X, ModifierKeys.None),
            "Cadrer" => new(Key.A, ModifierKeys.None),
            _ => a.Command
        } }).ToList();
    }
    public bool Translate(ref Key key, ref ModifierKeys modifiers)
    {
        var gesture = new Shortcut(key, modifiers); var action = Actions.FirstOrDefault(a => Bindings.GetValueOrDefault(a.Label) == gesture);
        if (action == null) return false;
        key = action.Command.Key; modifiers = action.Command.Modifiers; return true;
    }
    public static void Validate(Dictionary<string, Shortcut> bindings)
    {
        if (bindings.Count != Actions.Count || Actions.Any(a => !bindings.ContainsKey(a.Label)) || bindings.Values.Any(b => b == null || !Enum.IsDefined(b.Key) || b.Key is Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed or Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin || (b.Modifiers & ~(ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift)) != 0 || b.Key == Key.Space && b.Modifiers == ModifierKeys.None || b.Key == Key.F4 && b.Modifiers == ModifierKeys.Alt) || bindings.Values.Distinct().Count() != bindings.Count) throw new InvalidDataException(L.T("Raccourcis invalides, réservés ou en double."));
    }
    public void Save(string path) { Validate(Bindings); Language = L.Normalize(Language); ShortcutVersion = 1; Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); MainWindow.WriteAtomic(path, JsonSerializer.Serialize(this, Plan.JsonOptions)); }
    public static ShortcutSettings Load(string path)
    {
        if (!File.Exists(path)) return new();
        string json = File.ReadAllText(path); using var document = JsonDocument.Parse(json);
        var settings = document.RootElement.TryGetProperty("Bindings", out _) ? JsonSerializer.Deserialize<ShortcutSettings>(json) : new ShortcutSettings { Bindings = JsonSerializer.Deserialize<Dictionary<string, Shortcut>>(json)! };
        var bindings = settings?.Bindings ?? throw new InvalidDataException(L.T("Préférences vides."));
        if (settings!.ShortcutVersion == 0)
        {
            // Only migrate unchanged legacy defaults. A user binding always owns its key first.
            var upgrade = Actions.Where(a => a.Default != a.Command && bindings.GetValueOrDefault(a.Label) == a.Command).ToList();
            foreach (var action in upgrade) bindings.Remove(action.Label);
            foreach (var action in upgrade)
                bindings[action.Label] = !bindings.Values.Contains(action.Default) ? action.Default : action.Command;
            settings.ShortcutVersion = 1;
        }
        // Add new actions without discarding older user bindings or taking an occupied shortcut.
        foreach (var action in Actions.Where(a => !bindings.ContainsKey(a.Label)))
        {
            var candidate = action.Default;
            if (bindings.Values.Contains(candidate))
                candidate = Enum.GetValues<Key>().Where(k => k >= Key.F1 && k <= Key.F24).Select(k => new Shortcut(k, ModifierKeys.Control | ModifierKeys.Alt)).First(c => !bindings.Values.Contains(c));
            bindings[action.Label] = candidate;
        }
        Validate(bindings); settings!.Language = L.Normalize(settings.Language); return settings;
    }
}
public partial class MainWindow
{
    public ShortcutSettings Shortcuts { get; set; } = new();
    private static string PreferencesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Coat", "preferences.json");
    private readonly List<(MenuItem Item, Shortcut Default)> shortcutMenus = new();
    public void LoadPreferences()
    {
        try { Shortcuts = ShortcutSettings.Load(PreferencesPath); ChangeLanguage(Shortcuts.Language); }
        catch (Exception ex) { Notify(L.T("Préférences non chargées : ") + ex.Message); }
    }
    private static Shortcut? ParseHint(string text)
    {
        if (text == "") return null;
        if (Enum.TryParse<Key>(text, out var plainKey)) return new(plainKey, ModifierKeys.None);
        try { var g = (KeyGesture)new KeyGestureConverter().ConvertFromInvariantString(text.Replace("Maj", "Shift").Replace("Suppr", "Delete").Replace("Origine", "Home"))!; return new(g.Key, g.Modifiers); } catch { return null; }
    }
    private string CurrentHint(Shortcut original)
    {
        var action = ShortcutSettings.Actions.FirstOrDefault(a => a.Command == original);
        return action == null ? original.ToString() : Shortcuts.Bindings[action.Label].ToString();
    }
    private void RefreshShortcutHints()
    {
        foreach (var (item, original) in shortcutMenus) item.InputGestureText = CurrentHint(original);
        foreach (var (tool, button) in toolButtons)
        {
            var key = tool switch { EditorTool.Select => Key.V, EditorTool.Draw => Key.D, EditorTool.Erase => Key.E, EditorTool.Paint => Key.C, EditorTool.Shapes => Key.R, EditorTool.Annotation => Key.T, _ => Key.None };
            button.ToolTip = CurrentHint(new(key, ModifierKeys.None));
        }
        UpdateStatus();
    }
    public Window CreatePreferencesWindow(string? settingsPath = null)
    {
        var dialog = new Window { Owner = this, Title = L.T("Préférences"), Width = 680, Height = 740, MinWidth = 560, MinHeight = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brushes.White, Foreground = Brushes.Black };
        var root = new DockPanel { Margin = new Thickness(18), Background = Brushes.White }; dialog.Content = root;
        var languageRow = new StackPanel { Margin = new Thickness(0, 0, 0, 18) }; DockPanel.SetDock(languageRow, Dock.Top); root.Children.Add(languageRow);
        languageRow.Children.Add(new TextBlock { Text = L.T("Langue de l’interface"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        var language = new ComboBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8), ItemsSource = new[] { "English", "Français" }, SelectedIndex = L.Language == "fr" ? 1 : 0 };
        System.Windows.Automation.AutomationProperties.SetName(language, L.T("Langue de l’interface")); languageRow.Children.Add(language);
        languageRow.Children.Add(new TextBlock { Text = L.T("Le changement de langue s’applique à l’enregistrement, sans redémarrage."), Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap });
        languageRow.Children.Add(new TextBlock { Text = L.T("Raccourcis clavier"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 20, 0, 0) });
        var intro = new TextBlock { Text = L.T("Cliquez sur une touche, puis pressez la nouvelle combinaison.\nLes doublons sont refusés. Maj pour extruder, Ctrl+clic et Espace+glisser restent des gestes fixes."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(intro, Dock.Top); root.Children.Add(intro);
        var searchRow = new StackPanel { Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(searchRow, Dock.Top); root.Children.Add(searchRow);
        searchRow.Children.Add(new TextBlock { Text = L.T("Rechercher un raccourci"), Margin = new Thickness(0, 0, 0, 5) });
        var search = new TextBox { Padding = new Thickness(7), Background = Brushes.White, Foreground = Brushes.Black };
        System.Windows.Automation.AutomationProperties.SetName(search, L.T("Rechercher un raccourci")); searchRow.Children.Add(search);
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var message = new TextBlock { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) }; footer.Children.Add(message);
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; footer.Children.Add(buttons);
        var rows = new StackPanel(); root.Children.Add(new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var draft = new Dictionary<string, Shortcut>(Shortcuts.Bindings);
        void Populate()
        {
            rows.Children.Clear();
            var matches = ShortcutSettings.Actions.Where(a => string.IsNullOrWhiteSpace(search.Text) || L.ShortcutLabel(a.Label).Contains(search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase) || draft[a.Label].ToString().Contains(search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase));
            foreach (var category in matches.GroupBy(a => a.Category))
            {
                rows.Children.Add(new TextBlock { Text = L.T(category.Key), FontWeight = FontWeights.SemiBold, Foreground = Brushes.DimGray, Margin = new Thickness(0, 14, 0, 7) });
                foreach (var action in category)
                {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
                var capture = new Button { Content = draft[action.Label].ToString(), Width = 180, Padding = new Thickness(8), ToolTip = L.T("Cliquez puis pressez une combinaison") };
                bool capturing = false;
                capture.Click += (_, _) => { capturing = true; capture.Content = L.T("Pressez les touches…"); capture.Focus(); };
                capture.PreviewKeyDown += (_, e) =>
                {
                    if (!capturing) return;
                    e.Handled = true; var key = e.Key == Key.System ? e.SystemKey : e.Key;
                    if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt) return;
                    var candidate = new Shortcut(key, Keyboard.Modifiers); var old = draft[action.Label]; draft[action.Label] = candidate;
                    try { ShortcutSettings.Validate(draft); message.Text = ""; capture.Content = candidate.ToString(); }
                    catch (Exception ex) { draft[action.Label] = old; capture.Content = old.ToString(); message.Text = ex.Message; }
                    capturing = false;
                };
                capture.LostKeyboardFocus += (_, _) => { capturing = false; capture.Content = draft[action.Label].ToString(); };
                System.Windows.Automation.AutomationProperties.SetName(capture, L.ShortcutLabel(action.Label));
                DockPanel.SetDock(capture, Dock.Right); row.Children.Add(capture); row.Children.Add(new TextBlock { Text = L.ShortcutLabel(action.Label), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 10, 0) }); rows.Children.Add(row);
                }
            }
        }
        search.TextChanged += (_, _) => Populate();
        void AddButton(string title, Action action) { var b = new Button { Content = title, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(5) }; b.Click += (_, _) => action(); buttons.Children.Add(b); }
        AddButton(L.T("Par défaut"), () => { draft = new ShortcutSettings().Bindings; language.SelectedIndex = 0; message.Text = ""; Populate(); });
        AddButton(L.T("Ergonomique"), () => { draft = new ShortcutSettings().Bindings; message.Text = ""; Populate(); });
        AddButton(L.T("Classique"), () => { draft = ShortcutSettings.ClassicBindings(); message.Text = ""; Populate(); });
        AddButton(L.T("Annuler"), dialog.Close);
        AddButton(L.T("Enregistrer"), () => { try { var next = new ShortcutSettings { Bindings = draft, Language = language.SelectedIndex == 1 ? "fr" : "en" }; next.Save(settingsPath ?? PreferencesPath); Shortcuts = next; dialog.Close(); ChangeLanguage(next.Language); } catch (Exception ex) { message.Text = L.T("Enregistrement impossible : ") + ex.Message; } });
        Populate(); return dialog;
    }
    private void ShowPreferences() { Canvas.CancelGesture(); CreatePreferencesWindow().ShowDialog(); }
}

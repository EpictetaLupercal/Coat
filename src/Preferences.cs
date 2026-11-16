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
    public override string ToString() => Key == Key.None ? L.T("Non attribué") : (Modifiers.HasFlag(ModifierKeys.Control) ? "Ctrl+" : "") + (Modifiers.HasFlag(ModifierKeys.Shift) ? L.T("Maj+") : "") + (Modifiers.HasFlag(ModifierKeys.Alt) ? "Alt+" : "") + L.KeyName(Key);
}
public record ShortcutAction(string Label, Shortcut Command, Shortcut Default)
{
    public string Category => Label.StartsWith("Grille ") ? "Grille" : Label switch
    {
        "Nouveau" or "Ouvrir" or "Enregistrer" or "Enregistrer sous" or "Importer une image" or "Exporter en PNG…" or "Exporter en SVG…" or "Exporter en .map…" or "Quitter" or "Charger le plan d’exemple" or "Ouvert récemment" or "Effacer la liste" => "Fichiers",
        "Sélection" or "Dessin" or "Gomme" or "Coloriage" or "Shapes" or "Annotations" or "Rectangle" or "Cercle" or "Escaliers droits" or "Escaliers en L" or "Escaliers en U" or "Escaliers en colimaçon" or "Aucun outil" => "Outils",
        "Sélection de points" or "Sélection d’arêtes" or "Sélection de zones" or "Translation" or "Rotation" or "Échelle" or "Sélection sur plusieurs niveaux" or "Organisation" or "Renommer le groupe" or "Sélectionner le groupe suivant" => "Sélection et transformations",
        "Aide" or "Préférences" => "Interface",
        _ when Label.StartsWith("Vue ") || Label.StartsWith("Niveau") || Label is "Cadrer" or "Grille visible" or "Longueurs visibles" => "Navigation et affichage",
        _ => "Édition"
    };
}
public class ShortcutSettings
{
    internal static readonly string[] FeatureLabels = {
        "Dissoudre les sommets", "Dissoudre les arêtes", "Jeton", "Dégradé", "Remplissage", "Double flèche", "Appliquer les flèches", "Appliquer le dégradé",
        "Accrochage : sommets, milieux et grille", "Flèche au début", "Flèche à la fin", "Triangle", "Ouverte", "Diamant", "Couleur de fin…", "Appliquer le jeton", "Supprimer le jeton",
        "Exporter en PNG…", "Exporter en SVG…", "Exporter en .map…", "Charger le plan d’exemple", "Quitter", "Revenir à l’origine", "À propos de Coat",
        "Supprimer ce niveau", "Appliquer la hauteur", "Affecter", "Sélection sur plusieurs niveaux", "Organisation", "Renommer le groupe", "Sélectionner le groupe suivant",
        "Rectangle", "Cercle", "Escaliers droits", "Escaliers en L", "Escaliers en U", "Escaliers en colimaçon", "Tourne à gauche", "Tourne à droite",
        "Remplir une zone", "Colorer les lignes", "Lignes continues", "Tirets longs", "Tirets courts", "Remplissage uni", "Remplissage à bandes", "Remplissage à points",
        "◉  Roue de couleurs…", "Utiliser cette couleur", "Colorer la sélection", "Définir la couleur des murs", "Couleur des murs…", "Appliquer les coordonnées", "Déplacer la sélection", "Extruder avec ce décalage", "Appliquer la rotation", "Appliquer l’échelle", "Échelle uniforme",
        "Appliquer le texte", "Supprimer le texte", "Appliquer à l’image", "Retirer l’image", "Afficher sous la grille", "Ouvert récemment", "Effacer la liste", "Zoom avant", "Zoom arrière", "Aucun outil",
        "Modifier la hauteur", "Modifier le nombre de points", "Modifier le nombre de marches", "Modifier l’angle", "Modifier la largeur"
    };
    public static readonly List<ShortcutAction> Actions = CreateActions();
    public Dictionary<string, Shortcut> Bindings { get; set; } = UnassignedBindings();
    public string Language { get; set; } = "en";
    public int ShortcutVersion { get; set; }
    public static readonly Shortcut Unassigned = new(Key.None, ModifierKeys.None);
    public static Dictionary<string, Shortcut> UnassignedBindings() => Actions.ToDictionary(a => a.Label, a => a.Label == "Confirmer" ? new Shortcut(Key.Enter, ModifierKeys.None) : new Shortcut(Key.None, ModifierKeys.None));
    public static Dictionary<string, Shortcut> ErgonomicBindings() => Actions.ToDictionary(a => a.Label, a => a.Default);
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
        foreach (var label in FeatureLabels) list.Add(new(label, new(Key.None, ModifierKeys.None), new(Key.None, ModifierKeys.None)));
        list.Add(new("Confirmer", new(Key.Enter, ModifierKeys.None), new(Key.Enter, ModifierKeys.None)));
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
        var gesture = new Shortcut(key, modifiers); var action = Actions.FirstOrDefault(a => gesture.Key != Key.None && Bindings.GetValueOrDefault(a.Label) == gesture);
        if (action == null) return false;
        key = action.Command.Key; modifiers = action.Command.Modifiers; return true;
    }
    public static void Validate(Dictionary<string, Shortcut> bindings)
    {
        if (bindings.Count != Actions.Count || Actions.Any(a => !bindings.ContainsKey(a.Label))) throw new InvalidDataException(L.T("Raccourcis invalides, réservés ou en double."));
        var assigned = bindings.Where(p => p.Value != Unassigned).ToList();
        if (assigned.Any(p => p.Value == null || !Enum.IsDefined(p.Value.Key) || p.Value.Key is Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed or Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin || (p.Value.Modifiers & ~(ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift)) != 0 || p.Value.Key == Key.Space && p.Value.Modifiers == ModifierKeys.None || p.Value.Key == Key.F4 && p.Value.Modifiers == ModifierKeys.Alt)) throw new InvalidDataException(L.T("Raccourcis invalides, réservés ou en double."));
        var conflict = assigned.GroupBy(p => p.Value).FirstOrDefault(g => g.Count() > 1);
        if (conflict != null) throw new InvalidDataException(L.T("Conflit de raccourci : ") + string.Join(" / ", conflict.Select(p => L.ShortcutLabel(p.Key))));
    }

    public void Save(string path) { Validate(Bindings); Language = L.Normalize(Language); ShortcutVersion = 2; Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); MainWindow.WriteAtomic(path, JsonSerializer.Serialize(this, Plan.JsonOptions)); }
    public static ShortcutSettings Load(string path)
    {
        if (!File.Exists(path)) return new();
        string json = File.ReadAllText(path); using var document = JsonDocument.Parse(json);
        var settings = document.RootElement.TryGetProperty("Bindings", out _) ? JsonSerializer.Deserialize<ShortcutSettings>(json) : new ShortcutSettings { Bindings = JsonSerializer.Deserialize<Dictionary<string, Shortcut>>(json)! };
        var bindings = settings?.Bindings ?? throw new InvalidDataException(L.T("Préférences vides."));
        // Never migrate a user's keys or invent shortcuts for newly introduced commands.
        foreach (var action in Actions.Where(a => !bindings.ContainsKey(a.Label)))
            bindings[action.Label] = action.Label == "Confirmer" && !bindings.Values.Contains(new Shortcut(Key.Enter, ModifierKeys.None)) ? new(Key.Enter, ModifierKeys.None) : Unassigned;
        settings!.ShortcutVersion = 2;
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
        try { Shortcuts = ShortcutSettings.Load(PreferencesPath); ChangeLanguage(Shortcuts.Language); LoadRecent(); }
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
        return action == null ? original.ToString() : Shortcuts.Bindings[action.Label].Key == Key.None ? "" : Shortcuts.Bindings[action.Label].ToString();
    }
    private void RefreshShortcutHints()
    {
        foreach (var (item, original) in shortcutMenus) item.InputGestureText = CurrentHint(original);
        foreach (var (tool, button) in toolButtons)
        {
            var key = tool switch { EditorTool.Select => Key.V, EditorTool.Draw => Key.D, EditorTool.Erase => Key.E, EditorTool.Paint => Key.C, EditorTool.Shapes => Key.R, EditorTool.Annotation => Key.T, _ => Key.None };
            button.ToolTip = CurrentHint(new(key, ModifierKeys.None));
        }
        foreach (var pair in featureMenus) { var key = Shortcuts.Bindings[pair.Value]; pair.Key.InputGestureText = key.Key == Key.None ? "" : key.ToString(); }
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
        var intro = new TextBlock { Text = L.T("Les raccourcis sont facultatifs. Cliquez sur une touche pour la modifier, ou sur × pour la supprimer. Les conflits sont refusés. Les nouveaux raccourcis restent non attribués."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(intro, Dock.Top); root.Children.Add(intro);
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
                var clear = new Button { Content = "×", Width = 28, Margin = new Thickness(4, 0, 0, 0), ToolTip = L.T("Effacer ce raccourci") };
                clear.Click += (_, _) => { draft[action.Label] = ShortcutSettings.Unassigned; message.Text = ""; Populate(); };
                DockPanel.SetDock(clear, Dock.Right); row.Children.Add(clear);
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
        void AddButton(string title, Action action) { var b = new Button { Content = title, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(5), IsDefault = title == L.T("Enregistrer"), IsCancel = title == L.T("Annuler") }; b.Click += (_, _) => action(); buttons.Children.Add(b); }
        AddButton(L.T("Par défaut"), () => { draft = new ShortcutSettings().Bindings; language.SelectedIndex = 0; message.Text = ""; Populate(); });
        AddButton(L.T("Ergonomique"), () => { draft = ShortcutSettings.ErgonomicBindings(); message.Text = ""; Populate(); });
        AddButton(L.T("Classique"), () => { draft = ShortcutSettings.ClassicBindings(); message.Text = ""; Populate(); });
        AddButton(L.T("Annuler"), dialog.Close);
        AddButton(L.T("Enregistrer"), () => { try { var next = new ShortcutSettings { Bindings = draft, Language = language.SelectedIndex == 1 ? "fr" : "en" }; next.Save(settingsPath ?? PreferencesPath); Shortcuts = next; dialog.Close(); ChangeLanguage(next.Language); } catch (Exception ex) { message.Text = L.T("Enregistrement impossible : ") + ex.Message; } });
        Populate(); return dialog;
    }
    private void ShowPreferences() { Canvas.CancelGesture(); CreatePreferencesWindow().ShowDialog(); }
}

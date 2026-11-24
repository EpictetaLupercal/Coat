// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Coat;

internal static class EditorLayoutTests
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var results = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            results.Add("PASS: " + name);
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow { TestMode = true, ShowInTaskbar = false, Width = 1050, Height = 680 };
        var canvas = window.Canvas;
        window.Shortcuts.AnimatedMenus = false;
        window.ChangeLanguage("en");
        window.Show();
        var plan = new Plan();
        plan.EnsureLevel(1);
        plan.Polygon(new V2[] { new(0, 0), new(128, 0), new(128, 128), new(0, 128) });
        plan.Annotations.Add(new Annotation { Id = plan.NextId++, X = 190, Y = 80, IsToken = true });
        window.LoadDocument(plan, "");
        void Layout() => InterfaceLayoutTests.Layout(window);
        IEnumerable<T> Children<T>() where T : DependencyObject => InterfaceLayoutTests.Children<T>(window);
        double X(Button button) => button.TranslatePoint(new Point(), window).X;
        bool InToolbar(Button button) => button.Content is System.Windows.Shapes.Path && button.TranslatePoint(new Point(), window).Y < 86;
        Button Command(string label, bool toolbar = false) => Children<Button>().Single(button =>
            AutomationProperties.GetName(button) == L.T(label) && InToolbar(button) == toolbar);
        void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void ClickMenu(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Expander Preview() => Children<Expander>().Single(e => Equals(e.Header, L.T("Aperçu des niveaux")));

        foreach (string language in new[] { "en", "fr" })
        {
            window.ChangeLanguage(language);
            Layout();
            var toolbar = Children<Button>().Where(InToolbar).ToArray();
            Check(toolbar.Length == 11 && toolbar.All(b => b.IsVisible && b.ActualWidth >= 30 && X(b) + b.ActualWidth <= window.ActualWidth),
                $"All toolbar icons fit at minimum width in {language}");
            Check(X(Command("Dessin", true)) < X(Command("Cotation", true)) && X(Command("Cotation", true)) < X(Command("Gomme", true)) &&
                X(Command("Miroir", true)) < X(Command("Dessin", true)) && X(Command("Shapes", true)) < X(Command("Texte", true)),
                $"The toolbar groups edit, creation and annotation commands in {language}");
            window.SetTool(EditorTool.Select); Layout();
            foreach (var (label, mode) in new[] { ("Sélection de points", SelectionMode.Vertex), ("Sélection d’arêtes", SelectionMode.Edge), ("Sélection de zones", SelectionMode.Face) })
            {
                Click(Command(label));
                Check(canvas.Tool == EditorTool.Select && canvas.Mode == mode, $"The {mode} contextual button activates its selection mode in {language}");
            }
            Click(Command("Plan")); Layout();
            Check(Children<LevelPreview>().Single().IsVisible && Children<TextBox>().Any(b => AutomationProperties.GetName(b) == L.T("Hauteur du niveau")) &&
                Children<TextBlock>().Any(t => t.Text == L.T("IMAGE DE RÉFÉRENCE")), $"The Plan tab contains the level preview, height and reference image in {language}");
            Check(Children<Button>().Where(b => !InToolbar(b) && new[] { L.T("Plan"), L.T("Sélection"), L.T("Modificateurs") }.Contains(AutomationProperties.GetName(b)))
                .Count(b => b.IsVisible && X(b) + b.ActualWidth <= window.ActualWidth) == 3, $"All three inspector tabs fit in {language}");
        }

        window.ChangeLanguage("en"); Layout();
        Preview().IsExpanded = false;
        Click(Command("Sélection")); Layout();
        Check(!Children<LevelPreview>().Any() && Command("Sélection").IsVisible && Children<TextBlock>().Any(t => t.Text == L.T("Aucune sélection")),
            "The Selection tab displays selection settings without the level preview");
        Click(Command("Modificateurs")); Layout();
        Check(canvas.Tool == EditorTool.Modifiers && Children<ComboBox>().Any(b => AutomationProperties.GetName(b) == L.T("Opération booléenne")),
            "Modifiers opens the Boolean controls");
        Click(Command("Plan")); Layout();
        Check(!Preview().IsExpanded && canvas.Tool == EditorTool.Select, "The preview stays collapsed between tabs and leaving Modifiers restores Select");
        Preview().IsExpanded = true;
        Layout(); MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-inspector-plan.png"));
        Click(Command("Sélection")); Layout();

        ContextMenu SelectionMenu(V2 point) => canvas.BuildSelectionMenu(point);
        MenuItem Item(ContextMenu menu, string label) => menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, L.T(label)));
        var menu = SelectionMenu(new(64, 64));
        Check(canvas.SelectedFaces.Count == 1 && Item(menu, "Grouper").IsEnabled && Item(menu, "Miroir").IsEnabled &&
            Item(menu, "Isoler la sélection").InputGestureText == "/" && Item(menu, "Dissoudre les arêtes").IsEnabled,
            "Right-click picks a face and exposes grouping, mirror, isolation and existing edit commands");
        ClickMenu(Item(menu, "Grouper"));
        menu = SelectionMenu(new(64, 64));
        Check(plan.Groups.Count == 1 && Item(menu, "Dissocier").IsEnabled, "Group is applied and enables Ungroup");
        ClickMenu(Item(menu, "Isoler la sélection"));
        Check(canvas.IsSelectionFocused, "The context menu isolates the current selection");
        ClickMenu(Item(SelectionMenu(new(64, 64)), "Quitter le mode Focus"));
        Check(!canvas.IsSelectionFocused, "The context menu exits the current isolation");
        var move = Item(SelectionMenu(new(64, 64)), "Déplacer vers le niveau");
        var choices = move.Items.OfType<MenuItem>().ToArray();
        Check(!choices[0].IsEnabled && choices[1].IsEnabled && choices[^1].IsEnabled, "Move to Level offers valid existing and new destinations");
        ClickMenu(choices[1]);
        Check(plan.ActiveLevel == 1 && plan.Faces().Single().Level == 1, "The level submenu moves the selected face");
        ClickMenu(Item(SelectionMenu(new(64, 64)), "Miroir"));
        Check(canvas.Tool == EditorTool.Mirror && canvas.SelectionPoints().Count > 0, "Mirror opens with the selection intact");
        window.SetTool(EditorTool.Select); window.ChangeLevel(0); canvas.ClearSelection();
        menu = SelectionMenu(new(190, 80));
        Check(canvas.SelectedAnnotations.Count == 1 && Item(menu, "Miroir").IsEnabled && !Item(menu, "Grouper").IsEnabled,
            "Right-click can target a token and disables grouping for annotations");

        window.SetTool(EditorTool.Draw);
        canvas.PointerDown(canvas.Screen(new(0, 0)), MouseButton.Left);
        canvas.PointerUp(MouseButton.Left);
        Check(canvas.Anchor != null, "The drawing gesture starts normally");
        canvas.PointerDown(canvas.Screen(new(0, 0)), MouseButton.Right);
        canvas.PointerUp(MouseButton.Right);
        Check(canvas.Anchor == null && canvas.Tool == EditorTool.Draw, "Right-click ends drawing and preserves the Draw tool");
        Check(L.Missing.Count == 0, "The updated editor interface is localized");

        window.Close(); app.Shutdown();
        File.WriteAllLines(Path.Combine(output, "results.txt"), results.Append($"\n{results.Count} checks passed."));
    }
}

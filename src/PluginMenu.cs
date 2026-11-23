// Coat — Epicteta Lupercal
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MoonSharp.Interpreter;

namespace Coat;

public partial class MainWindow
{
    private bool pluginRunning;
    internal string PluginFolder { get; set; } = Path.Combine(AppContext.BaseDirectory, "plugins");

    private MenuItem BuildPluginsMenu()
    {
        var menu = Menu(L.T("Plugins"));
        RefreshPluginsMenu(menu);
        menu.SubmenuOpened += (_, e) =>
        {
            if (e.Source == menu) RefreshPluginsMenu(menu);
        };
        return menu;
    }

    private void RefreshPluginsMenu(MenuItem menu)
    {
        menu.Items.Clear();
        PluginAction(menu, L.T("Ouvrir le dossier des plugins…"), () =>
        {
            try
            {
                Directory.CreateDirectory(PluginFolder);
                Process.Start(new ProcessStartInfo { FileName = PluginFolder, UseShellExecute = true });
            }
            catch (Exception ex) { Error(L.T("Dossier des plugins inaccessible"), ex); }
        });
        menu.Items.Add(new Separator());

        void Message(string title) => menu.Items.Add(new MenuItem { Header = title, IsEnabled = false });
        try
        {
            var plugins = LuaPlugin.Discover(PluginFolder);
            if (plugins.Count == 0) Message(L.T("Aucun plugin Lua trouvé"));
            foreach (var plugin in plugins)
            {
                var item = DropDownItem(plugin.Name.Replace("_", "__"));
                item.ToolTip = plugin.Path;
                PluginAction(item, L.T("Lire le code…"), () => ViewLuaSource(plugin));
                var run = DropDownItem(L.T("Exécuter le plugin"));
                run.IsEnabled = !pluginRunning;
                run.Click += async (_, _) => await RunPlugin(plugin);
                item.Items.Add(run);
                menu.Items.Add(item);
            }
        }
        catch (IOException) { Message(L.T("Dossier des plugins inaccessible")); }
        catch (UnauthorizedAccessException) { Message(L.T("Dossier des plugins inaccessible")); }
    }

    private static void PluginAction(MenuItem parent, string title, Action action)
    {
        var item = DropDownItem(title);
        item.Click += (_, _) => action();
        parent.Items.Add(item);
    }

    private void ViewLuaSource(LuaPlugin plugin)
    {
        try { ShowPluginText(plugin.Name, plugin.ReadSource(), wrap: false); }
        catch (Exception ex) { Error(L.T("Impossible de lire le plugin Lua"), ex); }
    }

    internal async Task RunPlugin(LuaPlugin plugin)
    {
        if (pluginRunning) return;
        pluginRunning = true;
        try
        {
            Canvas.CancelGesture();
            var document = Document;
            var before = document.Serialize();
            var info = LuaPlanInfo.Capture(document);
            var source = plugin.ReadSource();
            Notify(L.F($"Exécution de {plugin.Name}…"));
            var result = await Task.Run(() => CoatLua.Run(source, plugin.Name, info));
            if (!IsLoaded) return;
            if (result.Rectangles.Count != 0 && (document != Document || before != Document.Serialize()))
                throw new InvalidOperationException(L.T("Le plan a changé pendant l’exécution du plugin. Relancez-le pour appliquer ses modifications."));
            var updated = CoatLua.Apply(before, result);
            if (updated is not null && updated.Serialize() != before)
                Mutate(() => Document = updated);
            Notify(L.F($"Plugin terminé : {plugin.Name}"));
            if (result.Output.Length > 0 && !TestMode)
                ShowPluginText(L.F($"Sortie de {plugin.Name}"), result.Output, wrap: true);
        }
        catch (InterpreterException ex)
        {
            Notify(L.T("Erreur de script Lua"));
            if (TestMode) throw;
            if (IsLoaded) ShowPluginText(plugin.Name, ex.DecoratedMessage ?? ex.Message, wrap: true);
        }
        catch (Exception ex)
        {
            Notify(L.T("Erreur de script Lua"));
            if (TestMode) throw;
            if (IsLoaded) Error(L.T("Impossible d’exécuter le plugin Lua"), ex);
        }
        finally { pluginRunning = false; }
    }

    private void ShowPluginText(string title, string text, bool wrap)
    {
        var display = new TextBox
        {
            Text = text, IsReadOnly = true, AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas"), Padding = new Thickness(12),
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto
        };
        var close = new Button { Content = L.T("Fermer"), IsCancel = true, MinWidth = 92, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var panel = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(close, Dock.Bottom);
        panel.Children.Add(close);
        panel.Children.Add(display);
        var dialog = new Window
        {
            Title = title, Owner = this, Content = panel, Resources = Resources,
            Width = 780, Height = 500, MinWidth = 360, MinHeight = 260, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Background, Foreground = Foreground
        };
        close.Click += (_, _) => dialog.Close();
        dialog.ShowDialog();
    }
}

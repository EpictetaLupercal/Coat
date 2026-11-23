// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MoonSharp.Interpreter;

namespace Coat;

internal static class LuaPluginTests
{
    internal static void Run(MainWindow window, Action<bool,string> check, string output)
    {
        var folder = Path.Combine(output, "plugins");
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, "Pack"));
        File.WriteAllText(Path.Combine(folder, "Notes.txt"), "Not a plugin");
        File.WriteAllText(Path.Combine(folder, "Pack", "main.lua"), "print('pack')");
        var rectangle = Path.Combine(folder, "Rectangle.lua");
        File.WriteAllText(rectangle, "coat.add_rectangle(0, 0, 128, 96); print('rectangle')");
        var plugins = LuaPlugin.Discover(folder);
        check(plugins.Select(plugin => plugin.Name).SequenceEqual(new[] {"Pack", "Rectangle"}),
            "Plugin catalog discovers standalone Lua files and addon entry points only");
        var plan = new Plan { ActiveLevel = 2 };
        plan.EnsureLevel(2);
        var info = LuaPlanInfo.Capture(plan);
        var result = CoatLua.Run("assert(io == nil and os == nil and require == nil and loadfile == nil); print(coat.levels[2].z, coat.active_level, coat.grid)",
            "test.lua", info);
        check(result.Output.Contains("384") && result.Output.Contains("32") && result.Rectangles.Count == 0,
            "Lua reads real plan data without file or system commands");
        void Fails(string code, string name)
        {
            bool failed = false;
            try { CoatLua.Run(code, name, info); }
            catch (InterpreterException) { failed = true; }
            check(failed, name);
        }
        Fails("while true do end", "Lua interrupts endless loops");
        Fails("coat.add_rectangle(0, 0, -2, 4)", "Invalid rectangle dimensions abort the script");

        window.LoadDocument(plan, "");
        window.PluginFolder = folder;
        window.ChangeLanguage("en");
        InterfaceLayoutTests.Layout(window);
        var menu = InterfaceLayoutTests.Children<Menu>(window).Single().Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Plugins"));
        var command = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Rectangle"));
        check(command.Items.OfType<MenuItem>().Select(item => item.Header).SequenceEqual(new[] {"View Source…", "Run Plugin"}) &&
            plan.Edges.Count == 0, "The Plugins menu offers source and run commands without executing anything on discovery");
        File.WriteAllText(Path.Combine(folder, "New.lua"), "print('found')");
        menu.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent, menu));
        check(menu.Items.OfType<MenuItem>().Any(item => Equals(item.Header, "New")), "Opening the menu finds new files without restarting");

        void RunAndWait(LuaPlugin plugin)
        {
            var task = window.Dispatcher.InvokeAsync(() => window.RunPlugin(plugin)).Task.Unwrap();
            if (!task.IsCompleted)
            {
                var frame = new DispatcherFrame();
                _ = task.ContinueWith(_ => window.Dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
                Dispatcher.PushFrame(frame);
            }
            task.GetAwaiter().GetResult();
        }

        string original = window.Document.Serialize();
        RunAndWait(plugins[0]);
        check(window.Document.Serialize() == original && !window.History.CanUndo, "Read-only plugin runs leave the document and undo history intact");
        RunAndWait(plugins[1]);
        check(window.Document.Edges.Count == 4 && window.Document.Vertices.All(point => point.Level == 2),
            "A successful plugin creates an outline on the active level through Coat's document model");
        window.Undo();
        check(window.Document.Serialize() == original, "One undo restores the entire plugin edit");
        File.WriteAllText(rectangle, "coat.add_rectangle(0, 0, 128, 96); error('abort')");
        bool aborted = false;
        try { RunAndWait(plugins[1]); }
        catch (InterpreterException) { aborted = true; }
        check(aborted && window.Document.Serialize() == original && !window.History.CanUndo, "A Lua error discards queued edits");
        check(L.Missing.Count == 0, "Plugin menu and messages are localized");
    }
}

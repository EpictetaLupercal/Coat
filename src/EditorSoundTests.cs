// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Coat;

internal static class EditorSoundTests
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
        var window = new MainWindow { TestMode = true, ShowInTaskbar = false };
        window.Shortcuts.AnimatedMenus = false;
        window.ChangeLanguage("en");
        window.Show();
        InterfaceLayoutTests.Layout(window);
        var requested = new List<EditorSoundCue>();
        window.EditorSoundRequested += requested.Add;
        var canvas = window.Canvas;
        canvas.Center = new V2();
        canvas.Zoom = 1;
        void Click(V2 point, MouseButton button = MouseButton.Left)
        {
            canvas.PointerDown(canvas.Screen(point), button);
            canvas.PointerUp(button);
        }

        window.SetTool(EditorTool.Draw);
        Click(new(0, 0));
        Click(new(0, 0));
        Click(new(128, 0));
        Click(new(128, 0), MouseButton.Right);
        Check(requested.SequenceEqual(new[] { EditorSoundCue.Drawing, EditorSoundCue.Drawing }) && window.Document.Edges.Count == 1,
            "Draw sounds play once at the anchor and once at the new point, with no duplicate or cancel sound");
        window.SetTool(EditorTool.Select);
        canvas.Mode = SelectionMode.Edge;
        requested.Clear();
        Click(new(64, 0));
        Click(new(64, 0));
        Click(new(0, 128));
        Check(requested.SequenceEqual(new[] { EditorSoundCue.Selection }), "Selecting an edge sounds once; regrabbing or clicking empty space is silent");
        requested.Clear();
        canvas.PointerDown(canvas.Screen(new(-48, 48)), MouseButton.Left);
        canvas.PointerMove(canvas.Screen(new(176, -48)));
        canvas.PointerUp(MouseButton.Left);
        Check(requested.SequenceEqual(new[] { EditorSoundCue.Selection }), "Marquee selection plays a single sound");
        requested.Clear();
        window.SetTool(EditorTool.Shapes);
        Click(new(192, 64));
        Click(new(320, 192));
        Check(requested.SequenceEqual(new[] { EditorSoundCue.Drawing }), "A completed shape plays one drawing sound");
        window.SetTool(EditorTool.Select);

        var settingsPath = Path.Combine(output, "sound-preferences.json");
        File.WriteAllText(settingsPath, System.Text.Json.JsonSerializer.Serialize(window.Shortcuts.Bindings, Plan.JsonOptions));
        Check(ShortcutSettings.Load(settingsPath).Audio.Volume == 50 && ShortcutSettings.Load(settingsPath).Audio.For(EditorSoundCue.Selection).Enabled,
            "Existing preferences load with the built-in sound defaults");
        var dialog = window.CreateSoundPreferencesWindow(settingsPath);
        dialog.Show();
        InterfaceLayoutTests.Layout(dialog);
        InterfaceLayoutTests.Children<Slider>(dialog).Single().Value = 35;
        InterfaceLayoutTests.Children<CheckBox>(dialog).Single(c => Equals(c.Content, "Scene selection")).IsChecked = false;
        InterfaceLayoutTests.Children<Button>(dialog).Single(b => b.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var reloaded = ShortcutSettings.Load(settingsPath);
        Check(reloaded.Audio.Volume == 35 && !reloaded.Audio.For(EditorSoundCue.Selection).Enabled && reloaded.Audio.For(EditorSoundCue.Drawing).Enabled,
            "The sound dialog saves volume and independent cue switches");
        canvas.ClearSelection();
        requested.Clear();
        Click(new(64, 0));
        window.SetTool(EditorTool.Draw);
        Click(new(0, 128));
        Check(requested.SequenceEqual(new[] { EditorSoundCue.Drawing }), "Switching off selection leaves drawing audible");
        window.Shortcuts.Audio.Volume = 0;
        requested.Clear();
        Click(new(128, 128));
        Check(requested.Count == 0, "Zero volume suppresses cue playback requests");

        var custom = Path.Combine(output, "custom.wav");
        var invalid = Path.Combine(output, "invalid.wav");
        File.Copy(EditorSoundLibrary.DefaultFile("drawing.wav"), custom, true);
        File.WriteAllText(invalid, "Not a WAV file");
        reloaded.Audio.Cues[EditorSoundCue.Drawing] = new(true, custom);
        reloaded.Save(settingsPath);
        Check(ShortcutSettings.Load(settingsPath).Audio.For(EditorSoundCue.Drawing).File == custom, "The custom WAV path survives save and reload");
        window.Shortcuts = ShortcutSettings.Load(settingsPath);
        dialog = window.CreateSoundPreferencesWindow(settingsPath);
        dialog.Show();
        InterfaceLayoutTests.Layout(dialog);
        string drawingLabel = L.T(EditorSoundLibrary.Cues.Single(c => c.Cue == EditorSoundCue.Drawing).Label);
        InterfaceLayoutTests.Children<Button>(dialog).Single(b => AutomationProperties.GetName(b) == "Built-in sound · " + drawingLabel)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        InterfaceLayoutTests.Children<Button>(dialog).Single(b => b.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(ShortcutSettings.Load(settingsPath).Audio.For(EditorSoundCue.Drawing).File == null, "The sound dialog can restore the bundled drawing sound");

        using var verifiedSelection = EditorSounds.Create(null, "selection.wav", 0);
        using var verifiedPing = EditorSounds.Create(null, "ping.wav", 0);
        using var verifiedCustom = EditorSounds.Create(custom, "drawing.wav", 0);
        using var verifiedMissing = EditorSounds.Create(Path.Combine(output, "missing.wav"), "drawing.wav", 0);
        using var verifiedInvalid = EditorSounds.Create(invalid, "drawing.wav", 0);
        var frame = new DispatcherFrame();
        var limit = DateTime.UtcNow.AddSeconds(8);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            if (!(verifiedSelection.IsReady && verifiedPing.IsReady && verifiedCustom.IsReady && verifiedMissing.IsReady && verifiedInvalid.IsReady) && DateTime.UtcNow < limit) return;
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        Check(verifiedSelection.IsReady && verifiedCustom.IsReady && verifiedCustom.Source?.LocalPath == custom, "WPF decodes both the bundled and the custom WAV");
        Check(verifiedPing.IsReady, "WPF decodes the bundled map ping");
        var drawing = EditorSoundLibrary.DefaultFile("drawing.wav");
        Check(verifiedMissing.IsReady && verifiedMissing.Source?.LocalPath == drawing && verifiedInvalid.IsReady && verifiedInvalid.Source?.LocalPath == drawing,
            "Missing and unreadable custom sounds fall back to the bundled WAV");
        window.Close();
        app.Shutdown();
        File.WriteAllLines(Path.Combine(output, "results.txt"), results.Append($"\n{results.Count} checks passed."));
    }
}

// Coat — Epicteta Lupercal
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Coat;

public partial class MainWindow
{
    private readonly EditorSounds editorSounds = new();
    internal event Action<EditorSoundCue>? EditorSoundRequested;

    internal void PlayEditorSound(EditorSoundCue cue)
    {
        if (Shortcuts.Audio.Volume <= 0 || !Shortcuts.Audio.For(cue).Enabled) return;
        EditorSoundRequested?.Invoke(cue);
        if (!TestMode) editorSounds.Play(cue);
    }

    private void ShowSoundPreferences() => CreateSoundPreferencesWindow().ShowDialog();

    internal Window CreateSoundPreferencesWindow(string? settingsPath = null)
    {
        var draft = Shortcuts.Audio.Copy();
        var dialog = new Window
        {
            Owner = this, Title = L.T("Sons de l’éditeur"), Width = 520, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = MenuBack, Foreground = MenuInk
        };
        if (IsBordeaux) dialog.Resources = Resources;
        var panel = new StackPanel { Margin = new Thickness(20) };
        dialog.Content = panel;
        var volumeLabel = new TextBlock { FontWeight = FontWeights.SemiBold };
        panel.Children.Add(volumeLabel);
        var volume = new Slider { Minimum = 0, Maximum = 100, SmallChange = 1, LargeChange = 10, Value = Math.Clamp(draft.Volume, 0, 100) };
        AutomationProperties.SetName(volume, L.T("Volume des sons"));
        panel.Children.Add(volume);
        void RefreshVolume()
        {
            draft.Volume = (int)Math.Round(volume.Value);
            volumeLabel.Text = L.F($"Volume des sons · {draft.Volume} %");
        }
        volume.ValueChanged += (_, _) => RefreshVolume();
        RefreshVolume();

        EditorSounds.SoundClip? preview = null;
        dialog.Closed += (_, _) => preview?.Dispose();
        foreach (var (cue, label, defaultFile) in EditorSoundLibrary.Cues)
        {
            var choice = draft.For(cue);
            var toggle = new CheckBox { Content = L.T(label), IsChecked = choice.Enabled, Margin = new Thickness(0, 12, 0, 4) };
            panel.Children.Add(toggle);
            toggle.Checked += (_, _) => draft.Cues[cue] = draft.For(cue) with { Enabled = true };
            toggle.Unchecked += (_, _) => draft.Cues[cue] = draft.For(cue) with { Enabled = false };
            var filename = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
            panel.Children.Add(filename);
            void RefreshFile()
            {
                var file = draft.For(cue).File;
                filename.Text = file == null ? L.T("Son intégré") : Path.GetFileName(file);
                filename.ToolTip = file;
            }
            RefreshFile();
            var actions = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            panel.Children.Add(actions);
            ButtonIn(actions, "Écouter", () =>
            {
                preview?.Dispose();
                preview = EditorSounds.Create(draft.For(cue).File, defaultFile, draft.Volume);
                preview.Play();
            });
            ButtonIn(actions, "Choisir un WAV…", () =>
            {
                var picker = new OpenFileDialog { Filter = L.T("Son WAV (*.wav)|*.wav"), CheckFileExists = true };
                if (picker.ShowDialog(dialog) != true) return;
                draft.Cues[cue] = draft.For(cue) with { File = picker.FileName };
                RefreshFile();
            });
            var restore = ButtonIn(actions, "Son intégré", () =>
            {
                draft.Cues[cue] = draft.For(cue) with { File = null };
                RefreshFile();
            });
            AutomationProperties.SetName(restore, L.T("Son intégré") + " · " + L.T(label));
        }

        var message = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(message);
        var footer = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        panel.Children.Add(footer);
        ButtonIn(footer, "Annuler", dialog.Close).IsCancel = true;
        ButtonIn(footer, "Enregistrer", () =>
        {
            var previous = Shortcuts.Audio;
            Shortcuts.Audio = draft;
            try { Shortcuts.Save(settingsPath ?? PreferencesPath); }
            catch (Exception ex)
            {
                Shortcuts.Audio = previous;
                message.Text = L.T("Enregistrement impossible : ") + ex.Message;
                return;
            }
            if (!TestMode) editorSounds.Configure(draft);
            dialog.Close();
        }).IsDefault = true;
        return dialog;

        static Button ButtonIn(Panel parent, string label, Action action)
        {
            var button = new Button { Content = L.T(label), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 5, 0) };
            button.Click += (_, _) => action();
            parent.Children.Add(button);
            return button;
        }
    }
}

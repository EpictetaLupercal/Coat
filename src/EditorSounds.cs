// Coat — Epicteta Lupercal
using System.IO;
using System.Windows.Media;

namespace Coat;

public enum EditorSoundCue { Selection, Drawing, Ping }

public sealed record EditorCueSettings(bool Enabled = true, string? File = null);

public sealed class EditorAudioSettings
{
    public int Volume { get; set; } = 50;
    public Dictionary<EditorSoundCue, EditorCueSettings> Cues { get; set; } = new();

    internal EditorCueSettings For(EditorSoundCue cue) => Cues?.GetValueOrDefault(cue) ?? new();
    internal EditorAudioSettings Copy() => new() { Volume = Volume, Cues = Cues == null ? new() : new(Cues) };
}

internal static class EditorSoundLibrary
{
    internal static readonly (EditorSoundCue Cue, string Label, string File)[] Cues =
    {
        (EditorSoundCue.Selection, "Sélectionner dans la scène", "selection.wav"),
        (EditorSoundCue.Drawing, "Dessiner dans la scène", "drawing.wav"),
        (EditorSoundCue.Ping, "Placer un ping", "ping.wav")
    };

    internal static string DefaultFile(string name) => Path.Combine(AppContext.BaseDirectory, "sounds", name);
}

internal sealed class EditorSounds : IDisposable
{
    private readonly Dictionary<EditorSoundCue, SoundClip> clips = new();

    internal void Configure(EditorAudioSettings settings)
    {
        Dispose();
        if (settings.Volume <= 0) return;
        foreach (var (cue, _, file) in EditorSoundLibrary.Cues)
        {
            var choice = settings.For(cue);
            if (choice.Enabled) clips[cue] = Create(choice.File, file, settings.Volume);
        }
    }

    internal void Play(EditorSoundCue cue)
    {
        if (clips.TryGetValue(cue, out var clip)) clip.Play();
    }

    internal static SoundClip Create(string? customFile, string defaultName, int volume) =>
        new(customFile, EditorSoundLibrary.DefaultFile(defaultName), Math.Clamp(volume, 0, 100) / 100.0);

    public void Dispose()
    {
        foreach (var clip in clips.Values) clip.Dispose();
        clips.Clear();
    }

    internal sealed class SoundClip : IDisposable
    {
        private readonly MediaPlayer player = new();
        private readonly string fallback;
        private bool usingFallback, ready, pending;
        internal bool IsReady => ready && player.HasAudio;
        internal Uri? Source => player.Source;

        internal SoundClip(string? customFile, string fallback, double volume)
        {
            this.fallback = fallback;
            player.Volume = volume;
            player.MediaOpened += OnOpened;
            player.MediaFailed += OnFailed;
            if (customFile == null || !Open(customFile)) OpenFallback();
        }

        private bool Open(string file)
        {
            if (!Path.IsPathFullyQualified(file) || !File.Exists(file) ||
                !Uri.TryCreate(file, UriKind.Absolute, out var uri) || !uri.IsFile) return false;
            player.Open(uri);
            return true;
        }

        private void OpenFallback()
        {
            usingFallback = true;
            Open(fallback);
        }

        internal void Play()
        {
            if (!ready) { pending = true; return; }
            player.Position = TimeSpan.Zero;
            player.Play();
        }

        private void OnOpened(object? sender, EventArgs args)
        {
            ready = true;
            if (!pending) return;
            pending = false;
            Play();
        }

        private void OnFailed(object? sender, EventArgs args)
        {
            ready = false;
            if (!usingFallback) OpenFallback();
        }

        public void Dispose()
        {
            player.MediaOpened -= OnOpened;
            player.MediaFailed -= OnFailed;
            player.Close();
        }
    }
}

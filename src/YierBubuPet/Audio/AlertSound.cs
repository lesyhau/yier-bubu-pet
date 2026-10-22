using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;

namespace YierBubuPet;

/// <summary>Plays the alert sound once or on repeat. Starting a new sound stops the previous one.</summary>
public sealed class AlertSound
{
    readonly MediaPlayer _player = new();
    bool _loop, _playing;

    /// <summary>Raised when a sound that isn't looping finishes (or fails to play).</summary>
    public event Action? Ended;

    public bool IsPlaying => _playing;

    public AlertSound()
    {
        _player.MediaEnded += (_, _) =>
        {
            if (_loop)
            {
                _player.Position = TimeSpan.Zero;
                _player.Play();
                return;
            }
            Stop();
            Ended?.Invoke();
        };
        // Unsupported or broken file, or Windows without media playback (N editions): stay quiet.
        _player.MediaFailed += (_, _) => { Stop(); Ended?.Invoke(); };
    }

    public void Play(string file, bool loop, double volume)
    {
        Stop();
        _loop = loop;
        _playing = true;
        _player.Open(new Uri(file, UriKind.Absolute));
        _player.Volume = Math.Clamp(volume, 0, 1);
        _player.Play();
    }

    /// <summary>
    /// The app's own sound: a ringtone for alerts that ring until clicked, a knock for the rest.
    /// MediaPlayer needs a file, so the embedded sound is copied to %LocalAppData%\YierBubuPet\sounds
    /// the first time. Returns null if that fails.
    /// </summary>
    public static string? BuiltIn(bool ringing)
    {
        var name = ringing ? "ringtone.wav" : "knock.wav";
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("sounds." + name);
            if (stream == null) return null;
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YierBubuPet", "sounds", name);
            if (!File.Exists(file) || new FileInfo(file).Length != stream.Length)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                using var output = File.Create(file);
                stream.CopyTo(output);
            }
            return file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Stop()
    {
        _loop = false;
        _playing = false;
        _player.Stop();
        _player.Close(); // release the file so it can be deleted or replaced
    }
}

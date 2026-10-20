using System;
using System.IO;
using System.Windows.Media;

namespace YierBubuPet;

/// <summary>
/// Plays the sound that goes with one window's current animation. Starting a new
/// sound (or calling <see cref="Stop"/>) cuts off the previous one. A sound with
/// "[loop]" in its file name repeats until the animation changes.
/// </summary>
public sealed class SoundChannel
{
    readonly Settings _settings;
    readonly MediaPlayer _player = new();
    bool _loop;

    public SoundChannel(Settings settings)
    {
        _settings = settings;
        _player.MediaEnded += (_, _) =>
        {
            if (!_loop) return;
            _player.Position = TimeSpan.Zero;
            _player.Play();
        };
        // Unsupported or broken file, or Windows without media playback (N editions): stay quiet.
        _player.MediaFailed += (_, _) => Stop();
    }

    /// <summary>Plays <paramref name="file"/> if the sound settings allow it.</summary>
    /// <param name="byUser">True when the animation was started by a click or drag rather than on the pet's own.</param>
    public void Play(string? file, bool byUser)
    {
        Stop();
        if (file == null || !Allowed(byUser)) return;
        _loop = Path.GetFileName(file).Contains("[loop]", StringComparison.OrdinalIgnoreCase);
        _player.Open(new Uri(file, UriKind.Absolute));
        _player.Volume = Math.Clamp(_settings.SoundVolume, 0, 1);
        _player.Play();
    }

    public void Stop()
    {
        _loop = false;
        _player.Stop();
        _player.Close(); // release the file so it can be deleted or replaced
    }

    bool Allowed(bool byUser) => _settings.SoundVolume > 0 && _settings.Sound switch
    {
        SoundMode.Always => true,
        SoundMode.Interactions => byUser,
        _ => false,
    };
}

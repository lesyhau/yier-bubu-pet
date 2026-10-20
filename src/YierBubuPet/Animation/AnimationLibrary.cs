using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfPixelFormats = System.Windows.Media.PixelFormats;

namespace YierBubuPet;

public sealed class AnimationClip
{
    public required string Path { get; init; }
    public required BitmapSource[] Frames { get; init; }
    public required int[] DelaysMs { get; init; }
    public required double Width { get; init; }   // DIPs
    public required double Height { get; init; }  // DIPs
    /// <summary>Most stickers face left or the viewer; put "[right]" in a file name if it faces right.</summary>
    public bool FacesRight { get; init; }
    public int LoopMs => DelaysMs.Sum();
}

/// <summary>
/// Finds animations under the animations folder by folder name
/// ("yier/walk", "together/sleep", ...) and decodes them on demand.
/// Also finds the sounds that go with them (see <see cref="PickSound"/>).
/// Drop a new file into a folder and it's picked up automatically.
/// </summary>
public sealed class AnimationLibrary : IDisposable
{
    static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".gif", ".webp", ".png", ".apng" };
    public static readonly HashSet<string> SoundExtensions = new(StringComparer.OrdinalIgnoreCase) { ".wav", ".mp3", ".wma", ".m4a", ".aac" };
    const int CacheSize = 12;

    readonly Dictionary<string, List<string>> _byKey = new(StringComparer.OrdinalIgnoreCase);
    // Sounds named like an animation ("hug.webp" + "hug.mp3") belong to that animation only.
    readonly Dictionary<string, List<string>> _soundsByClip = new(StringComparer.OrdinalIgnoreCase);
    // Other sounds in a folder go with any animation in that folder that has no sound of its own.
    readonly Dictionary<string, List<string>> _soundsByKey = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Task<AnimationClip?>> _cache = new(StringComparer.OrdinalIgnoreCase);
    readonly LinkedList<string> _recent = new();
    readonly FileSystemWatcher _watcher;
    readonly DispatcherTimer _debounce;
    readonly Random _rng = new();
    readonly double _dpiScale;
    int _generation;

    public string Root { get; }
    public double HeightDip { get; private set; }

    /// <summary>Raised after the folder contents or the pet size change.</summary>
    public event Action? Changed;

    public AnimationLibrary(string root, double heightDip, double dpiScale)
    {
        Root = root;
        HeightDip = heightDip;
        _dpiScale = dpiScale;
        Scan();

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Reload(); };

        _watcher = new FileSystemWatcher(root) { IncludeSubdirectories = true };
        FileSystemEventHandler onChange = (_, _) => _debounce.Dispatcher.BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); });
        _watcher.Created += onChange;
        _watcher.Deleted += onChange;
        _watcher.Changed += onChange;
        _watcher.Renamed += (s, e) => onChange(s, e);
        _watcher.EnableRaisingEvents = true;
    }

    public void SetHeight(double heightDip)
    {
        HeightDip = heightDip;
        ClearCache();
        Changed?.Invoke();
    }

    public void Reload()
    {
        ClearCache();
        Scan();
        Changed?.Invoke();
    }

    public bool Has(string key) => _byKey.TryGetValue(key, out var list) && list.Count > 0;

    public int Count(string key) => _byKey.TryGetValue(key, out var list) ? list.Count : 0;

    /// <summary>Picks a random file for the key, avoiding <paramref name="avoid"/> when there is a choice.</summary>
    public string? Pick(string key, string? avoid = null)
    {
        if (!_byKey.TryGetValue(key, out var list) || list.Count == 0) return null;
        var choices = list.Count > 1 && avoid != null ? list.Where(f => f != avoid).ToList() : list;
        return choices[_rng.Next(choices.Count)];
    }

    /// <summary>
    /// The sound to play with an animation file: one with the same name (any audio extension) if there is one,
    /// otherwise a random "loose" sound from the same folder, otherwise null.
    /// </summary>
    public string? PickSound(string clipPath)
    {
        if (_soundsByClip.TryGetValue(StripExtension(clipPath), out var own)) return own[_rng.Next(own.Count)];
        var key = KeyOf(clipPath);
        return _soundsByKey.TryGetValue(key, out var loose) ? loose[_rng.Next(loose.Count)] : null;
    }

    /// <summary>Decodes off the UI thread; returns null if the file can't be read.</summary>
    public Task<AnimationClip?> LoadAsync(string path)
    {
        _recent.Remove(path);
        _recent.AddFirst(path);
        if (_cache.TryGetValue(path, out var cached)) return cached;

        var task = LoadUncachedAsync(path);
        _cache[path] = task;
        while (_recent.Count > CacheSize)
        {
            _cache.Remove(_recent.Last!.Value);
            _recent.RemoveLast();
        }
        return task;
    }

    async Task<AnimationClip?> LoadUncachedAsync(string path)
    {
        int heightPx = (int)Math.Round(HeightDip * _dpiScale);
        int generation = _generation;
        DecodedClip decoded;
        try
        {
            decoded = await Task.Run(() => ClipDecoder.Decode(path, heightPx));
        }
        catch
        {
            return null;
        }
        if (generation != _generation) return null; // size changed while decoding

        var frames = decoded.Frames.Select(bytes =>
        {
            var bmp = BitmapSource.Create(decoded.Width, decoded.Height, 96, 96,
                WpfPixelFormats.Pbgra32, null, bytes, decoded.Width * 4);
            bmp.Freeze();
            return bmp;
        }).ToArray();

        return new AnimationClip
        {
            Path = path,
            Frames = frames,
            DelaysMs = decoded.DelaysMs,
            Width = decoded.Width / _dpiScale,
            Height = decoded.Height / _dpiScale,
            FacesRight = System.IO.Path.GetFileName(path).Contains("[right]", StringComparison.OrdinalIgnoreCase),
        };
    }

    void ClearCache()
    {
        _generation++;
        _cache.Clear();
        _recent.Clear();
    }

    void Scan()
    {
        _byKey.Clear();
        _soundsByClip.Clear();
        _soundsByKey.Clear();
        if (!Directory.Exists(Root)) return;

        var sounds = new List<string>();
        var clips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(Root, "*.*", SearchOption.AllDirectories))
        {
            var ext = System.IO.Path.GetExtension(file);
            if (SoundExtensions.Contains(ext)) { sounds.Add(file); continue; }
            if (!Extensions.Contains(ext)) continue;
            Add(_byKey, KeyOf(file), file);
            clips.Add(StripExtension(file));
        }

        foreach (var sound in sounds)
        {
            var stem = StripExtension(sound);
            if (clips.Contains(stem)) Add(_soundsByClip, stem, sound);
            else Add(_soundsByKey, KeyOf(sound), sound);
        }
    }

    static void Add(Dictionary<string, List<string>> map, string key, string file)
    {
        if (!map.TryGetValue(key, out var list)) map[key] = list = new List<string>();
        list.Add(file);
    }

    /// <summary>"yier/walk" for a file in animations\yier\walk.</summary>
    string KeyOf(string file) =>
        System.IO.Path.GetRelativePath(Root, System.IO.Path.GetDirectoryName(file)!).Replace('\\', '/');

    static string StripExtension(string file) =>
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file)!, System.IO.Path.GetFileNameWithoutExtension(file));

    public void Dispose() => _watcher.Dispose();
}

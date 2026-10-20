using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YierBubuPet;

/// <summary>
/// The starter set of stickers. The artwork isn't part of this repository; StarterPack.json lists
/// where each sticker lives on Tenor and which animation folder it belongs in, and this downloads them.
/// </summary>
internal static class StarterPack
{
    sealed record Entry(string Folder, string Id, string Url);

    static readonly Lazy<List<Entry>> Entries = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("StarterPack.json")!;
        return JsonSerializer.Deserialize<List<Entry>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    });

    public static int Total => Entries.Value.Count;

    /// <summary>Downloads the stickers that aren't in the animations folder yet.</summary>
    public static async Task<(int Downloaded, int Failed)> DownloadMissingAsync(string root)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("YierBubuPet/1.0");
        int downloaded = 0, failed = 0;

        foreach (var entry in Entries.Value)
        {
            var target = TargetPath(root, entry);
            if (target == null) { failed++; continue; }
            if (File.Exists(target)) continue;
            try
            {
                var bytes = await http.GetByteArrayAsync(entry.Url);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllBytesAsync(target, bytes);
                downloaded++;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
            {
                failed++;
            }
        }
        return (downloaded, failed);
    }

    /// <summary>
    /// Copies the built-in sound effects (assets/sounds in the repository) into the animations folder.
    /// A folder that already has sounds is left alone, so your own sounds are never replaced.
    /// </summary>
    public static int InstallDefaultSounds(string root)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var rootFull = Path.GetFullPath(root);
        var files = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("sounds/", StringComparison.Ordinal))
            .Select(n => (Resource: n, Relative: n["sounds/".Length..].Replace('\\', '/')))
            .GroupBy(f => Path.GetDirectoryName(f.Relative) ?? "");

        int installed = 0;
        foreach (var folder in files)
        {
            var dir = Path.GetFullPath(Path.Combine(rootFull, folder.Key));
            if (Directory.Exists(dir) && Directory.EnumerateFiles(dir).Any(f => AnimationLibrary.SoundExtensions.Contains(Path.GetExtension(f))))
                continue;
            try
            {
                Directory.CreateDirectory(dir);
                foreach (var (resource, relative) in folder)
                {
                    using var src = assembly.GetManifestResourceStream(resource)!;
                    using var dst = File.Create(Path.Combine(dir, Path.GetFileName(relative)));
                    src.CopyTo(dst);
                    installed++;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return installed;
    }

    /// <summary>animations/&lt;folder&gt;/&lt;id&gt;_&lt;name&gt;.webp, or null if the entry looks wrong.</summary>
    static string? TargetPath(string root, Entry entry)
    {
        if (!Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return null;
        // Same naming as tools/fetch_tenor.py, so stickers fetched either way aren't downloaded twice.
        var name = Regex.Replace(Path.GetFileName(uri.LocalPath), "[^A-Za-z0-9._-]", "_");
        if (string.IsNullOrEmpty(name) || !entry.Id.All(char.IsAsciiDigit)) return null;

        var rootFull = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(rootFull, entry.Folder, $"{entry.Id}_{name}"));
        return path.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? path : null;
    }
}

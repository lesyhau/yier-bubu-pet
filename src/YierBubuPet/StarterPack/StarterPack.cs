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

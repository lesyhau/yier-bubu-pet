using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YierBubuPet;

/// <summary>A newer release on GitHub.</summary>
/// <param name="DownloadUrl">The file this copy of the app can update itself with, or null if there's none (open <paramref name="PageUrl"/> instead).</param>
public sealed record UpdateInfo(Version Version, string PageUrl, string? DownloadUrl, string? FileName, long Size);

/// <summary>
/// Checks GitHub Releases for a newer version and updates the app in place.
/// </summary>
/// <remarks>
/// A release must include these files (installer/build.ps1 and the README's release steps make them):
/// <c>YierBubuPet-&lt;version&gt;-setup-x64.exe</c> / <c>-arm64.exe</c> (installer) and
/// <c>YierBubuPet-win-x64.exe</c> / <c>-arm64.exe</c> (portable). The release tag is the version, e.g. <c>v1.2.0</c>.
/// <list type="bullet">
/// <item>Installed copy: downloads the installer and runs it silently; it reopens the app when done.</item>
/// <item>Portable copy: downloads the new exe, renames the running one to .old, puts the new one in its place and starts it.</item>
/// </list>
/// </remarks>
public static class Updater
{
    const string Owner = "lesyhau", Repo = "yier-bubu-pet";
    const string LatestApi = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
    public const string ReleasesPage = $"https://github.com/{Owner}/{Repo}/releases/latest";

    static string ExePath => Environment.ProcessPath!;
    static string Arch => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";

    public static Version Current { get; } = ParseVersion(
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
        ?? Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

    /// <summary>Installed with the installer (it leaves its uninstaller next to the exe).</summary>
    public static bool IsInstalled => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    /// <summary>A single self-contained exe, which can replace itself. (Other builds just link to the download page.)</summary>
    static bool IsSingleFile => string.IsNullOrEmpty(typeof(Updater).Assembly.Location);

    static HttpClient NewClient(TimeSpan timeout)
    {
        var http = new HttpClient { Timeout = timeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"YierBubuPet/{Current}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    /// <summary>The newer release, or null when this is the latest. Throws on network or GitHub errors.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken cancel = default)
    {
        using var http = NewClient(TimeSpan.FromSeconds(20));
        using var stream = await http.GetStreamAsync(LatestApi, cancel);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel);
        var release = json.RootElement;

        var version = ParseVersion(release.GetProperty("tag_name").GetString());
        if (version == null || version <= Current) return null;
        string page = release.TryGetProperty("html_url", out var u) ? u.GetString() ?? ReleasesPage : ReleasesPage;

        // The file this copy needs: the installer when installed, the single exe when portable.
        string? wanted = IsInstalled ? $"-setup-{Arch}.exe" : IsSingleFile ? $"YierBubuPet-win-{Arch}.exe" : null;
        if (wanted != null && release.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(wanted, StringComparison.OrdinalIgnoreCase)) continue;
                string url = asset.GetProperty("browser_download_url").GetString() ?? "";
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com") continue;
                return new UpdateInfo(version, page, url, name, asset.GetProperty("size").GetInt64());
            }
        }
        return new UpdateInfo(version, page, null, null, 0);
    }

    /// <summary>Downloads the update to a temporary file and returns its path. Reports progress from 0 to 1.</summary>
    public static async Task<string> DownloadAsync(UpdateInfo update, IProgress<double> progress, CancellationToken cancel = default)
    {
        if (update.DownloadUrl == null || update.FileName == null) throw new InvalidOperationException("No download for this update.");
        var dir = Path.Combine(Path.GetTempPath(), "YierBubuPet-update");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Path.GetFileName(update.FileName));

        using var http = NewClient(TimeSpan.FromMinutes(10));
        using var response = await http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? update.Size;
        await using (var source = await response.Content.ReadAsStreamAsync(cancel))
        await using (var target = File.Create(path))
        {
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancel)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancel);
                done += read;
                if (total > 0) progress.Report(Math.Min(1, (double)done / total));
            }
        }
        if (update.Size > 0 && new FileInfo(path).Length != update.Size)
            throw new IOException("The download is incomplete.");
        return path;
    }

    /// <summary>
    /// Starts the downloaded update. On success the caller must exit right away (the installer, or the new exe,
    /// takes over). Releases the single-instance lock first so the new copy can start.
    /// </summary>
    public static void Apply(string downloaded)
    {
        if (IsInstalled)
        {
            // Same install mode as now: per user, or for all users (that one shows a UAC prompt).
            bool allUsers = AppContext.BaseDirectory.StartsWith(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase);
            var args = $"/SILENT /SUPPRESSMSGBOXES /NORESTART /UPDATE {(allUsers ? "/ALLUSERS" : "/CURRENTUSER")}";
            Program.ReleaseSingleInstance();
            Process.Start(new ProcessStartInfo(downloaded, args) { UseShellExecute = true });
            return;
        }

        // Portable: a running exe can't be overwritten, but it can be renamed.
        var exe = ExePath;
        var old = exe + ".old";
        if (File.Exists(old)) File.Delete(old);
        File.Move(exe, old);
        try
        {
            File.Move(downloaded, exe);
        }
        catch
        {
            File.Move(old, exe); // put things back
            throw;
        }
        Program.ReleaseSingleInstance();
        Process.Start(new ProcessStartInfo(exe, "--updated") { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory });
    }

    /// <summary>Removes what a portable update left behind (the previous exe and the download folder).</summary>
    public static void CleanUpAfterUpdate()
    {
        try
        {
            var old = ExePath + ".old";
            if (File.Exists(old)) File.Delete(old);
            var dir = Path.Combine(Path.GetTempPath(), "YierBubuPet-update");
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch { /* still in use; try again next time */ }
    }

    /// <summary>"v1.2.0", "1.2", "1.2.0+abc" → 1.2.0.</summary>
    static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim().TrimStart('v', 'V');
        text = new string(text.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        if (!Version.TryParse(text.Contains('.') ? text : text + ".0", out var v)) return null;
        return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
    }
}

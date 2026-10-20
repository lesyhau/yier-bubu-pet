using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace YierBubuPet;

public static class Program
{
    const string MutexName = "YierBubuPet.SingleInstance";
    static Mutex? _singleInstance;

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--dump")
        {
            DumpFrames(args[1], args[2]);
            return;
        }
        if (args.Length == 2 && args[0] == "--starter-pack")
        {
            // Exit code = number of stickers that failed to download.
            var (_, failed) = StarterPack.DownloadMissingAsync(args[1]).GetAwaiter().GetResult();
            Environment.ExitCode = failed;
            return;
        }

        // Right after an update the previous version may still be closing; give it a moment.
        bool firstInstance = false;
        for (int i = 0; i < 20 && !firstInstance; i++)
        {
            _singleInstance?.Dispose();
            _singleInstance = new Mutex(true, MutexName, out firstInstance);
            if (!firstInstance && !args.Contains("--updated")) return;
            if (!firstInstance) Thread.Sleep(250);
        }
        if (!firstInstance) return;
        Updater.CleanUpAfterUpdate();

        var root = FindAnimationsFolder() ?? CreateAnimationsFolder();

        Screens.Init();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Stage? stage = null;
        app.Startup += (_, _) => stage = new Stage(root);
        app.Exit += (_, _) => stage?.Dispose();
        app.Run();
        ReleaseSingleInstance();
    }

    /// <summary>Lets another copy start (and lets the installer see that this one has closed). Call on the UI thread.</summary>
    public static void ReleaseSingleInstance()
    {
        if (_singleInstance == null) return;
        try { _singleInstance.ReleaseMutex(); } catch (ApplicationException) { /* not owned */ }
        _singleInstance.Dispose();
        _singleInstance = null;
    }

    /// <summary>Debug aid: YierBubuPet.exe --dump in.gif outDir writes the processed frames as PNGs.</summary>
    static void DumpFrames(string file, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var clip = ClipDecoder.Decode(file, 240);
        for (int i = 0; i < clip.Frames.Length; i++)
        {
            var info = new SkiaSharp.SKImageInfo(clip.Width, clip.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            using var img = SkiaSharp.SKImage.FromPixelCopy(info, clip.Frames[i]);
            using var png = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(outDir, $"{i:000}_{clip.DelaysMs[i]}ms.png"), png.ToArray());
        }
    }

    /// <summary>
    /// Looks for "animations" next to the exe, then in parent folders (so dev builds use the one at the
    /// repository root), then in %LocalAppData% (used when the exe's own folder isn't writable).
    /// </summary>
    static string? FindAnimationsFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "animations");
            if (Directory.Exists(candidate)) return candidate;
        }
        var perUser = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YierBubuPet", "animations");
        return Directory.Exists(perUser) ? perUser : null;
    }

    /// <summary>
    /// Next to the exe when that folder is writable (portable use, or the installer's default per-user install);
    /// otherwise, e.g. when installed for all users in Program Files, %LocalAppData%\YierBubuPet\animations.
    /// </summary>
    static string CreateAnimationsFolder()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "animations");
        try
        {
            Directory.CreateDirectory(local);
            return local;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            var perUser = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YierBubuPet", "animations");
            Directory.CreateDirectory(perUser);
            return perUser;
        }
    }
}

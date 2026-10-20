using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace YierBubuPet;

public static class Program
{
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
            StarterPack.InstallDefaultSounds(args[1]);
            Environment.ExitCode = failed;
            return;
        }

        using var mutex = new Mutex(true, "YierBubuPet.SingleInstance", out bool firstInstance);
        if (!firstInstance) return;

        var root = FindAnimationsFolder() ?? CreateAnimationsFolder();

        Screens.Init();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Stage? stage = null;
        app.Startup += (_, _) => stage = new Stage(root);
        app.Exit += (_, _) => stage?.Dispose();
        app.Run();
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

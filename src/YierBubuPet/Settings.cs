using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YierBubuPet;

public enum SoundMode { Off, Interactions, Always }

public sealed class Settings
{
    /// <summary>Height of a pet on screen, in device-independent pixels.</summary>
    public double PetHeight { get; set; } = 120;

    /// <summary>Walking speed in DIPs per second.</summary>
    public double WalkSpeed { get; set; } = 70;

    /// <summary>The pets fall asleep after you stop using the PC for this long.</summary>
    public double SleepAfterMinutes { get; set; } = 5;

    /// <summary>Average seconds between the pets coming together on their own.</summary>
    public double TogetherEverySeconds { get; set; } = 90;

    /// <summary>When sounds play: never, only when you click/drag the pets, or for everything they do.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SoundMode Sound { get; set; } = SoundMode.Interactions;

    /// <summary>Sound volume, 0 to 1.</summary>
    public double SoundVolume { get; set; } = 0.6;

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YierBubuPet", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { /* fall back to defaults on a corrupt file */ }
        return new Settings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

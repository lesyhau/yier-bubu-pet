using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YierBubuPet;

/// <summary>Which part of the screen the pets walk around in.</summary>
public enum WalkArea { Full, LeftHalf, RightHalf }

public enum AlarmRepeat { Once, Daily, Weekdays }

/// <summary>An alarm (a time of day) or a timer (a moment in the future). Both ring until clicked.</summary>
public sealed class Alarm
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>True for a countdown timer (uses <see cref="Due"/>), false for an alarm (uses <see cref="Time"/>).</summary>
    public bool IsTimer { get; set; }

    /// <summary>Alarm: time of day.</summary>
    public TimeSpan Time { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AlarmRepeat Repeat { get; set; } = AlarmRepeat.Once;

    /// <summary>Timer: when it rings (local time).</summary>
    public DateTime Due { get; set; }

    /// <summary>Shown in the speech bubble when it rings.</summary>
    public string Message { get; set; } = "";

    /// <summary>When it was created or last rang; the next ring is the first occurrence after this.</summary>
    public DateTime Since { get; set; } = DateTime.Now;

    /// <summary>The next time this rings, after <see cref="Since"/>.</summary>
    public DateTime NextRing()
    {
        if (IsTimer) return Due;
        var day = Since.Date;
        for (int i = 0; i < 9; i++, day = day.AddDays(1))
        {
            var at = day + Time;
            if (at <= Since) continue;
            if (Repeat == AlarmRepeat.Weekdays && at.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            return at;
        }
        return DateTime.MaxValue;
    }
}

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

    /// <summary>Part of the screen the pets stay in (they can still be dragged anywhere).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WalkArea WalkArea { get; set; } = WalkArea.Full;

    /// <summary>Menu language: "auto" (follow Windows), "en" or "vi".</summary>
    public string Language { get; set; } = "auto";

    /// <summary>Control panel sections that are expanded ("alarms", "alerts", "settings", "animations").</summary>
    public List<string> ExpandedSections { get; set; } = new() { "alarms" };

    // ---- alerts (animations\alert) ----

    public List<Alarm> Alarms { get; set; } = new();

    /// <summary>React to notification pop-ups from other apps.</summary>
    public bool AlertOnNotifications { get; set; } = true;

    /// <summary>Ring at the start of every hour (not while you're away).</summary>
    public bool AlertHourly { get; set; } = false;

    /// <summary>Warn when the battery drops to <see cref="BatteryLowPercent"/> while unplugged.</summary>
    public bool AlertBatteryLow { get; set; } = true;
    public int BatteryLowPercent { get; set; } = 20;

    /// <summary>React when the charger is plugged in or unplugged.</summary>
    public bool AlertCharger { get; set; } = false;

    /// <summary>Say hello when you unlock the PC.</summary>
    public bool AlertUnlock { get; set; } = false;

    /// <summary>Play the alert sound (the audio file in animations\alert).</summary>
    public bool AlertSound { get; set; } = true;

    /// <summary>Alert sound volume, 0 to 1.</summary>
    public double AlertVolume { get; set; } = 0.8;

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
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException) { /* settings are a convenience; never crash over them */ }
    }
}

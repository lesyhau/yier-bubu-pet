using System;
using System.Linq;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace YierBubuPet;

public enum AlertKind { Alarm, Timer, Notification, Hourly, Battery, Charger, Unlock, Test }

/// <summary>What happened, for the pets to show.</summary>
/// <param name="Persistent">Keeps ringing (sound on repeat) until clicked, instead of playing once.</param>
public sealed record AlertEvent(AlertKind Kind, string Text, bool Persistent)
{
    /// <summary>Optional sub-folder of animations\alert for this kind, e.g. "alert/alarm".</summary>
    public string Folder => "alert/" + Kind.ToString().ToLowerInvariant();
}

/// <summary>
/// Watches the things that can trigger an alert (alarms and timers, the hourly chime, notifications from
/// other apps, battery, charger, unlocking the PC) and raises <see cref="Fired"/>. Call <see cref="Tick"/> often.
/// </summary>
public sealed class AlertCenter : IDisposable
{
    readonly Settings _settings;
    readonly NotificationWatcher? _notifications;
    DateTime _lastCheck = DateTime.MinValue;
    int _lastHour = -1;
    Forms.PowerLineStatus _lastPower = Forms.PowerLineStatus.Unknown;
    bool _batteryWarned;

    public event Action<AlertEvent>? Fired;

    /// <summary>Raised when the alarm list changes (an alarm or timer rang and was removed).</summary>
    public event Action? AlarmsChanged;

    public AlertCenter(Settings settings)
    {
        _settings = settings;
        _lastHour = DateTime.Now.Hour;
        try
        {
            _notifications = new NotificationWatcher();
            _notifications.NotificationShown += () =>
            {
                if (_settings.AlertOnNotifications)
                    Fired?.Invoke(new AlertEvent(AlertKind.Notification, Strings.Get("Alert.Notification"), false));
            };
        }
        catch { _notifications = null; }
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    /// <summary>Tray balloons are notifications too; call this before showing one so it isn't treated as an alert.</summary>
    public void IgnoreNotificationsFor(TimeSpan time)
    {
        if (_notifications != null) _notifications.IgnoreUntil = DateTime.Now + time;
    }

    public void Tick()
    {
        var now = DateTime.Now;
        if ((now - _lastCheck).TotalMilliseconds < 500) return;
        _lastCheck = now;
        CheckAlarms(now);
        CheckHour(now);
        CheckPower();
    }

    void CheckAlarms(DateTime now)
    {
        bool changed = false;
        foreach (var alarm in _settings.Alarms.ToList())
        {
            var ring = alarm.NextRing();
            if (now < ring) continue;
            // Missed by a lot (PC was asleep or off): skip it rather than ring late.
            bool late = (now - ring).TotalMinutes > 10;
            if (!late)
            {
                string text = alarm.IsTimer
                    ? Join(Strings.Get("Alert.Timer"), alarm.Message)
                    : Join(Strings.Format("Alert.Alarm", ring.ToString("HH:mm")), alarm.Message);
                Fired?.Invoke(new AlertEvent(alarm.IsTimer ? AlertKind.Timer : AlertKind.Alarm, text, true));
            }
            if (alarm.IsTimer || alarm.Repeat == AlarmRepeat.Once) _settings.Alarms.Remove(alarm);
            else alarm.Since = now;
            changed = true;
        }
        if (changed)
        {
            _settings.Save();
            AlarmsChanged?.Invoke();
        }
    }

    void CheckHour(DateTime now)
    {
        if (now.Hour == _lastHour) return;
        _lastHour = now.Hour;
        if (_settings.AlertHourly && now.Minute < 2)
            Fired?.Invoke(new AlertEvent(AlertKind.Hourly, Strings.Format("Alert.Hourly", now.ToString("HH:00")), false));
    }

    void CheckPower()
    {
        var status = Forms.SystemInformation.PowerStatus;
        if (status.BatteryChargeStatus.HasFlag(Forms.BatteryChargeStatus.NoSystemBattery)) return; // desktop PC

        var line = status.PowerLineStatus;
        if (_lastPower != Forms.PowerLineStatus.Unknown && line != _lastPower && _settings.AlertCharger)
        {
            if (line == Forms.PowerLineStatus.Online)
                Fired?.Invoke(new AlertEvent(AlertKind.Charger, Strings.Get("Alert.Charging"), false));
            else if (line == Forms.PowerLineStatus.Offline)
                Fired?.Invoke(new AlertEvent(AlertKind.Charger, Strings.Get("Alert.Unplugged"), false));
        }
        _lastPower = line;

        float level = status.BatteryLifePercent; // 0..1, or 255 when unknown
        if (level > 1) return;
        int percent = (int)Math.Round(level * 100);
        if (line == Forms.PowerLineStatus.Online || percent > _settings.BatteryLowPercent + 5)
            _batteryWarned = false;
        else if (!_batteryWarned && line == Forms.PowerLineStatus.Offline && percent <= _settings.BatteryLowPercent)
        {
            _batteryWarned = true;
            if (_settings.AlertBatteryLow)
                Fired?.Invoke(new AlertEvent(AlertKind.Battery, Strings.Format("Alert.Battery", percent), true));
        }
    }

    void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock && _settings.AlertUnlock)
            Fired?.Invoke(new AlertEvent(AlertKind.Unlock, Strings.Get("Alert.Unlock"), false));
    }

    static string Join(string title, string message) =>
        string.IsNullOrWhiteSpace(message) ? title : title + "\n" + message.Trim();

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _notifications?.Dispose();
    }
}

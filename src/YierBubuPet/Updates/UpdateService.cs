using System;
using System.Windows;

namespace YierBubuPet;

public enum UpdateState { Idle, Checking, UpToDate, Available, Downloading, Installing, Failed }

/// <summary>
/// The update status shared by the control panel and the About window. Checks GitHub shortly after
/// start-up and then once a day (if enabled), and runs the download and install.
/// </summary>
public sealed class UpdateService
{
    readonly Settings _settings;
    DateTime _startedAt = DateTime.Now;

    public UpdateState State { get; private set; } = UpdateState.Idle;
    public UpdateInfo? Available { get; private set; }
    /// <summary>Download progress, 0 to 1.</summary>
    public double Progress { get; private set; }
    /// <summary>The failure was the download (not the check).</summary>
    public bool DownloadFailed { get; private set; }
    /// <summary>The available update was found by clicking "Check for updates" (not the daily check).</summary>
    public bool FoundManually { get; private set; }

    /// <summary>Raised on the UI thread when the state changes.</summary>
    public event Action? Changed;
    /// <summary>Raised on the UI thread as the download progresses (at most once per percent).</summary>
    public event Action? ProgressChanged;
    /// <summary>Raised when an automatic check finds a new version.</summary>
    public event Action<UpdateInfo>? Found;

    public UpdateService(Settings settings) => _settings = settings;

    /// <summary>Call from the frame loop: checks 15 s after start-up, then every 24 hours.</summary>
    public void Tick()
    {
        if (!_settings.AutoCheckUpdates || State is UpdateState.Checking or UpdateState.Downloading or UpdateState.Installing) return;
        var now = DateTime.Now;
        if ((now - _startedAt).TotalSeconds < 15) return;
        if ((now - _settings.LastUpdateCheck).TotalHours < 24) return;
        _ = CheckAsync(manual: false);
    }

    public async System.Threading.Tasks.Task CheckAsync(bool manual)
    {
        if (State is UpdateState.Checking or UpdateState.Downloading or UpdateState.Installing) return;
        Set(UpdateState.Checking);
        _settings.LastUpdateCheck = DateTime.Now;
        _settings.Save();
        try
        {
            Available = await Updater.CheckAsync();
            DownloadFailed = false;
            FoundManually = manual;
            Set(Available != null ? UpdateState.Available : UpdateState.UpToDate);
            if (Available != null && !manual) Found?.Invoke(Available);
        }
        catch
        {
            // Offline or GitHub unavailable: say so when asked, stay quiet otherwise.
            Set(manual ? UpdateState.Failed : Available != null ? UpdateState.Available : UpdateState.Idle);
        }
    }

    /// <summary>Downloads and installs the available update, then exits the app. Opens the release page when this copy can't update itself.</summary>
    public async void Install()
    {
        var update = Available;
        if (update == null || State is UpdateState.Downloading or UpdateState.Installing) return;
        if (update.DownloadUrl == null)
        {
            OpenPage();
            return;
        }
        Progress = 0;
        Set(UpdateState.Downloading);
        try
        {
            var file = await Updater.DownloadAsync(update, new Progress<double>(p =>
            {
                bool step = (int)(p * 100) != (int)(Progress * 100);
                Progress = p;
                if (step) ProgressChanged?.Invoke();
            }));
            Set(UpdateState.Installing);
            Updater.Apply(file);
            Application.Current.Shutdown();
        }
        catch
        {
            DownloadFailed = true;
            Set(UpdateState.Failed);
        }
    }

    public void OpenPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Available?.PageUrl ?? Updater.ReleasesPage) { UseShellExecute = true });
        }
        catch { /* no browser */ }
    }

    /// <summary>One line describing the current state, for the UI.</summary>
    public string StatusText => State switch
    {
        UpdateState.Checking => Strings.Get("Update.Checking"),
        UpdateState.UpToDate => Strings.Get("Update.UpToDate"),
        UpdateState.Available => Strings.Format("Update.Available", Available!.Version.ToString(3)),
        UpdateState.Downloading => Strings.Format("Update.Downloading", (int)Math.Round(Progress * 100)),
        UpdateState.Installing => Strings.Get("Update.Installing"),
        UpdateState.Failed => Strings.Get(DownloadFailed ? "Update.DownloadFailed" : "Update.Failed"),
        _ => "",
    };

    void Set(UpdateState state)
    {
        State = state;
        Changed?.Invoke();
    }
}

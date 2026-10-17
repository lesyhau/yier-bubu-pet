using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace YierBubuPet;

/// <summary>
/// Owns both pets, runs the frame loop, brings the pets together for
/// "together" animations, puts them to sleep while you're away, and the tray menu.
/// </summary>
public sealed class Stage : IDisposable
{
    enum Mode { Free, Meeting, Together, Sleeping }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string AppName = "YierBubuPet";

    readonly Settings _settings;
    readonly AnimationLibrary _lib;
    readonly List<Pet> _pets = new();
    readonly SpriteWindow _duo = new();
    readonly Random _rng = new();
    readonly DispatcherTimer _timer;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly Forms.NotifyIcon _tray;
    readonly Forms.ContextMenuStrip _menu;

    Mode _mode = Mode.Free;
    string _togetherKey = "together/play";
    double _meetX, _gap, _duoMs, _nextTogetherMs, _lastTick;
    int _duoLoops, _duoToken;
    bool _shown = true, _bedtime;

    public Stage(string animationsRoot)
    {
        _settings = Settings.Load();
        _lib = new AnimationLibrary(animationsRoot, _settings.PetHeight, Screens.DpiScale);
        _lib.Changed += () =>
        {
            foreach (var p in _pets) p.Refresh();
            AddMissingPets();
        };
        AddMissingPets();

        _duo.MouseLeftButtonUp += (_, _) => OnDuoClicked();
        _duo.MouseRightButtonUp += (_, _) => ShowMenuAtCursor();

        _menu = BuildMenu();
        _tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "Yier & Bubu",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => TogetherNow();

        ScheduleTogether();
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        if (_pets.Count == 0) OfferStarterPack();
    }

    /// <summary>Creates a pet for each character that has idle animations and isn't on screen yet.</summary>
    void AddMissingPets()
    {
        var area = Screens.PrimaryWorkArea;
        foreach (var (name, offset) in new[] { ("yier", 260.0), ("bubu", 130.0) })
        {
            if (_pets.Any(p => p.Name == name) || !_lib.Has($"{name}/idle")) continue;
            var pet = new Pet(name, _lib, _settings, _rng, area.Right - offset, area.Bottom);
            pet.Touched += OnPetTouched;
            pet.RightClicked += _ => ShowMenuAtCursor();
            pet.SetShown(_shown);
            _pets.Add(pet);
            pet.Start();
        }
    }

    void OfferStarterPack()
    {
        var answer = MessageBox.Show(
            $"There are no animations in\n{_lib.Root}\n\nDownload the starter pack of {StarterPack.Total} Yier & Bubu stickers from Tenor (about 5 MB)?",
            "Yier & Bubu", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes) DownloadStarterPack();
    }

    async void DownloadStarterPack()
    {
        _tray.ShowBalloonTip(3000, "Yier & Bubu", "Downloading stickers…", Forms.ToolTipIcon.None);
        var (downloaded, failed) = await StarterPack.DownloadMissingAsync(_lib.Root);
        _lib.Reload();
        string text = failed == 0
            ? $"{downloaded} new sticker(s) downloaded."
            : $"{downloaded} new sticker(s) downloaded, {failed} failed. Try again later from the tray menu.";
        _tray.ShowBalloonTip(4000, "Yier & Bubu", text, Forms.ToolTipIcon.None);
    }

    void Tick()
    {
        double now = _clock.Elapsed.TotalMilliseconds;
        double dt = Math.Min(100, now - _lastTick);
        _lastTick = now;

        foreach (var pet in _pets) pet.Tick(dt);
        UpdateSleep();

        switch (_mode)
        {
            case Mode.Free:
                _nextTogetherMs -= dt;
                if (_nextTogetherMs <= 0)
                {
                    ScheduleTogether();
                    if (_pets.All(p => p.State is PetState.Idle or PetState.Walk)) StartMeeting("together/play");
                }
                break;

            case Mode.Meeting:
                if (_pets.Any(p => p.State is not (PetState.Walk or PetState.Wait)))
                    EndTogether();
                else if (_pets.All(p => p.State == PetState.Wait))
                    ShowTogether();
                break;

            case Mode.Together:
                if (_duo.IsVisible && _duo.Advance(dt)) _duoLoops++;
                _duoMs += dt;
                bool asleep = _togetherKey == "together/sleep";
                if (!asleep && ((_duoLoops >= 1 && _duoMs > 3500) || _duoMs > 12000))
                    EndTogether();
                break;
        }
    }

    // ---- together ----

    bool CanBeTogether(string key) => _pets.Count == 2 && _lib.Has(key);

    void StartMeeting(string key)
    {
        if (!CanBeTogether(key)) return;
        _togetherKey = key;
        _mode = Mode.Meeting;

        var left = _pets.OrderBy(p => p.X).First();
        var right = _pets.First(p => p != left);
        var area = Screens.WorkAreaAt(left.X, left.Y - 1);
        _gap = _settings.PetHeight * 0.35;
        _meetX = Math.Clamp((left.X + right.X) / 2, area.Left + _settings.PetHeight, area.Right - _settings.PetHeight);
        left.WalkTo(_meetX - _gap);
        right.WalkTo(_meetX + _gap);
    }

    async void ShowTogether()
    {
        _mode = Mode.Together;
        _duoMs = 0;
        _duoLoops = 0;
        int token = ++_duoToken;

        var file = _lib.Pick(_togetherKey, _duo.Animation?.Path);
        var clip = file == null ? null : await _lib.LoadAsync(file);
        if (token != _duoToken || _mode != Mode.Together) return;
        if (clip == null) { EndTogether(); return; }

        double feet = _pets.Max(p => p.Y);
        _duo.Play(clip);
        _duo.Face(_rng.Next(2) == 0);
        _duo.PlaceAt(_meetX, feet);
        foreach (var p in _pets) p.Hide();
        if (_shown) _duo.Show();
        _duoMs = 0;
        _duoLoops = 0;
    }

    void EndTogether()
    {
        _duoToken++;
        bool wasShowing = _mode == Mode.Together;
        _mode = Mode.Free;
        _duo.Hide();
        var ordered = _pets.OrderBy(p => p.X).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            if (wasShowing && ordered[i].State == PetState.Hidden)
                ordered[i].Reappear(_meetX + (i == 0 ? -_gap : _gap));
            else
                ordered[i].Release();
        }
        ScheduleTogether();
    }

    void TogetherNow()
    {
        WakeUp();
        if (_mode == Mode.Together) ShowTogether(); // already together: play another one
        else if (_mode == Mode.Free) StartMeeting("together/play");
    }

    void OnDuoClicked()
    {
        if (_togetherKey == "together/sleep") { _bedtime = false; WakeUp(); }
        else ShowTogether();
    }

    void ScheduleTogether() =>
        _nextTogetherMs = _settings.TogetherEverySeconds * 1000 * (0.6 + _rng.NextDouble() * 0.8);

    // ---- sleep ----

    bool IsAsleep => _mode == Mode.Sleeping || (_mode is Mode.Meeting or Mode.Together && _togetherKey == "together/sleep");

    void UpdateSleep()
    {
        bool away = Native.UserIdleTime().TotalMinutes >= _settings.SleepAfterMinutes;
        if ((away || _bedtime) && !IsAsleep) GoToSleep();
        else if (!away && !_bedtime && IsAsleep) WakeUp();
    }

    void GoToSleep()
    {
        if (_mode != Mode.Free) EndTogether();
        if (CanBeTogether("together/sleep"))
        {
            StartMeeting("together/sleep");
            return;
        }
        _mode = Mode.Sleeping;
        foreach (var p in _pets) p.Sleep();
    }

    void WakeUp()
    {
        if (!IsAsleep) return;
        if (_mode == Mode.Sleeping)
        {
            _mode = Mode.Free;
            foreach (var p in _pets) p.Wake();
        }
        else EndTogether();
    }

    void OnPetTouched(Pet pet)
    {
        _bedtime = false;
        if (_mode == Mode.Sleeping)
        {
            _mode = Mode.Free;
            foreach (var p in _pets.Where(p => p != pet)) p.Wake();
        }
        else if (_mode == Mode.Meeting) EndTogether();
    }

    // ---- tray menu ----

    Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        var hug = new Forms.ToolStripMenuItem("Together time 💕", null, (_, _) => TogetherNow());
        var bed = new Forms.ToolStripMenuItem("Bedtime 💤");
        bed.Click += (_, _) =>
        {
            _bedtime = !IsAsleep;
            if (_bedtime) GoToSleep(); else WakeUp();
        };

        var size = new Forms.ToolStripMenuItem("Size");
        foreach (var (label, h) in new[] { ("Small", 90.0), ("Medium", 120.0), ("Large", 160.0), ("Huge", 220.0) })
        {
            var item = new Forms.ToolStripMenuItem(label) { Tag = h };
            item.Click += (_, _) =>
            {
                _settings.PetHeight = h;
                _settings.Save();
                _lib.SetHeight(h);
                if (_mode == Mode.Together) ShowTogether();
            };
            size.DropDownItems.Add(item);
        }

        var show = new Forms.ToolStripMenuItem("Hide pets");
        show.Click += (_, _) =>
        {
            _shown = !_shown;
            foreach (var p in _pets) p.SetShown(_shown);
            if (!_shown) _duo.Hide();
            else if (_mode == Mode.Together) _duo.Show();
        };

        var startup = new Forms.ToolStripMenuItem("Start with Windows");
        startup.Click += (_, _) => SetStartWithWindows(!StartsWithWindows());

        menu.Items.Add(hug);
        menu.Items.Add(bed);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(size);
        menu.Items.Add(show);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Open animations folder", null, (_, _) => Process.Start("explorer.exe", _lib.Root));
        menu.Items.Add("Reload animations", null, (_, _) => _lib.Reload());
        menu.Items.Add("Download starter stickers", null, (_, _) => DownloadStarterPack());
        menu.Items.Add(startup);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Application.Current.Shutdown());

        menu.Opening += (_, _) =>
        {
            bed.Text = IsAsleep ? "Wake up ☀️" : "Bedtime 💤";
            show.Text = _shown ? "Hide pets" : "Show pets";
            startup.Checked = StartsWithWindows();
            foreach (Forms.ToolStripMenuItem item in size.DropDownItems)
                item.Checked = (double)item.Tag! == _settings.PetHeight;
        };
        return menu;
    }

    void ShowMenuAtCursor() => _menu.Show(Forms.Cursor.Position);

    static bool StartsWithWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is string;
    }

    static void SetStartWithWindows(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enable) key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(AppName, throwOnMissingValue: false);
    }

    public void Dispose()
    {
        _timer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _lib.Dispose();
    }
}

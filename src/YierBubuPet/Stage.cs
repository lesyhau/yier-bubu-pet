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
    readonly SoundChannel _duoSound;
    readonly Random _rng = new();
    readonly DispatcherTimer _timer;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly Forms.NotifyIcon _tray;
    Forms.ContextMenuStrip _menu;

    Mode _mode = Mode.Free;
    string _togetherKey = "together/play";
    double _meetX, _gap, _duoMs, _nextTogetherMs, _lastTick;
    int _duoLoops, _duoToken;
    bool _shown = true, _bedtime;
    bool _togetherByUser; // the current "together" was asked for from the menu or by a click

    public Stage(string animationsRoot)
    {
        _settings = Settings.Load();
        Strings.Apply(_settings.Language);
        _lib = new AnimationLibrary(animationsRoot, _settings.PetHeight, Screens.DpiScale);
        _duoSound = new SoundChannel(_settings);
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
        var area = Screens.Restrict(Screens.PrimaryWorkArea, _settings.WalkArea);
        foreach (var (name, offset) in new[] { ("yier", 260.0), ("bubu", 130.0) })
        {
            if (_pets.Any(p => p.Name == name) || !_lib.Has($"{name}/idle")) continue;
            double x = Math.Max(area.Left + 60, area.Right - offset);
            var pet = new Pet(name, _lib, _settings, _rng, x, area.Bottom);
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
            Strings.Format("Starter.Offer", _lib.Root, StarterPack.Total),
            "Yier & Bubu", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes) DownloadStarterPack();
    }

    async void DownloadStarterPack()
    {
        _tray.ShowBalloonTip(3000, "Yier & Bubu", Strings.Get("Starter.Downloading"), Forms.ToolTipIcon.None);
        var (downloaded, failed) = await StarterPack.DownloadMissingAsync(_lib.Root);
        StarterPack.InstallDefaultSounds(_lib.Root);
        _lib.Reload();
        string text = failed == 0
            ? Strings.Format("Starter.Done", downloaded)
            : Strings.Format("Starter.Failed", downloaded, failed);
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
                    if (_pets.All(p => p.State is PetState.Idle or PetState.Walk)) StartMeeting("together/play", byUser: false);
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

    void StartMeeting(string key, bool byUser)
    {
        if (!CanBeTogether(key)) return;
        _togetherKey = key;
        _togetherByUser = byUser;
        _mode = Mode.Meeting;

        var left = _pets.OrderBy(p => p.X).First();
        var right = _pets.First(p => p != left);
        var area = Screens.Restrict(Screens.WorkAreaAt(left.X, left.Y - 1), _settings.WalkArea);
        _gap = _settings.PetHeight * 0.35;
        double lo = area.Left + _settings.PetHeight, hi = Math.Max(lo, area.Right - _settings.PetHeight);
        _meetX = Math.Clamp((left.X + right.X) / 2, lo, hi);
        left.WalkTo(_meetX - _gap);
        right.WalkTo(_meetX + _gap);
    }

    /// <param name="byUser">Set when the user asked for this one; otherwise keeps how the meeting started.</param>
    /// <param name="sound">False when only re-showing (e.g. after a size change).</param>
    async void ShowTogether(bool? byUser = null, bool sound = true)
    {
        if (byUser.HasValue) _togetherByUser = byUser.Value;
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
        if (_shown)
        {
            _duo.Show();
            if (sound) _duoSound.Play(_lib.PickSound(file!), _togetherByUser);
        }
        _duoMs = 0;
        _duoLoops = 0;
    }

    void EndTogether()
    {
        _duoToken++;
        bool wasShowing = _mode == Mode.Together;
        _mode = Mode.Free;
        _duo.Hide();
        _duoSound.Stop();
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
        if (_mode == Mode.Together) ShowTogether(byUser: true); // already together: play another one
        else if (_mode == Mode.Free) StartMeeting("together/play", byUser: true);
    }

    void OnDuoClicked()
    {
        if (_togetherKey == "together/sleep") { _bedtime = false; WakeUp(); }
        else ShowTogether(byUser: true);
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
            StartMeeting("together/sleep", byUser: _bedtime);
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
        string T(string key) => Strings.Get(key);

        var hug = new Forms.ToolStripMenuItem(T("Together"), null, (_, _) => TogetherNow());
        var bed = new Forms.ToolStripMenuItem(T("Bedtime"));
        bed.Click += (_, _) =>
        {
            _bedtime = !IsAsleep;
            if (_bedtime) GoToSleep(); else WakeUp();
        };

        var size = Choices(T("Size"),
            new (string, object)[] { (T("Size.Small"), 90.0), (T("Size.Medium"), 120.0), (T("Size.Large"), 160.0), (T("Size.Huge"), 220.0) },
            v => (double)v == _settings.PetHeight,
            v =>
            {
                _settings.PetHeight = (double)v;
                _lib.SetHeight(_settings.PetHeight);
                if (_mode == Mode.Together) ShowTogether(sound: false);
            });

        var sound = Choices(T("Sound"),
            new (string, object)[] { (T("Sound.Off"), SoundMode.Off), (T("Sound.Interactions"), SoundMode.Interactions), (T("Sound.Always"), SoundMode.Always) },
            v => (SoundMode)v == _settings.Sound,
            v =>
            {
                _settings.Sound = (SoundMode)v;
                if (_settings.Sound == SoundMode.Off) StopAllSounds();
            });
        sound.DropDownItems.Add(new Forms.ToolStripSeparator());
        AddChoices(sound,
            new (string, object)[] { (T("Volume.Quiet"), 0.3), (T("Volume.Normal"), 0.6), (T("Volume.Loud"), 1.0) },
            v => (double)v == _settings.SoundVolume,
            v => _settings.SoundVolume = (double)v);

        var walk = Choices(T("WalkArea"),
            new (string, object)[] { (T("WalkArea.Full"), WalkArea.Full), (T("WalkArea.Left"), WalkArea.LeftHalf), (T("WalkArea.Right"), WalkArea.RightHalf) },
            v => (WalkArea)v == _settings.WalkArea,
            v =>
            {
                _settings.WalkArea = (WalkArea)v;
                if (_mode == Mode.Meeting) EndTogether(); // the meeting point may be outside the new area
            });

        var language = Choices(T("Language"),
            new (string, object)[] { (T("Language.Auto"), "auto"), ("English", "en"), ("Tiếng Việt", "vi") },
            v => (string)v == _settings.Language,
            v =>
            {
                _settings.Language = (string)v;
                Strings.Apply(_settings.Language);
                // Rebuild after this menu has closed.
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                {
                    var old = _menu;
                    _menu = BuildMenu();
                    _tray.ContextMenuStrip = _menu;
                    old.Dispose();
                });
            });

        var show = new Forms.ToolStripMenuItem(T("HidePets"));
        show.Click += (_, _) =>
        {
            _shown = !_shown;
            foreach (var p in _pets) p.SetShown(_shown);
            if (!_shown) { _duo.Hide(); _duoSound.Stop(); }
            else if (_mode == Mode.Together) _duo.Show();
        };

        var startup = new Forms.ToolStripMenuItem(T("StartWithWindows"));
        startup.Click += (_, _) => SetStartWithWindows(!StartsWithWindows());

        menu.Items.Add(hug);
        menu.Items.Add(bed);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(size);
        menu.Items.Add(walk);
        menu.Items.Add(sound);
        menu.Items.Add(show);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(T("OpenFolder"), null, (_, _) => Process.Start("explorer.exe", _lib.Root));
        menu.Items.Add(T("Reload"), null, (_, _) => _lib.Reload());
        menu.Items.Add(T("DownloadStarter"), null, (_, _) => DownloadStarterPack());
        menu.Items.Add(startup);
        menu.Items.Add(language);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(T("About"), null, (_, _) => AboutWindow.ShowAbout());
        menu.Items.Add(T("Donate"), null, (_, _) => AboutWindow.ShowAbout(donate: true));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(T("Exit"), null, (_, _) => Application.Current.Shutdown());

        menu.Opening += (_, _) =>
        {
            bed.Text = IsAsleep ? T("WakeUp") : T("Bedtime");
            show.Text = _shown ? T("HidePets") : T("ShowPets");
            startup.Checked = StartsWithWindows();
        };
        return menu;
    }

    /// <summary>A sub-menu of options where the current one is ticked. Picking one saves the settings.</summary>
    Forms.ToolStripMenuItem Choices(string title, (string Label, object Value)[] options, Func<object, bool> isCurrent, Action<object> pick)
    {
        var parent = new Forms.ToolStripMenuItem(title);
        AddChoices(parent, options, isCurrent, pick);
        return parent;
    }

    void AddChoices(Forms.ToolStripMenuItem parent, (string Label, object Value)[] options, Func<object, bool> isCurrent, Action<object> pick)
    {
        var items = new List<(Forms.ToolStripMenuItem Item, object Value)>();
        foreach (var (label, value) in options)
        {
            var item = new Forms.ToolStripMenuItem(label);
            item.Click += (_, _) =>
            {
                pick(value);
                _settings.Save();
            };
            parent.DropDownItems.Add(item);
            items.Add((item, value));
        }
        parent.DropDownOpening += (_, _) =>
        {
            foreach (var (item, value) in items) item.Checked = isCurrent(value);
        };
    }

    void StopAllSounds()
    {
        foreach (var p in _pets) p.Sound.Stop();
        _duoSound.Stop();
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
        StopAllSounds();
        _tray.Visible = false;
        _tray.Dispose();
        _lib.Dispose();
    }
}

using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace YierBubuPet;

/// <summary>
/// The pop-up panel shown from the tray icon or by right-clicking a pet: quick actions, alarms and
/// timers, alert options and settings, as cards. It closes when it loses focus.
/// </summary>
public sealed class ControlPanel : Window
{
    const double PanelWidth = 340;
    readonly Stage _stage;
    readonly ScrollViewer _scroll;
    DateTime _hiddenAt;

    public ControlPanel(Stage stage)
    {
        _stage = stage;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Width = PanelWidth + 20;
        SizeToContent = SizeToContent.Height;
        Title = "Yier & Bubu";

        _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(10, 10, 10, 10) };
        Content = new Border
        {
            Child = _scroll,
            Background = Ui.Background,
            CornerRadius = new CornerRadius(12),
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(10),
            Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = 0.22 },
        };
        Deactivated += (_, _) => { Hide(); _hiddenAt = DateTime.Now; };
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Hide(); };
    }

    /// <summary>Opens the panel with its corner at the cursor, kept inside the screen.</summary>
    public void ShowAtCursor()
    {
        // Clicking the tray icon while the panel is open first closes it (it loses focus); don't reopen it.
        if ((DateTime.Now - _hiddenAt).TotalMilliseconds < 300) return;
        Refresh();
        var cursor = Screens.CursorDip();
        var area = Screens.WorkAreaAt(cursor.X, cursor.Y);
        MaxHeight = area.Height - 8;
        _scroll.MaxHeight = MaxHeight - 20;

        Left = -10000; // measure off-screen first so it doesn't flash at the old position
        Show();
        UpdateLayout();
        double w = ActualWidth, h = ActualHeight;
        // Open up and to the left of the cursor (the tray is usually bottom-right), flipping if there's no room.
        double left = cursor.X - w + 10, top = cursor.Y - h + 10;
        if (left < area.Left) left = cursor.X - 10;
        if (top < area.Top) top = cursor.Y - 10;
        Left = Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - w));
        Top = Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - h));
        Activate();
    }

    /// <summary>
    /// Safety net for when Windows didn't give the panel focus (so it never gets "deactivated"):
    /// close it when the mouse is pressed outside it. Called from the frame loop.
    /// </summary>
    public void CloseIfClickedOutside()
    {
        if (!IsVisible || IsActive || !Native.MouseButtonDown()) return;
        var p = Screens.CursorDip();
        if (p.X < Left || p.X > Left + ActualWidth || p.Y < Top || p.Y > Top + ActualHeight)
        {
            Hide();
            _hiddenAt = DateTime.Now;
        }
    }

    /// <summary>Rebuilds the contents from the current state.</summary>
    public void Refresh()
    {
        string T(string key) => Strings.Get(key);
        var s = _stage.Settings;
        var root = new StackPanel { Width = PanelWidth };

        // ---- header ----
        var logo = LoadLogo();
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        title.Children.Add(Ui.Label("Yier & Bubu", 15, FontWeights.SemiBold));
        title.Children.Add(Ui.Label(Strings.Format("About.Version", Version()), 12, color: Ui.Subtle));
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        if (logo != null) left.Children.Add(new Image { Source = logo, Width = 42, Height = 42 });
        left.Children.Add(title);
        var icons = new StackPanel { Orientation = Orientation.Horizontal };
        icons.Children.Add(Ui.IconButton("", T("About"), () => { Hide(); AboutWindow.ShowAbout(); }));
        icons.Children.Add(Ui.IconButton("", T("Exit"), () => _stage.Exit()));
        var header = Ui.Row(left, icons, top: 0);
        header.Margin = new Thickness(6, 2, 2, 2);
        root.Children.Add(header);

        // ---- hero: quick actions ----
        var hero = new StackPanel();
        hero.Children.Add(Ui.Label(T("Panel.Hello"), 16, FontWeights.SemiBold));
        hero.Children.Add(Ui.Label(Status(), 13, color: Ui.Subtle, margin: new Thickness(0, 6, 0, 0)));
        hero.Children.Add(Ui.PrimaryButton(T("Together"), () => { _stage.TogetherNow(); Hide(); }));
        hero.Children.Add(Ui.Pair(
            Ui.SecondaryButton(_stage.IsAsleep ? T("WakeUp") : T("Bedtime"), () => { _stage.ToggleBedtime(); Refresh(); }),
            Ui.SecondaryButton(_stage.PetsShown ? T("HidePets") : T("ShowPets"), () => { _stage.TogglePetsShown(); Refresh(); })));
        root.Children.Add(new Border
        {
            Child = hero,
            Background = Ui.HeroGradient,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 16),
            Margin = new Thickness(0, 8, 0, 0),
        });

        // ---- alarms & timers ----
        var alarms = new StackPanel();
        alarms.Children.Add(Ui.Header(T("Panel.Alarms"), T("Panel.AddAlarm"), () => AddAlarm(timer: false)));
        var list = s.Alarms.OrderBy(a => a.NextRing()).ToList();
        if (list.Count == 0)
            alarms.Children.Add(Ui.Label(T("Panel.NoAlarms"), 13, color: Ui.Subtle, margin: new Thickness(0, 2, 0, 0)));
        foreach (var alarm in list)
        {
            var info = new StackPanel();
            info.Children.Add(Ui.Label(Describe(alarm), 13, FontWeights.SemiBold));
            if (alarm.Message.Length > 0) info.Children.Add(Ui.Label(alarm.Message, 12, color: Ui.Subtle));
            alarms.Children.Add(Ui.Row(info, Ui.Link(T("Panel.Remove"), () => { _stage.RemoveAlarm(alarm); Refresh(); })));
        }
        alarms.Children.Add(Ui.Divider());
        var quick = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var minutes in new[] { 5, 10, 25, 60 })
        {
            var pill = Ui.SecondaryButton(Strings.Format("Panel.Minutes", minutes), () =>
            {
                _stage.AddAlarm(new Alarm { IsTimer = true, Due = DateTime.Now.AddMinutes(minutes) });
                Refresh();
            });
            pill.MinWidth = 50;
            pill.Height = 28;
            pill.Padding = new Thickness(10, 0, 10, 0);
            pill.Margin = new Thickness(0, 0, 6, 0);
            quick.Children.Add(pill);
        }
        alarms.Children.Add(Ui.Row(Ui.Label(T("Panel.Timer")), Ui.Link(T("Panel.CustomTimer"), () => AddAlarm(timer: true))));
        quick.Margin = new Thickness(0, 8, 0, 0);
        alarms.Children.Add(quick);
        root.Children.Add(Ui.CardOf(alarms));

        // ---- alerts ----
        var alerts = new StackPanel();
        alerts.Children.Add(Ui.Header(T("Panel.Alerts"), T("Panel.TestAlert"), () => { Hide(); _stage.TestAlert(); }));
        alerts.Children.Add(Ui.Label(T("Panel.AlertsHint"), 12, color: Ui.Subtle));
        void Switch(string label, bool value, Action<bool> set) =>
            alerts.Children.Add(Ui.Row(Ui.Label(label), Ui.Toggle(value, v => { set(v); s.Save(); }), top: 10));
        Switch(T("Panel.OnNotifications"), s.AlertOnNotifications, v => s.AlertOnNotifications = v);
        Switch(T("Panel.Hourly"), s.AlertHourly, v => s.AlertHourly = v);
        Switch(Strings.Format("Panel.BatteryLow", s.BatteryLowPercent), s.AlertBatteryLow, v => s.AlertBatteryLow = v);
        Switch(T("Panel.Charger"), s.AlertCharger, v => s.AlertCharger = v);
        Switch(T("Panel.Unlock"), s.AlertUnlock, v => s.AlertUnlock = v);
        alerts.Children.Add(Ui.Divider());
        Switch(T("Panel.AlertSound"), s.AlertSound, v => s.AlertSound = v);
        root.Children.Add(Ui.CardOf(alerts));

        // ---- pets ----
        var pets = new StackPanel();
        pets.Children.Add(Ui.Header(T("Panel.Pets")));
        void Choice<TValue>(string label, (string, TValue)[] options, TValue current, Action<TValue> set)
        {
            pets.Children.Add(Ui.Label(label, 13, margin: new Thickness(0, 8, 0, 4)));
            var seg = Ui.Segmented(options, current, v => { set(v); s.Save(); });
            seg.HorizontalAlignment = HorizontalAlignment.Left;
            pets.Children.Add(seg);
        }
        Choice(T("Size"), new[] { (T("Size.Small"), 90.0), (T("Size.Medium"), 120.0), (T("Size.Large"), 160.0), (T("Size.Huge"), 220.0) },
            s.PetHeight, h => _stage.SetPetHeight(h));
        Choice(T("WalkArea"), new[] { (T("WalkArea.Full"), WalkArea.Full), (T("WalkArea.Left"), WalkArea.LeftHalf), (T("WalkArea.Right"), WalkArea.RightHalf) },
            s.WalkArea, a => _stage.SetWalkArea(a));
        Choice(T("Language"), new[] { (T("Language.Auto"), "auto"), ("English", "en"), ("Tiếng Việt", "vi") },
            s.Language, lang => { _stage.SetLanguage(lang); Dispatcher.BeginInvoke(Refresh); });
        pets.Children.Add(Ui.Divider());
        pets.Children.Add(Ui.Row(Ui.Label(T("StartWithWindows")), Ui.Toggle(Stage.StartsWithWindows(), Stage.SetStartWithWindows), top: 6));
        root.Children.Add(Ui.CardOf(pets));

        // ---- animations ----
        var files = new StackPanel();
        files.Children.Add(Ui.Header(T("Panel.Animations"), T("Panel.OpenFolder"), () => { Hide(); _stage.OpenAnimationsFolder(); }));
        files.Children.Add(Ui.Label(Strings.Format("Panel.AnimationCount", _stage.Library.TotalCount), 12, color: Ui.Subtle));
        files.Children.Add(Ui.IconLink("", T("Reload"), () => { _stage.ReloadAnimations(); Refresh(); }));
        files.Children.Add(Ui.IconLink("", T("DownloadStarter"), () => { Hide(); _stage.DownloadStarterPack(); }));
        root.Children.Add(Ui.CardOf(files));

        // ---- support ----
        root.Children.Add(Ui.CardOf(Ui.Row(
            Ui.Label(T("Donate"), 13, FontWeights.SemiBold),
            Ui.Link(T("Panel.Support") + " ›", () => { Hide(); AboutWindow.ShowAbout(donate: true); }), top: 0)));

        _scroll.Content = root;
    }

    async void AddAlarm(bool timer)
    {
        Hide();
        await System.Windows.Threading.Dispatcher.Yield();
        var dialog = new AlarmDialog(timer);
        if (dialog.ShowDialog() == true && dialog.Result != null) _stage.AddAlarm(dialog.Result);
    }

    string Status()
    {
        if (_stage.PetCount == 0) return Strings.Get("Panel.StatusNone");
        if (!_stage.PetsShown) return Strings.Get("Panel.StatusHidden");
        return Strings.Get(_stage.IsAsleep ? "Panel.StatusSleeping" : "Panel.StatusPlaying");
    }

    static string Describe(Alarm a)
    {
        var at = a.NextRing();
        if (a.IsTimer) return "⏳ " + Strings.Format("Panel.TimerAt", at.ToString("HH:mm"));
        string repeat = Strings.Get(a.Repeat switch
        {
            AlarmRepeat.Daily => "Alarm.Daily",
            AlarmRepeat.Weekdays => "Alarm.Weekdays",
            _ => "Alarm.Once",
        });
        return $"⏰ {a.Time:hh\\:mm} · {repeat}";
    }

    static string Version()
    {
        var asm = Assembly.GetExecutingAssembly();
        return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString(3) ?? "";
    }

    static BitmapSource? _logo;
    static BitmapSource? LoadLogo()
    {
        if (_logo != null) return _logo;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png");
        if (stream == null) return null;
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        frame.Freeze();
        return _logo = frame;
    }
}

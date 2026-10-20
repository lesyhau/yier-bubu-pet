using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YierBubuPet;

/// <summary>Asks for a new alarm (time, repeat, message) or timer (minutes, message).</summary>
public sealed class AlarmDialog : Window
{
    readonly bool _timer;
    readonly TextBox _when;
    readonly ComboBox? _repeat;
    readonly TextBox _message;
    readonly TextBlock _error;

    /// <summary>The alarm or timer to add, set when the user clicks OK.</summary>
    public Alarm? Result { get; private set; }

    public AlarmDialog(bool timer)
    {
        _timer = timer;
        Title = Strings.Get(timer ? "Alarm.NewTimer" : "Alarm.NewAlarm");
        Width = 340;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        Background = Ui.Background;

        var card = new StackPanel();
        card.Children.Add(Ui.Label(Title, 16, FontWeights.SemiBold, margin: new Thickness(0, 0, 0, 10)));

        card.Children.Add(Ui.Label(Strings.Get(timer ? "Alarm.Minutes" : "Alarm.Time"), 12, color: Ui.Subtle));
        _when = Input(timer ? "10" : DateTime.Now.AddMinutes(30).ToString("HH:mm"));
        card.Children.Add(_when);

        if (!timer)
        {
            card.Children.Add(Ui.Label(Strings.Get("Alarm.Repeat"), 12, color: Ui.Subtle, margin: new Thickness(0, 10, 0, 0)));
            _repeat = new ComboBox { Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(6, 4, 6, 4) };
            _repeat.Items.Add(Strings.Get("Alarm.Once"));
            _repeat.Items.Add(Strings.Get("Alarm.Daily"));
            _repeat.Items.Add(Strings.Get("Alarm.Weekdays"));
            _repeat.SelectedIndex = 0;
            card.Children.Add(_repeat);
        }

        card.Children.Add(Ui.Label(Strings.Get("Alarm.Message"), 12, color: Ui.Subtle, margin: new Thickness(0, 10, 0, 0)));
        _message = Input("");
        card.Children.Add(_message);

        _error = Ui.Label("", 12, color: Brushes.Firebrick, margin: new Thickness(0, 6, 0, 0));
        _error.Visibility = Visibility.Collapsed;
        card.Children.Add(_error);

        var ok = Ui.PrimaryButton(Strings.Get("Alarm.Save"), Save);
        var cancel = Ui.SecondaryButton(Strings.Get("Alarm.Cancel"), Close);
        ok.Margin = new Thickness(0);
        ok.Height = 34;
        var buttons = Ui.Pair(cancel, ok);
        buttons.Margin = new Thickness(0, 14, 0, 0);
        card.Children.Add(buttons);

        Content = new Border
        {
            Child = card,
            Background = Ui.Card,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 14, 18, 16),
            Margin = new Thickness(10),
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Save();
            else if (e.Key == Key.Escape) Close();
        };
        Loaded += (_, _) => { _when.Focus(); _when.SelectAll(); };
    }

    static TextBox Input(string text) => new()
    {
        Text = text,
        FontSize = 14,
        Padding = new Thickness(6, 4, 6, 4),
        Margin = new Thickness(0, 4, 0, 0),
        BorderBrush = Ui.Line,
    };

    void Save()
    {
        var alarm = new Alarm { IsTimer = _timer, Message = _message.Text.Trim(), Since = DateTime.Now };
        var input = _when.Text.Trim();
        if (_timer)
        {
            if (!double.TryParse(input.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)
                || minutes <= 0 || minutes > 24 * 60)
            {
                ShowError(Strings.Get("Alarm.BadMinutes"));
                return;
            }
            alarm.Due = DateTime.Now.AddMinutes(minutes);
        }
        else
        {
            if (!TryParseTime(input, out var time))
            {
                ShowError(Strings.Get("Alarm.BadTime"));
                return;
            }
            alarm.Time = time;
            alarm.Repeat = (AlarmRepeat)Math.Max(0, _repeat!.SelectedIndex);
        }
        Result = alarm;
        DialogResult = true;
    }

    /// <summary>Accepts "7:30", "07:30", "730", "7h30", "19.30" and "7".</summary>
    static bool TryParseTime(string s, out TimeSpan time)
    {
        time = default;
        s = s.ToLowerInvariant().Replace('h', ':').Replace('.', ':').Replace(' ', ':').TrimEnd(':');
        int hours, minutes = 0;
        if (s.Contains(':'))
        {
            var parts = s.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out hours) || !int.TryParse(parts[1], out minutes)) return false;
        }
        else if (int.TryParse(s, out int n))
        {
            if (s.Length <= 2) hours = n;
            else { hours = n / 100; minutes = n % 100; }
        }
        else return false;
        if (hours is < 0 or > 23 || minutes is < 0 or > 59) return false;
        time = new TimeSpan(hours, minutes, 0);
        return true;
    }

    void ShowError(string text)
    {
        _error.Text = text;
        _error.Visibility = Visibility.Visible;
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YierBubuPet;

/// <summary>Where donations go. Leave a value null to hide that part.</summary>
internal static class Donation
{
    /// <summary>
    /// Opened by the PayPal button. PayPal's "Send money" page; donors send to <see cref="PayPalEmail"/> there.
    /// Replace with a PayPal.Me link (https://paypal.me/yourname) if you get one.
    /// </summary>
    public static readonly string? PayPalUrl = "https://www.paypal.com/myaccount/transfer/homepage/pay";

    public static readonly string? PayPalEmail = "le.sy.hau@outlook.com";

    // Bank transfer (Vietnam). The QR code is the embedded resource "donate-qr.png"
    // (assets/donate/donate-qr.png, made by tools/make_qr.py); these are shown below it.
    public static readonly string? BankAccountName = "LE SY HAU";
    public static readonly string? BankAccountNumber = "0181003535874";
    public static readonly string? BankName = "Vietcombank";
}

/// <summary>About the app, with links and the donation options.</summary>
public sealed class AboutWindow : Window
{
    public const string RepoUrl = "https://github.com/lesyhau/yier-bubu-pet";
    static AboutWindow? _open;

    /// <summary>Shows the window (or brings it to the front), optionally scrolled to the donation section.</summary>
    public static void ShowAbout(bool donate = false)
    {
        if (_open == null)
        {
            _open = new AboutWindow();
            _open.Closed += (_, _) => _open = null;
            _open.Show();
        }
        _open.Activate();
        if (donate) _open._donate.BringIntoView();
    }

    readonly FrameworkElement _donate;

    AboutWindow()
    {
        Title = Strings.Get("About");
        Width = 420;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height - 20;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.Background;
        var logo = LoadImage("logo.png");
        if (logo != null) Icon = logo;

        var root = new StackPanel { Margin = new Thickness(14, 6, 14, 14) };

        // ---- about ----
        var about = new StackPanel();
        if (logo != null)
            about.Children.Add(new Image { Source = logo, Width = 96, Height = 96, Margin = new Thickness(0, 4, 0, 6) });
        about.Children.Add(Center(Ui.Label("Yier & Bubu", 20, FontWeights.Bold)));
        about.Children.Add(Center(Ui.Label(Strings.Format("About.Version", Version()), 12, color: Ui.Subtle)));
        about.Children.Add(Ui.Label(Strings.Get("About.Description"), 13, margin: new Thickness(0, 12, 0, 0)));
        about.Children.Add(Ui.Label(Strings.Get("About.FanProject"), 12, color: Ui.Subtle, margin: new Thickness(0, 10, 0, 0)));
        about.Children.Add(Ui.Label(Strings.Get("About.License"), 12, color: Ui.Subtle, margin: new Thickness(0, 2, 0, 0)));
        var source = Ui.Link(Strings.Get("About.Source") + " ›", () => Open(RepoUrl));
        source.Margin = new Thickness(0, 10, 0, 0);
        source.HorizontalAlignment = HorizontalAlignment.Left;
        about.Children.Add(source);
        root.Children.Add(Ui.CardOf(about));

        // ---- support ----
        var donate = new StackPanel();
        var intro = new StackPanel();
        intro.Children.Add(Ui.Label(Strings.Get("About.DonateTitle"), 16, FontWeights.SemiBold));
        intro.Children.Add(Ui.Label(Strings.Get("About.DonateText"), 13, color: Ui.Subtle, margin: new Thickness(0, 4, 0, 0)));
        donate.Children.Add(new Border { Child = intro, Margin = new Thickness(4, 16, 4, 0) });
        if (Donation.PayPalEmail != null || Donation.PayPalUrl != null) donate.Children.Add(PayPalCard());
        var qr = QrCard();
        if (qr != null) donate.Children.Add(qr);
        _donate = donate;
        root.Children.Add(donate);

        Content = new ScrollViewer
        {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
    }

    static Border PayPalCard()
    {
        var card = new StackPanel();
        card.Children.Add(Ui.Label("PayPal", 15, FontWeights.SemiBold));
        if (Donation.PayPalEmail is string email)
        {
            card.Children.Add(Ui.Label(Strings.Get("About.PayPalSendTo"), 13, color: Ui.Subtle, margin: new Thickness(0, 4, 0, 0)));
            var address = Ui.Label(email, 14, FontWeights.Bold);
            address.TextTrimming = TextTrimming.CharacterEllipsis;
            address.TextWrapping = TextWrapping.NoWrap;
            card.Children.Add(Ui.Row(address, Ui.CopyButton(email, Strings.Get("About.CopyEmail"))));
        }
        if (Donation.PayPalUrl is string url)
            card.Children.Add(Ui.PrimaryButton(Strings.Get("About.PayPal"), () => Open(url)));
        return Ui.CardOf(card);
    }

    static Border? QrCard()
    {
        var qr = LoadImage("donate-qr.png");
        if (qr == null) return null;

        var card = new StackPanel();
        card.Children.Add(Center(Ui.Label(Strings.Get("About.QrCaption"), 13, color: Ui.Subtle)));
        var image = new Image { Source = qr, Width = 200, Height = 200 * qr.PixelHeight / Math.Max(1, qr.PixelWidth) };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor); // keep the modules crisp
        card.Children.Add(new Border
        {
            Child = image,
            Background = Brushes.White,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
        });

        // Account details, like a banking app shows them.
        var details = new StackPanel();
        void Detail(string label, string? value, bool copy = false)
        {
            if (value == null) return;
            var text = Ui.Label(value, 14, FontWeights.Bold);
            UIElement right = text;
            if (copy)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(text);
                var button = Ui.CopyButton(value, Strings.Get("About.CopyNumber"));
                button.Margin = new Thickness(4, 0, -6, 0);
                row.Children.Add(button);
                right = row;
            }
            if (details.Children.Count > 0) details.Children.Add(new Border { Height = 1, Background = Ui.Line, Margin = new Thickness(0, 8, 0, 0) });
            details.Children.Add(Ui.Row(Ui.Label(label, 13, color: Ui.Subtle), right, top: 8));
        }
        Detail(Strings.Get("About.AccountName"), Donation.BankAccountName);
        Detail(Strings.Get("About.AccountNumber"), Donation.BankAccountNumber, copy: true);
        Detail(Strings.Get("About.Bank"), Donation.BankName);
        if (details.Children.Count > 0)
        {
            card.Children.Add(new Border
            {
                Child = details,
                Background = Ui.Background,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 2, 14, 10),
                Margin = new Thickness(0, 12, 0, 0),
            });
        }
        return Ui.CardOf(card);
    }

    static FrameworkElement Center(TextBlock t)
    {
        t.HorizontalAlignment = HorizontalAlignment.Center;
        t.TextAlignment = TextAlignment.Center;
        return t;
    }

    static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* no browser */ }
    }

    static string Version()
    {
        var asm = Assembly.GetExecutingAssembly();
        return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString(3) ?? "";
    }

    /// <summary>An embedded PNG, or null if this build doesn't include it.</summary>
    static BitmapSource? LoadImage(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream == null) return null;
        var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        var frame = BitmapFrame.Create(copy, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        frame.Freeze();
        return frame;
    }
}

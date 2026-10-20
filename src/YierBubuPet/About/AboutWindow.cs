using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YierBubuPet;

/// <summary>Where donations go. Leave a value null to hide that option.</summary>
internal static class Donation
{
    /// <summary>
    /// Opened by the PayPal button. PayPal's "Send money" page; donors paste <see cref="PayPalEmail"/> there.
    /// Replace with a PayPal.Me link (https://paypal.me/yourname) if you get one.
    /// </summary>
    public static readonly string? PayPalUrl = "https://www.paypal.com/myaccount/transfer/homepage/pay";

    /// <summary>The PayPal account email, shown with a Copy button (works when PayPal.Me isn't available).</summary>
    public static readonly string? PayPalEmail = "le.sy.hau@outlook.com";

    // The VietQR bank code is the embedded resource "donate-qr.png" (assets/donate/donate-qr.png, made by tools/make_qr.py).
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
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.White;
        var logo = LoadImage("logo.png");
        if (logo != null) Icon = logo;

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        if (logo != null)
            panel.Children.Add(new Image { Source = logo, Width = 112, Height = 112, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(Text("Yier & Bubu", 22, FontWeights.Bold, center: true));
        panel.Children.Add(Text(Strings.Format("About.Version", Version()), 12, color: Brushes.Gray, center: true));
        panel.Children.Add(Text(Strings.Get("About.Description"), 13, margin: new Thickness(0, 14, 0, 0)));
        panel.Children.Add(Text(Strings.Get("About.FanProject"), 12, color: Brushes.Gray, margin: new Thickness(0, 10, 0, 0)));
        panel.Children.Add(Text(Strings.Get("About.License"), 12, color: Brushes.Gray, margin: new Thickness(0, 2, 0, 0)));
        panel.Children.Add(Link(Strings.Get("About.Source"), RepoUrl));

        _donate = DonationSection();
        panel.Children.Add(_donate);

        var close = new Button
        {
            Content = Strings.Get("About.Close"),
            MinWidth = 90,
            Padding = new Thickness(12, 4, 12, 4),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
            IsCancel = true,
            IsDefault = true,
        };
        close.Click += (_, _) => Close();
        panel.Children.Add(close);

        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = SystemParameters.WorkArea.Height - 40 };
    }

    FrameworkElement DonationSection()
    {
        var box = new StackPanel();
        var border = new Border
        {
            Child = box,
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF0, 0xF4)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 14),
            Margin = new Thickness(0, 18, 0, 0),
        };
        box.Children.Add(Text(Strings.Get("About.DonateTitle"), 15, FontWeights.SemiBold));
        box.Children.Add(Text(Strings.Get("About.DonateText"), 13, margin: new Thickness(0, 4, 0, 0)));

        if (Donation.PayPalUrl is string paypalUrl)
        {
            var paypal = new Button
            {
                Content = Strings.Get("About.PayPal"),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xC4, 0x39)),
                FontWeight = FontWeights.SemiBold,
            };
            paypal.Click += (_, _) => Open(paypalUrl);
            box.Children.Add(paypal);
        }
        if (Donation.PayPalEmail is string email)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            row.Children.Add(new TextBox { Text = "PayPal: " + email, IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center });
            var copy = new Button { Content = Strings.Get("About.PayPalCopy"), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
            copy.Click += (_, _) =>
            {
                try { Clipboard.SetText(email); copy.Content = Strings.Get("About.Copied"); }
                catch { /* clipboard busy */ }
            };
            row.Children.Add(copy);
            box.Children.Add(row);
        }

        var qr = LoadImage("donate-qr.png");
        if (qr != null)
        {
            box.Children.Add(new Border
            {
                Child = new Image { Source = qr, Width = 220, Height = 220 * qr.PixelHeight / Math.Max(1, qr.PixelWidth) },
                Background = Brushes.White,
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 14, 0, 0),
            });
            RenderOptions.SetBitmapScalingMode((Image)((Border)box.Children[^1]).Child, BitmapScalingMode.NearestNeighbor);
            box.Children.Add(Text(Strings.Get("About.QrCaption"), 12, color: Brushes.DimGray, center: true, margin: new Thickness(0, 6, 0, 0)));
        }
        return border;
    }

    static TextBlock Text(string text, double size, FontWeight? weight = null, Brush? color = null, bool center = false, Thickness? margin = null) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = color ?? Brushes.Black,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = center ? TextAlignment.Center : TextAlignment.Left,
        Margin = margin ?? new Thickness(0),
    };

    static TextBlock Link(string text, string url)
    {
        var link = new Hyperlink(new Run(text)) { NavigateUri = new Uri(url) };
        link.RequestNavigate += (_, e) => { Open(e.Uri.AbsoluteUri); e.Handled = true; };
        return new TextBlock(link) { FontSize = 13, Margin = new Thickness(0, 10, 0, 0) };
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

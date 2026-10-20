using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YierBubuPet;

/// <summary>
/// Colours and small building blocks for the control panel and dialogs: white rounded cards on a
/// light grey background, link-style actions, toggle switches, pill selectors and a gradient button.
/// Built in code (no XAML) so every control looks the same on Windows 10 and 11.
/// </summary>
internal static class Ui
{
    public static readonly Brush Background = Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xF1, 0xF4)));
    public static readonly Brush Card = Brushes.White;
    public static readonly Brush Text = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x24)));
    public static readonly Brush Subtle = Freeze(new SolidColorBrush(Color.FromRgb(0x6E, 0x6B, 0x75)));
    public static readonly Brush Line = Freeze(new SolidColorBrush(Color.FromRgb(0xEC, 0xEA, 0xEF)));
    public static readonly Brush Accent = Freeze(new SolidColorBrush(Color.FromRgb(0xE0, 0x4F, 0x86)));
    public static readonly Brush AccentSoft = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xE4, 0xEE)));
    public static readonly Brush ButtonGradient = Freeze(new LinearGradientBrush(
        Color.FromRgb(0xFF, 0x6F, 0x9F), Color.FromRgb(0x9B, 0x6B, 0xFF), 0));
    public static readonly Brush HeroGradient = Freeze(new LinearGradientBrush(
        Color.FromRgb(0xFF, 0xF0, 0xF5), Color.FromRgb(0xEF, 0xEA, 0xFF), 20));
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    static T Freeze<T>(T brush) where T : Freezable { brush.Freeze(); return brush; }

    public static TextBlock Label(string text, double size = 13, FontWeight? weight = null, Brush? color = null, Thickness? margin = null) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = color ?? Text,
        TextWrapping = TextWrapping.Wrap,
        Margin = margin ?? new Thickness(0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>A white rounded card holding the given rows.</summary>
    public static Border CardOf(params UIElement[] rows)
    {
        var stack = new StackPanel();
        foreach (var r in rows) stack.Children.Add(r);
        return new Border
        {
            Child = stack,
            Background = Card,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 8, 0, 0),
        };
    }

    /// <summary>Section title on the left, an optional link on the right ("Open folder ›").</summary>
    public static UIElement Header(string title, string? link = null, Action? onLink = null)
    {
        var grid = Row(Label(title, 15, FontWeights.SemiBold), link != null ? Link(link + " ›", onLink!, Subtle) : null);
        grid.Margin = new Thickness(0, 0, 0, 6);
        return grid;
    }

    /// <summary>A line with something on the left and something right-aligned.</summary>
    public static Grid Row(UIElement left, UIElement? right, double top = 6)
    {
        var grid = new Grid { Margin = new Thickness(0, top, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(left);
        if (right != null)
        {
            Grid.SetColumn(right, 1);
            if (right is FrameworkElement fe) { fe.VerticalAlignment = VerticalAlignment.Center; fe.Margin = new Thickness(12, 0, 0, 0); }
            grid.Children.Add(right);
        }
        return grid;
    }

    public static Border Divider() => new()
    {
        Height = 1,
        Background = Line,
        Margin = new Thickness(0, 10, 0, 4),
    };

    /// <summary>Clickable text in the accent colour, underlined on hover.</summary>
    public static TextBlock Link(string text, Action onClick, Brush? color = null)
    {
        var t = Label(text, 13, color: color ?? Accent);
        t.Cursor = Cursors.Hand;
        t.TextWrapping = TextWrapping.NoWrap;
        t.MouseEnter += (_, _) => t.TextDecorations = TextDecorations.Underline;
        t.MouseLeave += (_, _) => t.TextDecorations = null;
        t.MouseLeftButtonUp += (_, e) => { e.Handled = true; onClick(); };
        return t;
    }

    /// <summary>A link with a Segoe icon in front, for list rows ("📁 Open animations folder").</summary>
    public static UIElement IconLink(string glyph, string text, Action onClick)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Cursor = Cursors.Hand, Background = Brushes.Transparent, Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(new TextBlock { Text = glyph, FontFamily = Icons, FontSize = 15, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        var label = Label(text);
        panel.Children.Add(label);
        panel.MouseEnter += (_, _) => label.Foreground = Accent;
        panel.MouseLeave += (_, _) => label.Foreground = Text;
        panel.MouseLeftButtonUp += (_, e) => { e.Handled = true; onClick(); };
        return panel;
    }

    /// <summary>A round icon button for the header (settings, exit, ...).</summary>
    public static Border IconButton(string glyph, string tooltip, Action onClick)
    {
        var b = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = tooltip,
            Child = new TextBlock { Text = glyph, FontFamily = Icons, FontSize = 15, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        b.MouseEnter += (_, _) => b.Background = Line;
        b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        b.MouseLeftButtonUp += (_, e) => { e.Handled = true; onClick(); };
        return b;
    }

    /// <summary>The big gradient button.</summary>
    public static Border PrimaryButton(string text, Action onClick)
    {
        var b = new Border
        {
            Background = ButtonGradient,
            CornerRadius = new CornerRadius(8),
            Height = 40,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 12, 0, 0),
            Child = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        b.MouseEnter += (_, _) => b.Opacity = 0.9;
        b.MouseLeave += (_, _) => b.Opacity = 1;
        b.MouseLeftButtonUp += (_, e) => { e.Handled = true; onClick(); };
        return b;
    }

    /// <summary>A white outlined button.</summary>
    public static Border SecondaryButton(string text, Action onClick)
    {
        var b = new Border
        {
            Background = Brushes.White,
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Height = 34,
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = text, Foreground = Text, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        b.MouseEnter += (_, _) => b.BorderBrush = Accent;
        b.MouseLeave += (_, _) => b.BorderBrush = Line;
        b.MouseLeftButtonUp += (_, e) => { e.Handled = true; onClick(); };
        return b;
    }

    /// <summary>Two buttons side by side, sharing the width.</summary>
    public static Grid Pair(UIElement a, UIElement b)
    {
        var g = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.Children.Add(a);
        Grid.SetColumn(b, 2);
        g.Children.Add(b);
        return g;
    }

    /// <summary>An on/off switch.</summary>
    public static Border Toggle(bool on, Action<bool> changed)
    {
        var knob = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = Brushes.White };
        var track = new Border
        {
            Width = 36,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(3),
            Cursor = Cursors.Hand,
            Child = knob,
        };
        void Show(bool value)
        {
            track.Background = value ? Accent : new SolidColorBrush(Color.FromRgb(0xC9, 0xC6, 0xCF));
            knob.HorizontalAlignment = value ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        Show(on);
        track.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            on = !on;
            Show(on);
            changed(on);
        };
        return track;
    }

    /// <summary>A row of pills where one is selected.</summary>
    public static Border Segmented<T>(IList<(string Label, T Value)> options, T current, Action<T> picked)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var pills = new List<(Border Pill, TextBlock Text, T Value)>();
        void Select(T value)
        {
            foreach (var (pill, text, v) in pills)
            {
                bool on = EqualityComparer<T>.Default.Equals(v, value);
                pill.Background = on ? Brushes.White : Brushes.Transparent;
                text.Foreground = on ? Accent : Subtle;
                text.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }
        foreach (var (label, value) in options)
        {
            var text = new TextBlock { Text = label, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };
            var pill = new Border { Child = text, CornerRadius = new CornerRadius(6), Padding = new Thickness(9, 4, 9, 4), Cursor = Cursors.Hand };
            pill.MouseLeftButtonUp += (_, e) => { e.Handled = true; Select(value); picked(value); };
            panel.Children.Add(pill);
            pills.Add((pill, text, value));
        }
        Select(current);
        return new Border { Child = panel, Background = Background, CornerRadius = new CornerRadius(8), Padding = new Thickness(2) };
    }
}

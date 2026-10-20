using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace YierBubuPet;

/// <summary>A small speech bubble shown above a pet during an alert. Clicking it dismisses the alert.</summary>
public sealed class SpeechBubble : Window
{
    readonly TextBlock _text;

    public SpeechBubble()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Title = "Yier & Bubu";
        SourceInitialized += (_, _) => Native.MakeToolWindow(this);

        _text = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Ui.Text,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 240,
            TextAlignment = TextAlignment.Center,
        };
        var bubble = new Border
        {
            Child = _text,
            Background = Brushes.White,
            BorderBrush = Ui.Accent,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 8, 14, 8),
        };
        // The little tail pointing down at the pet.
        var tail = new Polygon
        {
            Points = new PointCollection { new(0, 0), new(16, 0), new(8, 10) },
            Fill = Brushes.White,
            Stroke = Ui.Accent,
            StrokeThickness = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, -2, 0, 0),
        };
        var stack = new StackPanel { Margin = new Thickness(8) };
        stack.Children.Add(bubble);
        stack.Children.Add(tail);
        stack.Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 1, Opacity = 0.25 };
        Content = stack;
    }

    public void Say(string text) => _text.Text = text;

    /// <summary>Puts the tail just above (x, top), in DIPs.</summary>
    public void PlaceAbove(double x, double top)
    {
        if (ActualWidth <= 0) UpdateLayout();
        double left = System.Math.Round(x - ActualWidth / 2), y = System.Math.Round(top - ActualHeight + 6);
        if (Left != left) Left = left;
        if (Top != y) Top = y;
    }
}

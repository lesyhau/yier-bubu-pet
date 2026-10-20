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
        var body = new Border
        {
            Child = _text,
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 0, TailHeight),
            VerticalAlignment = VerticalAlignment.Top,
        };
        // The bubble and its tail are drawn as one outline, so there's no seam where they meet.
        var outline = new Path
        {
            Fill = Brushes.White,
            Stroke = Ui.Accent,
            StrokeThickness = Stroke,
            StrokeLineJoin = PenLineJoin.Round,
        };
        body.SizeChanged += (_, _) => outline.Data = Outline(body.ActualWidth, body.ActualHeight);

        var grid = new Grid { Margin = new Thickness(8) };
        grid.Children.Add(outline);
        grid.Children.Add(body);
        grid.Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 1, Opacity = 0.25 };
        Content = grid;
    }

    const double Stroke = 2, Radius = 14, TailHeight = 9, TailWidth = 18;

    /// <summary>A rounded box of w × h with a tail pointing down from the middle of its bottom edge.</summary>
    static Geometry Outline(double w, double h)
    {
        double s = Stroke / 2; // keep the stroke inside the box
        var box = new RectangleGeometry(new Rect(s, s, w - Stroke, h - Stroke), Radius - s, Radius - s);
        double mid = w / 2, bottom = h - s;
        var tail = new PathGeometry(new[]
        {
            new PathFigure(new Point(mid - TailWidth / 2, bottom - Radius / 2), new PathSegment[]
            {
                new LineSegment(new Point(mid + TailWidth / 2, bottom - Radius / 2), true),
                new LineSegment(new Point(mid + TailWidth / 2, bottom), true),
                new LineSegment(new Point(mid, bottom + TailHeight - s), true),
                new LineSegment(new Point(mid - TailWidth / 2, bottom), true),
            }, closed: true),
        });
        var shape = new CombinedGeometry(GeometryCombineMode.Union, box, tail);
        shape.Freeze();
        return shape;
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

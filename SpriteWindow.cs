using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YierBubuPet;

/// <summary>
/// A borderless, transparent, always-on-top window showing one animation.
/// Fully transparent pixels let clicks through to whatever is underneath.
/// </summary>
public sealed class SpriteWindow : Window
{
    readonly Image _image = new() { Stretch = Stretch.Fill, RenderTransformOrigin = new Point(0.5, 0.5) };
    readonly ScaleTransform _flip = new();
    int _frame;
    double _frameElapsed;

    public AnimationClip? Animation { get; private set; }

    public SpriteWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Title = "Yier & Bubu";
        _image.RenderTransform = _flip;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        Content = _image;
        SourceInitialized += (_, _) => Native.MakeToolWindow(this);
    }

    public void Play(AnimationClip clip)
    {
        Animation = clip;
        _frame = 0;
        _frameElapsed = 0;
        Width = clip.Width;
        Height = clip.Height;
        _image.Source = clip.Frames[0];
    }

    /// <summary>Show the clip facing right (true) or left (false).</summary>
    public void Face(bool right)
    {
        if (Animation == null) return;
        _flip.ScaleX = right == Animation.FacesRight ? 1 : -1;
    }

    /// <summary>Advances the animation; returns true when it wraps back to the first frame.</summary>
    public bool Advance(double ms)
    {
        if (Animation == null) return false;
        bool looped = false;
        _frameElapsed += ms;
        int start = _frame;
        while (_frameElapsed >= Animation.DelaysMs[_frame])
        {
            _frameElapsed -= Animation.DelaysMs[_frame];
            _frame = (_frame + 1) % Animation.Frames.Length;
            if (_frame == 0) looped = true;
        }
        if (_frame != start) _image.Source = Animation.Frames[_frame];
        return looped;
    }

    /// <summary>Positions the window so its bottom-centre sits at (x, feetY), in DIPs.</summary>
    public void PlaceAt(double x, double feetY)
    {
        double left = Math.Round(x - Width / 2), top = Math.Round(feetY - Height);
        if (Left != left) Left = left;
        if (Top != top) Top = top;
    }
}

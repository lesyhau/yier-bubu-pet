using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace YierBubuPet;

public enum PetState
{
    Idle,    // standing around
    Walk,    // walking along the bottom of the screen
    React,   // playing a one-off reaction (clicked, or just for fun)
    Sleep,
    Drag,    // being carried by the mouse
    Fall,    // dropped, falling back to the taskbar
    Wait,    // arrived where the stage sent it, waiting for the other pet
    Hidden,  // temporarily replaced by a "together" animation
}

/// <summary>One character (Yier or Bubu): decides what to do next and moves its window.</summary>
public sealed class Pet
{
    // Animation folders to try for each state, in order of preference.
    static readonly Dictionary<PetState, string[]> Anims = new()
    {
        [PetState.Idle] = new[] { "idle" },
        [PetState.Walk] = new[] { "walk", "idle" },
        [PetState.React] = new[] { "click", "idle" },
        [PetState.Sleep] = new[] { "sleep", "idle" },
        [PetState.Drag] = new[] { "drag", "click", "idle" },
        [PetState.Fall] = new[] { "drag", "click", "idle" },
        [PetState.Wait] = new[] { "idle" },
        [PetState.Hidden] = new[] { "idle" },
    };

    const double Gravity = 2600; // DIPs/s²

    readonly AnimationLibrary _lib;
    readonly Settings _settings;
    readonly Random _rng;

    double _stateMs, _idleLimitMs, _targetX, _vy;
    int _loops, _playToken;
    bool _summoned, _facingRight, _shown = true;
    string? _lastFile;
    bool _pressed, _dragging;
    Point _pressPos;
    Vector _grab;

    public string Name { get; }
    public SpriteWindow Window { get; } = new();
    public SoundChannel Sound { get; }
    public PetState State { get; private set; }
    public double X { get; private set; }   // centre, DIPs
    public double Y { get; private set; }   // feet, DIPs

    /// <summary>Raised when the user drags or clicks the pet.</summary>
    public event Action<Pet>? Touched;
    public event Action<Pet>? RightClicked;

    public Pet(string name, AnimationLibrary lib, Settings settings, Random rng, double x, double y)
    {
        Name = name;
        _lib = lib;
        _settings = settings;
        _rng = rng;
        Sound = new SoundChannel(settings);
        X = x;
        Y = y;
        _facingRight = rng.Next(2) == 0;

        Window.MouseLeftButtonDown += OnMouseDown;
        Window.MouseMove += OnMouseMove;
        Window.MouseLeftButtonUp += OnMouseUp;
        Window.MouseRightButtonUp += (_, e) => { e.Handled = true; RightClicked?.Invoke(this); };
    }

    public void Start() => Enter(PetState.Idle);

    public void Tick(double dt)
    {
        _stateMs += dt;
        if (Window.Advance(dt)) _loops++;

        var area = Screens.WorkAreaAt(X, Y - 1);
        var range = Screens.Restrict(area, _settings.WalkArea);
        switch (State)
        {
            case PetState.Idle:
                double margin = Window.Width > 0 ? Window.Width / 2 : 40;
                if (X < range.Left + margin - 1 || X > range.Right - margin + 1)
                {
                    // Outside the walking area (dropped there, or the setting changed): walk back in.
                    _targetX = Math.Clamp(X, range.Left + margin, Math.Max(range.Left + margin, range.Right - margin));
                    Enter(PetState.Walk);
                }
                else if (_stateMs > _idleLimitMs) ChooseNext(range);
                break;

            case PetState.Walk:
                double step = _settings.WalkSpeed * dt / 1000;
                if (Math.Abs(_targetX - X) <= step)
                {
                    X = _targetX;
                    Enter(_summoned ? PetState.Wait : PetState.Idle);
                }
                else
                {
                    _facingRight = _targetX > X;
                    X += _facingRight ? step : -step;
                    Window.Face(_facingRight);
                }
                break;

            case PetState.React:
                if ((_loops >= 1 && _stateMs > 1800) || _stateMs > 6000)
                    Enter(_summoned ? PetState.Wait : PetState.Idle);
                break;

            case PetState.Fall:
                _vy += Gravity * dt / 1000;
                Y += _vy * dt / 1000;
                if (Y >= area.Bottom)
                {
                    Y = area.Bottom;
                    Enter(PetState.Idle);
                }
                break;
        }

        if (State is not (PetState.Drag or PetState.Fall))
        {
            // Stay on the "floor" (just above the taskbar) and inside the screen.
            Y = area.Bottom;
            double half = Window.Width > 0 ? Window.Width / 2 : 40;
            X = Math.Clamp(X, area.Left + half, Math.Max(area.Left + half, area.Right - half));
        }
        Window.PlaceAt(X, Y);
    }

    /// <summary>Walk to x and wait there (used to bring the two pets together).</summary>
    public void WalkTo(double x)
    {
        _summoned = true;
        _targetX = x;
        Enter(Math.Abs(x - X) < 2 ? PetState.Wait : PetState.Walk);
    }

    /// <summary>Go back to roaming freely.</summary>
    public void Release()
    {
        _summoned = false;
        if (State is PetState.Wait or PetState.Walk or PetState.Hidden) Enter(PetState.Idle);
    }

    public void Hide()
    {
        State = PetState.Hidden;
        Sound.Stop();
        Window.Hide();
    }

    public void Reappear(double x)
    {
        X = x;
        _summoned = false;
        Enter(PetState.Idle);
        if (_shown) Window.Show();
    }

    public void Sleep()
    {
        _summoned = false;
        if (State is not (PetState.Drag or PetState.Fall or PetState.Sleep)) Enter(PetState.Sleep);
    }

    public void Wake()
    {
        if (State == PetState.Sleep) Enter(PetState.React);
    }

    /// <summary>Hide or show the pet without changing what it's doing.</summary>
    public void SetShown(bool shown)
    {
        _shown = shown;
        if (!shown) { Window.Hide(); Sound.Stop(); }
        else if (State != PetState.Hidden && Window.Animation != null) Window.Show();
    }

    /// <summary>Pick a fresh animation for the current state (e.g. after the pet size changed).</summary>
    public void Refresh()
    {
        if (State != PetState.Hidden) PlayFor(State, SoundCue.None);
    }

    void ChooseNext(Rect area)
    {
        double r = _rng.NextDouble();
        if (r < 0.55)
        {
            double half = Window.Width / 2;
            double lo = area.Left + half, hi = area.Right - half;
            double target = X;
            for (int i = 0; i < 5 && Math.Abs(target - X) < 150; i++)
                target = lo + _rng.NextDouble() * Math.Max(0, hi - lo);
            _targetX = target;
            Enter(PetState.Walk);
        }
        else if (r < 0.7)
            Enter(PetState.React);
        else
            Enter(PetState.Idle);
    }

    /// <param name="byUser">The user caused this (click or drag), which decides whether its sound plays.</param>
    void Enter(PetState state, bool newClip = true, bool byUser = false)
    {
        State = state;
        _stateMs = 0;
        _loops = 0;
        if (state == PetState.Idle) _idleLimitMs = 4000 + _rng.NextDouble() * 6000;
        if (state == PetState.Fall) _vy = 0;
        if (newClip) PlayFor(state, byUser ? SoundCue.User : SoundCue.Auto);
    }

    enum SoundCue { None, Auto, User }

    async void PlayFor(PetState state, SoundCue cue)
    {
        int token = ++_playToken;
        if (cue != SoundCue.None) Sound.Stop();
        string? file = null;
        foreach (var folder in Anims[state])
            if ((file = _lib.Pick($"{Name}/{folder}", _lastFile)) != null) break;
        if (file == null) return;

        var clip = await _lib.LoadAsync(file);
        if (token != _playToken || clip == null) return;

        _lastFile = file;
        _loops = 0;
        Window.Play(clip);
        if (cue != SoundCue.None && _shown && State != PetState.Hidden)
            Sound.Play(_lib.PickSound(file), cue == SoundCue.User);
        Window.Face(_facingRight);
        Window.PlaceAt(X, Y);
        if (_shown && State != PetState.Hidden && !Window.IsVisible) Window.Show();
    }

    void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = true;
        _pressPos = Screens.CursorDip();
        _grab = new Vector(_pressPos.X - X, _pressPos.Y - Y);
        Window.CaptureMouse();
        e.Handled = true;
    }

    void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed) return;
        var p = Screens.CursorDip();
        if (!_dragging && (p - _pressPos).Length > 5)
        {
            _dragging = true;
            _summoned = false;
            Enter(PetState.Drag, byUser: true);
            Touched?.Invoke(this);
        }
        if (_dragging)
        {
            X = p.X - _grab.X;
            Y = p.Y - _grab.Y;
            Window.PlaceAt(X, Y);
        }
    }

    void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed) return;
        _pressed = false;
        Window.ReleaseMouseCapture();
        if (_dragging)
        {
            _dragging = false;
            Enter(PetState.Fall, newClip: false);
        }
        else
        {
            _summoned = false;
            Enter(PetState.React, byUser: true);
            Touched?.Invoke(this);
        }
    }
}

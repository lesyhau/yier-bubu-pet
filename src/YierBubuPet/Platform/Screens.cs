using System.Windows;
using Forms = System.Windows.Forms;

namespace YierBubuPet;

internal static class Screens
{
    /// <summary>Physical pixels per DIP (1.25 at 125% scaling).</summary>
    public static double DpiScale { get; private set; } = 1;

    public static void Init()
    {
        using var g = System.Drawing.Graphics.FromHwnd(System.IntPtr.Zero);
        DpiScale = g.DpiX / 96.0;
    }

    /// <summary>Work area (screen minus taskbar) of the monitor containing the point, in DIPs.</summary>
    public static Rect WorkAreaAt(double x, double y)
    {
        var s = DpiScale;
        var wa = Forms.Screen.FromPoint(new System.Drawing.Point((int)(x * s), (int)(y * s))).WorkingArea;
        return new Rect(wa.Left / s, wa.Top / s, wa.Width / s, wa.Height / s);
    }

    public static Rect PrimaryWorkArea
    {
        get
        {
            var s = DpiScale;
            var wa = Forms.Screen.PrimaryScreen!.WorkingArea;
            return new Rect(wa.Left / s, wa.Top / s, wa.Width / s, wa.Height / s);
        }
    }

    /// <summary>The part of a work area the pets may walk in.</summary>
    public static Rect Restrict(Rect workArea, WalkArea mode)
    {
        double half = workArea.Width / 2;
        return mode switch
        {
            WalkArea.LeftHalf => new Rect(workArea.Left, workArea.Top, half, workArea.Height),
            WalkArea.RightHalf => new Rect(workArea.Left + half, workArea.Top, half, workArea.Height),
            _ => workArea,
        };
    }

    public static Point CursorDip()
    {
        var p = Native.CursorPixels();
        return new Point(p.X / DpiScale, p.Y / DpiScale);
    }
}

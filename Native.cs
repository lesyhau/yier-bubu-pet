using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YierBubuPet;

internal static class Native
{
    const int GWL_EXSTYLE = -20;
    const long WS_EX_TOOLWINDOW = 0x00000080;   // hide from Alt+Tab
    const long WS_EX_NOACTIVATE = 0x08000000;   // clicking a pet never steals focus

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value);

    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT p);

    public static void MakeToolWindow(Window w)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
    }

    /// <summary>Time since the last keyboard or mouse input anywhere in Windows.</summary>
    public static TimeSpan UserIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
    }

    /// <summary>Cursor position in physical pixels.</summary>
    public static Point CursorPixels()
    {
        GetCursorPos(out var p);
        return new Point(p.X, p.Y);
    }
}

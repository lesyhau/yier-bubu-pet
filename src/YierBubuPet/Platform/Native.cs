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

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int key);

    /// <summary>True while the left or right mouse button is held down (anywhere on screen).</summary>
    public static bool MouseButtonDown() => (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0;

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetClipboardData(uint format, IntPtr data);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalLock(IntPtr mem);

    [DllImport("kernel32.dll")]
    static extern bool GlobalUnlock(IntPtr mem);

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalFree(IntPtr mem);

    /// <summary>
    /// Puts text on the clipboard with plain Win32 calls. (WPF's Clipboard.SetText goes through OLE and
    /// can block the UI thread, which also runs the pets, while other apps are reading the clipboard.)
    /// Gives up after about 150 ms if another app keeps the clipboard open.
    /// </summary>
    public static bool CopyText(string text, Window owner)
    {
        const uint CF_UNICODETEXT = 13, GMEM_MOVEABLE = 0x0002;
        var hwnd = new WindowInteropHelper(owner).Handle;
        bool opened = false;
        for (int i = 0; i < 10 && !(opened = OpenClipboard(hwnd)); i++) System.Threading.Thread.Sleep(15);
        if (!opened) return false;
        try
        {
            EmptyClipboard();
            var mem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)((text.Length + 1) * 2));
            if (mem == IntPtr.Zero) return false;
            var ptr = GlobalLock(mem);
            Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
            Marshal.WriteInt16(ptr, text.Length * 2, 0);
            GlobalUnlock(mem);
            if (SetClipboardData(CF_UNICODETEXT, mem) != IntPtr.Zero) return true; // the clipboard owns it now
            GlobalFree(mem);
            return false;
        }
        finally
        {
            CloseClipboard();
        }
    }

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

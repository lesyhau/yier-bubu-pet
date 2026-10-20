using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Forms = System.Windows.Forms;

namespace YierBubuPet;

/// <summary>
/// Notices when Windows shows a notification pop-up (a "toast") from any app.
/// </summary>
/// <remarks>
/// Windows only lets Store-packaged apps read notifications, so instead this watches for the
/// toast window itself appearing: a small "Windows.UI.Core.CoreWindow" owned by the Windows shell
/// (ShellExperienceHost), near the corner of the screen. It's a best effort: no pop-up appears
/// (and nothing is noticed) in Do not disturb / Focus mode, and a future Windows update could change
/// how toasts are drawn.
/// </remarks>
public sealed class NotificationWatcher : IDisposable
{
    const uint EVENT_OBJECT_SHOW = 0x8002;
    const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    const int OBJID_WINDOW = 0;

    delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [DllImport("user32.dll")]
    static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint tid, uint flags);

    [DllImport("user32.dll")]
    static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    readonly WinEventProc _proc; // kept in a field so the garbage collector doesn't free the callback
    readonly IntPtr _hook;
    DateTime _last = DateTime.MinValue;

    /// <summary>Notifications that arrive while this is in the future are ignored (e.g. our own tray balloons).</summary>
    public DateTime IgnoreUntil { get; set; }

    /// <summary>Raised on the UI thread when a notification pop-up appears.</summary>
    public event Action? NotificationShown;

    /// <summary>Must be created on the UI thread (the hook calls back on the creating thread's message loop).</summary>
    public NotificationWatcher()
    {
        _proc = OnEvent;
        _hook = SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW, IntPtr.Zero, _proc, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }

    void OnEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            if (idObject != OBJID_WINDOW || hwnd == IntPtr.Zero || !IsToast(hwnd)) return;
            var now = DateTime.Now;
            if (now < IgnoreUntil || (now - _last).TotalSeconds < 3) return; // one pop-up can fire several events
            _last = now;
            NotificationShown?.Invoke();
        }
        catch
        {
            // Never let an exception escape into the Windows callback.
        }
    }

    static bool IsToast(IntPtr hwnd)
    {
        var cls = new StringBuilder(64);
        GetClassName(hwnd, cls, cls.Capacity);
        if (cls.ToString() != "Windows.UI.Core.CoreWindow") return false;

        GetWindowThreadProcessId(hwnd, out uint pid);
        string process;
        try { using var p = Process.GetProcessById((int)pid); process = p.ProcessName; }
        catch { return false; }
        if (!process.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase)) return false;

        // Toasts are small and sit against the right edge of the screen. This rules out the
        // notification centre and Quick settings (tall) and the volume/brightness pop-up (centred).
        if (!GetWindowRect(hwnd, out var r)) return false;
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        var screen = Forms.Screen.FromHandle(hwnd).Bounds;
        if (w <= 0 || h <= 0) return false;
        bool small = w < screen.Width * 0.35 && h < screen.Height * 0.32;
        bool atRightEdge = r.Right >= screen.Right - screen.Width * 0.06;
        return small && atRightEdge;
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) UnhookWinEvent(_hook);
    }
}

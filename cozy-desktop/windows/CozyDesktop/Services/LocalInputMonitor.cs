using System.Runtime.InteropServices;

namespace CozyDesktop.Services;

/// <summary>
/// Raises <see cref="PhysicalActivity"/> when the user touches THIS machine's own
/// keyboard or mouse — and ignores input that deskflow injected (so receiving the
/// other computer's cursor doesn't count). This is how multi-controller handoff
/// knows "the user just started using this machine."
///
/// Low-level hooks (WH_KEYBOARD_LL / WH_MOUSE_LL) run in-process on a thread with a
/// message loop — install from the WPF UI thread.
/// </summary>
public sealed class LocalInputMonitor : IDisposable
{
    public event Action? PhysicalActivity;

    private const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14, HC_ACTION = 0;
    private const uint LLKHF_INJECTED = 0x10;
    private const uint LLMHF_INJECTED = 0x01;

    private IntPtr _kbHook, _mouseHook;
    private HookProc? _kbProc, _mouseProc; // keep refs so the GC can't collect them
    private long _lastFire;

    public void Start()
    {
        _kbProc = KbCallback;
        _mouseProc = MouseCallback;
        var mod = GetModuleHandle(null);
        _kbHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbProc, mod, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, mod, 0);
    }

    public void Stop() => Dispose();

    private void Fire()
    {
        // Throttle: at most ~6 times/sec is plenty for "user is active here".
        var now = Environment.TickCount64;
        if (now - _lastFire < 150) return;
        _lastFire = now;
        PhysicalActivity?.Invoke();
    }

    private IntPtr KbCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if ((info.flags & LLKHF_INJECTED) == 0) Fire();
        }
        return CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if ((info.flags & LLMHF_INJECTED) == 0) Fire();
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_kbHook != IntPtr.Zero) { UnhookWindowsHookEx(_kbHook); _kbHook = IntPtr.Zero; }
        if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
        _kbProc = null; _mouseProc = null;
    }

    // ---- interop ----
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public int x, y; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}

using System.Runtime.InteropServices;

namespace WinLangSwitcher;

internal sealed class KeyboardHook(Action onCapsLockPressed) : IDisposable
{
    // Holding a strong reference prevents the delegate from being GC'd
    // while WinAPI still holds the function pointer.
    private NativeMethods.HookProc? _proc;
    private IntPtr _handle;

    public void Install()
    {
        if (_handle != IntPtr.Zero)
            return;

        _proc = Callback;
        _handle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            IntPtr.Zero,
            0);

        if (_handle == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"SetWindowsHookEx failed, GetLastError={err}");
        }
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return NativeMethods.CallNextHookEx(_handle, nCode, wParam, lParam);

        try
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if (data.vkCode == NativeMethods.VK_CAPITAL)
            {
                // Our own synthetic CapsLock events (e.g. startup reset) are
                // tagged so we let them flow to the OS instead of swallowing.
                if ((nuint) data.dwExtraInfo == NativeMethods.SyntheticInputMarker)
                    return NativeMethods.CallNextHookEx(_handle, nCode, wParam, lParam);

                var msg = wParam.ToInt32();
                if (msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
                    onCapsLockPressed();

                return 1; // swallow both keydown and keyup
            }
        }
        catch
        {
            // Never let an exception escape into user32 — Windows will silently
            // unhook us if the callback throws or takes too long.
        }

        return NativeMethods.CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_handle);
            _handle = IntPtr.Zero;
        }

        _proc = null;
    }
}

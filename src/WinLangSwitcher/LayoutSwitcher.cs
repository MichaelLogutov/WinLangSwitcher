using System.Runtime.InteropServices;

namespace WinLangSwitcher;

internal static class LayoutSwitcher
{
    private const int MaxLayouts = 16;

    public static IntPtr PickNext(IntPtr current, IntPtr[] all)
    {
        foreach (var hkl in all)
        {
            if (hkl != current)
                return hkl;
        }
        return current;
    }

    public static void ToggleNext()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
            return;

        var targetThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        if (targetThread == 0)
            return;

        // WM_INPUTLANGCHANGEREQUEST per MSDN must be posted to the *focused*
        // window, not the foreground top-level. For ordinary apps they coincide,
        // but composite hosts like the common file/folder dialog keep focus on a
        // deeply nested shell child — posting to the dialog frame is silently
        // ignored and the layout never flips.
        var target = ResolveFocusedWindow(targetThread, foreground);

        var current = NativeMethods.GetKeyboardLayout(targetThread);

        var buffer = new IntPtr[MaxLayouts];
        var count = NativeMethods.GetKeyboardLayoutList(buffer.Length, buffer);
        if (count <= 0)
            return;

        var all = new IntPtr[count];
        Array.Copy(buffer, all, count);

        var next = PickNext(current, all);
        if (next == current)
            return;

        var posted = NativeMethods.PostMessage(
            target,
            NativeMethods.WM_INPUTLANGCHANGEREQUEST,
            IntPtr.Zero,
            next);

        if (!posted)
            TryAttachAndActivate(targetThread, next);
    }

    private static IntPtr ResolveFocusedWindow(uint targetThread, IntPtr fallback)
    {
        var info = new NativeMethods.GUITHREADINFO
        {
            cbSize = (uint) Marshal.SizeOf<NativeMethods.GUITHREADINFO>(),
        };

        if (NativeMethods.GetGUIThreadInfo(targetThread, ref info) && info.hwndFocus != IntPtr.Zero)
            return info.hwndFocus;

        return fallback;
    }

    private static void TryAttachAndActivate(uint targetThread, IntPtr hkl)
    {
        var ourThread = NativeMethods.GetCurrentThreadId();
        if (ourThread == targetThread)
            return;

        if (!NativeMethods.AttachThreadInput(ourThread, targetThread, true))
            return;

        try
        {
            NativeMethods.ActivateKeyboardLayout(hkl, NativeMethods.KLF_SETFORPROCESS);
        }
        finally
        {
            NativeMethods.AttachThreadInput(ourThread, targetThread, false);
        }
    }
}

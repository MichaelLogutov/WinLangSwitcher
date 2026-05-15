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
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return;

        var targetThread = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        if (targetThread == 0)
            return;

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
            hwnd,
            NativeMethods.WM_INPUTLANGCHANGEREQUEST,
            IntPtr.Zero,
            next);

        if (!posted)
            TryAttachAndActivate(targetThread, next);
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

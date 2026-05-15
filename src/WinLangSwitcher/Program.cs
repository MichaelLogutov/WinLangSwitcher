namespace WinLangSwitcher;

internal static class Program
{
    private const string MutexName = @"Local\WinLangSwitcher_SingleInstance";

    private static int Main(string[] args) => args switch
    {
        [] => RunMain(),
        ["--install"] => RunInstall(),
        ["--uninstall"] => RunUninstall(),
        _ => RunUsage(),
    };

    private static int RunMain()
    {
        // Console-subsystem binaries get a fresh console allocated by Windows when
        // launched by Task Scheduler / Explorer (visible as a brief window flash).
        // Drop it before anything slow runs. When launched from a shell we share
        // the shell's console (process count > 1) — leave it so error output below
        // still reaches the user.
        if (HasOwnConsole())
            NativeMethods.FreeConsole();

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
            return 0; // another instance is already running

        using var hook = new KeyboardHook(LayoutSwitcher.ToggleNext);
        try
        {
            // Order matters only as defense-in-depth: the marker handshake in
            // KeyboardHook would pass our synthetic events through even after
            // Install, but doing the reset first sidesteps that path entirely.
            ResetCapsLockIfOn();
            hook.Install();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WinLangSwitcher: failed to install keyboard hook: {ex.Message}");
            return 1;
        }

        // Past startup — detach from any inherited shell console so we don't pin it.
        NativeMethods.FreeConsole();
        RunMessageLoop();

        return 0;
    }

    private static bool HasOwnConsole()
    {
        var buf = new uint[2];
        var count = NativeMethods.GetConsoleProcessList(buf, (uint) buf.Length);
        return count == 1;
    }

    private static void ResetCapsLockIfOn()
    {
        if (!IsCapsLockOn())
            return;

        // Tag with SyntheticInputMarker so KeyboardHook recognizes these as our
        // reset and passes them through; otherwise the hook swallows them and
        // the toggle never flips.
        var marker = NativeMethods.SyntheticInputMarker;
        NativeMethods.keybd_event((byte) NativeMethods.VK_CAPITAL, 0, 0, marker);
        NativeMethods.keybd_event((byte) NativeMethods.VK_CAPITAL, 0, NativeMethods.KEYEVENTF_KEYUP, marker);
    }

    private static bool IsCapsLockOn()
    {
        // GetKeyState reads the calling thread's input-queue snapshot of the
        // keyboard state. A freshly started background thread has an empty
        // queue, so the toggle bit reads 0 even when CapsLock is physically on.
        // Attach to the foreground thread (which has been processing input) so
        // GetKeyState reflects the real lock state.
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero)
            return false;

        var fgThread = NativeMethods.GetWindowThreadProcessId(fg, out _);
        if (fgThread == 0)
            return false;

        var ourThread = NativeMethods.GetCurrentThreadId();
        if (ourThread == fgThread)
            return (NativeMethods.GetKeyState((int) NativeMethods.VK_CAPITAL) & 1) != 0;

        if (!NativeMethods.AttachThreadInput(ourThread, fgThread, true))
            return false;
        try
        {
            return (NativeMethods.GetKeyState((int) NativeMethods.VK_CAPITAL) & 1) != 0;
        }
        finally
        {
            NativeMethods.AttachThreadInput(ourThread, fgThread, false);
        }
    }

    // Pumping GetMessage is what lets user32 deliver LL keyboard hook callbacks
    // on this thread; we have no windows/timers, so Translate/Dispatch are unneeded.
    private static void RunMessageLoop()
    {
        while (true)
        {
            var result = NativeMethods.GetMessage(out _, IntPtr.Zero, 0, 0);
            if (result is 0 or -1) // WM_QUIT or error
                break;
        }
    }

    private static int RunInstall()
    {
        try
        {
            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Environment.ProcessPath was null");

            Autostart.Install(exePath);
            Console.Out.WriteLine($"Autostart installed: {exePath}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WinLangSwitcher: install failed: {ex.Message}");
            return 2;
        }
    }

    private static int RunUninstall()
    {
        try
        {
            Autostart.Uninstall();
            Console.Out.WriteLine("Autostart uninstalled.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WinLangSwitcher: uninstall failed: {ex.Message}");
            return 2;
        }
    }

    private static int RunUsage()
    {
        Console.Error.WriteLine("Usage: WinLangSwitcher.exe [--install | --uninstall]");
        return 2;
    }
}

using System.Diagnostics;
using System.Security;
using System.Text;

namespace WinLangSwitcher;

internal static class Autostart
{
    private const string TaskName = "WinLangSwitcher";

    public static void Install(string exePath)
    {
        var userId = $"{Environment.UserDomainName}\\{Environment.UserName}";
        var xml = BuildXml(exePath, userId);

        var tempPath = Path.Combine(Path.GetTempPath(), $"winlangswitcher-{Guid.NewGuid():N}.xml");
        // schtasks /XML expects UTF-16 LE with BOM (the schema declares encoding="UTF-16").
        File.WriteAllText(tempPath, xml, new UnicodeEncoding(bigEndian: false, byteOrderMark: true));

        try
        {
            RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{tempPath}\" /F");
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best effort */ }
        }
    }

    public static void Uninstall()
    {
        if (!TaskExists(TaskName))
            return;

        RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
    }

    internal static string BuildXml(string exePath, string userId)
    {
        var exe = SecurityElement.Escape(exePath);
        var user = SecurityElement.Escape(userId);
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>true</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{exe}</Command>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static bool TaskExists(string taskName)
    {
        var (exitCode, _, _) = RunProcess("schtasks.exe", $"/Query /TN \"{taskName}\"");
        return exitCode == 0;
    }

    private static void RunSchtasks(string args)
    {
        var (exitCode, stdout, stderr) = RunProcess("schtasks.exe", args);
        if (exitCode != 0)
        {
            var message = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : stdout.Trim();
            throw new InvalidOperationException(
                $"schtasks {args} failed (exit {exitCode}): {message}");
        }
    }

    private static (int ExitCode, string Stdout, string Stderr) RunProcess(string fileName, string args)
    {
        var psi = new ProcessStartInfo(fileName, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {fileName}");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();

        p.WaitForExit();

        return (p.ExitCode, stdout, stderr);
    }
}

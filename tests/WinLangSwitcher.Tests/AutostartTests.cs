using System.Xml;
using Xunit;

namespace WinLangSwitcher.Tests;

public class AutostartTests
{
    [Fact]
    public void BuildXml_IsWellFormedAndContainsExeAndUser()
    {
        var xml = Autostart.BuildXml(
            exePath: @"C:\Users\micha\AppData\Local\WinLangSwitcher\WinLangSwitcher.exe",
            userId: @"DESKTOP-ABC\micha");

        var doc = new XmlDocument();
        doc.LoadXml(xml); // throws if malformed

        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("t", "http://schemas.microsoft.com/windows/2004/02/mit/task");

        var command = doc.SelectSingleNode("/t:Task/t:Actions/t:Exec/t:Command", nsmgr);
        Assert.NotNull(command);
        Assert.Equal(@"C:\Users\micha\AppData\Local\WinLangSwitcher\WinLangSwitcher.exe", command!.InnerText);

        var triggerUser = doc.SelectSingleNode("/t:Task/t:Triggers/t:LogonTrigger/t:UserId", nsmgr);
        Assert.NotNull(triggerUser);
        Assert.Equal(@"DESKTOP-ABC\micha", triggerUser!.InnerText);

        var principalUser = doc.SelectSingleNode("/t:Task/t:Principals/t:Principal/t:UserId", nsmgr);
        Assert.NotNull(principalUser);
        Assert.Equal(@"DESKTOP-ABC\micha", principalUser!.InnerText);
    }

    [Fact]
    public void BuildXml_SetsRunLevelAndLogonType()
    {
        var xml = Autostart.BuildXml(@"C:\app.exe", @"PC\user");

        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("t", "http://schemas.microsoft.com/windows/2004/02/mit/task");

        Assert.Equal("InteractiveToken",
            doc.SelectSingleNode("/t:Task/t:Principals/t:Principal/t:LogonType", nsmgr)!.InnerText);
        Assert.Equal("HighestAvailable",
            doc.SelectSingleNode("/t:Task/t:Principals/t:Principal/t:RunLevel", nsmgr)!.InnerText);
    }

    [Fact]
    public void BuildXml_HasNoExecutionTimeLimitAndIgnoresBatteryGuards()
    {
        var xml = Autostart.BuildXml(@"C:\app.exe", @"PC\user");

        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("t", "http://schemas.microsoft.com/windows/2004/02/mit/task");

        Assert.Equal("PT0S",
            doc.SelectSingleNode("/t:Task/t:Settings/t:ExecutionTimeLimit", nsmgr)!.InnerText);
        Assert.Equal("false",
            doc.SelectSingleNode("/t:Task/t:Settings/t:DisallowStartIfOnBatteries", nsmgr)!.InnerText);
        Assert.Equal("false",
            doc.SelectSingleNode("/t:Task/t:Settings/t:StopIfGoingOnBatteries", nsmgr)!.InnerText);
    }

    [Fact]
    public void BuildXml_EscapesSpecialCharsInExePath()
    {
        var xml = Autostart.BuildXml(@"C:\weird & path\app.exe", @"PC\user");

        var doc = new XmlDocument();
        doc.LoadXml(xml); // would throw if & was not escaped
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("t", "http://schemas.microsoft.com/windows/2004/02/mit/task");

        Assert.Equal(@"C:\weird & path\app.exe",
            doc.SelectSingleNode("/t:Task/t:Actions/t:Exec/t:Command", nsmgr)!.InnerText);
    }
}

using System;
using System.IO;
using RadoubLauncher.Services;
using Xunit;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for the platform-agnostic parts of desktop shortcut creation (#1437):
/// the freedesktop .desktop entry content and target-path construction. Actual
/// .lnk COM creation and filesystem writes are platform side effects verified
/// manually.
/// </summary>
public class DesktopShortcutServiceTests
{
    [Fact]
    public void BuildLinuxDesktopEntry_StartsWithDesktopEntryHeader()
    {
        var entry = DesktopShortcutService.BuildLinuxDesktopEntry(
            name: "Trebuchet", execPath: "/opt/trebuchet/Trebuchet", iconPath: "/opt/trebuchet/icon.png",
            comment: "Radoub launcher");

        Assert.StartsWith("[Desktop Entry]", entry);
    }

    [Fact]
    public void BuildLinuxDesktopEntry_ContainsRequiredKeys()
    {
        var entry = DesktopShortcutService.BuildLinuxDesktopEntry(
            name: "Trebuchet", execPath: "/opt/trebuchet/Trebuchet", iconPath: "/opt/trebuchet/icon.png",
            comment: "Radoub launcher");

        Assert.Contains("Type=Application", entry);
        Assert.Contains("Name=Trebuchet", entry);
        Assert.Contains("Exec=/opt/trebuchet/Trebuchet", entry);
        Assert.Contains("Icon=/opt/trebuchet/icon.png", entry);
        Assert.Contains("Comment=Radoub launcher", entry);
        Assert.Contains("Terminal=false", entry);
        Assert.Contains("Categories=", entry);
    }

    [Fact]
    public void BuildLinuxDesktopEntry_QuotesExecPathWithSpaces()
    {
        // freedesktop spec: arguments with spaces must be quoted in Exec.
        var entry = DesktopShortcutService.BuildLinuxDesktopEntry(
            name: "Trebuchet", execPath: "/home/My Apps/Trebuchet", iconPath: "/home/My Apps/icon.png",
            comment: "x");

        Assert.Contains("Exec=\"/home/My Apps/Trebuchet\"", entry);
    }

    [Fact]
    public void BuildLinuxDesktopEntry_OmitsIconLineWhenIconPathEmpty()
    {
        var entry = DesktopShortcutService.BuildLinuxDesktopEntry(
            name: "Trebuchet", execPath: "/opt/trebuchet/Trebuchet", iconPath: "", comment: "x");

        Assert.DoesNotContain("Icon=", entry);
    }

    [Fact]
    public void GetLinuxDesktopFilePath_SanitizesNameToLowercaseDashed()
    {
        // Neutral root (not /home/<user> or /Users/<user>) so the privacy scanner
        // doesn't flag a hardcoded user path. Path.Combine keeps it separator-agnostic.
        var appsDir = Path.Combine("apps");
        var path = DesktopShortcutService.GetLinuxDesktopFilePath(appsDir, "Trebuchet");

        Assert.Equal(Path.Combine(appsDir, "trebuchet.desktop"), path);
    }

    [Fact]
    public void GetWindowsShortcutPath_EndsWithLnkOnDesktop()
    {
        // Neutral root (not C:\Users\<user>) for the privacy scanner; Path.Combine
        // matches the host separator, and the production code uses Path.Combine too,
        // so this passes on both Windows and the Linux CI runner.
        var desktop = Path.Combine("desktop");
        var path = DesktopShortcutService.GetWindowsShortcutPath(desktop, "Trebuchet");

        Assert.Equal(Path.Combine(desktop, "Trebuchet.lnk"), path);
    }
}

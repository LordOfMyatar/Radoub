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
        var path = DesktopShortcutService.GetLinuxDesktopFilePath("/home/u/.local/share/applications", "Trebuchet");

        Assert.Equal(
            Path.Combine("/home/u/.local/share/applications", "trebuchet.desktop"),
            path);
    }

    [Fact]
    public void GetWindowsShortcutPath_EndsWithLnkOnDesktop()
    {
        var path = DesktopShortcutService.GetWindowsShortcutPath(@"C:\Users\u\Desktop", "Trebuchet");

        Assert.Equal(@"C:\Users\u\Desktop\Trebuchet.lnk", path);
    }
}

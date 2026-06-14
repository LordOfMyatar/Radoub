using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Quartermaster;

/// <summary>
/// Menu / keyboard-shortcut wiring smoke tests (#2362, epic #2359).
/// Proves the top-level menus open and key items stay wired; deep behavior is covered
/// by NavigationTests / ValueChangeTests.
/// </summary>
[Collection("QuartermasterSequential")]
public class MenuShortcutSmokeTests : QuartermasterTestBase
{
    private void Launch()
    {
        StartApplication();
        WaitForTitleContains("Quartermaster", DefaultTimeout);
        Thread.Sleep(500);
        MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
    }

    [Fact]
    [Trait("Category", "MenuSmoke")]
    public void TopLevelMenus_Exist()
    {
        Launch();

        Assert.NotNull(FindMenu("File"));
        Assert.NotNull(FindMenu("Edit"));
        Assert.NotNull(FindMenu("Character"));
        Assert.NotNull(FindMenu("View"));
    }

    [Fact]
    [Trait("Category", "MenuSmoke")]
    public void EditMenu_Has_FindAndReplace_Items()
    {
        Launch();

        ClickMenu("Edit");
        Thread.Sleep(300);

        Assert.NotNull(FindMenuItemOnDesktop("Find..."));
        Assert.NotNull(FindMenuItemOnDesktop("Replace..."));

        SendEscape();
    }

    [Fact]
    [Trait("Category", "MenuSmoke")]
    public void CreatureBrowser_TogglesVia_F4()
    {
        Launch();

        SendKeyboardShortcut(FlaUI.Core.WindowsAPI.VirtualKeyShort.F4);
        Thread.Sleep(500);

        MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
        Assert.NotNull(MainWindow);
        Assert.False(App!.HasExited, "F4 should toggle the creature browser, not exit the app");

        SendKeyboardShortcut(FlaUI.Core.WindowsAPI.VirtualKeyShort.F4);
        Thread.Sleep(300);
    }
}

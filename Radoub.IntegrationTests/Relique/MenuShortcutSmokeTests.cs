using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Relique;

/// <summary>
/// Menu / keyboard-shortcut wiring smoke tests (#2362, epic #2359).
/// </summary>
[Collection("ReliqueSequential")]
public class MenuShortcutSmokeTests : ReliqueTestBase
{
    private void Launch()
    {
        StartApplication();
        WaitForTitleContains("Relique", DefaultTimeout);
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
        Assert.NotNull(FindMenu("View"));
        Assert.NotNull(FindMenu("Help"));
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
    public void ItemBrowser_TogglesVia_F4()
    {
        Launch();

        SendKeyboardShortcut(FlaUI.Core.WindowsAPI.VirtualKeyShort.F4);
        Thread.Sleep(500);

        MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
        Assert.NotNull(MainWindow);
        Assert.False(App!.HasExited, "F4 should toggle the item browser, not exit the app");

        SendKeyboardShortcut(FlaUI.Core.WindowsAPI.VirtualKeyShort.F4);
        Thread.Sleep(300);
    }
}

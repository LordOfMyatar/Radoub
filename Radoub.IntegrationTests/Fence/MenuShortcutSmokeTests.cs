using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Fence;

/// <summary>
/// Menu / keyboard-shortcut wiring smoke tests (#2362, epic #2359).
/// Fence's Edit-menu Undo/Redo are currently disabled stubs (caught at build time by the
/// Tier-A dead-stub lint); when #2231 wires them, add an enabled-state assertion here.
/// </summary>
[Collection("FenceSequential")]
public class MenuShortcutSmokeTests : FenceTestBase
{
    private void Launch()
    {
        StartApplication();
        WaitForTitleContains("Fence", DefaultTimeout);
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
    public void StoreBrowser_TogglesVia_F4()
    {
        Launch();

        SendKeyboardShortcut(FlaUI.Core.WindowsAPI.VirtualKeyShort.F4);
        Thread.Sleep(500);

        MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
        Assert.NotNull(MainWindow);
        Assert.False(App!.HasExited, "F4 should toggle the store browser, not exit the app");

        SendKeyboardShortcut(FlaUI.Core.WindowsAPI.VirtualKeyShort.F4);
        Thread.Sleep(300);
    }
}

using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Manifest;

/// <summary>
/// Menu / keyboard-shortcut wiring smoke tests (#2362, epic #2359).
/// Manifest's Edit-menu Undo/Redo are currently disabled stubs (caught at build time by the
/// Tier-A dead-stub lint); when #2231 wires them, add an enabled-state assertion here.
/// </summary>
[Collection("ManifestSequential")]
public class MenuShortcutSmokeTests : ManifestTestBase
{
    private void Launch()
    {
        StartApplication();
        WaitForTitleContains("Manifest", DefaultTimeout);
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
}

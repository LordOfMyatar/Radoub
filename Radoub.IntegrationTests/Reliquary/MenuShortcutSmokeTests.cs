using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Reliquary;

/// <summary>
/// Menu / keyboard-shortcut wiring smoke tests (#2362, epic #2359).
/// Reliquary is the reference impl for wired Undo/Redo, so its Edit menu surfaces them
/// (unlike Manifest/Fence, whose stubs the Tier-A lint catches at build time).
/// </summary>
[Collection("ReliquarySequential")]
public class MenuShortcutSmokeTests : ReliquaryTestBase
{
    private void Launch()
    {
        StartApplication();
        WaitForTitleContains("Reliquary", DefaultTimeout);
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
        Assert.NotNull(FindMenu("Help"));
    }

    [Fact]
    [Trait("Category", "MenuSmoke")]
    public void EditMenu_Has_Undo_And_Redo_Items()
    {
        Launch();

        ClickMenu("Edit");
        Thread.Sleep(300);

        Assert.NotNull(FindMenuItemOnDesktop("Undo"));
        Assert.NotNull(FindMenuItemOnDesktop("Redo"));

        SendEscape();
    }
}

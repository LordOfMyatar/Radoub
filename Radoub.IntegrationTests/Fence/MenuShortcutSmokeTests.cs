using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Fence;

/// <summary>
/// Menu / keyboard-shortcut wiring smoke tests (#2362, epic #2359).
/// Fence's Edit-menu Undo/Redo are wired to the shared UndoRedoManager (#2255 / epic #2231);
/// they start disabled with no edit history. <see cref="EditMenu_HasUndoRedo_DisabledAtLaunch"/>
/// asserts the wired-but-empty state.
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
    public void EditMenu_HasUndoRedo_DisabledAtLaunch()
    {
        Launch();

        ClickMenu("Edit");
        Thread.Sleep(300);

        // Undo/Redo are now wired (#2255), not dead stubs — they exist but start disabled because
        // a freshly launched app has no edit history.
        var undo = FindMenuItemOnDesktop("Undo");
        var redo = FindMenuItemOnDesktop("Redo");
        Assert.NotNull(undo);
        Assert.NotNull(redo);
        Assert.False(undo!.Properties.IsEnabled.Value, "Undo should be disabled with no edit history");
        Assert.False(redo!.Properties.IsEnabled.Value, "Redo should be disabled with no edit history");

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

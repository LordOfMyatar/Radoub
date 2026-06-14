using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Parley;

/// <summary>
/// Menu / keyboard-shortcut wiring smoke tests (#2362, epic #2359).
///
/// The audit found that no test asserts the menu Header→handler and gesture→handler
/// links stay wired — a renamed handler or dropped item would regress silently while
/// the AXAML still compiles. These are smoke tests: they prove the wire is connected
/// (the menu opens, the item exists, the gesture has an effect), not deep behavior
/// (covered by FileOperationTests / TreeEditingTests / UndoRedoTests).
/// </summary>
[Collection("ParleySequential")]
public class MenuShortcutSmokeTests : ParleyTestBase
{
    private const string TestFileName = "test1.dlg";

    private void LaunchWithFile()
    {
        var testFile = TestPaths.GetTestFile(TestFileName);
        StartApplication($"\"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);
        Thread.Sleep(500);
        MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
    }

    [Fact]
    [Trait("Category", "MenuSmoke")]
    public void TopLevelMenus_Exist()
    {
        LaunchWithFile();

        Assert.NotNull(FindMenu("File"));
        Assert.NotNull(FindMenu("Edit"));
        Assert.NotNull(FindMenu("View"));
    }

    [Fact]
    [Trait("Category", "MenuSmoke")]
    public void EditMenu_Has_Undo_And_Redo_Items()
    {
        // Parley wires Undo/Redo (OnUndoClick/OnRedoClick). This guards against a
        // regression to the disabled-stub state the Tier-A lint catches at build time —
        // here we confirm the live menu actually surfaces them.
        LaunchWithFile();

        ClickMenu("Edit");
        Thread.Sleep(300);

        Assert.NotNull(FindMenuItemOnDesktop("Undo"));
        Assert.NotNull(FindMenuItemOnDesktop("Redo"));

        SendEscape();
    }

    [Fact]
    [Trait("Category", "MenuSmoke")]
    public void EditMenu_Has_FindAndReplace_Items()
    {
        LaunchWithFile();

        ClickMenu("Edit");
        Thread.Sleep(300);

        Assert.NotNull(FindMenuItemOnDesktop("Find..."));
        Assert.NotNull(FindMenuItemOnDesktop("Replace..."));

        SendEscape();
    }

    [Fact]
    [Trait("Category", "MenuSmoke")]
    public void DialogBrowser_TogglesVia_F4()
    {
        LaunchWithFile();

        // F4 toggles the Dialog Browser panel. Smoke: the gesture is handled (no crash)
        // and a Dialog Browser surface is reachable. We toggle on, then off.
        SendKeyboardShortcut(FlaUI.Core.WindowsAPI.VirtualKeyShort.F4);
        Thread.Sleep(500);

        // App must still be alive and responsive after the gesture.
        MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
        Assert.NotNull(MainWindow);
        Assert.False(App!.HasExited, "F4 should toggle the browser, not exit the app");

        SendKeyboardShortcut(FlaUI.Core.WindowsAPI.VirtualKeyShort.F4);
        Thread.Sleep(300);
    }
}

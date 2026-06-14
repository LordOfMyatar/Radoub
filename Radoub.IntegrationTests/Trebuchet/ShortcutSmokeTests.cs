using FlaUI.Core.WindowsAPI;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Trebuchet;

/// <summary>
/// Keyboard-shortcut wiring smoke tests (#2362, epic #2359).
///
/// Trebuchet has no menu bar — it's a launcher hub with tab navigation driven by
/// Ctrl+1-4 and Ctrl+Shift+F (Marlinspike), handled in MainWindow's OnKeyDown. Tab
/// *clicking* is covered by WorkspaceTests; these tests cover the keyboard path, which
/// was previously unverified and would regress silently if the OnKeyDown switch broke.
/// </summary>
[Collection("TrebuchetSequential")]
public class ShortcutSmokeTests : TrebuchetTestBase
{
    private void Launch()
    {
        StartApplication();
        WaitForTitleContains("Trebuchet", DefaultTimeout);
        Thread.Sleep(1000); // Wait for module + tabs to load
        MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
    }

    private static bool IsSelected(FlaUI.Core.AutomationElements.AutomationElement? tab)
    {
        var sel = tab?.Patterns.SelectionItem.PatternOrDefault;
        return sel?.IsSelected == true;
    }

    [Fact]
    [Trait("Category", "ShortcutSmoke")]
    public void CtrlNumber_SwitchesWorkspaceTab()
    {
        Launch();

        // Ctrl+2 → Factions tab.
        EnsureFocused();
        SendKeyboardShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_2);
        Thread.Sleep(500);

        var factionsTab = FindTabByName("Factions");
        Assert.NotNull(factionsTab);
        Assert.True(IsSelected(factionsTab), "Ctrl+2 should select the Factions tab");

        // Ctrl+1 → back to Module tab.
        EnsureFocused();
        SendKeyboardShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_1);
        Thread.Sleep(500);

        var moduleTab = FindTabByName("Module");
        Assert.NotNull(moduleTab);
        Assert.True(IsSelected(moduleTab), "Ctrl+1 should select the Module tab");
    }

    [Fact]
    [Trait("Category", "ShortcutSmoke")]
    public void CtrlShiftF_SwitchesToMarlinspike()
    {
        Launch();

        EnsureFocused();
        SendKeyboardShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_F);
        Thread.Sleep(500);

        var marlinspikeTab = FindTabByName("Marlinspike");
        Assert.NotNull(marlinspikeTab);
        Assert.True(IsSelected(marlinspikeTab), "Ctrl+Shift+F should select the Marlinspike tab");
    }
}

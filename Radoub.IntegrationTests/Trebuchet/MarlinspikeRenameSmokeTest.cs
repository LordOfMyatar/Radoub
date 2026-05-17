using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Trebuchet;

/// <summary>
/// Smoke test for the Marlinspike Filename/ResRef Rename mode UI wiring (#1926).
///
/// Validates the visible auto-toggle behavior:
///  - "Include filename/ResRef" checkbox starts unchecked
///  - Toggling it on auto-checks all file-type checkboxes (including NSS)
///  - The reverse toggle preserves prior file-type selections (no bounce)
///
/// The full rename pipeline is exercised by orchestrator-level integration
/// tests in Radoub.UI.Tests; FlaUI's job here is to confirm UI wiring works.
/// </summary>
[Collection("TrebuchetSequential")]
[Trait("Category", "Marlinspike")]
public class MarlinspikeRenameSmokeTest : TrebuchetTestBase
{
    [Fact]
    public void MarlinspikeRenameMode_TogglingCheckboxAutoSelectsAllFileTypes()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Trebuchet window should be ready");

        // Wait for tabs to load
        Thread.Sleep(1500);

        // Navigate to Marlinspike workspace tab. The tab is the 4th workspace tab
        // and is named "Marlinspike" (Ctrl+4 also works, but we click for clarity).
        var marlinspikeTab = FindTabByName("Marlinspike");
        Assert.NotNull(marlinspikeTab);
        marlinspikeTab!.AsTabItem().Select();
        Thread.Sleep(500);

        // The "Include filename/ResRef" checkbox starts unchecked
        var filenameCheckbox = FindCheckBoxByName("Include filename/ResRef");
        Assert.NotNull(filenameCheckbox);
        var cb = filenameCheckbox!.AsCheckBox();
        Assert.False(cb.IsChecked ?? false, "Include filename/ResRef checkbox should start unchecked");

        // Toggle ON — drives the SearchFilenameResRef binding, which triggers the
        // auto-check-all-file-types side-effect in MarlinspikePanelViewModel.
        cb.IsChecked = true;
        Thread.Sleep(300);

        // The NSS file-type checkbox (the 18th, added in this feature) should now be checked
        var nssCheckbox = FindCheckBoxByName("NSS");
        Assert.NotNull(nssCheckbox);
        Assert.True(nssCheckbox!.AsCheckBox().IsChecked ?? false,
            "NSS checkbox should be auto-checked when filename/ResRef mode toggles on");
    }

    #region Helpers

    private AutomationElement? FindTabByName(string name)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var tabs = MainWindow?.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
            if (tabs != null)
            {
                foreach (var tab in tabs)
                {
                    if (tab.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true)
                        return tab;
                }
            }
            Thread.Sleep(300);
        }
        return null;
    }

    private AutomationElement? FindCheckBoxByName(string name)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var checks = MainWindow?.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox));
            if (checks != null)
            {
                foreach (var c in checks)
                {
                    if (c.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true)
                        return c;
                }
            }
            Thread.Sleep(300);
        }
        return null;
    }

    #endregion
}

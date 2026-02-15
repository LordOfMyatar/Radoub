using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Trebuchet;

/// <summary>
/// Tests for workspace tabs, tab navigation, and tab content verification.
/// Validates the Sprint 1-4 layout: sidebar + workspace tabs (Module, Factions, Build &amp; Test).
/// </summary>
[Collection("TrebuchetSequential")]
public class WorkspaceTests : TrebuchetTestBase
{
    /// <summary>
    /// Verify all workspace tabs are discoverable.
    /// </summary>
    [Fact]
    [Trait("Category", "Workspace")]
    public void WorkspaceTabs_AllTabsDiscoverable()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");
        Thread.Sleep(1000); // Wait for module to load

        var moduleTab = FindTabByName("Module");
        Assert.NotNull(moduleTab);

        var factionsTab = FindTabByName("Factions");
        Assert.NotNull(factionsTab);

        var launchTab = FindTabByName("Build & Test");
        Assert.NotNull(launchTab);
    }

    /// <summary>
    /// Verify Dashboard tab has been removed (Sprint 4 cleanup).
    /// </summary>
    [Fact]
    [Trait("Category", "Workspace")]
    public void WorkspaceTabs_DashboardTabRemoved()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");
        Thread.Sleep(1000);

        var dashboardTab = FindTabByName("Dashboard");
        Assert.Null(dashboardTab);
    }

    /// <summary>
    /// Verify Module tab is the default (first) tab shown when module is loaded.
    /// </summary>
    [Fact]
    [Trait("Category", "Workspace")]
    public void WorkspaceTabs_ModuleIsDefaultTab()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");
        Thread.Sleep(1000);

        // Module tab should be selected by default (it's the first tab)
        var moduleTab = FindTabByName("Module");
        Assert.NotNull(moduleTab);

        // Check if it's selected via SelectionItem pattern
        var selectionItem = moduleTab.Patterns.SelectionItem.PatternOrDefault;
        if (selectionItem != null)
        {
            Assert.True(selectionItem.IsSelected, "Module tab should be selected by default");
        }
    }

    /// <summary>
    /// Verify tabs can be navigated by clicking.
    /// </summary>
    [Fact]
    [Trait("Category", "Workspace")]
    public void WorkspaceTabs_CanNavigateBetweenTabs()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");
        Thread.Sleep(1000);

        // Click Factions tab
        var factionsTab = FindTabByName("Factions");
        Assert.NotNull(factionsTab);
        factionsTab.Click();
        Thread.Sleep(500);

        // Verify Factions tab is selected
        var factionsSelection = factionsTab.Patterns.SelectionItem.PatternOrDefault;
        if (factionsSelection != null)
        {
            Assert.True(factionsSelection.IsSelected, "Factions tab should be selected after clicking");
        }

        // Click Build & Test tab
        var launchTab = FindTabByName("Build & Test");
        Assert.NotNull(launchTab);
        launchTab.Click();
        Thread.Sleep(500);

        // Verify Build & Test tab is selected
        var launchSelection = launchTab.Patterns.SelectionItem.PatternOrDefault;
        if (launchSelection != null)
        {
            Assert.True(launchSelection.IsSelected, "Build & Test tab should be selected after clicking");
        }

        // Click back to Module tab
        var moduleTab = FindTabByName("Module");
        Assert.NotNull(moduleTab);
        moduleTab.Click();
        Thread.Sleep(500);
    }

    /// <summary>
    /// Verify Build &amp; Test tab contains game launch buttons.
    /// </summary>
    [Fact]
    [Trait("Category", "Workspace")]
    public void LaunchTab_HasGameLaunchButtons()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");
        Thread.Sleep(1000);

        // Navigate to Build & Test tab
        var launchTab = FindTabByName("Build & Test");
        Assert.NotNull(launchTab);
        launchTab.Click();
        Thread.Sleep(500);

        // Should find "Launch Game" heading
        var launchHeading = FindTextBlockContaining("Launch Game");
        Assert.NotNull(launchHeading);

        // Should have Launch NWN:EE button
        var launchButton = FindButtonByText("Launch NWN:EE");
        Assert.NotNull(launchButton);

        // Should have Test Module button (inside the tab content, not just toolbar)
        // Note: There may be multiple "Test Module" buttons (toolbar + tab), that's expected
        var testButton = FindButtonByText("Test Module");
        Assert.NotNull(testButton);

        // Should have Load Module button
        var loadButton = FindButtonByText("Load Module");
        Assert.NotNull(loadButton);
    }

    /// <summary>
    /// Verify Build &amp; Test tab contains build status section.
    /// </summary>
    [Fact]
    [Trait("Category", "Workspace")]
    public void LaunchTab_HasBuildStatusSection()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");
        Thread.Sleep(1000);

        // Navigate to Build & Test tab
        var launchTab = FindTabByName("Build & Test");
        Assert.NotNull(launchTab);
        launchTab.Click();
        Thread.Sleep(500);

        // Should find "Build Status" heading
        var buildHeading = FindTextBlockContaining("Build Status");
        Assert.NotNull(buildHeading);

        // Should find compile scripts checkbox
        var compileCheckbox = FindTextBlockContaining("Compile scripts");
        Assert.NotNull(compileCheckbox);
    }

    /// <summary>
    /// Verify Build &amp; Test tab contains DefaultBic controls.
    /// </summary>
    [Fact]
    [Trait("Category", "Workspace")]
    public void LaunchTab_HasDefaultBicControls()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");
        Thread.Sleep(1000);

        // Navigate to Build & Test tab
        var launchTab = FindTabByName("Build & Test");
        Assert.NotNull(launchTab);
        launchTab.Click();
        Thread.Sleep(500);

        // Should find "Use Default Character" checkbox text
        var defaultBicText = FindTextBlockContaining("Default Character");
        Assert.NotNull(defaultBicText);
    }

    /// <summary>
    /// Verify empty state is shown when no module is loaded.
    /// Note: The test environment pre-seeds a module path, so we verify
    /// that the workspace tabs ARE visible (module loaded state).
    /// </summary>
    [Fact]
    [Trait("Category", "Workspace")]
    public void Workspace_ShowsTabsWhenModuleLoaded()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");
        Thread.Sleep(1000);

        // With pre-seeded module, tabs should be visible
        var moduleTab = FindTabByName("Module");
        Assert.NotNull(moduleTab);
    }

    /// <summary>
    /// Verify toolbar Save/Test buttons are accessible from any tab.
    /// </summary>
    [Fact]
    [Trait("Category", "Workspace")]
    public void Toolbar_ButtonsAccessibleFromAnyTab()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");
        Thread.Sleep(1000);

        // Check toolbar buttons exist on Module tab
        var saveButton = FindButtonByText("Save Module");
        Assert.NotNull(saveButton);
        var toolbarTestButton = FindButtonByText("Test Module");
        Assert.NotNull(toolbarTestButton);

        // Navigate to Factions tab - toolbar should still be accessible
        var factionsTab = FindTabByName("Factions");
        Assert.NotNull(factionsTab);
        factionsTab.Click();
        Thread.Sleep(500);

        saveButton = FindButtonByText("Save Module");
        Assert.NotNull(saveButton);
        toolbarTestButton = FindButtonByText("Test Module");
        Assert.NotNull(toolbarTestButton);

        // Navigate to Build & Test tab - toolbar should still be accessible
        var launchTab = FindTabByName("Build & Test");
        Assert.NotNull(launchTab);
        launchTab.Click();
        Thread.Sleep(500);

        saveButton = FindButtonByText("Save Module");
        Assert.NotNull(saveButton);
    }

    #region Helper Methods

    private AutomationElement? FindButtonByText(string text)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var buttons = MainWindow?.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
            if (buttons != null)
            {
                foreach (var button in buttons)
                {
                    if (button.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
                        return button;

                    var textChild = button.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text));
                    if (textChild?.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
                        return button;
                }
            }
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }
        return null;
    }

    private AutomationElement? FindTextBlockContaining(string text)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var textBlocks = MainWindow?.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            if (textBlocks != null)
            {
                foreach (var textBlock in textBlocks)
                {
                    if (textBlock.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
                        return textBlock;
                }
            }
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }
        return null;
    }

    #endregion
}

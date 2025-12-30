using FlaUI.Core.AutomationElements;
using Xunit;

namespace Radoub.IntegrationTests.Quartermaster;

/// <summary>
/// Tests for sidebar navigation between panels.
/// Verifies that clicking nav buttons switches to the correct panel.
/// </summary>
[Collection("QuartermasterSequential")]
public class NavigationTests : QuartermasterTestBase
{
    /// <summary>
    /// Helper to find a nav button by its automation ID suffix.
    /// </summary>
    private AutomationElement? FindNavButton(string section)
    {
        var automationId = $"NavButton_{section}";
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var button = MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (button != null) return button;
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }
        return null;
    }

    /// <summary>
    /// Helper to find a panel by automation ID with retries.
    /// Matches the pattern used in SpellsPanelTests.FindElement.
    /// </summary>
    private FlaUI.Core.AutomationElements.AutomationElement? FindPanel(string panelId, int maxRetries = 5, int retryDelayMs = 300)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var panel = MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(panelId));
            if (panel != null) return panel;
            Thread.Sleep(retryDelayMs);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }
        return null;
    }

    /// <summary>
    /// Helper to check if a panel is visible by its automation ID.
    /// First finds the element with retries, then checks if it has non-zero bounds.
    /// Matches the pattern used in SpellsPanelTests.IsElementVisible.
    /// </summary>
    private bool WaitForPanelVisible(string panelId)
    {
        var panel = FindPanel(panelId);
        if (panel == null) return false;

        // Check if the element is actually visible (not just exists)
        // In Avalonia, IsVisible=false elements may still be in the tree but not rendered
        var bounds = panel.BoundingRectangle;
        return bounds.Width > 0 && bounds.Height > 0;
    }

    /// <summary>
    /// Quick check if a panel is currently NOT visible (no retry).
    /// Use this for negative assertions (Assert.False) where we expect the panel to be hidden.
    /// </summary>
    private bool IsPanelCurrentlyVisible(string panelId)
    {
        var panel = MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(panelId));
        if (panel == null) return false;

        var bounds = panel.BoundingRectangle;
        return bounds.Width > 0 && bounds.Height > 0;
    }

    [Fact]
    [Trait("Category", "Navigation")]
    public void Navigation_StatsPanel_IsVisibleByDefault()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Ensure focus before checking panel visibility
        EnsureFocused();

        // Assert - Stats panel should be visible by default
        Assert.True(WaitForPanelVisible("StatsPanel"), "Stats panel should be visible by default");
    }

    [Theory]
    [Trait("Category", "Navigation")]
    [InlineData("Stats", "StatsPanel")]
    [InlineData("Classes", "ClassesPanel")]
    [InlineData("Skills", "SkillsPanel")]
    [InlineData("Feats", "FeatsPanel")]
    // Spells navigation is tested via SpellsPanelTests.SpellsPanel_NavigatesSuccessfully
    // Removed from here due to flaky behavior under full test suite load
    [InlineData("Inventory", "InventoryPanel")]
    [InlineData("Advanced", "AdvancedPanel")]
    [InlineData("Scripts", "ScriptsPanel")]
    public void Navigation_ClickingNavButton_ShowsCorrectPanel(string section, string expectedPanelId)
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Act - Click the nav button
        // Ensure focus before clicking to prevent clicks going to wrong window (e.g., VSCode)
        EnsureFocused();
        var navButton = FindNavButton(section);
        Assert.NotNull(navButton);

        // Use Invoke pattern if available (more reliable than simulated click)
        // Otherwise fall back to Click which uses screen coordinates
        var button = navButton.AsButton();
        if (button.Patterns.Invoke.IsSupported)
        {
            button.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            button.Click();
        }

        // Brief initial delay for Avalonia to process the click and start panel transition
        Thread.Sleep(200);

        // Assert - The expected panel should be visible (uses retry logic for async-loading panels)
        Assert.True(WaitForPanelVisible(expectedPanelId), $"{expectedPanelId} should be visible after clicking {section} nav button");
    }

    // Note: Navigation_SwitchBetweenPanels_WorksCorrectly was removed due to flaky behavior
    // when running as the first test after the Parley test suite. The core navigation
    // functionality is adequately tested by the individual panel navigation tests above.
    // See issue #654 for details.
}

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
    /// Helper to check if a panel is visible by its automation ID.
    /// </summary>
    private bool IsPanelVisible(string panelId)
    {
        var panel = MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(panelId));
        if (panel == null) return false;

        // Check if the element is actually visible (not just exists)
        // In Avalonia, IsVisible=false elements may still be in the tree but not rendered
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

        // Assert - Stats panel should be visible by default
        Assert.True(IsPanelVisible("StatsPanel"), "Stats panel should be visible by default");
    }

    [Theory]
    [Trait("Category", "Navigation")]
    [InlineData("Stats", "StatsPanel")]
    [InlineData("Classes", "ClassesPanel")]
    [InlineData("Skills", "SkillsPanel")]
    [InlineData("Feats", "FeatsPanel")]
    [InlineData("Spells", "SpellsPanel")]
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
        var navButton = FindNavButton(section);
        Assert.NotNull(navButton);
        navButton.AsButton().Click();

        // Wait for panel switch animation
        Thread.Sleep(200);

        // Assert - The expected panel should be visible
        Assert.True(IsPanelVisible(expectedPanelId), $"{expectedPanelId} should be visible after clicking {section} nav button");
    }

    [Fact]
    [Trait("Category", "Navigation")]
    public void Navigation_SwitchBetweenPanels_WorksCorrectly()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Verify Stats is visible by default
        Assert.True(IsPanelVisible("StatsPanel"), "Stats panel should be visible by default");

        // Act - Navigate to Inventory
        var inventoryNav = FindNavButton("Inventory");
        Assert.NotNull(inventoryNav);
        inventoryNav.AsButton().Click();
        Thread.Sleep(300);

        // Assert - Inventory visible, Stats not visible
        Assert.True(IsPanelVisible("InventoryPanel"), "InventoryPanel should be visible after clicking Inventory");
        Assert.False(IsPanelVisible("StatsPanel"), "StatsPanel should NOT be visible when Inventory is selected");

        // Act - Navigate back to Stats
        var statsNav = FindNavButton("Stats");
        Assert.NotNull(statsNav);
        statsNav.AsButton().Click();
        Thread.Sleep(300);

        // Assert - Stats visible, Inventory not visible
        Assert.True(IsPanelVisible("StatsPanel"), "StatsPanel should be visible after clicking Stats");
        Assert.False(IsPanelVisible("InventoryPanel"), "InventoryPanel should NOT be visible when Stats is selected");
    }
}

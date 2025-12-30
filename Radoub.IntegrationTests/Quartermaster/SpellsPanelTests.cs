using FlaUI.Core.AutomationElements;
using Xunit;

namespace Radoub.IntegrationTests.Quartermaster;

/// <summary>
/// Tests for the Spells panel functionality.
/// Verifies search, filtering, and class selection work correctly.
/// </summary>
[Collection("QuartermasterSequential")]
public class SpellsPanelTests : QuartermasterTestBase
{
    /// <summary>
    /// Helper to navigate to the Spells panel.
    /// </summary>
    private void NavigateToSpells()
    {
        var navButton = FindElement("NavButton_Spells");
        Assert.NotNull(navButton);
        navButton.AsButton().Click();
        Thread.Sleep(200);
    }

    /// <summary>
    /// Helper to find an element by automation ID with retries.
    /// </summary>
    private AutomationElement? FindElement(string automationId, int maxRetries = 5)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var element = MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (element != null) return element;
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }
        return null;
    }

    /// <summary>
    /// Checks if an element is visible by its automation ID.
    /// </summary>
    private bool IsElementVisible(string automationId)
    {
        var element = FindElement(automationId);
        if (element == null) return false;
        var bounds = element.BoundingRectangle;
        return bounds.Width > 0 && bounds.Height > 0;
    }

    [Fact]
    [Trait("Category", "SpellsPanel")]
    public void SpellsPanel_NavigatesSuccessfully()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Act
        NavigateToSpells();

        // Assert
        Assert.True(IsElementVisible("SpellsPanel"), "SpellsPanel should be visible after navigation");
    }

    [Fact]
    [Trait("Category", "SpellsPanel")]
    public void SpellsPanel_HasSearchBox()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Act
        NavigateToSpells();

        // Assert
        var searchBox = FindElement("SpellsSearchBox");
        Assert.NotNull(searchBox);
    }

    [Fact]
    [Trait("Category", "SpellsPanel")]
    public void SpellsPanel_HasLevelFilter()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Act
        NavigateToSpells();

        // Assert
        var levelFilter = FindElement("SpellsLevelFilter");
        Assert.NotNull(levelFilter);
    }

    [Fact]
    [Trait("Category", "SpellsPanel")]
    public void SpellsPanel_HasSchoolFilter()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Act
        NavigateToSpells();

        // Assert
        var schoolFilter = FindElement("SpellsSchoolFilter");
        Assert.NotNull(schoolFilter);
    }

    [Fact]
    [Trait("Category", "SpellsPanel")]
    public void SpellsPanel_HasStatusFilter()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Act
        NavigateToSpells();

        // Assert
        var statusFilter = FindElement("SpellsStatusFilter");
        Assert.NotNull(statusFilter);
    }

    [Fact]
    [Trait("Category", "SpellsPanel")]
    public void SpellsPanel_HasClassRadioButtons()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Act
        NavigateToSpells();

        // Assert - Check for first 3 class radio buttons
        var class1 = FindElement("SpellsClass1");
        Assert.NotNull(class1);

        var class2 = FindElement("SpellsClass2");
        Assert.NotNull(class2);

        var class3 = FindElement("SpellsClass3");
        Assert.NotNull(class3);
    }

    [Fact]
    [Trait("Category", "SpellsPanel")]
    public void SpellsPanel_HasClearSearchButton()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Act
        NavigateToSpells();

        // Assert
        var clearButton = FindElement("SpellsClearSearch");
        Assert.NotNull(clearButton);
    }

    [Fact(Skip = "Avalonia Expander AutomationId not exposed correctly in FlaUI")]
    [Trait("Category", "SpellsPanel")]
    public void SpellsPanel_HasMetaMagicExpander()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Act
        NavigateToSpells();
        // Wait for panel to fully render including footer elements
        Thread.Sleep(500);

        // Assert - Use extended retries for footer elements
        var expander = FindElement("MetaMagicExpander", maxRetries: 10);
        Assert.NotNull(expander);
    }

    [Fact]
    [Trait("Category", "SpellsPanel")]
    public void SpellsPanel_HasSpellsSummary()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Act
        NavigateToSpells();

        // Assert
        var summary = FindElement("SpellsSummary");
        Assert.NotNull(summary);
    }

    [Fact]
    [Trait("Category", "SpellsPanel")]
    public void SpellsPanel_HasPlaceholderActionButtons()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Act
        NavigateToSpells();

        // Assert - Check placeholder action buttons exist
        var clearListButton = FindElement("ClearSpellListButton");
        Assert.NotNull(clearListButton);

        var saveListButton = FindElement("SaveSpellListButton");
        Assert.NotNull(saveListButton);

        var loadListButton = FindElement("LoadSpellListButton");
        Assert.NotNull(loadListButton);
    }
}

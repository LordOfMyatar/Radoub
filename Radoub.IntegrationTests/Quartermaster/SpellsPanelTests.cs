using FlaUI.Core.AutomationElements;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Quartermaster;

/// <summary>
/// Tests for the Spells panel functionality.
/// Uses consolidated step-based testing for efficient diagnostics.
/// </summary>
[Collection("QuartermasterSequential")]
public class SpellsPanelTests : QuartermasterTestBase
{
    /// <summary>
    /// Consolidated test for Spells panel UI elements.
    /// Replaces 10 individual tests with diagnostic step tracking.
    /// Continue on first failure, stop on second (1 failure = specific issue, 2 = dumpster fire).
    /// </summary>
    [Fact]
    [Trait("Category", "SpellsPanel")]
    public void SpellsPanel_AllUIElementsPresent()
    {
        var steps = new TestSteps();

        // Setup steps
        steps.Run("Launch Quartermaster", () =>
        {
            StartApplication();
            return true;
        });

        steps.Run("Wait for window ready", () =>
            WaitForTitleContains("Quartermaster", DefaultTimeout));

        steps.Run("Navigate to Spells panel", () =>
        {
            var navButton = FindElement("NavButton_Spells");
            if (navButton == null) return false;
            navButton.AsButton().Click();
            Thread.Sleep(200);
            return IsElementVisible("SpellsPanel");
        });

        // Core UI elements - search and filters
        steps.Run("SearchBox exists", () => FindElement("SpellsSearchBox") != null);
        steps.Run("ClearSearch button exists", () => FindElement("SpellsClearSearch") != null);
        steps.Run("LevelFilter exists", () => FindElement("SpellsLevelFilter") != null);
        steps.Run("SchoolFilter exists", () => FindElement("SpellsSchoolFilter") != null);
        steps.Run("StatusFilter exists", () => FindElement("SpellsStatusFilter") != null);

        // Class radio buttons (check first 3)
        steps.Run("Class1 radio exists", () => FindElement("SpellsClass1") != null);
        steps.Run("Class2 radio exists", () => FindElement("SpellsClass2") != null);
        steps.Run("Class3 radio exists", () => FindElement("SpellsClass3") != null);

        // Summary and action buttons
        steps.Run("SpellSlotSummary exists", () => FindElement("SpellSlotSummary") != null);
        steps.Run("ClearSpellList button exists", () => FindElement("ClearSpellListButton") != null);
        steps.Run("SaveSpellList button exists", () => FindElement("SaveSpellListButton") != null);
        steps.Run("LoadSpellList button exists", () => FindElement("LoadSpellListButton") != null);

        steps.AssertAllPassed();
    }

    /// <summary>
    /// Test for MetaMagic expander - separate because it has known Avalonia issues.
    /// Skipped until Avalonia AutomationId exposure is fixed.
    /// </summary>
    [Fact(Skip = "Avalonia Expander AutomationId not exposed correctly in FlaUI")]
    [Trait("Category", "SpellsPanel")]
    public void SpellsPanel_MetaMagicExpander_Exists()
    {
        var steps = new TestSteps();

        steps.Run("Launch and navigate", () =>
        {
            StartApplication();
            WaitForTitleContains("Quartermaster", DefaultTimeout);
            var navButton = FindElement("NavButton_Spells");
            navButton?.AsButton().Click();
            Thread.Sleep(500); // Extra time for footer elements
            return IsElementVisible("SpellsPanel");
        });

        steps.Run("MetaMagicExpander exists", () => FindElement("MetaMagicExpander", maxRetries: 10) != null);

        steps.AssertAllPassed();
    }
}

using FlaUI.Core.AutomationElements;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Quartermaster;

/// <summary>
/// Tests for the Classes panel functionality.
/// Tests level-up, add class, and level constraints.
/// </summary>
[Collection("QuartermasterSequential")]
public class ClassesPanelTests : QuartermasterTestBase
{
    /// <summary>
    /// Navigate to Classes panel and ensure it's visible.
    /// </summary>
    private bool NavigateToClassesPanel()
    {
        var navButton = FindElement("NavButton_Classes");
        if (navButton == null) return false;

        var button = navButton.AsButton();
        if (button.Patterns.Invoke.IsSupported)
        {
            button.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            button.Click();
        }

        Thread.Sleep(200);
        return IsElementVisible("ClassesPanel");
    }

    /// <summary>
    /// Test that Classes panel UI elements are present.
    /// </summary>
    [Fact]
    [Trait("Category", "ClassesPanel")]
    public void ClassesPanel_AllUIElementsPresent()
    {
        var steps = new TestSteps();

        steps.Run("Launch Quartermaster", () =>
        {
            StartApplication();
            return true;
        });

        steps.Run("Wait for window ready", () =>
            WaitForTitleContains("Quartermaster", DefaultTimeout));

        steps.Run("Navigate to Classes panel", () => NavigateToClassesPanel());

        // Core UI elements
        steps.Run("TotalLevelText exists", () => FindElement("TotalLevelText") != null);
        steps.Run("ClassSlotsList exists", () => FindElement("ClassSlotsList") != null);
        steps.Run("AddClassButton exists", () => FindElement("AddClassButton") != null);

        // Alignment controls
        steps.Run("AlignmentName exists", () => FindElement("AlignmentName") != null);
        steps.Run("GoodEvilSlider exists", () => FindElement("GoodEvilSlider") != null);
        steps.Run("LawChaosSlider exists", () => FindElement("LawChaosSlider") != null);

        // Package controls
        steps.Run("PackageText exists", () => FindElement("PackageText") != null);
        steps.Run("PackagePickerButton exists", () => FindElement("PackagePickerButton") != null);

        steps.AssertAllPassed();
    }

    /// <summary>
    /// Test that Add Class button opens the class picker window.
    /// Requires a creature to be loaded AND game data (classes.2da) to be available.
    /// Skipped in test environment without full game data.
    /// </summary>
    [Fact(Skip = "Requires game data (classes.2da) not available in test environment")]
    [Trait("Category", "ClassesPanel")]
    public void ClassesPanel_AddClassButton_OpensClassPicker()
    {
        var steps = new TestSteps();

        // Load test creature file - picker requires creature data
        var testCreature = TestPaths.GetTestModuleFile("parleypirate.utc");

        steps.Run("Launch with creature and navigate", () =>
        {
            StartApplication($"--file \"{testCreature}\"");
            WaitForTitleContains("parleypirate", DefaultTimeout);
            return NavigateToClassesPanel();
        });

        steps.Run("Click Add Class button", () =>
        {
            var addClassButton = FindElement("AddClassButton");
            if (addClassButton == null) return false;

            var button = addClassButton.AsButton();
            if (button.Patterns.Invoke.IsSupported)
            {
                button.Patterns.Invoke.Pattern.Invoke();
            }
            else
            {
                button.Click();
            }
            Thread.Sleep(500); // Wait for dialog to open
            return true;
        });

        steps.Run("Class picker window opens", () =>
        {
            // Look for the ClassPickerWindow
            var pickerWindow = FindPopupByTitle("Select Class", maxRetries: 10);
            if (pickerWindow == null) return false;

            // Verify the picker has expected elements
            var classesListBox = pickerWindow.FindFirstDescendant(cf => cf.ByAutomationId("ClassesListBox"));
            if (classesListBox == null) return false;

            var okButton = pickerWindow.FindFirstDescendant(cf => cf.ByAutomationId("OkButton"));
            if (okButton == null) return false;

            var cancelButton = pickerWindow.FindFirstDescendant(cf => cf.ByAutomationId("CancelButton"));
            return cancelButton != null;
        });

        steps.Run("Close picker with Cancel", () =>
        {
            var pickerWindow = FindPopupByTitle("Select Class", maxRetries: 5);
            if (pickerWindow == null) return false;

            var cancelButton = pickerWindow.FindFirstDescendant(cf => cf.ByAutomationId("CancelButton"));
            if (cancelButton == null) return false;

            cancelButton.AsButton().Click();
            Thread.Sleep(300);
            return true;
        });

        steps.AssertAllPassed();
    }

    /// <summary>
    /// Test that Package picker button opens the package picker window.
    /// Requires a creature to be loaded AND game data (packages.2da) to be available.
    /// Skipped in test environment without full game data.
    /// </summary>
    [Fact(Skip = "Requires game data (packages.2da) not available in test environment")]
    [Trait("Category", "ClassesPanel")]
    public void ClassesPanel_PackagePickerButton_OpensPackagePicker()
    {
        var steps = new TestSteps();

        // Load test creature file - picker requires creature data
        var testCreature = TestPaths.GetTestModuleFile("parleypirate.utc");

        steps.Run("Launch with creature and navigate", () =>
        {
            StartApplication($"--file \"{testCreature}\"");
            WaitForTitleContains("parleypirate", DefaultTimeout);
            return NavigateToClassesPanel();
        });

        steps.Run("Click Package Picker button", () =>
        {
            var packageButton = FindElement("PackagePickerButton");
            if (packageButton == null) return false;

            var button = packageButton.AsButton();
            if (button.Patterns.Invoke.IsSupported)
            {
                button.Patterns.Invoke.Pattern.Invoke();
            }
            else
            {
                button.Click();
            }
            Thread.Sleep(500);
            return true;
        });

        steps.Run("Package picker window opens", () =>
        {
            var pickerWindow = FindPopupByTitle("Select Package", maxRetries: 10);
            if (pickerWindow == null) return false;

            var packagesListBox = pickerWindow.FindFirstDescendant(cf => cf.ByAutomationId("PackagesListBox"));
            return packagesListBox != null;
        });

        steps.Run("Close picker with Cancel", () =>
        {
            var pickerWindow = FindPopupByTitle("Select Package", maxRetries: 5);
            if (pickerWindow == null) return false;

            var cancelButton = pickerWindow.FindFirstDescendant(cf => cf.ByAutomationId("CancelButton"));
            if (cancelButton == null) return false;

            cancelButton.AsButton().Click();
            Thread.Sleep(300);
            return true;
        });

        steps.AssertAllPassed();
    }
}

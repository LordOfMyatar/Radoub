using FlaUI.Core.Definitions;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Parley;

/// <summary>
/// Tests for browser popup windows (Sound, Script, Creature).
/// Previously blocked by FlaUI tab navigation - now resolved with AutomationIds.
/// Issue #441.
/// </summary>
[Collection("ParleySequential")]
public class BrowserWindowTests : ParleyTestBase
{
    private const string TestFileName = "test1.dlg";

    /// <summary>
    /// Test that clicking Browse sound button opens the Sound Browser window.
    /// Uses test HAK with minimal WAV files (test1.hak in TestData).
    /// Previously skipped - enabled by #722 with test data infrastructure.
    /// </summary>
    [Fact]
    [Trait("Category", "Browser")]
    public void BrowseSoundButton_OpensSoundBrowserWindow()
    {
        var steps = new TestSteps();
        var testFile = TestPaths.GetTestFile(TestFileName);

        steps.Run("Launch Parley with test file", () =>
        {
            StartApplication($"\"{testFile}\"");
            return WaitForTitleContains(TestFileName, FileOperationTimeout);
        });

        steps.Run("Select first tree node", () =>
        {
            var tree = FindElement("DialogTreeView");
            var firstItem = tree?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(200);
            return firstItem != null;
        });

        steps.Run("Navigate to Node tab", () =>
        {
            var tab = FindTabByName("Node");
            tab?.Click();
            Thread.Sleep(200);
            return tab != null;
        });

        steps.Run("Find BrowseSoundButton", () =>
            FindElement("BrowseSoundButton") != null);

        steps.Run("Click BrowseSoundButton", () =>
        {
            var button = FindElement("BrowseSoundButton");
            button?.Click();
            Thread.Sleep(500);
            return true;
        });

        steps.Run("Sound Browser window opens", () =>
        {
            // Try both title search and AutomationId search
            // Avalonia popups may not appear in GetAllTopLevelWindows
            for (int i = 0; i < 15; i++)
            {
                var popup = FindPopupByTitle("Sound Browser", maxRetries: 1);
                if (popup != null) return true;

                // Also try searching desktop for the window by AutomationId
                var desktop = Automation?.GetDesktop();
                var browserWindow = desktop?.FindFirstDescendant(cf => cf.ByAutomationId("SoundBrowserWindow"));
                if (browserWindow != null) return true;

                Thread.Sleep(300);
            }
            return false;
        });

        steps.Run("Sound Browser has list box", () =>
        {
            // Search desktop for the browser window
            var desktop = Automation?.GetDesktop();
            var browserWindow = desktop?.FindFirstDescendant(cf => cf.ByAutomationId("SoundBrowserWindow"));
            if (browserWindow == null)
            {
                browserWindow = FindPopupByTitle("Sound Browser");
            }
            var listBox = browserWindow?.FindFirstDescendant(cf => cf.ByAutomationId("SoundListBox"));
            return listBox != null;
        });

        steps.Run("Close Sound Browser", () =>
        {
            // Try to find and close via popup helper first
            var popup = FindPopupByTitle("Sound Browser", maxRetries: 1);
            if (popup != null)
            {
                popup.Close();
                Thread.Sleep(300);
                return true;
            }

            // Fallback: find by AutomationId and send close via pattern
            var desktop = Automation?.GetDesktop();
            var browserWindow = desktop?.FindFirstDescendant(cf => cf.ByAutomationId("SoundBrowserWindow"));
            if (browserWindow != null)
            {
                var windowPattern = browserWindow.Patterns.Window.PatternOrDefault;
                windowPattern?.Close();
                Thread.Sleep(300);
            }
            return true;
        });

        steps.AssertAllPassed();
    }

    /// <summary>
    /// Test that clicking Browse script button opens the Script Browser window.
    /// </summary>
    [Fact]
    [Trait("Category", "Browser")]
    public void BrowseScriptButton_OpensScriptBrowserWindow()
    {
        var steps = new TestSteps();
        var testFile = TestPaths.GetTestFile(TestFileName);

        steps.Run("Launch Parley with test file", () =>
        {
            StartApplication($"\"{testFile}\"");
            return WaitForTitleContains(TestFileName, FileOperationTimeout);
        });

        steps.Run("Select first tree node", () =>
        {
            var tree = FindElement("DialogTreeView");
            var firstItem = tree?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(200);
            return firstItem != null;
        });

        steps.Run("Navigate to Scripts tab", () =>
        {
            var tab = FindTabByName("Scripts");
            tab?.Click();
            Thread.Sleep(200);
            return tab != null;
        });

        steps.Run("Find BrowseConditionalScriptButton", () =>
            FindElement("BrowseConditionalScriptButton") != null);

        steps.Run("Click BrowseConditionalScriptButton", () =>
        {
            var button = FindElement("BrowseConditionalScriptButton");
            EnsureFocused(); // Ensure main window focus before clicking
            button?.Click();
            Thread.Sleep(800); // Wait for Script Browser to open
            return true;
        });

        steps.Run("Script Browser window opens", () =>
        {
            var popup = FindPopupByTitle("Script Browser");
            return popup != null;
        });

        steps.Run("Script Browser has list box", () =>
        {
            var popup = FindPopupByTitle("Script Browser");
            var listBox = popup?.FindFirstDescendant(cf => cf.ByAutomationId("ScriptListBox"));
            return listBox != null;
        });

        steps.Run("Close Script Browser", () =>
        {
            var popup = FindPopupByTitle("Script Browser");
            popup?.Close();
            Thread.Sleep(300);
            return true;
        });

        steps.AssertAllPassed();
    }

    /// <summary>
    /// Test that clicking Browse creature button opens the Creature Picker window.
    /// Uses parleypirate.utc in TestFiles directory.
    /// </summary>
    [Fact]
    [Trait("Category", "Browser")]
    public void BrowseCreatureButton_OpensCreaturePickerWindow()
    {
        var steps = new TestSteps();
        var testFile = TestPaths.GetTestFile(TestFileName);

        steps.Run("Launch Parley with test file", () =>
        {
            StartApplication($"\"{testFile}\"");
            return WaitForTitleContains(TestFileName, FileOperationTimeout);
        });

        steps.Run("Select first tree node", () =>
        {
            var tree = FindElement("DialogTreeView");
            var firstItem = tree?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(200);
            return firstItem != null;
        });

        steps.Run("Find BrowseCreatureButton", () =>
            FindElement("BrowseCreatureButton") != null);

        steps.Run("Click BrowseCreatureButton", () =>
        {
            var button = FindElement("BrowseCreatureButton");
            EnsureFocused(); // Ensure main window focus before clicking
            button?.Click();
            Thread.Sleep(800); // Wait for Creature Picker to open
            return true;
        });

        steps.Run("Creature Picker window opens", () =>
        {
            // Try both title search and AutomationId search
            // Avalonia popups may not appear in GetAllTopLevelWindows
            for (int i = 0; i < 15; i++)
            {
                var popup = FindPopupByTitle("Select Creature", maxRetries: 1);
                if (popup != null) return true;

                // Also try searching desktop for the window by AutomationId
                var desktop = Automation?.GetDesktop();
                var pickerWindow = desktop?.FindFirstDescendant(cf => cf.ByAutomationId("CreaturePickerWindow"));
                if (pickerWindow != null) return true;

                Thread.Sleep(300);
            }
            return false;
        });

        steps.Run("Creature Picker has list box", () =>
        {
            // Search desktop for the picker window
            var desktop = Automation?.GetDesktop();
            var pickerWindow = desktop?.FindFirstDescendant(cf => cf.ByAutomationId("CreaturePickerWindow"));
            if (pickerWindow == null)
            {
                pickerWindow = FindPopupByTitle("Select Creature");
            }
            var listBox = pickerWindow?.FindFirstDescendant(cf => cf.ByAutomationId("CreaturesListBox"));
            return listBox != null;
        });

        steps.Run("Close Creature Picker", () =>
        {
            // Try to find and close via popup helper first
            var popup = FindPopupByTitle("Select Creature", maxRetries: 1);
            if (popup != null)
            {
                popup.Close();
                Thread.Sleep(300);
                return true;
            }

            // Fallback: find by AutomationId and send close via pattern
            var desktop = Automation?.GetDesktop();
            var pickerWindow = desktop?.FindFirstDescendant(cf => cf.ByAutomationId("CreaturePickerWindow"));
            if (pickerWindow != null)
            {
                var windowPattern = pickerWindow.Patterns.Window.PatternOrDefault;
                windowPattern?.Close();
                Thread.Sleep(300);
            }
            return true;
        });

        steps.AssertAllPassed();
    }

    /// <summary>
    /// Test that Creature Picker loads creatures from TestModule directory.
    /// Uses eay.dlg from TestModule which has bandit002.utc and earyldor.utc in same folder.
    /// This tests the tag browsing feature with the new test data infrastructure (#722).
    /// </summary>
    [Fact]
    [Trait("Category", "Browser")]
    public void CreaturePicker_LoadsCreaturesFromTestModule()
    {
        var steps = new TestSteps();
        // Use dialog from TestModule so creatures are loaded from same directory
        var testFile = TestPaths.GetTestModuleFile("eay.dlg");

        steps.Run("Launch Parley with TestModule dialog", () =>
        {
            StartApplication($"\"{testFile}\"");
            return WaitForTitleContains("eay.dlg", FileOperationTimeout);
        });

        steps.Run("Select a dialog entry node (not ROOT)", () =>
        {
            var tree = FindElement("DialogTreeView");
            if (tree == null) return false;

            // Get all tree items - first one is ROOT, we need a child entry
            var allItems = tree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
            if (allItems.Length < 2) return false; // Need at least ROOT + one entry

            // Select second item (first actual entry, not ROOT)
            allItems[1].Click();
            Thread.Sleep(300);
            return true;
        });

        steps.Run("Click BrowseCreatureButton", () =>
        {
            var button = FindElement("BrowseCreatureButton");
            EnsureFocused();
            button?.Click();
            Thread.Sleep(1500); // Wait for Creature Picker to load creatures
            return true;
        });

        steps.Run("Creature Picker window opens", () =>
        {
            for (int i = 0; i < 15; i++)
            {
                var popup = FindPopupByTitle("Select Creature", maxRetries: 1);
                if (popup != null) return true;

                var desktop = Automation?.GetDesktop();
                var pickerWindow = desktop?.FindFirstDescendant(cf => cf.ByAutomationId("CreaturePickerWindow"));
                if (pickerWindow != null) return true;

                Thread.Sleep(300);
            }
            return false;
        });

        steps.Run("Creature list has items from TestModule", () =>
        {
            // Find the picker window
            var desktop = Automation?.GetDesktop();
            var pickerWindow = desktop?.FindFirstDescendant(cf => cf.ByAutomationId("CreaturePickerWindow"));
            if (pickerWindow == null)
            {
                pickerWindow = FindPopupByTitle("Select Creature");
            }
            if (pickerWindow == null) return false;

            // Find the creatures list box
            var listBox = pickerWindow.FindFirstDescendant(cf => cf.ByAutomationId("CreaturesListBox"));
            if (listBox == null) return false;

            // Check that list has items (TestModule has bandit002.utc and earyldor.utc)
            var items = listBox.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
            return items.Length >= 2; // At least 2 creatures from TestModule
        });

        steps.Run("Close Creature Picker", () =>
        {
            var popup = FindPopupByTitle("Select Creature", maxRetries: 1);
            if (popup != null)
            {
                popup.Close();
                Thread.Sleep(300);
                return true;
            }

            var desktop = Automation?.GetDesktop();
            var pickerWindow = desktop?.FindFirstDescendant(cf => cf.ByAutomationId("CreaturePickerWindow"));
            if (pickerWindow != null)
            {
                var windowPattern = pickerWindow.Patterns.Window.PatternOrDefault;
                windowPattern?.Close();
                Thread.Sleep(300);
            }
            return true;
        });

        steps.AssertAllPassed();
    }
}

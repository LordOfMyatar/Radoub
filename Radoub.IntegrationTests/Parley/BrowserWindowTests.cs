using FlaUI.Core.AutomationElements;
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
    /// Helper to find a tab item by name with retries.
    /// </summary>
    private AutomationElement? FindTabByName(string name, int maxRetries = 5)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var tabs = MainWindow?.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
            if (tabs != null)
            {
                foreach (var tab in tabs)
                {
                    if (tab.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                        return tab;
                }
            }
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }
        return null;
    }

    /// <summary>
    /// Helper to find a popup window by automation ID.
    /// </summary>
    private Window? FindPopupWindow(string automationId, int maxRetries = 10)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var windows = App?.GetAllTopLevelWindows(Automation!);
            if (windows != null)
            {
                foreach (var window in windows)
                {
                    // Check automation ID
                    var props = window.Properties;
                    if (props.AutomationId.TryGetValue(out var id) && id == automationId)
                        return window;
                }
            }
            Thread.Sleep(300);
        }
        return null;
    }

    /// <summary>
    /// Helper to find a popup window by title containing text.
    /// </summary>
    private Window? FindPopupByTitle(string titleContains, int maxRetries = 10)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var windows = App?.GetAllTopLevelWindows(Automation!);
            if (windows != null)
            {
                foreach (var window in windows)
                {
                    if (window.Title?.Contains(titleContains, StringComparison.OrdinalIgnoreCase) == true)
                        return window;
                }
            }
            Thread.Sleep(300);
        }
        return null;
    }

    /// <summary>
    /// Test that clicking Browse sound button opens the Sound Browser window.
    /// Skipped: Requires module context with sound files. See #701.
    /// </summary>
    [Fact(Skip = "Requires module context with sound files (#701)")]
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
            var popup = FindPopupByTitle("Sound Browser");
            return popup != null;
        });

        steps.Run("Sound Browser has list box", () =>
        {
            var popup = FindPopupByTitle("Sound Browser");
            var listBox = popup?.FindFirstDescendant(cf => cf.ByAutomationId("SoundListBox"));
            return listBox != null;
        });

        steps.Run("Close Sound Browser", () =>
        {
            var popup = FindPopupByTitle("Sound Browser");
            popup?.Close();
            Thread.Sleep(300);
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
            button?.Click();
            Thread.Sleep(500);
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
    /// Skipped: Window doesn't open in test context - needs investigation. See #700.
    /// </summary>
    [Fact(Skip = "Window doesn't open in test context - needs investigation (#700)")]
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
            button?.Click();
            Thread.Sleep(500);
            return true;
        });

        steps.Run("Creature Picker window opens", () =>
        {
            var popup = FindPopupByTitle("Select Creature");
            return popup != null;
        });

        steps.Run("Creature Picker has list box", () =>
        {
            var popup = FindPopupByTitle("Select Creature");
            var listBox = popup?.FindFirstDescendant(cf => cf.ByAutomationId("CreaturesListBox"));
            return listBox != null;
        });

        steps.Run("Close Creature Picker", () =>
        {
            var popup = FindPopupByTitle("Select Creature");
            popup?.Close();
            Thread.Sleep(300);
            return true;
        });

        steps.AssertAllPassed();
    }
}

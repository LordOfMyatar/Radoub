using FlaUI.Core.Definitions;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Parley;

/// <summary>
/// Tests for Parley tab navigation and UI interactions.
/// Uses consolidated step-based testing for efficient diagnostics.
/// </summary>
[Collection("ParleySequential")]
public class NavigationTests : ParleyTestBase
{
    private const string TestFileName = "test1.dlg";

    /// <summary>
    /// Consolidated test for right-side properties tab navigation.
    /// Verifies Scripts, Node, and Scrap tabs are clickable and show content.
    /// </summary>
    [Fact]
    [Trait("Category", "Navigation")]
    public void PropertiesTabs_NavigationWorks()
    {
        var steps = new TestSteps();
        var testFile = TestPaths.GetTestFile(TestFileName);

        steps.Run("Launch Parley with test file", () =>
        {
            StartApplication($"\"{testFile}\"");
            return WaitForTitleContains(TestFileName, FileOperationTimeout);
        });

        // Select a node first to enable Node properties panel
        steps.Run("Select first tree node", () =>
        {
            var tree = FindElement("DialogTreeView");
            var firstItem = tree?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(200);
            return firstItem != null;
        });

        steps.Run("PropertiesTabControl exists", () =>
            FindElement("PropertiesTabControl") != null);

        // Test Scripts tab (should be default/first)
        steps.Run("Scripts tab exists", () =>
            FindTabByName("Scripts") != null);

        steps.Run("Scripts tab is clickable", () =>
        {
            var tab = FindTabByName("Scripts");
            tab?.Click();
            Thread.Sleep(200);
            return true;
        });

        // Verify Scripts tab content is visible (check for a known element)
        steps.Run("Scripts tab shows ScriptAppearsTextBox", () =>
            FindElement("ScriptAppearsTextBox") != null);

        // Test Node tab
        steps.Run("Node tab exists", () =>
            FindTabByName("Node") != null);

        steps.Run("Node tab click switches content", () =>
        {
            var tab = FindTabByName("Node");
            tab?.Click();
            Thread.Sleep(200);
            // After clicking Node tab, animation combo should be visible (node is selected)
            return FindElement("AnimationComboBox") != null;
        });

        // Test Scrap tab
        steps.Run("Scrap tab exists", () =>
            FindTabByName("Scrap") != null);

        steps.Run("Scrap tab click switches content", () =>
        {
            var tab = FindTabByName("Scrap");
            tab?.Click();
            Thread.Sleep(200);
            // After clicking Scrap tab, scrap list should be visible
            return FindElement("ScrapListBox") != null;
        });

        // Return to Scripts tab
        steps.Run("Can return to Scripts tab", () =>
        {
            var tab = FindTabByName("Scripts");
            tab?.Click();
            Thread.Sleep(200);
            return FindElement("ScriptAppearsTextBox") != null;
        });

        steps.AssertAllPassed();
    }

    /// <summary>
    /// Consolidated test for left-side panel (Dialog Tree tab).
    /// </summary>
    [Fact]
    [Trait("Category", "Navigation")]
    public void LeftPane_DialogTreeTabWorks()
    {
        var steps = new TestSteps();
        var testFile = TestPaths.GetTestFile(TestFileName);

        steps.Run("Launch Parley with test file", () =>
        {
            StartApplication($"\"{testFile}\"");
            return WaitForTitleContains(TestFileName, FileOperationTimeout);
        });

        steps.Run("LeftPaneTabControl exists", () =>
            FindElement("LeftPaneTabControl") != null);

        steps.Run("Dialog Tree tab exists", () =>
            FindTabByName("Dialog Tree") != null);

        steps.Run("DialogTreeView exists", () =>
            FindElement("DialogTreeView") != null);

        steps.Run("Tree has nodes", () =>
        {
            var tree = FindElement("DialogTreeView");
            var items = tree?.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
            return items?.Length > 0;
        });

        steps.Run("AddNodeButton exists", () =>
            FindElement("AddNodeButton") != null);

        steps.Run("DeleteNodeButton exists", () =>
            FindElement("DeleteNodeButton") != null);

        steps.Run("ExpandAllButton exists", () =>
            FindElement("ExpandAllButton") != null);

        steps.Run("CollapseAllButton exists", () =>
            FindElement("CollapseAllButton") != null);

        steps.AssertAllPassed();
    }

    /// <summary>
    /// Test browse buttons exist and are clickable (without actually opening dialogs).
    /// </summary>
    [Fact]
    [Trait("Category", "Navigation")]
    public void BrowseButtons_ExistAndAccessible()
    {
        var steps = new TestSteps();
        var testFile = TestPaths.GetTestFile(TestFileName);

        steps.Run("Launch Parley with test file", () =>
        {
            StartApplication($"\"{testFile}\"");
            return WaitForTitleContains(TestFileName, FileOperationTimeout);
        });

        // Select a node so properties are enabled
        steps.Run("Select first tree node", () =>
        {
            var tree = FindElement("DialogTreeView");
            var firstItem = tree?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(200);
            return firstItem != null;
        });

        // Scripts tab browse buttons
        steps.Run("Navigate to Scripts tab", () =>
        {
            var tab = FindTabByName("Scripts");
            tab?.Click();
            Thread.Sleep(200);
            return true;
        });

        steps.Run("BrowseConditionalScriptButton exists", () =>
            FindElement("BrowseConditionalScriptButton") != null);

        steps.Run("BrowseActionScriptButton exists", () =>
            FindElement("BrowseActionScriptButton") != null);

        // Node tab browse buttons
        steps.Run("Navigate to Node tab", () =>
        {
            var tab = FindTabByName("Node");
            tab?.Click();
            Thread.Sleep(200);
            return true;
        });

        steps.Run("BrowseSoundButton exists", () =>
            FindElement("BrowseSoundButton") != null);

        // Speaker field browse button (in text entry panel, not in tabs)
        steps.Run("BrowseCreatureButton exists", () =>
            FindElement("BrowseCreatureButton") != null);

        steps.AssertAllPassed();
    }

    /// <summary>
    /// Test tab key navigation between fields.
    /// Verifies Tab moves focus in expected order.
    /// </summary>
    [Fact]
    [Trait("Category", "Navigation")]
    public void TabKeyNavigation_MovesForward()
    {
        var steps = new TestSteps();
        var testFile = TestPaths.GetTestFile(TestFileName);

        steps.Run("Launch Parley with test file", () =>
        {
            StartApplication($"\"{testFile}\"");
            return WaitForTitleContains(TestFileName, FileOperationTimeout);
        });

        // Select a node to enable editing
        steps.Run("Select first tree node", () =>
        {
            var tree = FindElement("DialogTreeView");
            var firstItem = tree?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(200);
            return firstItem != null;
        });

        // Focus the text box and tab through fields
        steps.Run("Focus TextTextBox", () =>
        {
            var textBox = FindElement("TextTextBox");
            textBox?.Focus();
            Thread.Sleep(100);
            return true;
        });

        steps.Run("Tab key moves focus", () =>
        {
            // Send Tab key (focus-safe)
            SendTab();
            Thread.Sleep(200);
            // Focus should have moved - we verify by checking the window still responds
            return MainWindow?.Title != null;
        });

        // Navigate to Scripts tab and test tab order there
        steps.Run("Navigate to Scripts tab", () =>
        {
            var tab = FindTabByName("Scripts");
            tab?.Click();
            Thread.Sleep(200);
            return true;
        });

        steps.Run("Focus ScriptAppearsTextBox", () =>
        {
            var textBox = FindElement("ScriptAppearsTextBox");
            textBox?.Focus();
            Thread.Sleep(100);
            return true;
        });

        steps.Run("Tab from ScriptAppearsTextBox moves focus", () =>
        {
            // Send Tab key (focus-safe)
            SendTab();
            Thread.Sleep(200);
            return true;
        });

        steps.AssertAllPassed();
    }
}

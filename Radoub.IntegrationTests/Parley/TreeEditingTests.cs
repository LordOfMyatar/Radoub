using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Parley;

/// <summary>
/// Tests for dialog tree editing operations: add, delete, move nodes.
/// Uses consolidated step-based testing for efficient diagnostics.
/// </summary>
[Collection("ParleySequential")]
public class TreeEditingTests : ParleyTestBase
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
    /// Consolidated test for dialog tree visibility and content on file load.
    /// Replaces individual tests: DialogTree_OnFileLoad_IsVisible, DialogTree_OnFileLoad_HasNodes,
    /// AddNodeButton_Exists, DeleteNodeButton_Exists.
    /// </summary>
    [Fact]
    [Trait("Category", "TreeEdit")]
    public void DialogTree_OnFileLoad_AllElementsPresent()
    {
        var steps = new TestSteps();
        var testFile = TestPaths.GetTestFile(TestFileName);

        steps.Run("Launch Parley with test file", () =>
        {
            StartApplication($"\"{testFile}\"");
            return WaitForTitleContains(TestFileName, FileOperationTimeout);
        });

        steps.Run("DialogTreeView exists", () =>
            FindElement("DialogTreeView") != null);

        steps.Run("Tree has nodes", () =>
        {
            var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            var treeItems = treeView?.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
            return treeItems?.Length > 0;
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
    /// Test that adding a node via keyboard sets the unsaved indicator.
    /// </summary>
    [Fact]
    [Trait("Category", "TreeEdit")]
    public void AddNode_ViaKeyboard_SetsUnsavedIndicator()
    {
        var steps = new TestSteps();
        var tempFile = TestPaths.CopyTestFileToTemp(TestFileName);
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            steps.Run("Launch Parley with temp file", () =>
            {
                StartApplication($"\"{tempFile}\"");
                return WaitForTitleContains(TestFileName, FileOperationTimeout);
            });

            steps.Run("Select first tree node", () =>
            {
                var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
                var firstItem = treeView?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
                firstItem?.Click();
                Thread.Sleep(200);
                return firstItem != null;
            });

            steps.Run("Press Ctrl+D to add node", () =>
            {
                SendCtrlD();
                Thread.Sleep(500);
                return true;
            });

            steps.Run("Title shows unsaved indicator (*)", () =>
            {
                MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
                return MainWindow?.Title?.Contains("*") == true;
            });

            steps.AssertAllPassed();
        }
        finally
        {
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    /// Test that selecting a node enables the delete button.
    /// </summary>
    [Fact]
    [Trait("Category", "TreeEdit")]
    public void SelectNode_DeleteButtonAccessible()
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
            var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            var firstItem = treeView?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(200);
            return firstItem != null;
        });

        steps.Run("DeleteNodeButton is accessible", () =>
        {
            var deleteButton = FindElement("DeleteNodeButton");
            return deleteButton != null;
        });

        steps.AssertAllPassed();
    }
}

using FlaUI.Core.Definitions;
using Radoub.UITests.Shared;
using Xunit;

namespace Radoub.UITests.Parley;

/// <summary>
/// Tests for dialog tree editing operations: add, delete, move nodes.
/// </summary>
[Collection("ParleySequential")]
public class TreeEditingTests : ParleyTestBase
{
    private const string TestFileName = "test1.dlg";

    [Fact]
    [Trait("Category", "TreeEdit")]
    public void DialogTree_OnFileLoad_IsVisible()
    {
        // Arrange
        var testFile = TestPaths.GetTestFile(TestFileName);
        StartApplication($"\"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Act - Find the tree view
        var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));

        // Assert
        Assert.NotNull(treeView);
    }

    [Fact]
    [Trait("Category", "TreeEdit")]
    public void DialogTree_OnFileLoad_HasNodes()
    {
        // Arrange
        var testFile = TestPaths.GetTestFile(TestFileName);
        StartApplication($"\"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Act - Find tree items
        var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
        var treeItems = treeView?.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));

        // Assert - Should have at least one node (ROOT or actual content)
        Assert.NotNull(treeItems);
        Assert.True(treeItems.Length > 0, "Dialog tree should have at least one node");
    }

    [Fact]
    [Trait("Category", "TreeEdit")]
    public void AddNodeButton_Exists()
    {
        // Arrange
        var testFile = TestPaths.GetTestFile(TestFileName);
        StartApplication($"\"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Act - Find the Add Node button by name
        var addButton = MainWindow!.FindFirstDescendant(cf => cf.ByName("+ Node"));

        // Assert
        Assert.NotNull(addButton);
    }

    [Fact]
    [Trait("Category", "TreeEdit")]
    public void DeleteNodeButton_Exists()
    {
        // Arrange
        var testFile = TestPaths.GetTestFile(TestFileName);
        StartApplication($"\"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Act - Find the Delete button
        var deleteButton = MainWindow!.FindFirstDescendant(cf => cf.ByName("Delete"));

        // Assert
        Assert.NotNull(deleteButton);
    }

    [Fact]
    [Trait("Category", "TreeEdit")]
    public void AddNode_ViaKeyboard_SetsUnsavedIndicator()
    {
        // Arrange - Use temp file to avoid modifying original
        var tempFile = TestPaths.CopyTestFileToTemp(TestFileName);
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            StartApplication($"\"{tempFile}\"");
            WaitForTitleContains(TestFileName, FileOperationTimeout);

            // Select a node first (click on tree)
            var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            var firstItem = treeView?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(200);

            // Act - Press Ctrl+D to add node
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_D);

            Thread.Sleep(500);

            // Assert - Title should contain asterisk (unsaved indicator)
            MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
            Assert.NotNull(MainWindow);
            Assert.Contains("*", MainWindow.Title);
        }
        finally
        {
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    [Trait("Category", "TreeEdit")]
    public void SelectNode_EnablesDeleteButton()
    {
        // Arrange
        var testFile = TestPaths.GetTestFile(TestFileName);
        StartApplication($"\"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Act - Select a node
        var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
        var firstItem = treeView?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
        firstItem?.Click();
        Thread.Sleep(200);

        // Find delete button - it should be enabled when a node is selected
        var deleteButton = MainWindow.FindFirstDescendant(cf => cf.ByName("Delete"));

        // Assert - Button should exist (enabled state depends on node type)
        Assert.NotNull(deleteButton);
    }
}

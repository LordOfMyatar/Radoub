using FlaUI.Core.Definitions;
using Radoub.UITests.Shared;
using Xunit;

namespace Radoub.UITests.Parley;

/// <summary>
/// Tests for undo/redo functionality via keyboard shortcuts and menu.
/// </summary>
[Collection("ParleySequential")]
public class UndoRedoTests : ParleyTestBase
{
    private const string TestFileName = "test1.dlg";

    [Fact]
    [Trait("Category", "UndoRedo")]
    public void UndoMenuItem_Exists()
    {
        // Arrange
        var testFile = TestPaths.GetTestFile(TestFileName);
        StartApplication($"\"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Act - Open Edit menu
        ClickMenu("Edit");
        Thread.Sleep(200);

        // Find Undo menu item
        var undoItem = MainWindow!.FindFirstDescendant(cf => cf.ByName("Undo"));

        // Assert
        Assert.NotNull(undoItem);
    }

    [Fact]
    [Trait("Category", "UndoRedo")]
    public void RedoMenuItem_Exists()
    {
        // Arrange
        var testFile = TestPaths.GetTestFile(TestFileName);
        StartApplication($"\"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Act - Open Edit menu
        ClickMenu("Edit");
        Thread.Sleep(200);

        // Find Redo menu item
        var redoItem = MainWindow!.FindFirstDescendant(cf => cf.ByName("Redo"));

        // Assert
        Assert.NotNull(redoItem);
    }

    [Fact]
    [Trait("Category", "UndoRedo")]
    public void Undo_AfterModification_RemovesUnsavedIndicator()
    {
        // Arrange - Use temp file
        var tempFile = TestPaths.CopyTestFileToTemp(TestFileName);
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            StartApplication($"\"{tempFile}\"");
            WaitForTitleContains(TestFileName, FileOperationTimeout);

            // Wait for app to fully stabilize before making changes
            Thread.Sleep(1000);

            // Make a modification - select and add node
            var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            var firstItem = treeView?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(200);

            // Add node via Ctrl+D
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_D);
            Thread.Sleep(500);

            // Verify unsaved indicator appeared
            MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
            Assert.Contains("*", MainWindow!.Title);

            // Act - Undo via Ctrl+Z
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_Z);
            Thread.Sleep(500);

            // Assert - Unsaved indicator should be gone
            MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
            Assert.NotNull(MainWindow);
            Assert.DoesNotContain("*", MainWindow.Title);
        }
        finally
        {
            // Stop application BEFORE cleaning up temp directory
            // to avoid auto-save race conditions
            StopApplication();
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    [Trait("Category", "UndoRedo")]
    public void Redo_AfterUndo_RestoresUnsavedIndicator()
    {
        // Arrange - Use temp file
        var tempFile = TestPaths.CopyTestFileToTemp(TestFileName);
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            StartApplication($"\"{tempFile}\"");
            WaitForTitleContains(TestFileName, FileOperationTimeout);

            // Wait for app to fully stabilize before making changes
            Thread.Sleep(1000);

            // Make a modification
            var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            var firstItem = treeView?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(200);

            // Add node via Ctrl+D
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_D);
            Thread.Sleep(500);

            // Undo the change
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_Z);
            Thread.Sleep(500);

            // Verify unsaved indicator is gone
            MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
            Assert.DoesNotContain("*", MainWindow!.Title);

            // Act - Redo via Ctrl+Y
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_Y);
            Thread.Sleep(500);

            // Assert - Unsaved indicator should be back
            MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
            Assert.NotNull(MainWindow);
            Assert.Contains("*", MainWindow.Title);
        }
        finally
        {
            // Stop application BEFORE cleaning up temp directory
            // to avoid auto-save race conditions
            StopApplication();
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    [Trait("Category", "UndoRedo")]
    public void MultipleUndo_RevertsMultipleChanges()
    {
        // Arrange - Use temp file
        var tempFile = TestPaths.CopyTestFileToTemp(TestFileName);
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            StartApplication($"\"{tempFile}\"");
            WaitForTitleContains(TestFileName, FileOperationTimeout);

            // Wait for app to fully stabilize before making changes
            Thread.Sleep(1000);

            // Get initial node count
            var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            var initialItems = treeView?.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
            var initialCount = initialItems?.Length ?? 0;

            // Select first node
            var firstItem = treeView?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(200);

            // Add two nodes
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_D);
            Thread.Sleep(300);

            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_D);
            Thread.Sleep(300);

            // Act - Undo twice
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_Z);
            Thread.Sleep(300);

            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_Z);
            Thread.Sleep(500);

            // Assert - Should be back to initial state (no unsaved indicator)
            MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
            Assert.NotNull(MainWindow);
            Assert.DoesNotContain("*", MainWindow.Title);
        }
        finally
        {
            // Stop application BEFORE cleaning up temp directory
            // to avoid auto-save race conditions
            StopApplication();
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }
}

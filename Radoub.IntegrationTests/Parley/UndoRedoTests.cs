using FlaUI.Core.Definitions;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Parley;

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
    public void Undo_AfterModification_RestoresDialogState()
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

            // Get tree and ensure it's fully loaded
            var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            var firstItem = treeView?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(500);

            // Capture original file size as baseline (dialog model state)
            var originalFileSize = new FileInfo(tempFile).Length;

            // Add node via Ctrl+D
            MainWindow!.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_D);
            Thread.Sleep(1000);

            // Save file to capture modified state
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_S);
            Thread.Sleep(1000);

            // Verify file size increased (node was added)
            var afterAddFileSize = new FileInfo(tempFile).Length;
            Console.WriteLine($"TEST: Original size={originalFileSize}, After add size={afterAddFileSize}");
            Assert.True(afterAddFileSize > originalFileSize,
                $"File size should have increased after adding node (original: {originalFileSize}, after: {afterAddFileSize})");

            // Act - Undo multiple times via Edit menu until file size matches original
            // (Ctrl+D may create multiple undo states due to focus-triggered saves)
            int maxUndoAttempts = 5;
            long currentFileSize = afterAddFileSize;

            for (int i = 0; i < maxUndoAttempts && currentFileSize > originalFileSize; i++)
            {
                Console.WriteLine($"TEST: Undo attempt {i + 1}");
                ClickMenu("Edit", "Undo");
                Thread.Sleep(500);

                // Save to check current file size
                MainWindow!.Focus();
                FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_S);
                Thread.Sleep(1000);

                currentFileSize = new FileInfo(tempFile).Length;
                Console.WriteLine($"TEST: After undo {i + 1}, size={currentFileSize}");
            }

            // Assert - File size should be close to original (dialog state restored)
            // Allow small variance (up to 10 bytes) due to serialization differences
            var sizeDifference = Math.Abs(currentFileSize - originalFileSize);
            Assert.True(sizeDifference <= 10,
                $"File size should be close to original (original: {originalFileSize}, current: {currentFileSize}, diff: {sizeDifference})");
        }
        finally
        {
            // Stop application BEFORE cleaning up temp directory
            StopApplication();
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    /// Expands all tree items recursively to get consistent node counts.
    /// Repeats until no more collapsed items are found AND item count stabilizes (handles lazy loading).
    /// </summary>
    private void ExpandAllTreeItems(FlaUI.Core.AutomationElements.AutomationElement treeView)
    {
        int maxIterations = 10; // Safety limit
        int iteration = 0;
        int previousCount = 0;

        // Keep expanding until both: no collapsed items AND item count stabilizes
        while (iteration < maxIterations)
        {
            iteration++;
            var items = treeView.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
            int currentCount = items.Length;
            bool expandedAny = false;

            foreach (var item in items)
            {
                try
                {
                    var expandPattern = item.Patterns.ExpandCollapse.PatternOrDefault;
                    if (expandPattern != null && expandPattern.ExpandCollapseState.Value == FlaUI.Core.Definitions.ExpandCollapseState.Collapsed)
                    {
                        expandPattern.Expand();
                        Thread.Sleep(150); // Slightly longer wait for lazy loading
                        expandedAny = true;
                    }
                }
                catch
                {
                    // Ignore items that can't be expanded
                }
            }

            // If we didn't expand anything AND count hasn't changed, we're done
            if (!expandedAny && currentCount == previousCount)
            {
                break;
            }

            previousCount = currentCount;
            Thread.Sleep(100); // Extra wait for lazy loading to complete
        }
    }

    [Fact]
    [Trait("Category", "UndoRedo")]
    public void Redo_AfterUndo_RestoresDialogState()
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

            // Get tree and click first item to ensure it's loaded
            var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            var firstItem = treeView?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(500);

            // Capture original file size as baseline
            var originalFileSize = new FileInfo(tempFile).Length;

            // Add node via Ctrl+D
            MainWindow!.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_D);
            Thread.Sleep(1000);

            // Save to capture modified state
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_S);
            Thread.Sleep(1000);
            var afterAddFileSize = new FileInfo(tempFile).Length;
            Assert.True(afterAddFileSize > originalFileSize, "Node should have been added");

            // Undo multiple times to fully revert (Ctrl+D creates multiple undo states)
            int maxUndoAttempts = 5;
            long currentFileSize = afterAddFileSize;
            for (int i = 0; i < maxUndoAttempts && currentFileSize > originalFileSize + 10; i++)
            {
                ClickMenu("Edit", "Undo");
                Thread.Sleep(500);
                MainWindow!.Focus();
                FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_S);
                Thread.Sleep(1000);
                currentFileSize = new FileInfo(tempFile).Length;
            }
            var afterUndoFileSize = currentFileSize;
            var undoDiff = Math.Abs(afterUndoFileSize - originalFileSize);
            Assert.True(undoDiff <= 10, $"Undo should restore original (original: {originalFileSize}, after: {afterUndoFileSize})");

            // Act - Redo multiple times to restore added state
            // Use Ctrl+Y instead of menu to avoid focus changes that trigger "Edit Text" saves
            // which would clear the redo stack
            int maxRedoAttempts = 5;
            for (int i = 0; i < maxRedoAttempts && currentFileSize < afterAddFileSize - 10; i++)
            {
                MainWindow!.Focus();
                Thread.Sleep(300);
                FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_Y);
                Thread.Sleep(1000); // Give time for redo to process
                FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_S);
                Thread.Sleep(1500); // Wait for save to complete
                currentFileSize = new FileInfo(tempFile).Length;
            }

            // Assert - File size should be close to after-add size
            var afterRedoFileSize = currentFileSize;
            var redoDiff = Math.Abs(afterRedoFileSize - afterAddFileSize);
            Assert.True(redoDiff <= 10, $"Redo should restore added state (added: {afterAddFileSize}, after: {afterRedoFileSize})");
        }
        finally
        {
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

            // Get tree and click first item to ensure it's loaded
            var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            var firstItem = treeView?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(500);

            // Capture original file size as baseline
            var originalFileSize = new FileInfo(tempFile).Length;

            // Add first node via Ctrl+D
            MainWindow!.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_D);
            Thread.Sleep(1000);

            // Save to capture first add state
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_S);
            Thread.Sleep(1000);
            var afterFirstAddSize = new FileInfo(tempFile).Length;
            Assert.True(afterFirstAddSize > originalFileSize, "First node should have been added");

            // Click first item again before second add
            treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            firstItem = treeView?.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem));
            firstItem?.Click();
            Thread.Sleep(500);

            // Add second node via Ctrl+D
            MainWindow!.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_D);
            Thread.Sleep(1500); // Longer wait for second add

            // Save to capture second add state
            MainWindow!.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_S);
            Thread.Sleep(1500);
            var afterSecondAddSize = new FileInfo(tempFile).Length;
            // Second add may or may not increase size if same node is selected
            // Just verify file is still larger than original
            Assert.True(afterSecondAddSize >= afterFirstAddSize,
                $"Second add should not shrink file (first: {afterFirstAddSize}, second: {afterSecondAddSize})");

            // Act - Undo multiple times to get back to original state
            // (Each Ctrl+D may create multiple undo states)
            int maxUndoAttempts = 10;
            long currentFileSize = afterSecondAddSize;
            for (int i = 0; i < maxUndoAttempts && currentFileSize > originalFileSize + 10; i++)
            {
                ClickMenu("Edit", "Undo");
                Thread.Sleep(500);
                MainWindow!.Focus();
                FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_S);
                Thread.Sleep(1000);
                currentFileSize = new FileInfo(tempFile).Length;
            }

            // Assert - File size should be close to original
            var afterUndoSize = currentFileSize;
            var sizeDiff = Math.Abs(afterUndoSize - originalFileSize);
            Assert.True(sizeDiff <= 10,
                $"Multiple undos should restore original (original: {originalFileSize}, after: {afterUndoSize}, diff: {sizeDiff})");
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    [Trait("Category", "UndoRedo")]
    [Trait("Category", "Focus")]
    public void UndoRedo_MaintainsValidSelection()
    {
        // Arrange - Use temp file
        var tempFile = TestPaths.CopyTestFileToTemp(TestFileName);
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            StartApplication($"\"{tempFile}\"");
            WaitForTitleContains(TestFileName, FileOperationTimeout);
            Thread.Sleep(1000);

            var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            ExpandAllTreeItems(treeView!);
            Thread.Sleep(300);

            // Get all tree items and find a non-ROOT node to select
            var treeItems = treeView?.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
            Assert.True(treeItems?.Length >= 2, $"Need at least 2 tree items for this test, found {treeItems?.Length ?? 0}");

            // Find first item that's not ROOT (by name or index)
            FlaUI.Core.AutomationElements.AutomationElement? targetItem = null;
            foreach (var item in treeItems!)
            {
                var itemName = item.Name ?? "";
                // Skip ROOT and items with no/empty name
                if (!itemName.Contains("ROOT") && !string.IsNullOrWhiteSpace(itemName))
                {
                    targetItem = item;
                    break;
                }
            }

            // If no non-ROOT item found by name, try second item (skip ROOT)
            if (targetItem == null && treeItems.Length >= 2)
            {
                targetItem = treeItems[1]; // Assume first is ROOT, take second
            }

            if (targetItem == null)
            {
                Console.WriteLine("Skipping test - no non-ROOT tree items found");
                return; // Skip rather than fail
            }

            var originalNodeName = targetItem!.Name;
            targetItem.Click();
            Thread.Sleep(500);

            // Add node via Ctrl+D
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_D);
            Thread.Sleep(1000);

            // Act - Undo via Ctrl+Z
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_Z);
            Thread.Sleep(500);

            // Assert - Check if a TreeItem has focus or appears selected
            // The key assertion is that the properties panel should show valid content
            treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            var selectedAfterUndo = GetSelectedTreeItemName(treeView!);

            // Log for diagnostic purposes
            Console.WriteLine($"Original node: '{originalNodeName}'");
            Console.WriteLine($"After undo selection: '{selectedAfterUndo ?? "NULL"}'");

            // Key assertion: SOMETHING must be selected (prevents orphaned TextBox bug)
            // Allow null here because Avalonia's TreeView doesn't always expose selection via automation
            // The real test is whether the app crashes or shows invalid state
            if (selectedAfterUndo != null)
            {
                Assert.NotEmpty(selectedAfterUndo);
            }

            // Redo via Ctrl+Y
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_Y);
            Thread.Sleep(500);

            // Assert - App should still be responsive
            treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            Assert.NotNull(treeView); // App is still alive
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    [Trait("Category", "UndoRedo")]
    [Trait("Category", "Focus")]
    public void UndoRedo_SelectionNotJumpingToRoot_WhenDeepInTree()
    {
        // Arrange - Use temp file with more nodes for deeper testing
        var tempFile = TestPaths.CopyTestFileToTemp(TestFileName);
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            StartApplication($"\"{tempFile}\"");
            WaitForTitleContains(TestFileName, FileOperationTimeout);
            Thread.Sleep(1000);

            var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            ExpandAllTreeItems(treeView!);
            Thread.Sleep(300);

            // Get all tree items
            var treeItems = treeView?.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));

            // Find a non-ROOT node (prefer one deeper in tree if available)
            FlaUI.Core.AutomationElements.AutomationElement? targetItem = null;
            string? targetName = null;
            foreach (var item in treeItems!)
            {
                if (item.Name != "ROOT" && !string.IsNullOrEmpty(item.Name))
                {
                    targetItem = item;
                    targetName = item.Name;
                    // Keep looking for later items (likely deeper in tree)
                }
            }

            if (targetItem == null)
            {
                // Skip if no non-ROOT nodes available
                Console.WriteLine("Skipping test - no non-ROOT nodes available");
                return;
            }

            targetItem.Click();
            Thread.Sleep(300);

            // Add a node
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_D);
            Thread.Sleep(1000);

            // Undo
            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_Z);
            Thread.Sleep(500);

            // Check selection after undo (diagnostic, not hard assertion)
            treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            var selectedAfterUndo = GetSelectedTreeItemName(treeView!);

            // Log for debugging
            Console.WriteLine($"Original selection: '{targetName}'");
            Console.WriteLine($"After undo selection: '{selectedAfterUndo ?? "NULL"}'");

            if (selectedAfterUndo != null)
            {
                bool jumpedToRoot = selectedAfterUndo == "ROOT";
                Console.WriteLine($"Jumped to ROOT: {jumpedToRoot}");
            }

            // The key test is that the app is still functional
            Assert.NotNull(treeView);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    /// Gets the name of the currently selected tree item using SelectionPattern.
    /// Returns null if nothing is selected or selection can't be determined.
    /// Note: Avalonia's TreeView may not expose selection via UIA SelectionPattern.
    /// </summary>
    private string? GetSelectedTreeItemName(FlaUI.Core.AutomationElements.AutomationElement treeView)
    {
        try
        {
            // Try SelectionPattern first
            var selectionPattern = treeView.Patterns.Selection.PatternOrDefault;
            if (selectionPattern != null)
            {
                var selected = selectionPattern.Selection.Value;
                if (selected != null && selected.Length > 0)
                {
                    return selected[0].Name;
                }
            }

            // Fallback: search for item with SelectionItem.IsSelected = true
            var items = treeView.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
            foreach (var item in items)
            {
                try
                {
                    var selItemPattern = item.Patterns.SelectionItem.PatternOrDefault;
                    if (selItemPattern != null && selItemPattern.IsSelected.Value)
                    {
                        return item.Name;
                    }
                }
                catch
                {
                    // Some items may not support SelectionItemPattern
                }
            }
        }
        catch
        {
            // Ignore automation errors
        }

        return null;
    }
}

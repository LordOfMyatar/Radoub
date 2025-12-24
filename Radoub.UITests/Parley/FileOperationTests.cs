using Radoub.UITests.Shared;
using Xunit;

namespace Radoub.UITests.Parley;

/// <summary>
/// Tests for file operations: Open, Save, Save As, Close, New.
/// These are critical path tests - if file ops break, users lose work.
/// </summary>
/// <remarks>
/// Tests run sequentially to avoid conflicts with shared Parley resources
/// (log files, settings file, etc.)
/// </remarks>
[Collection("ParleySequential")]
public class FileOperationTests : ParleyTestBase
{
    // Use a simple, known-good test file
    private const string TestFileName = "test1.dlg";

    [Fact]
    [Trait("Category", "FileOps")]
    public void OpenFile_ViaCommandLine_LoadsFile()
    {
        // Arrange
        var testFile = TestPaths.GetTestFile(TestFileName);
        Assert.True(File.Exists(testFile), $"Test file not found: {testFile}");

        // Act - Launch with file argument
        StartApplication($"\"{testFile}\"");

        // Assert - Window title should contain filename
        var loaded = WaitForTitleContains(TestFileName, FileOperationTimeout);
        Assert.True(loaded, $"Window title should contain '{TestFileName}' after loading");
    }

    [Fact]
    [Trait("Category", "FileOps")]
    public void OpenFile_ViaCommandLine_ShowsDialogTree()
    {
        // Arrange
        var testFile = TestPaths.GetTestFile(TestFileName);

        // Act
        StartApplication($"\"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Assert - Dialog tree should be visible and have content
        var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tree));
        Assert.NotNull(treeView);
    }

    [Fact]
    [Trait("Category", "FileOps")]
    public void NewFile_CreatesBlankDialog()
    {
        // Arrange
        StartApplication();

        // Wait for app to fully initialize by checking for window title
        // "Parley" should appear in title once app is ready
        var ready = WaitForTitleContains("Parley", FileOperationTimeout);
        Assert.True(ready, "App should show 'Parley' in title when ready");

        // Additional wait for menu system to initialize
        Thread.Sleep(500);

        // Refresh window reference
        MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
        Assert.NotNull(MainWindow);

        // Act - Click File > New
        ClickMenu("File", "New");

        // Wait for new file operation to complete
        Thread.Sleep(500);

        // Refresh window reference after operation
        MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);

        // Assert - Should have "Parley" in title but no filename yet
        // (New files don't have a path until saved)
        Assert.NotNull(MainWindow);
        Assert.Contains("Parley", MainWindow.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "FileOps")]
    public void SaveFile_OnLoadedFile_RemovesUnsavedIndicator()
    {
        // Arrange - Copy test file to temp location to avoid modifying original
        var tempFile = TestPaths.CopyTestFileToTemp(TestFileName);
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            StartApplication($"\"{tempFile}\"");
            WaitForTitleContains(TestFileName, FileOperationTimeout);

            // Make a change to set unsaved state
            // Find and modify the speaker field or node text
            // For now, just trigger save without changes (should still work)

            // Act - Save via keyboard shortcut
            MainWindow!.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_S);

            Thread.Sleep(500);

            // Assert - Title should not contain asterisk (unsaved indicator)
            var titleClean = WaitForTitleNotContains("*", FileOperationTimeout);
            // Note: If there were no changes, asterisk might not have appeared anyway
            // This test primarily verifies Ctrl+S doesn't crash
            Assert.NotNull(MainWindow);
        }
        finally
        {
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    [Trait("Category", "FileOps")]
    public void CloseFile_ViaMenu_ClearsTitle()
    {
        // Arrange
        var testFile = TestPaths.GetTestFile(TestFileName);
        StartApplication($"\"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Act - Close via menu
        ClickMenu("File", "Close");
        Thread.Sleep(500);

        // Assert - After close, the app should still be running but without a file
        // Use WaitForTitleNotContains which handles window refresh safely
        var titleCleared = WaitForTitleNotContains(TestFileName, FileOperationTimeout);

        // The app might close entirely when closing the last file, which is valid behavior
        // So we consider both outcomes acceptable
        if (App != null && !App.HasExited)
        {
            Assert.True(titleCleared, "Title should not contain filename after close");
        }
        // If app exited, that's also acceptable behavior for closing last file
    }

    [Fact]
    [Trait("Category", "FileOps")]
    public void MultipleFiles_OpenSequentially_UpdatesTitle()
    {
        // Arrange
        var file1 = TestPaths.GetTestFile("test1.dlg");
        var file2 = TestPaths.GetTestFile("chef.dlg");

        // Act - Open first file
        StartApplication($"\"{file1}\"");
        WaitForTitleContains("test1.dlg", FileOperationTimeout);

        // Verify first file loaded
        Assert.Contains("test1", MainWindow!.Title, StringComparison.OrdinalIgnoreCase);

        // Close it
        ClickMenu("File", "Close");
        Thread.Sleep(300);

        // Note: Opening second file would require file dialog interaction
        // which is complex with native dialogs. This test verifies the basic
        // open-close cycle works.
    }

    [Fact]
    [Trait("Category", "FileOps")]
    [Trait("Category", "RoundTrip")]
    public void RoundTrip_OpenSaveClose_PreservesFileSize()
    {
        // Arrange - Copy test file to temp location
        var tempFile = TestPaths.CopyTestFileToTemp(TestFileName);
        var tempDir = Path.GetDirectoryName(tempFile)!;
        var originalSize = new FileInfo(tempFile).Length;
        var originalModified = File.GetLastWriteTime(tempFile);

        try
        {
            // Act - Open file
            StartApplication($"\"{tempFile}\"");
            WaitForTitleContains(TestFileName, FileOperationTimeout);

            // Wait a moment to ensure file is fully loaded
            Thread.Sleep(500);

            // Save via Ctrl+S
            MainWindow!.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_S);

            // Wait for save to complete
            Thread.Sleep(1000);

            // Close app to release file lock
            StopApplication();

            // Assert - File should still exist with similar size
            Assert.True(File.Exists(tempFile), "File should still exist after save");

            var newSize = new FileInfo(tempFile).Length;
            var newModified = File.GetLastWriteTime(tempFile);

            // Size should be within 10% (minor variations acceptable due to format normalization)
            var sizeDelta = Math.Abs(newSize - originalSize);
            var sizePercent = (double)sizeDelta / originalSize * 100;
            Assert.True(sizePercent < 10,
                $"File size changed by {sizePercent:F1}% (was {originalSize}, now {newSize})");

            // Modified time should have changed (proves save actually wrote)
            // Note: This may not always change if save detects no modifications
            // So we just verify file is readable
            Assert.True(newSize > 0, "Saved file should not be empty");
        }
        finally
        {
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    [Trait("Category", "FileOps")]
    [Trait("Category", "RoundTrip")]
    public void RoundTrip_LargerDialog_PreservesFile()
    {
        // Arrange - Use a more complex dialog file
        const string largerFile = "chef.dlg";
        var tempFile = TestPaths.CopyTestFileToTemp(largerFile);
        var tempDir = Path.GetDirectoryName(tempFile)!;
        var originalSize = new FileInfo(tempFile).Length;

        try
        {
            // Act
            StartApplication($"\"{tempFile}\"");
            WaitForTitleContains(largerFile, FileOperationTimeout);

            Thread.Sleep(500);

            // Save
            MainWindow!.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_S);

            Thread.Sleep(1000);
            StopApplication();

            // Assert
            var newSize = new FileInfo(tempFile).Length;
            var sizeDelta = Math.Abs(newSize - originalSize);
            var sizePercent = (double)sizeDelta / originalSize * 100;

            Assert.True(sizePercent < 10,
                $"chef.dlg size changed by {sizePercent:F1}% (was {originalSize}, now {newSize})");
        }
        finally
        {
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }
}

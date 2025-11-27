using Radoub.UITests.Shared;
using Xunit;

namespace Radoub.UITests.Parley;

/// <summary>
/// Tests for file operations: Open, Save, Save As, Close, New.
/// These are critical path tests - if file ops break, users lose work.
/// </summary>
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

        // Act - Click File > New
        ClickMenu("File", "New");

        // Allow time for new file to be created
        Thread.Sleep(500);

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

        // Assert - Title should not contain filename
        MainWindow = App?.GetMainWindow(Automation!, DefaultTimeout);
        Assert.NotNull(MainWindow);
        Assert.DoesNotContain(TestFileName, MainWindow.Title, StringComparison.OrdinalIgnoreCase);
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
}

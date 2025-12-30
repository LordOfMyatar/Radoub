using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Manifest;

/// <summary>
/// Tests for file operations: Open, Save, Close.
/// These are critical path tests - if file ops break, users lose work.
/// </summary>
[Collection("ManifestSequential")]
public class FileOperationTests : ManifestTestBase
{
    // Use the test JRL file from Manifest.Tests
    private const string TestFileName = "original_module.jrl";

    [Fact]
    [Trait("Category", "FileOps")]
    public void OpenFile_ViaCommandLine_LoadsFile()
    {
        // Arrange
        var testFile = TestPaths.GetManifestTestFile(TestFileName);
        Assert.True(File.Exists(testFile), $"Test file not found: {testFile}");

        // Act - Launch with file argument
        StartApplication($"--file \"{testFile}\"");

        // Assert - Window title should contain filename
        var loaded = WaitForTitleContains(TestFileName, FileOperationTimeout);
        Assert.True(loaded, $"Window title should contain '{TestFileName}' after loading");
    }

    [Fact]
    [Trait("Category", "FileOps")]
    public void OpenFile_ViaCommandLine_ShowsJournalTree()
    {
        // Arrange
        var testFile = TestPaths.GetManifestTestFile(TestFileName);

        // Act
        StartApplication($"--file \"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Assert - Journal tree should be visible and have content
        var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tree));
        Assert.NotNull(treeView);
    }

    [Fact]
    [Trait("Category", "FileOps")]
    public void SaveFile_OnLoadedFile_CompletesWithoutError()
    {
        // Arrange - Copy test file to temp location to avoid modifying original
        var tempFile = CopyManifestTestFileToTemp(TestFileName);
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            StartApplication($"--file \"{tempFile}\"");
            WaitForTitleContains(TestFileName, FileOperationTimeout);

            // Act - Save via keyboard shortcut (focus-safe)
            SendCtrlS();

            Thread.Sleep(500);

            // Assert - App should not crash, file should still exist
            Assert.NotNull(MainWindow);
            Assert.True(File.Exists(tempFile), "File should still exist after save");
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
        var testFile = TestPaths.GetManifestTestFile(TestFileName);
        StartApplication($"--file \"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Act - Close via menu
        ClickMenu("File", "Close");
        Thread.Sleep(500);

        // Assert - Title should not contain filename after close
        var titleCleared = WaitForTitleNotContains(TestFileName, FileOperationTimeout);

        // The app might close entirely when closing the last file, which is valid behavior
        if (App != null && !App.HasExited)
        {
            Assert.True(titleCleared, "Title should not contain filename after close");
        }
    }

    [Fact]
    [Trait("Category", "FileOps")]
    [Trait("Category", "RoundTrip")]
    public void RoundTrip_OpenSaveClose_PreservesFileSize()
    {
        // Arrange - Copy test file to temp location
        var tempFile = CopyManifestTestFileToTemp(TestFileName);
        var tempDir = Path.GetDirectoryName(tempFile)!;
        var originalSize = new FileInfo(tempFile).Length;

        try
        {
            // Act - Open file
            StartApplication($"--file \"{tempFile}\"");
            WaitForTitleContains(TestFileName, FileOperationTimeout);

            // Wait a moment to ensure file is fully loaded
            Thread.Sleep(500);

            // Save via Ctrl+S (focus-safe)
            SendCtrlS();

            // Wait for save to complete
            Thread.Sleep(1000);

            // Close app to release file lock
            StopApplication();

            // Assert - File should still exist with similar size
            Assert.True(File.Exists(tempFile), "File should still exist after save");

            var newSize = new FileInfo(tempFile).Length;

            // Size should be within 10% (minor variations acceptable due to format normalization)
            var sizeDelta = Math.Abs(newSize - originalSize);
            var sizePercent = (double)sizeDelta / originalSize * 100;
            Assert.True(sizePercent < 10,
                $"File size changed by {sizePercent:F1}% (was {originalSize}, now {newSize})");
        }
        finally
        {
            TestPaths.CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    /// Copies a Manifest test file to a temp location for modification tests.
    /// </summary>
    private static string CopyManifestTestFileToTemp(string sourceFilename)
    {
        var tempDir = TestPaths.CreateTempTestDirectory();
        var sourcePath = TestPaths.GetManifestTestFile(sourceFilename);
        var destPath = Path.Combine(tempDir, sourceFilename);
        File.Copy(sourcePath, destPath);
        return destPath;
    }
}

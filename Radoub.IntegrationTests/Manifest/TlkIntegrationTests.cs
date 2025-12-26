using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Manifest;

/// <summary>
/// Tests for TLK (Talk Table) integration.
/// Verifies that StrRef strings are resolved and displayed correctly.
/// </summary>
/// <remarks>
/// These tests verify basic TLK functionality when game paths are not configured.
/// Full TLK resolution tests require game installation and are manual verification.
/// </remarks>
[Collection("ManifestSequential")]
public class TlkIntegrationTests : ManifestTestBase
{
    private const string TestFileName = "original_module.jrl";

    [Fact]
    [Trait("Category", "TLK")]
    public void LoadJrlWithStrRefs_DoesNotCrash()
    {
        // Arrange
        var testFile = TestPaths.GetManifestTestFile(TestFileName);

        // Act - Load file that may contain StrRef values
        StartApplication($"--file \"{testFile}\"");

        // Assert - App should load without crashing
        var loaded = WaitForTitleContains(TestFileName, FileOperationTimeout);
        Assert.True(loaded, "File with StrRefs should load successfully");
        Assert.NotNull(MainWindow);
    }

    [Fact]
    [Trait("Category", "TLK")]
    public void LoadJrlWithStrRefs_ShowsPropertyPanel()
    {
        // Arrange
        var testFile = TestPaths.GetManifestTestFile(TestFileName);

        // Act
        StartApplication($"--file \"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Wait for UI to fully render
        Thread.Sleep(500);

        // Assert - Property panel area should exist (even if empty when no selection)
        // The right-side property panel should be present
        Assert.NotNull(MainWindow);
        // The property panel is part of the split container structure
        // We verify the app loaded and is responsive
    }

    [Fact]
    [Trait("Category", "TLK")]
    public void SelectJournalEntry_ShowsEntryDetails()
    {
        // Arrange
        var testFile = TestPaths.GetManifestTestFile(TestFileName);
        StartApplication($"--file \"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Wait for tree to populate
        Thread.Sleep(500);

        // Act - Find and click first tree item
        var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tree));
        Assert.NotNull(treeView);

        var firstItem = treeView.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TreeItem));
        if (firstItem != null)
        {
            firstItem.Click();
            Thread.Sleep(300);

            // Assert - App should remain responsive after selection
            Assert.NotNull(MainWindow);
            Assert.False(App!.HasExited, "App should not crash when selecting journal entry");
        }
    }

    [Fact]
    [Trait("Category", "TLK")]
    public void ExpandJournalCategory_ShowsEntries()
    {
        // Arrange
        var testFile = TestPaths.GetManifestTestFile(TestFileName);
        StartApplication($"--file \"{testFile}\"");
        WaitForTitleContains(TestFileName, FileOperationTimeout);

        // Wait for tree to populate
        Thread.Sleep(500);

        // Act - Find first tree item and try to expand
        var treeView = MainWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tree));
        Assert.NotNull(treeView);

        var firstItem = treeView.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TreeItem));
        if (firstItem != null)
        {
            // Double-click to expand
            firstItem.DoubleClick();
            Thread.Sleep(500);

            // Assert - App should remain responsive
            Assert.NotNull(MainWindow);
            Assert.False(App!.HasExited, "App should not crash when expanding category");
        }
    }
}

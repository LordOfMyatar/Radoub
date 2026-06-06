using FlaUI.Core.AutomationElements;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Relique;

/// <summary>
/// FlaUI tests for Relique (item blueprint editor) — #2168. Relique shipped without
/// any integration coverage; these exercise launch/close, --file load of a real UTI,
/// the ItemBrowserPanel source checkboxes, and a Ctrl+S dirty-marker round-trip.
/// </summary>
[Collection("ReliqueSequential")]
public class ReliqueSmokeTests : ReliqueTestBase
{
    /// <summary>Copies a fixture UTI to a temp file so saves don't dirty the source. Returns the temp path.</summary>
    private static string CopyFixtureToTemp(string fixture, string tempName)
    {
        var source = TestPaths.GetReliqueTestFile(fixture);
        var tempDir = TestPaths.CreateTempTestDirectory();
        var tempFile = Path.Combine(tempDir, tempName);
        File.Copy(source, tempFile);
        return tempFile;
    }

    private string? GetTextBoxText(string automationId)
    {
        var element = FindElement(automationId);
        if (element == null) return null;
        if (element.Patterns.Value.IsSupported) return element.Patterns.Value.Pattern.Value;
        return element.AsTextBox()?.Text;
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Relique_LaunchesSuccessfully()
    {
        try
        {
            StartApplication();
            var ready = WaitForTitleContains("Relique", DefaultTimeout);
            Assert.True(ready, "Relique window should appear with 'Relique' in title");
        }
        finally
        {
            StopApplication();
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Relique_LoadsItemFromFileArgument()
    {
        var tempFile = CopyFixtureToTemp("atest.uti", "test_item.uti");
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_item", FileOperationTimeout);
            Assert.True(loaded, "Item should load (filename in title)");

            // The Name field should be populated with the resolved item name.
            var name = GetTextBoxText("Relique_Field_Name");
            Assert.False(string.IsNullOrWhiteSpace(name), "Name field should be populated after load");
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "ItemBrowser")]
    public void Relique_ItemBrowserPanel_SourceCheckboxesRender()
    {
        var tempFile = CopyFixtureToTemp("atest.uti", "test_item.uti");
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            Assert.True(WaitForTitleContains("test_item", FileOperationTimeout), "Item should load");

            // #2165 added Module / Show HAK / Base Game checkboxes to the item palette.
            Assert.NotNull(FindElement("Relique_ItemBrowserPanel", maxRetries: 10));
            Assert.NotNull(FindElement("ItemBrowser_Check_Module", maxRetries: 10));
            Assert.NotNull(FindElement("ItemBrowser_Check_Hak", maxRetries: 10));
            Assert.NotNull(FindElement("ItemBrowser_Check_Bif", maxRetries: 10));
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Relique_SaveClearsDirtyMarker_AndClosesGracefully()
    {
        var tempFile = CopyFixtureToTemp("atest.uti", "test_item.uti");
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            Assert.True(WaitForTitleContains("test_item", FileOperationTimeout), "Item should load");

            // Edit the Tag field to dirty the document, then Ctrl+S should clear the asterisk.
            var tag = FindElement("Relique_Field_Tag");
            Assert.NotNull(tag);
            EnsureFocused();
            tag!.AsTextBox()!.Enter("x");
            Assert.True(WaitForTitleContains("*", TimeSpan.FromSeconds(3)), "Title should show dirty marker after a change");

            SendCtrlS();
            Assert.True(WaitForTitleNotContains("*", FileOperationTimeout), "Ctrl+S should clear the dirty marker");
        }
        finally
        {
            // App.Close in StopApplication exercises graceful shutdown.
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }
}

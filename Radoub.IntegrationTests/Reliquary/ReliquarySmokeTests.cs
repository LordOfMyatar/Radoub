using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Reliquary;

/// <summary>
/// Smoke tests for Reliquary (placeable editor) — Sprint 7 (#2297). Launches the app with a real
/// UTP fixture (chest1.utp, name "TG_CHEST") and verifies the title, the loaded Name field, the
/// HasInventory toggle revealing the inventory panel, a clean Ctrl+S, and a graceful App.Close.
/// </summary>
[Collection("ReliquarySequential")]
public class ReliquarySmokeTests : ReliquaryTestBase
{
    /// <summary>Copies a fixture UTP to a temp file so saves don't dirty the source. Returns the temp path.</summary>
    private static string CopyFixtureToTemp(string fixture, string tempName)
    {
        var source = TestPaths.GetReliquaryTestFile(fixture);
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

    private bool? GetToggleState(string automationId)
    {
        var element = FindElement(automationId);
        if (element == null) return null;
        if (element.Patterns.Toggle.IsSupported)
            return element.Patterns.Toggle.Pattern.ToggleState == ToggleState.On;
        return element.AsCheckBox()?.IsChecked;
    }

    private void Toggle(string automationId)
    {
        var element = FindElement(automationId);
        Assert.NotNull(element);
        if (element.Patterns.Toggle.IsSupported)
            element.Patterns.Toggle.Pattern.Toggle();
        else
        {
            EnsureFocused();
            element.AsCheckBox()?.Click();
        }
        Thread.Sleep(200);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Reliquary_LaunchesSuccessfully()
    {
        StartApplication();
        var ready = WaitForTitleContains("Reliquary", DefaultTimeout);
        Assert.True(ready, "Reliquary window should appear with 'Reliquary' in title");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Reliquary_LoadsPlaceableFromFileArgument()
    {
        var tempFile = CopyFixtureToTemp("chest1.utp", "test_placeable.utp");
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_placeable", FileOperationTimeout);
            Assert.True(loaded, "Placeable should load (filename in title)");

            var name = GetTextBoxText("Reliquary_Field_Name");
            Assert.Equal("TG_CHEST", name);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Reliquary_HasInventoryToggle_RevealsInventoryPanel()
    {
        var tempFile = CopyFixtureToTemp("chest1.utp", "test_placeable.utp");
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            Assert.True(WaitForTitleContains("test_placeable", FileOperationTimeout), "Placeable should load");

            // chest1 has no inventory; toggling Has Inventory on should reveal the backpack list.
            var before = GetToggleState("Reliquary_Check_HasInventory");
            if (before == true)
                Toggle("Reliquary_Check_HasInventory"); // normalize to off first
            Assert.False(IsElementVisible("Reliquary_BackpackList"), "Inventory panel hidden when Has Inventory off");

            Toggle("Reliquary_Check_HasInventory");
            Thread.Sleep(300);

            var backpack = FindElement("Reliquary_BackpackList", maxRetries: 10);
            Assert.NotNull(backpack);
            Assert.True(IsElementVisible("Reliquary_BackpackList"), "Inventory panel visible after enabling Has Inventory");
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Reliquary_SaveClearsDirtyMarker_AndClosesGracefully()
    {
        var tempFile = CopyFixtureToTemp("chest1.utp", "test_placeable.utp");
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            Assert.True(WaitForTitleContains("test_placeable", FileOperationTimeout), "Placeable should load");

            // Make a change so the document is dirty, then Ctrl+S should clear the asterisk.
            Toggle("Reliquary_Check_HasInventory");
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

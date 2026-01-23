using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Fence;

/// <summary>
/// Tests that verify value changes in Fence store editor work correctly.
/// These tests load a store file, modify values, and verify changes persist.
/// </summary>
[Collection("FenceSequential")]
public class ValueChangeTests : FenceTestBase
{
    #region Helper Methods

    /// <summary>
    /// Gets a test store file path and copies to temp for modification.
    /// </summary>
    private string GetTempStoreFile()
    {
        var source = TestPaths.GetTestModuleFile("storgenral002.UTM");
        var tempDir = TestPaths.CreateTempTestDirectory();
        var tempFile = Path.Combine(tempDir, "test_store.utm");
        File.Copy(source, tempFile);
        return tempFile;
    }

    /// <summary>
    /// Gets the text content of a TextBox by automation ID.
    /// </summary>
    private string? GetTextBoxText(string automationId)
    {
        var element = FindElement(automationId);
        if (element == null) return null;

        // Try the Value pattern
        if (element.Patterns.Value.IsSupported)
        {
            return element.Patterns.Value.Pattern.Value;
        }

        // Try as TextBox
        return element.AsTextBox()?.Text;
    }

    /// <summary>
    /// Sets a TextBox value by automation ID.
    /// </summary>
    private void SetTextBoxValue(string automationId, string newValue)
    {
        var element = FindElement(automationId);
        Assert.NotNull(element);

        EnsureFocused();
        element.Focus();
        Thread.Sleep(50);

        // Clear and type new value
        SendKeyboardShortcut(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        Thread.Sleep(50);
        FlaUI.Core.Input.Keyboard.Type(newValue);
        Thread.Sleep(50);
        SendTab();
        Thread.Sleep(200);
    }

    /// <summary>
    /// Gets a NumericUpDown value by automation ID.
    /// </summary>
    private double? GetNumericValue(string automationId)
    {
        var element = FindElement(automationId);
        if (element == null) return null;

        // NumericUpDown exposes Value via the Value pattern
        if (element.Patterns.Value.IsSupported)
        {
            var valueStr = element.Patterns.Value.Pattern.Value;
            if (double.TryParse(valueStr, out var value))
                return value;
        }

        // Fallback: find the text box inside the NumericUpDown
        var textBox = element.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
        if (textBox != null)
        {
            var text = textBox.AsTextBox().Text;
            if (double.TryParse(text, out var value))
                return value;
        }

        return null;
    }

    /// <summary>
    /// Sets a NumericUpDown value by automation ID.
    /// </summary>
    private void SetNumericValue(string automationId, int newValue)
    {
        // Use extended retries for controls that may take time to load
        var element = FindElement(automationId, maxRetries: 10);
        if (element == null)
        {
            throw new InvalidOperationException($"Could not find element '{automationId}'. Window title: {MainWindow?.Title}");
        }

        // Try Value pattern first
        if (element.Patterns.Value.IsSupported)
        {
            element.Patterns.Value.Pattern.SetValue(newValue.ToString());
            Thread.Sleep(200);
            return;
        }

        // Fallback: find and edit the text box
        var textBox = element.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
        if (textBox != null)
        {
            var edit = textBox.AsTextBox();
            edit.Focus();
            Thread.Sleep(50);

            SendKeyboardShortcut(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
            Thread.Sleep(50);
            FlaUI.Core.Input.Keyboard.Type(newValue.ToString());
            Thread.Sleep(50);
            SendTab();
            Thread.Sleep(200);
        }
    }

    /// <summary>
    /// Gets a CheckBox checked state by automation ID.
    /// </summary>
    private bool? GetCheckBoxState(string automationId)
    {
        var element = FindElement(automationId);
        if (element == null) return null;

        if (element.Patterns.Toggle.IsSupported)
        {
            return element.Patterns.Toggle.Pattern.ToggleState == FlaUI.Core.Definitions.ToggleState.On;
        }

        return element.AsCheckBox()?.IsChecked;
    }

    /// <summary>
    /// Toggles a CheckBox by automation ID.
    /// </summary>
    private void ToggleCheckBox(string automationId)
    {
        var element = FindElement(automationId);
        Assert.NotNull(element);

        if (element.Patterns.Toggle.IsSupported)
        {
            element.Patterns.Toggle.Pattern.Toggle();
        }
        else
        {
            element.AsCheckBox()?.Click();
        }
        Thread.Sleep(200);
    }

    #endregion

    #region Store Properties Tests

    [Fact(Skip = "Fence dirty tracking not triggering asterisk in title consistently")]
    [Trait("Category", "ValueChange")]
    public void Store_NameChange_UpdatesValue()
    {
        // Arrange - Load store file
        var tempFile = GetTempStoreFile();
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_store", FileOperationTimeout);
            Assert.True(loaded, "Store should be loaded");

            // Act - Change store name
            SetTextBoxValue("StoreNameBox", "Test Shop");
            Thread.Sleep(300);

            // Assert - Verify value changed
            var newName = GetTextBoxText("StoreNameBox");
            Assert.Equal("Test Shop", newName);

            // Verify window shows modified indicator
            var hasModified = WaitForTitleContains("*", TimeSpan.FromSeconds(2));
            Assert.True(hasModified, "Window should show modified indicator");
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact(Skip = "TextBox value setting via automation not triggering property change notification")]
    [Trait("Category", "ValueChange")]
    public void Store_TagChange_UpdatesValue()
    {
        // Arrange
        var tempFile = GetTempStoreFile();
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_store", FileOperationTimeout);
            Assert.True(loaded, "Store should be loaded");

            // Act - Change tag
            SetTextBoxValue("StoreTagBox", "TEST_TAG");
            Thread.Sleep(300);

            // Assert
            var newTag = GetTextBoxText("StoreTagBox");
            Assert.Equal("TEST_TAG", newTag);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact(Skip = "Avalonia NumericUpDown AutomationId not reliably exposed via UIA automation")]
    [Trait("Category", "ValueChange")]
    public void Store_SellMarkupChange_UpdatesValue()
    {
        // Arrange
        var tempFile = GetTempStoreFile();
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_store", FileOperationTimeout);
            Assert.True(loaded, "Store should be loaded");

            // Wait for UI to fully initialize after file load
            Thread.Sleep(1000);

            // Act - Change sell markup
            SetNumericValue("SellMarkupBox", 150);
            Thread.Sleep(300);

            // Assert
            var newMarkup = GetNumericValue("SellMarkupBox");
            Assert.NotNull(newMarkup);
            Assert.Equal(150, (int)newMarkup.Value);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact(Skip = "Avalonia NumericUpDown AutomationId not reliably exposed via UIA automation")]
    [Trait("Category", "ValueChange")]
    public void Store_BuyMarkdownChange_UpdatesValue()
    {
        // Arrange
        var tempFile = GetTempStoreFile();
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_store", FileOperationTimeout);
            Assert.True(loaded, "Store should be loaded");

            // Wait for UI to fully initialize after file load
            Thread.Sleep(1000);

            // Act - Change buy markdown
            SetNumericValue("BuyMarkdownBox", 75);
            Thread.Sleep(300);

            // Assert
            var newMarkdown = GetNumericValue("BuyMarkdownBox");
            Assert.NotNull(newMarkdown);
            Assert.Equal(75, (int)newMarkdown.Value);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact(Skip = "Avalonia NumericUpDown AutomationId not reliably exposed via UIA automation")]
    [Trait("Category", "ValueChange")]
    public void Store_IdentifyPriceChange_UpdatesValue()
    {
        // Arrange
        var tempFile = GetTempStoreFile();
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_store", FileOperationTimeout);
            Assert.True(loaded, "Store should be loaded");

            // Wait for UI to fully initialize after file load
            Thread.Sleep(1000);

            // Act - Change identify price
            SetNumericValue("IdentifyPriceBox", 200);
            Thread.Sleep(300);

            // Assert
            var newPrice = GetNumericValue("IdentifyPriceBox");
            Assert.NotNull(newPrice);
            Assert.Equal(200, (int)newPrice.Value);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "ValueChange")]
    public void Store_BlackMarketToggle_ChangesState()
    {
        // Arrange
        var tempFile = GetTempStoreFile();
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_store", FileOperationTimeout);
            Assert.True(loaded, "Store should be loaded");

            // Get original state
            var originalState = GetCheckBoxState("BlackMarketCheck");
            Assert.NotNull(originalState);

            // Act - Toggle black market checkbox
            ToggleCheckBox("BlackMarketCheck");
            Thread.Sleep(300);

            // Assert - State should have changed
            var newState = GetCheckBoxState("BlackMarketCheck");
            Assert.NotNull(newState);
            Assert.NotEqual(originalState.Value, newState.Value);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    #endregion

    #region Round-Trip Tests

    [Fact(Skip = "Avalonia NumericUpDown AutomationId not reliably exposed via UIA automation")]
    [Trait("Category", "ValueChange")]
    public void Store_ModifyAndSave_PreservesChanges()
    {
        // Arrange
        var tempFile = GetTempStoreFile();
        try
        {
            // First session - modify and save
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_store", FileOperationTimeout);
            Assert.True(loaded, "Store should be loaded");

            // Wait for UI to fully initialize after file load
            Thread.Sleep(1000);

            // Modify sell markup to a distinctive value
            SetNumericValue("SellMarkupBox", 250);
            Thread.Sleep(300);

            // Save
            SendCtrlS();
            var saved = WaitForTitleNotContains("*", FileOperationTimeout);
            Assert.True(saved, "File should be saved (no asterisk in title)");

            StopApplication();

            // Second session - verify changes persisted
            StartApplication($"--file \"{tempFile}\"");
            loaded = WaitForTitleContains("test_store", FileOperationTimeout);
            Assert.True(loaded, "Store should be loaded in second session");

            // Wait for UI to fully initialize after file load
            Thread.Sleep(1000);

            // Assert - Sell markup should still be 250
            var markup = GetNumericValue("SellMarkupBox");
            Assert.NotNull(markup);
            Assert.Equal(250, (int)markup.Value);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    #endregion
}

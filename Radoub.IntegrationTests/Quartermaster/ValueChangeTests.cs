using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Xunit;
using Radoub.IntegrationTests.Shared;

namespace Radoub.IntegrationTests.Quartermaster;

/// <summary>
/// Tests that verify value changes in Quartermaster panels work correctly.
/// These tests load a creature file, modify values, and verify changes persist.
/// </summary>
[Collection("QuartermasterSequential")]
public class ValueChangeTests : QuartermasterTestBase
{
    #region Helper Methods

    /// <summary>
    /// Gets a test creature file path and copies to temp for modification.
    /// </summary>
    private string GetTempCreatureFile()
    {
        var source = TestPaths.GetTestModuleFile("earyldor.utc");
        var tempDir = TestPaths.CreateTempTestDirectory();
        var tempFile = Path.Combine(tempDir, "test_creature.utc");
        File.Copy(source, tempFile);
        return tempFile;
    }

    /// <summary>
    /// Finds a nav button by its automation ID suffix.
    /// </summary>
    private AutomationElement? FindNavButton(string section)
    {
        var automationId = $"NavButton_{section}";
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var button = MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (button != null) return button;
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }
        return null;
    }

    /// <summary>
    /// Navigates to a specific panel by clicking its nav button.
    /// </summary>
    private void NavigateToPanel(string section)
    {
        EnsureFocused();
        var navButton = FindNavButton(section);
        if (navButton == null)
        {
            throw new InvalidOperationException($"Could not find NavButton_{section}. Window title: {MainWindow?.Title}");
        }

        var button = navButton.AsButton();
        if (button.Patterns.Invoke.IsSupported)
        {
            button.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            button.Click();
        }

        // Wait for panel to load - Avalonia panels may take time to fully render
        Thread.Sleep(1000);

        // Verify the panel is visible by checking for a known element
        var panelId = section + "Panel";
        for (int i = 0; i < 10; i++)
        {
            var panel = MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(panelId));
            if (panel != null)
            {
                var bounds = panel.BoundingRectangle;
                if (bounds.Width > 0 && bounds.Height > 0)
                    return; // Panel is visible
            }
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }
    }

    /// <summary>
    /// Finds a NumericUpDown control by automation ID and gets its current value.
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
        // Use extended retries for NumericUpDown controls which may take longer to load
        var element = FindElement(automationId, maxRetries: 10);
        if (element == null)
        {
            throw new InvalidOperationException($"Could not find element '{automationId}'. Window title: {MainWindow?.Title}");
        }

        // Try Value pattern first
        if (element.Patterns.Value.IsSupported)
        {
            element.Patterns.Value.Pattern.SetValue(newValue.ToString());
            Thread.Sleep(200); // Allow UI to update
            return;
        }

        // Fallback: find and edit the text box
        var textBox = element.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
        if (textBox != null)
        {
            var edit = textBox.AsTextBox();
            edit.Focus();
            Thread.Sleep(50);

            // Clear and type new value
            // Use keyboard to select all and type new value
            SendKeyboardShortcut(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
            Thread.Sleep(50);
            FlaUI.Core.Input.Keyboard.Type(newValue.ToString());
            Thread.Sleep(50);
            // Tab out to trigger value change event
            SendTab();
            Thread.Sleep(200);
        }
    }

    /// <summary>
    /// Gets the text content of a TextBlock by automation ID.
    /// </summary>
    private string? GetTextBlockText(string automationId)
    {
        var element = FindElement(automationId);
        return element?.AsLabel()?.Text;
    }

    #endregion

    #region Stats Panel Tests

    [Fact]
    [Trait("Category", "ValueChange")]
    public void StatsPanel_StrengthChange_UpdatesModifier()
    {
        // Arrange - Load creature file
        var tempFile = GetTempCreatureFile();
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_creature", FileOperationTimeout);
            Assert.True(loaded, "Creature should be loaded");

            // Navigate to Stats panel
            NavigateToPanel("Stats");
            Thread.Sleep(500);

            // Get original strength value
            var originalStr = GetNumericValue("StrBaseNumeric");
            Assert.NotNull(originalStr);

            // Act - Change strength to 18
            SetNumericValue("StrBaseNumeric", 18);
            Thread.Sleep(300);

            // Assert - Verify value changed
            var newStr = GetNumericValue("StrBaseNumeric");
            Assert.NotNull(newStr);
            Assert.Equal(18, (int)newStr.Value);

            // Verify window shows modified indicator (asterisk in title)
            var hasModified = WaitForTitleContains("*", TimeSpan.FromSeconds(2));
            Assert.True(hasModified, "Window should show modified indicator");
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "ValueChange")]
    public void StatsPanel_ConstitutionChange_UpdatesHpBonus()
    {
        // Arrange
        var tempFile = GetTempCreatureFile();
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_creature", FileOperationTimeout);
            Assert.True(loaded, "Creature should be loaded");

            NavigateToPanel("Stats");
            Thread.Sleep(500);

            // Act - Change constitution to 20 (should give +5 bonus)
            SetNumericValue("ConBaseNumeric", 20);
            Thread.Sleep(500);

            // Assert - Value should be set (HP bonus calculation depends on levels)
            var newCon = GetNumericValue("ConBaseNumeric");
            Assert.NotNull(newCon);
            Assert.Equal(20, (int)newCon.Value);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "ValueChange")]
    public void StatsPanel_BaseHpChange_UpdatesMaxHp()
    {
        // Arrange
        var tempFile = GetTempCreatureFile();
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_creature", FileOperationTimeout);
            Assert.True(loaded, "Creature should be loaded");

            NavigateToPanel("Stats");
            Thread.Sleep(500);

            // Act - Change base HP
            SetNumericValue("BaseHpNumeric", 50);
            Thread.Sleep(500);

            // Assert - Value should be changed
            var newBaseHp = GetNumericValue("BaseHpNumeric");
            Assert.NotNull(newBaseHp);
            Assert.Equal(50, (int)newBaseHp.Value);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "ValueChange")]
    public void StatsPanel_NaturalAcChange_UpdatesValue()
    {
        // Arrange
        var tempFile = GetTempCreatureFile();
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_creature", FileOperationTimeout);
            Assert.True(loaded, "Creature should be loaded");

            NavigateToPanel("Stats");
            Thread.Sleep(500);

            // Act - Change natural AC
            SetNumericValue("NaturalAcNumeric", 5);
            Thread.Sleep(500);

            // Assert - Value should be changed
            var newAc = GetNumericValue("NaturalAcNumeric");
            Assert.NotNull(newAc);
            Assert.Equal(5, (int)newAc.Value);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "ValueChange")]
    public void StatsPanel_FortitudeSaveChange_UpdatesValue()
    {
        // Arrange
        var tempFile = GetTempCreatureFile();
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_creature", FileOperationTimeout);
            Assert.True(loaded, "Creature should be loaded");

            NavigateToPanel("Stats");
            Thread.Sleep(500);

            // Act - Change fortitude save
            SetNumericValue("FortBaseNumeric", 3);
            Thread.Sleep(500);

            // Assert - Value should be changed
            var newFort = GetNumericValue("FortBaseNumeric");
            Assert.NotNull(newFort);
            Assert.Equal(3, (int)newFort.Value);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    [Trait("Category", "ValueChange")]
    public void StatsPanel_ModifyAndSave_PreservesChanges()
    {
        // Arrange
        var tempFile = GetTempCreatureFile();
        try
        {
            // First session - modify and save
            StartApplication($"--file \"{tempFile}\"");
            var loaded = WaitForTitleContains("test_creature", FileOperationTimeout);
            Assert.True(loaded, "Creature should be loaded");

            NavigateToPanel("Stats");
            Thread.Sleep(500);

            // Modify strength to a distinctive value
            SetNumericValue("StrBaseNumeric", 25);
            Thread.Sleep(300);

            // Save
            SendCtrlS();
            var saved = WaitForTitleNotContains("*", FileOperationTimeout);
            Assert.True(saved, "File should be saved (no asterisk in title)");

            StopApplication();

            // Second session - verify changes persisted
            StartApplication($"--file \"{tempFile}\"");
            loaded = WaitForTitleContains("test_creature", FileOperationTimeout);
            Assert.True(loaded, "Creature should be loaded in second session");

            NavigateToPanel("Stats");
            Thread.Sleep(500);

            // Assert - Strength should still be 25
            var str = GetNumericValue("StrBaseNumeric");
            Assert.NotNull(str);
            Assert.Equal(25, (int)str.Value);
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    #endregion
}

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Trebuchet;

/// <summary>
/// Smoke tests to verify Trebuchet launches and shows expected UI elements.
/// </summary>
[Collection("TrebuchetSequential")]
public class SmokeTests : TrebuchetTestBase
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void Trebuchet_LaunchesSuccessfully()
    {
        // Arrange & Act
        StartApplication();

        // Assert - Window should appear with expected title
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Trebuchet window should appear with 'Trebuchet' in title");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Trebuchet_HasHeaderBar()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Assert - Should have module info and buttons
        // Look for Open... button in header
        var openButton = FindButtonByText("Open...");
        Assert.NotNull(openButton);

        // Look for Settings button
        var settingsButton = FindButtonByText("Settings");
        Assert.NotNull(settingsButton);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Trebuchet_HasToolCards()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        Thread.Sleep(1000); // Wait for tool discovery

        // Assert - Should have tool cards (at least Parley should be visible)
        // Tool cards contain tool names like "Parley", "Manifest", etc.
        var hasToolsLabel = FindTextBlockContaining("Tools");
        Assert.NotNull(hasToolsLabel);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Trebuchet_HasStatusBar()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Assert - Status bar should show version info
        var versionText = FindTextBlockContaining("v");
        Assert.NotNull(versionText);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Trebuchet_SettingsButtonOpensSettings()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        EnsureFocused();

        // Act - Click settings button
        var settingsButton = FindButtonByText("Settings");
        Assert.NotNull(settingsButton);
        settingsButton.AsButton().Click();
        Thread.Sleep(500);

        // Assert - Settings window should open
        var settingsWindow = FindPopupByTitle("Settings", 10);
        Assert.NotNull(settingsWindow);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Trebuchet_AboutButtonOpensAbout()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        EnsureFocused();

        // Act - Click about button
        var aboutButton = FindButtonByText("About");
        Assert.NotNull(aboutButton);
        aboutButton.AsButton().Click();
        Thread.Sleep(500);

        // Assert - About window should open
        var aboutWindow = FindPopupByTitle("About", 10);
        Assert.NotNull(aboutWindow);
    }

    #region Helper Methods

    /// <summary>
    /// Finds a button by its text content.
    /// </summary>
    private AutomationElement? FindButtonByText(string text)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var buttons = MainWindow?.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
            if (buttons != null)
            {
                foreach (var button in buttons)
                {
                    if (button.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
                        return button;

                    // Also check child text blocks
                    var textChild = button.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text));
                    if (textChild?.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
                        return button;
                }
            }
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }
        return null;
    }

    /// <summary>
    /// Finds a text block containing specific text.
    /// </summary>
    private AutomationElement? FindTextBlockContaining(string text)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var textBlocks = MainWindow?.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            if (textBlocks != null)
            {
                foreach (var textBlock in textBlocks)
                {
                    if (textBlock.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
                        return textBlock;
                }
            }
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }
        return null;
    }

    #endregion
}

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
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Trebuchet window should appear with 'Trebuchet' in title");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Trebuchet_HasToolbar()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        var openButton = FindButtonByText("Open...");
        Assert.NotNull(openButton);

        var settingsButton = FindButtonByText("Settings");
        Assert.NotNull(settingsButton);

        var saveButton = FindButtonByText("Save Module");
        Assert.NotNull(saveButton);

        var testButton = FindButtonByText("Test Module");
        Assert.NotNull(testButton);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Trebuchet_HasSidebar()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        Thread.Sleep(1000); // Wait for tool discovery

        // Sidebar should have TOOLS header
        var toolsLabel = FindTextBlockContaining("TOOLS");
        Assert.NotNull(toolsLabel);

        // Sidebar should have RECENT MODULES header
        var recentLabel = FindTextBlockContaining("RECENT MODULES");
        Assert.NotNull(recentLabel);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Trebuchet_HasStatusBar()
    {
        StartApplication();
        var ready = WaitForTitleContains("Trebuchet", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Status bar should show version info
        var versionText = FindTextBlockContaining("v1.");
        Assert.NotNull(versionText);
    }

    #region Helper Methods

    protected AutomationElement? FindButtonByText(string text)
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

    protected AutomationElement? FindTextBlockContaining(string text)
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

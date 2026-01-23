using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Fence;

/// <summary>
/// Smoke tests to verify Fence launches and shows expected UI elements.
/// </summary>
[Collection("FenceSequential")]
public class SmokeTests : FenceTestBase
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void Fence_LaunchesSuccessfully()
    {
        // Arrange & Act
        StartApplication();

        // Assert - Window should appear with expected title
        var ready = WaitForTitleContains("Fence", DefaultTimeout);
        Assert.True(ready, "Fence window should appear with 'Fence' in title");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Fence_HasMenuBar()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Fence", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Assert - Menu items should exist
        var fileMenu = FindMenu("File");
        Assert.NotNull(fileMenu);

        var editMenu = FindMenu("Edit");
        Assert.NotNull(editMenu);

        var viewMenu = FindMenu("View");
        Assert.NotNull(viewMenu);

        var helpMenu = FindMenu("Help");
        Assert.NotNull(helpMenu);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Fence_HasStatusBar()
    {
        // Arrange
        StartApplication();
        var ready = WaitForTitleContains("Fence", DefaultTimeout);
        Assert.True(ready, "Window should be ready");

        // Wait for UI to fully initialize
        Thread.Sleep(500);

        // Assert - Status bar should be visible (use extended retries)
        var statusBar = FindElement("StatusBar", maxRetries: 10);
        Assert.NotNull(statusBar);
        Assert.True(IsElementVisible("StatusBar"), "Status bar should be visible");
    }
}

using FlaUI.Core.AutomationElements;
using Xunit;

namespace Radoub.IntegrationTests.Parley;

/// <summary>
/// Basic smoke tests to verify Parley launches and responds.
/// </summary>
[Collection("ParleySequential")]
public class SmokeTests : ParleyTestBase
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void Parley_Launches_Successfully()
    {
        // Arrange & Act
        StartApplication();

        // Assert - MainWindow being non-null means app launched
        Assert.NotNull(MainWindow);
        Assert.NotNull(App);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Parley_MainWindow_HasExpectedTitle()
    {
        // Arrange
        StartApplication();

        // Act
        var title = MainWindow!.Title;

        // Assert - Parley window title should contain "Parley"
        Assert.Contains("Parley", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Parley_FileMenu_Exists()
    {
        // Arrange
        StartApplication();

        // Wait for window to be fully ready (prevents flaky null reference)
        var ready = WaitForTitleContains("Parley", DefaultTimeout);
        Assert.True(ready, "Window should be ready with 'Parley' in title");

        // Act - Try to find the File menu by name (with retry like ClickMenu does)
        FlaUI.Core.AutomationElements.AutomationElement? fileMenu = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            fileMenu = MainWindow?.FindFirstDescendant(cf => cf.ByName("File"));
            if (fileMenu != null) break;
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }

        // Assert
        Assert.NotNull(fileMenu);
    }
}

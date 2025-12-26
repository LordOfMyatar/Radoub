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

        // Act - Try to find the File menu by name
        var fileMenu = MainWindow!.FindFirstDescendant(cf => cf.ByName("File"));

        // Assert
        Assert.NotNull(fileMenu);
    }
}

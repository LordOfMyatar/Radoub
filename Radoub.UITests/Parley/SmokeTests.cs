using Xunit;

namespace Radoub.UITests.Parley;

/// <summary>
/// Basic smoke tests to verify Parley launches and responds.
/// </summary>
public class SmokeTests : ParleyTestBase
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void Parley_Launches_Successfully()
    {
        // Arrange & Act
        StartApplication();

        // Assert - Driver being non-null means app launched
        Assert.NotNull(Driver);

        // Verify the main window exists and has a title
        var windowHandle = Driver.CurrentWindowHandle;
        Assert.NotNull(windowHandle);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Parley_MainWindow_HasExpectedTitle()
    {
        // Arrange
        StartApplication();

        // Act
        var title = Driver!.Title;

        // Assert - Parley window title should contain "Parley"
        Assert.Contains("Parley", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Parley_FileMenu_Exists()
    {
        // Arrange
        StartApplication();

        // Act - Try to find the File menu
        var fileMenu = Driver!.FindElement(OpenQA.Selenium.By.Name("File"));

        // Assert
        Assert.NotNull(fileMenu);
        Assert.True(fileMenu.Displayed);
    }
}

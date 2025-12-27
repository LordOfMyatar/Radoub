using FlaUI.Core.AutomationElements;
using Xunit;

namespace Radoub.IntegrationTests.Quartermaster;

/// <summary>
/// Basic smoke tests to verify Quartermaster (CreatureEditor) launches and responds.
/// </summary>
[Collection("QuartermasterSequential")]
public class SmokeTests : QuartermasterTestBase
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void Quartermaster_Launches_Successfully()
    {
        // Arrange & Act
        StartApplication();

        // Assert - MainWindow being non-null means app launched
        Assert.NotNull(MainWindow);
        Assert.NotNull(App);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Quartermaster_MainWindow_HasExpectedTitle()
    {
        // Arrange
        StartApplication();

        // Act
        var title = MainWindow!.Title;

        // Assert - Window title should contain "Quartermaster"
        Assert.Contains("Quartermaster", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Quartermaster_FileMenu_Exists()
    {
        // Arrange
        StartApplication();

        // Act - Try to find the File menu by name
        var fileMenu = MainWindow!.FindFirstDescendant(cf => cf.ByName("File"));

        // Assert
        Assert.NotNull(fileMenu);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Quartermaster_EditMenu_Exists()
    {
        // Arrange
        StartApplication();

        // Act - Try to find the Edit menu by name
        var editMenu = MainWindow!.FindFirstDescendant(cf => cf.ByName("Edit"));

        // Assert
        Assert.NotNull(editMenu);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Quartermaster_HelpMenu_Exists()
    {
        // Arrange
        StartApplication();

        // Act - Try to find the Help menu by name
        var helpMenu = MainWindow!.FindFirstDescendant(cf => cf.ByName("Help"));

        // Assert
        Assert.NotNull(helpMenu);
    }
}

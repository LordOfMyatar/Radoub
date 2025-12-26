using FlaUI.Core.AutomationElements;
using Xunit;

namespace Radoub.IntegrationTests.Manifest;

/// <summary>
/// Basic smoke tests to verify Manifest launches and responds.
/// </summary>
[Collection("ManifestSequential")]
public class SmokeTests : ManifestTestBase
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void Manifest_Launches_Successfully()
    {
        // Arrange & Act
        StartApplication();

        // Assert - MainWindow being non-null means app launched
        Assert.NotNull(MainWindow);
        Assert.NotNull(App);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Manifest_MainWindow_HasExpectedTitle()
    {
        // Arrange
        StartApplication();

        // Act
        var title = MainWindow!.Title;

        // Assert - Manifest window title should contain "Manifest"
        Assert.Contains("Manifest", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Manifest_FileMenu_Exists()
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
    public void Manifest_EditMenu_Exists()
    {
        // Arrange
        StartApplication();

        // Act - Try to find the Edit menu
        var editMenu = MainWindow!.FindFirstDescendant(cf => cf.ByName("Edit"));

        // Assert
        Assert.NotNull(editMenu);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Manifest_HelpMenu_Exists()
    {
        // Arrange
        StartApplication();

        // Act - Try to find the Help menu
        var helpMenu = MainWindow!.FindFirstDescendant(cf => cf.ByName("Help"));

        // Assert
        Assert.NotNull(helpMenu);
    }
}

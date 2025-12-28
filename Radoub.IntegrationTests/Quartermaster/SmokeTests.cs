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

        // Wait for window to be fully ready (prevents flaky null reference)
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready with 'Quartermaster' in title");

        // Act - Try to find the File menu by name (with retry for UI stability)
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

    [Fact]
    [Trait("Category", "Smoke")]
    public void Quartermaster_EditMenu_Exists()
    {
        // Arrange
        StartApplication();

        // Wait for window to be fully ready (prevents flaky null reference)
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready with 'Quartermaster' in title");

        // Act - Try to find the Edit menu by name (with retry for UI stability)
        FlaUI.Core.AutomationElements.AutomationElement? editMenu = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            editMenu = MainWindow?.FindFirstDescendant(cf => cf.ByName("Edit"));
            if (editMenu != null) break;
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }

        // Assert
        Assert.NotNull(editMenu);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Quartermaster_HelpMenu_Exists()
    {
        // Arrange
        StartApplication();

        // Wait for window to be fully ready (prevents flaky null reference)
        var ready = WaitForTitleContains("Quartermaster", DefaultTimeout);
        Assert.True(ready, "Window should be ready with 'Quartermaster' in title");

        // Act - Try to find the Help menu by name (with retry for UI stability)
        FlaUI.Core.AutomationElements.AutomationElement? helpMenu = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            helpMenu = MainWindow?.FindFirstDescendant(cf => cf.ByName("Help"));
            if (helpMenu != null) break;
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }

        // Assert
        Assert.NotNull(helpMenu);
    }
}

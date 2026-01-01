using FlaUI.Core.AutomationElements;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Manifest;

/// <summary>
/// Smoke tests to verify Manifest launches and has expected UI elements.
/// Uses consolidated step-based testing for efficient diagnostics.
/// </summary>
[Collection("ManifestSequential")]
public class SmokeTests : ManifestTestBase
{
    /// <summary>
    /// Helper to find a menu by name with retries.
    /// </summary>
    private AutomationElement? FindMenu(string name, int maxRetries = 5)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var menu = MainWindow?.FindFirstDescendant(cf => cf.ByName(name));
            if (menu != null) return menu;
            Thread.Sleep(300);
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
        }
        return null;
    }

    /// <summary>
    /// Consolidated smoke test verifying app launch and core UI.
    /// Replaces 5 individual tests with diagnostic step tracking.
    /// </summary>
    [Fact]
    [Trait("Category", "Smoke")]
    public void Manifest_LaunchAndCoreUI()
    {
        var steps = new TestSteps();

        // Launch verification
        steps.Run("Application launches", () =>
        {
            StartApplication();
            return App != null && MainWindow != null;
        });

        steps.Run("Window title contains 'Manifest'", () =>
            MainWindow?.Title?.Contains("Manifest", StringComparison.OrdinalIgnoreCase) == true);

        // Wait for UI to stabilize
        steps.Run("Window ready", () =>
            WaitForTitleContains("Manifest", DefaultTimeout));

        // Menu bar verification
        steps.Run("File menu exists", () => FindMenu("File") != null);
        steps.Run("Edit menu exists", () => FindMenu("Edit") != null);
        steps.Run("Help menu exists", () => FindMenu("Help") != null);

        steps.AssertAllPassed();
    }
}

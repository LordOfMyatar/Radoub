using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Quartermaster;

/// <summary>
/// Smoke tests to verify Quartermaster launches and has expected UI elements.
/// Uses consolidated step-based testing for efficient diagnostics.
/// </summary>
[Collection("QuartermasterSequential")]
public class SmokeTests : QuartermasterTestBase
{
    /// <summary>
    /// Consolidated smoke test verifying app launch and core UI.
    /// Replaces 5 individual tests with diagnostic step tracking.
    /// </summary>
    [Fact]
    [Trait("Category", "Smoke")]
    public void Quartermaster_LaunchAndCoreUI()
    {
        var steps = new TestSteps();

        // Launch verification
        steps.Run("Application launches", () =>
        {
            StartApplication();
            return App != null && MainWindow != null;
        });

        steps.Run("Window title contains 'Quartermaster'", () =>
            MainWindow?.Title?.Contains("Quartermaster", StringComparison.OrdinalIgnoreCase) == true);

        // Wait for UI to stabilize
        steps.Run("Window ready", () =>
            WaitForTitleContains("Quartermaster", DefaultTimeout));

        // Menu bar verification
        steps.Run("File menu exists", () => FindMenu("File") != null);
        steps.Run("Edit menu exists", () => FindMenu("Edit") != null);
        steps.Run("Help menu exists", () => FindMenu("Help") != null);

        steps.AssertAllPassed();
    }
}

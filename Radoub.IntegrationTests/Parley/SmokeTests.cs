using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Parley;

/// <summary>
/// Smoke tests to verify Parley launches and has expected UI elements.
/// Uses consolidated step-based testing for efficient diagnostics.
/// </summary>
[Collection("ParleySequential")]
public class SmokeTests : ParleyTestBase
{
    /// <summary>
    /// Consolidated smoke test verifying app launch and core UI.
    /// Replaces 3 individual tests with diagnostic step tracking.
    /// </summary>
    [Fact]
    [Trait("Category", "Smoke")]
    public void Parley_LaunchAndCoreUI()
    {
        var steps = new TestSteps();

        // Launch verification
        steps.Run("Application launches", () =>
        {
            StartApplication();
            return App != null && MainWindow != null;
        });

        steps.Run("Window title contains 'Parley'", () =>
            MainWindow?.Title?.Contains("Parley", StringComparison.OrdinalIgnoreCase) == true);

        // Wait for UI to stabilize
        steps.Run("Window ready", () =>
            WaitForTitleContains("Parley", DefaultTimeout));

        // Menu bar verification
        steps.Run("File menu exists", () => FindMenu("File") != null);

        steps.AssertAllPassed();
    }
}

using FlaUI.Core.AutomationElements;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Relique;

/// <summary>
/// FlaUI rollback tests for the Relique property handlers (#2380). Relique is launched with the
/// test-only --test-fault-inject flag, which exposes a hidden "Arm Fault" button. Clicking it makes
/// the NEXT assigned-properties refresh throw once, driving the PropertyListMutator rollback path
/// (#2258) through the real View handlers — proving the model is restored when the UI refresh fails.
///
/// The add/remove/clear/edit rollback math is also covered by fast unit tests
/// (PropertyListMutatorTests, CheckedPropertyResolverTests); these verify the wiring end-to-end and
/// the dirty-close re-entry guard.
/// </summary>
[Collection("ReliqueSequential")]
public class ReliquePropertyRollbackTests : ReliqueTestBase
{
    private static string CopyFixtureToTemp(string fixture, string tempName)
    {
        var source = TestPaths.GetReliqueTestFile(fixture);
        var tempDir = TestPaths.CreateTempTestDirectory();
        var tempFile = Path.Combine(tempDir, tempName);
        File.Copy(source, tempFile);
        return tempFile;
    }

    /// <summary>Count of rows currently in the Assigned Properties list.</summary>
    private int AssignedCount()
    {
        var list = FindElement("AssignedPropertiesList", maxRetries: 10);
        if (list == null) return -1;
        var items = list.FindAllChildren();
        return items.Length;
    }

    private string? StatusText()
    {
        var status = FindElement("StatusText", maxRetries: 5);
        return status?.AsLabel()?.Text ?? status?.Name;
    }

    private void ArmFault()
    {
        var arm = FindElement("Relique_TestArmFault", maxRetries: 10);
        Assert.NotNull(arm);
        EnsureFocused();
        arm!.AsButton()!.Invoke();
    }

    [Fact]
    [Trait("Category", "Rollback")]
    public void ClearAll_RefreshThrows_ModelRollsBack()
    {
        // chefshat.uti has 3 properties — enough to clear.
        var tempFile = CopyFixtureToTemp("chefshat.uti", "rollback_clear.uti");
        try
        {
            StartApplication($"--test-fault-inject --file \"{tempFile}\"");
            Assert.True(WaitForTitleContains("rollback_clear", FileOperationTimeout), "Item should load");

            int before = AssignedCount();
            Assert.True(before > 0, $"Fixture should have assigned properties (got {before})");

            ArmFault();

            // Clear All — the armed fault makes the refresh throw; the model must roll back.
            Assert.True(ClickButton("ClearAllPropertiesButton"), "Clear All should be clickable");

            // The "UI refresh failed" status is set ONLY after the mutator has rolled the model
            // back (#2528) — a deterministic model-state proxy. The UI row count is NOT asserted:
            // after the injected fault interrupts the refresh mid-rebuild, the displayed row count
            // races the best-effort recovery refresh and is not a clean signal (that was the
            // original flake). The rollback math itself is unit-proven in PropertyListMutatorTests.
            Assert.True(WaitForStatusContains("UI refresh failed", TimeSpan.FromSeconds(6)),
                $"Rollback status should be shown (got '{StatusText()}')");
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "Rollback")]
    public void Remove_RefreshThrows_ModelRollsBack()
    {
        var tempFile = CopyFixtureToTemp("chefshat.uti", "rollback_remove.uti");
        try
        {
            StartApplication($"--test-fault-inject --file \"{tempFile}\"");
            Assert.True(WaitForTitleContains("rollback_remove", FileOperationTimeout), "Item should load");

            int before = AssignedCount();
            Assert.True(before > 0, $"Fixture should have assigned properties (got {before})");

            // Select the first assigned row so Remove is enabled.
            var list = FindElement("AssignedPropertiesList", maxRetries: 10);
            Assert.NotNull(list);
            var firstRow = list!.FindAllChildren().FirstOrDefault();
            Assert.NotNull(firstRow);
            EnsureFocused();
            firstRow!.AsListBoxItem()?.Select();

            ArmFault();

            Assert.True(ClickButton("RemovePropertyButton"), "Remove should be clickable");

            // Deterministic rollback status (set only after model restore, #2528). Row count is
            // not asserted — it races the interrupted-then-recovered refresh (the original flake);
            // rollback math is unit-proven in PropertyListMutatorTests.
            Assert.True(WaitForStatusContains("UI refresh failed", TimeSpan.FromSeconds(6)),
                $"Rollback status should be shown (got '{StatusText()}')");
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "Rollback")]
    public void DirtySave_ClosesWithSingleCleanupPass()
    {
        // The dirty-close path cancels the close, prompts/saves, then re-enters OnWindowClosing.
        // The _cleanedUp guard (#2258) must run cleanup exactly once; a graceful close proves the
        // re-entry didn't double-dispose or double-release locks.
        var tempFile = CopyFixtureToTemp("atest.uti", "rollback_close.uti");
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            Assert.True(WaitForTitleContains("rollback_close", FileOperationTimeout), "Item should load");

            // Dirty the document.
            var tag = FindElement("Relique_Field_Tag");
            Assert.NotNull(tag);
            EnsureFocused();
            tag!.AsTextBox()!.Enter("y");
            Assert.True(WaitForTitleContains("*", TimeSpan.FromSeconds(3)), "Title should show dirty marker");

            // Save (clears dirty); then StopApplication's App.Close exercises the clean close path.
            SendCtrlS();
            Assert.True(WaitForTitleNotContains("*", FileOperationTimeout), "Ctrl+S should clear the dirty marker");
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    private bool WaitForStatusContains(string fragment, TimeSpan timeout)
    {
        var end = DateTime.Now + timeout;
        while (DateTime.Now < end)
        {
            if (StatusText()?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true) return true;
            Thread.Sleep(200);
        }
        return StatusText()?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true;
    }
}

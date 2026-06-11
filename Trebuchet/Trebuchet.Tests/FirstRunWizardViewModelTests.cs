using System.Linq;
using RadoubLauncher.Services;
using RadoubLauncher.ViewModels;
using Xunit;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for the first-run wizard navigation and summary behavior (#1020).
/// These exercise the crash fixed after the backup-slider report: changing a
/// value must not rebuild the summary collection on every tick, and summary rows
/// must carry their own jump command (no fragile $parent-cast XAML binding).
///
/// The view model is constructed headless (no Window, no ThemeManager) via the
/// test constructor so navigation/summary logic is unit-testable without FlaUI.
/// </summary>
public class FirstRunWizardViewModelTests
{
    private static FirstRunWizardViewModel CreateHeadless() =>
        FirstRunWizardViewModel.CreateForTest(WizardMode.Welcome);

    [Fact]
    public void ChangingBackupRetention_DoesNotRebuildSummaryRows()
    {
        var vm = CreateHeadless();
        vm.StepIndex = vm.LastStepIndex; // show summary → builds rows once
        var rowsBefore = vm.SummaryRows.ToList();

        vm.BackupRetentionDays = 45;

        // The collection instance contents should not have been cleared/rebuilt by
        // the value change itself (that thrash + the summary's live bindings was the
        // crash). The displayed value updates when the summary is (re)entered.
        Assert.Same(rowsBefore[0], vm.SummaryRows[0]);
    }

    [Fact]
    public void EnteringSummaryStep_RefreshesValues()
    {
        var vm = CreateHeadless();
        vm.BackupRetentionDays = 60;

        vm.StepIndex = vm.LastStepIndex; // enter summary

        var backupRow = vm.SummaryRows.Single(r => r.Label == "Backup retention");
        Assert.Contains("60", backupRow.Value);
    }

    [Fact]
    public void SummaryRow_JumpCommand_NavigatesToItsStep()
    {
        var vm = CreateHeadless();
        vm.StepIndex = vm.LastStepIndex;

        var gameRow = vm.SummaryRows.Single(r => r.Label == "Game path");
        gameRow.Jump.Execute(null);

        Assert.Equal(gameRow.StepIndex, vm.StepIndex);
    }

    [Fact]
    public void BackupRetentionDays_ClampsToValidRange()
    {
        var vm = CreateHeadless();

        vm.BackupRetentionDays = 999;
        Assert.True(vm.BackupRetentionDays <= 90);

        vm.BackupRetentionDays = 0;
        Assert.True(vm.BackupRetentionDays >= 1);
    }
}

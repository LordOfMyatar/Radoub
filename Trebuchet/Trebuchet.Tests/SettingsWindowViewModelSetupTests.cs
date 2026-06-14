using System;
using System.IO;
using Radoub.Formats.Settings;
using Radoub.TestUtilities.Helpers;
using RadoubLauncher.Services;
using RadoubLauncher.ViewModels;
using Xunit;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for the tabbed Settings window acting as the first-run / version-gate setup
/// flow (#2419). The view model is constructed headless (no Window, no ThemeManager)
/// via <see cref="SettingsWindowViewModel.CreateForTest"/> so setup-mode flags,
/// banner/visibility, and Save/Cancel persistence are unit-testable without FlaUI.
///
/// Mutates the RadoubSettings + SettingsService singletons; both are reset to isolated
/// temp directories per test so state does not leak. Parallelization is disabled
/// assembly-wide (TestCollections.cs).
/// </summary>
public class SettingsWindowViewModelSetupTests : IDisposable
{
    private const string TestVersion = "1.40.0-alpha";
    private readonly string _radoubDir;
    private readonly string _trebuchetDir;

    public SettingsWindowViewModelSetupTests()
    {
        _radoubDir = Path.Combine(Path.GetTempPath(), $"RadoubTest_{Guid.NewGuid():N}");
        _trebuchetDir = Path.Combine(Path.GetTempPath(), $"TrebuchetTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_radoubDir);
        Directory.CreateDirectory(_trebuchetDir);

        SingletonTestHelper.ResetSingleton<RadoubSettings>("_instance", "_settingsDirectory");
        SingletonTestHelper.ConfigureSettingsDirectory("RADOUB_SETTINGS_DIR", _radoubDir);
        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("TREBUCHET_SETTINGS_DIR", _trebuchetDir);
    }

    public void Dispose()
    {
        SingletonTestHelper.ResetSingleton<RadoubSettings>("_instance", "_settingsDirectory");
        SingletonTestHelper.ConfigureSettingsDirectory("RADOUB_SETTINGS_DIR", null);
        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("TREBUCHET_SETTINGS_DIR", null);

        foreach (var dir in new[] { _radoubDir, _trebuchetDir })
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { /* best-effort cleanup */ }
        }
    }

    private static SettingsWindowViewModel Setup(SettingsSetupMode mode) =>
        SettingsWindowViewModel.CreateForTest(mode, TestVersion);

    [Fact]
    public void NormalMode_IsNotSetup_NoBannerOrModulePicker()
    {
        var vm = Setup(SettingsSetupMode.Normal);

        Assert.False(vm.IsSetupMode);
        Assert.False(vm.IsWelcome);
        Assert.False(vm.ShowModulePicker);
    }

    [Fact]
    public void WelcomeMode_IsSetup_AndWelcome_ShowsModulePicker()
    {
        var vm = Setup(SettingsSetupMode.Welcome);

        Assert.True(vm.IsSetupMode);
        Assert.True(vm.IsWelcome);
        Assert.True(vm.ShowModulePicker);
        Assert.False(string.IsNullOrEmpty(vm.BannerText));
    }

    [Fact]
    public void WelcomeBackMode_IsSetup_NotWelcome_HidesModulePicker()
    {
        var vm = Setup(SettingsSetupMode.WelcomeBack);

        Assert.True(vm.IsSetupMode);
        Assert.False(vm.IsWelcome);
        Assert.False(vm.ShowModulePicker);
        Assert.NotEqual(Setup(SettingsSetupMode.Welcome).BannerText, vm.BannerText);
    }

    [Fact]
    public void Next_AdvancesTab_AndStopsAtLast()
    {
        var vm = Setup(SettingsSetupMode.Welcome);
        Assert.Equal(0, vm.SelectedTabIndex);

        for (int i = 0; i < 20; i++)
            vm.NextCommand.Execute(null);

        Assert.Equal(vm.LastTabIndex, vm.SelectedTabIndex);
    }

    [Fact]
    public void Save_InSetupMode_StampsWizardHasRunAndLastSetupVersion()
    {
        var vm = Setup(SettingsSetupMode.Welcome);

        vm.SaveCommand.Execute(null);

        var settings = RadoubSettings.Instance;
        Assert.True(settings.WizardHasRun);
        Assert.Equal(TestVersion, settings.LastSetupVersion);
    }

    [Fact]
    public void Cancel_InSetupMode_AlsoStampsWizardHasRunAndLastSetupVersion()
    {
        // B1: dismissing first-run still marks it complete for this version so we
        // do not re-nag every launch (the missing-game-path guard is separate).
        var vm = Setup(SettingsSetupMode.Welcome);

        vm.CancelCommand.Execute(null);

        var settings = RadoubSettings.Instance;
        Assert.True(settings.WizardHasRun);
        Assert.Equal(TestVersion, settings.LastSetupVersion);
    }

    [Fact]
    public void Save_InNormalMode_DoesNotStampSetupState()
    {
        var vm = Setup(SettingsSetupMode.Normal);

        vm.SaveCommand.Execute(null);

        var settings = RadoubSettings.Instance;
        Assert.False(settings.WizardHasRun);
        Assert.True(string.IsNullOrEmpty(settings.LastSetupVersion));
    }

    [Fact]
    public void Save_InWelcome_WithChosenModule_SetsCurrentModulePath()
    {
        var vm = Setup(SettingsSetupMode.Welcome);
        vm.SetChosenModuleForTest(@"C:\mods\demo.mod");

        vm.SaveCommand.Execute(null);

        Assert.Equal(@"C:\mods\demo.mod", RadoubSettings.Instance.CurrentModulePath);
    }

    [Fact]
    public void EnteringReviewTab_PopulatesSummaryRows()
    {
        var vm = Setup(SettingsSetupMode.Welcome);
        Assert.Empty(vm.SummaryRows);

        vm.SelectedTabIndex = vm.LastTabIndex; // Review tab

        Assert.NotEmpty(vm.SummaryRows);
        Assert.Contains(vm.SummaryRows, r => r.Label == "Log level");
        Assert.Contains(vm.SummaryRows, r => r.Label == "Theme");
    }

    [Fact]
    public void SummaryRow_JumpCommand_NavigatesToItsTab()
    {
        var vm = Setup(SettingsSetupMode.Welcome);
        vm.SelectedTabIndex = vm.LastTabIndex;

        var logRow = vm.SummaryRows.Single(r => r.Label == "Log level");
        logRow.Jump.Execute(null);

        Assert.Equal(logRow.TabIndex, vm.SelectedTabIndex);
    }

    [Fact]
    public void Save_WithoutThemeChange_DoesNotChangeSharedThemeId()
    {
        // Honor the user's existing theme: an auto-opened setup window the user never
        // touched must not rewrite SharedThemeId (the Light-over-Dark clobber, #2419).
        var before = RadoubSettings.Instance.SharedThemeId;
        var vm = Setup(SettingsSetupMode.Welcome);

        vm.SaveCommand.Execute(null);

        Assert.Equal(before, RadoubSettings.Instance.SharedThemeId);
    }

    [Fact]
    public void Save_InWelcome_NoModuleChosen_DoesNotChangeCurrentModulePath()
    {
        // RadoubSettings may auto-detect a module path on first construction, so we
        // assert Save with no chosen module leaves whatever was there untouched —
        // not that it is empty.
        var before = RadoubSettings.Instance.CurrentModulePath;
        var vm = Setup(SettingsSetupMode.Welcome);

        vm.SaveCommand.Execute(null);

        Assert.Equal(before, RadoubSettings.Instance.CurrentModulePath);
    }
}

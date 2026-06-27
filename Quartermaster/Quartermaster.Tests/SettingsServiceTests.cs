using Quartermaster.Services;
using Radoub.TestUtilities.Bases;
using Radoub.TestUtilities.Helpers;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for Quartermaster's SettingsService. The shared singleton/window/recent-files/
/// log-retention contract lives in <see cref="ToolSettingsServiceTestBase{TService}"/> (#2464);
/// only Quartermaster-specific behavior (panel width, ValidationLevel) is below.
/// </summary>
public class SettingsServiceTests : ToolSettingsServiceTestBase<SettingsService>
{
    protected override string SettingsEnvironmentVariable => "QUARTERMASTER_SETTINGS_DIR";
    protected override string ToolDirPrefix => "Quartermaster";
    protected override string RecentFileExtension => ".utc";

    protected override SettingsService GetInstance() => SettingsService.Instance;
    protected override void ResetSingleton() => SingletonTestHelper.ResetSingleton<SettingsService>();

    protected override double GetWindowWidth(SettingsService s) => s.WindowWidth;
    protected override void SetWindowWidth(SettingsService s, double v) => s.WindowWidth = v;
    protected override double GetWindowHeight(SettingsService s) => s.WindowHeight;
    protected override void SetWindowHeight(SettingsService s, double v) => s.WindowHeight = v;
    protected override IReadOnlyList<string> GetRecentFiles(SettingsService s) => s.RecentFiles;
    protected override void AddRecentFile(SettingsService s, string path) => s.AddRecentFile(path);
    protected override int GetMaxRecentFiles(SettingsService s) => s.MaxRecentFiles;
    protected override void SetMaxRecentFiles(SettingsService s, int v) => s.MaxRecentFiles = v;
    protected override int GetLogRetentionSessions(SettingsService s) => s.LogRetentionSessions;
    protected override void SetLogRetentionSessions(SettingsService s, int v) => s.LogRetentionSessions = v;

    // ---- Quartermaster-specific ----

    [Fact]
    public void SharedSettings_ReturnsRadoubSettings()
    {
        Assert.NotNull(SettingsService.SharedSettings);
    }

    [Fact]
    public void LeftPanelWidth_EnforcesRange()
    {
        var service = GetInstance();

        service.LeftPanelWidth = 100;
        Assert.True(service.LeftPanelWidth >= 200);

        service.LeftPanelWidth = 800;
        Assert.True(service.LeftPanelWidth <= 600);
    }

    [Fact]
    public void ValidationLevel_LegacyWarningValue_MigratesToNone()
    {
        // #1882: The old Warning=1 tier was removed. Persisted settings with
        // Warning (1) must migrate to None since TN allowed everything CE allows.
        var settingsFile = Path.Combine(TestSettingsDir, "QuartermasterSettings.json");
        File.WriteAllText(settingsFile,
            "{ \"ValidationLevel\": 1, \"WindowWidth\": 1024, \"WindowHeight\": 768 }");

        ResetSingleton();
        var service = GetInstance();

        Assert.Equal(ValidationLevel.None, service.ValidationLevel);
    }

    [Fact]
    public void ValidationLevel_DefaultValue_IsNone()
    {
        // #1882: Default is now None (CE) — permissive, matches prior TN default intent
        ResetSingleton();
        var service = GetInstance();

        Assert.Equal(ValidationLevel.None, service.ValidationLevel);
    }

    [Fact]
    public void ValidationLevel_StrictValue_PreservedAcrossReload()
    {
        var service = GetInstance();
        service.ValidationLevel = ValidationLevel.Strict;

        ResetSingleton();
        var reloaded = GetInstance();

        Assert.Equal(ValidationLevel.Strict, reloaded.ValidationLevel);
    }

    [Fact]
    public void ValidationLevelComboBoxMap_ToIndex_NoneMapsTo0()
    {
        Assert.Equal(0, ValidationLevelComboBoxMap.ToComboBoxIndex(ValidationLevel.None));
    }

    [Fact]
    public void ValidationLevelComboBoxMap_ToIndex_StrictMapsTo1()
    {
        // Strict is enum value 2, but occupies index 1 in the two-item ComboBox
        Assert.Equal(1, ValidationLevelComboBoxMap.ToComboBoxIndex(ValidationLevel.Strict));
    }

    [Fact]
    public void ValidationLevelComboBoxMap_FromIndex_0MapsToNone()
    {
        Assert.Equal(ValidationLevel.None, ValidationLevelComboBoxMap.FromComboBoxIndex(0));
    }

    [Fact]
    public void ValidationLevelComboBoxMap_FromIndex_1MapsToStrict()
    {
        Assert.Equal(ValidationLevel.Strict, ValidationLevelComboBoxMap.FromComboBoxIndex(1));
    }
}

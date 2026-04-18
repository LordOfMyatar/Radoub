using Quartermaster.Services;
using Radoub.TestUtilities.Helpers;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for SettingsService.
/// Uses isolated temp directory to avoid corrupting user settings.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly string _testSettingsDir;

    public SettingsServiceTests()
    {
        // Create isolated temp directory for each test run
        _testSettingsDir = Path.Combine(Path.GetTempPath(), $"Quartermaster_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testSettingsDir);

        // Reset singleton and configure via environment variable
        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("QUARTERMASTER_SETTINGS_DIR", _testSettingsDir);
    }

    public void Dispose()
    {
        // Reset singleton and clear env var
        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("QUARTERMASTER_SETTINGS_DIR", null);

        // Clean up temp directory
        try
        {
            if (Directory.Exists(_testSettingsDir))
                Directory.Delete(_testSettingsDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup failures
        }
    }

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        var instance1 = SettingsService.Instance;
        var instance2 = SettingsService.Instance;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void SharedSettings_ReturnsRadoubSettings()
    {
        var sharedSettings = SettingsService.SharedSettings;

        Assert.NotNull(sharedSettings);
    }

    [Fact]
    public void WindowWidth_EnforcesMinimum()
    {
        var service = SettingsService.Instance;

        // WindowWidth should not go below 600
        service.WindowWidth = 100;

        Assert.True(service.WindowWidth >= 600);
    }

    [Fact]
    public void WindowHeight_EnforcesMinimum()
    {
        var service = SettingsService.Instance;

        // WindowHeight should not go below 400
        service.WindowHeight = 100;

        Assert.True(service.WindowHeight >= 400);
    }

    [Fact]
    public void LeftPanelWidth_EnforcesRange()
    {
        var service = SettingsService.Instance;

        // LeftPanelWidth should be between 200 and 600
        service.LeftPanelWidth = 100;
        Assert.True(service.LeftPanelWidth >= 200);

        service.LeftPanelWidth = 800;
        Assert.True(service.LeftPanelWidth <= 600);
    }

    [Fact]
    public void MaxRecentFiles_EnforcesRange()
    {
        var service = SettingsService.Instance;

        // MaxRecentFiles should be between 1 and 20
        service.MaxRecentFiles = 0;
        Assert.True(service.MaxRecentFiles >= 1);

        service.MaxRecentFiles = 100;
        Assert.True(service.MaxRecentFiles <= 20);
    }

    [Fact]
    public void RecentFiles_ReturnsListCopy()
    {
        var service = SettingsService.Instance;

        var list1 = service.RecentFiles;
        var list2 = service.RecentFiles;

        // Each call should return a new list (defensive copy)
        Assert.NotSame(list1, list2);
    }

    [Fact]
    public void LogRetentionSessions_EnforcesRange()
    {
        var service = SettingsService.Instance;

        // LogRetentionSessions should be between 1 and 10
        service.LogRetentionSessions = 0;
        Assert.True(service.LogRetentionSessions >= 1);

        service.LogRetentionSessions = 100;
        Assert.True(service.LogRetentionSessions <= 10);
    }

    [Fact]
    public void AddRecentFile_AddsToList()
    {
        var service = SettingsService.Instance;

        // Create a temp file that exists
        var tempFile = Path.Combine(_testSettingsDir, "test.utc");
        File.WriteAllText(tempFile, "test");

        service.AddRecentFile(tempFile);

        Assert.Contains(tempFile, service.RecentFiles);
    }

    [Fact]
    public void Settings_PersistAcrossReload()
    {
        // Set some values
        var service = SettingsService.Instance;
        service.WindowWidth = 1000;

        // Reset and reload
        SingletonTestHelper.ResetSingleton<SettingsService>();

        var reloaded = SettingsService.Instance;

        Assert.Equal(1000, reloaded.WindowWidth);
    }

    [Fact]
    public void ValidationLevel_LegacyWarningValue_MigratesToNone()
    {
        // #1882: The old Warning=1 tier was removed. Persisted settings with
        // Warning (1) must migrate to None since TN allowed everything CE allows.
        var settingsFile = Path.Combine(_testSettingsDir, "QuartermasterSettings.json");
        File.WriteAllText(settingsFile,
            "{ \"ValidationLevel\": 1, \"WindowWidth\": 1024, \"WindowHeight\": 768 }");

        SingletonTestHelper.ResetSingleton<SettingsService>();
        var service = SettingsService.Instance;

        Assert.Equal(ValidationLevel.None, service.ValidationLevel);
    }

    [Fact]
    public void ValidationLevel_DefaultValue_IsNone()
    {
        // #1882: Default is now None (CE) — permissive, matches prior TN default intent
        SingletonTestHelper.ResetSingleton<SettingsService>();
        var service = SettingsService.Instance;

        Assert.Equal(ValidationLevel.None, service.ValidationLevel);
    }

    [Fact]
    public void ValidationLevel_StrictValue_PreservedAcrossReload()
    {
        var service = SettingsService.Instance;
        service.ValidationLevel = ValidationLevel.Strict;

        SingletonTestHelper.ResetSingleton<SettingsService>();
        var reloaded = SettingsService.Instance;

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

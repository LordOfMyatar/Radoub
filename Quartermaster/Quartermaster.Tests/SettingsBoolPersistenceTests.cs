using Quartermaster.Services;
using Radoub.TestUtilities.Helpers;

namespace Quartermaster.Tests;

/// <summary>
/// Round-trip persistence coverage for Quartermaster SettingsService bool flags (#2361).
///
/// CreatureBrowserPanelVisible and RecordLevelHistory both default true and had no
/// save→reload→assert test. ResetSingleton between writer and reader forces a fresh
/// load from the isolated JSON file, so each test brackets a real persistence cycle.
/// </summary>
public class SettingsBoolPersistenceTests : IDisposable
{
    private readonly string _testSettingsDir;

    public SettingsBoolPersistenceTests()
    {
        _testSettingsDir = Path.Combine(Path.GetTempPath(), $"Quartermaster_BoolTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testSettingsDir);

        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("QUARTERMASTER_SETTINGS_DIR", _testSettingsDir);
    }

    public void Dispose()
    {
        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("QUARTERMASTER_SETTINGS_DIR", null);
        try
        {
            if (Directory.Exists(_testSettingsDir))
                Directory.Delete(_testSettingsDir, recursive: true);
        }
        catch { /* best-effort cleanup */ }
    }

    [Fact]
    public void CreatureBrowserPanelVisible_RoundTrips()
    {
        SettingsService.Instance.CreatureBrowserPanelVisible = false;

        SingletonTestHelper.ResetSingleton<SettingsService>();

        Assert.False(SettingsService.Instance.CreatureBrowserPanelVisible);
    }

    [Fact]
    public void RecordLevelHistory_RoundTrips()
    {
        SettingsService.Instance.RecordLevelHistory = false;

        SingletonTestHelper.ResetSingleton<SettingsService>();

        Assert.False(SettingsService.Instance.RecordLevelHistory);
    }

    [Fact]
    public void CreatureBrowserPanelVisible_DefaultsTrue()
    {
        Assert.True(SettingsService.Instance.CreatureBrowserPanelVisible);
    }

    [Fact]
    public void RecordLevelHistory_DefaultsTrue()
    {
        Assert.True(SettingsService.Instance.RecordLevelHistory);
    }
}

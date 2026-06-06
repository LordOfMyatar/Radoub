using RadoubLauncher.Services;
using Radoub.TestUtilities.Helpers;

namespace Trebuchet.Tests;

/// <summary>
/// Round-trip persistence coverage for Trebuchet SettingsService build-flag bools (#2361).
///
/// CompileScriptsEnabled had a default + can-enable test but no persistence cycle;
/// BuildUncompiledScriptsEnabled and AlwaysSaveBeforeTesting had none. All three
/// default false. ResetSingleton between writer and reader forces a fresh load from
/// the isolated JSON file, so each test brackets a real save→reload cycle.
/// </summary>
public class SettingsBoolPersistenceTests : IDisposable
{
    private readonly string _testDirectory;

    public SettingsBoolPersistenceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Trebuchet_BoolTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("TREBUCHET_SETTINGS_DIR", _testDirectory);
    }

    public void Dispose()
    {
        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("TREBUCHET_SETTINGS_DIR", null);
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }
        catch { /* best-effort cleanup */ }
    }

    [Fact]
    public void CompileScriptsEnabled_RoundTrips()
    {
        SettingsService.Instance.CompileScriptsEnabled = true;

        SingletonTestHelper.ResetSingleton<SettingsService>();

        Assert.True(SettingsService.Instance.CompileScriptsEnabled);
    }

    [Fact]
    public void BuildUncompiledScriptsEnabled_RoundTrips()
    {
        SettingsService.Instance.BuildUncompiledScriptsEnabled = true;

        SingletonTestHelper.ResetSingleton<SettingsService>();

        Assert.True(SettingsService.Instance.BuildUncompiledScriptsEnabled);
    }

    [Fact]
    public void AlwaysSaveBeforeTesting_RoundTrips()
    {
        SettingsService.Instance.AlwaysSaveBeforeTesting = true;

        SingletonTestHelper.ResetSingleton<SettingsService>();

        Assert.True(SettingsService.Instance.AlwaysSaveBeforeTesting);
    }

    [Fact]
    public void BuildFlags_DefaultFalse()
    {
        var settings = SettingsService.Instance;
        Assert.False(settings.CompileScriptsEnabled);
        Assert.False(settings.BuildUncompiledScriptsEnabled);
        Assert.False(settings.AlwaysSaveBeforeTesting);
    }
}

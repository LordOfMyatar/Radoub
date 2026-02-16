using Radoub.Formats.Settings;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for RadoubSettings persistence and cross-process propagation (#1384).
/// Verifies that settings written by one process (Trebuchet) can be read by another (child tools).
/// </summary>
public class RadoubSettingsTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _fakeTlkPath;
    private readonly string _fakeModulePath;

    public RadoubSettingsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"RadoubSettingsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        // Build privacy-safe test paths under temp
        _fakeTlkPath = Path.Combine(_testDirectory, "tlk", "custom.tlk");
        _fakeModulePath = Path.Combine(_testDirectory, "modules", "mymodule");

        RadoubSettings.ResetForTesting();
        RadoubSettings.ConfigureForTesting(_testDirectory);
    }

    public void Dispose()
    {
        RadoubSettings.ResetForTesting();

        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch { /* Ignore cleanup errors */ }
    }

    [Fact]
    public void CustomTlkPath_PersistsToFile()
    {
        var settings = RadoubSettings.Instance;
        settings.CustomTlkPath = _fakeTlkPath;

        // Verify the JSON file was written
        var filePath = Path.Combine(_testDirectory, "RadoubSettings.json");
        Assert.True(File.Exists(filePath));

        var json = File.ReadAllText(filePath);
        Assert.Contains("CustomTlkPath", json);
    }

    [Fact]
    public void CurrentModulePath_PersistsToFile()
    {
        var settings = RadoubSettings.Instance;
        settings.CurrentModulePath = _fakeModulePath;

        var filePath = Path.Combine(_testDirectory, "RadoubSettings.json");
        Assert.True(File.Exists(filePath));

        var json = File.ReadAllText(filePath);
        Assert.Contains("CurrentModulePath", json);
    }

    [Fact]
    public void ReloadSettings_ReadsUpdatedValues()
    {
        var originalTlk = Path.Combine(_testDirectory, "original", "path.tlk");
        var originalModule = Path.Combine(_testDirectory, "original", "module");
        var updatedTlk = Path.Combine(_testDirectory, "updated", "custom.tlk");
        var updatedModule = Path.Combine(_testDirectory, "updated", "module");

        var settings = RadoubSettings.Instance;
        settings.CustomTlkPath = originalTlk;
        settings.CurrentModulePath = originalModule;

        Assert.Equal(originalTlk, settings.CustomTlkPath);
        Assert.Equal(originalModule, settings.CurrentModulePath);

        // Simulate another process writing to the settings file
        var filePath = Path.Combine(_testDirectory, "RadoubSettings.json");
        var json = File.ReadAllText(filePath);
        json = json.Replace(originalTlk.Replace("\\", "\\\\"), updatedTlk.Replace("\\", "\\\\"));
        json = json.Replace(originalModule.Replace("\\", "\\\\"), updatedModule.Replace("\\", "\\\\"));
        File.WriteAllText(filePath, json);

        // Reload should pick up the new values
        settings.ReloadSettings();

        Assert.Equal(updatedTlk, settings.CustomTlkPath);
        Assert.Equal(updatedModule, settings.CurrentModulePath);
    }

    [Fact]
    public void Settings_SurviveNewInstance()
    {
        // First instance writes settings
        var settings1 = RadoubSettings.Instance;
        settings1.CustomTlkPath = _fakeTlkPath;
        settings1.CurrentModulePath = _fakeModulePath;

        // Reset and create a new instance (simulates child process startup)
        RadoubSettings.ResetForTesting();
        RadoubSettings.ConfigureForTesting(_testDirectory);

        var settings2 = RadoubSettings.Instance;

        // New instance should have the same values from disk
        Assert.Equal(_fakeTlkPath, settings2.CustomTlkPath);
        Assert.Equal(_fakeModulePath, settings2.CurrentModulePath);
    }

    [Fact]
    public void Settings_PathContraction_RoundTrips()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var fullPath = Path.Combine(userHome, "NWN", "tlk", "custom.tlk");

        var settings1 = RadoubSettings.Instance;
        settings1.CustomTlkPath = fullPath;

        // The file should contain contracted path (~)
        var filePath = Path.Combine(_testDirectory, "RadoubSettings.json");
        var json = File.ReadAllText(filePath);
        Assert.Contains("~", json);

        // Reset and reload - should expand back to full path
        RadoubSettings.ResetForTesting();
        RadoubSettings.ConfigureForTesting(_testDirectory);

        var settings2 = RadoubSettings.Instance;
        Assert.Equal(fullPath, settings2.CustomTlkPath);
    }

    [Fact]
    public void EmptyCustomTlkPath_PersistsCorrectly()
    {
        var settings = RadoubSettings.Instance;
        settings.CustomTlkPath = _fakeTlkPath;
        settings.CustomTlkPath = "";

        // Reset and reload
        RadoubSettings.ResetForTesting();
        RadoubSettings.ConfigureForTesting(_testDirectory);

        var settings2 = RadoubSettings.Instance;
        Assert.Equal("", settings2.CustomTlkPath);
    }

    [Fact]
    public void LoadSettings_HandlesCorruptJson()
    {
        // Write corrupt JSON
        var filePath = Path.Combine(_testDirectory, "RadoubSettings.json");
        File.WriteAllText(filePath, "{ this is not valid json }}}");

        // Should not throw, just use defaults
        RadoubSettings.ResetForTesting();
        RadoubSettings.ConfigureForTesting(_testDirectory);

        var settings = RadoubSettings.Instance;
        // CustomTlkPath is never auto-detected, so it should be empty
        Assert.Equal("", settings.CustomTlkPath);
        // CurrentModulePath may be auto-detected if NWN is installed, so just verify no crash
        Assert.NotNull(settings.CurrentModulePath);
    }

    [Fact]
    public void LoadSettings_HandlesMissingFile()
    {
        // No settings file exists - should not throw
        var settings = RadoubSettings.Instance;
        // CustomTlkPath is never auto-detected, so it should be empty
        Assert.Equal("", settings.CustomTlkPath);
        // CurrentModulePath may be auto-detected if NWN is installed
        Assert.NotNull(settings.CurrentModulePath);
    }

    [Fact]
    public void NullCustomTlkPath_TreatedAsEmpty()
    {
        var settings = RadoubSettings.Instance;
        settings.CustomTlkPath = null!;

        Assert.Equal("", settings.CustomTlkPath);
    }

    [Fact]
    public void NullCurrentModulePath_TreatedAsEmpty()
    {
        var settings = RadoubSettings.Instance;
        settings.CurrentModulePath = null!;

        Assert.Equal("", settings.CurrentModulePath);
    }
}

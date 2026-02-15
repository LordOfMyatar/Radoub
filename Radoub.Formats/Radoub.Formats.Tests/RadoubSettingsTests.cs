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

    public RadoubSettingsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"RadoubSettingsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

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
        settings.CustomTlkPath = "/home/user/NWN/tlk/custom.tlk";

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
        settings.CurrentModulePath = "/home/user/NWN/modules/mymodule";

        var filePath = Path.Combine(_testDirectory, "RadoubSettings.json");
        Assert.True(File.Exists(filePath));

        var json = File.ReadAllText(filePath);
        Assert.Contains("CurrentModulePath", json);
    }

    [Fact]
    public void ReloadSettings_ReadsUpdatedValues()
    {
        var settings = RadoubSettings.Instance;
        settings.CustomTlkPath = "/original/path.tlk";
        settings.CurrentModulePath = "/original/module";

        Assert.Equal("/original/path.tlk", settings.CustomTlkPath);
        Assert.Equal("/original/module", settings.CurrentModulePath);

        // Simulate another process writing to the settings file
        var filePath = Path.Combine(_testDirectory, "RadoubSettings.json");
        var json = File.ReadAllText(filePath);
        json = json.Replace("/original/path.tlk", "/updated/custom.tlk");
        json = json.Replace("/original/module", "/updated/module");
        File.WriteAllText(filePath, json);

        // Reload should pick up the new values
        settings.ReloadSettings();

        Assert.Equal("/updated/custom.tlk", settings.CustomTlkPath);
        Assert.Equal("/updated/module", settings.CurrentModulePath);
    }

    [Fact]
    public void Settings_SurviveNewInstance()
    {
        // First instance writes settings
        var settings1 = RadoubSettings.Instance;
        settings1.CustomTlkPath = "/home/user/tlk/test.tlk";
        settings1.CurrentModulePath = "/home/user/modules/test_module";

        // Reset and create a new instance (simulates child process startup)
        RadoubSettings.ResetForTesting();
        RadoubSettings.ConfigureForTesting(_testDirectory);

        var settings2 = RadoubSettings.Instance;

        // New instance should have the same values from disk
        Assert.Equal("/home/user/tlk/test.tlk", settings2.CustomTlkPath);
        Assert.Equal("/home/user/modules/test_module", settings2.CurrentModulePath);
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
        settings.CustomTlkPath = "/some/path.tlk";
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

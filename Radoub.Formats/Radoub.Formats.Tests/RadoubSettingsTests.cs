using System.Text.Json;
using Radoub.Formats.Common;
using Radoub.Formats.Settings;
using Radoub.Formats.Tests.Settings;
using Radoub.TestUtilities.Helpers;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for RadoubSettings persistence and cross-process propagation (#1384).
/// Verifies that settings written by one process (Trebuchet) can be read by another (child tools).
///
/// Singleton lifecycle (env var + temp dir + static reset) is owned by
/// <see cref="RadoubSettingsFixture"/> via the "RadoubSettings" collection.
/// Tests that need to re-bind the singleton mid-class (simulating a child
/// process startup) call <see cref="ResetAndConfigure"/>.
/// </summary>
[Collection("RadoubSettings")]
public class RadoubSettingsTests
{
    private readonly RadoubSettingsFixture _fixture;
    private readonly string _fakeTlkPath;
    private readonly string _fakeModulePath;

    public RadoubSettingsTests(RadoubSettingsFixture fixture)
    {
        _fixture = fixture;
        _fakeTlkPath = Path.Combine(_fixture.TestDirectory, "tlk", "custom.tlk");
        _fakeModulePath = Path.Combine(_fixture.TestDirectory, "modules", "mymodule");

        // The fixture is shared across the class, so prior tests may have written
        // RadoubSettings.json into the same temp directory. Delete the persisted
        // file at the start of each test so each fact gets the same "fresh process"
        // semantics the original per-test temp-dir design provided. ResetAndConfigure
        // (called mid-test by some facts) intentionally does NOT delete the file —
        // those facts depend on file contents surviving a singleton reset.
        var persistedFile = Path.Combine(_fixture.TestDirectory, "RadoubSettings.json");
        if (File.Exists(persistedFile))
        {
            try { File.Delete(persistedFile); }
            catch { /* best-effort */ }
        }
        ResetAndConfigure();
    }

    /// <summary>
    /// Reset the RadoubSettings singleton and rebind the env var to the fixture's
    /// test directory. Does NOT delete the persisted JSON file — tests that simulate
    /// a child-process restart need the file contents to survive the singleton reset.
    /// </summary>
    private void ResetAndConfigure()
    {
        SingletonTestHelper.ResetSingleton<RadoubSettings>("_instance", "_settingsDirectory");
        SingletonTestHelper.ConfigureSettingsDirectory("RADOUB_SETTINGS_DIR", _fixture.TestDirectory);
    }

    [Fact]
    public void CustomTlkPath_PersistsToFile()
    {
        var settings = RadoubSettings.Instance;
        settings.CustomTlkPath = _fakeTlkPath;

        // Verify the JSON file was written
        var filePath = Path.Combine(_fixture.TestDirectory, "RadoubSettings.json");
        Assert.True(File.Exists(filePath));

        var json = File.ReadAllText(filePath);
        Assert.Contains("CustomTlkPath", json);
    }

    [Fact]
    public void CurrentModulePath_PersistsToFile()
    {
        var settings = RadoubSettings.Instance;
        settings.CurrentModulePath = _fakeModulePath;

        var filePath = Path.Combine(_fixture.TestDirectory, "RadoubSettings.json");
        Assert.True(File.Exists(filePath));

        var json = File.ReadAllText(filePath);
        Assert.Contains("CurrentModulePath", json);
    }

    [Fact]
    public void ReloadSettings_ReadsUpdatedValues()
    {
        var originalTlk = Path.Combine(_fixture.TestDirectory, "original", "path.tlk");
        var originalModule = Path.Combine(_fixture.TestDirectory, "original", "module");
        var updatedTlk = Path.Combine(_fixture.TestDirectory, "updated", "custom.tlk");
        var updatedModule = Path.Combine(_fixture.TestDirectory, "updated", "module");

        var settings = RadoubSettings.Instance;
        settings.CustomTlkPath = originalTlk;
        settings.CurrentModulePath = originalModule;

        Assert.Equal(originalTlk, settings.CustomTlkPath);
        Assert.Equal(originalModule, settings.CurrentModulePath);

        // Simulate another process writing updated values to the settings file.
        // Must use ContractPath since that's how RadoubSettings persists paths -
        // on Windows, temp paths are under user profile and get contracted to ~/...
        var filePath = Path.Combine(_fixture.TestDirectory, "RadoubSettings.json");
        var updatedData = new Dictionary<string, string>
        {
            ["CustomTlkPath"] = PathHelper.ContractPath(updatedTlk),
            ["CurrentModulePath"] = PathHelper.ContractPath(updatedModule)
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(updatedData, new JsonSerializerOptions { WriteIndented = true }));

        // Reload should pick up the new values
        settings.ReloadSettings();

        Assert.Equal(updatedTlk, settings.CustomTlkPath);
        Assert.Equal(updatedModule, settings.CurrentModulePath);
    }

    [Fact]
    public void WizardState_DefaultsToNotRunWithNoAcknowledgedGaps()
    {
        var settings = RadoubSettings.Instance;

        Assert.False(settings.WizardHasRun);
        Assert.Empty(settings.AcknowledgedWizardGaps);
    }

    [Fact]
    public void AcknowledgeWizardGaps_PersistsAndSurvivesReload()
    {
        var settings = RadoubSettings.Instance;
        settings.AcknowledgeWizardGaps(new[] { "gamePath", "theme" });

        Assert.True(settings.WizardHasRun);
        Assert.Contains("gamePath", settings.AcknowledgedWizardGaps);
        Assert.Contains("theme", settings.AcknowledgedWizardGaps);

        // Simulate a fresh process: reset singleton, reload from disk.
        ResetAndConfigure();
        var reloaded = RadoubSettings.Instance;

        Assert.True(reloaded.WizardHasRun);
        Assert.Contains("gamePath", reloaded.AcknowledgedWizardGaps);
        Assert.Contains("theme", reloaded.AcknowledgedWizardGaps);
    }

    [Fact]
    public void AcknowledgeWizardGaps_MergesWithPreviousAcknowledgements()
    {
        var settings = RadoubSettings.Instance;
        settings.AcknowledgeWizardGaps(new[] { "gamePath" });
        settings.AcknowledgeWizardGaps(new[] { "newGap" });

        Assert.Contains("gamePath", settings.AcknowledgedWizardGaps);
        Assert.Contains("newGap", settings.AcknowledgedWizardGaps);
    }

    [Fact]
    public void Settings_SurviveNewInstance()
    {
        // First instance writes settings
        var settings1 = RadoubSettings.Instance;
        settings1.CustomTlkPath = _fakeTlkPath;
        settings1.CurrentModulePath = _fakeModulePath;

        // Reset and create a new instance (simulates child process startup)
        ResetAndConfigure();

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
        var filePath = Path.Combine(_fixture.TestDirectory, "RadoubSettings.json");
        var json = File.ReadAllText(filePath);
        Assert.Contains("~", json);

        // Reset and reload - should expand back to full path
        ResetAndConfigure();

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
        ResetAndConfigure();

        var settings2 = RadoubSettings.Instance;
        Assert.Equal("", settings2.CustomTlkPath);
    }

    [Fact]
    public void LoadSettings_HandlesCorruptJson()
    {
        // Write corrupt JSON
        var filePath = Path.Combine(_fixture.TestDirectory, "RadoubSettings.json");
        File.WriteAllText(filePath, "{ this is not valid json }}}");

        // Should not throw, just use defaults
        ResetAndConfigure();

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

    // ---- Bool persistence round-trip coverage (#2361) ----

    [Fact]
    public void TlkUseFemale_RoundTrips()
    {
        // Default is false; flip to true and confirm it survives a singleton reset.
        RadoubSettings.Instance.TlkUseFemale = true;

        ResetAndConfigure();

        Assert.True(RadoubSettings.Instance.TlkUseFemale);
    }

    [Fact]
    public void UseSharedLogging_RoundTrips()
    {
        // Default is true; flip to false and confirm it survives a singleton reset.
        RadoubSettings.Instance.UseSharedLogging = false;

        ResetAndConfigure();

        Assert.False(RadoubSettings.Instance.UseSharedLogging);
    }

    [Fact]
    public void BoolDefaults_AreStable()
    {
        var settings = RadoubSettings.Instance;
        Assert.False(settings.TlkUseFemale);
        Assert.True(settings.UseSharedLogging);
    }
}

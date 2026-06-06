using Radoub.Formats.Settings;
using Radoub.TestUtilities.Helpers;
using Xunit;

namespace Radoub.Formats.Tests.Settings;

/// <summary>
/// ReliquaryPath is the cross-tool discovery path Trebuchet reads to launch
/// Reliquary (mirrors ParleyPath/FencePath/ReliquePath). New tool — no legacy
/// key migration. Sprint 4 (#2294).
/// </summary>
[Collection("RadoubSettings")]
public class ReliquaryPathTests
{
    private readonly RadoubSettingsFixture _fixture;

    public ReliquaryPathTests(RadoubSettingsFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ReliquaryPath_DefaultsToEmpty()
    {
        // Bind to a fresh dir so a sibling test's persisted value (shared
        // collection fixture reuses one temp dir + JSON file) can't leak in.
        var freshDir = Path.Combine(Path.GetTempPath(), $"ReliquaryPathDefault_{Guid.NewGuid():N}");
        Directory.CreateDirectory(freshDir);
        try
        {
            SingletonTestHelper.ResetSingleton<RadoubSettings>("_instance", "_settingsDirectory");
            SingletonTestHelper.ConfigureSettingsDirectory("RADOUB_SETTINGS_DIR", freshDir);

            Assert.Equal("", RadoubSettings.Instance.ReliquaryPath);
        }
        finally
        {
            // Restore the fixture's dir so later tests in the collection still
            // bind where the fixture expects.
            SingletonTestHelper.ResetSingleton<RadoubSettings>("_instance", "_settingsDirectory");
            SingletonTestHelper.ConfigureSettingsDirectory("RADOUB_SETTINGS_DIR", _fixture.TestDirectory);
            try { Directory.Delete(freshDir, true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void ReliquaryPath_SetAndGet_RoundTrips()
    {
        _ = _fixture;
        var settings = RadoubSettings.Instance;

        settings.ReliquaryPath = "/some/path/Reliquary.exe";

        Assert.Equal("/some/path/Reliquary.exe", settings.ReliquaryPath);
    }

    [Fact]
    public void ReliquaryPath_PersistsAcrossReload()
    {
        _ = _fixture;

        RadoubSettings.Instance.ReliquaryPath = "/persisted/Reliquary.exe";

        // Drop the singleton and rebind to the same temp dir — forces a fresh
        // load from RadoubSettings.json.
        SingletonTestHelper.ResetSingleton<RadoubSettings>("_instance", "_settingsDirectory");
        SingletonTestHelper.ConfigureSettingsDirectory("RADOUB_SETTINGS_DIR", _fixture.TestDirectory);

        Assert.Equal("/persisted/Reliquary.exe", RadoubSettings.Instance.ReliquaryPath);
    }
}

using Radoub.Formats.Settings;
using Radoub.TestUtilities.Helpers;
using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for ToolLauncherService.RefreshPathsFromSiblingDirectory (#2079).
///
/// When Trebuchet is updated in place, RadoubSettings still holds the old tool
/// paths from the previous install location. On startup, Trebuchet must
/// overwrite those cached paths with any sibling executables that live next to
/// the current Trebuchet binary — otherwise launching a tool fires
/// File.Exists() against a path that no longer exists.
/// </summary>
public class ToolLauncherSiblingRefreshTests : IDisposable
{
    private static readonly string ExeExt = OperatingSystem.IsWindows() ? ".exe" : "";

    private readonly string _testDirectory;
    private readonly string _settingsDirectory;

    public ToolLauncherSiblingRefreshTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"TrebuchetSiblingTest_{Guid.NewGuid():N}");
        _settingsDirectory = Path.Combine(_testDirectory, "settings");
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_settingsDirectory);

        // Isolate RadoubSettings so the test doesn't clobber the user's tool paths
        SingletonTestHelper.ResetSingleton<RadoubSettings>("_instance", "_settingsDirectory");
        SingletonTestHelper.ConfigureSettingsDirectory("RADOUB_SETTINGS_DIR", _settingsDirectory);

        // Reset ToolLauncherService so each test gets a fresh discovery pass
        SingletonTestHelper.ResetSingleton<ToolLauncherService>();
    }

    public void Dispose()
    {
        SingletonTestHelper.ResetSingleton<ToolLauncherService>();
        SingletonTestHelper.ResetSingleton<RadoubSettings>("_instance", "_settingsDirectory");
        SingletonTestHelper.ConfigureSettingsDirectory("RADOUB_SETTINGS_DIR", null);

        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch { /* ignore cleanup errors */ }
    }

    /// <summary>
    /// Materialize ToolLauncherService.Instance (which runs the production ctor
    /// — including its own DiscoverTools pass that can write dev-path data into
    /// RadoubSettings) and then return a clean handle. Tests must arrange stale
    /// settings AFTER calling this; otherwise the ctor's discovery cascade
    /// overwrites whatever the test just wrote.
    /// </summary>
    private static ToolLauncherService GetInstanceWithCleanSettings()
    {
        var instance = ToolLauncherService.Instance;
        // Wipe anything the ctor's discovery wrote so test arrangements aren't
        // racing the singleton's own startup writes.
        var s = RadoubSettings.Instance;
        s.ParleyPath = "";
        s.ManifestPath = "";
        s.QuartermasterPath = "";
        s.FencePath = "";
        s.ReliquePath = "";
        return instance;
    }

    [Fact]
    public void RefreshPathsFromSiblingDirectory_OverwritesStaleSetting_WhenSiblingExists()
    {
        // Arrange: stale path in settings + a sibling exe at the new location
        var launcher = GetInstanceWithCleanSettings();
        var stalePath = Path.Combine(_testDirectory, "old", "Parley" + ExeExt);
        var siblingPath = Path.Combine(_testDirectory, "Parley" + ExeExt);
        File.WriteAllBytes(siblingPath, Array.Empty<byte>());
        RadoubSettings.Instance.ParleyPath = stalePath;

        // Act
        launcher.RefreshPathsFromSiblingDirectory(_testDirectory);

        // Assert
        Assert.Equal(siblingPath, RadoubSettings.Instance.ParleyPath);
    }

    [Fact]
    public void RefreshPathsFromSiblingDirectory_LeavesSettingAlone_WhenNoSibling()
    {
        // Arrange: only the stale setting exists, no sibling exe to overwrite with
        var launcher = GetInstanceWithCleanSettings();
        var stalePath = Path.Combine(_testDirectory, "elsewhere", "Manifest" + ExeExt);
        RadoubSettings.Instance.ManifestPath = stalePath;

        // Act
        launcher.RefreshPathsFromSiblingDirectory(_testDirectory);

        // Assert
        Assert.Equal(stalePath, RadoubSettings.Instance.ManifestPath);
    }

    [Fact]
    public void RefreshPathsFromSiblingDirectory_HandlesNullDirectory_WithoutThrowing()
    {
        // Arrange
        var launcher = GetInstanceWithCleanSettings();
        var stalePath = Path.Combine(_testDirectory, "elsewhere", "Quartermaster" + ExeExt);
        RadoubSettings.Instance.QuartermasterPath = stalePath;

        // Act + Assert: should be a no-op, not an exception
        var ex = Record.Exception(() => launcher.RefreshPathsFromSiblingDirectory(null));
        Assert.Null(ex);
        Assert.Equal(stalePath, RadoubSettings.Instance.QuartermasterPath);
    }

    [Fact]
    public void RefreshPathsFromSiblingDirectory_FindsReliqueSibling()
    {
        // After #2080 the built exe is Relique.exe (AssemblyName aligned with tool name);
        // sibling discovery walks Name → executable.
        var launcher = GetInstanceWithCleanSettings();
        var stalePath = Path.Combine(_testDirectory, "old", "Relique" + ExeExt);
        var siblingPath = Path.Combine(_testDirectory, "Relique" + ExeExt);
        File.WriteAllBytes(siblingPath, Array.Empty<byte>());
        RadoubSettings.Instance.ReliquePath = stalePath;

        // Act
        launcher.RefreshPathsFromSiblingDirectory(_testDirectory);

        // Assert
        Assert.Equal(siblingPath, RadoubSettings.Instance.ReliquePath);
    }

    [Fact]
    public void RefreshPathsFromSiblingDirectory_RewritesAllKnownTools_WhenAllSiblingsExist()
    {
        // Arrange: every tool has a sibling exe next to Trebuchet
        var launcher = GetInstanceWithCleanSettings();
        var parleySibling = Path.Combine(_testDirectory, "Parley" + ExeExt);
        var manifestSibling = Path.Combine(_testDirectory, "Manifest" + ExeExt);
        var qmSibling = Path.Combine(_testDirectory, "Quartermaster" + ExeExt);
        var fenceSibling = Path.Combine(_testDirectory, "Fence" + ExeExt);
        var reliqueSibling = Path.Combine(_testDirectory, "Relique" + ExeExt);

        foreach (var p in new[] { parleySibling, manifestSibling, qmSibling, fenceSibling, reliqueSibling })
        {
            File.WriteAllBytes(p, Array.Empty<byte>());
        }

        // Pre-populate with stale paths
        var settings = RadoubSettings.Instance;
        var oldDir = Path.Combine(_testDirectory, "old");
        settings.ParleyPath = Path.Combine(oldDir, "Parley" + ExeExt);
        settings.ManifestPath = Path.Combine(oldDir, "Manifest" + ExeExt);
        settings.QuartermasterPath = Path.Combine(oldDir, "Quartermaster" + ExeExt);
        settings.FencePath = Path.Combine(oldDir, "Fence" + ExeExt);
        settings.ReliquePath = Path.Combine(oldDir, "Relique" + ExeExt);

        // Act
        launcher.RefreshPathsFromSiblingDirectory(_testDirectory);

        // Assert
        Assert.Equal(parleySibling, settings.ParleyPath);
        Assert.Equal(manifestSibling, settings.ManifestPath);
        Assert.Equal(qmSibling, settings.QuartermasterPath);
        Assert.Equal(fenceSibling, settings.FencePath);
        Assert.Equal(reliqueSibling, settings.ReliquePath);
    }
}

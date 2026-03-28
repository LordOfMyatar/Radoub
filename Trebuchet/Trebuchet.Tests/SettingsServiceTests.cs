using RadoubLauncher.Services;
using Radoub.Formats.Logging;
using Radoub.TestUtilities.Helpers;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for SettingsService property validation and persistence.
/// Uses isolated temp directory to avoid corrupting user settings.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly string _testDirectory;

    public SettingsServiceTests()
    {
        // Create isolated test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"TrebuchetTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        // Reset singleton and configure via environment variable
        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("TREBUCHET_SETTINGS_DIR", _testDirectory);
    }

    public void Dispose()
    {
        // Reset singleton and clear env var
        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("TREBUCHET_SETTINGS_DIR", null);

        // Clean up test directory
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch { /* Ignore cleanup errors */ }
    }

    #region Window Size Tests

    [Fact]
    public void WindowWidth_ClampedToMinimum()
    {
        var settings = SettingsService.Instance;
        settings.WindowWidth = 100; // Below minimum of 600

        Assert.Equal(600, settings.WindowWidth);
    }

    [Fact]
    public void WindowHeight_ClampedToMinimum()
    {
        var settings = SettingsService.Instance;
        settings.WindowHeight = 100; // Below minimum of 400

        Assert.Equal(400, settings.WindowHeight);
    }

    [Fact]
    public void WindowWidth_AcceptsValidValue()
    {
        var settings = SettingsService.Instance;
        settings.WindowWidth = 1200;

        Assert.Equal(1200, settings.WindowWidth);
    }

    [Fact]
    public void WindowHeight_AcceptsValidValue()
    {
        var settings = SettingsService.Instance;
        settings.WindowHeight = 800;

        Assert.Equal(800, settings.WindowHeight);
    }

    #endregion

    #region Font Scale Tests

    [Fact]
    public void FontSizeScale_ClampedToMinimum()
    {
        var settings = SettingsService.Instance;
        settings.FontSizeScale = 0.5; // Below minimum of 0.8

        Assert.Equal(0.8, settings.FontSizeScale);
    }

    [Fact]
    public void FontSizeScale_ClampedToMaximum()
    {
        var settings = SettingsService.Instance;
        settings.FontSizeScale = 2.0; // Above maximum of 1.5

        Assert.Equal(1.5, settings.FontSizeScale);
    }

    [Fact]
    public void FontSizeScale_AcceptsValidValue()
    {
        var settings = SettingsService.Instance;
        settings.FontSizeScale = 1.2;

        Assert.Equal(1.2, settings.FontSizeScale);
    }

    #endregion

    #region Logging Settings Tests

    [Fact]
    public void LogRetentionSessions_ClampedToMinimum()
    {
        var settings = SettingsService.Instance;
        settings.LogRetentionSessions = 0; // Below minimum of 1

        Assert.Equal(1, settings.LogRetentionSessions);
    }

    [Fact]
    public void LogRetentionSessions_ClampedToMaximum()
    {
        var settings = SettingsService.Instance;
        settings.LogRetentionSessions = 20; // Above maximum of 10

        Assert.Equal(10, settings.LogRetentionSessions);
    }

    [Fact]
    public void LogRetentionSessions_AcceptsValidValue()
    {
        var settings = SettingsService.Instance;
        settings.LogRetentionSessions = 5;

        Assert.Equal(5, settings.LogRetentionSessions);
    }

    [Fact]
    public void CurrentLogLevel_CanBeSet()
    {
        var settings = SettingsService.Instance;
        settings.CurrentLogLevel = LogLevel.DEBUG;

        Assert.Equal(LogLevel.DEBUG, settings.CurrentLogLevel);
    }

    #endregion

    #region Recent Modules Tests

    [Fact]
    public void MaxRecentModules_ClampedToMinimum()
    {
        var settings = SettingsService.Instance;
        settings.MaxRecentModules = 0; // Below minimum of 1

        Assert.Equal(1, settings.MaxRecentModules);
    }

    [Fact]
    public void MaxRecentModules_ClampedToMaximum()
    {
        var settings = SettingsService.Instance;
        settings.MaxRecentModules = 50; // Above maximum of 20

        Assert.Equal(20, settings.MaxRecentModules);
    }

    [Fact]
    public void MaxRecentModules_AcceptsValidValue()
    {
        var settings = SettingsService.Instance;
        settings.MaxRecentModules = 15;

        Assert.Equal(15, settings.MaxRecentModules);
    }

    [Fact]
    public void RecentModules_InitiallyEmpty()
    {
        var settings = SettingsService.Instance;
        Assert.Empty(settings.RecentModules);
    }

    [Fact]
    public void AddRecentModule_IgnoresEmptyPath()
    {
        var settings = SettingsService.Instance;
        settings.AddRecentModule("");

        Assert.Empty(settings.RecentModules);
    }

    [Fact]
    public void AddRecentModule_IgnoresNonExistentPath()
    {
        var settings = SettingsService.Instance;
        settings.AddRecentModule("/nonexistent/path/module.mod");

        Assert.Empty(settings.RecentModules);
    }

    [Fact]
    public void ClearRecentModules_ClearsList()
    {
        var settings = SettingsService.Instance;
        // First add a real existing directory
        settings.AddRecentModule(_testDirectory);
        Assert.Single(settings.RecentModules);

        settings.ClearRecentModules();

        Assert.Empty(settings.RecentModules);
    }

    #endregion

    #region Build Settings Tests

    [Fact]
    public void CompileScriptsEnabled_DefaultsFalse()
    {
        var settings = SettingsService.Instance;
        Assert.False(settings.CompileScriptsEnabled);
    }

    [Fact]
    public void CompileScriptsEnabled_CanBeEnabled()
    {
        var settings = SettingsService.Instance;
        settings.CompileScriptsEnabled = true;

        Assert.True(settings.CompileScriptsEnabled);
    }

    #endregion
}

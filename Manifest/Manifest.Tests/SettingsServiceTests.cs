using System.ComponentModel;
using Manifest.Services;
using Radoub.Formats.Logging;
using Xunit;

namespace Manifest.Tests;

/// <summary>
/// Tests for SettingsService.
/// Uses isolated temp directory via MANIFEST_SETTINGS_DIR environment variable.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly string _testSettingsDir;
    private readonly string? _originalEnvValue;

    public SettingsServiceTests()
    {
        // Save original env value
        _originalEnvValue = Environment.GetEnvironmentVariable("MANIFEST_SETTINGS_DIR");

        // Create isolated temp directory for each test run
        _testSettingsDir = Path.Combine(Path.GetTempPath(), $"Manifest_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testSettingsDir);

        // Reset and configure for testing BEFORE first Instance access
        SettingsService.ResetForTesting();
        Environment.SetEnvironmentVariable("MANIFEST_SETTINGS_DIR", _testSettingsDir);
    }

    public void Dispose()
    {
        // Reset singleton so next test gets fresh instance
        SettingsService.ResetForTesting();

        // Restore original env value
        Environment.SetEnvironmentVariable("MANIFEST_SETTINGS_DIR", _originalEnvValue);

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
    public void LoadSettings_CorruptedJson_ReturnsDefaults()
    {
        // Arrange - write corrupted JSON
        var settingsFile = Path.Combine(_testSettingsDir, "ManifestSettings.json");
        File.WriteAllText(settingsFile, "{ invalid json [[[");

        // Act - reset and reload
        SettingsService.ResetForTesting();
        var service = SettingsService.Instance;

        // Assert - should have default values
        Assert.Equal(14, service.FontSize);
        Assert.Equal(1000, service.WindowWidth);
        Assert.Equal(700, service.WindowHeight);
    }

    [Fact]
    public void SaveSettings_RoundTrip_PreservesAllValues()
    {
        // Arrange
        var service = SettingsService.Instance;
        service.FontSize = 18;
        service.WindowWidth = 1200;
        service.WindowHeight = 800;
        service.WindowMaximized = true;
        service.TreePanelWidth = 350;
        service.CurrentThemeId = "org.manifest.theme.dark";
        service.LogRetentionSessions = 5;
        service.CurrentLogLevel = LogLevel.DEBUG;
        service.SpellCheckEnabled = false;

        // Act - reset and reload
        SettingsService.ResetForTesting();
        var reloaded = SettingsService.Instance;

        // Assert
        Assert.Equal(18, reloaded.FontSize);
        Assert.Equal(1200, reloaded.WindowWidth);
        Assert.Equal(800, reloaded.WindowHeight);
        Assert.True(reloaded.WindowMaximized);
        Assert.Equal(350, reloaded.TreePanelWidth);
        Assert.Equal("org.manifest.theme.dark", reloaded.CurrentThemeId);
        Assert.Equal(5, reloaded.LogRetentionSessions);
        Assert.Equal(LogLevel.DEBUG, reloaded.CurrentLogLevel);
        Assert.False(reloaded.SpellCheckEnabled);
    }

    [Fact]
    public void FontSize_OutOfRange_ClampedToBounds()
    {
        var service = SettingsService.Instance;

        // Below minimum (8)
        service.FontSize = 5;
        Assert.Equal(8, service.FontSize);

        // Above maximum (24)
        service.FontSize = 30;
        Assert.Equal(24, service.FontSize);

        // Within range
        service.FontSize = 16;
        Assert.Equal(16, service.FontSize);
    }

    [Fact]
    public void WindowDimensions_Invalid_UseDefaults()
    {
        var service = SettingsService.Instance;

        // WindowWidth minimum is 400
        service.WindowWidth = 100;
        Assert.True(service.WindowWidth >= 400);

        // WindowHeight minimum is 300
        service.WindowHeight = 100;
        Assert.True(service.WindowHeight >= 300);
    }

    [Fact]
    public void TreePanelWidth_EnforcesRange()
    {
        var service = SettingsService.Instance;

        // Minimum is 150
        service.TreePanelWidth = 50;
        Assert.True(service.TreePanelWidth >= 150);

        // Maximum is 600
        service.TreePanelWidth = 800;
        Assert.True(service.TreePanelWidth <= 600);
    }

    [Fact]
    public void RecentFiles_Cleanup_RemovesMissingFiles()
    {
        // Arrange - create a temp file then delete it
        var tempFile = Path.Combine(_testSettingsDir, "test.jrl");
        File.WriteAllText(tempFile, "test");

        var service = SettingsService.Instance;
        service.AddRecentFile(tempFile);
        Assert.Contains(tempFile, service.RecentFiles);

        // Delete the file
        File.Delete(tempFile);

        // Act - reset and reload (cleanup happens on load)
        SettingsService.ResetForTesting();
        var reloaded = SettingsService.Instance;

        // Assert - missing file should be removed
        Assert.DoesNotContain(tempFile, reloaded.RecentFiles);
    }

    [Fact]
    public void RecentFiles_MaxCount_TrimsList()
    {
        var service = SettingsService.Instance;
        service.MaxRecentFiles = 3;

        // Create and add more files than max
        for (int i = 0; i < 5; i++)
        {
            var tempFile = Path.Combine(_testSettingsDir, $"test{i}.jrl");
            File.WriteAllText(tempFile, "test");
            service.AddRecentFile(tempFile);
        }

        // Assert - should only keep MaxRecentFiles
        Assert.True(service.RecentFiles.Count <= 3);
    }

    [Fact]
    public void PropertyChanged_Fires_OnValueChange()
    {
        var service = SettingsService.Instance;
        var changedProperties = new List<string>();

        service.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName != null)
                changedProperties.Add(e.PropertyName);
        };

        // Act
        service.FontSize = 20;
        service.WindowWidth = 1100;
        service.CurrentThemeId = "test.theme";

        // Assert
        Assert.Contains("FontSize", changedProperties);
        Assert.Contains("WindowWidth", changedProperties);
        Assert.Contains("CurrentThemeId", changedProperties);
    }

    [Fact]
    public void LogRetentionSessions_EnforcesRange()
    {
        var service = SettingsService.Instance;

        // Minimum is 1
        service.LogRetentionSessions = 0;
        Assert.True(service.LogRetentionSessions >= 1);

        // Maximum is 10
        service.LogRetentionSessions = 100;
        Assert.True(service.LogRetentionSessions <= 10);
    }

    [Fact]
    public void MaxRecentFiles_EnforcesRange()
    {
        var service = SettingsService.Instance;

        // Minimum is 1
        service.MaxRecentFiles = 0;
        Assert.True(service.MaxRecentFiles >= 1);

        // Maximum is 20
        service.MaxRecentFiles = 100;
        Assert.True(service.MaxRecentFiles <= 20);
    }

    [Fact]
    public void AddRecentFile_MovesExistingToFront()
    {
        var service = SettingsService.Instance;

        // Create test files
        var file1 = Path.Combine(_testSettingsDir, "first.jrl");
        var file2 = Path.Combine(_testSettingsDir, "second.jrl");
        File.WriteAllText(file1, "test");
        File.WriteAllText(file2, "test");

        // Add files
        service.AddRecentFile(file1);
        service.AddRecentFile(file2);

        // file2 should be first now
        Assert.Equal(file2, service.RecentFiles[0]);

        // Re-add file1
        service.AddRecentFile(file1);

        // file1 should now be first
        Assert.Equal(file1, service.RecentFiles[0]);
    }
}

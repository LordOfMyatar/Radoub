using Quartermaster.Services;

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

        // Reset and configure for testing BEFORE first Instance access
        SettingsService.ResetForTesting();
        SettingsService.ConfigureForTesting(_testSettingsDir);
    }

    public void Dispose()
    {
        // Reset singleton so next test gets fresh instance
        SettingsService.ResetForTesting();

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
    public void FontSize_EnforcesRange()
    {
        var service = SettingsService.Instance;

        // FontSize should be between 8 and 24
        service.FontSize = 5;
        Assert.True(service.FontSize >= 8);

        service.FontSize = 30;
        Assert.True(service.FontSize <= 24);
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
    public void CurrentThemeId_HasDefaultValue()
    {
        var service = SettingsService.Instance;

        // Should have a default theme
        Assert.False(string.IsNullOrEmpty(service.CurrentThemeId));
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
        service.FontSize = 18;
        service.WindowWidth = 1000;

        // Reset and reload
        SettingsService.ResetForTesting();
        SettingsService.ConfigureForTesting(_testSettingsDir);

        var reloaded = SettingsService.Instance;

        Assert.Equal(18, reloaded.FontSize);
        Assert.Equal(1000, reloaded.WindowWidth);
    }
}

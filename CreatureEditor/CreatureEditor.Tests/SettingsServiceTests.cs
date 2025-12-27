using CreatureEditor.Services;

namespace CreatureEditor.Tests;

/// <summary>
/// Tests for SettingsService.
/// Note: SettingsService is a singleton, so these tests verify behavior
/// without modifying actual user settings.
/// </summary>
public class SettingsServiceTests
{
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
        var originalWidth = service.WindowWidth;

        try
        {
            // WindowWidth should not go below 600
            service.WindowWidth = 100;

            Assert.True(service.WindowWidth >= 600);
        }
        finally
        {
            service.WindowWidth = originalWidth;
        }
    }

    [Fact]
    public void WindowHeight_EnforcesMinimum()
    {
        var service = SettingsService.Instance;
        var originalHeight = service.WindowHeight;

        try
        {
            // WindowHeight should not go below 400
            service.WindowHeight = 100;

            Assert.True(service.WindowHeight >= 400);
        }
        finally
        {
            service.WindowHeight = originalHeight;
        }
    }

    [Fact]
    public void FontSize_EnforcesRange()
    {
        var service = SettingsService.Instance;
        var originalSize = service.FontSize;

        try
        {
            // FontSize should be between 8 and 24
            service.FontSize = 5;
            Assert.True(service.FontSize >= 8);

            service.FontSize = 30;
            Assert.True(service.FontSize <= 24);
        }
        finally
        {
            service.FontSize = originalSize;
        }
    }

    [Fact]
    public void LeftPanelWidth_EnforcesRange()
    {
        var service = SettingsService.Instance;
        var originalWidth = service.LeftPanelWidth;

        try
        {
            // LeftPanelWidth should be between 200 and 600
            service.LeftPanelWidth = 100;
            Assert.True(service.LeftPanelWidth >= 200);

            service.LeftPanelWidth = 800;
            Assert.True(service.LeftPanelWidth <= 600);
        }
        finally
        {
            service.LeftPanelWidth = originalWidth;
        }
    }

    [Fact]
    public void MaxRecentFiles_EnforcesRange()
    {
        var service = SettingsService.Instance;
        var originalMax = service.MaxRecentFiles;

        try
        {
            // MaxRecentFiles should be between 1 and 20
            service.MaxRecentFiles = 0;
            Assert.True(service.MaxRecentFiles >= 1);

            service.MaxRecentFiles = 100;
            Assert.True(service.MaxRecentFiles <= 20);
        }
        finally
        {
            service.MaxRecentFiles = originalMax;
        }
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
        var originalRetention = service.LogRetentionSessions;

        try
        {
            // LogRetentionSessions should be between 1 and 10
            service.LogRetentionSessions = 0;
            Assert.True(service.LogRetentionSessions >= 1);

            service.LogRetentionSessions = 100;
            Assert.True(service.LogRetentionSessions <= 10);
        }
        finally
        {
            service.LogRetentionSessions = originalRetention;
        }
    }
}

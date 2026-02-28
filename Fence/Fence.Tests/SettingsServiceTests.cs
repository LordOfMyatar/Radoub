using MerchantEditor.Services;
using Radoub.TestUtilities.Helpers;

namespace Fence.Tests;

[Collection("SettingsService")]
public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsServiceTests()
    {
        // Create isolated temp directory for each test
        _tempDir = Path.Combine(Path.GetTempPath(), "FenceTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        // Reset singleton and configure via environment variable
        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("FENCE_SETTINGS_DIR", _tempDir);
    }

    public void Dispose()
    {
        SingletonTestHelper.ResetSingleton<SettingsService>();
        SingletonTestHelper.ConfigureSettingsDirectory("FENCE_SETTINGS_DIR", null);
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    [Fact]
    public void Instance_ReturnsNonNull()
    {
        // Act
        var instance = SettingsService.Instance;

        // Assert
        Assert.NotNull(instance);
    }

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        // Act
        var instance1 = SettingsService.Instance;
        var instance2 = SettingsService.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void WindowWidth_ClampedToMinimum()
    {
        // Arrange
        var settings = SettingsService.Instance;

        // Act
        settings.WindowWidth = 100; // Below minimum

        // Assert
        Assert.Equal(600, settings.WindowWidth);
    }

    [Fact]
    public void WindowHeight_ClampedToMinimum()
    {
        // Arrange
        var settings = SettingsService.Instance;

        // Act
        settings.WindowHeight = 100; // Below minimum

        // Assert
        Assert.Equal(400, settings.WindowHeight);
    }

    [Fact]
    public void FontSize_ClampedToRange()
    {
        // Arrange
        var settings = SettingsService.Instance;

        // Act & Assert - below minimum
        settings.FontSize = 4;
        Assert.Equal(8, settings.FontSize);

        // Act & Assert - above maximum
        settings.FontSize = 30;
        Assert.Equal(24, settings.FontSize);
    }

    [Fact]
    public void LeftPanelWidth_ClampedToRange()
    {
        // Arrange
        var settings = SettingsService.Instance;

        // Act & Assert - below minimum
        settings.LeftPanelWidth = 100;
        Assert.Equal(250, settings.LeftPanelWidth);

        // Act & Assert - above maximum
        settings.LeftPanelWidth = 800;
        Assert.Equal(700, settings.LeftPanelWidth);
    }

    [Fact]
    public void RecentFiles_InitiallyEmpty()
    {
        // Arrange
        var settings = SettingsService.Instance;

        // Assert
        Assert.Empty(settings.RecentFiles);
    }

    [Fact]
    public void ClearRecentFiles_EmptiesList()
    {
        // Arrange
        var settings = SettingsService.Instance;
        var testFile = Path.Combine(_tempDir, "test.utm");
        File.WriteAllText(testFile, "");
        settings.AddRecentFile(testFile);
        Assert.NotEmpty(settings.RecentFiles);

        // Act
        settings.ClearRecentFiles();

        // Assert
        Assert.Empty(settings.RecentFiles);
    }

    [Fact]
    public void MaxRecentFiles_ClampedToRange()
    {
        // Arrange
        var settings = SettingsService.Instance;

        // Act & Assert - below minimum
        settings.MaxRecentFiles = 0;
        Assert.Equal(1, settings.MaxRecentFiles);

        // Act & Assert - above maximum
        settings.MaxRecentFiles = 50;
        Assert.Equal(20, settings.MaxRecentFiles);
    }

    [Fact]
    public void CurrentThemeId_HasDefault()
    {
        // Arrange
        var settings = SettingsService.Instance;

        // Assert
        Assert.NotNull(settings.CurrentThemeId);
        Assert.Contains("theme", settings.CurrentThemeId);
    }

    [Fact]
    public void LogRetentionSessions_ClampedToRange()
    {
        // Arrange
        var settings = SettingsService.Instance;

        // Act & Assert - below minimum
        settings.LogRetentionSessions = 0;
        Assert.Equal(1, settings.LogRetentionSessions);

        // Act & Assert - above maximum
        settings.LogRetentionSessions = 20;
        Assert.Equal(10, settings.LogRetentionSessions);
    }

    [Fact]
    public void RightPanelWidth_ClampedToRange()
    {
        var settings = SettingsService.Instance;

        settings.RightPanelWidth = 100;
        Assert.Equal(250, settings.RightPanelWidth);

        settings.RightPanelWidth = 900;
        Assert.Equal(700, settings.RightPanelWidth);
    }

    [Fact]
    public void StoreBrowserPanelWidth_ClampedToRange()
    {
        var settings = SettingsService.Instance;

        settings.StoreBrowserPanelWidth = 50;
        Assert.Equal(150, settings.StoreBrowserPanelWidth);

        settings.StoreBrowserPanelWidth = 600;
        Assert.Equal(400, settings.StoreBrowserPanelWidth);
    }

    [Fact]
    public void ItemDetailsPanelWidth_ClampedToRange()
    {
        var settings = SettingsService.Instance;

        settings.ItemDetailsPanelWidth = 50;
        Assert.Equal(180, settings.ItemDetailsPanelWidth);

        settings.ItemDetailsPanelWidth = 700;
        Assert.Equal(500, settings.ItemDetailsPanelWidth);
    }

    [Fact]
    public void StoreBrowserPanelVisible_DefaultTrue()
    {
        var settings = SettingsService.Instance;

        Assert.True(settings.StoreBrowserPanelVisible);
    }

    [Fact]
    public void ItemDetailsPanelVisible_DefaultTrue()
    {
        var settings = SettingsService.Instance;

        Assert.True(settings.ItemDetailsPanelVisible);
    }

    [Fact]
    public void PanelVisible_SetToFalse_Persists()
    {
        var settings = SettingsService.Instance;

        settings.StoreBrowserPanelVisible = false;
        Assert.False(settings.StoreBrowserPanelVisible);

        settings.ItemDetailsPanelVisible = false;
        Assert.False(settings.ItemDetailsPanelVisible);
    }

    [Fact]
    public async Task ValidateRecentFilesAsync_RemovesMissingFiles()
    {
        var settings = SettingsService.Instance;

        // Add a file that exists and one that doesn't
        var existingFile = Path.Combine(_tempDir, "exists.utm");
        File.WriteAllText(existingFile, "");
        settings.AddRecentFile(existingFile);
        settings.AddRecentFile(Path.Combine(_tempDir, "missing.utm"));

        await settings.ValidateRecentFilesAsync();

        var recent = settings.RecentFiles;
        Assert.Contains(existingFile, recent);
        Assert.DoesNotContain(Path.Combine(_tempDir, "missing.utm"), recent);
    }

    [Fact]
    public async Task ValidateRecentFilesAsync_AllFilesExist_NoChange()
    {
        var settings = SettingsService.Instance;
        var file1 = Path.Combine(_tempDir, "file1.utm");
        var file2 = Path.Combine(_tempDir, "file2.utm");
        File.WriteAllText(file1, "");
        File.WriteAllText(file2, "");
        settings.AddRecentFile(file1);
        settings.AddRecentFile(file2);

        await settings.ValidateRecentFilesAsync();

        Assert.Equal(2, settings.RecentFiles.Count);
    }

    [Fact]
    public async Task ValidateRecentFilesAsync_EmptyList_DoesNotThrow()
    {
        var settings = SettingsService.Instance;

        var exception = await Record.ExceptionAsync(() => settings.ValidateRecentFilesAsync());

        Assert.Null(exception);
    }

    [Fact]
    public void Settings_PersistAcrossReload()
    {
        var settings = SettingsService.Instance;
        settings.LeftPanelWidth = 500;
        settings.RightPanelWidth = 350;
        settings.StoreBrowserPanelVisible = false;

        // Reset and reload
        SingletonTestHelper.ResetSingleton<SettingsService>();
        var reloaded = SettingsService.Instance;

        Assert.Equal(500, reloaded.LeftPanelWidth);
        Assert.Equal(350, reloaded.RightPanelWidth);
        Assert.False(reloaded.StoreBrowserPanelVisible);
    }
}

[CollectionDefinition("SettingsService", DisableParallelization = true)]
public class SettingsServiceCollection : ICollectionFixture<object>
{
    // This class exists solely to define the collection and disable parallelization
    // for tests that share the SettingsService singleton
}

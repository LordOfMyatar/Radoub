using MerchantEditor.Services;

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
        SettingsService.ConfigureForTesting(_tempDir);
    }

    public void Dispose()
    {
        SettingsService.ResetForTesting();
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
}

[CollectionDefinition("SettingsService", DisableParallelization = true)]
public class SettingsServiceCollection : ICollectionFixture<object>
{
    // This class exists solely to define the collection and disable parallelization
    // for tests that share the SettingsService singleton
}

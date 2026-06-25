using MerchantEditor.Services;
using Radoub.TestUtilities.Bases;
using Radoub.TestUtilities.Helpers;

namespace Fence.Tests;

/// <summary>
/// Tests for Fence's SettingsService. The shared singleton/window/recent-files/log-retention
/// contract lives in <see cref="ToolSettingsServiceTestBase{TService}"/> (#2464); only
/// Fence-specific behavior (panel widths, visibility, async recent-file validation) is below.
/// </summary>
[Collection("SettingsService")]
public class SettingsServiceTests : ToolSettingsServiceTestBase<SettingsService>
{
    protected override string SettingsEnvironmentVariable => "FENCE_SETTINGS_DIR";
    protected override string ToolDirPrefix => "Fence";
    protected override string RecentFileExtension => ".utm";

    protected override SettingsService GetInstance() => SettingsService.Instance;
    protected override void ResetSingleton() => SingletonTestHelper.ResetSingleton<SettingsService>();

    protected override double GetWindowWidth(SettingsService s) => s.WindowWidth;
    protected override void SetWindowWidth(SettingsService s, double v) => s.WindowWidth = v;
    protected override double GetWindowHeight(SettingsService s) => s.WindowHeight;
    protected override void SetWindowHeight(SettingsService s, double v) => s.WindowHeight = v;
    protected override IReadOnlyList<string> GetRecentFiles(SettingsService s) => s.RecentFiles;
    protected override void AddRecentFile(SettingsService s, string path) => s.AddRecentFile(path);
    protected override int GetMaxRecentFiles(SettingsService s) => s.MaxRecentFiles;
    protected override void SetMaxRecentFiles(SettingsService s, int v) => s.MaxRecentFiles = v;
    protected override int GetLogRetentionSessions(SettingsService s) => s.LogRetentionSessions;
    protected override void SetLogRetentionSessions(SettingsService s, int v) => s.LogRetentionSessions = v;

    // ---- Fence-specific ----

    [Fact]
    public void LeftPanelWidth_ClampedToRange()
    {
        var settings = GetInstance();

        settings.LeftPanelWidth = 100;
        Assert.Equal(250, settings.LeftPanelWidth);

        settings.LeftPanelWidth = 800;
        Assert.Equal(700, settings.LeftPanelWidth);
    }

    [Fact]
    public void RightPanelWidth_ClampedToRange()
    {
        var settings = GetInstance();

        settings.RightPanelWidth = 100;
        Assert.Equal(250, settings.RightPanelWidth);

        settings.RightPanelWidth = 900;
        Assert.Equal(700, settings.RightPanelWidth);
    }

    [Fact]
    public void StoreBrowserPanelWidth_ClampedToRange()
    {
        var settings = GetInstance();

        settings.StoreBrowserPanelWidth = 50;
        Assert.Equal(150, settings.StoreBrowserPanelWidth);

        settings.StoreBrowserPanelWidth = 600;
        Assert.Equal(400, settings.StoreBrowserPanelWidth);
    }

    [Fact]
    public void ItemDetailsPanelWidth_ClampedToRange()
    {
        var settings = GetInstance();

        settings.ItemDetailsPanelWidth = 50;
        Assert.Equal(180, settings.ItemDetailsPanelWidth);

        settings.ItemDetailsPanelWidth = 700;
        Assert.Equal(500, settings.ItemDetailsPanelWidth);
    }

    [Fact]
    public void StoreBrowserPanelVisible_DefaultTrue()
    {
        Assert.True(GetInstance().StoreBrowserPanelVisible);
    }

    [Fact]
    public void ItemDetailsPanelVisible_DefaultTrue()
    {
        Assert.True(GetInstance().ItemDetailsPanelVisible);
    }

    [Fact]
    public void PanelVisible_SetToFalse_Persists()
    {
        var settings = GetInstance();

        settings.StoreBrowserPanelVisible = false;
        Assert.False(settings.StoreBrowserPanelVisible);

        settings.ItemDetailsPanelVisible = false;
        Assert.False(settings.ItemDetailsPanelVisible);
    }

    [Fact]
    public void ClearRecentFiles_EmptiesList()
    {
        var settings = GetInstance();
        var testFile = Path.Combine(TestSettingsDir, "test.utm");
        File.WriteAllText(testFile, "");
        settings.AddRecentFile(testFile);
        Assert.NotEmpty(settings.RecentFiles);

        settings.ClearRecentFiles();

        Assert.Empty(settings.RecentFiles);
    }

    [Fact]
    public async Task ValidateRecentFilesAsync_RemovesMissingFiles()
    {
        var settings = GetInstance();

        var existingFile = Path.Combine(TestSettingsDir, "exists.utm");
        File.WriteAllText(existingFile, "");
        settings.AddRecentFile(existingFile);
        settings.AddRecentFile(Path.Combine(TestSettingsDir, "missing.utm"));

        await settings.ValidateRecentFilesAsync();

        var recent = settings.RecentFiles;
        Assert.Contains(existingFile, recent);
        Assert.DoesNotContain(Path.Combine(TestSettingsDir, "missing.utm"), recent);
    }

    [Fact]
    public async Task ValidateRecentFilesAsync_AllFilesExist_NoChange()
    {
        var settings = GetInstance();
        var file1 = Path.Combine(TestSettingsDir, "file1.utm");
        var file2 = Path.Combine(TestSettingsDir, "file2.utm");
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
        var settings = GetInstance();

        var exception = await Record.ExceptionAsync(() => settings.ValidateRecentFilesAsync());

        Assert.Null(exception);
    }

    [Fact]
    public void Settings_PersistAcrossReload()
    {
        var settings = GetInstance();
        settings.LeftPanelWidth = 500;
        settings.RightPanelWidth = 350;
        settings.StoreBrowserPanelVisible = false;

        ResetSingleton();
        var reloaded = GetInstance();

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

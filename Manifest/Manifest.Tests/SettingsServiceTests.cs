using Manifest.Services;
using Radoub.Formats.Logging;
using Radoub.TestUtilities.Bases;
using Radoub.TestUtilities.Helpers;
using Xunit;

namespace Manifest.Tests;

/// <summary>
/// Tests for Manifest's SettingsService. The shared singleton/window/recent-files/log-retention
/// contract lives in <see cref="ToolSettingsServiceTestBase{TService}"/> (#2464); only
/// Manifest-specific behavior (corrupt-JSON recovery, full round-trip, tree panel, MRU ordering,
/// PropertyChanged) is below.
/// </summary>
public class SettingsServiceTests : ToolSettingsServiceTestBase<SettingsService>
{
    protected override string SettingsEnvironmentVariable => "MANIFEST_SETTINGS_DIR";
    protected override string ToolDirPrefix => "Manifest";
    protected override string RecentFileExtension => ".jrl";
    protected override double ExpectedMinWindowWidth => 400;
    protected override double ExpectedMinWindowHeight => 300;

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

    // ---- Manifest-specific ----

    [Fact]
    public void SharedSettings_ReturnsRadoubSettings()
    {
        Assert.NotNull(SettingsService.SharedSettings);
    }

    [Fact]
    public void LoadSettings_CorruptedJson_ReturnsDefaults()
    {
        var settingsFile = Path.Combine(TestSettingsDir, "ManifestSettings.json");
        File.WriteAllText(settingsFile, "{ invalid json [[[");

        ResetSingleton();
        var service = GetInstance();

        Assert.Equal(1000, service.WindowWidth);
        Assert.Equal(700, service.WindowHeight);
    }

    [Fact]
    public void SaveSettings_RoundTrip_PreservesAllValues()
    {
        var service = GetInstance();
        service.WindowWidth = 1200;
        service.WindowHeight = 800;
        service.WindowMaximized = true;
        service.TreePanelWidth = 350;
        service.LogRetentionSessions = 5;
        service.CurrentLogLevel = LogLevel.DEBUG;
        service.SpellCheckEnabled = false;

        ResetSingleton();
        var reloaded = GetInstance();

        Assert.Equal(1200, reloaded.WindowWidth);
        Assert.Equal(800, reloaded.WindowHeight);
        Assert.True(reloaded.WindowMaximized);
        Assert.Equal(350, reloaded.TreePanelWidth);
        Assert.Equal(5, reloaded.LogRetentionSessions);
        Assert.Equal(LogLevel.DEBUG, reloaded.CurrentLogLevel);
        Assert.False(reloaded.SpellCheckEnabled);
    }

    [Fact]
    public void TreePanelWidth_EnforcesRange()
    {
        var service = GetInstance();

        service.TreePanelWidth = 50;
        Assert.True(service.TreePanelWidth >= 150);

        service.TreePanelWidth = 800;
        Assert.True(service.TreePanelWidth <= 600);
    }

    [Fact]
    public void RecentFiles_Cleanup_RemovesMissingFiles()
    {
        var tempFile = Path.Combine(TestSettingsDir, "test.jrl");
        File.WriteAllText(tempFile, "test");

        var service = GetInstance();
        service.AddRecentFile(tempFile);
        Assert.Contains(tempFile, service.RecentFiles);

        File.Delete(tempFile);

        ResetSingleton();
        var reloaded = GetInstance();

        Assert.DoesNotContain(tempFile, reloaded.RecentFiles);
    }

    [Fact]
    public void RecentFiles_MaxCount_TrimsList()
    {
        var service = GetInstance();
        service.MaxRecentFiles = 3;

        for (int i = 0; i < 5; i++)
        {
            var tempFile = Path.Combine(TestSettingsDir, $"test{i}.jrl");
            File.WriteAllText(tempFile, "test");
            service.AddRecentFile(tempFile);
        }

        Assert.True(service.RecentFiles.Count <= 3);
    }

    [Fact]
    public void PropertyChanged_Fires_OnValueChange()
    {
        var service = GetInstance();
        var changedProperties = new List<string>();

        service.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName != null)
                changedProperties.Add(e.PropertyName);
        };

        service.WindowWidth = 1100;

        Assert.Contains("WindowWidth", changedProperties);
    }

    [Fact]
    public void AddRecentFile_MovesExistingToFront()
    {
        var service = GetInstance();

        var file1 = Path.Combine(TestSettingsDir, "first.jrl");
        var file2 = Path.Combine(TestSettingsDir, "second.jrl");
        File.WriteAllText(file1, "test");
        File.WriteAllText(file2, "test");

        service.AddRecentFile(file1);
        service.AddRecentFile(file2);
        Assert.Equal(file2, service.RecentFiles[0]);

        service.AddRecentFile(file1);
        Assert.Equal(file1, service.RecentFiles[0]);
    }
}

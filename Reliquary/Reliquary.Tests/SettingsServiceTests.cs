using System.Reflection;
using PlaceableEditor.Services;
using Radoub.TestUtilities.Bases;
using Radoub.TestUtilities.Helpers;

namespace PlaceableEditor.Tests;

/// <summary>
/// Tests for Reliquary's SettingsService. The shared singleton/window/recent-files/log-retention
/// contract lives in <see cref="ToolSettingsServiceTestBase{TService}"/> (#2464); only
/// Reliquary-specific behavior (tool identity, browser panel width) is below.
/// Deriving from the base also gives Reliquary proper per-test isolation it previously lacked.
/// </summary>
public class SettingsServiceTests : ToolSettingsServiceTestBase<SettingsService>
{
    protected override string SettingsEnvironmentVariable => "RELIQUARY_SETTINGS_DIR";
    protected override string ToolDirPrefix => "Reliquary";
    protected override string RecentFileExtension => ".utp";

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

    // ---- Reliquary-specific ----

    [Fact]
    public void ToolName_IsReliquary()
    {
        var settings = GetInstance();
        var toolNameProp = settings.GetType()
            .GetProperty("ToolName", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(toolNameProp);
        Assert.Equal("Reliquary", toolNameProp!.GetValue(settings));
    }

    [Fact]
    public void SettingsEnvironmentVariable_IsReliquary()
    {
        var settings = GetInstance();
        var envVarProp = settings.GetType()
            .GetProperty("SettingsEnvironmentVariable", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(envVarProp);
        Assert.Equal("RELIQUARY_SETTINGS_DIR", envVarProp!.GetValue(settings));
    }

    [Fact]
    public void BrowserPanelWidth_ClampsMinimum()
    {
        var settings = GetInstance();
        settings.BrowserPanelWidth = 50;
        Assert.True(settings.BrowserPanelWidth >= 150);
    }

    [Fact]
    public void BrowserPanelWidth_ClampsMaximum()
    {
        var settings = GetInstance();
        settings.BrowserPanelWidth = 9999;
        Assert.True(settings.BrowserPanelWidth <= 500);
    }
}

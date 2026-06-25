using System.Reflection;
using ItemEditor.Services;
using Radoub.TestUtilities.Bases;
using Radoub.TestUtilities.Helpers;

namespace ItemEditor.Tests;

/// <summary>
/// Tests for Relique's SettingsService. The shared singleton/window/recent-files/log-retention
/// contract lives in <see cref="ToolSettingsServiceTestBase{TService}"/> (#2464); only
/// Relique-specific behavior (tool identity, browser panel width, item-editor prefs) is below.
/// Deriving from the base also gives Relique proper per-test isolation it previously lacked.
/// </summary>
public class SettingsServiceTests : ToolSettingsServiceTestBase<SettingsService>
{
    protected override string SettingsEnvironmentVariable => "RELIQUE_SETTINGS_DIR";
    protected override string ToolDirPrefix => "Relique";
    protected override string RecentFileExtension => ".uti";

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

    // ---- Relique-specific ----

    [Fact]
    public void ToolName_IsRelique()
    {
        var settings = GetInstance();
        var toolNameProp = settings.GetType()
            .GetProperty("ToolName", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(toolNameProp);
        Assert.Equal("Relique", toolNameProp!.GetValue(settings));
    }

    [Fact]
    public void SettingsEnvironmentVariable_IsRelique()
    {
        var settings = GetInstance();
        var envVarProp = settings.GetType()
            .GetProperty("SettingsEnvironmentVariable", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(envVarProp);
        Assert.Equal("RELIQUE_SETTINGS_DIR", envVarProp!.GetValue(settings));
    }

    [Fact]
    public void BrowserPanelWidth_ClampsMinimum()
    {
        var settings = GetInstance();
        settings.BrowserPanelWidth = 50;
        Assert.Equal(150, settings.BrowserPanelWidth);
    }

    [Fact]
    public void BrowserPanelWidth_ClampsMaximum()
    {
        var settings = GetInstance();
        settings.BrowserPanelWidth = 999;
        Assert.Equal(500, settings.BrowserPanelWidth);
    }

    [Fact]
    public void BrowserPanelWidth_AcceptsValidValue()
    {
        var settings = GetInstance();
        settings.BrowserPanelWidth = 300;
        Assert.Equal(300, settings.BrowserPanelWidth);
    }

    [Fact]
    public void OpenInEditorAfterCreate_DefaultsToTrue()
    {
        Assert.True(GetInstance().OpenInEditorAfterCreate);
    }

    [Fact]
    public void PreviewGender_RoundTripsValue()
    {
        var settings = GetInstance();
        settings.PreviewGender = 1;
        Assert.Equal(1, settings.PreviewGender);
        settings.PreviewGender = 0;
        Assert.Equal(0, settings.PreviewGender);
    }
}

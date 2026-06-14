using System;
using System.IO;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Tests for the base-class BrowserPanelWidth / BrowserPanelVisible settings (#2356).
/// Exercises the base behavior through a minimal concrete subclass.
/// </summary>
public class BaseToolSettingsBrowserPanelTests : IDisposable
{
    private const string EnvVar = "RADOUBUI_BROWSERPANEL_TEST_DIR";
    private readonly string _dir;

    public BaseToolSettingsBrowserPanelTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"RadoubUI_BrowserPanel_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable(EnvVar, _dir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvVar, null);
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* ignore */ }
    }

    [Fact]
    public void Defaults_WidthAndVisible()
    {
        var service = new TestSettingsService();
        Assert.Equal(250, service.BrowserPanelWidth);
        Assert.True(service.BrowserPanelVisible);
    }

    [Fact]
    public void Width_RoundTripsThroughDisk()
    {
        var service = new TestSettingsService();
        service.BrowserPanelWidth = 333;
        service.BrowserPanelVisible = false;

        var reloaded = new TestSettingsService();
        Assert.Equal(333, reloaded.BrowserPanelWidth);
        Assert.False(reloaded.BrowserPanelVisible);
    }

    [Fact]
    public void Width_ClampedToMinMax()
    {
        var service = new TestSettingsService();

        service.BrowserPanelWidth = 10;   // below min (150)
        Assert.Equal(150, service.BrowserPanelWidth);

        service.BrowserPanelWidth = 9000; // above max (500)
        Assert.Equal(500, service.BrowserPanelWidth);
    }

    [Fact]
    public void Width_RespectsOverriddenClamp()
    {
        var service = new WideClampSettingsService();

        service.BrowserPanelWidth = 10;
        Assert.Equal(200, service.BrowserPanelWidth);   // overridden min

        service.BrowserPanelWidth = 9000;
        Assert.Equal(900, service.BrowserPanelWidth);   // overridden max
    }

    private sealed class TestSettingsService : BaseToolSettingsService<TestSettingsService.Data>
    {
        public TestSettingsService() => Initialize();
        protected override string ToolName => "BrowserTool";
        protected override string SettingsEnvironmentVariable => EnvVar;
        protected override string SettingsFileName => "BrowserToolSettings.json";
        protected override void LoadToolSettings(Data settings) { }
        protected override void SaveToolSettings(Data settings) { }
        public sealed class Data : BaseSettingsData { }
    }

    private sealed class WideClampSettingsService : BaseToolSettingsService<WideClampSettingsService.Data>
    {
        public WideClampSettingsService() => Initialize();
        protected override string ToolName => "WideTool";
        protected override string SettingsEnvironmentVariable => EnvVar;
        protected override string SettingsFileName => "WideToolSettings.json";
        protected override double MinBrowserPanelWidth => 200;
        protected override double MaxBrowserPanelWidth => 900;
        protected override void LoadToolSettings(Data settings) { }
        protected override void SaveToolSettings(Data settings) { }
        public sealed class Data : BaseSettingsData { }
    }
}

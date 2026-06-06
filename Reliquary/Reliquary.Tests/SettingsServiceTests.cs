using System.Reflection;
using PlaceableEditor.Services;

namespace PlaceableEditor.Tests;

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
    public void ToolName_IsReliquary()
    {
        var settings = SettingsService.Instance;
        var toolNameProp = settings.GetType()
            .GetProperty("ToolName", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(toolNameProp);
        Assert.Equal("Reliquary", toolNameProp!.GetValue(settings));
    }

    [Fact]
    public void SettingsEnvironmentVariable_IsReliquary()
    {
        var settings = SettingsService.Instance;
        var envVarProp = settings.GetType()
            .GetProperty("SettingsEnvironmentVariable", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(envVarProp);
        Assert.Equal("RELIQUARY_SETTINGS_DIR", envVarProp!.GetValue(settings));
    }

    [Fact]
    public void BrowserPanelWidth_ClampsMinimum()
    {
        var settings = SettingsService.Instance;
        settings.BrowserPanelWidth = 50;
        Assert.True(settings.BrowserPanelWidth >= 150);
    }

    [Fact]
    public void BrowserPanelWidth_ClampsMaximum()
    {
        var settings = SettingsService.Instance;
        settings.BrowserPanelWidth = 9999;
        Assert.True(settings.BrowserPanelWidth <= 500);
    }
}

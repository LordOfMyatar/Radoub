using ItemEditor.Services;

namespace ItemEditor.Tests;

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
    public void BrowserPanelWidth_ClampsMinimum()
    {
        var settings = SettingsService.Instance;
        settings.BrowserPanelWidth = 50;
        Assert.Equal(150, settings.BrowserPanelWidth);
    }

    [Fact]
    public void BrowserPanelWidth_ClampsMaximum()
    {
        var settings = SettingsService.Instance;
        settings.BrowserPanelWidth = 999;
        Assert.Equal(500, settings.BrowserPanelWidth);
    }

    [Fact]
    public void BrowserPanelWidth_AcceptsValidValue()
    {
        var settings = SettingsService.Instance;
        settings.BrowserPanelWidth = 300;
        Assert.Equal(300, settings.BrowserPanelWidth);
    }
}

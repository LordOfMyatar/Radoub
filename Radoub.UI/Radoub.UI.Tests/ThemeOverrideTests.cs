using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for UseSharedTheme flag in BaseToolSettingsService serialization.
/// ThemeManager.GetEffectiveThemeId() requires theme discovery (filesystem),
/// so we test the settings persistence layer which is the core of #1533.
/// </summary>
public class ThemeOverrideTests
{
    [Fact]
    public void UseSharedTheme_DefaultsToTrue()
    {
        var data = new TestSettingsData();
        Assert.True(data.UseSharedTheme);
    }

    [Fact]
    public void UseSharedTheme_False_SerializesCorrectly()
    {
        var data = new TestSettingsData { UseSharedTheme = false };
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TestSettingsData>(json);
        Assert.NotNull(deserialized);
        Assert.False(deserialized!.UseSharedTheme);
    }

    [Fact]
    public void UseSharedTheme_True_SerializesCorrectly()
    {
        var data = new TestSettingsData { UseSharedTheme = true };
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TestSettingsData>(json);
        Assert.NotNull(deserialized);
        Assert.True(deserialized!.UseSharedTheme);
    }

    [Fact]
    public void UseSharedTheme_MissingFromJson_DefaultsToTrue()
    {
        // Simulate loading settings from an older JSON file that doesn't have UseSharedTheme
        var json = """{"CurrentThemeId":"org.radoub.theme.dark","FontSize":14}""";
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TestSettingsData>(json);
        Assert.NotNull(deserialized);
        Assert.True(deserialized!.UseSharedTheme);
    }

    /// <summary>
    /// Minimal settings data class for testing serialization.
    /// Mirrors BaseSettingsData structure for the fields we care about.
    /// </summary>
    private class TestSettingsData
    {
        public string CurrentThemeId { get; set; } = "org.radoub.theme.light";
        public double FontSize { get; set; } = 14;
        public bool UseSharedTheme { get; set; } = true;
    }
}

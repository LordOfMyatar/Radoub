using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for legacy settings deserialization after theme unification (#2006).
/// Verifies that old JSON files with UseSharedTheme/CurrentThemeId/FontSize fields
/// deserialize gracefully (fields ignored) without errors.
/// </summary>
public class ThemeOverrideTests
{
    [Fact]
    public void LegacyJson_WithUseSharedTheme_DeserializesWithoutError()
    {
        var json = """{"UseSharedTheme":false,"CurrentThemeId":"org.radoub.theme.dark","FontSize":16}""";
        var deserialized = JsonSerializer.Deserialize<LegacySettingsData>(json);
        Assert.NotNull(deserialized);
    }

    [Fact]
    public void LegacyJson_WithoutThemeFields_DeserializesWithoutError()
    {
        var json = """{"WindowLeft":100,"WindowTop":100}""";
        var deserialized = JsonSerializer.Deserialize<LegacySettingsData>(json);
        Assert.NotNull(deserialized);
    }

    [Fact]
    public void LegacyFields_DoNotSerializeWhenDefault()
    {
        var data = new LegacySettingsData();
        var json = JsonSerializer.Serialize(data);
        Assert.DoesNotContain("UseSharedTheme", json);
        Assert.DoesNotContain("CurrentThemeId", json);
        Assert.DoesNotContain("FontFamily", json);
    }

    /// <summary>
    /// Mirrors BaseSettingsData legacy fields — kept for deserialization of old JSON.
    /// </summary>
    private class LegacySettingsData
    {
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double FontSize { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FontFamily { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CurrentThemeId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool UseSharedTheme { get; set; }
    }
}

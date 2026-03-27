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
        // Old JSON from before theme unification — should not throw
        var json = """{"UseSharedTheme":false,"CurrentThemeId":"org.radoub.theme.dark","FontSize":16}""";
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Services.BaseToolSettingsService.BaseSettingsData>(json);
        Assert.NotNull(deserialized);
    }

    [Fact]
    public void LegacyJson_WithoutThemeFields_DeserializesWithoutError()
    {
        // New JSON without theme/font fields
        var json = """{"WindowLeft":100,"WindowTop":100}""";
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Services.BaseToolSettingsService.BaseSettingsData>(json);
        Assert.NotNull(deserialized);
    }

    [Fact]
    public void BaseSettingsData_DoesNotSerializeThemeFields()
    {
        // Theme/font fields should not appear in serialized output (WhenWritingDefault/Null)
        var data = new Services.BaseToolSettingsService.BaseSettingsData();
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        Assert.DoesNotContain("UseSharedTheme", json);
        Assert.DoesNotContain("CurrentThemeId", json);
        Assert.DoesNotContain("FontFamily", json);
    }
}

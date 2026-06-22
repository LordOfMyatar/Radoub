using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

public class SharedPaletteCachePropertiesTests
{
    [Fact]
    public void SharedPaletteCacheItem_HasPropertiesDisplay()
    {
        var item = new SharedPaletteCacheItem
        {
            ResRef = "nw_wswdg001",
            DisplayName = "Dagger",
            PropertiesDisplay = "Enhancement +1, Keen"
        };

        Assert.Equal("Enhancement +1, Keen", item.PropertiesDisplay);
    }

    [Fact]
    public void SharedPaletteCacheItem_PropertiesDisplay_DefaultsToEmpty()
    {
        var item = new SharedPaletteCacheItem();
        Assert.Equal(string.Empty, item.PropertiesDisplay);
    }

    [Fact]
    public void PaletteId_RoundTripsThroughJson()
    {
        var item = new SharedPaletteCacheItem { ResRef = "longsword", PaletteId = 7 };
        var json = System.Text.Json.JsonSerializer.Serialize(item);
        var restored = System.Text.Json.JsonSerializer.Deserialize<SharedPaletteCacheItem>(json)!;
        Assert.Equal((byte?)7, restored.PaletteId);
    }

    [Fact]
    public void PaletteId_NullRoundTripsThroughJson()
    {
        var item = new SharedPaletteCacheItem { ResRef = "x", PaletteId = null };
        var json = System.Text.Json.JsonSerializer.Serialize(item);
        var restored = System.Text.Json.JsonSerializer.Deserialize<SharedPaletteCacheItem>(json)!;
        Assert.Null(restored.PaletteId);
    }
}

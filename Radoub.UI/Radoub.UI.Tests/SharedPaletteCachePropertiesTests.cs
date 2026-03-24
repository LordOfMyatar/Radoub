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
}

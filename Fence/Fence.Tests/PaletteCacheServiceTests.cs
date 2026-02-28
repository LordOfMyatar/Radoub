using MerchantEditor.Services;

namespace Fence.Tests;

/// <summary>
/// Tests for PaletteCacheService cache lifecycle.
/// </summary>
public class PaletteCacheServiceTests : IDisposable
{
    private readonly string _testDir;

    public PaletteCacheServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"FencePalette_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void Constructor_CreatesService()
    {
        var service = new PaletteCacheService();

        Assert.NotNull(service);
    }

    [Fact]
    public void HasValidCache_NoCache_ReturnsFalse()
    {
        var service = new PaletteCacheService();

        Assert.False(service.HasValidCache());
    }

    [Fact]
    public void LoadCache_NoCache_ReturnsNull()
    {
        var service = new PaletteCacheService();

        var result = service.LoadCache();

        Assert.Null(result);
    }

    [Fact]
    public void GetCacheInfo_NoCache_ReturnsNull()
    {
        var service = new PaletteCacheService();

        var info = service.GetCacheInfo();

        Assert.Null(info);
    }

    [Fact]
    public void ClearCache_WhenNoCache_DoesNotThrow()
    {
        var service = new PaletteCacheService();

        var exception = Record.Exception(() => service.ClearCache());

        Assert.Null(exception);
    }

    [Fact]
    public void CachedPaletteItem_DefaultValues_AreCorrect()
    {
        var item = new CachedPaletteItem();

        Assert.Equal(string.Empty, item.ResRef);
        Assert.Equal(string.Empty, item.DisplayName);
        Assert.Equal(string.Empty, item.BaseItemType);
        Assert.Equal(0, item.BaseItemIndex);
        Assert.Equal(0, item.BaseValue);
        Assert.Equal(string.Empty, item.Tag);
        Assert.False(item.IsStandard);
    }

    [Fact]
    public void CachedPaletteItem_SetProperties_PreservesValues()
    {
        var item = new CachedPaletteItem
        {
            ResRef = "nw_wswls001",
            DisplayName = "Longsword",
            BaseItemType = "Longsword",
            BaseItemIndex = 4,
            BaseValue = 15,
            Tag = "NW_WSWLS001",
            IsStandard = true
        };

        Assert.Equal("nw_wswls001", item.ResRef);
        Assert.Equal("Longsword", item.DisplayName);
        Assert.Equal(4, item.BaseItemIndex);
        Assert.Equal(15, item.BaseValue);
        Assert.True(item.IsStandard);
    }
}

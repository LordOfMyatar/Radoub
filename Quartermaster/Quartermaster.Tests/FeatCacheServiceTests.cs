using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for FeatCacheService in-memory cache operations.
/// Covers feat caching, class/race granted feat caching, idempotent loading,
/// cache clearing, and memory state tracking.
/// </summary>
public class FeatCacheServiceTests
{
    private FeatCacheService CreateService() => new FeatCacheService();

    private CachedFeatEntry CreateEntry(int id, string name = "TestFeat") => new CachedFeatEntry
    {
        FeatId = id,
        Name = name,
        Description = $"Description for {name}",
        Category = 0,
        IsUniversal = false
    };

    #region Initial State

    [Fact]
    public void NewService_IsNotLoaded()
    {
        var service = CreateService();
        Assert.False(service.IsMemoryCacheLoaded);
    }

    [Fact]
    public void NewService_GetAllFeatIds_ReturnsNull()
    {
        var service = CreateService();
        Assert.Null(service.GetAllFeatIds());
    }

    [Fact]
    public void NewService_TryGetFeat_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.TryGetFeat(0, out var entry));
        Assert.Null(entry);
    }

    #endregion

    #region CacheFeat / TryGetFeat

    [Fact]
    public void CacheFeat_ThenTryGet_ReturnsEntry()
    {
        var service = CreateService();
        var entry = CreateEntry(42, "Power Attack");

        service.CacheFeat(entry);

        Assert.True(service.TryGetFeat(42, out var result));
        Assert.NotNull(result);
        Assert.Equal("Power Attack", result!.Name);
        Assert.Equal("Description for Power Attack", result.Description);
    }

    [Fact]
    public void CacheFeat_OverwritesExisting()
    {
        var service = CreateService();
        service.CacheFeat(CreateEntry(1, "Original"));
        service.CacheFeat(CreateEntry(1, "Updated"));

        Assert.True(service.TryGetFeat(1, out var result));
        Assert.Equal("Updated", result!.Name);
    }

    [Fact]
    public void CacheFeat_SetsMemoryCacheLoaded()
    {
        var service = CreateService();
        Assert.False(service.IsMemoryCacheLoaded);

        service.CacheFeat(CreateEntry(1));
        Assert.True(service.IsMemoryCacheLoaded);
    }

    [Fact]
    public void TryGetFeat_MissingId_ReturnsFalse()
    {
        var service = CreateService();
        service.CacheFeat(CreateEntry(1));

        Assert.False(service.TryGetFeat(999, out _));
    }

    #endregion

    #region SetAllFeatIds / GetAllFeatIds

    [Fact]
    public void SetAllFeatIds_ThenGet_ReturnsIds()
    {
        var service = CreateService();
        var ids = new List<int> { 1, 5, 10, 42 };

        service.SetAllFeatIds(ids);

        var result = service.GetAllFeatIds();
        Assert.NotNull(result);
        Assert.Equal(4, result!.Count);
        Assert.Contains(42, result);
    }

    #endregion

    #region Class Granted Feats

    [Fact]
    public void CacheClassGrantedFeats_ThenTryGet_ReturnsFeats()
    {
        var service = CreateService();
        var feats = new HashSet<int> { 10, 20, 30 };

        service.CacheClassGrantedFeats(4, feats); // Fighter class

        Assert.True(service.TryGetClassGrantedFeats(4, out var result));
        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
        Assert.Contains(20, result);
    }

    [Fact]
    public void TryGetClassGrantedFeats_Uncached_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.TryGetClassGrantedFeats(4, out _));
    }

    [Fact]
    public void TryGetClassGrantedFeats_WrongClass_ReturnsFalse()
    {
        var service = CreateService();
        service.CacheClassGrantedFeats(4, new HashSet<int> { 10 });

        Assert.False(service.TryGetClassGrantedFeats(5, out _));
    }

    [Fact]
    public void CacheClassGrantedFeats_OverwritesPrevious()
    {
        var service = CreateService();
        service.CacheClassGrantedFeats(4, new HashSet<int> { 10, 20 });
        service.CacheClassGrantedFeats(4, new HashSet<int> { 30 });

        Assert.True(service.TryGetClassGrantedFeats(4, out var result));
        Assert.Single(result!);
        Assert.Contains(30, result);
    }

    #endregion

    #region Race Granted Feats

    [Fact]
    public void CacheRaceGrantedFeats_ThenTryGet_ReturnsFeats()
    {
        var service = CreateService();
        var feats = new HashSet<int> { 100, 200 };

        service.CacheRaceGrantedFeats(6, feats); // Human race

        Assert.True(service.TryGetRaceGrantedFeats(6, out var result));
        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
    }

    [Fact]
    public void TryGetRaceGrantedFeats_Uncached_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.TryGetRaceGrantedFeats(6, out _));
    }

    #endregion

    #region ClearCache

    [Fact]
    public void ClearCache_ResetsAllInMemoryState()
    {
        var service = CreateService();

        // Populate all caches
        service.CacheFeat(CreateEntry(1));
        service.SetAllFeatIds(new List<int> { 1 });
        service.CacheClassGrantedFeats(4, new HashSet<int> { 10 });
        service.CacheRaceGrantedFeats(6, new HashSet<int> { 100 });

        // Verify populated
        Assert.True(service.IsMemoryCacheLoaded);
        Assert.NotNull(service.GetAllFeatIds());

        service.ClearCache();

        // Verify all cleared
        Assert.False(service.IsMemoryCacheLoaded);
        Assert.Null(service.GetAllFeatIds());
        Assert.False(service.TryGetFeat(1, out _));
        Assert.False(service.TryGetClassGrantedFeats(4, out _));
        Assert.False(service.TryGetRaceGrantedFeats(6, out _));
    }

    #endregion

    #region Idempotent Loading

    [Fact]
    public void CacheFeat_LazyInitializesDict()
    {
        var service = CreateService();
        // First CacheFeat call should create the dictionary
        service.CacheFeat(CreateEntry(1));
        Assert.True(service.IsMemoryCacheLoaded);

        // Second CacheFeat should add to same dictionary
        service.CacheFeat(CreateEntry(2));
        Assert.True(service.TryGetFeat(1, out _));
        Assert.True(service.TryGetFeat(2, out _));
    }

    [Fact]
    public void CacheClassGrantedFeats_LazyInitializesDict()
    {
        var service = CreateService();
        service.CacheClassGrantedFeats(0, new HashSet<int> { 1 });
        service.CacheClassGrantedFeats(1, new HashSet<int> { 2 });

        Assert.True(service.TryGetClassGrantedFeats(0, out _));
        Assert.True(service.TryGetClassGrantedFeats(1, out _));
    }

    [Fact]
    public void CacheRaceGrantedFeats_LazyInitializesDict()
    {
        var service = CreateService();
        service.CacheRaceGrantedFeats(0, new HashSet<int> { 1 });
        service.CacheRaceGrantedFeats(1, new HashSet<int> { 2 });

        Assert.True(service.TryGetRaceGrantedFeats(0, out _));
        Assert.True(service.TryGetRaceGrantedFeats(1, out _));
    }

    #endregion

    #region CachedFeatEntry Properties

    [Fact]
    public void CachedFeatEntry_DefaultValues()
    {
        var entry = new CachedFeatEntry();
        Assert.Equal(0, entry.FeatId);
        Assert.Equal("", entry.Name);
        Assert.Equal("", entry.Description);
        Assert.Equal(0, entry.Category);
        Assert.False(entry.IsUniversal);
    }

    [Fact]
    public void CachedFeatEntry_RoundTripValues()
    {
        var entry = new CachedFeatEntry
        {
            FeatId = 42,
            Name = "Power Attack",
            Description = "Trade AB for damage",
            Category = 3,
            IsUniversal = true
        };

        Assert.Equal(42, entry.FeatId);
        Assert.Equal("Power Attack", entry.Name);
        Assert.Equal("Trade AB for damage", entry.Description);
        Assert.Equal(3, entry.Category);
        Assert.True(entry.IsUniversal);
    }

    #endregion
}

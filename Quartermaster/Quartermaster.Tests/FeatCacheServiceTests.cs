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
        Assert.NotNull(result);
        Assert.Single(result);
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

    #region Multiple Feats and Cache Isolation

    [Fact]
    public void CacheFeat_MultipleFeats_AllRetrievable()
    {
        var service = CreateService();
        for (int i = 0; i < 50; i++)
            service.CacheFeat(CreateEntry(i, $"Feat_{i}"));

        for (int i = 0; i < 50; i++)
        {
            Assert.True(service.TryGetFeat(i, out var result));
            Assert.Equal($"Feat_{i}", result!.Name);
        }
    }

    [Fact]
    public void SetAllFeatIds_DoesNotAffectFeatCache()
    {
        var service = CreateService();
        service.SetAllFeatIds(new List<int> { 1, 2, 3 });

        // AllFeatIds is just an ID list — doesn't populate feat cache
        Assert.False(service.IsMemoryCacheLoaded);
        Assert.False(service.TryGetFeat(1, out _));
    }

    [Fact]
    public void SetAllFeatIds_Overwrite_ReplacesOldList()
    {
        var service = CreateService();
        service.SetAllFeatIds(new List<int> { 1, 2, 3 });
        service.SetAllFeatIds(new List<int> { 10, 20 });

        var result = service.GetAllFeatIds();
        Assert.Equal(2, result!.Count);
        Assert.Contains(10, result);
        Assert.DoesNotContain(1, result);
    }

    #endregion

    #region Three Cache Independence

    [Fact]
    public void ThreeCaches_AreIndependent()
    {
        var service = CreateService();

        // Populate feat cache only
        service.CacheFeat(CreateEntry(1, "Power Attack"));

        // Class and race caches should still be empty
        Assert.False(service.TryGetClassGrantedFeats(0, out _));
        Assert.False(service.TryGetRaceGrantedFeats(0, out _));

        // Populate class cache only
        service.CacheClassGrantedFeats(4, new HashSet<int> { 10 });

        // Race cache should still be empty
        Assert.False(service.TryGetRaceGrantedFeats(0, out _));

        // Populate race cache
        service.CacheRaceGrantedFeats(6, new HashSet<int> { 100 });

        // All three should be populated independently
        Assert.True(service.TryGetFeat(1, out _));
        Assert.True(service.TryGetClassGrantedFeats(4, out _));
        Assert.True(service.TryGetRaceGrantedFeats(6, out _));
    }

    [Fact]
    public void ClearCache_ThenRepopulate_Works()
    {
        var service = CreateService();

        service.CacheFeat(CreateEntry(1));
        service.CacheClassGrantedFeats(4, new HashSet<int> { 10 });
        service.CacheRaceGrantedFeats(6, new HashSet<int> { 100 });
        service.SetAllFeatIds(new List<int> { 1 });

        service.ClearCache();

        // Repopulate after clear
        service.CacheFeat(CreateEntry(2, "Toughness"));
        service.CacheClassGrantedFeats(5, new HashSet<int> { 20 });
        service.CacheRaceGrantedFeats(0, new HashSet<int> { 200 });
        service.SetAllFeatIds(new List<int> { 2 });

        Assert.True(service.IsMemoryCacheLoaded);
        Assert.True(service.TryGetFeat(2, out var entry));
        Assert.Equal("Toughness", entry!.Name);
        Assert.False(service.TryGetFeat(1, out _)); // Old feat gone
        Assert.True(service.TryGetClassGrantedFeats(5, out _));
        Assert.False(service.TryGetClassGrantedFeats(4, out _)); // Old class gone
        Assert.True(service.TryGetRaceGrantedFeats(0, out _));
        Assert.Contains(2, service.GetAllFeatIds()!);
    }

    #endregion

    #region SaveCacheToDisk Edge Cases

    [Fact]
    public async Task SaveCacheToDiskAsync_EmptyCache_DoesNotThrow()
    {
        var service = CreateService();
        // No feats cached — should silently return
        await service.SaveCacheToDiskAsync();
    }

    #endregion

    #region Race Granted Feats Edge Cases

    [Fact]
    public void CacheRaceGrantedFeats_OverwritesPrevious()
    {
        var service = CreateService();
        service.CacheRaceGrantedFeats(6, new HashSet<int> { 100, 200 });
        service.CacheRaceGrantedFeats(6, new HashSet<int> { 300 });

        Assert.True(service.TryGetRaceGrantedFeats(6, out var result));
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Contains(300, result);
    }

    [Fact]
    public void TryGetRaceGrantedFeats_WrongRace_ReturnsFalse()
    {
        var service = CreateService();
        service.CacheRaceGrantedFeats(6, new HashSet<int> { 100 });

        Assert.False(service.TryGetRaceGrantedFeats(0, out _));
    }

    #endregion

    #region Multiple Classes and Races

    [Fact]
    public void CacheClassGrantedFeats_MultipleClasses_IndependentLookup()
    {
        var service = CreateService();
        service.CacheClassGrantedFeats(4, new HashSet<int> { 10, 11 }); // Fighter
        service.CacheClassGrantedFeats(5, new HashSet<int> { 20 });     // Rogue
        service.CacheClassGrantedFeats(10, new HashSet<int> { 30, 31, 32 }); // Wizard

        Assert.True(service.TryGetClassGrantedFeats(4, out var fighter));
        Assert.Equal(2, fighter!.Count);

        Assert.True(service.TryGetClassGrantedFeats(5, out var rogue));
        Assert.Single(rogue!);

        Assert.True(service.TryGetClassGrantedFeats(10, out var wizard));
        Assert.Equal(3, wizard!.Count);
    }

    [Fact]
    public void CacheRaceGrantedFeats_MultipleRaces_IndependentLookup()
    {
        var service = CreateService();
        service.CacheRaceGrantedFeats(0, new HashSet<int> { 100 });  // Dwarf
        service.CacheRaceGrantedFeats(1, new HashSet<int> { 200, 201 }); // Elf
        service.CacheRaceGrantedFeats(6, new HashSet<int> { 300 });  // Human

        Assert.True(service.TryGetRaceGrantedFeats(0, out var dwarf));
        Assert.Single(dwarf!);

        Assert.True(service.TryGetRaceGrantedFeats(1, out var elf));
        Assert.Equal(2, elf!.Count);

        Assert.True(service.TryGetRaceGrantedFeats(6, out var human));
        Assert.Single(human!);
    }

    #endregion

    #region Empty Set Caching

    [Fact]
    public void CacheClassGrantedFeats_EmptySet_StillCaches()
    {
        var service = CreateService();
        service.CacheClassGrantedFeats(4, new HashSet<int>());

        Assert.True(service.TryGetClassGrantedFeats(4, out var result));
        Assert.Empty(result!);
    }

    [Fact]
    public void CacheRaceGrantedFeats_EmptySet_StillCaches()
    {
        var service = CreateService();
        service.CacheRaceGrantedFeats(6, new HashSet<int>());

        Assert.True(service.TryGetRaceGrantedFeats(6, out var result));
        Assert.Empty(result!);
    }

    [Fact]
    public void SetAllFeatIds_EmptyList_SetsEmptyList()
    {
        var service = CreateService();
        service.SetAllFeatIds(new List<int>());

        var result = service.GetAllFeatIds();
        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    #endregion
}

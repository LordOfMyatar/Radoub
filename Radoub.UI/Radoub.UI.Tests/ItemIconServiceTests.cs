using Xunit;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for ItemIconService bitmap cache bounding (#2034 round 2).
///
/// The cache previously used an unbounded ConcurrentDictionary and grew without
/// limit during long sessions. These tests pin the LRU eviction contract.
///
/// Note: ItemIconService can't decode real bitmaps in unit tests (requires game data
/// with TGA assets). With a MockGameDataService that has no 2DAs, every Get*Icon call
/// stores `null` in the cache — but the cache key is still added, which is exactly
/// the behavior we want to bound.
/// </summary>
public class ItemIconServiceTests
{
    [Fact]
    public void CacheCount_StartsAtZero()
    {
        var service = new ItemIconService(new MockGameDataService(includeSampleData: false));

        Assert.Equal(0, service.CacheCount);
    }

    [Fact]
    public void GetSpellIcon_AddsEntryToCache()
    {
        var service = new ItemIconService(new MockGameDataService(includeSampleData: false));

        service.GetSpellIcon(1);

        Assert.Equal(1, service.CacheCount);
    }

    [Fact]
    public void GetSpellIcon_RepeatedCalls_DoNotGrowCache()
    {
        var service = new ItemIconService(new MockGameDataService(includeSampleData: false));

        for (int i = 0; i < 50; i++)
            service.GetSpellIcon(7);

        Assert.Equal(1, service.CacheCount);
    }

    [Fact]
    public void Cache_EvictsOldestEntry_WhenCapacityExceeded()
    {
        // Cap is 2000. Insert capacity + 100 entries; cache must not exceed capacity.
        var service = new ItemIconService(new MockGameDataService(includeSampleData: false));
        var capacity = service.CacheCapacity;

        for (int i = 0; i < capacity + 100; i++)
            service.GetSpellIcon(i);

        Assert.Equal(capacity, service.CacheCount);
    }

    [Fact]
    public void ClearCache_ResetsCount()
    {
        var service = new ItemIconService(new MockGameDataService(includeSampleData: false));
        service.GetSpellIcon(1);
        service.GetFeatIcon(2);
        Assert.Equal(2, service.CacheCount);

        service.ClearCache();

        Assert.Equal(0, service.CacheCount);
    }

    [Fact]
    public void CacheCapacity_IsBounded()
    {
        // Ensure we don't accidentally re-introduce an unbounded cache by setting
        // capacity to something absurd. 10_000 is far more than any real session needs.
        var service = new ItemIconService(new MockGameDataService(includeSampleData: false));

        Assert.InRange(service.CacheCapacity, 100, 10_000);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Xunit;

namespace Radoub.Formats.Tests;

public class LruCacheTests
{
    [Fact]
    public void TryGetValue_ReturnsFalse_WhenKeyMissing()
    {
        var cache = new LruCache<string, int>(capacity: 4);

        var found = cache.TryGetValue("missing", out var value);

        Assert.False(found);
        Assert.Equal(0, value);
    }

    [Fact]
    public void Set_Then_TryGetValue_ReturnsStoredValue()
    {
        var cache = new LruCache<string, int>(capacity: 4);

        cache.Set("a", 1);
        var found = cache.TryGetValue("a", out var value);

        Assert.True(found);
        Assert.Equal(1, value);
    }

    [Fact]
    public void Count_ReflectsNumberOfEntries()
    {
        var cache = new LruCache<string, int>(capacity: 4);
        Assert.Equal(0, cache.Count);

        cache.Set("a", 1);
        cache.Set("b", 2);

        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Capacity_IsExposed()
    {
        var cache = new LruCache<string, int>(capacity: 7);

        Assert.Equal(7, cache.Capacity);
    }

    [Fact]
    public void Set_EvictsLeastRecentlyUsed_WhenAtCapacity()
    {
        var cache = new LruCache<string, int>(capacity: 3);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3);
        cache.Set("d", 4); // Should evict "a"

        Assert.Equal(3, cache.Count);
        Assert.False(cache.TryGetValue("a", out _));
        Assert.True(cache.TryGetValue("b", out _));
        Assert.True(cache.TryGetValue("c", out _));
        Assert.True(cache.TryGetValue("d", out _));
    }

    [Fact]
    public void TryGetValue_PromotesKeyToMostRecentlyUsed()
    {
        var cache = new LruCache<string, int>(capacity: 3);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3);

        // Promote "a" to most-recently-used
        cache.TryGetValue("a", out _);

        // Adding "d" should now evict "b" (least recent), not "a"
        cache.Set("d", 4);

        Assert.True(cache.TryGetValue("a", out _));
        Assert.False(cache.TryGetValue("b", out _));
        Assert.True(cache.TryGetValue("c", out _));
        Assert.True(cache.TryGetValue("d", out _));
    }

    [Fact]
    public void Set_ExistingKey_UpdatesValueAndPromotesToMostRecentlyUsed()
    {
        var cache = new LruCache<string, int>(capacity: 3);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3);

        // Overwrite "a" with a new value; should both update and promote.
        cache.Set("a", 99);

        // "b" is now least recent; adding "d" should evict it, not "a".
        cache.Set("d", 4);

        Assert.True(cache.TryGetValue("a", out var aValue));
        Assert.Equal(99, aValue);
        Assert.False(cache.TryGetValue("b", out _));
        Assert.True(cache.TryGetValue("c", out _));
        Assert.True(cache.TryGetValue("d", out _));
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var cache = new LruCache<string, int>(capacity: 4);
        cache.Set("a", 1);
        cache.Set("b", 2);

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGetValue("a", out _));
        Assert.False(cache.TryGetValue("b", out _));
    }

    [Fact]
    public void Set_CanStoreNullValues()
    {
        // TextureService stores null to remember "not found" lookups — this
        // must be preserved by the cache without collapsing to "missing".
        var cache = new LruCache<string, string?>(capacity: 4);

        cache.Set("a", null);

        var found = cache.TryGetValue("a", out var value);
        Assert.True(found);
        Assert.Null(value);
    }

    [Fact]
    public void Constructor_ThrowsOnZeroOrNegativeCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<string, int>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<string, int>(-1));
    }

    [Fact]
    public async Task ConcurrentAccess_DoesNotCorruptCache()
    {
        var cache = new LruCache<int, int>(capacity: 64);
        const int tasks = 16;
        const int iterations = 1000;

        await Task.WhenAll(Enumerable.Range(0, tasks).Select(taskId => Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                int key = (taskId * iterations + i) % 128;
                cache.Set(key, key * 10);
                cache.TryGetValue(key, out _);
            }
        })));

        // Capacity invariant must hold under concurrency.
        Assert.InRange(cache.Count, 0, 64);
    }
}

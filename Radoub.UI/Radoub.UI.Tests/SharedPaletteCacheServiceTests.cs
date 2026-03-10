using Xunit;
using Radoub.UI.Services;

namespace Radoub.UI.Tests;

/// <summary>
/// TDD tests for SharedPaletteCacheService.
/// Tests cover: save/load, validation, aggregation, invalidation, thread safety, and edge cases.
/// </summary>
public class SharedPaletteCacheServiceTests : IDisposable
{
    private readonly string _testCacheDir;
    private readonly SharedPaletteCacheService _service;

    public SharedPaletteCacheServiceTests()
    {
        // Use a unique temp directory per test to avoid cross-test interference
        _testCacheDir = Path.Combine(Path.GetTempPath(), "RadoubTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testCacheDir);
        _service = new SharedPaletteCacheService(_testCacheDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testCacheDir))
        {
            try { Directory.Delete(_testCacheDir, recursive: true); }
            catch { /* cleanup best-effort */ }
        }
    }

    private static List<SharedPaletteCacheItem> CreateTestItems(int count = 3)
    {
        var items = new List<SharedPaletteCacheItem>();
        for (int i = 0; i < count; i++)
        {
            items.Add(new SharedPaletteCacheItem
            {
                ResRef = $"item_{i:D3}",
                Tag = $"TAG_{i:D3}",
                DisplayName = $"Test Item {i}",
                BaseItemTypeName = "Sword",
                BaseItemType = 1,
                BaseValue = (uint)(100 * (i + 1)),
                IsStandard = true
            });
        }
        return items;
    }

    #region Save and Load

    [Fact]
    public async Task SaveSourceCacheAsync_CreatesFileOnDisk()
    {
        var items = CreateTestItems();

        await _service.SaveSourceCacheAsync("bif", items, validationPath: "/game/path");

        // Verify file exists
        var cacheFile = Path.Combine(_testCacheDir, "bif.json");
        Assert.True(File.Exists(cacheFile));
    }

    [Fact]
    public async Task LoadSourceCache_ReturnsItemsAfterSave()
    {
        var items = CreateTestItems(5);
        await _service.SaveSourceCacheAsync("bif", items, validationPath: "/game/path");

        var loaded = _service.LoadSourceCache("bif");

        Assert.NotNull(loaded);
        Assert.Equal(5, loaded.Count);
        Assert.Equal("item_000", loaded[0].ResRef);
        Assert.Equal("TAG_000", loaded[0].Tag);
        Assert.Equal("Test Item 0", loaded[0].DisplayName);
        Assert.Equal("Sword", loaded[0].BaseItemTypeName);
        Assert.Equal(1, loaded[0].BaseItemType);
        Assert.Equal(100u, loaded[0].BaseValue);
        Assert.True(loaded[0].IsStandard);
    }

    [Fact]
    public void LoadSourceCache_ReturnsNull_WhenNoCacheExists()
    {
        var loaded = _service.LoadSourceCache("bif");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task SaveSourceCacheAsync_OverrideSource_CreatesSeparateFile()
    {
        var bifItems = CreateTestItems(2);
        var overrideItems = CreateTestItems(3);

        await _service.SaveSourceCacheAsync("bif", bifItems, validationPath: "/game/path");
        await _service.SaveSourceCacheAsync("override", overrideItems, validationPath: "/nwn/path");

        Assert.True(File.Exists(Path.Combine(_testCacheDir, "bif.json")));
        Assert.True(File.Exists(Path.Combine(_testCacheDir, "override.json")));

        var loadedBif = _service.LoadSourceCache("bif");
        var loadedOverride = _service.LoadSourceCache("override");
        Assert.Equal(2, loadedBif!.Count);
        Assert.Equal(3, loadedOverride!.Count);
    }

    [Fact]
    public async Task SaveSourceCacheAsync_HakSource_UsesSourcePathInFilename()
    {
        var items = CreateTestItems();
        var hakPath = Path.Combine(_testCacheDir, "cep2_top_v2.hak");
        File.WriteAllText(hakPath, "fake hak"); // Create fake HAK file

        await _service.SaveSourceCacheAsync("hak", items, validationPath: hakPath,
            sourceModified: File.GetLastWriteTimeUtc(hakPath));

        // Should create hak_cep2_top_v2.json
        var expectedFile = Path.Combine(_testCacheDir, "hak_cep2_top_v2.json");
        Assert.True(File.Exists(expectedFile));
    }

    [Fact]
    public async Task SaveSourceCacheAsync_EmptyItems_SavesEmptyList()
    {
        await _service.SaveSourceCacheAsync("bif", new List<SharedPaletteCacheItem>(), validationPath: "/path");

        var loaded = _service.LoadSourceCache("bif");
        Assert.NotNull(loaded);
        Assert.Empty(loaded);
    }

    #endregion

    #region Validation

    [Fact]
    public void HasValidSourceCache_ReturnsFalse_WhenNoCacheExists()
    {
        Assert.False(_service.HasValidSourceCache("bif"));
    }

    [Fact]
    public async Task HasValidSourceCache_ReturnsTrue_WhenValidCacheExists()
    {
        await _service.SaveSourceCacheAsync("bif", CreateTestItems(), validationPath: "/game/path");

        Assert.True(_service.HasValidSourceCache("bif"));
    }

    [Fact]
    public async Task HasValidSourceCache_ReturnsFalse_WhenVersionMismatch()
    {
        // Save a cache, then manually corrupt the version
        await _service.SaveSourceCacheAsync("bif", CreateTestItems(), validationPath: "/path");
        var cacheFile = Path.Combine(_testCacheDir, "bif.json");
        var json = File.ReadAllText(cacheFile);
        json = json.Replace("\"Version\":1", "\"Version\":999");
        File.WriteAllText(cacheFile, json);

        Assert.False(_service.HasValidSourceCache("bif"));
    }

    [Fact]
    public async Task HasValidSourceCache_Hak_ReturnsFalse_WhenHakModified()
    {
        var hakPath = Path.Combine(_testCacheDir, "test.hak");
        File.WriteAllText(hakPath, "original content");
        var originalModTime = File.GetLastWriteTimeUtc(hakPath);

        await _service.SaveSourceCacheAsync("hak", CreateTestItems(),
            validationPath: hakPath, sourceModified: originalModTime);

        Assert.True(_service.HasValidSourceCache("hak", hakPath));

        // Modify the HAK file
        await Task.Delay(50); // Ensure different timestamp
        File.WriteAllText(hakPath, "modified content");

        Assert.False(_service.HasValidSourceCache("hak", hakPath));
    }

    [Fact]
    public async Task HasValidSourceCache_ReturnsFalse_WhenCacheFileCorrupted()
    {
        await _service.SaveSourceCacheAsync("bif", CreateTestItems(), validationPath: "/path");
        var cacheFile = Path.Combine(_testCacheDir, "bif.json");
        File.WriteAllText(cacheFile, "not valid json{{{");

        Assert.False(_service.HasValidSourceCache("bif"));
    }

    #endregion

    #region Aggregation

    [Fact]
    public async Task GetAggregatedCache_CombinesAllSources()
    {
        var bifItems = CreateTestItems(2);
        var overrideItems = CreateTestItems(3);

        await _service.SaveSourceCacheAsync("bif", bifItems, validationPath: "/game");
        await _service.SaveSourceCacheAsync("override", overrideItems, validationPath: "/nwn");

        var aggregated = _service.GetAggregatedCache();

        Assert.NotNull(aggregated);
        Assert.Equal(5, aggregated.Count);
    }

    [Fact]
    public void GetAggregatedCache_ReturnsNull_WhenNoCachesExist()
    {
        var aggregated = _service.GetAggregatedCache();
        Assert.Null(aggregated);
    }

    [Fact]
    public async Task GetAggregatedCache_IncludesValidHakCaches()
    {
        var hakPath = Path.Combine(_testCacheDir, "mymod.hak");
        File.WriteAllText(hakPath, "fake hak");

        await _service.SaveSourceCacheAsync("bif", CreateTestItems(2), validationPath: "/game");
        await _service.SaveSourceCacheAsync("hak", CreateTestItems(4),
            validationPath: hakPath, sourceModified: File.GetLastWriteTimeUtc(hakPath));

        var aggregated = _service.GetAggregatedCache();

        Assert.NotNull(aggregated);
        Assert.Equal(6, aggregated.Count);
    }

    [Fact]
    public async Task GetAggregatedCache_SkipsStaleHakCaches()
    {
        var hakPath = Path.Combine(_testCacheDir, "old.hak");
        File.WriteAllText(hakPath, "original");

        await _service.SaveSourceCacheAsync("bif", CreateTestItems(2), validationPath: "/game");
        await _service.SaveSourceCacheAsync("hak", CreateTestItems(4),
            validationPath: hakPath, sourceModified: File.GetLastWriteTimeUtc(hakPath));

        // Modify the HAK so its cache becomes stale
        await Task.Delay(50);
        File.WriteAllText(hakPath, "modified");

        _service.InvalidateAggregatedCache();
        var aggregated = _service.GetAggregatedCache();

        // Should only include BIF items, not stale HAK
        Assert.NotNull(aggregated);
        Assert.Equal(2, aggregated.Count);
    }

    [Fact]
    public async Task GetAggregatedCache_CachesResultInMemory()
    {
        await _service.SaveSourceCacheAsync("bif", CreateTestItems(3), validationPath: "/game");

        var first = _service.GetAggregatedCache();
        var second = _service.GetAggregatedCache();

        // Should return same reference (cached in memory)
        Assert.Same(first, second);
    }

    #endregion

    #region Invalidation and Clearing

    [Fact]
    public async Task InvalidateAggregatedCache_ForcesReloadFromDisk()
    {
        await _service.SaveSourceCacheAsync("bif", CreateTestItems(3), validationPath: "/game");
        var first = _service.GetAggregatedCache();

        _service.InvalidateAggregatedCache();

        var second = _service.GetAggregatedCache();
        Assert.NotSame(first, second);
        Assert.Equal(first!.Count, second!.Count);
    }

    [Fact]
    public async Task ClearSourceCache_RemovesCacheFile()
    {
        await _service.SaveSourceCacheAsync("bif", CreateTestItems(), validationPath: "/game");
        Assert.True(File.Exists(Path.Combine(_testCacheDir, "bif.json")));

        _service.ClearSourceCache("bif");

        Assert.False(File.Exists(Path.Combine(_testCacheDir, "bif.json")));
    }

    [Fact]
    public async Task ClearSourceCache_InvalidatesAggregatedCache()
    {
        await _service.SaveSourceCacheAsync("bif", CreateTestItems(3), validationPath: "/game");
        var before = _service.GetAggregatedCache();
        Assert.NotNull(before);

        _service.ClearSourceCache("bif");

        var after = _service.GetAggregatedCache();
        Assert.Null(after);
    }

    [Fact]
    public async Task ClearAllCaches_RemovesAllCacheFiles()
    {
        await _service.SaveSourceCacheAsync("bif", CreateTestItems(), validationPath: "/game");
        await _service.SaveSourceCacheAsync("override", CreateTestItems(), validationPath: "/nwn");

        _service.ClearAllCaches();

        Assert.Empty(Directory.GetFiles(_testCacheDir, "*.json"));
    }

    [Fact]
    public void ClearSourceCache_NoOp_WhenCacheDoesNotExist()
    {
        // Should not throw
        _service.ClearSourceCache("bif");
    }

    [Fact]
    public void ClearAllCaches_NoOp_WhenNoCachesExist()
    {
        // Should not throw
        _service.ClearAllCaches();
    }

    #endregion

    #region Statistics

    [Fact]
    public async Task GetCacheStatistics_ReturnsCorrectCounts()
    {
        await _service.SaveSourceCacheAsync("bif", CreateTestItems(10), validationPath: "/game");
        await _service.SaveSourceCacheAsync("override", CreateTestItems(5), validationPath: "/nwn");

        var stats = _service.GetCacheStatistics();

        Assert.Equal(15, stats.TotalItems);
        Assert.True(stats.TotalSizeKB > 0);
        Assert.Equal(10, stats.SourceCounts["bif"]);
        Assert.Equal(5, stats.SourceCounts["override"]);
    }

    [Fact]
    public void GetCacheStatistics_ReturnsEmpty_WhenNoCaches()
    {
        var stats = _service.GetCacheStatistics();

        Assert.Equal(0, stats.TotalItems);
        Assert.Equal(0.0, stats.TotalSizeKB);
        Assert.Empty(stats.SourceCounts);
    }

    #endregion

    #region Thread Safety

    [Fact]
    public async Task ConcurrentReads_DoNotThrow()
    {
        await _service.SaveSourceCacheAsync("bif", CreateTestItems(100), validationPath: "/game");
        await _service.SaveSourceCacheAsync("override", CreateTestItems(50), validationPath: "/nwn");

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            var result = _service.GetAggregatedCache();
            Assert.NotNull(result);
            Assert.Equal(150, result.Count);
        }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ConcurrentReadAndInvalidate_DoNotThrow()
    {
        await _service.SaveSourceCacheAsync("bif", CreateTestItems(50), validationPath: "/game");

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var readTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                _ = _service.GetAggregatedCache();
                await Task.Yield();
            }
        });

        var invalidateTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                _service.InvalidateAggregatedCache();
                await Task.Delay(10);
            }
        });

        await Task.WhenAll(readTask, invalidateTask);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task HakCacheFileName_SanitizesSpecialCharacters()
    {
        var hakPath = Path.Combine(_testCacheDir, "my mod (v2.0).hak");
        File.WriteAllText(hakPath, "fake");

        await _service.SaveSourceCacheAsync("hak", CreateTestItems(),
            validationPath: hakPath, sourceModified: File.GetLastWriteTimeUtc(hakPath));

        // Should sanitize to safe filename
        var files = Directory.GetFiles(_testCacheDir, "hak_*.json");
        Assert.Single(files);
    }

    [Fact]
    public void CacheDirectory_ReturnsCorrectPath()
    {
        Assert.Equal(_testCacheDir, _service.CacheDirectory);
    }

    [Fact]
    public async Task MultipleHaks_GetSeparateCacheFiles()
    {
        var hak1 = Path.Combine(_testCacheDir, "mod_a.hak");
        var hak2 = Path.Combine(_testCacheDir, "mod_b.hak");
        File.WriteAllText(hak1, "fake1");
        File.WriteAllText(hak2, "fake2");

        await _service.SaveSourceCacheAsync("hak", CreateTestItems(2),
            validationPath: hak1, sourceModified: File.GetLastWriteTimeUtc(hak1));
        await _service.SaveSourceCacheAsync("hak", CreateTestItems(3),
            validationPath: hak2, sourceModified: File.GetLastWriteTimeUtc(hak2));

        var hakFiles = Directory.GetFiles(_testCacheDir, "hak_*.json");
        Assert.Equal(2, hakFiles.Length);

        var aggregated = _service.GetAggregatedCache();
        Assert.NotNull(aggregated);
        Assert.Equal(5, aggregated.Count);
    }

    #endregion

    #region Module-Aware Aggregation

    [Fact]
    public async Task GetAggregatedCache_WithHakFilter_OnlyIncludesSpecifiedHaks()
    {
        var hak1 = Path.Combine(_testCacheDir, "cep2_top.hak");
        var hak2 = Path.Combine(_testCacheDir, "cep2_core.hak");
        var hak3 = Path.Combine(_testCacheDir, "other_mod.hak");
        File.WriteAllText(hak1, "fake1");
        File.WriteAllText(hak2, "fake2");
        File.WriteAllText(hak3, "fake3");

        // Save BIF + 3 HAK caches
        await _service.SaveSourceCacheAsync("bif", CreateTestItems(2), validationPath: "/game");
        await _service.SaveSourceCacheAsync("hak", CreateTestItems(3),
            validationPath: hak1, sourceModified: File.GetLastWriteTimeUtc(hak1));
        await _service.SaveSourceCacheAsync("hak", CreateTestItems(4),
            validationPath: hak2, sourceModified: File.GetLastWriteTimeUtc(hak2));
        await _service.SaveSourceCacheAsync("hak", CreateTestItems(5),
            validationPath: hak3, sourceModified: File.GetLastWriteTimeUtc(hak3));

        // Unfiltered should get all 14 items
        var unfiltered = _service.GetAggregatedCache();
        Assert.Equal(14, unfiltered!.Count);

        // Filtered to just hak1 + hak2 should get 2 + 3 + 4 = 9 items (BIF + 2 HAKs)
        _service.InvalidateAggregatedCache();
        var filtered = _service.GetAggregatedCache(new[] { hak1, hak2 });
        Assert.Equal(9, filtered!.Count);
    }

    [Fact]
    public async Task GetAggregatedCache_WithEmptyHakFilter_ExcludesAllHaks()
    {
        var hakPath = Path.Combine(_testCacheDir, "some.hak");
        File.WriteAllText(hakPath, "fake");

        await _service.SaveSourceCacheAsync("bif", CreateTestItems(3), validationPath: "/game");
        await _service.SaveSourceCacheAsync("override", CreateTestItems(2), validationPath: "/nwn");
        await _service.SaveSourceCacheAsync("hak", CreateTestItems(5),
            validationPath: hakPath, sourceModified: File.GetLastWriteTimeUtc(hakPath));

        _service.InvalidateAggregatedCache();
        var filtered = _service.GetAggregatedCache(Array.Empty<string>());

        // Should include BIF + Override but no HAKs
        Assert.Equal(5, filtered!.Count);
    }

    [Fact]
    public async Task GetAggregatedCache_WithNullFilter_IncludesAllHaks()
    {
        var hakPath = Path.Combine(_testCacheDir, "mod.hak");
        File.WriteAllText(hakPath, "fake");

        await _service.SaveSourceCacheAsync("bif", CreateTestItems(2), validationPath: "/game");
        await _service.SaveSourceCacheAsync("hak", CreateTestItems(3),
            validationPath: hakPath, sourceModified: File.GetLastWriteTimeUtc(hakPath));

        _service.InvalidateAggregatedCache();
        var result = _service.GetAggregatedCache(null);

        // null filter = include everything (same as parameterless overload)
        Assert.Equal(5, result!.Count);
    }

    [Fact]
    public async Task GetAggregatedCache_WithFilter_DoesNotCacheFilteredResult()
    {
        var hak1 = Path.Combine(_testCacheDir, "a.hak");
        var hak2 = Path.Combine(_testCacheDir, "b.hak");
        File.WriteAllText(hak1, "fake1");
        File.WriteAllText(hak2, "fake2");

        await _service.SaveSourceCacheAsync("bif", CreateTestItems(1), validationPath: "/game");
        await _service.SaveSourceCacheAsync("hak", CreateTestItems(2),
            validationPath: hak1, sourceModified: File.GetLastWriteTimeUtc(hak1));
        await _service.SaveSourceCacheAsync("hak", CreateTestItems(3),
            validationPath: hak2, sourceModified: File.GetLastWriteTimeUtc(hak2));

        // Filtered call with hak1 only
        var filtered1 = _service.GetAggregatedCache(new[] { hak1 });
        Assert.Equal(3, filtered1!.Count); // 1 BIF + 2 hak1

        // Different filter with hak2 only should return different result
        var filtered2 = _service.GetAggregatedCache(new[] { hak2 });
        Assert.Equal(4, filtered2!.Count); // 1 BIF + 3 hak2
    }

    #endregion
}

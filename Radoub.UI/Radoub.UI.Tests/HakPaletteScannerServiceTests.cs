using Xunit;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Gff;
using Radoub.Formats.Ifo;
using Radoub.Formats.Uti;
using Radoub.UI.Services;

namespace Radoub.UI.Tests;

/// <summary>
/// TDD tests for HakPaletteScannerService.
/// Tests cover: HAK path resolution from module.ifo, HAK scanning for UTI items,
/// cache integration, and edge cases.
/// </summary>
public class HakPaletteScannerServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testCacheDir;
    private readonly SharedPaletteCacheService _cacheService;

    public HakPaletteScannerServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RadoubTests", Guid.NewGuid().ToString("N"));
        _testCacheDir = Path.Combine(_testDir, "Cache");
        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(_testCacheDir);
        _cacheService = new SharedPaletteCacheService(_testCacheDir);
    }

    public void Dispose()
    {
        _cacheService.Dispose();
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); }
            catch { /* cleanup best-effort */ }
        }
    }

    #region Test Helpers

    /// <summary>
    /// Create a minimal module directory with module.ifo containing the specified HAK list.
    /// </summary>
    private string CreateTestModule(List<string> hakNames)
    {
        var moduleDir = Path.Combine(_testDir, "module");
        Directory.CreateDirectory(moduleDir);

        var ifo = new IfoFile();
        foreach (var hak in hakNames)
            ifo.HakList.Add(hak);

        IfoWriter.Write(ifo, Path.Combine(moduleDir, "module.ifo"));
        return moduleDir;
    }

    /// <summary>
    /// Create a HAK directory with HAK files.
    /// </summary>
    private string CreateHakDirectory()
    {
        var hakDir = Path.Combine(_testDir, "hak");
        Directory.CreateDirectory(hakDir);
        return hakDir;
    }

    /// <summary>
    /// Create a test HAK file containing UTI items.
    /// </summary>
    private string CreateTestHak(string hakDir, string hakName, List<(string resRef, string tag, int baseItem, uint cost)> items)
    {
        var hakPath = Path.Combine(hakDir, $"{hakName}.hak");

        var erf = new ErfFile
        {
            FileType = "HAK ",
            FileVersion = "V1.0"
        };

        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>();

        foreach (var (resRef, tag, baseItem, cost) in items)
        {
            var uti = new UtiFile
            {
                TemplateResRef = resRef,
                Tag = tag,
                BaseItem = baseItem,
                Cost = cost
            };
            uti.LocalizedName.LocalizedStrings[0] = $"Test {resRef}";

            var utiBytes = UtiWriter.Write(uti);

            erf.Resources.Add(new ErfResourceEntry
            {
                ResRef = resRef,
                ResourceType = ResourceTypes.Uti,
                ResId = (uint)erf.Resources.Count
            });

            resourceData[(resRef, ResourceTypes.Uti)] = utiBytes;
        }

        ErfWriter.Write(erf, hakPath, resourceData);
        return hakPath;
    }

    /// <summary>
    /// Create a test HAK file with non-UTI resources only.
    /// </summary>
    private string CreateTestHakWithNoItems(string hakDir, string hakName)
    {
        var hakPath = Path.Combine(hakDir, $"{hakName}.hak");

        var erf = new ErfFile
        {
            FileType = "HAK ",
            FileVersion = "V1.0"
        };

        // Add a non-UTI resource (e.g., a script - NCS type)
        erf.Resources.Add(new ErfResourceEntry
        {
            ResRef = "test_script",
            ResourceType = ResourceTypes.Ncs,
            ResId = 0
        });

        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>
        {
            [("test_script", ResourceTypes.Ncs)] = new byte[] { 0x4E, 0x43, 0x53, 0x20 }
        };

        ErfWriter.Write(erf, hakPath, resourceData);
        return hakPath;
    }

    #endregion

    #region HAK Path Resolution

    [Fact]
    public void ResolveModuleHakPaths_ReturnsEmpty_WhenNoHaksInIfo()
    {
        var moduleDir = CreateTestModule(new List<string>());
        var hakDir = CreateHakDirectory();

        var scanner = new HakPaletteScannerService();
        var paths = scanner.ResolveModuleHakPaths(moduleDir, new[] { hakDir });

        Assert.Empty(paths);
    }

    [Fact]
    public void ResolveModuleHakPaths_ResolvesHakNamesToFilePaths()
    {
        var hakDir = CreateHakDirectory();
        CreateTestHakWithNoItems(hakDir, "my_custom");
        var moduleDir = CreateTestModule(new List<string> { "my_custom" });

        var scanner = new HakPaletteScannerService();
        var paths = scanner.ResolveModuleHakPaths(moduleDir, new[] { hakDir });

        Assert.Single(paths);
        Assert.Equal(Path.Combine(hakDir, "my_custom.hak"), paths[0]);
    }

    [Fact]
    public void ResolveModuleHakPaths_SkipsMissingHaks()
    {
        var hakDir = CreateHakDirectory();
        CreateTestHakWithNoItems(hakDir, "exists_hak");
        var moduleDir = CreateTestModule(new List<string> { "exists_hak", "missing_hak" });

        var scanner = new HakPaletteScannerService();
        var paths = scanner.ResolveModuleHakPaths(moduleDir, new[] { hakDir });

        Assert.Single(paths);
        Assert.Contains("exists_hak.hak", paths[0]);
    }

    [Fact]
    public void ResolveModuleHakPaths_SearchesMultipleDirectories()
    {
        var hakDir1 = Path.Combine(_testDir, "hak1");
        var hakDir2 = Path.Combine(_testDir, "hak2");
        Directory.CreateDirectory(hakDir1);
        Directory.CreateDirectory(hakDir2);

        CreateTestHakWithNoItems(hakDir1, "hak_a");
        CreateTestHakWithNoItems(hakDir2, "hak_b");

        var moduleDir = CreateTestModule(new List<string> { "hak_a", "hak_b" });

        var scanner = new HakPaletteScannerService();
        var paths = scanner.ResolveModuleHakPaths(moduleDir, new[] { hakDir1, hakDir2 });

        Assert.Equal(2, paths.Count);
    }

    [Fact]
    public void ResolveModuleHakPaths_ReturnsEmpty_WhenNoModuleIfo()
    {
        var emptyDir = Path.Combine(_testDir, "empty_module");
        Directory.CreateDirectory(emptyDir);

        var scanner = new HakPaletteScannerService();
        var paths = scanner.ResolveModuleHakPaths(emptyDir, new[] { CreateHakDirectory() });

        Assert.Empty(paths);
    }

    [Fact]
    public void ResolveModuleHakPaths_CaseInsensitiveHakName()
    {
        var hakDir = CreateHakDirectory();
        // HAK file on disk may have different case than what's in module.ifo
        CreateTestHakWithNoItems(hakDir, "MyCustom");
        var moduleDir = CreateTestModule(new List<string> { "mycustom" });

        var scanner = new HakPaletteScannerService();
        var paths = scanner.ResolveModuleHakPaths(moduleDir, new[] { hakDir });

        // Should find the file regardless of case
        Assert.Single(paths);
    }

    #endregion

    #region HAK Scanning

    [Fact]
    public async Task ScanHakForItemsAsync_ReturnsItemsFromHak()
    {
        var hakDir = CreateHakDirectory();
        var hakPath = CreateTestHak(hakDir, "weapons", new List<(string, string, int, uint)>
        {
            ("nw_wswls001", "LONGSWORD", 22, 300),
            ("nw_wswss001", "SHORTSWORD", 22, 200)
        });

        var scanner = new HakPaletteScannerService();
        var items = await scanner.ScanHakForItemsAsync(hakPath, CancellationToken.None);

        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.ResRef == "nw_wswls001");
        Assert.Contains(items, i => i.ResRef == "nw_wswss001");
    }

    [Fact]
    public async Task ScanHakForItemsAsync_SetsCorrectFields()
    {
        var hakDir = CreateHakDirectory();
        var hakPath = CreateTestHak(hakDir, "armor", new List<(string, string, int, uint)>
        {
            ("nw_aarcl001", "CHAINMAIL", 16, 500)
        });

        var scanner = new HakPaletteScannerService();
        var items = await scanner.ScanHakForItemsAsync(hakPath, CancellationToken.None);

        Assert.Single(items);
        var item = items[0];
        Assert.Equal("nw_aarcl001", item.ResRef);
        Assert.Equal("CHAINMAIL", item.Tag);
        Assert.Equal(16, item.BaseItemType);
        Assert.Equal(500u, item.BaseValue);
        Assert.False(item.IsStandard); // HAK items are not standard/base game
    }

    [Fact]
    public async Task ScanHakForItemsAsync_ReturnsEmpty_WhenNoUtiResources()
    {
        var hakDir = CreateHakDirectory();
        var hakPath = CreateTestHakWithNoItems(hakDir, "scripts_only");

        var scanner = new HakPaletteScannerService();
        var items = await scanner.ScanHakForItemsAsync(hakPath, CancellationToken.None);

        Assert.Empty(items);
    }

    [Fact]
    public async Task ScanHakForItemsAsync_RespectsCancel()
    {
        var hakDir = CreateHakDirectory();
        // Create HAK with items
        var hakPath = CreateTestHak(hakDir, "big_hak", new List<(string, string, int, uint)>
        {
            ("item_001", "TAG1", 1, 100),
            ("item_002", "TAG2", 1, 200)
        });

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var scanner = new HakPaletteScannerService();
        var items = await scanner.ScanHakForItemsAsync(hakPath, cts.Token);

        // Should return empty or partial — not throw
        Assert.True(items.Count < 2);
    }

    [Fact]
    public async Task ScanHakForItemsAsync_DeduplicatesResRefs()
    {
        var hakDir = CreateHakDirectory();
        // Create HAK with duplicate ResRefs (shouldn't happen in practice, but be safe)
        var hakPath = CreateTestHak(hakDir, "dupes", new List<(string, string, int, uint)>
        {
            ("same_item", "TAG1", 1, 100),
            ("same_item", "TAG2", 1, 200) // Same ResRef
        });

        var scanner = new HakPaletteScannerService();
        var items = await scanner.ScanHakForItemsAsync(hakPath, CancellationToken.None);

        // ERF may not preserve order, but should deduplicate by ResRef
        Assert.Single(items);
        Assert.Equal("same_item", items[0].ResRef);
    }

    #endregion

    #region Full Pipeline: Scan and Cache

    [Fact]
    public async Task ScanAndCacheModuleHaksAsync_CachesItemsPerHak()
    {
        var hakDir = CreateHakDirectory();
        CreateTestHak(hakDir, "hak_a", new List<(string, string, int, uint)>
        {
            ("item_a1", "TAGA1", 1, 100),
            ("item_a2", "TAGA2", 2, 200)
        });
        CreateTestHak(hakDir, "hak_b", new List<(string, string, int, uint)>
        {
            ("item_b1", "TAGB1", 3, 300)
        });

        var moduleDir = CreateTestModule(new List<string> { "hak_a", "hak_b" });

        var scanner = new HakPaletteScannerService();
        var result = await scanner.ScanAndCacheModuleHaksAsync(
            moduleDir, new[] { hakDir }, _cacheService, CancellationToken.None);

        Assert.Equal(3, result.TotalItemsScanned);
        Assert.Equal(2, result.HaksScanned);
        Assert.Equal(0, result.HaksSkipped);

        // Verify individual HAK caches exist
        var hakAItems = _cacheService.LoadSourceCache("hak",
            Path.Combine(hakDir, "hak_a.hak"));
        var hakBItems = _cacheService.LoadSourceCache("hak",
            Path.Combine(hakDir, "hak_b.hak"));

        Assert.NotNull(hakAItems);
        Assert.Equal(2, hakAItems.Count);
        Assert.NotNull(hakBItems);
        Assert.Single(hakBItems);
    }

    [Fact]
    public async Task ScanAndCacheModuleHaksAsync_SkipsValidCachedHaks()
    {
        var hakDir = CreateHakDirectory();
        var hakPath = CreateTestHak(hakDir, "cached_hak", new List<(string, string, int, uint)>
        {
            ("item_1", "TAG1", 1, 100)
        });

        var moduleDir = CreateTestModule(new List<string> { "cached_hak" });

        // Pre-populate cache for this HAK
        await _cacheService.SaveSourceCacheAsync("hak",
            new List<SharedPaletteCacheItem>
            {
                new() { ResRef = "item_1", Tag = "TAG1", BaseItemType = 1, BaseValue = 100 }
            },
            validationPath: hakPath,
            sourceModified: File.GetLastWriteTimeUtc(hakPath));

        var scanner = new HakPaletteScannerService();
        var result = await scanner.ScanAndCacheModuleHaksAsync(
            moduleDir, new[] { hakDir }, _cacheService, CancellationToken.None);

        Assert.Equal(0, result.TotalItemsScanned); // Nothing scanned — used cache
        Assert.Equal(0, result.HaksScanned);
        Assert.Equal(1, result.HaksSkipped); // Skipped because cache is valid
    }

    [Fact]
    public async Task ScanAndCacheModuleHaksAsync_RescansWhenHakModified()
    {
        var hakDir = CreateHakDirectory();
        var hakPath = CreateTestHak(hakDir, "changing_hak", new List<(string, string, int, uint)>
        {
            ("old_item", "OLD", 1, 100)
        });

        var moduleDir = CreateTestModule(new List<string> { "changing_hak" });

        // Pre-cache with old mod time
        await _cacheService.SaveSourceCacheAsync("hak",
            new List<SharedPaletteCacheItem>
            {
                new() { ResRef = "old_item", Tag = "OLD", BaseItemType = 1, BaseValue = 100 }
            },
            validationPath: hakPath,
            sourceModified: DateTime.UtcNow.AddDays(-1)); // Old timestamp

        var scanner = new HakPaletteScannerService();
        var result = await scanner.ScanAndCacheModuleHaksAsync(
            moduleDir, new[] { hakDir }, _cacheService, CancellationToken.None);

        Assert.Equal(1, result.TotalItemsScanned); // Rescanned due to stale cache
        Assert.Equal(1, result.HaksScanned);
    }

    [Fact]
    public async Task ScanAndCacheModuleHaksAsync_HandlesEmptyModule()
    {
        var moduleDir = CreateTestModule(new List<string>()); // No HAKs
        var hakDir = CreateHakDirectory();

        var scanner = new HakPaletteScannerService();
        var result = await scanner.ScanAndCacheModuleHaksAsync(
            moduleDir, new[] { hakDir }, _cacheService, CancellationToken.None);

        Assert.Equal(0, result.TotalItemsScanned);
        Assert.Equal(0, result.HaksScanned);
        Assert.Equal(0, result.HaksSkipped);
    }

    [Fact]
    public async Task ScanAndCacheModuleHaksAsync_InvalidatesAggregatedCache()
    {
        var hakDir = CreateHakDirectory();
        CreateTestHak(hakDir, "new_hak", new List<(string, string, int, uint)>
        {
            ("item_1", "TAG1", 1, 100)
        });

        // Pre-populate BIF cache so aggregated cache has data
        await _cacheService.SaveSourceCacheAsync("bif",
            new List<SharedPaletteCacheItem>
            {
                new() { ResRef = "bif_item", Tag = "BIF", BaseItemType = 1, BaseValue = 50 }
            },
            validationPath: "/game");

        var aggregatedBefore = _cacheService.GetAggregatedCache();
        Assert.NotNull(aggregatedBefore);
        Assert.Single(aggregatedBefore); // Only BIF

        var moduleDir = CreateTestModule(new List<string> { "new_hak" });

        var scanner = new HakPaletteScannerService();
        await scanner.ScanAndCacheModuleHaksAsync(
            moduleDir, new[] { hakDir }, _cacheService, CancellationToken.None);

        // Aggregated cache should now include HAK items
        var aggregatedAfter = _cacheService.GetAggregatedCache();
        Assert.NotNull(aggregatedAfter);
        Assert.Equal(2, aggregatedAfter.Count); // BIF + HAK
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ScanHakForItemsAsync_HandlesCorruptHak()
    {
        var hakDir = CreateHakDirectory();
        var hakPath = Path.Combine(hakDir, "corrupt.hak");
        File.WriteAllBytes(hakPath, new byte[] { 0x00, 0x01, 0x02, 0x03 });

        var scanner = new HakPaletteScannerService();
        var items = await scanner.ScanHakForItemsAsync(hakPath, CancellationToken.None);

        // Should return empty, not throw
        Assert.Empty(items);
    }

    [Fact]
    public async Task ScanHakForItemsAsync_HandlesNonExistentFile()
    {
        var scanner = new HakPaletteScannerService();
        var items = await scanner.ScanHakForItemsAsync(
            Path.Combine(_testDir, "nonexistent.hak"), CancellationToken.None);

        Assert.Empty(items);
    }

    [Fact]
    public void ResolveModuleHakPaths_PreservesIfoOrder()
    {
        var hakDir = CreateHakDirectory();
        CreateTestHakWithNoItems(hakDir, "hak_z");
        CreateTestHakWithNoItems(hakDir, "hak_a");
        CreateTestHakWithNoItems(hakDir, "hak_m");

        // IFO order matters — first HAK has highest priority
        var moduleDir = CreateTestModule(new List<string> { "hak_z", "hak_a", "hak_m" });

        var scanner = new HakPaletteScannerService();
        var paths = scanner.ResolveModuleHakPaths(moduleDir, new[] { hakDir });

        Assert.Equal(3, paths.Count);
        Assert.Contains("hak_z.hak", paths[0]);
        Assert.Contains("hak_a.hak", paths[1]);
        Assert.Contains("hak_m.hak", paths[2]);
    }

    #endregion
}

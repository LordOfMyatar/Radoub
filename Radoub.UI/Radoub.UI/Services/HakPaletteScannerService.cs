using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.Formats.Resolver;
using Radoub.Formats.Uti;

namespace Radoub.UI.Services;

/// <summary>
/// Scans HAK files referenced by a module's IFO for UTI (item) resources
/// and populates the shared palette cache with per-HAK granularity.
/// </summary>
public class HakPaletteScannerService
{
    /// <summary>
    /// Read module.ifo from the module directory and resolve HAK names to file paths.
    /// Delegates to ModuleHakResolver in Radoub.Formats.
    /// Returns resolved paths in module.ifo priority order (first = highest priority).
    /// </summary>
    public List<string> ResolveModuleHakPaths(string moduleDirectory, IEnumerable<string> hakSearchPaths)
    {
        return ModuleHakResolver.ResolveModuleHakPaths(moduleDirectory, hakSearchPaths);
    }

    /// <summary>
    /// Scan a single HAK file for UTI (item) resources and return cache items.
    /// Returns empty list on error or cancellation. Does not throw.
    /// </summary>
    public async Task<List<SharedPaletteCacheItem>> ScanHakForItemsAsync(
        string hakPath, CancellationToken token)
    {
        var items = new List<SharedPaletteCacheItem>();

        if (!File.Exists(hakPath) || token.IsCancellationRequested)
            return items;

        return await Task.Run(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ErfFile hak;
            try
            {
                hak = ErfReader.Read(hakPath);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to read HAK '{Path.GetFileName(hakPath)}': {ex.Message}");
                return items;
            }
            var readMs = sw.ElapsedMilliseconds;

            var utiEntries = hak.GetResourcesByType(ResourceTypes.Uti).ToList();
            if (utiEntries.Count == 0)
                return items;

            var seenResRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in utiEntries)
            {
                if (token.IsCancellationRequested)
                    break;

                if (seenResRefs.Contains(entry.ResRef))
                    continue;

                try
                {
                    var data = ErfReader.ExtractResource(hakPath, entry);
                    if (data != null && data.Length > 0)
                    {
                        var uti = UtiReader.Read(data);

                        // Resolve display name from localized name
                        var displayName = uti.LocalizedName.GetDefault()
                            ?? uti.TemplateResRef;

                        items.Add(new SharedPaletteCacheItem
                        {
                            ResRef = entry.ResRef,
                            Tag = uti.Tag ?? string.Empty,
                            DisplayName = displayName,
                            BaseItemTypeName = string.Empty, // Set by consumer with 2DA lookup
                            BaseItemType = uti.BaseItem,
                            BaseValue = uti.Cost,
                            IsStandard = false // HAK items are custom content
                        });
                        seenResRefs.Add(entry.ResRef);
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Failed to parse UTI '{entry.ResRef}' from HAK '{Path.GetFileName(hakPath)}': {ex.Message}");
                }
            }

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"[TIMING] HakPaletteScanner: '{Path.GetFileName(hakPath)}' — {items.Count}/{utiEntries.Count} items, ERF read {readMs}ms, total {sw.ElapsedMilliseconds}ms (full read + GFF parse)");

            return items;
        }, token);
    }

    /// <summary>
    /// Scan all module HAKs and save per-HAK caches. Skips HAKs with valid caches.
    /// Returns scan result summary.
    /// </summary>
    public async Task<HakScanResult> ScanAndCacheModuleHaksAsync(
        string moduleDirectory,
        IEnumerable<string> hakSearchPaths,
        ISharedPaletteCacheService cacheService,
        CancellationToken token)
    {
        var result = new HakScanResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var hakPaths = ResolveModuleHakPaths(moduleDirectory, hakSearchPaths);
        if (hakPaths.Count == 0)
            return result;

        bool anyNewCaches = false;

        foreach (var hakPath in hakPaths)
        {
            if (token.IsCancellationRequested)
                break;

            // Check if cache is still valid for this HAK
            if (cacheService.HasValidSourceCache("hak", hakPath))
            {
                result.HaksSkipped++;
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"HAK cache valid, skipping: {Path.GetFileName(hakPath)}");
                continue;
            }

            // Scan HAK and cache results
            var items = await ScanHakForItemsAsync(hakPath, token);

            if (!token.IsCancellationRequested)
            {
                await cacheService.SaveSourceCacheAsync("hak", items,
                    validationPath: hakPath,
                    sourceModified: File.GetLastWriteTimeUtc(hakPath));

                result.HaksScanned++;
                result.TotalItemsScanned += items.Count;
                anyNewCaches = true;
            }
        }

        // Invalidate aggregated cache if any new HAK caches were written
        if (anyNewCaches)
        {
            cacheService.InvalidateAggregatedCache();
        }

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"[TIMING] HakPaletteScanner: Total — {result.HaksScanned} scanned, {result.HaksSkipped} cached, {result.TotalItemsScanned} items in {sw.ElapsedMilliseconds}ms");

        return result;
    }

}

/// <summary>
/// Result of scanning module HAK files.
/// </summary>
public class HakScanResult
{
    /// <summary>Number of HAK files that were actually scanned (cache was stale/missing).</summary>
    public int HaksScanned { get; set; }

    /// <summary>Number of HAK files skipped because their cache was still valid.</summary>
    public int HaksSkipped { get; set; }

    /// <summary>Total number of items found across all scanned HAKs.</summary>
    public int TotalItemsScanned { get; set; }
}

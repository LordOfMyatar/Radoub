using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.UI.Services;
using Radoub.UI.ViewModels;

namespace RadoubLauncher.Services;

/// <summary>
/// Pre-warms the shared item palette cache from Trebuchet on startup.
/// BIF cache is built after GameDataService initializes.
/// HAK cache is built after module loads (auto-load or manual).
/// All operations are fire-and-forget with cancellation.
/// </summary>
public class PaletteCacheWarmupService
{
    private readonly ISharedPaletteCacheService _cacheService;
    private readonly HakPaletteScannerService _hakScanner;
    private CancellationTokenSource? _bifWarmCts;
    private CancellationTokenSource? _hakWarmCts;

    public PaletteCacheWarmupService(
        ISharedPaletteCacheService cacheService,
        HakPaletteScannerService hakScanner)
    {
        _cacheService = cacheService;
        _hakScanner = hakScanner;
    }

    /// <summary>
    /// Warm the BIF cache if stale or missing. Skips if already valid.
    /// Uses build lock sentinels for cross-process coordination.
    /// </summary>
    public async Task WarmBifCacheAsync(IGameDataService gameDataService, CancellationToken cancellationToken)
    {
        _bifWarmCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _bifWarmCts.Token;

        try
        {
            if (_cacheService.HasValidSourceCache("bif"))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Pre-warm: BIF cache valid, skipping");
                return;
            }

            if (!_cacheService.AcquireBuildLock("bif"))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    "Pre-warm: BIF build lock held by another process, skipping");
                return;
            }

            try
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Pre-warm: BIF cache stale/missing, building...");
                var sw = Stopwatch.StartNew();

                var factory = new ItemViewModelFactory(gameDataService);
                var resources = gameDataService.ListResources(Radoub.Formats.Common.ResourceTypes.Uti);
                var items = new List<SharedPaletteCacheItem>();

                foreach (var resource in resources)
                {
                    token.ThrowIfCancellationRequested();

                    if (resource.Source != Radoub.Formats.Services.GameResourceSource.Bif)
                        continue;

                    try
                    {
                        var data = gameDataService.FindResource(resource.ResRef, resource.ResourceType);
                        if (data == null) continue;

                        var uti = Radoub.Formats.Uti.UtiReader.Read(data);
                        var displayName = factory.GetItemDisplayName(uti);
                        var baseItemName = factory.GetBaseItemTypeName(uti.BaseItem);
                        var propertiesDisplay = factory.GetPropertiesDisplay(uti.Properties);

                        items.Add(new SharedPaletteCacheItem
                        {
                            ResRef = resource.ResRef,
                            Tag = uti.Tag ?? "",
                            DisplayName = displayName,
                            BaseItemType = uti.BaseItem,
                            BaseItemTypeName = baseItemName,
                            BaseValue = uti.Cost,
                            IsStandard = true,
                            PropertiesDisplay = propertiesDisplay,
                            SourceLocation = "bif"
                        });
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"Pre-warm: Failed to process BIF UTI '{resource.ResRef}': {ex.Message}");
                    }
                }

                await _cacheService.SaveSourceCacheAsync("bif", items);

                sw.Stop();
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Pre-warm: BIF cache built ({items.Count} items, {sw.ElapsedMilliseconds}ms)");
            }
            finally
            {
                _cacheService.ReleaseBuildLock("bif");
            }
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Pre-warm: BIF warm cancelled");
        }
    }

    /// <summary>
    /// Warm HAK caches for the currently loaded module.
    /// Cancels any previous HAK warm operation before starting.
    /// </summary>
    public async Task WarmModuleHakCacheAsync(
        IGameDataService gameDataService,
        string moduleDirectory,
        IEnumerable<string> hakSearchPaths,
        CancellationToken cancellationToken)
    {
        // Cancel previous HAK warm if running
        _hakWarmCts?.Cancel();
        _hakWarmCts?.Dispose();
        _hakWarmCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _hakWarmCts.Token;

        try
        {
            var moduleName = Path.GetFileName(moduleDirectory);
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Pre-warm: Starting HAK scan for module {moduleName}");

            var sw = Stopwatch.StartNew();

            var result = await _hakScanner.ScanAndCacheModuleHaksAsync(
                moduleDirectory, hakSearchPaths, _cacheService, token);

            sw.Stop();
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Pre-warm: HAK scan complete ({result.HaksScanned} scanned, {result.HaksSkipped} skipped, {sw.ElapsedMilliseconds}ms)");
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Pre-warm: HAK warm cancelled (module changed)");
        }
    }

    /// <summary>
    /// Cancel all running warm-up operations (called on app shutdown).
    /// </summary>
    public void CancelAll()
    {
        _bifWarmCts?.Cancel();
        _hakWarmCts?.Cancel();
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "Pre-warm: Cancelled (app closing)");
    }

    public void Dispose()
    {
        _bifWarmCts?.Dispose();
        _hakWarmCts?.Dispose();
        (_cacheService as IDisposable)?.Dispose();
    }
}

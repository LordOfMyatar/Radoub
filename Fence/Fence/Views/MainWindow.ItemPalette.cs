using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.Formats.Uti;
using Radoub.UI.Services;
using Radoub.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: Item Palette loading and filtering
/// Uses shared cross-tool palette cache for item data.
/// Load-all strategy with shared ItemFilterPanel for filtering.
/// </summary>
public partial class MainWindow
{
    // Base item types to exclude from palette (creature weapons, internal items)
    private static readonly HashSet<int> ExcludedBaseItemTypes = new()
    {
        69,  // Creature Bite
        70,  // Creature Claw
        71,  // Creature Gore
        72,  // Creature Slashing
        73,  // Creature Piercing/Bludgeoning
        255, // Invalid/special marker
    };

    private readonly ISharedPaletteCacheService _sharedCacheService = new SharedPaletteCacheService();

    // HAK scanner for loading items from module-referenced HAK files
    private readonly HakPaletteScannerService _hakScanner = new();

    private List<SharedPaletteCacheItem>? _cachedPaletteData;
    private string? _lastModuleDirectory;

    // Active HAK paths from current module's IFO (for filtered aggregation)
    private List<string>? _activeHakPaths;

    #region Item Palette

    /// <summary>
    /// Initialize palette - pre-warm cache and load all items.
    /// Uses load-all strategy with shared ItemFilterPanel for filtering.
    /// </summary>
    private void StartItemPaletteLoad(CancellationToken token = default)
    {
        if (_gameDataService == null || !_gameDataService.IsConfigured)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Item palette load skipped - GameDataService not configured");
            UpdateStatusBar("Ready (item palette unavailable - configure game paths in Settings)");
            return;
        }

        // Pre-warm cache in background, then load all items
        _ = PreWarmAndLoadPaletteAsync(token);

        UpdateStatusBar("Loading item palette...");
    }

    private async Task PreWarmAndLoadPaletteAsync(CancellationToken token = default)
    {
        await PreWarmCacheAsync(token);
        await LoadAllPaletteItemsAsync(token);
    }

    /// <summary>
    /// Pre-warm the cache in background so it's ready when user filters.
    /// Uses shared cross-tool cache.
    /// </summary>
    private async Task PreWarmCacheAsync(CancellationToken token = default)
    {
        try
        {
            token.ThrowIfCancellationRequested();

            // Resolve active HAK paths for module-aware filtering
            _activeHakPaths = ResolveActiveHakPaths();

            // Try to load from shared cache (filtered to active HAKs)
            var aggregated = await Task.Run(() => _sharedCacheService.GetAggregatedCache(_activeHakPaths), token);
            if (aggregated != null)
            {
                // Filter out excluded base item types
                _cachedPaletteData = aggregated
                    .Where(i => !ExcludedBaseItemTypes.Contains(i.BaseItemType))
                    .ToList();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Cache pre-warmed from shared cache: {_cachedPaletteData.Count} items ready");

                // Scan module HAKs in background (skips cached, refreshes stale)
                _ = ScanModuleHaksAsync(token);
                return;
            }

            // Check if another process (e.g. Trebuchet) is building the cache (#1633)
            var bifLockCleared = await _sharedCacheService.WaitForBuildLock("bif", timeout: 60000, cancellationToken: token);
            if (bifLockCleared)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "BIF build lock cleared, reloading from cache");
                _sharedCacheService.InvalidateAggregatedCache();
                var freshCache = _sharedCacheService.GetAggregatedCache(_activeHakPaths);
                if (freshCache != null)
                {
                    _cachedPaletteData = freshCache
                        .Where(i => !ExcludedBaseItemTypes.Contains(i.BaseItemType))
                        .ToList();
                    _ = ScanModuleHaksAsync(token);
                    return;
                }
            }

            // No shared cache and no lock — build cache and scan HAKs in parallel
            UnifiedLogger.LogApplication(LogLevel.INFO, "Building palette cache in background...");
            var buildTask = BuildCacheInBackgroundAsync();
            var hakTask = ScanModuleHaksAsync(token);
            await Task.WhenAll(buildTask, hakTask);

            // Re-aggregate after both complete to include HAK items
            _sharedCacheService.InvalidateAggregatedCache();
            var freshAggregated = _sharedCacheService.GetAggregatedCache(_activeHakPaths);
            if (freshAggregated != null)
            {
                _cachedPaletteData = freshAggregated
                    .Where(i => !ExcludedBaseItemTypes.Contains(i.BaseItemType))
                    .ToList();
            }
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Cache pre-warm cancelled");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Cache pre-warm failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Build the full cache in background without displaying items.
    /// Saves to shared cache location for cross-tool consumption.
    /// </summary>
    private async Task BuildCacheInBackgroundAsync()
    {
        var cacheItems = new List<SharedPaletteCacheItem>();

        await Task.Run(() =>
        {
            var gameResources = _gameDataService!.ListResources(ResourceTypes.Uti).ToList();
            var existingResRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var resourceInfo in gameResources)
            {
                if (existingResRefs.Contains(resourceInfo.ResRef))
                    continue;

                try
                {
                    var utiData = _gameDataService.FindResource(resourceInfo.ResRef, ResourceTypes.Uti);
                    if (utiData != null)
                    {
                        var item = UtiReader.Read(utiData);
                        var displayName = _itemViewModelFactory?.GetItemDisplayName(item)
                            ?? item.LocalizedName.GetDefault()
                            ?? resourceInfo.ResRef;
                        var baseItemTypeName = _itemViewModelFactory?.GetBaseItemTypeName(item.BaseItem) ?? string.Empty;
                        var propertiesDisplay = _itemViewModelFactory?.GetPropertiesDisplay(item.Properties) ?? string.Empty;

                        if (!ExcludedBaseItemTypes.Contains(item.BaseItem))
                        {
                            cacheItems.Add(new SharedPaletteCacheItem
                            {
                                ResRef = resourceInfo.ResRef,
                                DisplayName = displayName,
                                BaseItemTypeName = baseItemTypeName,
                                BaseItemType = item.BaseItem,
                                BaseValue = item.Cost,
                                Tag = item.Tag ?? string.Empty,
                                IsStandard = resourceInfo.Source == GameResourceSource.Bif,
                                PropertiesDisplay = propertiesDisplay,
                                SourceLocation = !string.IsNullOrEmpty(resourceInfo.SourcePath)
                                    ? Path.GetFileName(resourceInfo.SourcePath)
                                    : resourceInfo.Source.ToString()
                            });
                            existingResRefs.Add(resourceInfo.ResRef);
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Failed to cache item {resourceInfo.ResRef}: {ex.Message}");
                }
            }
        });

        // Save to shared cache (split by source for cross-tool benefit)
        var bifItems = cacheItems.Where(i => i.IsStandard).ToList();
        var customItems = cacheItems.Where(i => !i.IsStandard).ToList();

        var settings = Radoub.Formats.Settings.RadoubSettings.Instance;
        if (bifItems.Count > 0)
            await _sharedCacheService.SaveSourceCacheAsync("bif", bifItems, settings.BaseGameInstallPath);
        if (customItems.Count > 0)
            await _sharedCacheService.SaveSourceCacheAsync("override", customItems, settings.NeverwinterNightsPath);

        _cachedPaletteData = cacheItems;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Background cache complete: {cacheItems.Count} items");
    }

    /// <summary>
    /// Load all palette items from cache into ItemViewModel collection.
    /// Matching QM's load-all-then-filter strategy.
    /// </summary>
    private async Task LoadAllPaletteItemsAsync(CancellationToken token = default)
    {
        if (_cachedPaletteData == null || _cachedPaletteData.Count == 0)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                UpdateStatusBar("Ready (no items available)"));
            return;
        }

        var viewModels = await Task.Run(() =>
        {
            var vms = new List<ItemViewModel>(_cachedPaletteData.Count);
            foreach (var cached in _cachedPaletteData)
            {
                token.ThrowIfCancellationRequested();
                var vm = new ItemViewModel
                {
                    ResRef = cached.ResRef,
                    Name = cached.DisplayName,
                    BaseItemName = cached.BaseItemTypeName,
                    BaseItem = cached.BaseItemType,
                    Value = cached.BaseValue,
                    Tag = !string.IsNullOrEmpty(cached.Tag) ? cached.Tag : cached.ResRef,
                    PropertiesDisplay = cached.PropertiesDisplay,
                    Source = cached.IsStandard ? GameResourceSource.Bif : GameResourceSource.Override,
                    SourceLocation = cached.SourceLocation,
                    IconBitmap = _itemIconService?.GetItemIcon(cached.BaseItemType)
                };
                _itemViewModelFactory?.PopulateEquipableSlots(vm, cached.BaseItemType);
                vms.Add(vm);
            }
            return vms;
        }, token);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Disconnect filter during bulk load
            if (_paletteFilter != null)
                _paletteFilter.Items = null;

            PaletteItems.Clear();
            foreach (var vm in viewModels)
                PaletteItems.Add(vm);

            if (_paletteFilter != null)
            {
                _paletteFilter.Items = PaletteItems;
                _paletteFilter.GameDataService = _gameDataService;
                // Show all sources by default (including module/custom items)
                _paletteFilter.ShowCustom = true;
                _paletteFilter.ApplyFilter();
            }

            UpdateStatusBar($"Ready - {PaletteItems.Count} items loaded");
        });

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Palette loaded: {viewModels.Count} items");
    }

    /// <summary>
    /// Clear and reload the palette cache. Called from Settings.
    /// Returns a task that completes when rebuild is done.
    /// </summary>
    public async Task ClearAndReloadPaletteCacheAsync()
    {
        _sharedCacheService.ClearAllCaches();
        _cachedPaletteData = null;
        _activeHakPaths = null;
        PaletteItems.Clear();

        // Rebuild cache and load all items
        UpdateStatusBar("Rebuilding palette cache...");
        await BuildCacheInBackgroundAsync();
        await ScanModuleHaksAsync();
        await LoadAllPaletteItemsAsync();
        PopulateModuleItems();
        UpdateStatusBar("Cache rebuilt successfully");
    }

    /// <summary>
    /// Get cache statistics for display in Settings.
    /// </summary>
    public SharedPaletteCacheStatistics GetPaletteCacheStatistics() => _sharedCacheService.GetCacheStatistics();

    /// <summary>
    /// Scan the module directory (sibling folder of opened .utm file) for loose .uti files
    /// and add them to the palette. Module items are NOT cached - scanned fresh each time.
    /// </summary>
    private void PopulateModuleItems()
    {
        var newModuleDir = string.IsNullOrEmpty(_currentFilePath)
            ? null
            : Path.GetDirectoryName(_currentFilePath);

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"PopulateModuleItems: scanning '{(newModuleDir != null ? UnifiedLogger.SanitizePath(newModuleDir) : "(null)")}' for module UTIs");

        // Clear old module items if directory changed
        if (_lastModuleDirectory != newModuleDir)
        {
            ClearModuleItems();
            _lastModuleDirectory = newModuleDir;
        }

        if (string.IsNullOrEmpty(newModuleDir) || !Directory.Exists(newModuleDir))
            return;

        // Disconnect filter during bulk add
        if (_paletteFilter != null)
            _paletteFilter.Items = null;

        var utiFiles = Directory.GetFiles(newModuleDir, "*.uti", SearchOption.TopDirectoryOnly);
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Found {utiFiles.Length} .uti files in module directory");

        var existingResRefs = new HashSet<string>(PaletteItems.Select(p => p.ResRef), StringComparer.OrdinalIgnoreCase);
        var moduleItemCount = 0;

        foreach (var utiPath in utiFiles)
        {
            try
            {
                var resRef = Path.GetFileNameWithoutExtension(utiPath);
                if (existingResRefs.Contains(resRef))
                    continue;

                var resolved = _itemResolutionService?.ResolveItem(resRef);
                if (resolved != null && !ExcludedBaseItemTypes.Contains(resolved.BaseItemType))
                {
                    PaletteItems.Add(new ItemViewModel
                    {
                        ResRef = resolved.ResRef,
                        Name = resolved.DisplayName,
                        BaseItemName = resolved.BaseItemTypeName,
                        BaseItem = resolved.BaseItemType,
                        Value = (uint)resolved.BaseCost,
                        Tag = resolved.Tag,
                        PropertiesDisplay = string.Empty,
                        Source = GameResourceSource.Module,
                        SourceLocation = Path.GetFileName(utiPath),
                        IconBitmap = _itemIconService?.GetItemIcon(resolved.BaseItemType)
                    });
                    existingResRefs.Add(resRef);
                    moduleItemCount++;
                }
            }
            catch (Exception ex)
            {
                var fileName = Path.GetFileName(utiPath);
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load UTI {fileName}: {ex.Message}");
            }
        }

        // Reconnect filter
        if (_paletteFilter != null)
        {
            _paletteFilter.Items = PaletteItems;
            _paletteFilter.ApplyFilter();
        }

        if (moduleItemCount > 0)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Added {moduleItemCount} module items to palette");
        }
    }

    /// <summary>
    /// Clear module items from the palette (when switching to a different module folder).
    /// </summary>
    private void ClearModuleItems()
    {
        // Disconnect filter during removal
        if (_paletteFilter != null)
            _paletteFilter.Items = null;

        var toRemove = PaletteItems.Where(p => p.Source == GameResourceSource.Module).ToList();

        foreach (var item in toRemove)
            PaletteItems.Remove(item);

        // Reconnect filter
        if (_paletteFilter != null)
        {
            _paletteFilter.Items = PaletteItems;
            _paletteFilter.ApplyFilter();
        }

        if (toRemove.Count > 0)
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Cleared {toRemove.Count} module items from palette");
    }

    /// <summary>
    /// Scan module HAK files and cache items. Skips HAKs with valid caches.
    /// After scanning, refreshes the aggregated cache data.
    /// </summary>
    private async Task ScanModuleHaksAsync(CancellationToken token = default)
    {
        var moduleDir = GetModuleWorkingDirectory();
        if (string.IsNullOrEmpty(moduleDir))
            return;

        try
        {
            token.ThrowIfCancellationRequested();

            var hakSearchPaths = RadoubSettings.Instance.GetAllHakSearchPaths();
            var result = await _hakScanner.ScanAndCacheModuleHaksAsync(
                moduleDir, hakSearchPaths, _sharedCacheService, token);

            if (result.HaksScanned > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"HAK scan: {result.HaksScanned} scanned, {result.HaksSkipped} cached, {result.TotalItemsScanned} items");

                // Refresh aggregated cache to include HAK items (filtered to active HAKs)
                _sharedCacheService.InvalidateAggregatedCache();
                var aggregated = _sharedCacheService.GetAggregatedCache(_activeHakPaths);
                if (aggregated != null)
                {
                    _cachedPaletteData = aggregated
                        .Where(i => !ExcludedBaseItemTypes.Contains(i.BaseItemType))
                        .ToList();

                    // Reload palette UI to include newly discovered HAK items and their icons
                    await LoadAllPaletteItemsAsync(token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "HAK scan cancelled");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"HAK scan failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolve the active HAK file paths from the current module's IFO.
    /// Returns null if no module is configured (includes all HAKs in aggregation).
    /// Returns empty list if module has no HAKs (excludes all HAKs from aggregation).
    /// </summary>
    private List<string>? ResolveActiveHakPaths()
    {
        var moduleDir = GetModuleWorkingDirectory();
        if (string.IsNullOrEmpty(moduleDir))
            return null; // No module — include all cached HAKs

        var hakSearchPaths = RadoubSettings.Instance.GetAllHakSearchPaths();
        return _hakScanner.ResolveModuleHakPaths(moduleDir, hakSearchPaths);
    }

    /// <summary>
    /// Get the module working directory (unpacked module folder).
    /// Resolves .mod file paths to their unpacked directories.
    /// </summary>
    private static string? GetModuleWorkingDirectory()
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (!RadoubSettings.IsValidModulePath(modulePath))
            return null;

        // If it's a .mod file, look for the unpacked directory alongside it
        if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
        {
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var moduleDir = Path.GetDirectoryName(modulePath);
            if (!string.IsNullOrEmpty(moduleDir))
            {
                var candidate = Path.Combine(moduleDir, moduleName);
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }

        // It's already a directory path
        if (Directory.Exists(modulePath))
            return modulePath;

        return null;
    }

    #endregion
}

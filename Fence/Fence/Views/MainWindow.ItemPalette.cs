using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MerchantEditor.ViewModels;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.Formats.Uti;
using Radoub.UI.Services;
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

    // Track which types have been loaded (for on-demand loading)
    private readonly HashSet<string> _loadedItemTypes = new(StringComparer.OrdinalIgnoreCase);
    private bool _allItemsLoaded;
    private List<SharedPaletteCacheItem>? _cachedPaletteData;
    private string? _lastModuleDirectory;

    // Active HAK paths from current module's IFO (for filtered aggregation)
    private List<string>? _activeHakPaths;

    #region Item Palette

    private void PopulateTypeFilter()
    {
        if (_baseItemTypeService == null) return;

        var types = _baseItemTypeService.GetBaseItemTypes();
        var items = new List<string> { "(All Types)" };
        items.AddRange(types.Select(t => t.DisplayName));
        ItemTypeFilter.ItemsSource = items;
        ItemTypeFilter.SelectedIndex = 0;
    }

    /// <summary>
    /// Initialize palette - don't load items yet, just prepare the cache.
    /// Items are loaded on-demand when user filters or searches.
    /// </summary>
    private void StartItemPaletteLoad()
    {
        if (_gameDataService == null || !_gameDataService.IsConfigured)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Item palette load skipped - GameDataService not configured");
            UpdateStatusBar("Ready (item palette unavailable - configure game paths in Settings)");
            return;
        }

        // Bind grid to collection
        ItemPaletteGrid.ItemsSource = PaletteItems;

        // Pre-warm cache in background (but don't display items yet)
        _ = PreWarmCacheAsync();

        UpdateStatusBar("Ready - select an item type to browse");
    }

    /// <summary>
    /// Pre-warm the cache in background so it's ready when user filters.
    /// Uses shared cross-tool cache.
    /// </summary>
    private async Task PreWarmCacheAsync()
    {
        try
        {
            // Resolve active HAK paths for module-aware filtering
            _activeHakPaths = ResolveActiveHakPaths();

            // Try to load from shared cache (filtered to active HAKs)
            var aggregated = await Task.Run(() => _sharedCacheService.GetAggregatedCache(_activeHakPaths));
            if (aggregated != null)
            {
                // Filter out excluded base item types
                _cachedPaletteData = aggregated
                    .Where(i => !ExcludedBaseItemTypes.Contains(i.BaseItemType))
                    .ToList();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Cache pre-warmed from shared cache: {_cachedPaletteData.Count} items ready");

                // Scan module HAKs in background (skips cached, refreshes stale)
                _ = ScanModuleHaksAsync();
                return;
            }

            // No shared cache - build it in background
            UnifiedLogger.LogApplication(LogLevel.INFO, "Building palette cache in background...");
            await BuildCacheInBackgroundAsync();

            // Scan module HAKs after building base caches
            await ScanModuleHaksAsync();
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
                    var resolved = _itemResolutionService!.ResolveItem(resourceInfo.ResRef);
                    if (resolved != null && !ExcludedBaseItemTypes.Contains(resolved.BaseItemType))
                    {
                        cacheItems.Add(new SharedPaletteCacheItem
                        {
                            ResRef = resolved.ResRef,
                            DisplayName = resolved.DisplayName,
                            BaseItemTypeName = resolved.BaseItemTypeName,
                            BaseItemType = resolved.BaseItemType,
                            BaseValue = (uint)resolved.BaseCost,
                            Tag = resolved.Tag,
                            IsStandard = resourceInfo.Source == GameResourceSource.Bif
                        });
                        existingResRefs.Add(resourceInfo.ResRef);
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
    /// Load items for a specific type filter. Called when user changes filter.
    /// </summary>
    public async Task LoadItemsForTypeAsync(string? typeFilter)
    {
        // If loading all items and already done, skip
        if (string.IsNullOrEmpty(typeFilter) && _allItemsLoaded)
            return;

        // If loading specific type and already loaded, skip
        if (!string.IsNullOrEmpty(typeFilter) && _loadedItemTypes.Contains(typeFilter))
            return;

        UpdateStatusBar($"Loading {typeFilter ?? "all"} items...");

        try
        {
            // Wait for cache if not ready
            if (_cachedPaletteData == null)
            {
                await PreWarmCacheAsync();
            }

            if (_cachedPaletteData == null)
            {
                UpdateStatusBar("Ready (no items available)");
                return;
            }

            // Filter cached data - use BaseItemTypeName for type matching
            var itemsToAdd = string.IsNullOrEmpty(typeFilter)
                ? _cachedPaletteData
                : _cachedPaletteData.Where(i => i.BaseItemTypeName.Equals(typeFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            // Convert to view models and add (skip already loaded)
            var existingResRefs = new HashSet<string>(PaletteItems.Select(p => p.ResRef), StringComparer.OrdinalIgnoreCase);
            var newItems = new List<PaletteItemViewModel>();

            foreach (var cached in itemsToAdd)
            {
                if (!existingResRefs.Contains(cached.ResRef))
                {
                    newItems.Add(new PaletteItemViewModel
                    {
                        ResRef = cached.ResRef,
                        DisplayName = cached.DisplayName,
                        BaseItemType = cached.BaseItemTypeName,
                        BaseItemIndex = cached.BaseItemType,
                        BaseValue = (int)cached.BaseValue,
                        Tag = cached.Tag,
                        IsStandard = cached.IsStandard
                    });
                }
            }

            // Add to UI
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var item in newItems)
                {
                    PaletteItems.Add(item);
                }
            });

            // Track what's loaded
            if (string.IsNullOrEmpty(typeFilter))
            {
                _allItemsLoaded = true;
            }
            else
            {
                _loadedItemTypes.Add(typeFilter);
            }

            var total = PaletteItems.Count;
            UpdateStatusBar($"Ready - {total} items loaded");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {newItems.Count} {typeFilter ?? "all"} items (total: {total})");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load items: {ex.Message}");
            UpdateStatusBar("Ready (load failed)");
        }
    }

    /// <summary>
    /// Clear and reload the palette cache. Called from Settings.
    /// Returns a task that completes when rebuild is done.
    /// </summary>
    public Task ClearAndReloadPaletteCacheAsync()
    {
        _sharedCacheService.ClearAllCaches();
        _cachedPaletteData = null;
        _activeHakPaths = null;
        _loadedItemTypes.Clear();
        _allItemsLoaded = false;
        PaletteItems.Clear();

        // Rebuild cache and refresh the current filter
        return RebuildCacheAndRefreshAsync();
    }

    /// <summary>
    /// Rebuild cache from scratch and refresh the palette display.
    /// </summary>
    private async Task RebuildCacheAndRefreshAsync()
    {
        try
        {
            UpdateStatusBar("Rebuilding palette cache...");

            // Build the cache
            await BuildCacheInBackgroundAsync();

            // Refresh the current filter selection
            var selectedType = ItemTypeFilter.SelectedItem as string;
            if (!string.IsNullOrEmpty(selectedType) && selectedType != "All Items")
            {
                await LoadItemsForTypeAsync(selectedType);
            }
            else if (selectedType == "All Items")
            {
                await LoadItemsForTypeAsync(null);
            }

            UpdateStatusBar("Cache rebuilt successfully");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Cache rebuild failed: {ex.Message}");
            UpdateStatusBar("Cache rebuild failed");
        }
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
                    PaletteItems.Add(new PaletteItemViewModel
                    {
                        ResRef = resolved.ResRef,
                        DisplayName = resolved.DisplayName,
                        BaseItemType = resolved.BaseItemTypeName,
                        BaseItemIndex = resolved.BaseItemType,
                        BaseValue = resolved.BaseCost,
                        Tag = resolved.Tag,
                        IsStandard = false,
                        IsModuleItem = true
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
        var toRemove = PaletteItems.Where(p => p.IsModuleItem).ToList();

        foreach (var item in toRemove)
            PaletteItems.Remove(item);

        if (toRemove.Count > 0)
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Cleared {toRemove.Count} module items from palette");
    }

    /// <summary>
    /// Scan module HAK files and cache items. Skips HAKs with valid caches.
    /// After scanning, refreshes the aggregated cache data.
    /// </summary>
    private async Task ScanModuleHaksAsync()
    {
        var moduleDir = GetModuleWorkingDirectory();
        if (string.IsNullOrEmpty(moduleDir))
            return;

        try
        {
            var hakSearchPaths = RadoubSettings.Instance.GetAllHakSearchPaths();
            var result = await _hakScanner.ScanAndCacheModuleHaksAsync(
                moduleDir, hakSearchPaths, _sharedCacheService, CancellationToken.None);

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
                }
            }
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

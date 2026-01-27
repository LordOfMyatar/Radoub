using Radoub.Formats.Logging;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.Services;
using Radoub.UI.ViewModels;
using Quartermaster.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Views;

/// <summary>
/// MainWindow partial class for item palette population.
/// Uses modular per-source caching - each source (BIF, Override, HAK) has independent cache.
/// Module folder items are scanned fresh (already unpacked files).
/// </summary>
public partial class MainWindow
{
    // Modular cache service with per-source granularity
    private readonly ModularPaletteCacheService _modularCacheService = new();

    // Cancellation token for background cache building
    private CancellationTokenSource? _paletteCacheCts;

    // Track loaded state
    private List<CachedPaletteItem>? _cachedPaletteData;
    private bool _paletteLoaded;

    // Track module directory for clearing stale module items
    private string? _lastModuleDirectory;

    /// <summary>
    /// Initialize palette loading - pre-warm cache in background but don't populate UI.
    /// Called on app startup.
    /// </summary>
    public void StartGameItemsLoad()
    {
        if (!_gameDataService.IsConfigured)
        {
            UnifiedLogger.LogInventory(LogLevel.WARN, "Item palette unavailable - GameDataService not configured");
            return;
        }

        // Pre-warm cache in background (non-blocking)
        _ = PreWarmCacheAsync();
    }

    /// <summary>
    /// Pre-warm the cache in background so it's ready when user navigates to Inventory.
    /// Uses modular caching - only rebuilds sources that need updating.
    /// </summary>
    private async Task PreWarmCacheAsync()
    {
        try
        {
            // Try to load aggregated cache from existing source caches
            _cachedPaletteData = await Task.Run(() => _modularCacheService.GetAggregatedCache());

            if (_cachedPaletteData != null && _cachedPaletteData.Count > 0)
            {
                UnifiedLogger.LogInventory(LogLevel.INFO, $"Cache pre-warmed from existing sources: {_cachedPaletteData.Count} items");

                // Check if any sources need rebuilding
                _ = RebuildStaleCachesAsync();
                return;
            }

            // No valid caches - build all from scratch
            UnifiedLogger.LogInventory(LogLevel.INFO, "Building palette caches in background...");
            await BuildAllCachesAsync();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogInventory(LogLevel.WARN, $"Cache pre-warm failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Rebuild only the source caches that are stale or missing.
    /// Runs in background without blocking UI.
    /// </summary>
    private async Task RebuildStaleCachesAsync()
    {
        _paletteCacheCts?.Cancel();
        _paletteCacheCts = new CancellationTokenSource();
        var token = _paletteCacheCts.Token;

        var sourcesToRebuild = new List<GameResourceSource>();

        // Check which sources need rebuilding
        if (!_modularCacheService.HasValidSourceCache(GameResourceSource.Bif))
            sourcesToRebuild.Add(GameResourceSource.Bif);

        if (!_modularCacheService.HasValidSourceCache(GameResourceSource.Override))
            sourcesToRebuild.Add(GameResourceSource.Override);

        // HAK caches are validated individually when loading aggregated cache
        // For now, we'll rebuild all HAKs if any are missing

        if (sourcesToRebuild.Count == 0)
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, "All source caches are valid");
            return;
        }

        UnifiedLogger.LogInventory(LogLevel.INFO, $"Rebuilding {sourcesToRebuild.Count} stale cache(s): {string.Join(", ", sourcesToRebuild)}");

        await Task.Run(async () =>
        {
            foreach (var source in sourcesToRebuild)
            {
                if (token.IsCancellationRequested)
                    break;

                await BuildSourceCacheAsync(source, token);
            }
        }, token);

        // Refresh aggregated cache
        if (!token.IsCancellationRequested)
        {
            _modularCacheService.InvalidateAggregatedCache();
            _cachedPaletteData = _modularCacheService.GetAggregatedCache();
        }
    }

    /// <summary>
    /// Build all source caches from scratch.
    /// </summary>
    private async Task BuildAllCachesAsync()
    {
        _paletteCacheCts?.Cancel();
        _paletteCacheCts = new CancellationTokenSource();
        var token = _paletteCacheCts.Token;

        await Task.Run(async () =>
        {
            // Build each source independently
            await BuildSourceCacheAsync(GameResourceSource.Bif, token);
            if (token.IsCancellationRequested) return;

            await BuildSourceCacheAsync(GameResourceSource.Override, token);
            if (token.IsCancellationRequested) return;

            // HAKs would be built here when HAK support is added
            // For now, HAK items are included with Override
        }, token);

        // Load aggregated result
        if (!token.IsCancellationRequested)
        {
            _cachedPaletteData = _modularCacheService.GetAggregatedCache();
            UnifiedLogger.LogInventory(LogLevel.INFO, $"All caches built: {_cachedPaletteData?.Count ?? 0} total items");
        }
    }

    /// <summary>
    /// Build cache for a specific source type.
    /// </summary>
    private async Task BuildSourceCacheAsync(GameResourceSource source, CancellationToken token)
    {
        var cacheItems = new List<CachedPaletteItem>();
        var existingResRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var gameResources = _gameDataService.ListResources(ResourceTypes.Uti)
            .Where(r => r.Source == source)
            .ToList();

        UnifiedLogger.LogInventory(LogLevel.INFO, $"Building {source} cache from {gameResources.Count} UTI resources...");

        foreach (var resourceInfo in gameResources)
        {
            if (token.IsCancellationRequested)
                break;

            if (existingResRefs.Contains(resourceInfo.ResRef))
                continue;

            try
            {
                var utiData = _gameDataService.FindResource(resourceInfo.ResRef, ResourceTypes.Uti);
                if (utiData != null)
                {
                    var item = UtiReader.Read(utiData);
                    var displayName = _itemViewModelFactory.GetItemDisplayName(item);
                    var baseItemTypeName = _itemViewModelFactory.GetBaseItemTypeName(item.BaseItem);

                    cacheItems.Add(new CachedPaletteItem
                    {
                        ResRef = resourceInfo.ResRef,
                        DisplayName = displayName,
                        BaseItemTypeName = baseItemTypeName,
                        BaseItemType = item.BaseItem,
                        BaseValue = item.Cost,
                        IsStandard = source == GameResourceSource.Bif
                    });
                    existingResRefs.Add(resourceInfo.ResRef);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Failed to cache item {resourceInfo.ResRef}: {ex.Message}");
            }
        }

        if (!token.IsCancellationRequested && cacheItems.Count > 0)
        {
            await _modularCacheService.SaveSourceCacheAsync(source, cacheItems);
            UnifiedLogger.LogInventory(LogLevel.INFO, $"{source} cache complete: {cacheItems.Count} items");
        }
    }

    /// <summary>
    /// Load palette items into the UI. Called when user navigates to Inventory panel.
    /// Uses cached data if available, otherwise waits for cache to be ready.
    /// </summary>
    public async Task LoadPaletteItemsAsync()
    {
        if (_paletteLoaded)
            return;

        UpdateStatus("Loading item palette...");

        try
        {
            // Wait for cache if not ready
            if (_cachedPaletteData == null)
            {
                await PreWarmCacheAsync();
            }

            if (_cachedPaletteData == null || _cachedPaletteData.Count == 0)
            {
                UpdateStatus("Ready (no items available)");
                _paletteLoaded = true;
                return;
            }

            // Load all items at once (no batching - faster)
            var standardItems = _cachedPaletteData.Where(i => i.IsStandard).ToList();
            var customItems = _cachedPaletteData.Where(i => !i.IsStandard).ToList();

            UnifiedLogger.LogInventory(LogLevel.INFO, $"Loading {standardItems.Count} standard + {customItems.Count} custom items");

            // Create all view models on background thread
            var viewModels = await Task.Run(() =>
            {
                var vms = new List<ItemViewModel>(_cachedPaletteData.Count);
                foreach (var cached in _cachedPaletteData)
                {
                    vms.Add(new ItemViewModel
                    {
                        ResRef = cached.ResRef,
                        Name = cached.DisplayName,
                        BaseItemName = cached.BaseItemTypeName,
                        BaseItem = cached.BaseItemType,
                        Value = cached.BaseValue,
                        Tag = cached.ResRef,
                        PropertiesDisplay = string.Empty,
                        Source = cached.IsStandard ? GameResourceSource.Bif : GameResourceSource.Override
                    });
                }
                return vms;
            });

            // Set all items at once (efficient - avoids per-item filter updates)
            InventoryPanelContent.SetPaletteItems(viewModels);

            _paletteLoaded = true;
            UpdateStatus($"Ready - {viewModels.Count:N0} items loaded");
            UnifiedLogger.LogInventory(LogLevel.INFO, $"Palette loaded: {viewModels.Count} items");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogInventory(LogLevel.ERROR, $"Failed to load palette: {ex.Message}");
            UpdateStatus("Ready (palette load failed)");
            _paletteLoaded = true;
        }
    }

    /// <summary>
    /// Populates the item palette from module directory (loose UTI files).
    /// Called when a creature file is loaded. Module items are NOT cached.
    /// </summary>
    private void PopulateItemPalette()
    {
        var newModuleDir = string.IsNullOrEmpty(_currentFilePath)
            ? null
            : Path.GetDirectoryName(_currentFilePath);

        // Clear old module items if directory changed
        if (_lastModuleDirectory != newModuleDir)
        {
            ClearModuleItems();
            _lastModuleDirectory = newModuleDir;
        }

        var moduleItemCount = 0;

        if (!string.IsNullOrEmpty(newModuleDir) && Directory.Exists(newModuleDir))
        {
            var utiFiles = Directory.GetFiles(newModuleDir, "*.uti", SearchOption.TopDirectoryOnly);
            foreach (var utiPath in utiFiles)
            {
                try
                {
                    var item = UtiReader.Read(utiPath);
                    var viewModel = _itemViewModelFactory.Create(item, GameResourceSource.Module);
                    SetupLazyIconLoading(viewModel);

                    if (!InventoryPanelContent.PaletteItems.Any(p => p.ResRef.Equals(viewModel.ResRef, StringComparison.OrdinalIgnoreCase)))
                    {
                        InventoryPanelContent.PaletteItems.Add(viewModel);
                        moduleItemCount++;
                    }
                }
                catch (Exception ex)
                {
                    var fileName = Path.GetFileName(utiPath);
                    UnifiedLogger.LogInventory(LogLevel.WARN, $"Failed to load UTI {fileName}: {ex.Message}");
                }
            }
        }

        if (moduleItemCount > 0)
        {
            UnifiedLogger.LogInventory(LogLevel.INFO, $"Added {moduleItemCount} module items to palette");
        }
    }

    /// <summary>
    /// Clear module items from the palette (when switching to different module folder).
    /// </summary>
    private void ClearModuleItems()
    {
        var moduleItems = InventoryPanelContent.PaletteItems
            .Where(i => i.Source == GameResourceSource.Module)
            .ToList();

        foreach (var item in moduleItems)
        {
            InventoryPanelContent.PaletteItems.Remove(item);
        }

        if (moduleItems.Count > 0)
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Cleared {moduleItems.Count} module items from palette");
        }
    }

    /// <summary>
    /// Clear and reload all palette caches. Called from Settings.
    /// </summary>
    public void ClearAndReloadPaletteCache()
    {
        _modularCacheService.ClearAllCaches();
        _cachedPaletteData = null;
        _paletteLoaded = false;
        _lastModuleDirectory = null;
        InventoryPanelContent.PaletteItems.Clear();
        StartGameItemsLoad();
    }

    /// <summary>
    /// Get cache statistics for display in Settings.
    /// </summary>
    public CacheStatistics GetPaletteCacheStatistics() => _modularCacheService.GetCacheStatistics();

    /// <summary>
    /// Sets up lazy icon loading for an item ViewModel.
    /// </summary>
    private void SetupLazyIconLoading(ItemViewModel itemVm)
    {
        if (!_itemIconService.IsGameDataAvailable)
            return;

        itemVm.SetIconLoader(item => _itemIconService.GetItemIcon(item));
    }
}

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
/// Uses on-demand loading - items are loaded when user navigates to Inventory panel.
/// Cache is built in background on startup for fast subsequent access.
/// </summary>
public partial class MainWindow
{
    // Palette cache service for disk caching
    private readonly PaletteCacheService _paletteCacheService = new();

    // Cancellation token for background cache building
    private CancellationTokenSource? _paletteCacheCts;

    // Track loaded state
    private List<CachedPaletteItem>? _cachedPaletteData;
    private bool _paletteLoaded;

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
    /// Does NOT populate the UI - that happens on-demand.
    /// </summary>
    private async Task PreWarmCacheAsync()
    {
        try
        {
            if (_paletteCacheService.HasValidCache())
            {
                // Load existing cache into memory
                _cachedPaletteData = await Task.Run(() => _paletteCacheService.LoadCache());
                if (_cachedPaletteData != null)
                {
                    UnifiedLogger.LogInventory(LogLevel.INFO, $"Cache pre-warmed: {_cachedPaletteData.Count} items ready");
                    return;
                }
            }

            // No valid cache - build it in background
            UnifiedLogger.LogInventory(LogLevel.INFO, "Building palette cache in background...");
            await BuildCacheInBackgroundAsync();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogInventory(LogLevel.WARN, $"Cache pre-warm failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Build the full cache in background without displaying items.
    /// This runs entirely on background threads until complete.
    /// </summary>
    private async Task BuildCacheInBackgroundAsync()
    {
        _paletteCacheCts?.Cancel();
        _paletteCacheCts = new CancellationTokenSource();
        var token = _paletteCacheCts.Token;

        var cacheItems = new List<CachedPaletteItem>();

        await Task.Run(() =>
        {
            var gameResources = _gameDataService.ListResources(ResourceTypes.Uti).ToList();
            var existingResRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            UnifiedLogger.LogInventory(LogLevel.INFO, $"Building cache from {gameResources.Count} UTI resources...");

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
                            IsStandard = resourceInfo.Source == GameResourceSource.Bif
                        });
                        existingResRefs.Add(resourceInfo.ResRef);
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Failed to cache item {resourceInfo.ResRef}: {ex.Message}");
                }
            }
        }, token);

        if (!token.IsCancellationRequested)
        {
            // Save cache to disk
            await _paletteCacheService.SaveCacheAsync(cacheItems);
            _cachedPaletteData = cacheItems;
            UnifiedLogger.LogInventory(LogLevel.INFO, $"Background cache complete: {cacheItems.Count} items");
        }
    }

    // Batch size for UI updates - small enough to keep UI responsive
    private const int UIBatchSize = 100;

    /// <summary>
    /// Load palette items into the UI. Called when user navigates to Inventory panel.
    /// Uses cached data if available, otherwise waits for cache to be ready.
    /// Adds items in small batches to keep UI responsive.
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

            if (_cachedPaletteData == null)
            {
                UpdateStatus("Ready (no items available)");
                _paletteLoaded = true;
                return;
            }

            // Load standard items first (visible immediately), then custom items
            // Filter defaults to hiding custom, so users see fast initial load
            var standardItems = _cachedPaletteData.Where(i => i.IsStandard).ToList();
            var customItems = _cachedPaletteData.Where(i => !i.IsStandard).ToList();
            var allItems = standardItems.Concat(customItems).ToList();
            var totalItems = allItems.Count;
            var loadedCount = 0;

            UnifiedLogger.LogInventory(LogLevel.INFO, $"Loading {standardItems.Count} standard + {customItems.Count} custom items");

            // Add items in small batches to keep UI responsive
            for (int i = 0; i < totalItems; i += UIBatchSize)
            {
                var batch = allItems.Skip(i).Take(UIBatchSize).ToList();

                // Create view models on background thread
                var viewModels = await Task.Run(() =>
                {
                    var vms = new List<ItemViewModel>(batch.Count);
                    foreach (var cached in batch)
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

                // Add batch to UI
                foreach (var vm in viewModels)
                {
                    InventoryPanelContent.PaletteItems.Add(vm);
                }

                loadedCount += viewModels.Count;

                // Update status periodically
                if (i % 500 == 0 || loadedCount >= totalItems)
                {
                    UpdateStatus($"Loading items... {loadedCount:N0} / {totalItems:N0}");
                }

                // Yield to UI thread to process events (clicks, repaints)
                await Task.Delay(1);
            }

            _paletteLoaded = true;
            UpdateStatus($"Ready - {totalItems:N0} items loaded");
            UnifiedLogger.LogInventory(LogLevel.INFO, $"Palette loaded: {totalItems} items");
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
    /// Called when a creature file is loaded.
    /// </summary>
    private void PopulateItemPalette()
    {
        var moduleItemCount = 0;

        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            var moduleDir = Path.GetDirectoryName(_currentFilePath);
            if (!string.IsNullOrEmpty(moduleDir) && Directory.Exists(moduleDir))
            {
                var utiFiles = Directory.GetFiles(moduleDir, "*.uti", SearchOption.TopDirectoryOnly);
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
        }

        if (moduleItemCount > 0)
        {
            UnifiedLogger.LogInventory(LogLevel.INFO, $"Added {moduleItemCount} module items to palette");
        }
    }

    /// <summary>
    /// Clear and reload the palette cache. Called from Settings.
    /// </summary>
    public void ClearAndReloadPaletteCache()
    {
        _paletteCacheService.ClearCache();
        _cachedPaletteData = null;
        _paletteLoaded = false;
        InventoryPanelContent.PaletteItems.Clear();
        StartGameItemsLoad();
    }

    /// <summary>
    /// Get cache info for display in Settings.
    /// </summary>
    public CacheInfo? GetPaletteCacheInfo() => _paletteCacheService.GetCacheInfo();

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

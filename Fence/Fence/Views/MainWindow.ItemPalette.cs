using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MerchantEditor.Services;
using MerchantEditor.ViewModels;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: Item Palette loading and filtering
/// Uses on-demand loading - items are loaded when user selects a type filter.
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

    private readonly PaletteCacheService _paletteCacheService = new();

    // Track which types have been loaded (for on-demand loading)
    private readonly HashSet<string> _loadedItemTypes = new(StringComparer.OrdinalIgnoreCase);
    private bool _allItemsLoaded;
    private List<CachedPaletteItem>? _cachedPaletteData;

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
    /// </summary>
    private async Task PreWarmCacheAsync()
    {
        try
        {
            if (_paletteCacheService.HasValidCache())
            {
                _cachedPaletteData = await Task.Run(() => _paletteCacheService.LoadCache());
                if (_cachedPaletteData != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Cache pre-warmed: {_cachedPaletteData.Count} items ready");
                    return;
                }
            }

            // No cache - build it in background
            UnifiedLogger.LogApplication(LogLevel.INFO, "Building palette cache in background...");
            await BuildCacheInBackgroundAsync();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Cache pre-warm failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Build the full cache in background without displaying items.
    /// </summary>
    private async Task BuildCacheInBackgroundAsync()
    {
        var cacheItems = new List<CachedPaletteItem>();

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
                        cacheItems.Add(new CachedPaletteItem
                        {
                            ResRef = resolved.ResRef,
                            DisplayName = resolved.DisplayName,
                            BaseItemType = resolved.BaseItemTypeName,
                            BaseValue = resolved.BaseCost,
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

        // Save and store
        await _paletteCacheService.SaveCacheAsync(cacheItems);
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

            // Filter cached data
            var itemsToAdd = string.IsNullOrEmpty(typeFilter)
                ? _cachedPaletteData
                : _cachedPaletteData.Where(i => i.BaseItemType.Equals(typeFilter, StringComparison.OrdinalIgnoreCase)).ToList();

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
                        BaseItemType = cached.BaseItemType,
                        BaseValue = cached.BaseValue,
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
        _paletteCacheService.ClearCache();
        _cachedPaletteData = null;
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
    /// Get cache info for display in Settings.
    /// </summary>
    public CacheInfo? GetPaletteCacheInfo() => _paletteCacheService.GetCacheInfo();

    #endregion
}

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
/// </summary>
public partial class MainWindow
{
    // Base item types to exclude from palette (creature weapons, internal items)
    // These are game internals that shouldn't appear in merchant stores
    private static readonly HashSet<int> ExcludedBaseItemTypes = new()
    {
        69,  // Creature Bite
        70,  // Creature Claw
        71,  // Creature Gore
        72,  // Creature Slashing
        73,  // Creature Piercing/Bludgeoning
        255, // Invalid/special marker
        // Add more as needed based on baseitems.2da
    };

    private readonly PaletteCacheService _paletteCacheService = new();

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

    private void StartItemPaletteLoad()
    {
        if (_gameDataService == null || !_gameDataService.IsConfigured)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Item palette load skipped - GameDataService not configured");
            UpdateStatusBar("Ready (item palette unavailable - configure game paths in Settings)");
            return;
        }

        _paletteLoadCts?.Cancel();
        _paletteLoadCts = new CancellationTokenSource();

        UnifiedLogger.LogApplication(LogLevel.INFO, "Starting item palette load...");
        _ = LoadItemPaletteWithCacheAsync(_paletteLoadCts.Token);
    }

    private async Task LoadItemPaletteWithCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Try to load from cache first
            if (_paletteCacheService.HasValidCache())
            {
                var cachedItems = await Task.Run(() => _paletteCacheService.LoadCache(), cancellationToken);
                if (cachedItems != null && cachedItems.Count > 0)
                {
                    await LoadFromCacheAsync(cachedItems, cancellationToken);
                    return;
                }
            }

            // No valid cache - show notification and build cache
            await ShowCacheBuildingNotification();
            await BuildAndCachePaletteAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Item palette loading cancelled");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Item palette load failed: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateStatusBar("Ready (item palette load failed)");
            });
        }
    }

    private async Task LoadFromCacheAsync(List<CachedPaletteItem> cachedItems, CancellationToken cancellationToken)
    {
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading {cachedItems.Count} items from cache...");

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            UpdateStatusBar("Loading from cache...");
        });

        // Load in batches to keep UI responsive
        var batch = new List<PaletteItemViewModel>();
        var loadedCount = 0;

        foreach (var cached in cachedItems)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            batch.Add(new PaletteItemViewModel
            {
                ResRef = cached.ResRef,
                DisplayName = cached.DisplayName,
                BaseItemType = cached.BaseItemType,
                BaseValue = cached.BaseValue,
                IsStandard = cached.IsStandard
            });
            loadedCount++;

            if (batch.Count >= PaletteBatchSize * 4) // Larger batches for cache since it's fast
            {
                await AddPaletteBatchAsync(batch);
                batch.Clear();
            }
        }

        // Add remaining items
        if (batch.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            await AddPaletteBatchAsync(batch);
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {loadedCount} items from cache");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateStatusBar($"Ready - {loadedCount} items in palette");
            });
        }
    }

    private async Task ShowCacheBuildingNotification()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var notification = new Window
            {
                Title = "Building Item Cache",
                Width = 350,
                Height = 120,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Building item palette cache...",
                            FontWeight = FontWeight.SemiBold
                        },
                        new TextBlock
                        {
                            Text = "This only happens once. Future launches will be faster.",
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = Brushes.Gray
                        }
                    }
                }
            };

            notification.Show(this);

            // Auto-close after delay - give users time to read the message
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try { notification.Close(); } catch { }
                });
            });
        });
    }

    private async Task BuildAndCachePaletteAsync(CancellationToken cancellationToken)
    {
        // Run ALL heavy work on background thread, only touch UI for batch updates
        var cacheItems = new List<CachedPaletteItem>();
        var loadedCount = 0;
        var lastUIUpdate = 0;

        await Task.Run(async () =>
        {
            // List all UTI resources from game data
            var gameResources = _gameDataService!.ListResources(ResourceTypes.Uti).ToList();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Found {gameResources.Count} UTI resources in game data");

            var existingResRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var batch = new List<PaletteItemViewModel>();

            foreach (var resourceInfo in gameResources)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (existingResRefs.Contains(resourceInfo.ResRef))
                    continue;

                try
                {
                    // Resolve the item to get display info - this is the expensive part
                    var resolved = _itemResolutionService!.ResolveItem(resourceInfo.ResRef);
                    if (resolved != null)
                    {
                        // Skip creature weapons and internal item types
                        if (ExcludedBaseItemTypes.Contains(resolved.BaseItemType))
                            continue;

                        var viewModel = new PaletteItemViewModel
                        {
                            ResRef = resolved.ResRef,
                            DisplayName = resolved.DisplayName,
                            BaseItemType = resolved.BaseItemTypeName,
                            BaseValue = resolved.BaseCost,
                            IsStandard = resourceInfo.Source == GameResourceSource.Bif
                        };

                        batch.Add(viewModel);

                        // Also add to cache list
                        cacheItems.Add(new CachedPaletteItem
                        {
                            ResRef = resolved.ResRef,
                            DisplayName = resolved.DisplayName,
                            BaseItemType = resolved.BaseItemTypeName,
                            BaseValue = resolved.BaseCost,
                            IsStandard = resourceInfo.Source == GameResourceSource.Bif
                        });

                        existingResRefs.Add(resourceInfo.ResRef);
                        loadedCount++;

                        // Add batch to UI periodically (larger batches = fewer UI updates = less jank)
                        if (batch.Count >= PaletteBatchSize * 4)
                        {
                            var batchCopy = batch.ToList();
                            batch.Clear();

                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                foreach (var item in batchCopy)
                                {
                                    PaletteItems.Add(item);
                                }
                            });

                            // Update status less frequently (every 500 items)
                            if (loadedCount - lastUIUpdate >= 500)
                            {
                                lastUIUpdate = loadedCount;
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    UpdateStatusBar($"Building cache... {loadedCount} items");
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Failed to load palette item {resourceInfo.ResRef}: {ex.Message}");
                }
            }

            // Add remaining items
            if (batch.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                var finalBatch = batch.ToList();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var item in finalBatch)
                    {
                        PaletteItems.Add(item);
                    }
                });
            }
        }, cancellationToken);

        if (!cancellationToken.IsCancellationRequested)
        {
            // Save cache for next time (on background thread)
            await _paletteCacheService.SaveCacheAsync(cacheItems);

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Item palette build complete: {loadedCount} items cached");
            UpdateStatusBar($"Ready - {loadedCount} items in palette");
        }
    }

    /// <summary>
    /// Clear and reload the palette cache. Called from Settings.
    /// </summary>
    public void ClearAndReloadPaletteCache()
    {
        _paletteCacheService.ClearCache();
        PaletteItems.Clear();
        StartItemPaletteLoad();
    }

    /// <summary>
    /// Get cache info for display in Settings.
    /// </summary>
    public CacheInfo? GetPaletteCacheInfo() => _paletteCacheService.GetCacheInfo();

    private async Task AddPaletteBatchAsync(List<PaletteItemViewModel> batch)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var item in batch)
            {
                PaletteItems.Add(item);
            }
            ItemPaletteGrid.ItemsSource = PaletteItems;
        });
    }

    #endregion
}

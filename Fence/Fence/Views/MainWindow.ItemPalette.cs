using Avalonia.Threading;
using MerchantEditor.ViewModels;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
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
    #region Item Palette

    private void PopulateTypeFilter()
    {
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
            return;
        }

        _paletteLoadCts?.Cancel();
        _paletteLoadCts = new CancellationTokenSource();

        UnifiedLogger.LogApplication(LogLevel.INFO, "Starting background load for item palette...");
        _ = LoadItemPaletteAsync(_paletteLoadCts.Token);
    }

    private async Task LoadItemPaletteAsync(CancellationToken cancellationToken)
    {
        try
        {
            // List all UTI resources from game data
            var gameResources = await Task.Run(() =>
                _gameDataService!.ListResources(ResourceTypes.Uti).ToList(),
                cancellationToken);

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Found {gameResources.Count} UTI resources in game data");

            var existingResRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var batch = new List<PaletteItemViewModel>();
            var loadedCount = 0;

            foreach (var resourceInfo in gameResources)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (existingResRefs.Contains(resourceInfo.ResRef))
                    continue;

                try
                {
                    // Resolve the item to get display info
                    var resolved = _itemResolutionService.ResolveItem(resourceInfo.ResRef);
                    if (resolved != null)
                    {
                        batch.Add(new PaletteItemViewModel
                        {
                            ResRef = resolved.ResRef,
                            DisplayName = resolved.DisplayName,
                            BaseItemType = resolved.BaseItemTypeName,
                            BaseValue = resolved.BaseCost,
                            IsStandard = resourceInfo.Source == GameResourceSource.Bif
                        });
                        existingResRefs.Add(resourceInfo.ResRef);
                        loadedCount++;

                        // Add batch to UI periodically
                        if (batch.Count >= PaletteBatchSize)
                        {
                            await AddPaletteBatchAsync(batch);
                            batch.Clear();
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
                await AddPaletteBatchAsync(batch);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Item palette load complete: {loadedCount} items");
            }
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Item palette loading cancelled");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Item palette load failed: {ex.Message}");
        }
    }

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

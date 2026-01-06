using Radoub.Formats.Logging;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Views;

/// <summary>
/// MainWindow partial class for item palette population.
/// Handles background loading of game items from BIF archives.
/// </summary>
public partial class MainWindow
{
    // Cancellation token for background palette loading
    private CancellationTokenSource? _paletteLoadCts;
    private const int PaletteBatchSize = 50;

    /// <summary>
    /// Starts loading game items (BIF) in background. Called on app startup.
    /// </summary>
    public void StartGameItemsLoad()
    {
        if (!_gameDataService.IsConfigured)
            return;

        _paletteLoadCts?.Cancel();
        _paletteLoadCts = new CancellationTokenSource();

        UnifiedLogger.LogInventory(LogLevel.INFO, "Starting background load for game items...");
        _ = LoadGameItemsAsync(_paletteLoadCts.Token);
    }

    /// <summary>
    /// Populates the item palette from multiple sources:
    /// 1. Module directory (loose UTI files) - loaded synchronously
    /// 2. Base game BIF archives - continues loading in background if not already done
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
    /// Loads game items (Override + BIF) in batches to avoid blocking UI.
    /// </summary>
    private async Task LoadGameItemsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var gameResources = await Task.Run(() =>
                _gameDataService.ListResources(ResourceTypes.Uti).ToList(),
                cancellationToken);

            var existingResRefs = new HashSet<string>(
                InventoryPanelContent.PaletteItems.Select(p => p.ResRef),
                StringComparer.OrdinalIgnoreCase);

            var batch = new List<ItemViewModel>();
            var gameItemCount = 0;

            foreach (var resourceInfo in gameResources)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (existingResRefs.Contains(resourceInfo.ResRef))
                    continue;

                try
                {
                    var utiData = await Task.Run(() =>
                        _gameDataService.FindResource(resourceInfo.ResRef, ResourceTypes.Uti),
                        cancellationToken);

                    if (utiData != null)
                    {
                        var item = UtiReader.Read(utiData);
                        var viewModel = _itemViewModelFactory.Create(item, resourceInfo.Source);
                        SetupLazyIconLoading(viewModel);
                        batch.Add(viewModel);
                        existingResRefs.Add(resourceInfo.ResRef);
                        gameItemCount++;

                        if (batch.Count >= PaletteBatchSize)
                        {
                            await AddBatchToUIAsync(batch);
                            batch.Clear();
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Failed to load UTI {resourceInfo.ResRef}: {ex.Message}");
                }
            }

            if (batch.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                await AddBatchToUIAsync(batch);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                UnifiedLogger.LogInventory(LogLevel.INFO, $"Background load complete: {gameItemCount} game items added to palette");
            }
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogInventory(LogLevel.DEBUG, "Palette loading cancelled");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogInventory(LogLevel.ERROR, $"Error loading game items: {ex.Message}");
        }
    }

    private async Task AddBatchToUIAsync(List<ItemViewModel> batch)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var item in batch)
            {
                InventoryPanelContent.PaletteItems.Add(item);
            }
        });
    }

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

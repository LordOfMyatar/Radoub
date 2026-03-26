using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MerchantEditor.ViewModels;
using Radoub.Formats.Utm;
using Radoub.UI.Services;
using Radoub.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: Store inventory operations (add/remove items, infinite flag, etc.)
/// </summary>
public partial class MainWindow
{
    #region Base Item to Store Panel Mapping

    /// <summary>
    /// Determine which store panel an item belongs to based on its base item type index.
    /// Reads StorePanel column from baseitems.2da via BaseItemTypeService.
    /// Falls back to Miscellaneous if the service is unavailable or type is unknown.
    /// </summary>
    private int GetStorePanelForBaseItemType(int baseItemIndex)
    {
        if (_baseItemTypeService != null)
            return _baseItemTypeService.GetStorePanelForBaseItem(baseItemIndex);

        return StorePanels.Miscellaneous;
    }

    #endregion

    #region Store Inventory Operations

    private void OnRemoveFromStore(object? sender, RoutedEventArgs e)
    {
        var selectedItems = StoreInventoryGrid.SelectedItems?.Cast<StoreItemViewModel>().ToList();
        if (selectedItems == null || selectedItems.Count == 0)
            return;

        foreach (var item in selectedItems)
        {
            StoreItems.Remove(item);
        }

        _documentState.MarkDirty();
        UpdateItemCount();

        // Refresh grid view (re-apply filter if active)
        ApplyStoreFilter();
    }

    private void OnSetInfinite(object? sender, RoutedEventArgs e)
    {
        SetInfiniteFlag(true);
    }

    private void OnClearInfinite(object? sender, RoutedEventArgs e)
    {
        SetInfiniteFlag(false);
    }

    private void OnSetAllInfinite(object? sender, RoutedEventArgs e)
    {
        if (StoreItems.Count == 0)
            return;

        foreach (var item in StoreItems)
        {
            item.Infinite = true;
        }

        _documentState.MarkDirty();
    }

    private void SetInfiniteFlag(bool value)
    {
        var selectedItems = StoreInventoryGrid.SelectedItems?.Cast<StoreItemViewModel>().ToList();
        if (selectedItems == null || selectedItems.Count == 0)
            return;

        foreach (var item in selectedItems)
        {
            item.Infinite = value;
        }

        _documentState.MarkDirty();
    }

    private void OnInfiniteCellClicked(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Get the data context (StoreItemViewModel) from the clicked element
        if (sender is Avalonia.Controls.Border border && border.DataContext is StoreItemViewModel item)
        {
            item.Infinite = !item.Infinite;
            _documentState.MarkDirty();
        }
    }

    private void OnAddToStore(object? sender, RoutedEventArgs e)
    {
        AddSelectedPaletteItems();
    }

    private void AddSelectedPaletteItems()
    {
        var selectedItems = ItemPaletteGrid.SelectedItems?.Cast<ItemViewModel>().ToList();
        if (selectedItems == null || selectedItems.Count == 0)
            return;

        // Get current markup/markdown values
        var markUp = int.TryParse(SellMarkupBox.Text, out var mu) ? mu : 100;
        var markDown = int.TryParse(BuyMarkdownBox.Text, out var md) ? md : 50;

        foreach (var item in selectedItems)
        {
            var sellPrice = (int)Math.Ceiling((int)item.Value * markUp / 100.0);
            var buyPrice = (int)Math.Floor((int)item.Value * markDown / 100.0);
            var panelId = GetStorePanelForBaseItemType(item.BaseItem);

            // Debug: Log panel assignment for troubleshooting
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.DEBUG,
                $"Adding item: {item.ResRef} | Type: {item.BaseItemName} | Panel: {panelId} ({StorePanels.GetPanelName(panelId)})");

            StoreItems.Add(new StoreItemViewModel
            {
                ResRef = item.ResRef,
                DisplayName = item.Name,
                Infinite = false,
                PanelId = panelId,
                BaseItemType = item.BaseItemName,
                BaseItemIndex = item.BaseItem,
                BaseValue = (int)item.Value,
                SellPrice = sellPrice,
                BuyPrice = buyPrice,
                IconBitmap = item.IconBitmap ?? _itemIconService?.GetItemIcon(item.BaseItem),
                SourceLocation = item.SourceLocation
            });
        }

        _documentState.MarkDirty();
        UpdateItemCount();

        // Refresh grid view (re-apply filter if active)
        ApplyStoreFilter();
    }

    private void OnStoreInventoryDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Double-click removes from store
        var selectedItems = StoreInventoryGrid.SelectedItems?.Cast<StoreItemViewModel>().ToList();
        if (selectedItems == null || selectedItems.Count == 0)
            return;

        foreach (var item in selectedItems)
        {
            StoreItems.Remove(item);
        }

        _documentState.MarkDirty();
        UpdateItemCount();

        // Refresh grid view (re-apply filter if active)
        ApplyStoreFilter();
    }

    private void OnItemPaletteDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Double-click adds to store
        AddSelectedPaletteItems();
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        OnRemoveFromStore(sender, e);
    }

    private void OnSelectAllStoreItems(object? sender, RoutedEventArgs e)
    {
        StoreInventoryGrid.SelectAll();
    }

    private void OnEditInItemEditor(object? sender, RoutedEventArgs e)
    {
        var selected = StoreInventoryGrid.SelectedItem as StoreItemViewModel;
        if (selected == null) return;

        var resRef = selected.ResRef;
        if (string.IsNullOrEmpty(resRef))
        {
            UpdateStatusBar("No ResRef available for selected item");
            return;
        }

        var moduleDir = ItemEditorLauncher.GetModuleWorkingDirectory();
        var status = ItemEditorLauncher.ResolveAndLaunch(resRef, moduleDir, _gameDataService);
        UpdateStatusBar(status);
    }

    private async void OnRefreshItemPalette(object? sender, RoutedEventArgs e)
    {
        UpdateStatusBar("Refreshing item palette...");
        await ClearAndReloadPaletteCacheAsync();
        UpdateStatusBar("Item palette refreshed");
    }

    #endregion

    #region Buy Restrictions

    private void OnBuyModeChanged(object? sender, RoutedEventArgs e)
    {
        // When mode changes to "Buy All", clear selections
        if (BuyAllRadio.IsChecked == true)
        {
            foreach (var item in SelectableBaseItemTypes)
            {
                item.IsSelected = false;
            }
        }

        if (!_documentState.IsLoading)
            _documentState.MarkDirty();
    }

    private void OnSelectAllTypes(object? sender, RoutedEventArgs e)
    {
        foreach (var item in SelectableBaseItemTypes)
        {
            item.IsSelected = true;
        }

        _documentState.MarkDirty();
    }

    private void OnClearAllTypes(object? sender, RoutedEventArgs e)
    {
        foreach (var item in SelectableBaseItemTypes)
        {
            item.IsSelected = false;
        }

        _documentState.MarkDirty();
    }

    private void OnItemTypeCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (!_documentState.IsLoading)
            _documentState.MarkDirty();
    }

    private void UpdateBuyRestrictions()
    {
        if (_currentStore == null) return;

        _currentStore.WillOnlyBuy.Clear();
        _currentStore.WillNotBuy.Clear();

        if (BuyAllRadio.IsChecked == true)
        {
            // No restrictions - both lists empty
        }
        else if (WillOnlyBuyRadio.IsChecked == true)
        {
            // Store selected items in WillOnlyBuy
            foreach (var item in SelectableBaseItemTypes.Where(t => t.IsSelected))
            {
                _currentStore.WillOnlyBuy.Add(item.BaseItemIndex);
            }
        }
        else if (WillNotBuyRadio.IsChecked == true)
        {
            // Store selected items in WillNotBuy
            foreach (var item in SelectableBaseItemTypes.Where(t => t.IsSelected))
            {
                _currentStore.WillNotBuy.Add(item.BaseItemIndex);
            }
        }
    }

    #endregion

    #region Search

    private void OnStoreSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyStoreFilter();
    }

    private void OnClearStoreSearch(object? sender, RoutedEventArgs e)
    {
        StoreSearchBox.Text = "";
        ApplyStoreFilter();
    }

    private void ApplyStoreFilter()
    {
        var searchText = StoreSearchBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(searchText))
        {
            // Show all items
            StoreInventoryGrid.ItemsSource = StoreItems;
        }
        else
        {
            // Filter by display name, resref, or type
            var filtered = StoreItems.Where(item =>
                item.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                item.ResRef.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                item.BaseItemType.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            StoreInventoryGrid.ItemsSource = filtered;
        }
    }

    #endregion
}

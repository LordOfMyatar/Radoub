using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MerchantEditor.ViewModels;
using Radoub.Formats.Utm;
using System;
using System.Linq;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: Store inventory operations (add/remove items, infinite flag, etc.)
/// </summary>
public partial class MainWindow
{
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

        _isDirty = true;
        UpdateTitle();
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

        _isDirty = true;
        UpdateTitle();

        // Refresh grid
        StoreInventoryGrid.ItemsSource = null;
        StoreInventoryGrid.ItemsSource = StoreItems;
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

        _isDirty = true;
        UpdateTitle();
        StoreInventoryGrid.ItemsSource = null;
        StoreInventoryGrid.ItemsSource = StoreItems;
    }

    private void OnAddToStore(object? sender, RoutedEventArgs e)
    {
        AddSelectedPaletteItems();
    }

    private void AddSelectedPaletteItems()
    {
        var selectedItems = ItemPaletteGrid.SelectedItems?.Cast<PaletteItemViewModel>().ToList();
        if (selectedItems == null || selectedItems.Count == 0)
            return;

        foreach (var item in selectedItems)
        {
            StoreItems.Add(new StoreItemViewModel
            {
                ResRef = item.ResRef,
                DisplayName = item.DisplayName,
                Infinite = false,
                PanelId = StorePanels.Miscellaneous, // Default panel
                BaseItemType = item.BaseItemType,
                SellPrice = 0,
                BuyPrice = 0
            });
        }

        _isDirty = true;
        UpdateTitle();
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

        _isDirty = true;
        UpdateTitle();
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

        _isDirty = true;
        UpdateTitle();
    }

    private void OnSelectAllTypes(object? sender, RoutedEventArgs e)
    {
        foreach (var item in SelectableBaseItemTypes)
        {
            item.IsSelected = true;
        }

        _isDirty = true;
        UpdateTitle();
    }

    private void OnClearAllTypes(object? sender, RoutedEventArgs e)
    {
        foreach (var item in SelectableBaseItemTypes)
        {
            item.IsSelected = false;
        }

        _isDirty = true;
        UpdateTitle();
    }

    private void OnItemTypeCheckChanged(object? sender, RoutedEventArgs e)
    {
        _isDirty = true;
        UpdateTitle();
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

    private async void OnPaletteSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        // If user is searching, load all items first (search needs full dataset)
        var searchText = PaletteSearchBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(searchText) && searchText.Length >= 2)
        {
            await LoadItemsForTypeAsync(null); // Load all items for search
        }
        ApplyPaletteFilter();
    }

    private void OnClearPaletteSearch(object? sender, RoutedEventArgs e)
    {
        PaletteSearchBox.Text = "";
        ApplyPaletteFilter();
    }

    private async void OnTypeFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Load items for the selected type on-demand
        var typeFilter = ItemTypeFilter.SelectedIndex > 0 ? ItemTypeFilter.SelectedItem?.ToString() : null;
        await LoadItemsForTypeAsync(typeFilter);
        ApplyPaletteFilter();
    }

    private void OnSourceFilterChanged(object? sender, RoutedEventArgs e)
    {
        ApplyPaletteFilter();
    }

    private void ApplyPaletteFilter()
    {
        var searchText = PaletteSearchBox.Text?.Trim() ?? "";
        var typeFilter = ItemTypeFilter.SelectedIndex > 0 ? ItemTypeFilter.SelectedItem?.ToString() : null;
        var showStandard = StandardItemsCheck?.IsChecked ?? true;
        var showCustom = CustomItemsCheck?.IsChecked ?? true;

        var filtered = PaletteItems.AsEnumerable();

        // Apply search text filter
        if (!string.IsNullOrEmpty(searchText))
        {
            filtered = filtered.Where(item =>
                item.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                item.ResRef.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                item.BaseItemType.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            );
        }

        // Apply type filter
        if (!string.IsNullOrEmpty(typeFilter))
        {
            filtered = filtered.Where(item =>
                item.BaseItemType.Equals(typeFilter, StringComparison.OrdinalIgnoreCase)
            );
        }

        // Apply source filter
        if (!showStandard || !showCustom)
        {
            filtered = filtered.Where(item =>
                (showStandard && item.IsStandard) || (showCustom && !item.IsStandard)
            );
        }

        ItemPaletteGrid.ItemsSource = filtered.ToList();
    }

    #endregion
}

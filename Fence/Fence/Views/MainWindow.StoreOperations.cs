using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MerchantEditor.ViewModels;
using Radoub.Formats.Utm;
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

    // Base item indices that belong to each store panel
    // Based on NWN base item categories from baseitems.2da
    private static readonly HashSet<int> ArmorItems = new()
    {
        16,  // Armor
        17,  // Belt
        19,  // Boots
        21,  // Cloak
        26,  // Gloves
        28,  // Helmet
        32,  // Large Shield
        34,  // Light Armor (obsolete but may exist)
        36,  // Medium Armor (obsolete but may exist)
        14,  // Small Shield
        59,  // Bracer
        52,  // Tower Shield
        78,  // Armor (heavy)
    };

    private static readonly HashSet<int> WeaponItems = new()
    {
        0,   // Shortsword
        1,   // Longsword
        2,   // Battleaxe
        3,   // Bastardsword
        4,   // Light Flail
        5,   // Warhammer
        6,   // Heavy Crossbow
        7,   // Light Crossbow
        8,   // Longbow
        9,   // Light Mace
        10,  // Halberd
        11,  // Shortbow
        12,  // Two-Bladed Sword
        13,  // Greatsword
        18,  // Bolt
        20,  // Bullet
        22,  // Dagger
        25,  // Arrow
        27,  // Greataxe
        29,  // Morning Star
        30,  // Quarterstaff
        31,  // Rapier
        33,  // Scimitar
        35,  // Scythe
        37,  // Club
        38,  // Sickle
        39,  // Spear
        40,  // Handaxe
        41,  // Kama
        42,  // Katana
        43,  // Kukri
        44,  // Nunchaku
        45,  // Sai
        46,  // Siangham  // (was Shuriken - corrected)
        47,  // Dart
        50,  // Light Hammer
        51,  // Throwing Axe
        53,  // Dire Mace
        54,  // Double Axe
        55,  // Heavy Flail
        56,  // Sling
        57,  // Shuriken
        58,  // Trident
        60,  // Whip
        63,  // Dwarven Waraxe
        64,  // Warmage (Staff)
    };

    private static readonly HashSet<int> PotionScrollItems = new()
    {
        49,  // Potion
        75,  // Scroll
        71,  // Kit (also in misc sometimes)
    };

    private static readonly HashSet<int> RingsAmuletItems = new()
    {
        15,  // Amulet
        48,  // Ring
    };

    /// <summary>
    /// Determine which store panel an item belongs to based on its base item type.
    /// </summary>
    private static int GetStorePanelForBaseItem(int baseItemIndex)
    {
        if (ArmorItems.Contains(baseItemIndex))
            return StorePanels.Armor;
        if (WeaponItems.Contains(baseItemIndex))
            return StorePanels.Weapons;
        if (PotionScrollItems.Contains(baseItemIndex))
            return StorePanels.Potions;
        if (RingsAmuletItems.Contains(baseItemIndex))
            return StorePanels.RingsAmulets;

        // Default to Miscellaneous for gems, wands, rods, misc items, etc.
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
                PanelId = GetStorePanelForBaseItem(item.BaseItemIndex),
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

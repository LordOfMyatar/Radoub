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

    // Store panel mapping by item type NAME (case-insensitive)
    // This is more robust than hardcoded indices since baseitems.2da row numbers
    // can vary between NWN versions or with custom content
    private static readonly HashSet<string> ArmorTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Armor", "Belt", "Boots", "Cloak", "Gloves", "Helmet",
        "Large Shield", "Small Shield", "Tower Shield", "Bracer",
        "Light Armor", "Medium Armor", "Heavy Armor"
    };

    private static readonly HashSet<string> WeaponTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shortsword", "Longsword", "Battleaxe", "Bastardsword", "Bastard Sword",
        "Light Flail", "Warhammer", "War Hammer", "Heavy Crossbow", "Light Crossbow",
        "Longbow", "Long Bow", "Light Mace", "Halberd", "Shortbow", "Short Bow",
        "Two-Bladed Sword", "Greatsword", "Great Sword", "Bolt", "Bullet",
        "Dagger", "Arrow", "Greataxe", "Great Axe", "Morningstar", "Morning Star",
        "Quarterstaff", "Quarter Staff", "Rapier", "Scimitar", "Scythe",
        "Club", "Sickle", "Spear", "Handaxe", "Hand Axe", "Kama", "Katana",
        "Kukri", "Nunchaku", "Sai", "Siangham", "Dart", "Light Hammer",
        "Throwing Axe", "Dire Mace", "Double Axe", "Heavy Flail", "Sling",
        "Shuriken", "Trident", "Whip", "Dwarven Waraxe", "Dwarven War Axe",
        "Warmage", "Magic Staff"
    };

    private static readonly HashSet<string> PotionScrollTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Potion", "Scroll", "Kit", "Trap Kit", "Healer's Kit", "Healers Kit",
        "Thieves' Tools", "Thieves Tools"
    };

    private static readonly HashSet<string> RingsAmuletTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Amulet", "Ring"
    };

    /// <summary>
    /// Determine which store panel an item belongs to based on its base item type NAME.
    /// Uses name-based matching for compatibility with different baseitems.2da versions.
    /// </summary>
    private static int GetStorePanelForBaseItemType(string baseItemTypeName)
    {
        if (string.IsNullOrEmpty(baseItemTypeName))
            return StorePanels.Miscellaneous;

        if (ArmorTypeNames.Contains(baseItemTypeName))
            return StorePanels.Armor;
        if (WeaponTypeNames.Contains(baseItemTypeName))
            return StorePanels.Weapons;
        if (PotionScrollTypeNames.Contains(baseItemTypeName))
            return StorePanels.Potions;
        if (RingsAmuletTypeNames.Contains(baseItemTypeName))
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

    private void OnInfiniteCellClicked(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Get the data context (StoreItemViewModel) from the clicked element
        if (sender is Avalonia.Controls.Border border && border.DataContext is StoreItemViewModel item)
        {
            item.Infinite = !item.Infinite;
            _isDirty = true;
            UpdateTitle();

            // Refresh the grid to show updated symbol
            StoreInventoryGrid.ItemsSource = null;
            StoreInventoryGrid.ItemsSource = StoreItems;
        }
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

        // Get current markup/markdown values
        var markUp = int.TryParse(SellMarkupBox.Text, out var mu) ? mu : 100;
        var markDown = int.TryParse(BuyMarkdownBox.Text, out var md) ? md : 50;

        foreach (var item in selectedItems)
        {
            var sellPrice = (int)Math.Ceiling(item.BaseValue * markUp / 100.0);
            var buyPrice = (int)Math.Floor(item.BaseValue * markDown / 100.0);
            var panelId = GetStorePanelForBaseItemType(item.BaseItemType);

            // Debug: Log panel assignment for troubleshooting
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.DEBUG,
                $"Adding item: {item.ResRef} | Type: {item.BaseItemType} | Panel: {panelId} ({StorePanels.GetPanelName(panelId)})");

            StoreItems.Add(new StoreItemViewModel
            {
                ResRef = item.ResRef,
                DisplayName = item.DisplayName,
                Infinite = false,
                PanelId = panelId,
                BaseItemType = item.BaseItemType,
                BaseValue = item.BaseValue,
                SellPrice = sellPrice,
                BuyPrice = buyPrice
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

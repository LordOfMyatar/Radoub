using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MerchantEditor.Services;
using MerchantEditor.ViewModels;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.ViewModels;
using System.Linq;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: Item details panel selection handling and context menu operations.
///
/// Field display lives in the shared <see cref="Radoub.UI.Controls.ItemDetailsPanel"/>
/// (item-only fields) and <see cref="MerchantEditor.Controls.StoreItemExtrasPanel"/>
/// (Fence-only sell/buy/infinite/panel) — each populated via DataContext.
/// This partial handles selection routing between store-grid and palette-grid and
/// the visibility toggle for the whole details column.
/// </summary>
public partial class MainWindow
{
    #region Item Details

    private bool _updatingSelection;

    private void OnStoreInventorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection) return;

        var selected = StoreInventoryGrid.SelectedItem as StoreItemViewModel;
        if (selected != null)
        {
            _updatingSelection = true;
            ItemPaletteGrid.SelectedItem = null;
            _updatingSelection = false;
            ShowStoreItemDetails(selected);
        }
        else
        {
            ClearItemDetails();
        }
    }

    private void OnPaletteSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection) return;

        var selected = ItemPaletteGrid.SelectedItem as ItemViewModel;
        if (selected != null)
        {
            _updatingSelection = true;
            StoreInventoryGrid.SelectedItem = null;
            _updatingSelection = false;
            ShowPaletteItemDetails(selected);
        }
    }

    private void ShowStoreItemDetails(StoreItemViewModel item)
    {
        // Build a shared ItemViewModel from the store item so the shared panel
        // can render the standard fields. PropertiesDisplay is resolved on demand
        // from the item resolution chain (same as before extraction).
        var resolved = _itemResolutionService?.ResolveItem(item.ResRef);

        var detailVm = new ItemViewModel
        {
            ResRef = item.ResRef,
            Tag = item.Tag,
            BaseItem = item.BaseItemIndex,
            Value = (uint)System.Math.Max(0, item.BaseValue),
            Source = GameResourceSource.Bif,
        };
        detailVm.Name = !string.IsNullOrEmpty(item.DisplayName) ? item.DisplayName : item.ResRef;
        detailVm.BaseItemName = item.BaseItemType;
        detailVm.PropertiesDisplay = resolved?.PropertiesDisplay ?? string.Empty;
        detailVm.SourceLocation = item.SourceLocation;
        detailVm.IconBitmap = item.IconBitmap ?? _itemIconService?.GetItemIcon(item.BaseItemIndex);

        ItemDetailsView.DataContext = detailVm;
        StoreItemExtrasView.DataContext = item;
    }

    private void ShowPaletteItemDetails(ItemViewModel item)
    {
        // Palette items already are ItemViewModel — bind directly.
        // Fence-only extras don't apply to palette items, so clear that DataContext.
        if (item.IconBitmap == null && _itemIconService != null)
        {
            item.IconBitmap = _itemIconService.GetItemIcon(item.BaseItem);
        }

        ItemDetailsView.DataContext = item;
        StoreItemExtrasView.DataContext = null;
    }

    private void ClearItemDetails()
    {
        ItemDetailsView.DataContext = null;
        StoreItemExtrasView.DataContext = null;
    }

    #endregion

    #region Item Details Panel Visibility

    private void OnToggleItemDetailsPanelClick(object? sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance;
        SetItemDetailsPanelVisible(!settings.ItemDetailsPanelVisible);
    }

    private void SetItemDetailsPanelVisible(bool visible)
    {
        var settings = SettingsService.Instance;
        settings.ItemDetailsPanelVisible = visible;

        var inventoryGrid = this.FindControl<Grid>("InventoryPaletteGrid");
        if (inventoryGrid == null)
        {
            // Fall back to finding the Row 2 grid by walking the visual tree
            // The grid is the direct content of Row 2 in MainGrid
            inventoryGrid = FindInventoryGrid();
        }

        if (inventoryGrid == null) return;

        // Columns: 0=Store, 1=Splitter, 2=Palette, 3=DetailsSplitter, 4=Details
        if (inventoryGrid.ColumnDefinitions.Count < 5) return;

        var detailsSplitterColumn = inventoryGrid.ColumnDefinitions[3];
        var detailsColumn = inventoryGrid.ColumnDefinitions[4];

        if (visible)
        {
            detailsSplitterColumn.Width = new GridLength(5, GridUnitType.Pixel);
            detailsColumn.Width = new GridLength(settings.ItemDetailsPanelWidth, GridUnitType.Pixel);
            detailsColumn.MinWidth = 180;
        }
        else
        {
            // Save current width before hiding
            if (detailsColumn.Width.IsAbsolute && detailsColumn.Width.Value > 0)
            {
                settings.ItemDetailsPanelWidth = detailsColumn.Width.Value;
            }

            detailsSplitterColumn.Width = new GridLength(0, GridUnitType.Pixel);
            detailsColumn.Width = new GridLength(0, GridUnitType.Pixel);
            detailsColumn.MinWidth = 0;
        }

        UpdateItemDetailsPanelMenuState();
    }

    private void RestoreItemDetailsPanelState()
    {
        var settings = SettingsService.Instance;
        if (!settings.ItemDetailsPanelVisible)
        {
            SetItemDetailsPanelVisible(false);
        }
        UpdateItemDetailsPanelMenuState();
    }

    private void SaveItemDetailsPanelSize()
    {
        var inventoryGrid = FindInventoryGrid();
        if (inventoryGrid == null || inventoryGrid.ColumnDefinitions.Count < 5) return;

        var detailsColumn = inventoryGrid.ColumnDefinitions[4];
        if (detailsColumn.Width.IsAbsolute && detailsColumn.Width.Value > 0)
        {
            SettingsService.Instance.ItemDetailsPanelWidth = detailsColumn.Width.Value;
        }
    }

    private void UpdateItemDetailsPanelMenuState()
    {
        var menuItem = this.FindControl<MenuItem>("ItemDetailsPanelMenuItem");
        if (menuItem != null)
        {
            var isVisible = SettingsService.Instance.ItemDetailsPanelVisible;
            menuItem.Icon = isVisible ? new TextBlock { Text = "✓" } : null;
        }
    }

    /// <summary>
    /// Find the inventory/palette/details grid (Row 2 of MainGrid).
    /// </summary>
    private Grid? FindInventoryGrid()
    {
        var mainGrid = this.FindControl<Grid>("MainGrid");
        if (mainGrid == null) return null;

        // Row 2 grid is the third child (after Row 0 and Row 1 content)
        foreach (var child in mainGrid.Children)
        {
            if (child is Grid grid && Grid.GetRow(grid) == 2)
                return grid;
        }
        return null;
    }

    #endregion

    #region Context Menu Actions

    private void OnContextToggleInfinite(object? sender, RoutedEventArgs e)
    {
        var selectedItems = StoreInventoryGrid.SelectedItems?.Cast<StoreItemViewModel>().ToList();
        if (selectedItems == null || selectedItems.Count == 0)
            return;

        foreach (var item in selectedItems)
        {
            item.Infinite = !item.Infinite;
        }

        _documentState.MarkDirty();

        // Refresh details if single selection (StoreItemExtrasPanel binds to Infinite directly,
        // so PropertyChanged on the existing VM updates the display automatically — but we still
        // refresh to keep the shared ItemDetailsPanel in sync if anything else changed).
        if (selectedItems.Count == 1)
            ShowStoreItemDetails(selectedItems[0]);
    }

    private async void OnContextCopyStoreResRef(object? sender, RoutedEventArgs e)
    {
        var selected = StoreInventoryGrid.SelectedItem as StoreItemViewModel;
        if (selected != null && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(selected.ResRef);
            UpdateStatusBar($"Copied ResRef: {selected.ResRef}");
        }
    }

    private async void OnContextCopyStoreTag(object? sender, RoutedEventArgs e)
    {
        var selected = StoreInventoryGrid.SelectedItem as StoreItemViewModel;
        if (selected != null && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(selected.Tag);
            UpdateStatusBar($"Copied Tag: {selected.Tag}");
        }
    }

    private async void OnContextCopyStoreName(object? sender, RoutedEventArgs e)
    {
        var selected = StoreInventoryGrid.SelectedItem as StoreItemViewModel;
        if (selected != null && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(selected.DisplayName);
            UpdateStatusBar($"Copied: {selected.DisplayName}");
        }
    }

    private async void OnContextCopyPaletteResRef(object? sender, RoutedEventArgs e)
    {
        var selected = ItemPaletteGrid.SelectedItem as ItemViewModel;
        if (selected != null && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(selected.ResRef);
            UpdateStatusBar($"Copied ResRef: {selected.ResRef}");
        }
    }

    private async void OnContextCopyPaletteTag(object? sender, RoutedEventArgs e)
    {
        var selected = ItemPaletteGrid.SelectedItem as ItemViewModel;
        if (selected != null && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(selected.Tag);
            UpdateStatusBar($"Copied Tag: {selected.Tag}");
        }
    }

    private async void OnContextCopyPaletteName(object? sender, RoutedEventArgs e)
    {
        var selected = ItemPaletteGrid.SelectedItem as ItemViewModel;
        if (selected != null && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(selected.Name);
            UpdateStatusBar($"Copied: {selected.Name}");
        }
    }

    #endregion
}

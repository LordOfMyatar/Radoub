using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MerchantEditor.Services;
using MerchantEditor.ViewModels;
using Radoub.Formats.Utm;
using System.Linq;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: Item details panel and context menu operations.
/// </summary>
public partial class MainWindow
{
    #region Item Details

    private void OnStoreInventorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = StoreInventoryGrid.SelectedItem as StoreItemViewModel;
        if (selected != null)
        {
            UpdateItemDetails(selected);
        }
        else
        {
            ClearItemDetails();
        }
    }

    private void OnPaletteSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Only show palette item details if nothing selected in store grid
        if (StoreInventoryGrid.SelectedItem != null)
            return;

        var selected = ItemPaletteGrid.SelectedItem as PaletteItemViewModel;
        if (selected != null)
        {
            UpdateItemDetailsFromPalette(selected);
        }
        else
        {
            ClearItemDetails();
        }
    }

    private void UpdateItemDetails(StoreItemViewModel item)
    {
        NoSelectionText.IsVisible = false;
        ItemDetailsScroll.IsVisible = true;

        DetailItemName.Text = item.DisplayName;
        DetailItemType.Text = item.BaseItemType;
        DetailResRef.Text = item.ResRef;
        DetailTag.Text = !string.IsNullOrEmpty(item.Tag) ? item.Tag : "(none)";
        DetailValue.Text = $"{item.BaseValue:N0} gp";
        DetailSellPrice.Text = $"{item.SellPrice:N0} gp";
        DetailBuyPrice.Text = $"{item.BuyPrice:N0} gp";
        DetailInfinite.Text = item.Infinite ? "Yes ∞" : "No";
        DetailStorePanel.Text = StorePanels.GetPanelName(item.PanelId);
    }

    private void UpdateItemDetailsFromPalette(PaletteItemViewModel item)
    {
        NoSelectionText.IsVisible = false;
        ItemDetailsScroll.IsVisible = true;

        DetailItemName.Text = item.DisplayName;
        DetailItemType.Text = item.BaseItemType;
        DetailResRef.Text = item.ResRef;
        DetailTag.Text = "(palette item)";
        DetailValue.Text = $"{item.BaseValue:N0} gp";

        // Calculate prices from current markup/markdown
        var markUp = int.TryParse(SellMarkupBox.Text, out var mu) ? mu : 100;
        var markDown = int.TryParse(BuyMarkdownBox.Text, out var md) ? md : 50;
        DetailSellPrice.Text = $"{(int)System.Math.Ceiling(item.BaseValue * markUp / 100.0):N0} gp";
        DetailBuyPrice.Text = $"{(int)System.Math.Floor(item.BaseValue * markDown / 100.0):N0} gp";
        DetailInfinite.Text = "—";
        DetailStorePanel.Text = StorePanels.GetPanelName(
            GetStorePanelForBaseItemType(item.BaseItemType));
    }

    private void ClearItemDetails()
    {
        NoSelectionText.IsVisible = true;
        ItemDetailsScroll.IsVisible = false;
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

        _isDirty = true;
        UpdateTitle();

        // Refresh details if single selection
        if (selectedItems.Count == 1)
            UpdateItemDetails(selectedItems[0]);
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
        var selected = ItemPaletteGrid.SelectedItem as PaletteItemViewModel;
        if (selected != null && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(selected.ResRef);
            UpdateStatusBar($"Copied ResRef: {selected.ResRef}");
        }
    }

    private async void OnContextCopyPaletteName(object? sender, RoutedEventArgs e)
    {
        var selected = ItemPaletteGrid.SelectedItem as PaletteItemViewModel;
        if (selected != null && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(selected.DisplayName);
            UpdateStatusBar($"Copied: {selected.DisplayName}");
        }
    }

    #endregion
}

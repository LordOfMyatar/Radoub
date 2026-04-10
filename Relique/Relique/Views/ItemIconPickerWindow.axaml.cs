using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.UI.Services;

namespace ItemEditor.Views;

/// <summary>
/// Icon picker dialog for selecting item model variations.
/// Shows all icon variations for a given base item type, rendered at correct inventory slot dimensions.
/// </summary>
public partial class ItemIconPickerWindow : Window
{
    private readonly IGameDataService? _gameDataService;
    private readonly ItemIconService? _itemIconService;
    private readonly int _baseItemType;
    private readonly int _invSlotWidth;
    private readonly int _invSlotHeight;
    private readonly ListBox _iconListBox;
    private readonly TextBlock _iconCountLabel;
    private readonly TextBlock _modelNumberLabel;
    private readonly TextBlock _inventorySizeLabel;
    private readonly Image _iconPreviewImage;

    private List<IconInfo> _icons = new();
    private IconInfo? _selectedIcon;

    private const int BaseSlotSize = 32;

    /// <summary>
    /// Parameterless constructor for XAML designer.
    /// </summary>
    public ItemIconPickerWindow()
    {
        InitializeComponent();
        _iconListBox = this.FindControl<ListBox>("IconListBox")!;
        _iconCountLabel = this.FindControl<TextBlock>("IconCountLabel")!;
        _modelNumberLabel = this.FindControl<TextBlock>("ModelNumberLabel")!;
        _inventorySizeLabel = this.FindControl<TextBlock>("InventorySizeLabel")!;
        _iconPreviewImage = this.FindControl<Image>("IconPreviewImage")!;
    }

    /// <summary>
    /// Creates a new icon picker for a specific base item type.
    /// </summary>
    public ItemIconPickerWindow(
        IGameDataService gameDataService,
        ItemIconService iconService,
        int baseItemType,
        byte currentModelPart,
        string baseItemName,
        int invSlotWidth,
        int invSlotHeight) : this()
    {
        _gameDataService = gameDataService;
        _itemIconService = iconService;
        _baseItemType = baseItemType;
        _invSlotWidth = Math.Max(1, invSlotWidth);
        _invSlotHeight = Math.Max(1, invSlotHeight);

        var titleText = this.FindControl<TextBlock>("TitleBarText");
        if (titleText != null)
            titleText.Text = $"Select Icon \u2014 {baseItemName}";
        Title = $"Select Icon \u2014 {baseItemName}";

        var sizeLabel = this.FindControl<TextBlock>("InventorySizeLabel");
        if (sizeLabel != null)
            sizeLabel.Text = $"{_invSlotWidth} \u00d7 {_invSlotHeight} slots";

        LoadIcons(currentModelPart);
    }

    private void LoadIcons(byte currentModelPart)
    {
        if (_gameDataService == null || _itemIconService == null)
            return;

        var baseItems = _gameDataService.Get2DA("baseitems");
        if (baseItems == null) return;

        var minRangeStr = baseItems.GetValue(_baseItemType, "MinRange");
        var maxRangeStr = baseItems.GetValue(_baseItemType, "MaxRange");

        int minRange = 0, maxRange = 0;
        if (minRangeStr != null && minRangeStr != "****") int.TryParse(minRangeStr, out minRange);
        if (maxRangeStr != null && maxRangeStr != "****") int.TryParse(maxRangeStr, out maxRange);

        int preSelectIndex = -1;

        for (int modelNum = minRange; modelNum <= maxRange; modelNum++)
        {
            var icon = _itemIconService.GetItemIcon(_baseItemType, modelNum);
            if (icon == null) continue;

            _icons.Add(new IconInfo { ModelNumber = (byte)modelNum, Bitmap = icon });

            if (modelNum == currentModelPart)
                preSelectIndex = _icons.Count - 1;
        }

        // Fallback: try default icon (model 0) if no numbered icons found
        if (_icons.Count == 0)
        {
            var defaultIcon = _itemIconService.GetItemIcon(_baseItemType);
            if (defaultIcon != null)
            {
                _icons.Add(new IconInfo { ModelNumber = 0, Bitmap = defaultIcon });
                preSelectIndex = 0;
            }
        }

        PopulateIconGrid();

        _iconCountLabel.Text = _icons.Count == 1
            ? "1 icon"
            : $"{_icons.Count} icons";

        // Pre-select current model part
        if (preSelectIndex >= 0 && preSelectIndex < _iconListBox.ItemCount)
        {
            _iconListBox.SelectedIndex = preSelectIndex;
            _iconListBox.ScrollIntoView(preSelectIndex);
        }
    }

    private void PopulateIconGrid()
    {
        _iconListBox.Items.Clear();

        int cellWidth = _invSlotWidth * BaseSlotSize;
        int cellHeight = _invSlotHeight * BaseSlotSize;

        foreach (var iconInfo in _icons)
        {
            var image = new Image
            {
                Source = iconInfo.Bitmap,
                Width = cellWidth,
                Height = cellHeight,
                Stretch = Stretch.Uniform
            };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

            var border = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(2),
                Padding = new Thickness(2),
                Margin = new Thickness(2),
                Child = image
            };

            var item = new ListBoxItem
            {
                Content = border,
                Tag = iconInfo,
                Padding = new Thickness(0)
            };
            ToolTip.SetTip(item, $"Model {iconInfo.ModelNumber}");

            _iconListBox.Items.Add(item);
        }
    }

    private void OnIconSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_iconListBox.SelectedItem is not ListBoxItem { Tag: IconInfo info })
        {
            _selectedIcon = null;
            _iconPreviewImage.Source = null;
            _modelNumberLabel.Text = "";
            return;
        }

        _selectedIcon = info;
        _iconPreviewImage.Source = info.Bitmap;
        _modelNumberLabel.Text = $"Model {info.ModelNumber}";
    }

    private void OnIconDoubleClicked(object? sender, TappedEventArgs e)
    {
        if (_selectedIcon != null)
        {
            Close(_selectedIcon.ModelNumber);
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedIcon != null)
        {
            Close(_selectedIcon.ModelNumber);
        }
        else
        {
            Close(null);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    #region Title Bar Events

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    #endregion

    private class IconInfo
    {
        public byte ModelNumber { get; set; }
        public Bitmap? Bitmap { get; set; }
    }
}

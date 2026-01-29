using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Radoub.UI.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Quartermaster.Views.Panels;

public partial class InventoryPanel : UserControl, INotifyPropertyChanged
{
    private ObservableCollection<EquipmentSlotViewModel> _equipmentSlots = new();
    private ObservableCollection<ItemViewModel> _backpackItems = new();
    private ObservableCollection<ItemViewModel> _paletteItems = new();
    private ObservableCollection<ItemViewModel> _filteredPaletteItems = new();

    private bool _hasBackpackSelection;
    private bool _hasPaletteSelection;

    // Controls - found in InitializeComponent
    private EquipmentSlotsPanel? _equipmentPanel;
    private ItemListView? _backpackList;
    private ItemFilterPanel? _paletteFilter;
    private ItemListView? _paletteList;
    private CheckBox? _bulkDropableCheck;
    private CheckBox? _bulkPickpocketCheck;

    // Item Details controls
    private TextBlock? _noSelectionText;
    private ScrollViewer? _itemDetailsScroll;
    private Image? _itemIcon;
    private TextBlock? _itemNameText;
    private TextBlock? _itemTypeText;
    private TextBlock? _itemResRefText;
    private TextBlock? _itemTagText;
    private TextBlock? _itemValueText;
    private TextBlock? _itemSourceText;
    private TextBlock? _itemPropertiesText;

    public new event PropertyChangedEventHandler? PropertyChanged;

    // Events for MainWindow to subscribe to
    public event EventHandler? InventoryChanged;
    public event EventHandler<EquipmentSlotViewModel>? EquipmentSlotClicked;
    public event EventHandler<EquipmentSlotViewModel>? EquipmentSlotDoubleClicked;
    public event EventHandler<EquipmentSlotDragEventArgs>? EquipmentSlotDragStarting;
    public event EventHandler<EquipmentSlotDropEventArgs>? EquipmentSlotItemDropped;
    public event EventHandler<ItemDropEventArgs>? BackpackItemDropped;
    public event EventHandler<ItemViewModel[]>? AddToBackpackRequested;
    public event EventHandler<ItemViewModel[]>? EquipItemsRequested;

    public bool HasBackpackSelection
    {
        get => _hasBackpackSelection;
        private set { _hasBackpackSelection = value; OnPropertyChanged(); }
    }

    public bool HasPaletteSelection
    {
        get => _hasPaletteSelection;
        private set { _hasPaletteSelection = value; OnPropertyChanged(); }
    }

    public ObservableCollection<EquipmentSlotViewModel> EquipmentSlots => _equipmentSlots;
    public ObservableCollection<ItemViewModel> BackpackItems => _backpackItems;
    public ObservableCollection<ItemViewModel> PaletteItems => _paletteItems;

    /// <summary>
    /// Efficiently set all palette items at once, avoiding per-item UI updates.
    /// </summary>
    public void SetPaletteItems(System.Collections.Generic.List<ItemViewModel> items)
    {
        // Disconnect filter temporarily to avoid per-item filter updates
        if (_paletteFilter != null)
        {
            _paletteFilter.Items = null;
        }

        // Clear and add all items
        _paletteItems.Clear();
        foreach (var item in items)
        {
            _paletteItems.Add(item);
        }

        // Reconnect filter and trigger single refresh
        if (_paletteFilter != null)
        {
            _paletteFilter.Items = _paletteItems;
            _paletteFilter.ApplyFilter();
        }
    }

    public InventoryPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Find controls
        _equipmentPanel = this.FindControl<EquipmentSlotsPanel>("EquipmentPanel");
        _backpackList = this.FindControl<ItemListView>("BackpackList");
        _paletteFilter = this.FindControl<ItemFilterPanel>("PaletteFilter");
        _paletteList = this.FindControl<ItemListView>("PaletteList");
        _bulkDropableCheck = this.FindControl<CheckBox>("BulkDropableCheck");
        _bulkPickpocketCheck = this.FindControl<CheckBox>("BulkPickpocketCheck");

        // Find item details controls
        _noSelectionText = this.FindControl<TextBlock>("NoSelectionText");
        _itemDetailsScroll = this.FindControl<ScrollViewer>("ItemDetailsScroll");
        _itemIcon = this.FindControl<Image>("ItemIcon");
        _itemNameText = this.FindControl<TextBlock>("ItemNameText");
        _itemTypeText = this.FindControl<TextBlock>("ItemTypeText");
        _itemResRefText = this.FindControl<TextBlock>("ItemResRefText");
        _itemTagText = this.FindControl<TextBlock>("ItemTagText");
        _itemValueText = this.FindControl<TextBlock>("ItemValueText");
        _itemSourceText = this.FindControl<TextBlock>("ItemSourceText");
        _itemPropertiesText = this.FindControl<TextBlock>("ItemPropertiesText");
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Set up equipment panel
        if (_equipmentPanel != null)
        {
            _equipmentPanel.Slots = _equipmentSlots;
            _equipmentPanel.SlotClicked += OnEquipmentSlotClicked;
            _equipmentPanel.SlotDoubleClicked += OnEquipmentSlotDoubleClicked;
            _equipmentPanel.DragStarting += OnEquipmentDragStarting;
            _equipmentPanel.ItemDropped += OnEquipmentSlotItemDropped;
        }

        // Set up backpack list
        if (_backpackList != null)
        {
            _backpackList.Items = _backpackItems;
            _backpackList.SelectionChanged += OnBackpackSelectionChanged;
            _backpackList.DragStarting += OnBackpackDragStarting;
            _backpackList.ItemDropped += OnBackpackItemDropped;
        }

        // Set up palette filter and list
        if (_paletteFilter != null)
        {
            _paletteFilter.Items = _paletteItems;
            _paletteFilter.FilteredItems = _filteredPaletteItems;
        }
        if (_paletteList != null)
        {
            _paletteList.Items = _filteredPaletteItems;
            _paletteList.SelectionChanged += OnPaletteSelectionChanged;
            _paletteList.DragStarting += OnPaletteDragStarting;
        }

        // Wire up bulk property checkboxes
        if (_bulkDropableCheck != null)
            _bulkDropableCheck.IsCheckedChanged += OnBulkDropableChanged;
        if (_bulkPickpocketCheck != null)
            _bulkPickpocketCheck.IsCheckedChanged += OnBulkPickpocketChanged;
    }

    public void SetGameDataService(IGameDataService gameDataService)
    {
        if (_paletteFilter != null)
            _paletteFilter.GameDataService = gameDataService;
    }

    public void InitializeSlots(ObservableCollection<EquipmentSlotViewModel> slots)
    {
        _equipmentSlots = slots;
        if (_equipmentPanel != null)
            _equipmentPanel.Slots = _equipmentSlots;
    }

    public void ClearAll()
    {
        _equipmentPanel?.ClearAllSlots();
        _backpackItems.Clear();
        // Don't clear palette - it's shared game data, not per-creature
        HasBackpackSelection = false;
        HasPaletteSelection = false;
    }

    public void UpdateInventoryCounts(out int equippedCount, out int backpackCount)
    {
        equippedCount = _equipmentSlots.Count(s => s.HasItem);
        backpackCount = _backpackItems.Count;
    }

    #region Equipment Panel Events

    private void OnEquipmentSlotClicked(object? sender, EquipmentSlotViewModel slot)
    {
        EquipmentSlotClicked?.Invoke(this, slot);
    }

    private void OnEquipmentSlotDoubleClicked(object? sender, EquipmentSlotViewModel slot)
    {
        EquipmentSlotDoubleClicked?.Invoke(this, slot);
    }

    private void OnEquipmentDragStarting(object? sender, EquipmentSlotDragEventArgs e)
    {
        if (e.Slot.HasItem && e.Slot.EquippedItem != null)
        {
            e.Data = e.Slot.EquippedItem;
            e.DataFormat = "EquippedItem";
            EquipmentSlotDragStarting?.Invoke(this, e);
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"Equipment drag starting: {e.Slot.EquippedItem.Name} from {e.Slot.Name}");
        }
    }

    private void OnEquipmentSlotItemDropped(object? sender, EquipmentSlotDropEventArgs e)
    {
        EquipmentSlotItemDropped?.Invoke(this, e);
    }

    #endregion

    #region Backpack Events

    private void OnBackpackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        HasBackpackSelection = _backpackList?.SelectedItems.Count > 0;
        UpdateBulkCheckboxStates();
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Backpack selection changed: {_backpackList?.SelectedItems.Count ?? 0} items");

        // Update item details - use first selected backpack item if any
        var selectedItem = _backpackList?.SelectedItems.FirstOrDefault();
        UpdateItemDetails(selectedItem);
    }

    private void UpdateBulkCheckboxStates()
    {
        if (_backpackList == null) return;

        var selectedItems = _backpackList.SelectedItems;
        if (selectedItems.Count == 0)
        {
            // No selection - reset checkboxes
            if (_bulkDropableCheck != null) _bulkDropableCheck.IsChecked = false;
            if (_bulkPickpocketCheck != null) _bulkPickpocketCheck.IsChecked = false;
            return;
        }

        // Set checkbox to reflect if ALL selected items have the property
        if (_bulkDropableCheck != null)
            _bulkDropableCheck.IsChecked = selectedItems.All(i => i.IsDropable);
        if (_bulkPickpocketCheck != null)
            _bulkPickpocketCheck.IsChecked = selectedItems.All(i => i.IsPickpocketable);
    }

    private void OnBackpackDragStarting(object? sender, ItemDragEventArgs e)
    {
        e.Data = e.Items;
        e.DataFormat = "BackpackItem";
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Backpack drag starting: {e.Items.Count} items");
    }

    private void OnBackpackItemDropped(object? sender, ItemDropEventArgs e)
    {
        BackpackItemDropped?.Invoke(this, e);
        UnifiedLogger.LogUI(LogLevel.DEBUG, "Item dropped on backpack");
    }

    private void OnDeleteSelectedClick(object? sender, RoutedEventArgs e)
    {
        if (_backpackList == null) return;

        var toDelete = _backpackList.CheckedItems.Count > 0
            ? _backpackList.CheckedItems
            : _backpackList.SelectedItems;

        if (toDelete.Count == 0) return;

        foreach (var item in toDelete.ToArray())
        {
            _backpackItems.Remove(item);
        }

        InventoryChanged?.Invoke(this, EventArgs.Empty);
        UnifiedLogger.LogInventory(LogLevel.INFO, $"Deleted {toDelete.Count} items from backpack");
    }

    private void OnBulkDropableChanged(object? sender, RoutedEventArgs e)
    {
        if (_backpackList == null || _bulkDropableCheck == null) return;

        var isChecked = _bulkDropableCheck.IsChecked ?? false;
        var selectedItems = _backpackList.SelectedItems;

        if (selectedItems.Count == 0) return;

        foreach (var item in selectedItems)
        {
            item.IsDropable = isChecked;
        }

        InventoryChanged?.Invoke(this, EventArgs.Empty);
        UnifiedLogger.LogInventory(LogLevel.INFO, $"Set Dropable={isChecked} for {selectedItems.Count} items");
    }

    private void OnBulkPickpocketChanged(object? sender, RoutedEventArgs e)
    {
        if (_backpackList == null || _bulkPickpocketCheck == null) return;

        var isChecked = _bulkPickpocketCheck.IsChecked ?? false;
        var selectedItems = _backpackList.SelectedItems;

        if (selectedItems.Count == 0) return;

        foreach (var item in selectedItems)
        {
            item.IsPickpocketable = isChecked;
        }

        InventoryChanged?.Invoke(this, EventArgs.Empty);
        UnifiedLogger.LogInventory(LogLevel.INFO, $"Set Pickpocketable={isChecked} for {selectedItems.Count} items");
    }

    #endregion

    #region Palette Events

    private void OnPaletteSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        HasPaletteSelection = _paletteList?.SelectedItems.Count > 0;
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Palette selection changed: {_paletteList?.SelectedItems.Count ?? 0} items");

        // Update item details - prefer backpack selection, fall back to palette
        var backpackSelected = _backpackList?.SelectedItems.FirstOrDefault();
        if (backpackSelected == null)
        {
            var paletteSelected = _paletteList?.SelectedItems.FirstOrDefault();
            UpdateItemDetails(paletteSelected);
        }
    }

    private void OnPaletteDragStarting(object? sender, ItemDragEventArgs e)
    {
        e.Data = e.Items;
        e.DataFormat = "PaletteItem";
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Palette drag starting: {e.Items.Count} items");
    }

    private void OnAddToBackpackClick(object? sender, RoutedEventArgs e)
    {
        if (!HasPaletteSelection || _paletteList == null) return;

        var selectedItems = _paletteList.SelectedItems.ToArray();
        if (selectedItems.Length > 0)
        {
            AddToBackpackRequested?.Invoke(this, selectedItems);
        }
    }

    private void OnEquipSelectedClick(object? sender, RoutedEventArgs e)
    {
        if (!HasPaletteSelection || _paletteList == null) return;

        var selectedItems = _paletteList.SelectedItems.ToArray();
        if (selectedItems.Length > 0)
        {
            EquipItemsRequested?.Invoke(this, selectedItems);
        }
    }

    /// <summary>
    /// Adds an item to the backpack. Called by MainWindow after creating the item.
    /// </summary>
    public void AddToBackpack(ItemViewModel item)
    {
        _backpackItems.Add(item);
        InventoryChanged?.Invoke(this, EventArgs.Empty);
        UnifiedLogger.LogInventory(LogLevel.INFO, $"Added to backpack: {item.Name}");
    }

    /// <summary>
    /// Removes an item from the backpack by ResRef.
    /// </summary>
    public bool RemoveFromBackpack(ItemViewModel item)
    {
        var removed = _backpackItems.Remove(item);
        if (removed)
        {
            InventoryChanged?.Invoke(this, EventArgs.Empty);
            UnifiedLogger.LogInventory(LogLevel.INFO, $"Removed from backpack: {item.Name}");
        }
        return removed;
    }

    #endregion

    #region Item Details

    /// <summary>
    /// Updates the item details panel with the selected item's information.
    /// </summary>
    private void UpdateItemDetails(ItemViewModel? item)
    {
        if (item == null)
        {
            // No selection - show placeholder
            if (_noSelectionText != null) _noSelectionText.IsVisible = true;
            if (_itemDetailsScroll != null) _itemDetailsScroll.IsVisible = false;
            return;
        }

        // Show details panel
        if (_noSelectionText != null) _noSelectionText.IsVisible = false;
        if (_itemDetailsScroll != null) _itemDetailsScroll.IsVisible = true;

        // Icon - prefer game icon, fall back to placeholder
        if (_itemIcon != null)
        {
            if (item.IconBitmap != null)
            {
                _itemIcon.Source = item.IconBitmap;
            }
            else if (!string.IsNullOrEmpty(item.IconPath))
            {
                try
                {
                    var uri = new System.Uri($"avares://Radoub.UI/{item.IconPath}");
                    var asset = Avalonia.Platform.AssetLoader.Open(uri);
                    _itemIcon.Source = new Avalonia.Media.Imaging.Bitmap(asset);
                }
                catch (Exception ex) when (ex is UriFormatException or FileNotFoundException or InvalidOperationException)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not load item icon from '{item.IconPath}': {ex.Message}");
                    _itemIcon.Source = null;
                }
            }
            else
            {
                _itemIcon.Source = null;
            }
        }

        // Basic info
        if (_itemNameText != null) _itemNameText.Text = item.Name;
        if (_itemTypeText != null) _itemTypeText.Text = item.BaseItemName;
        if (_itemResRefText != null) _itemResRefText.Text = item.ResRef;
        if (_itemTagText != null) _itemTagText.Text = item.Tag;
        if (_itemValueText != null) _itemValueText.Text = $"{item.Value:N0} gp";
        if (_itemSourceText != null) _itemSourceText.Text = item.Source.ToString();

        // Properties
        if (_itemPropertiesText != null)
        {
            if (!string.IsNullOrEmpty(item.PropertiesDisplay))
            {
                _itemPropertiesText.Text = item.PropertiesDisplay;
            }
            else if (item.PropertyCount > 0)
            {
                _itemPropertiesText.Text = $"{item.PropertyCount} properties";
            }
            else
            {
                _itemPropertiesText.Text = "None";
            }
        }
    }

    /// <summary>
    /// Clears the item details panel.
    /// </summary>
    public void ClearItemDetails()
    {
        UpdateItemDetails(null);
    }

    #endregion

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

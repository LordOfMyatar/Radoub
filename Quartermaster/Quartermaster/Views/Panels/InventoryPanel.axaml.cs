using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.UI.Controls;
using Radoub.UI.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

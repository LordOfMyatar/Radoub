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

    public new event PropertyChangedEventHandler? PropertyChanged;

    // Events for MainWindow to subscribe to
    public event EventHandler? InventoryChanged;
    public event EventHandler<EquipmentSlotViewModel>? EquipmentSlotClicked;
    public event EventHandler<EquipmentSlotViewModel>? EquipmentSlotDoubleClicked;
    public event EventHandler<EquipmentSlotDropEventArgs>? EquipmentSlotItemDropped;

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
            _equipmentPanel.ItemDropped += OnEquipmentSlotItemDropped;
        }

        // Set up backpack list
        if (_backpackList != null)
        {
            _backpackList.Items = _backpackItems;
            _backpackList.SelectionChanged += OnBackpackSelectionChanged;
            _backpackList.DragStarting += OnBackpackDragStarting;
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
        _paletteItems.Clear();
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

    private void OnEquipmentSlotItemDropped(object? sender, EquipmentSlotDropEventArgs e)
    {
        EquipmentSlotItemDropped?.Invoke(this, e);
    }

    #endregion

    #region Backpack Events

    private void OnBackpackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        HasBackpackSelection = _backpackList?.SelectedItems.Count > 0;
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Backpack selection changed: {_backpackList?.SelectedItems.Count ?? 0} items");
    }

    private void OnBackpackDragStarting(object? sender, ItemDragEventArgs e)
    {
        e.Data = e.Items;
        e.DataFormat = "BackpackItem";
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Backpack drag starting: {e.Items.Count} items");
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

        foreach (var item in _paletteList.SelectedItems)
        {
            // TODO: Clone the item and add to backpack
            UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Would add to backpack: {item.Name}");
        }

        InventoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnEquipSelectedClick(object? sender, RoutedEventArgs e)
    {
        if (!HasPaletteSelection) return;

        // TODO: Determine appropriate slot and equip item
        UnifiedLogger.LogInventory(LogLevel.DEBUG, "Equip selected clicked");
    }

    #endregion

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

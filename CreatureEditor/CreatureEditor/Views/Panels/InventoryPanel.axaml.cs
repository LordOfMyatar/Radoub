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

namespace CreatureEditor.Views.Panels;

public partial class InventoryPanel : UserControl, INotifyPropertyChanged
{
    private ObservableCollection<EquipmentSlotViewModel> _equipmentSlots = new();
    private ObservableCollection<ItemViewModel> _backpackItems = new();
    private ObservableCollection<ItemViewModel> _paletteItems = new();
    private ObservableCollection<ItemViewModel> _filteredPaletteItems = new();

    private bool _hasBackpackSelection;
    private bool _hasPaletteSelection;

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
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Set up equipment panel
        EquipmentPanel.Slots = _equipmentSlots;
        EquipmentPanel.SlotClicked += OnEquipmentSlotClicked;
        EquipmentPanel.SlotDoubleClicked += OnEquipmentSlotDoubleClicked;
        EquipmentPanel.ItemDropped += OnEquipmentSlotItemDropped;

        // Set up backpack list
        BackpackList.Items = _backpackItems;
        BackpackList.SelectionChanged += OnBackpackSelectionChanged;
        BackpackList.DragStarting += OnBackpackDragStarting;

        // Set up palette filter and list
        PaletteFilter.Items = _paletteItems;
        PaletteFilter.FilteredItems = _filteredPaletteItems;
        PaletteList.Items = _filteredPaletteItems;
        PaletteList.SelectionChanged += OnPaletteSelectionChanged;
        PaletteList.DragStarting += OnPaletteDragStarting;
    }

    public void SetGameDataService(IGameDataService gameDataService)
    {
        PaletteFilter.GameDataService = gameDataService;
    }

    public void InitializeSlots(ObservableCollection<EquipmentSlotViewModel> slots)
    {
        _equipmentSlots = slots;
        EquipmentPanel.Slots = _equipmentSlots;
    }

    public void ClearAll()
    {
        EquipmentPanel.ClearAllSlots();
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
        HasBackpackSelection = BackpackList.SelectedItems.Count > 0;
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Backpack selection changed: {BackpackList.SelectedItems.Count} items");
    }

    private void OnBackpackDragStarting(object? sender, ItemDragEventArgs e)
    {
        e.Data = e.Items;
        e.DataFormat = "BackpackItem";
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Backpack drag starting: {e.Items.Count} items");
    }

    private void OnDeleteSelectedClick(object? sender, RoutedEventArgs e)
    {
        var toDelete = BackpackList.CheckedItems.Count > 0
            ? BackpackList.CheckedItems
            : BackpackList.SelectedItems;

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
        HasPaletteSelection = PaletteList.SelectedItems.Count > 0;
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Palette selection changed: {PaletteList.SelectedItems.Count} items");
    }

    private void OnPaletteDragStarting(object? sender, ItemDragEventArgs e)
    {
        e.Data = e.Items;
        e.DataFormat = "PaletteItem";
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Palette drag starting: {e.Items.Count} items");
    }

    private void OnAddToBackpackClick(object? sender, RoutedEventArgs e)
    {
        if (!HasPaletteSelection) return;

        foreach (var item in PaletteList.SelectedItems)
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

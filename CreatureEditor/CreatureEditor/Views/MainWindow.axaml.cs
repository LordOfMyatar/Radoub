using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CreatureEditor.Services;
using CreatureEditor.Views.Helpers;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;
using Radoub.Formats.Uti;
using Radoub.UI.Controls;
using Radoub.UI.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CreatureEditor.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private UtcFile? _currentCreature;
    private string? _currentFilePath;
    private bool _isDirty;
    private bool _isBicFile;

    // Game data service for BIF/TLK lookups
    private readonly IGameDataService _gameDataService;
    private readonly ItemViewModelFactory _itemViewModelFactory;

    // Equipment slots collection
    private ObservableCollection<EquipmentSlotViewModel> _equipmentSlots = new();

    // Backpack items collection
    private ObservableCollection<ItemViewModel> _backpackItems = new();

    // Palette items collection (available items to add)
    private ObservableCollection<ItemViewModel> _paletteItems = new();

    // Selection state tracking
    private bool _hasSelection;
    private bool _hasBackpackSelection;
    private bool _hasPaletteSelection;

    // Bindable properties for UI state
    public bool HasFile => _currentCreature != null;
    public bool HasSelection
    {
        get => _hasSelection;
        private set { _hasSelection = value; OnPropertyChanged(); }
    }
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
    public bool CanAddItem => HasFile && HasPaletteSelection;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        // Note: Don't set DataContext = this; it causes stack overflow with Radoub.UI controls.
        // Use ElementName bindings in XAML instead.

        // Initialize game data service for BIF/TLK lookups
        _gameDataService = new GameDataService();
        _itemViewModelFactory = new ItemViewModelFactory(_gameDataService);

        if (_gameDataService.IsConfigured)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "GameDataService initialized - BIF lookup enabled");
        }
        else
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "GameDataService not configured - BIF lookup disabled");
        }

        InitializeEquipmentSlots();
        RestoreWindowPosition();

        Closing += OnWindowClosing;
        Opened += OnWindowOpened;

        UnifiedLogger.LogApplication(LogLevel.INFO, "CreatureEditor MainWindow initialized");
    }

    private void InitializeEquipmentSlots()
    {
        // Create all equipment slots using the factory
        var allSlots = EquipmentSlotFactory.CreateAllSlots();
        foreach (var slot in allSlots)
        {
            _equipmentSlots.Add(slot);
        }

        // Bind to EquipmentPanel
        EquipmentPanel.Slots = _equipmentSlots;

        // Bind backpack and palette lists
        BackpackList.Items = _backpackItems;
        PaletteList.Items = _paletteItems;

        // Wire up filter to palette list
        PaletteFilter.Items = _paletteItems;
        PaletteFilter.FilteredItems = new ObservableCollection<ItemViewModel>();

        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Initialized {_equipmentSlots.Count} equipment slots");
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;

        UpdateRecentFilesMenu();
        await HandleStartupFileAsync();
    }

    private async Task HandleStartupFileAsync()
    {
        var options = CommandLineService.Options;

        if (string.IsNullOrEmpty(options.FilePath))
            return;

        if (!File.Exists(options.FilePath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Command line file not found: {UnifiedLogger.SanitizePath(options.FilePath)}");
            UpdateStatus($"File not found: {Path.GetFileName(options.FilePath)}");
            return;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading file from command line: {UnifiedLogger.SanitizePath(options.FilePath)}");
        await LoadFile(options.FilePath);
    }

    private void RestoreWindowPosition()
    {
        var settings = SettingsService.Instance;
        Position = new Avalonia.PixelPoint((int)settings.WindowLeft, (int)settings.WindowTop);
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;

        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        // Restore panel widths
        if (MainGrid.ColumnDefinitions.Count > 0)
        {
            MainGrid.ColumnDefinitions[0].Width = new Avalonia.Controls.GridLength(settings.LeftPanelWidth);
        }
    }

    private void SaveWindowPosition()
    {
        var settings = SettingsService.Instance;

        if (WindowState == WindowState.Normal)
        {
            settings.WindowLeft = Position.X;
            settings.WindowTop = Position.Y;
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
        }
        settings.WindowMaximized = WindowState == WindowState.Maximized;

        // Save panel widths
        if (MainGrid.ColumnDefinitions.Count > 0)
        {
            settings.LeftPanelWidth = MainGrid.ColumnDefinitions[0].Width.Value;
        }
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isDirty)
        {
            e.Cancel = true;
            var result = await DialogHelper.ShowUnsavedChangesDialog(this);
            if (result == "Save")
            {
                await SaveFile();
                Close();
            }
            else if (result == "Discard")
            {
                _isDirty = false;
                Close();
            }
        }
        else
        {
            SaveWindowPosition();
            _gameDataService.Dispose();
        }
    }

    #region Inventory UI

    private void PopulateInventoryUI()
    {
        if (_currentCreature == null) return;

        // Clear existing data
        ClearInventoryUI();

        // Populate equipment slots from EquipItemList
        foreach (var equippedItem in _currentCreature.EquipItemList)
        {
            var slot = EquipmentSlotFactory.GetSlotByFlag(_equipmentSlots, equippedItem.Slot);
            if (slot != null && !string.IsNullOrEmpty(equippedItem.EquipRes))
            {
                // Create placeholder item from ResRef (full resolution requires game data)
                var itemVm = CreatePlaceholderItem(equippedItem.EquipRes);
                slot.EquippedItem = itemVm;
                UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Equipped {equippedItem.EquipRes} to {slot.Name}");
            }
        }

        // Populate backpack from ItemList
        foreach (var invItem in _currentCreature.ItemList)
        {
            if (!string.IsNullOrEmpty(invItem.InventoryRes))
            {
                var itemVm = CreatePlaceholderItem(invItem.InventoryRes, invItem.Dropable, invItem.Pickpocketable);
                _backpackItems.Add(itemVm);
                UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Added to backpack: {invItem.InventoryRes}");
            }
        }

        UnifiedLogger.LogInventory(LogLevel.INFO, $"Populated inventory: {_currentCreature.EquipItemList.Count} equipped, {_backpackItems.Count} in backpack");
    }

    /// <summary>
    /// Creates an ItemViewModel from a ResRef, attempting to load the actual UTI file.
    /// Resolution order: Module directory → Override → HAK → BIF archives.
    /// Falls back to placeholder data if UTI file not found anywhere.
    /// </summary>
    private ItemViewModel CreatePlaceholderItem(string resRef, bool dropable = true, bool pickpocketable = false)
    {
        UtiFile? item = null;
        var source = GameResourceSource.Bif;

        // 1. Try module directory first (highest priority for module-specific items)
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            var moduleDir = Path.GetDirectoryName(_currentFilePath);
            if (moduleDir != null)
            {
                var utiPath = Path.Combine(moduleDir, resRef + ".uti");
                if (File.Exists(utiPath))
                {
                    try
                    {
                        item = UtiReader.Read(utiPath);
                        source = GameResourceSource.Module;
                        UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Loaded UTI from module: {resRef}");
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogInventory(LogLevel.WARN, $"Failed to load UTI {resRef} from module: {ex.Message}");
                    }
                }
            }
        }

        // 2. Try GameDataService (Override → HAK → BIF) if not found in module
        if (item == null && _gameDataService.IsConfigured)
        {
            try
            {
                var utiData = _gameDataService.FindResource(resRef, ResourceTypes.Uti);
                if (utiData != null)
                {
                    item = UtiReader.Read(utiData);
                    // GameDataService searches Override first, then HAK, then BIF
                    // For now, mark as Bif source (could enhance to track actual source)
                    source = GameResourceSource.Bif;
                    UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Loaded UTI from game data: {resRef}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogInventory(LogLevel.WARN, $"Failed to load UTI {resRef} from game data: {ex.Message}");
            }
        }

        // 3. Fall back to placeholder if UTI not found anywhere
        if (item == null)
        {
            item = new UtiFile
            {
                TemplateResRef = resRef,
                Tag = resRef
            };
            item.LocalizedName.SetString(0, resRef);
            UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Created placeholder for UTI: {resRef}");

            // Return basic placeholder without factory resolution
            return new ItemViewModel(
                item,
                resolvedName: resRef,
                baseItemName: "(unknown)",
                propertiesDisplay: "",
                source: source
            );
        }

        // Use factory for proper name resolution via 2DA/TLK
        return _itemViewModelFactory.Create(item, source);
    }

    private void ClearInventoryUI()
    {
        // Clear equipment slots
        EquipmentPanel.ClearAllSlots();

        // Clear backpack
        _backpackItems.Clear();

        // Update selection state
        HasSelection = false;
        HasBackpackSelection = false;
    }

    #endregion

    #region Equipment Panel Events

    private void OnEquipmentSlotClicked(object? sender, EquipmentSlotViewModel slot)
    {
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Equipment slot clicked: {slot.Name}");
        HasSelection = slot.HasItem;
    }

    private void OnEquipmentSlotDoubleClicked(object? sender, EquipmentSlotViewModel slot)
    {
        if (slot.HasItem)
        {
            // TODO: Open item properties dialog
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"Equipment slot double-clicked: {slot.Name} - {slot.EquippedItem?.Name}");
        }
    }

    private void OnEquipmentSlotItemDropped(object? sender, EquipmentSlotDropEventArgs e)
    {
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Item dropped on slot: {e.TargetSlot.Name}");
        // TODO: Handle item drop (equip item to slot)
        MarkDirty();
    }

    #endregion

    #region Backpack List Events

    private void OnBackpackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        HasBackpackSelection = BackpackList.SelectedItems.Count > 0;
        HasSelection = HasBackpackSelection;
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Backpack selection changed: {BackpackList.SelectedItems.Count} items");
    }

    private void OnBackpackCheckedChanged(object? sender, EventArgs e)
    {
        var checkedCount = BackpackList.CheckedItems.Count;
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Backpack checked items: {checkedCount}");
    }

    private void OnBackpackDragStarting(object? sender, ItemDragEventArgs e)
    {
        // Set drag data for backpack items
        e.Data = e.Items;
        e.DataFormat = "BackpackItem";
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Backpack drag starting: {e.Items.Count} items");
    }

    #endregion

    #region Palette List Events

    private void OnPaletteSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        HasPaletteSelection = PaletteList.SelectedItems.Count > 0;
        OnPropertyChanged(nameof(CanAddItem));
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Palette selection changed: {PaletteList.SelectedItems.Count} items");
    }

    private void OnPaletteDragStarting(object? sender, ItemDragEventArgs e)
    {
        // Set drag data for palette items
        e.Data = e.Items;
        e.DataFormat = "PaletteItem";
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Palette drag starting: {e.Items.Count} items");
    }

    #endregion

    #region Edit Operations

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (EquipmentPanel.SelectedSlot?.HasItem == true)
        {
            // Unequip item from slot
            var slot = EquipmentPanel.SelectedSlot;
            slot.EquippedItem = null;
            MarkDirty();
            UpdateInventoryCounts();
            UnifiedLogger.LogInventory(LogLevel.INFO, $"Unequipped item from {slot.Name}");
        }
        else if (HasBackpackSelection)
        {
            OnDeleteSelectedClick(sender, e);
        }
    }

    private void OnDeleteSelectedClick(object? sender, RoutedEventArgs e)
    {
        // Delete checked items first, then selected items
        var toDelete = BackpackList.CheckedItems.Count > 0
            ? BackpackList.CheckedItems
            : BackpackList.SelectedItems;

        if (toDelete.Count == 0) return;

        foreach (var item in toDelete.ToArray())
        {
            _backpackItems.Remove(item);
        }

        MarkDirty();
        UpdateInventoryCounts();
        UnifiedLogger.LogInventory(LogLevel.INFO, $"Deleted {toDelete.Count} items from backpack");
    }

    private void OnAddItemClick(object? sender, RoutedEventArgs e)
    {
        OnAddToBackpackClick(sender, e);
    }

    private void OnAddToBackpackClick(object? sender, RoutedEventArgs e)
    {
        if (!HasFile || !HasPaletteSelection) return;

        foreach (var item in PaletteList.SelectedItems)
        {
            // TODO: Clone the item and add to backpack
            // _backpackItems.Add(clonedItem);
            UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Would add to backpack: {item.Name}");
        }

        MarkDirty();
        UpdateInventoryCounts();
    }

    private void OnEquipSelectedClick(object? sender, RoutedEventArgs e)
    {
        if (!HasFile || !HasPaletteSelection) return;

        // TODO: Determine appropriate slot and equip item
        UnifiedLogger.LogInventory(LogLevel.DEBUG, "Equip selected clicked");
    }

    #endregion

    #region UI Updates

    private void UpdateTitle()
    {
        var displayPath = _currentFilePath != null ? UnifiedLogger.SanitizePath(_currentFilePath) : "Untitled";
        var dirty = _isDirty ? "*" : "";
        var fileType = _isBicFile ? " (Player)" : "";
        Title = $"Creature Editor - {displayPath}{fileType}{dirty}";
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }

    private void UpdateInventoryCounts()
    {
        if (_currentCreature == null)
        {
            InventoryCountText.Text = "";
            FilePathText.Text = "";
            return;
        }

        var equippedCount = _equipmentSlots.Count(s => s.HasItem);
        var backpackCount = _backpackItems.Count;
        InventoryCountText.Text = $"{equippedCount} equipped, {backpackCount} in backpack";
        FilePathText.Text = _currentFilePath != null ? UnifiedLogger.SanitizePath(_currentFilePath) : "";
    }

    private void MarkDirty()
    {
        if (!_isDirty)
        {
            _isDirty = true;
            UpdateTitle();
        }
    }

    #endregion

    #region Keyboard Shortcuts

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.O:
                    _ = OpenFile();
                    e.Handled = true;
                    break;
                case Key.S:
                    if (HasFile)
                    {
                        _ = SaveFile();
                        e.Handled = true;
                    }
                    break;
            }
        }
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            switch (e.Key)
            {
                case Key.S:
                    if (HasFile)
                    {
                        _ = SaveFileAs();
                        e.Handled = true;
                    }
                    break;
            }
        }
        else if (e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.Delete:
                    if (HasSelection)
                    {
                        OnDeleteClick(sender, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
                case Key.F1:
                    OnAboutClick(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }
    }

    #endregion

    #region Menu Handlers - Dialogs

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        // TODO: Open settings window
        UpdateStatus("Settings not yet implemented");
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        DialogHelper.ShowAboutDialog(this);
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        await DialogHelper.ShowErrorDialog(this, title, message);
    }

    #endregion

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

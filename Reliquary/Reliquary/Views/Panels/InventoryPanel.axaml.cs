using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Radoub.UI.Controls;
using Radoub.UI.ViewModels;

namespace PlaceableEditor.Views.Panels;

/// <summary>
/// Inventory panel (design §5.4): placeable contents (flat backpack list) LEFT, UTI palette
/// MIDDLE, read-only resolved item details RIGHT. Adventurer-backpack model — no body slots.
/// Stays thin like <see cref="BehaviorPanel"/>: add/remove are raised as events for the host
/// (MainWindow) to service via the undo manager and UTI resolution. UTP placeable contents store
/// only the item ResRef + grid position, so the details pane shows resolved UTI values read-only
/// (the format carries no per-instance stack/charges/plot to edit).
/// </summary>
public partial class InventoryPanel : UserControl, INotifyPropertyChanged
{
    private readonly ObservableCollection<ItemViewModel> _backpackItems = new();
    private readonly ObservableCollection<ItemViewModel> _paletteItems = new();
    private readonly ObservableCollection<ItemViewModel> _filteredPaletteItems = new();

    private ItemListView? _backpackList;
    private ItemListView? _paletteList;
    private ItemFilterPanel? _paletteFilter;
    private ItemDetailsPanel? _itemDetailsView;
    private TextBlock? _contentsCount;

    private bool _hasBackpackSelection;
    private bool _hasPaletteSelection;

    public new event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when "Add Selected" is clicked (carries the palette item to add).</summary>
    public event EventHandler<ItemViewModel>? AddItemRequested;

    /// <summary>Raised when "Remove" is clicked (carries the backpack item to remove).</summary>
    public event EventHandler<ItemViewModel>? RemoveItemRequested;

    /// <summary>Raised when the row context "Edit" is chosen — host opens the UTI in Relique.</summary>
    public event EventHandler<ItemViewModel>? EditItemRequested;

    /// <summary>Resolve a cache-loaded palette item into a fully-loaded item for the details pane.</summary>
    public Func<ItemViewModel, ItemViewModel?>? ItemResolver { get; set; }

    /// <summary>Give the palette filter game data so its item-type dropdown can populate from 2DA.</summary>
    public void SetGameDataService(Radoub.Formats.Services.IGameDataService gameDataService)
    {
        if (_paletteFilter != null)
            _paletteFilter.GameDataService = gameDataService;
    }

    public InventoryPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _backpackList = this.FindControl<ItemListView>("BackpackList");
        _paletteList = this.FindControl<ItemListView>("PaletteList");
        _paletteFilter = this.FindControl<ItemFilterPanel>("PaletteFilter");
        _itemDetailsView = this.FindControl<ItemDetailsPanel>("ItemDetailsView");
        _contentsCount = this.FindControl<TextBlock>("ContentsCount");

        if (_backpackList != null)
        {
            _backpackList.Items = _backpackItems;
            _backpackList.SelectionChanged += OnBackpackSelectionChanged;
        }
        if (_paletteFilter != null)
        {
            _paletteFilter.Items = _paletteItems;
            // Filter writes its results into the same collection the list shows (QM pattern).
            _paletteFilter.FilteredItems = _filteredPaletteItems;
            // Source visibility defaults come from FilterState (#1995): Standard/HAK/Module on,
            // Override off. No per-source force here.
        }
        if (_paletteList != null)
        {
            _paletteList.Items = _filteredPaletteItems;
            _paletteList.SelectionChanged += OnPaletteSelectionChanged;
            // #2415: double-click + context "Add to Inventory" both add via the existing add path.
            _paletteList.ItemOpenRequested += OnPaletteItemActivated;
            _paletteList.AddToBackpackRequested += OnPaletteItemActivated;
            // Context "Edit" → open the UTI in Relique (host handles cross-tool dispatch).
            _paletteList.ItemEditRequested += OnItemEditRequested;
        }
        if (_backpackList != null)
        {
            // #2415 symmetry: double-click adds nothing on backpack; context "Delete" removes.
            _backpackList.DeleteRequested += OnBackpackItemActivated;
            _backpackList.ItemEditRequested += OnItemEditRequested;
        }
        UpdateContentsCount();
    }

    /// <summary>True while at least one backpack row is selected (drives Remove enablement).</summary>
    public bool HasBackpackSelection
    {
        get => _hasBackpackSelection;
        private set { _hasBackpackSelection = value; OnPropertyChanged(); }
    }

    /// <summary>True while at least one palette row is selected (drives Add enablement).</summary>
    public bool HasPaletteSelection
    {
        get => _hasPaletteSelection;
        private set { _hasPaletteSelection = value; OnPropertyChanged(); }
    }

    /// <summary>The backpack collection the host's add/remove commands mutate (UI side).</summary>
    public ObservableCollection<ItemViewModel> BackpackItems => _backpackItems;

    /// <summary>Replace the backpack contents (called by the host after loading a placeable).</summary>
    public void SetBackpackItems(IEnumerable<ItemViewModel> items)
    {
        _backpackItems.Clear();
        foreach (var item in items)
            _backpackItems.Add(item);
        UpdateContentsCount();
    }

    /// <summary>Replace the palette contents (called by the host after the palette cache loads).</summary>
    public void SetPaletteItems(List<ItemViewModel> items)
    {
        if (_paletteFilter != null) _paletteFilter.Items = null;
        _paletteItems.Clear();
        foreach (var item in items)
            _paletteItems.Add(item);
        if (_paletteFilter != null)
        {
            _paletteFilter.Items = _paletteItems;
            _paletteFilter.ApplyFilter();
        }
    }

    /// <summary>Notify the contents counter after the host mutates the backpack via a command.</summary>
    public void OnBackpackChanged() => UpdateContentsCount();

    private void OnBackpackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        HasBackpackSelection = _backpackList?.SelectedItems.Count > 0;
        var selected = _backpackList?.SelectedItems.FirstOrDefault();
        ShowDetails(selected);
    }

    private void OnPaletteSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        HasPaletteSelection = _paletteList?.SelectedItems.Count > 0;
        var selected = _paletteList?.SelectedItems.FirstOrDefault();
        if (selected != null) ShowDetails(selected);
    }

    private void ShowDetails(ItemViewModel? item)
    {
        if (_itemDetailsView == null) return;
        if (item == null) { _itemDetailsView.DataContext = null; return; }

        // Cache-loaded palette rows lack full UTI data; resolve through the host for the details pane.
        var display = item.Item == null && ItemResolver != null ? ItemResolver(item) ?? item : item;
        _itemDetailsView.DataContext = display;
    }

    private void OnAddSelectedClick(object? sender, RoutedEventArgs e)
    {
        var selected = _paletteList?.SelectedItems.FirstOrDefault();
        if (selected != null) AddItemRequested?.Invoke(this, selected);
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        var selected = _backpackList?.SelectedItems.FirstOrDefault();
        if (selected != null) RemoveItemRequested?.Invoke(this, selected);
    }

    /// <summary>Palette double-click / context "Add to Backpack" → add the activated item (#2415).</summary>
    private void OnPaletteItemActivated(object? sender, ItemViewModel item)
        => AddItemRequested?.Invoke(this, item);

    /// <summary>Backpack context "Delete" → remove the activated item (#2415).</summary>
    private void OnBackpackItemActivated(object? sender, ItemViewModel item)
        => RemoveItemRequested?.Invoke(this, item);

    /// <summary>Context "Edit" → host opens the item's UTI in Relique.</summary>
    private void OnItemEditRequested(object? sender, ItemViewModel item)
        => EditItemRequested?.Invoke(this, item);

    private void UpdateContentsCount()
    {
        if (_contentsCount != null)
            _contentsCount.Text = $"Total: {_backpackItems.Count} item{(_backpackItems.Count == 1 ? "" : "s")}";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Radoub.UI.Settings;
using Radoub.UI.ViewModels;

namespace Radoub.UI.Controls;

/// <summary>
/// A DataGrid-based control for displaying item lists with sorting, selection, and filtering.
/// Used by backpack view, item palette, and merchant panels.
/// Selection uses DataGrid row selection (Extended mode supports Ctrl+Click, Shift+Click).
/// </summary>
public partial class ItemListView : UserControl
{
    /// <summary>
    /// Items to display in the grid.
    /// </summary>
    public static readonly StyledProperty<ObservableCollection<ItemViewModel>?> ItemsProperty =
        AvaloniaProperty.Register<ItemListView, ObservableCollection<ItemViewModel>?>(nameof(Items));

    /// <summary>
    /// Currently selected items (via row selection).
    /// </summary>
    public static readonly StyledProperty<ObservableCollection<ItemViewModel>> SelectedItemsProperty =
        AvaloniaProperty.Register<ItemListView, ObservableCollection<ItemViewModel>>(
            nameof(SelectedItems),
            defaultValue: new ObservableCollection<ItemViewModel>());

    /// <summary>
    /// Context key for column width persistence.
    /// </summary>
    public static readonly StyledProperty<string> ContextKeyProperty =
        AvaloniaProperty.Register<ItemListView, string>(nameof(ContextKey), defaultValue: "Default");

    /// <summary>
    /// Column settings provider for width persistence.
    /// </summary>
    public static readonly StyledProperty<IColumnSettings?> ColumnSettingsProperty =
        AvaloniaProperty.Register<ItemListView, IColumnSettings?>(nameof(ColumnSettings));

    /// <summary>
    /// Event raised when row selection changes.
    /// </summary>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// Event raised when user requests to open an item.
    /// </summary>
    public event EventHandler<ItemViewModel>? ItemOpenRequested;

    /// <summary>
    /// Event raised when user requests to edit an item.
    /// </summary>
    public event EventHandler<ItemViewModel>? ItemEditRequested;

    /// <summary>
    /// Event raised when drag operation starts.
    /// Handler should set e.Data with drag data.
    /// </summary>
    public event EventHandler<ItemDragEventArgs>? DragStarting;

    /// <summary>
    /// Event raised when items are dropped onto this list.
    /// Handler should process the dropped data.
    /// </summary>
    public event EventHandler<ItemDropEventArgs>? ItemDropped;

    /// <summary>
    /// Event raised when "Equip" is selected from context menu.
    /// Available when ContextKey is "Backpack" or "Palette".
    /// </summary>
    public event EventHandler<ItemViewModel>? EquipRequested;

    /// <summary>
    /// Event raised when "Add to Backpack" is selected from context menu.
    /// Available when ContextKey is "Palette".
    /// </summary>
    public event EventHandler<ItemViewModel>? AddToBackpackRequested;

    /// <summary>
    /// Event raised when "Delete" is selected from context menu.
    /// Available when ContextKey is "Backpack".
    /// </summary>
    public event EventHandler<ItemViewModel>? DeleteRequested;

    // Column keys for persistence (must match XAML column order)
    private static readonly string[] ColumnKeys = { "Icon", "Name", "ResRef", "Tag", "Type", "Value", "Properties" };

    // Drag state
    private Point _dragStartPoint;
    private bool _isDragging;
    private const double DragThreshold = 5;

    public ItemListView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // Wire up drag events
        ItemsGrid.PointerPressed += OnGridPointerPressed;
        ItemsGrid.PointerMoved += OnGridPointerMoved;
        ItemsGrid.PointerReleased += OnGridPointerReleased;

        // Enable drop support
        DragDrop.SetAllowDrop(ItemsGrid, true);
        ItemsGrid.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        ItemsGrid.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        LoadColumnWidths();

        // Subscribe to column width changes
        foreach (var column in ItemsGrid.Columns)
        {
            column.PropertyChanged += OnColumnPropertyChanged;
        }

        // Ensure ItemsSource is set after control is fully loaded
        if (Items != null && ItemsGrid.ItemsSource != Items)
        {
            ItemsGrid.ItemsSource = Items;
        }

        // Add context-specific menu items based on ContextKey
        BuildContextSpecificMenuItems();
    }

    private void BuildContextSpecificMenuItems()
    {
        if (RowContextMenu == null) return;

        var contextKey = ContextKey;

        if (contextKey == "Backpack")
        {
            // Insert "Equip" and "Delete" before the separator/copy items
            var equipItem = new MenuItem { Header = "Equip" };
            equipItem.Click += OnContextMenuEquip;

            var deleteItem = new MenuItem { Header = "Delete" };
            deleteItem.Click += OnContextMenuDelete;

            // Insert at position 2 (after Open/Edit)
            RowContextMenu.Items.Insert(2, new Separator());
            RowContextMenu.Items.Insert(3, equipItem);
            RowContextMenu.Items.Insert(4, deleteItem);
        }
        else if (contextKey == "Palette")
        {
            var addToBackpackItem = new MenuItem { Header = "Add to Backpack" };
            addToBackpackItem.Click += OnContextMenuAddToBackpack;

            var equipItem = new MenuItem { Header = "Equip" };
            equipItem.Click += OnContextMenuEquip;

            RowContextMenu.Items.Insert(2, new Separator());
            RowContextMenu.Items.Insert(3, addToBackpackItem);
            RowContextMenu.Items.Insert(4, equipItem);
        }
    }

    private void OnContextMenuEquip(object? sender, RoutedEventArgs e)
    {
        var selected = ItemsGrid.SelectedItem as ItemViewModel;
        if (selected != null)
            EquipRequested?.Invoke(this, selected);
    }

    private void OnContextMenuDelete(object? sender, RoutedEventArgs e)
    {
        var selected = ItemsGrid.SelectedItem as ItemViewModel;
        if (selected != null)
            DeleteRequested?.Invoke(this, selected);
    }

    private void OnContextMenuAddToBackpack(object? sender, RoutedEventArgs e)
    {
        var selected = ItemsGrid.SelectedItem as ItemViewModel;
        if (selected != null)
            AddToBackpackRequested?.Invoke(this, selected);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SaveColumnWidths();

        foreach (var column in ItemsGrid.Columns)
        {
            column.PropertyChanged -= OnColumnPropertyChanged;
        }
    }

    private void OnColumnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == "ActualWidth" && sender is DataGridColumn column)
        {
            SaveColumnWidth(column);
        }
    }

    /// <summary>
    /// Items to display in the grid.
    /// </summary>
    public ObservableCollection<ItemViewModel>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>
    /// Currently selected items (via row selection).
    /// </summary>
    public ObservableCollection<ItemViewModel> SelectedItems
    {
        get => GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    /// <summary>
    /// Context key for column width persistence (e.g., "Backpack", "Palette").
    /// </summary>
    public string ContextKey
    {
        get => GetValue(ContextKeyProperty);
        set => SetValue(ContextKeyProperty, value);
    }

    /// <summary>
    /// Column settings provider for width persistence.
    /// </summary>
    public IColumnSettings? ColumnSettings
    {
        get => GetValue(ColumnSettingsProperty);
        set => SetValue(ColumnSettingsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsProperty)
        {
            // ItemsGrid may be null if called before InitializeComponent completes
            if (ItemsGrid != null)
            {
                ItemsGrid.ItemsSource = change.NewValue as ObservableCollection<ItemViewModel>;
            }
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SelectedItems.Clear();
        foreach (var item in ItemsGrid.SelectedItems.Cast<ItemViewModel>())
        {
            SelectedItems.Add(item);
        }
        SelectionChanged?.Invoke(this, e);
    }

    #region Column Width Persistence

    private void LoadColumnWidths()
    {
        if (ColumnSettings == null) return;

        for (int i = 0; i < ItemsGrid.Columns.Count && i < ColumnKeys.Length; i++)
        {
            var column = ItemsGrid.Columns[i];
            var key = ColumnKeys[i];

            // Skip non-resizable columns
            if (!column.CanUserResize) continue;

            var width = ColumnSettings.GetColumnWidth(ContextKey, key);
            if (width.HasValue && width.Value > 0)
            {
                column.Width = new DataGridLength(width.Value);
            }
        }
    }

    private void SaveColumnWidths()
    {
        if (ColumnSettings == null) return;

        for (int i = 0; i < ItemsGrid.Columns.Count && i < ColumnKeys.Length; i++)
        {
            var column = ItemsGrid.Columns[i];
            var key = ColumnKeys[i];

            if (!column.CanUserResize) continue;

            ColumnSettings.SetColumnWidth(ContextKey, key, column.ActualWidth);
        }

        ColumnSettings.Save();
    }

    private void SaveColumnWidth(DataGridColumn column)
    {
        if (ColumnSettings == null) return;

        var index = ItemsGrid.Columns.IndexOf(column);
        if (index >= 0 && index < ColumnKeys.Length)
        {
            var key = ColumnKeys[index];
            ColumnSettings.SetColumnWidth(ContextKey, key, column.ActualWidth);
        }
    }

    #endregion

    #region Context Menu Handlers

    private void OnContextMenuOpen(object? sender, RoutedEventArgs e)
    {
        var selected = ItemsGrid.SelectedItem as ItemViewModel;
        if (selected != null)
        {
            ItemOpenRequested?.Invoke(this, selected);
        }
    }

    private void OnContextMenuEdit(object? sender, RoutedEventArgs e)
    {
        var selected = ItemsGrid.SelectedItem as ItemViewModel;
        if (selected != null)
        {
            ItemEditRequested?.Invoke(this, selected);
        }
    }

    private async void OnContextMenuCopyResRef(object? sender, RoutedEventArgs e)
    {
        var selected = ItemsGrid.SelectedItem as ItemViewModel;
        if (selected != null && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(selected.ResRef);
        }
    }

    private async void OnContextMenuCopyTag(object? sender, RoutedEventArgs e)
    {
        var selected = ItemsGrid.SelectedItem as ItemViewModel;
        if (selected != null && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(selected.Tag);
        }
    }

    private async void OnContextMenuCopyName(object? sender, RoutedEventArgs e)
    {
        var selected = ItemsGrid.SelectedItem as ItemViewModel;
        if (selected != null && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(selected.Name);
        }
    }

    #endregion

    #region Drag Source

    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(ItemsGrid).Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = e.GetPosition(ItemsGrid);
            _isDragging = false;
        }
    }

    private async void OnGridPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(ItemsGrid).Properties.IsLeftButtonPressed)
        {
            _isDragging = false;
            return;
        }

        var currentPoint = e.GetPosition(ItemsGrid);
        var delta = currentPoint - _dragStartPoint;

        // Check if we've moved beyond the drag threshold
        if (!_isDragging && (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold))
        {
            _isDragging = true;

            var selectedItems = ItemsGrid.SelectedItems.Cast<ItemViewModel>().ToList();
            if (selectedItems.Count == 0) return;

            // Raise event to let consumer provide drag data
            var args = new ItemDragEventArgs(selectedItems);
            DragStarting?.Invoke(this, args);

            if (args.Data != null)
            {
#pragma warning disable CS0618 // DataObject is obsolete - Avalonia 11 uses DataTransfer
                var dragData = new DataObject();
                dragData.Set(args.DataFormat ?? "ItemViewModels", args.Data);

                await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy | DragDropEffects.Move);
#pragma warning restore CS0618
            }

            _isDragging = false;
        }
    }

    private void OnGridPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }

    #endregion

    #region Drop Handling

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Accept drops if we have a handler
        if (ItemDropped != null)
        {
            e.DragEffects = DragDropEffects.Copy | DragDropEffects.Move;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (ItemDropped == null) return;

#pragma warning disable CS0618 // e.Data is obsolete but we need IDataObject.Get/Contains methods
        var args = new ItemDropEventArgs(e.Data);
#pragma warning restore CS0618
        ItemDropped.Invoke(this, args);
    }

    #endregion
}

/// <summary>
/// Event args for drag operations from ItemListView.
/// </summary>
public class ItemDragEventArgs : EventArgs
{
    /// <summary>
    /// Items being dragged.
    /// </summary>
    public IReadOnlyList<ItemViewModel> Items { get; }

    /// <summary>
    /// Data to include in drag operation. Set by event handler.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Data format string. Defaults to "ItemViewModels".
    /// </summary>
    public string? DataFormat { get; set; }

    public ItemDragEventArgs(IReadOnlyList<ItemViewModel> items)
    {
        Items = items;
    }
}

/// <summary>
/// Event args for drop operations on ItemListView.
/// </summary>
public class ItemDropEventArgs : EventArgs
{
    /// <summary>
    /// The drag data transfer object (uses deprecated IDataObject for compatibility with DragDrop.Set).
    /// </summary>
#pragma warning disable CS0618 // IDataObject is obsolete
    public IDataObject DataObject { get; }

    public ItemDropEventArgs(IDataObject dataObject)
    {
        DataObject = dataObject;
    }
#pragma warning restore CS0618
}

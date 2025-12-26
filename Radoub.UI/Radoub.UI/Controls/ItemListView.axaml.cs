using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
/// </summary>
public partial class ItemListView : UserControl
{
    /// <summary>
    /// Items to display in the grid.
    /// </summary>
    public static readonly StyledProperty<ObservableCollection<ItemViewModel>?> ItemsProperty =
        AvaloniaProperty.Register<ItemListView, ObservableCollection<ItemViewModel>?>(nameof(Items));

    /// <summary>
    /// Currently selected items (via row selection, not checkbox).
    /// </summary>
    public static readonly StyledProperty<ObservableCollection<ItemViewModel>> SelectedItemsProperty =
        AvaloniaProperty.Register<ItemListView, ObservableCollection<ItemViewModel>>(
            nameof(SelectedItems),
            defaultValue: new ObservableCollection<ItemViewModel>());

    /// <summary>
    /// Items marked via checkbox selection.
    /// </summary>
    public static readonly StyledProperty<ObservableCollection<ItemViewModel>> CheckedItemsProperty =
        AvaloniaProperty.Register<ItemListView, ObservableCollection<ItemViewModel>>(
            nameof(CheckedItems),
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
    /// Event raised when checkbox selection changes.
    /// </summary>
    public event EventHandler? CheckedItemsChanged;

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

    // Column keys for persistence
    private static readonly string[] ColumnKeys = { "Check", "Icon", "Name", "ResRef", "Tag", "Type", "Value", "Properties" };

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
        // This handles the case where Items was set before the control loaded
        if (Items != null && ItemsGrid.ItemsSource != Items)
        {
            ItemsGrid.ItemsSource = Items;
            UpdateSelectionCount();
        }
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
    /// Items marked via checkbox selection.
    /// </summary>
    public ObservableCollection<ItemViewModel> CheckedItems
    {
        get => GetValue(CheckedItemsProperty);
        set => SetValue(CheckedItemsProperty, value);
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
            var oldItems = change.OldValue as ObservableCollection<ItemViewModel>;
            var newItems = change.NewValue as ObservableCollection<ItemViewModel>;

            if (oldItems != null)
            {
                oldItems.CollectionChanged -= OnItemsCollectionChanged;
                UnsubscribeFromItemChanges(oldItems);
            }

            if (newItems != null)
            {
                newItems.CollectionChanged += OnItemsCollectionChanged;
                SubscribeToItemChanges(newItems);
            }

            // ItemsGrid may be null if called before InitializeComponent completes
            if (ItemsGrid != null)
            {
                ItemsGrid.ItemsSource = newItems;
            }
            UpdateSelectionCount();
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            UnsubscribeFromItemChanges(e.OldItems.Cast<ItemViewModel>());
        }

        if (e.NewItems != null)
        {
            SubscribeToItemChanges(e.NewItems.Cast<ItemViewModel>());
        }

        UpdateSelectionCount();
    }

    private void SubscribeToItemChanges(IEnumerable<ItemViewModel> items)
    {
        foreach (var item in items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
    }

    private void UnsubscribeFromItemChanges(IEnumerable<ItemViewModel> items)
    {
        foreach (var item in items)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
    }

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemViewModel.IsSelected))
        {
            UpdateCheckedItems();
        }
    }

    private void UpdateCheckedItems()
    {
        CheckedItems.Clear();
        if (Items != null)
        {
            foreach (var item in Items.Where(i => i.IsSelected))
            {
                CheckedItems.Add(item);
            }
        }
        UpdateSelectionCount();
        CheckedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelectionCount()
    {
        var checkedCount = Items?.Count(i => i.IsSelected) ?? 0;
        var totalCount = Items?.Count ?? 0;
        SelectionCountText.Text = $"{checkedCount} of {totalCount} checked";
    }

    private void OnSelectAllClick(object? sender, RoutedEventArgs e)
    {
        if (Items == null) return;

        foreach (var item in Items)
        {
            item.IsSelected = true;
        }
    }

    private void OnSelectNoneClick(object? sender, RoutedEventArgs e)
    {
        if (Items == null) return;

        foreach (var item in Items)
        {
            item.IsSelected = false;
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

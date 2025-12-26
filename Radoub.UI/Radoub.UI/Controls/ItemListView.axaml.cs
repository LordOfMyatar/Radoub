using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
    /// Event raised when row selection changes.
    /// </summary>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// Event raised when checkbox selection changes.
    /// </summary>
    public event EventHandler? CheckedItemsChanged;

    public ItemListView()
    {
        InitializeComponent();
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

            ItemsGrid.ItemsSource = newItems;
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
}

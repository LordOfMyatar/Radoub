using System.Collections.ObjectModel;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.Formats.Services;
using Radoub.UI.Models;
using Radoub.UI.Settings;
using Radoub.UI.ViewModels;

namespace Radoub.UI.Controls;

/// <summary>
/// Filter panel for ItemListView. Provides text search, source filtering, and type filtering.
/// Integrates with ItemListView via FilteredItems property.
/// </summary>
public partial class ItemFilterPanel : UserControl
{
    #region Styled Properties

    /// <summary>
    /// Source items to filter.
    /// </summary>
    public static readonly StyledProperty<ObservableCollection<ItemViewModel>?> ItemsProperty =
        AvaloniaProperty.Register<ItemFilterPanel, ObservableCollection<ItemViewModel>?>(nameof(Items));

    /// <summary>
    /// Filtered items output.
    /// </summary>
    public static readonly StyledProperty<ObservableCollection<ItemViewModel>> FilteredItemsProperty =
        AvaloniaProperty.Register<ItemFilterPanel, ObservableCollection<ItemViewModel>>(
            nameof(FilteredItems),
            defaultValue: new ObservableCollection<ItemViewModel>());

    /// <summary>
    /// Search text for filtering.
    /// </summary>
    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<ItemFilterPanel, string>(nameof(SearchText), defaultValue: string.Empty);

    /// <summary>
    /// Show items from base game (Standard).
    /// </summary>
    public static readonly StyledProperty<bool> ShowStandardProperty =
        AvaloniaProperty.Register<ItemFilterPanel, bool>(nameof(ShowStandard), defaultValue: true);

    /// <summary>
    /// Show items from module/HAK (Custom).
    /// </summary>
    public static readonly StyledProperty<bool> ShowCustomProperty =
        AvaloniaProperty.Register<ItemFilterPanel, bool>(nameof(ShowCustom), defaultValue: true);

    /// <summary>
    /// Available item types for filtering.
    /// </summary>
    public static readonly StyledProperty<ObservableCollection<ItemTypeInfo>> ItemTypesProperty =
        AvaloniaProperty.Register<ItemFilterPanel, ObservableCollection<ItemTypeInfo>>(
            nameof(ItemTypes),
            defaultValue: new ObservableCollection<ItemTypeInfo>());

    /// <summary>
    /// Currently selected item type filter.
    /// </summary>
    public static readonly StyledProperty<ItemTypeInfo?> SelectedItemTypeProperty =
        AvaloniaProperty.Register<ItemFilterPanel, ItemTypeInfo?>(nameof(SelectedItemType));

    /// <summary>
    /// Game data service for loading item types from baseitems.2da.
    /// </summary>
    public static readonly StyledProperty<IGameDataService?> GameDataServiceProperty =
        AvaloniaProperty.Register<ItemFilterPanel, IGameDataService?>(nameof(GameDataService));

    /// <summary>
    /// Filter settings provider for state persistence.
    /// </summary>
    public static readonly StyledProperty<IFilterSettings?> FilterSettingsProperty =
        AvaloniaProperty.Register<ItemFilterPanel, IFilterSettings?>(nameof(FilterSettings));

    /// <summary>
    /// Context key for filter state persistence.
    /// </summary>
    public static readonly StyledProperty<string> ContextKeyProperty =
        AvaloniaProperty.Register<ItemFilterPanel, string>(nameof(ContextKey), defaultValue: "Default");

    #endregion

    #region Events

    /// <summary>
    /// Raised when filter criteria change.
    /// </summary>
    public event EventHandler? FilterChanged;

    #endregion

    #region Private Fields

    private readonly System.Timers.Timer _debounceTimer;
    private const int DebounceDelayMs = 300;
    private bool _isInitialized;

    #endregion

    public ItemFilterPanel()
    {
        InitializeComponent();

        // Setup debounce timer for search
        _debounceTimer = new System.Timers.Timer(DebounceDelayMs);
        _debounceTimer.AutoReset = false;
        _debounceTimer.Elapsed += OnDebounceTimerElapsed;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #region Public Properties

    /// <summary>
    /// Source items to filter.
    /// </summary>
    public ObservableCollection<ItemViewModel>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>
    /// Filtered items output. Bind ItemListView.Items to this.
    /// </summary>
    public ObservableCollection<ItemViewModel> FilteredItems
    {
        get => GetValue(FilteredItemsProperty);
        set => SetValue(FilteredItemsProperty, value);
    }

    /// <summary>
    /// Current search text.
    /// </summary>
    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    /// <summary>
    /// Show base game items.
    /// </summary>
    public bool ShowStandard
    {
        get => GetValue(ShowStandardProperty);
        set => SetValue(ShowStandardProperty, value);
    }

    /// <summary>
    /// Show module/HAK items.
    /// </summary>
    public bool ShowCustom
    {
        get => GetValue(ShowCustomProperty);
        set => SetValue(ShowCustomProperty, value);
    }

    /// <summary>
    /// Available item types from baseitems.2da.
    /// </summary>
    public ObservableCollection<ItemTypeInfo> ItemTypes
    {
        get => GetValue(ItemTypesProperty);
        set => SetValue(ItemTypesProperty, value);
    }

    /// <summary>
    /// Currently selected item type filter. Null means "All Types".
    /// </summary>
    public ItemTypeInfo? SelectedItemType
    {
        get => GetValue(SelectedItemTypeProperty);
        set => SetValue(SelectedItemTypeProperty, value);
    }

    /// <summary>
    /// Game data service for loading item types.
    /// </summary>
    public IGameDataService? GameDataService
    {
        get => GetValue(GameDataServiceProperty);
        set => SetValue(GameDataServiceProperty, value);
    }

    /// <summary>
    /// Filter settings provider for persistence.
    /// </summary>
    public IFilterSettings? FilterSettings
    {
        get => GetValue(FilterSettingsProperty);
        set => SetValue(FilterSettingsProperty, value);
    }

    /// <summary>
    /// Context key for filter settings (e.g., "Backpack", "Palette").
    /// </summary>
    public string ContextKey
    {
        get => GetValue(ContextKeyProperty);
        set => SetValue(ContextKeyProperty, value);
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        LoadItemTypes();
        LoadFilterState();
        _isInitialized = true;
        ApplyFilter();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SaveFilterState();
        _debounceTimer.Stop();
        _debounceTimer.Dispose();

        // Unsubscribe from collection changes
        if (Items != null)
        {
            Items.CollectionChanged -= OnItemsCollectionChanged;
        }
    }

    #endregion

    #region Property Changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsProperty)
        {
            // Unsubscribe from old collection
            if (change.OldValue is ObservableCollection<ItemViewModel> oldItems)
            {
                oldItems.CollectionChanged -= OnItemsCollectionChanged;
            }

            // Subscribe to new collection
            if (change.NewValue is ObservableCollection<ItemViewModel> newItems)
            {
                newItems.CollectionChanged += OnItemsCollectionChanged;
            }

            ApplyFilter();
        }
        else if (change.Property == SearchTextProperty)
        {
            // Debounce search text changes
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
        else if (change.Property == ShowStandardProperty ||
                 change.Property == ShowCustomProperty ||
                 change.Property == SelectedItemTypeProperty)
        {
            ApplyFilter();
        }
        else if (change.Property == GameDataServiceProperty)
        {
            LoadItemTypes();
            ApplyFilter();
        }
    }

    private void OnDebounceTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // Run filter on UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(ApplyFilter);
    }

    private void OnItemsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Re-apply filter when source items change
        ApplyFilter();
    }

    #endregion

    #region Item Type Loading

    private void LoadItemTypes()
    {
        ItemTypes.Clear();

        // Always add "All Types" option
        ItemTypes.Add(ItemTypeInfo.AllTypes);

        if (GameDataService == null || !GameDataService.IsConfigured)
            return;

        var baseItems = GameDataService.Get2DA("baseitems");
        if (baseItems == null)
            return;

        // Load valid base item types
        for (int i = 0; i < baseItems.Rows.Count; i++)
        {
            // Skip invalid/unused entries
            var label = baseItems.GetValue(i, "label");
            if (string.IsNullOrEmpty(label) || label == "****")
                continue;

            // Get display name from TLK
            var nameStrRef = baseItems.GetValue(i, "Name");
            string displayName;
            if (nameStrRef != null && nameStrRef != "****")
            {
                displayName = GameDataService.GetString(nameStrRef) ?? label;
            }
            else
            {
                displayName = label;
            }

            ItemTypes.Add(new ItemTypeInfo(i, displayName, label));
        }

        // Set default selection to "All Types"
        SelectedItemType = ItemTypeInfo.AllTypes;
    }

    #endregion

    #region Filtering

    /// <summary>
    /// Apply current filter settings to Items and populate FilteredItems.
    /// </summary>
    public void ApplyFilter()
    {
        FilteredItems.Clear();

        if (Items == null)
        {
            UpdateResultCount(0, 0);
            return;
        }

        var searchLower = SearchText?.ToLowerInvariant() ?? string.Empty;
        var typeFilter = SelectedItemType;
        var showStandard = ShowStandard;
        var showCustom = ShowCustom;

        int totalCount = Items.Count;
        int matchCount = 0;

        foreach (var item in Items)
        {
            if (MatchesFilter(item, searchLower, typeFilter, showStandard, showCustom))
            {
                FilteredItems.Add(item);
                matchCount++;
            }
        }

        UpdateResultCount(matchCount, totalCount);

        if (_isInitialized)
        {
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool MatchesFilter(
        ItemViewModel item,
        string searchLower,
        ItemTypeInfo? typeFilter,
        bool showStandard,
        bool showCustom)
    {
        // Source filter (Standard = BIF, Custom = Override/HAK/Module)
        if (item.IsStandard && !showStandard) return false;
        if (item.IsCustom && !showCustom) return false;

        // Type filter
        if (typeFilter != null && !typeFilter.IsAllTypes)
        {
            if (item.BaseItem != typeFilter.BaseItemIndex)
                return false;
        }

        // Text search (name, tag, resref)
        if (!string.IsNullOrEmpty(searchLower))
        {
            var nameMatch = item.Name.ToLowerInvariant().Contains(searchLower);
            var tagMatch = item.Tag.ToLowerInvariant().Contains(searchLower);
            var resRefMatch = item.ResRef.ToLowerInvariant().Contains(searchLower);

            if (!nameMatch && !tagMatch && !resRefMatch)
                return false;
        }

        return true;
    }

    private void UpdateResultCount(int matchCount, int totalCount)
    {
        ResultCount.Text = matchCount == totalCount
            ? $"{totalCount} items"
            : $"{matchCount} of {totalCount} items";
    }

    #endregion

    #region Filter State Persistence

    private void LoadFilterState()
    {
        if (FilterSettings == null) return;

        var state = FilterSettings.GetFilterState(ContextKey);
        if (state == null) return;

        ShowStandard = state.ShowStandard;
        ShowCustom = state.ShowCustom;
        SearchText = state.SearchText ?? string.Empty;

        // Restore selected type
        if (state.SelectedBaseItemIndex.HasValue)
        {
            SelectedItemType = ItemTypes.FirstOrDefault(t => t.BaseItemIndex == state.SelectedBaseItemIndex.Value)
                              ?? ItemTypeInfo.AllTypes;
        }
        else
        {
            SelectedItemType = ItemTypeInfo.AllTypes;
        }
    }

    private void SaveFilterState()
    {
        if (FilterSettings == null) return;

        var state = new FilterState
        {
            ShowStandard = ShowStandard,
            ShowCustom = ShowCustom,
            SearchText = string.IsNullOrEmpty(SearchText) ? null : SearchText,
            SelectedBaseItemIndex = SelectedItemType?.IsAllTypes == true ? null : SelectedItemType?.BaseItemIndex
        };

        FilterSettings.SetFilterState(ContextKey, state);
        FilterSettings.Save();
    }

    #endregion

    #region Event Handlers

    private void OnClearSearchClick(object? sender, RoutedEventArgs e)
    {
        SearchText = string.Empty;
    }

    #endregion
}

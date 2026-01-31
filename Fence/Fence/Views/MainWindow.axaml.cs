using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MerchantEditor.Services;
using MerchantEditor.ViewModels;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.Formats.Utm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Radoub.UI.Utils;
using Radoub.UI.Views;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow - Fence Merchant Editor
/// Partial class split into:
/// - MainWindow.axaml.cs (this file): Core initialization, properties, UI helpers
/// - MainWindow.FileOps.cs: File operations (Open/Save/Recent)
/// - MainWindow.ItemPalette.cs: Item palette loading
/// - MainWindow.StoreOperations.cs: Store inventory and buy restrictions
/// - MainWindow.Variables.cs: Local variable operations (add/edit/remove)
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private UtmFile? _currentStore;
    private string? _currentFilePath;
    private bool _isDirty;

    // Regex for valid ResRef characters (alphanumeric + underscore)
    private static readonly Regex ValidResRefPattern = new(@"^[a-zA-Z0-9_]*$", RegexOptions.Compiled);

    private BaseItemTypeService? _baseItemTypeService;
    private ItemResolutionService? _itemResolutionService;
    private IGameDataService? _gameDataService;
    private bool _servicesInitialized;

    // Store palette categories loaded from storepal.itp
    // Maps dropdown index to category ID for CEP/custom content support
    private readonly List<PaletteCategory> _storeCategories = new();

    public ObservableCollection<StoreItemViewModel> StoreItems { get; } = new();
    public ObservableCollection<PaletteItemViewModel> PaletteItems { get; } = new();
    public ObservableCollection<SelectableBaseItemTypeViewModel> SelectableBaseItemTypes { get; } = new();

    public bool HasFile => _currentStore != null;
    public bool HasSelection => StoreInventoryGrid?.SelectedItems?.Count > 0;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Defer heavy I/O (GameDataService, palette loading) to Opened event
        // Only do fast, synchronous UI setup here
        Loaded += OnWindowLoaded;
        Opened += OnWindowOpened;

        // Restore window position from settings
        RestoreWindowPosition();

        // Wire up event handlers
        Closing += OnWindowClosing;

        // Wire up data grids for double-click
        StoreInventoryGrid.DoubleTapped += OnStoreInventoryDoubleTapped;
        ItemPaletteGrid.DoubleTapped += OnItemPaletteDoubleTapped;

        // Track property changes on store items for dirty state and validation
        StoreItems.CollectionChanged += OnStoreItemsCollectionChanged;

        // Set up recent files menu
        UpdateRecentFilesMenu();

        // Initialize store browser panel (#1144)
        InitializeStoreBrowserPanel();

        UnifiedLogger.LogApplication(LogLevel.INFO, "Fence MainWindow initialized");
    }

    #region Store Browser Panel (#1144)

    /// <summary>
    /// Initializes store browser panel with context and event handlers (#1144).
    /// </summary>
    private void InitializeStoreBrowserPanel()
    {
        var storeBrowserPanel = this.FindControl<StoreBrowserPanel>("StoreBrowserPanel");
        if (storeBrowserPanel == null)
        {
            UnifiedLogger.LogUI(LogLevel.WARN, "StoreBrowserPanel not found");
            return;
        }

        // Note: StoreBrowserPanel gets NWN path from RadoubSettings internally
        // The context is only needed for HAK scanning

        // Subscribe to file selection events
        storeBrowserPanel.FileSelected += OnStoreBrowserFileSelected;

        // Subscribe to collapse/expand events
        storeBrowserPanel.CollapsedChanged += OnStoreBrowserCollapsedChanged;

        // Restore panel state from settings
        RestoreStoreBrowserPanelState();

        // Update menu item checkmark
        UpdateStoreBrowserMenuState();

        UnifiedLogger.LogUI(LogLevel.INFO, "StoreBrowserPanel initialized");
    }

    /// <summary>
    /// Restores store browser panel state from settings (#1144).
    /// </summary>
    private void RestoreStoreBrowserPanelState()
    {
        var settings = SettingsService.Instance;
        var outerContentGrid = this.FindControl<Grid>("OuterContentGrid");
        var storeBrowserPanel = this.FindControl<StoreBrowserPanel>("StoreBrowserPanel");
        var storeBrowserSplitter = this.FindControl<GridSplitter>("StoreBrowserSplitter");

        if (outerContentGrid == null || storeBrowserPanel == null || storeBrowserSplitter == null)
            return;

        var storeBrowserColumn = outerContentGrid.ColumnDefinitions[0];
        var storeBrowserSplitterColumn = outerContentGrid.ColumnDefinitions[1];

        if (settings.StoreBrowserPanelVisible)
        {
            storeBrowserColumn.Width = new GridLength(settings.StoreBrowserPanelWidth, GridUnitType.Pixel);
            storeBrowserSplitterColumn.Width = new GridLength(5, GridUnitType.Pixel);
            storeBrowserPanel.IsVisible = true;
            storeBrowserSplitter.IsVisible = true;
        }
        else
        {
            storeBrowserColumn.Width = new GridLength(0, GridUnitType.Pixel);
            storeBrowserSplitterColumn.Width = new GridLength(0, GridUnitType.Pixel);
            storeBrowserPanel.IsVisible = false;
            storeBrowserSplitter.IsVisible = false;
        }
    }

    /// <summary>
    /// Saves store browser panel width to settings (#1144).
    /// </summary>
    private void SaveStoreBrowserPanelSize()
    {
        var outerContentGrid = this.FindControl<Grid>("OuterContentGrid");
        if (outerContentGrid == null) return;

        var storeBrowserColumn = outerContentGrid.ColumnDefinitions[0];
        if (storeBrowserColumn.Width.IsAbsolute && storeBrowserColumn.Width.Value > 0)
        {
            SettingsService.Instance.StoreBrowserPanelWidth = storeBrowserColumn.Width.Value;
        }
    }

    /// <summary>
    /// Sets store browser panel visibility (#1144).
    /// </summary>
    private void SetStoreBrowserPanelVisible(bool visible)
    {
        var settings = SettingsService.Instance;
        settings.StoreBrowserPanelVisible = visible;

        var outerContentGrid = this.FindControl<Grid>("OuterContentGrid");
        var storeBrowserPanel = this.FindControl<StoreBrowserPanel>("StoreBrowserPanel");
        var storeBrowserSplitter = this.FindControl<GridSplitter>("StoreBrowserSplitter");

        if (outerContentGrid == null || storeBrowserPanel == null || storeBrowserSplitter == null)
            return;

        var storeBrowserColumn = outerContentGrid.ColumnDefinitions[0];
        var storeBrowserSplitterColumn = outerContentGrid.ColumnDefinitions[1];

        if (visible)
        {
            storeBrowserColumn.Width = new GridLength(settings.StoreBrowserPanelWidth, GridUnitType.Pixel);
            storeBrowserSplitterColumn.Width = new GridLength(5, GridUnitType.Pixel);
            storeBrowserPanel.IsVisible = true;
            storeBrowserSplitter.IsVisible = true;
        }
        else
        {
            // Save current width before hiding
            if (storeBrowserColumn.Width.IsAbsolute && storeBrowserColumn.Width.Value > 0)
            {
                settings.StoreBrowserPanelWidth = storeBrowserColumn.Width.Value;
            }

            storeBrowserColumn.Width = new GridLength(0, GridUnitType.Pixel);
            storeBrowserSplitterColumn.Width = new GridLength(0, GridUnitType.Pixel);
            storeBrowserPanel.IsVisible = false;
            storeBrowserSplitter.IsVisible = false;
        }

        UpdateStoreBrowserMenuState();
    }

    /// <summary>
    /// Handles collapse/expand button clicks from StoreBrowserPanel (#1144).
    /// </summary>
    private void OnStoreBrowserCollapsedChanged(object? sender, bool isCollapsed)
    {
        SetStoreBrowserPanelVisible(!isCollapsed);
    }

    /// <summary>
    /// Updates the StoreBrowserPanel's current file highlight (#1144).
    /// </summary>
    private void UpdateStoreBrowserCurrentFile(string? filePath)
    {
        var storeBrowserPanel = this.FindControl<StoreBrowserPanel>("StoreBrowserPanel");
        if (storeBrowserPanel != null)
        {
            storeBrowserPanel.CurrentFilePath = filePath;

            // Update module path if we have a file
            if (!string.IsNullOrEmpty(filePath))
            {
                var modulePath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(modulePath))
                {
                    storeBrowserPanel.ModulePath = modulePath;
                }
            }
        }
    }

    /// <summary>
    /// Handles file selection in the store browser panel (#1144).
    /// </summary>
    private async void OnStoreBrowserFileSelected(object? sender, FileSelectedEventArgs e)
    {
        // Only load on single click (per issue requirements)
        if (e.Entry.IsFromHak)
        {
            // HAK files can't be edited directly - show info
            UpdateStatusBar($"HAK stores are read-only: {e.Entry.Name}");
            return;
        }

        if (string.IsNullOrEmpty(e.Entry.FilePath))
        {
            UnifiedLogger.LogUI(LogLevel.WARN, $"StoreBrowserPanel: No file path for {e.Entry.Name}");
            return;
        }

        // Skip if this is already the loaded file
        if (string.Equals(_currentFilePath, e.Entry.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"StoreBrowserPanel: File already loaded: {e.Entry.Name}");
            return;
        }

        // Auto-save if dirty
        if (_isDirty && _currentStore != null && !string.IsNullOrEmpty(_currentFilePath))
        {
            UpdateStatusBar("Auto-saving...");
            await SaveFileAsync(_currentFilePath);
        }

        // Load the selected file
        LoadFile(e.Entry.FilePath);

        // Update the current file highlight
        UpdateStoreBrowserCurrentFile(e.Entry.FilePath);
    }

    /// <summary>
    /// Toggles store browser panel visibility from View menu (#1144).
    /// </summary>
    private void OnToggleStoreBrowserClick(object? sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance;
        SetStoreBrowserPanelVisible(!settings.StoreBrowserPanelVisible);
    }

    /// <summary>
    /// Updates View menu checkmark for Store Browser item (#1144).
    /// </summary>
    private void UpdateStoreBrowserMenuState()
    {
        var menuItem = this.FindControl<MenuItem>("StoreBrowserMenuItem");
        if (menuItem != null)
        {
            var isVisible = SettingsService.Instance.StoreBrowserPanelVisible;
            menuItem.Icon = isVisible ? new TextBlock { Text = "✓" } : null;
        }
    }

    #endregion

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;

        UpdateStatusBar("Initializing...");

        // Fire and forget - don't block UI thread
        // Service init and palette loading happen in background
        _ = InitializeAndLoadAsync();
    }

    private async System.Threading.Tasks.Task InitializeAndLoadAsync()
    {
        // Initialize services on background thread - this is the expensive part
        await InitializeServicesAsync();

        // Load store categories on background thread (triggers KEY cache + BIF metadata load)
        // Then update UI on main thread
        var categories = await System.Threading.Tasks.Task.Run(() =>
        {
            if (_gameDataService?.IsConfigured != true)
                return new List<PaletteCategory>();

            return _gameDataService.GetPaletteCategories(Radoub.Formats.Common.ResourceTypes.Utm).ToList();
        });

        // Now populate UI with pre-loaded categories (fast - no I/O)
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            PopulateCategoryDropdownFromList(categories);
        });

        // Start background loading tasks in parallel (fire-and-forget)
        _ = LoadBaseItemTypesAsync();
        StartItemPaletteLoad();

        // Handle command line file
        var options = CommandLineService.Options;
        if (!string.IsNullOrEmpty(options.FilePath) && File.Exists(options.FilePath))
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                LoadFile(options.FilePath);
            });
        }
    }

    private async System.Threading.Tasks.Task InitializeServicesAsync()
    {
        if (_servicesInitialized) return;

        // Run the expensive GameDataService initialization on a background thread
        await System.Threading.Tasks.Task.Run(() =>
        {
            _gameDataService = CreateGameDataService();
            _baseItemTypeService = new BaseItemTypeService(_gameDataService);
            _itemResolutionService = new ItemResolutionService(_gameDataService);
        });

        _servicesInitialized = true;
        UnifiedLogger.LogApplication(LogLevel.INFO, "Fence services initialized");
    }

    private IGameDataService? CreateGameDataService()
    {
        try
        {
            var settings = RadoubSettings.Instance;

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Fence: Checking game paths configuration...");
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  HasGamePaths: {settings.HasGamePaths}");
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  BaseGameInstallPath: {settings.BaseGameInstallPath ?? "(not set)"}");
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  NeverwinterNightsPath: {settings.NeverwinterNightsPath ?? "(not set)"}");

            if (!settings.HasGamePaths)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Fence: Game paths not configured - configure in Settings to enable BIF lookup");
                return null;
            }

            // Use default constructor which reads from RadoubSettings
            var service = new GameDataService();

            if (service.IsConfigured)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "Fence: GameDataService initialized successfully - BIF lookup enabled");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Fence: GameDataService created but not configured - check game path settings");
            }

            return service;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Fence: Failed to create GameDataService: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Populate the store category dropdown from pre-loaded categories.
    /// Categories are loaded on background thread to avoid UI blocking.
    /// </summary>
    private void PopulateCategoryDropdownFromList(List<PaletteCategory> categories)
    {
        StoreCategoryBox.Items.Clear();
        _storeCategories.Clear();

        if (categories.Count > 0)
        {
            // Sort by ID for consistent ordering
            categories.Sort((a, b) => a.Id.CompareTo(b.Id));

            foreach (var category in categories)
            {
                _storeCategories.Add(category);
                StoreCategoryBox.Items.Add(category.Name);
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Populated {categories.Count} store palette categories");
            StoreCategoryBox.SelectedIndex = 0;
            return;
        }

        // Fallback to hardcoded defaults when game data unavailable
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "Using fallback store categories (game data unavailable)");
        var fallbackCategories = new (byte Id, string Name)[]
        {
            (0, "Merchants"),
            (1, "Custom 1"),
            (2, "Custom 2"),
            (3, "Custom 3"),
            (4, "Custom 4"),
            (5, "Custom 5")
        };

        foreach (var (id, name) in fallbackCategories)
        {
            _storeCategories.Add(new PaletteCategory { Id = id, Name = name });
            StoreCategoryBox.Items.Add(name);
        }

        StoreCategoryBox.SelectedIndex = 0;
    }

    private void RestoreWindowPosition()
    {
        var settings = SettingsService.Instance;

        Position = new PixelPoint((int)settings.WindowLeft, (int)settings.WindowTop);
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;

        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        // Unsubscribe to prevent multiple calls
        Loaded -= OnWindowLoaded;

        // Loaded event fires before Opened - just log, heavy work is in OnWindowOpened
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "Window loaded");
    }

    private async System.Threading.Tasks.Task LoadBaseItemTypesAsync()
    {
        if (_baseItemTypeService == null) return;

        // Load base item types on background thread and create view models there too
        var viewModels = await System.Threading.Tasks.Task.Run(() =>
        {
            var types = _baseItemTypeService.GetBaseItemTypes();
            return types.Select(t => new SelectableBaseItemTypeViewModel(t.BaseItemIndex, t.DisplayName)).ToList();
        });

        // Update UI on main thread - batch update to avoid 575 individual collection changes
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            SelectableBaseItemTypes.Clear();
            foreach (var vm in viewModels)
            {
                SelectableBaseItemTypes.Add(vm);
            }
            ItemTypeCheckboxes.ItemsSource = SelectableBaseItemTypes;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {SelectableBaseItemTypes.Count} base item types for buy restrictions");

            // Populate type filter after base items loaded
            PopulateTypeFilter();
        });

        // Don't update status here - let palette loading control the status
        // UpdateStatusBar("Ready") is called when palette loading completes
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
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isDirty)
        {
            // Show non-modal warning - user can still close
            ShowUnsavedChangesWarning();
        }

        SaveWindowPosition();
        SaveStoreBrowserPanelSize();
    }

    private void ShowUnsavedChangesWarning()
    {
        // Non-modal notification
        var dialog = new Window
        {
            Title = "Unsaved Changes",
            Width = 300,
            Height = 120,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "You have unsaved changes.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
                }
            }
        };

        if (dialog.Content is StackPanel panel && panel.Children.LastOrDefault() is Button btn)
        {
            btn.Click += (s, e) => dialog.Close();
        }

        dialog.Show(this);
    }

    #region Store UI Population

    private void PopulateStoreProperties()
    {
        if (_currentStore == null) return;

        StoreNameBox.Text = _currentStore.LocName.GetDefault();
        StoreTagBox.Text = _currentStore.Tag;
        StoreResRefBox.Text = _currentStore.ResRef;

        SellMarkupBox.Text = _currentStore.MarkUp.ToString();
        BuyMarkdownBox.Text = _currentStore.MarkDown.ToString();
        IdentifyPriceBox.Text = _currentStore.IdentifyPrice.ToString();

        BlackMarketCheck.IsChecked = _currentStore.BlackMarket;
        BlackMarketMarkdownBox.Text = _currentStore.BM_MarkDown.ToString();

        MaxBuyPriceCheck.IsChecked = _currentStore.MaxBuyPrice >= 0;
        MaxBuyPriceBox.Text = Math.Max(0, _currentStore.MaxBuyPrice).ToString();

        LimitedGoldCheck.IsChecked = _currentStore.StoreGold >= 0;
        StoreGoldBox.Text = Math.Max(0, _currentStore.StoreGold).ToString();

        // Set category (PaletteID) - find matching category by ID, not index
        var categoryIndex = _storeCategories.FindIndex(c => c.Id == _currentStore.PaletteID);
        StoreCategoryBox.SelectedIndex = categoryIndex >= 0 ? categoryIndex : 0;

        // Scripts and comment
        OnOpenStoreBox.Text = _currentStore.OnOpenStore;
        OnStoreClosedBox.Text = _currentStore.OnStoreClosed;
        CommentBox.Text = _currentStore.Comment;

        // Populate buy restrictions
        PopulateBuyRestrictions();
    }

    private void PopulateBuyRestrictions()
    {
        if (_currentStore == null) return;

        // Clear all selections first
        foreach (var item in SelectableBaseItemTypes)
        {
            item.IsSelected = false;
        }

        // Determine mode and populate
        if (_currentStore.WillNotBuy.Count > 0)
        {
            // WillNotBuy mode (takes precedence per spec)
            WillNotBuyRadio.IsChecked = true;
            foreach (var baseItemIndex in _currentStore.WillNotBuy)
            {
                var item = SelectableBaseItemTypes.FirstOrDefault(t => t.BaseItemIndex == baseItemIndex);
                if (item != null)
                {
                    item.IsSelected = true;
                }
            }
        }
        else if (_currentStore.WillOnlyBuy.Count > 0)
        {
            // WillOnlyBuy mode
            WillOnlyBuyRadio.IsChecked = true;
            foreach (var baseItemIndex in _currentStore.WillOnlyBuy)
            {
                var item = SelectableBaseItemTypes.FirstOrDefault(t => t.BaseItemIndex == baseItemIndex);
                if (item != null)
                {
                    item.IsSelected = true;
                }
            }
        }
        else
        {
            // No restrictions
            BuyAllRadio.IsChecked = true;
        }
    }

    #endregion

    #region UI Updates

    private void UpdateStatusBar(string message)
    {
        StatusText.Text = message;

        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            // Sanitize path for display
            var displayPath = _currentFilePath;
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) && displayPath.StartsWith(userProfile))
            {
                displayPath = "~" + displayPath.Substring(userProfile.Length);
            }
            FilePathText.Text = displayPath;
        }
        else
        {
            FilePathText.Text = "";
        }
    }

    private void UpdateItemCount()
    {
        ItemCountText.Text = $"{StoreItems.Count} items";
    }

    private void UpdateTitle()
    {
        var title = "Fence - Merchant Editor";

        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            title = $"Fence - {Path.GetFileName(_currentFilePath)}";
        }

        if (_isDirty)
        {
            title += " *";
        }

        Title = title;
    }

    #endregion

    #region ResRef Change Tracking

    private void OnStoreItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Subscribe to property changes for new items
        if (e.NewItems != null)
        {
            foreach (StoreItemViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnStoreItemPropertyChanged;
            }
        }

        // Unsubscribe from removed items
        if (e.OldItems != null)
        {
            foreach (StoreItemViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnStoreItemPropertyChanged;
            }
        }
    }

    private void OnStoreItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not StoreItemViewModel item)
            return;

        // Only track ResRef changes (other editable fields like Infinite are tracked elsewhere)
        if (e.PropertyName != nameof(StoreItemViewModel.ResRef))
            return;

        var resRef = item.ResRef;

        // Validate ResRef
        if (!ValidateResRef(resRef, item, out var warning))
        {
            // Show warning in status bar
            UpdateStatusBar(warning);
        }
        else
        {
            UpdateStatusBar("Ready");
        }

        // Mark document dirty
        _isDirty = true;
        UpdateTitle();
    }

    /// <summary>
    /// Validates a ResRef value and returns any warning messages.
    /// </summary>
    /// <param name="resRef">The ResRef to validate</param>
    /// <param name="currentItem">The item being edited (excluded from duplicate check)</param>
    /// <param name="warning">Warning message if validation fails</param>
    /// <returns>True if valid, false if there are issues</returns>
    private bool ValidateResRef(string resRef, StoreItemViewModel currentItem, out string warning)
    {
        warning = string.Empty;

        if (string.IsNullOrWhiteSpace(resRef))
        {
            warning = "Warning: ResRef is empty";
            return false;
        }

        // Check for invalid characters
        if (!ValidResRefPattern.IsMatch(resRef))
        {
            warning = "Warning: ResRef contains non-standard characters (use a-z, 0-9, _)";
            return false;
        }

        // Check for duplicates in store
        var duplicate = StoreItems.FirstOrDefault(i =>
            i != currentItem &&
            string.Equals(i.ResRef, resRef, StringComparison.OrdinalIgnoreCase));

        if (duplicate != null)
        {
            warning = $"Warning: ResRef '{resRef}' already exists in store";
            return false;
        }

        return true;
    }

    #endregion

    #region Settings and About

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.SetMainWindow(this);
        settingsWindow.Show(this); // Non-modal
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var aboutWindow = AboutWindow.Create(new AboutWindowConfig
        {
            ToolName = "Fence",
            Subtitle = "Merchant Editor for Neverwinter Nights",
            Version = VersionHelper.GetVersion(),
            IconBitmap = new Avalonia.Media.Imaging.Bitmap(
                Avalonia.Platform.AssetLoader.Open(
                    new System.Uri("avares://Fence/Assets/fence.ico")))
        });
        aboutWindow.Show(this);
    }

    #endregion

    #region Keyboard Handling

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Only handle Ctrl shortcuts - don't interfere with normal text input
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    OnNewClick(null, e);
                    e.Handled = true;
                    return;
                case Key.O:
                    OnOpenClick(null, e);
                    e.Handled = true;
                    return;
                case Key.S:
                    if (HasFile)
                    {
                        OnSaveClick(null, e);
                        e.Handled = true;
                    }
                    return;
            }
        }
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            switch (e.Key)
            {
                case Key.S:
                    if (HasFile)
                    {
                        OnSaveAsClick(null, e);
                        e.Handled = true;
                    }
                    return;
            }
        }
        // Don't mark as handled for other keys - let them propagate to controls
    }

    #endregion

    #region Helpers

    private void ShowError(string message)
    {
        var dialog = new Window
        {
            Title = "Error",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
                }
            }
        };

        if (dialog.Content is StackPanel panel && panel.Children.LastOrDefault() is Button btn)
        {
            btn.Click += (s, e) => dialog.Close();
        }

        dialog.Show(this); // Non-modal
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

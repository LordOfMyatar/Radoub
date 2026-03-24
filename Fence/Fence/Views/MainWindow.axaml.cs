using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MerchantEditor.Services;
using MerchantEditor.ViewModels;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Search;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.Formats.Utm;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Radoub.UI.Services.Search;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Radoub.UI.Utils;
using Radoub.UI.ViewModels;
using Radoub.UI.Views;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow - Fence Merchant Editor
/// Partial class split into:
/// - MainWindow.axaml.cs (this file): Core initialization, properties, UI helpers
/// - MainWindow.FileOps.cs: File operations (Open/Save/Recent)
/// - MainWindow.ItemPalette.cs: Item palette loading
/// - MainWindow.ItemDetails.cs: Item detail panel operations
/// - MainWindow.StoreOperations.cs: Store inventory and buy restrictions
/// - MainWindow.Variables.cs: Local variable operations (add/edit/remove)
/// - MainWindow.Scripts.cs: Script management
/// - MainWindow.StoreBrowser.cs: Store browser panel (#1144, #1367)
/// - MainWindow.LanguageMenu.cs: TLK language menu (#1362)
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private UtmFile? _currentStore;
    private readonly DocumentState _documentState = new("Fence", " - Merchant Editor");

    // Convenience accessors for document state (used across partial files)
    private string? _currentFilePath
    {
        get => _documentState.CurrentFilePath;
        set => _documentState.CurrentFilePath = value;
    }
    private bool _isDirty
    {
        get => _documentState.IsDirty;
        set { if (value) _documentState.ForceDirty(); else _documentState.ClearDirty(); }
    }

    // Regex for valid ResRef characters (alphanumeric + underscore)
    private static readonly Regex ValidResRefPattern = new(@"^[a-zA-Z0-9_]*$", RegexOptions.Compiled);

    private BaseItemTypeService? _baseItemTypeService;
    private ItemResolutionService? _itemResolutionService;
    private IGameDataService? _gameDataService;
    private TlkService? _tlkService;
    private ItemIconService? _itemIconService;
    private bool _servicesInitialized;

    // Cancellation token for async operations - cancelled on window close
    private CancellationTokenSource? _windowCts;

    // Shared filter panel for item palette
    private ItemFilterPanel? _paletteFilter;

    // Store palette categories loaded from storepal.itp
    // Maps dropdown index to category ID for CEP/custom content support
    private readonly List<PaletteCategory> _storeCategories = new();

    public ObservableCollection<StoreItemViewModel> StoreItems { get; } = new();
    public ObservableCollection<ItemViewModel> PaletteItems { get; } = new();
    public ObservableCollection<SelectableBaseItemTypeViewModel> SelectableBaseItemTypes { get; } = new();

    public bool HasFile => _currentStore != null;
    public bool HasSelection => StoreInventoryGrid?.SelectedItems?.Count > 0;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Find shared filter panel
        _paletteFilter = this.FindControl<ItemFilterPanel>("PaletteFilter");

        // Wire up shared document state for title bar updates
        _documentState.DirtyStateChanged += () => Title = _documentState.GetTitle();

        // Defer heavy I/O (GameDataService, palette loading) to Opened event
        // Only do fast, synchronous UI setup here
        Loaded += OnWindowLoaded;
        Opened += OnWindowOpened;

        // Initialize theme menu state (#1533)
        UpdateUseRadoubThemeMenuState();

        // Restore window position from settings
        RestoreWindowPosition();

        // Wire up event handlers
        Closing += OnWindowClosing;

        // Wire up data grids for double-click
        StoreInventoryGrid.DoubleTapped += OnStoreInventoryDoubleTapped;
        ItemPaletteGrid.DoubleTapped += OnItemPaletteDoubleTapped;

        // Wire up store property change tracking for dirty flag (#1536)
        WireUpStorePropertyTracking();

        // Track property changes on store items for dirty state and validation
        StoreItems.CollectionChanged += OnStoreItemsCollectionChanged;

        // Set up recent files menu
        UpdateRecentFilesMenu();

        // Initialize store browser panel (#1144)
        InitializeStoreBrowserPanel();

        // Show module context in status bar (#1003)
        UpdateModuleIndicator();

        // Initialize search bar with UTM search provider
        var searchBar = this.FindControl<SearchBar>("FileSearchBar");
        searchBar?.Initialize(
            new FileSearchService(new UtmSearchProvider()),
            new (string, SearchFieldCategory)[]
            {
                ("Text", SearchFieldCategory.Content),
                ("Tags", SearchFieldCategory.Identity),
                ("Scripts", SearchFieldCategory.Script),
                ("Metadata", SearchFieldCategory.Metadata),
                ("Variables", SearchFieldCategory.Variable),
            });
        if (searchBar != null)
        {
            searchBar.FileModified += OnSearchFileModified;
            searchBar.NavigateToMatch += OnSearchNavigateToMatch;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, "Fence MainWindow initialized");
    }

    #region Lifecycle

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;

        _windowCts = new CancellationTokenSource();

        UpdateStatusBar("Initializing...");

        // Fire and forget - don't block UI thread
        // Service init and palette loading happen in background
        _ = InitializeAndLoadAsync(_windowCts.Token);
    }

    private async Task InitializeAndLoadAsync(CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();

            // Initialize services on background thread - this is the expensive part
            await InitializeServicesAsync();

            token.ThrowIfCancellationRequested();

            // Load store categories on background thread (triggers KEY cache + BIF metadata load)
            // Then update UI on main thread
            var categories = await Task.Run(() =>
            {
                if (_gameDataService?.IsConfigured != true)
                    return new List<PaletteCategory>();

                return _gameDataService.GetPaletteCategories(Radoub.Formats.Common.ResourceTypes.Utm).ToList();
            }, token);

            // Now populate UI with pre-loaded categories (fast - no I/O)
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                PopulateCategoryDropdownFromList(categories);
                PopulateLanguageMenu();
            });

            token.ThrowIfCancellationRequested();

            // Start background loading tasks in parallel (fire-and-forget)
            _ = LoadBaseItemTypesAsync(token);
            StartItemPaletteLoad(token);

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
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Initialization cancelled (window closing)");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Initialization failed: {ex.Message}");
            UpdateStatusBar("Initialization failed - some features may be unavailable");
        }
    }

    private async Task InitializeServicesAsync()
    {
        if (_servicesInitialized) return;

        // Run the expensive GameDataService initialization on a background thread
        await Task.Run(() =>
        {
            _gameDataService = CreateGameDataService();
            _baseItemTypeService = new BaseItemTypeService(_gameDataService);

            // Create TlkService with settings integration for language-aware string resolution (#1361)
            _tlkService = new TlkService();
            _tlkService.EnableSettingsIntegration();

            _itemResolutionService = new ItemResolutionService(_gameDataService, _tlkService);
            if (_gameDataService != null)
                _itemIconService = new ItemIconService(_gameDataService);
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
                // Configure module-aware HAK scanning for 2DA/resource resolution (#1314)
                var moduleDir = GetModuleWorkingDirectory();
                if (!string.IsNullOrEmpty(moduleDir))
                {
                    service.ConfigureModuleHaks(moduleDir);
                }
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

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        // Unsubscribe to prevent multiple calls
        Loaded -= OnWindowLoaded;

        // Restore panel visibility states
        RestoreItemDetailsPanelState();

        UnifiedLogger.LogApplication(LogLevel.DEBUG, "Window loaded");
    }

    private void RestoreWindowPosition()
    {
        WindowPositionHelper.Restore(this, SettingsService.Instance);
    }

    private void SaveWindowPosition()
    {
        WindowPositionHelper.Save(this, SettingsService.Instance);
    }

    private bool _isClosing;

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Prevent re-entrant close (HandleClosingAsync cancels then re-calls Close())
        if (_isClosing)
            return;

        var shouldClose = await FileOperationsHelper.HandleClosingAsync(
            this, e, _documentState.IsDirty, async () =>
            {
                if (string.IsNullOrEmpty(_currentFilePath))
                {
                    // New unsaved file - trigger SaveAs
                    OnSaveAsClick(null, new RoutedEventArgs());
                    return !_documentState.IsDirty; // true if save succeeded
                }
                await SaveFile(_currentFilePath);
                return true;
            });

        if (shouldClose)
        {
            _isClosing = true;
            _documentState.ClearDirty();
            FileSessionLockService.ReleaseAllLocks();
            SaveWindowPosition();
            SaveStoreBrowserPanelSize();
            SaveItemDetailsPanelSize();

            // Cancel all async operations
            _windowCts?.Cancel();
            _windowCts?.Dispose();

            // Dispose TlkService to unsubscribe from settings events
            _tlkService?.Dispose();

            if (e.Cancel)
            {
                // HandleClosingAsync set Cancel=true, we need to re-close
                Close();
            }
        }
    }

    #endregion

    #region Service Initialization

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

    private async Task LoadBaseItemTypesAsync(CancellationToken token = default)
    {
        if (_baseItemTypeService == null) return;

        try
        {
            // Load base item types on background thread and create view models there too
            var viewModels = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var types = _baseItemTypeService.GetBaseItemTypes();
                return types.Select(t => new SelectableBaseItemTypeViewModel(t.BaseItemIndex, t.DisplayName)).ToList();
            }, token);

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
            });
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Base item type loading cancelled");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load base item types: {ex.Message}");
        }

        // Don't update status here - let palette loading control the status
        // UpdateStatusBar("Ready") is called when palette loading completes
    }

    #endregion

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
        Title = _documentState.GetTitle();
    }

    #endregion

    #region Store Property Change Tracking

    /// <summary>
    /// Subscribe to TextChanged/CheckChanged events on store property controls
    /// so that editing any field marks the document dirty (#1536).
    /// </summary>
    private void WireUpStorePropertyTracking()
    {
        // TextBoxes
        StoreNameBox.TextChanged += OnStorePropertyTextChanged;
        StoreTagBox.TextChanged += OnStorePropertyTextChanged;
        SellMarkupBox.TextChanged += OnStorePropertyTextChanged;
        BuyMarkdownBox.TextChanged += OnStorePropertyTextChanged;
        IdentifyPriceBox.TextChanged += OnStorePropertyTextChanged;
        OnOpenStoreBox.TextChanged += OnStorePropertyTextChanged;
        OnStoreClosedBox.TextChanged += OnStorePropertyTextChanged;
        MaxBuyPriceBox.TextChanged += OnStorePropertyTextChanged;
        StoreGoldBox.TextChanged += OnStorePropertyTextChanged;
        BlackMarketMarkdownBox.TextChanged += OnStorePropertyTextChanged;
        CommentBox.TextChanged += OnStorePropertyTextChanged;

        // CheckBoxes
        BlackMarketCheck.IsCheckedChanged += OnStorePropertyCheckChanged;
        MaxBuyPriceCheck.IsCheckedChanged += OnStorePropertyCheckChanged;
        LimitedGoldCheck.IsCheckedChanged += OnStorePropertyCheckChanged;

        // ComboBox
        StoreCategoryBox.SelectionChanged += OnStorePropertySelectionChanged;
    }

    private void OnStorePropertyTextChanged(object? sender, TextChangedEventArgs e)
    {
        _documentState.MarkDirty();
    }

    private void OnStorePropertyCheckChanged(object? sender, RoutedEventArgs e)
    {
        _documentState.MarkDirty();
    }

    private void OnStorePropertySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _documentState.MarkDirty();
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

        // Mark document dirty (guard against load-time property changes #1743)
        if (!_documentState.IsLoading)
            _documentState.MarkDirty();
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

        // Check length (Aurora Engine limit: 16 characters)
        if (resRef.Length > 16)
        {
            warning = $"Warning: ResRef is too long ({resRef.Length} characters, max 16)";
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

    private void OnEditSettingsFileClick(object? sender, RoutedEventArgs e)
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", "RadoubSettings.json");

        if (!File.Exists(settingsPath))
        {
            UpdateStatusBar("Settings file not found");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(settingsPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to open settings file: {ex.Message}");
            UpdateStatusBar("Could not open settings file");
        }
    }

    private void OnToggleUseRadoubThemeClick(object? sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance;
        settings.UseSharedTheme = !settings.UseSharedTheme;
        UpdateUseRadoubThemeMenuState();
        Radoub.UI.Services.ThemeManager.Instance.ApplyEffectiveTheme(settings.CurrentThemeId, settings.UseSharedTheme);
    }

    private void UpdateUseRadoubThemeMenuState()
    {
        var menuItem = this.FindControl<MenuItem>("UseRadoubThemeMenuItem");
        if (menuItem != null)
            menuItem.Icon = SettingsService.Instance.UseSharedTheme ? new TextBlock { Text = "✓" } : null;
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
        // Handle function keys without modifiers
        if (e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.F3:
                    this.FindControl<SearchBar>("FileSearchBar")?.FindNext();
                    e.Handled = true;
                    return;
                case Key.F4:
                    // Toggle store browser panel (#1144)
                    OnToggleStoreBrowserClick(null, e);
                    e.Handled = true;
                    return;
                case Key.F5:
                    // Toggle item details panel (#1259)
                    OnToggleItemDetailsPanelClick(null, e);
                    e.Handled = true;
                    return;
                case Key.Delete:
                    if (HasSelection)
                    {
                        OnDeleteClick(null, e);
                        e.Handled = true;
                    }
                    return;
            }
        }
        // Handle Ctrl shortcuts - don't interfere with normal text input
        else if (e.KeyModifiers == KeyModifiers.Control)
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
                case Key.F:
                    if (HasFile)
                    {
                        OnFindClick(null, e);
                        e.Handled = true;
                    }
                    return;
                case Key.H:
                    if (HasFile)
                    {
                        OnFindReplaceClick(null, e);
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
        else if (e.KeyModifiers == KeyModifiers.Shift)
        {
            switch (e.Key)
            {
                case Key.F3:
                    this.FindControl<SearchBar>("FileSearchBar")?.FindPrevious();
                    e.Handled = true;
                    return;
            }
        }
        // Don't mark as handled for other keys - let them propagate to controls
    }

    #endregion

    #region Search

    private void OnFindClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<SearchBar>("FileSearchBar")?.Show(_currentFilePath);
    }

    private void OnFindReplaceClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<SearchBar>("FileSearchBar")?.ShowReplace(_currentFilePath);
    }

    private void OnSearchFileModified(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            LoadFile(_currentFilePath);
            UpdateStatusBar("File reloaded after replace.");
        }
    }

    private void OnSearchNavigateToMatch(object? sender, Radoub.Formats.Search.SearchMatch? match)
    {
        if (match == null) { UpdateStatusBar("No matches"); return; }
        var preview = match.FullFieldValue.Length > 60
            ? match.FullFieldValue[..60] + "..."
            : match.FullFieldValue;
        UpdateStatusBar($"Found \"{match.MatchedText}\" in {match.Field.Name}: {preview}");
    }

    #endregion

    #region Helpers

    private void ShowError(string message)
    {
        DialogHelper.ShowError(this, "Error", message);
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Title Bar Handlers

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    #endregion
}

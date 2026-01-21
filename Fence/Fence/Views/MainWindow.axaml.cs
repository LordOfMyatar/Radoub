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
using System.Threading;
using Radoub.UI.Controls;
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
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(HasFile))
            {
                // Update UI bindings
            }
        };

        // Wire up data grids for double-click
        StoreInventoryGrid.DoubleTapped += OnStoreInventoryDoubleTapped;
        ItemPaletteGrid.DoubleTapped += OnItemPaletteDoubleTapped;

        // Set up recent files menu
        UpdateRecentFilesMenu();

        UnifiedLogger.LogApplication(LogLevel.INFO, "Fence MainWindow initialized");
    }

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

    private void LoadBaseItemTypes()
    {
        SelectableBaseItemTypes.Clear();

        var types = _baseItemTypeService.GetBaseItemTypes();
        foreach (var type in types)
        {
            SelectableBaseItemTypes.Add(new SelectableBaseItemTypeViewModel(type.BaseItemIndex, type.DisplayName));
        }

        ItemTypeCheckboxes.ItemsSource = SelectableBaseItemTypes;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {SelectableBaseItemTypes.Count} base item types for buy restrictions");
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

        SellMarkupBox.Value = _currentStore.MarkUp;
        BuyMarkdownBox.Value = _currentStore.MarkDown;
        IdentifyPriceBox.Value = _currentStore.IdentifyPrice;

        BlackMarketCheck.IsChecked = _currentStore.BlackMarket;
        BlackMarketMarkdownBox.Value = _currentStore.BM_MarkDown;

        MaxBuyPriceCheck.IsChecked = _currentStore.MaxBuyPrice >= 0;
        MaxBuyPriceBox.Value = Math.Max(0, _currentStore.MaxBuyPrice);

        LimitedGoldCheck.IsChecked = _currentStore.StoreGold >= 0;
        StoreGoldBox.Value = Math.Max(0, _currentStore.StoreGold);

        // Set category (PaletteID) - find matching category by ID, not index
        var categoryIndex = _storeCategories.FindIndex(c => c.Id == _currentStore.PaletteID);
        StoreCategoryBox.SelectedIndex = categoryIndex >= 0 ? categoryIndex : 0;

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

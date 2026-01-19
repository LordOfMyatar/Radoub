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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

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

    private readonly BaseItemTypeService _baseItemTypeService;
    private readonly ItemResolutionService _itemResolutionService;
    private readonly IGameDataService? _gameDataService;
    private CancellationTokenSource? _paletteLoadCts;
    private const int PaletteBatchSize = 50;

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

        // Initialize game data service and related services
        _gameDataService = CreateGameDataService();
        _baseItemTypeService = new BaseItemTypeService(_gameDataService);
        _itemResolutionService = new ItemResolutionService(_gameDataService);

        // Load base item types for buy restrictions and type filter
        LoadBaseItemTypes();
        PopulateTypeFilter();

        // Populate store category dropdown (toolset palette categories)
        PopulateCategoryDropdown();

        // Start background loading of item palette
        StartItemPaletteLoad();

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

        // Handle command line arguments
        var options = CommandLineService.Options;
        if (!string.IsNullOrEmpty(options.FilePath) && File.Exists(options.FilePath))
        {
            // Defer loading until window is shown
            Opened += async (s, e) =>
            {
                await System.Threading.Tasks.Task.Delay(100);
                LoadFile(options.FilePath);
            };
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, "Fence MainWindow initialized");
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
    /// Populate the store category dropdown with toolset palette categories.
    /// These map to category node IDs in storepal.itp.
    /// </summary>
    private void PopulateCategoryDropdown()
    {
        // Merchant palette categories from storepal.itp
        // Only these categories exist for filing merchants:
        StoreCategoryBox.Items.Clear();
        StoreCategoryBox.Items.Add("Merchants");   // 0 - Default category
        StoreCategoryBox.Items.Add("Custom 1");    // 1
        StoreCategoryBox.Items.Add("Custom 2");    // 2
        StoreCategoryBox.Items.Add("Custom 3");    // 3
        StoreCategoryBox.Items.Add("Custom 4");    // 4
        StoreCategoryBox.Items.Add("Custom 5");    // 5

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

        // Set category (PaletteID) - clamp to valid range for our dropdown
        var categoryIndex = Math.Min(_currentStore.PaletteID, StoreCategoryBox.Items.Count - 1);
        StoreCategoryBox.SelectedIndex = categoryIndex;

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

    private void PopulateStoreInventory()
    {
        StoreItems.Clear();

        if (_currentStore == null) return;

        // Get current markup/markdown for price calculations
        var markUp = (int)(SellMarkupBox.Value ?? 100);
        var markDown = (int)(BuyMarkdownBox.Value ?? 50);

        foreach (var panel in _currentStore.StoreList)
        {
            foreach (var item in panel.Items)
            {
                // Resolve item data from UTI
                var resolved = _itemResolutionService.ResolveItem(item.InventoryRes);

                StoreItems.Add(new StoreItemViewModel
                {
                    ResRef = item.InventoryRes,
                    Tag = resolved?.Tag ?? item.InventoryRes,
                    DisplayName = resolved?.DisplayName ?? item.InventoryRes,
                    Infinite = item.Infinite,
                    PanelId = panel.PanelId,
                    BaseItemType = resolved?.BaseItemTypeName ?? "Unknown",
                    SellPrice = resolved?.CalculateSellPrice(markUp) ?? 0,
                    BuyPrice = resolved?.CalculateBuyPrice(markDown) ?? 0
                });
            }
        }

        StoreInventoryGrid.ItemsSource = StoreItems;
        UpdateItemCount();

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Populated {StoreItems.Count} store items");
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
        settingsWindow.Show(this); // Non-modal
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var aboutWindow = new Window
        {
            Title = "About Fence",
            Width = 350,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Fence", FontSize = 24, FontWeight = Avalonia.Media.FontWeight.Bold },
                    new TextBlock { Text = "Merchant Editor for Neverwinter Nights" },
                    new TextBlock { Text = "Part of the Radoub Toolset" },
                    new TextBlock { Text = "Version 0.9.31-alpha", Margin = new Thickness(0, 12, 0, 0) },
                    new TextBlock { Text = "© 2025 LNS Development" }
                }
            }
        };

        aboutWindow.Show(this); // Non-modal
    }

    #endregion

    #region Keyboard Handling

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Keyboard shortcuts are handled via InputGestures in menu items
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

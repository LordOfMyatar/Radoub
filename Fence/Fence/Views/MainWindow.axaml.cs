using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

namespace MerchantEditor.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private UtmFile? _currentStore;
    private string? _currentFilePath;
    private bool _isDirty;

    private readonly BaseItemTypeService _baseItemTypeService;
    private readonly IGameDataService? _gameDataService;

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

        // Initialize game data service and base item type service
        _gameDataService = CreateGameDataService();
        _baseItemTypeService = new BaseItemTypeService(_gameDataService);

        // Load base item types for buy restrictions
        LoadBaseItemTypes();

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
            if (!settings.HasGamePaths)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Game paths not configured");
                return null;
            }

            // Use default constructor which reads from RadoubSettings
            return new GameDataService();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to create GameDataService: {ex.Message}");
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

    #region File Operations

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Store",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Store Files") { Patterns = new[] { "*.utm" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            LoadFile(path);
        }
    }

    private void LoadFile(string filePath)
    {
        try
        {
            _currentStore = UtmReader.Read(filePath);
            _currentFilePath = filePath;
            _isDirty = false;

            // Update UI
            PopulateStoreProperties();
            PopulateStoreInventory();
            UpdateStatusBar($"Loaded: {Path.GetFileName(filePath)}");
            UpdateTitle();

            // Add to recent files
            SettingsService.Instance.AddRecentFile(filePath);
            UpdateRecentFilesMenu();

            OnPropertyChanged(nameof(HasFile));

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded store: {UnifiedLogger.SanitizePath(filePath)}");
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load file: {ex.Message}");
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load store: {ex.Message}");
        }
    }

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

        foreach (var panel in _currentStore.StoreList)
        {
            foreach (var item in panel.Items)
            {
                StoreItems.Add(new StoreItemViewModel
                {
                    ResRef = item.InventoryRes,
                    DisplayName = item.InventoryRes, // TODO: resolve from UTI
                    Infinite = item.Infinite,
                    PanelId = panel.PanelId,
                    BaseItemType = "Unknown", // TODO: resolve from UTI
                    SellPrice = 0, // TODO: calculate
                    BuyPrice = 0   // TODO: calculate
                });
            }
        }

        StoreInventoryGrid.ItemsSource = StoreItems;
        UpdateItemCount();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStore == null || string.IsNullOrEmpty(_currentFilePath))
            return;

        await SaveFile(_currentFilePath);
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStore == null)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Store As",
            DefaultExtension = "utm",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Store Files") { Patterns = new[] { "*.utm" } }
            },
            SuggestedFileName = _currentStore.ResRef
        });

        if (file != null)
        {
            await SaveFile(file.Path.LocalPath);
        }
    }

    private async System.Threading.Tasks.Task SaveFile(string filePath)
    {
        if (_currentStore == null)
            return;

        try
        {
            // Update store from UI
            UpdateStoreFromUI();

            UtmWriter.Write(_currentStore, filePath);
            _currentFilePath = filePath;
            _isDirty = false;

            UpdateStatusBar($"Saved: {Path.GetFileName(filePath)}");
            UpdateTitle();

            // Add to recent files
            SettingsService.Instance.AddRecentFile(filePath);
            UpdateRecentFilesMenu();

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Saved store: {UnifiedLogger.SanitizePath(filePath)}");
        }
        catch (Exception ex)
        {
            ShowError($"Failed to save file: {ex.Message}");
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save store: {ex.Message}");
        }
    }

    private void UpdateStoreFromUI()
    {
        if (_currentStore == null) return;

        _currentStore.LocName.SetString(0, StoreNameBox.Text ?? ""); // 0 = English male
        _currentStore.Tag = StoreTagBox.Text ?? "";

        _currentStore.MarkUp = (int)(SellMarkupBox.Value ?? 100);
        _currentStore.MarkDown = (int)(BuyMarkdownBox.Value ?? 50);
        _currentStore.IdentifyPrice = (int)(IdentifyPriceBox.Value ?? 100);

        _currentStore.BlackMarket = BlackMarketCheck.IsChecked ?? false;
        _currentStore.BM_MarkDown = (int)(BlackMarketMarkdownBox.Value ?? 25);

        _currentStore.MaxBuyPrice = (MaxBuyPriceCheck.IsChecked ?? false) ? (int)(MaxBuyPriceBox.Value ?? 0) : -1;
        _currentStore.StoreGold = (LimitedGoldCheck.IsChecked ?? false) ? (int)(StoreGoldBox.Value ?? 0) : -1;

        // Update buy restrictions
        UpdateBuyRestrictions();

        // Update inventory from view model
        _currentStore.StoreList.Clear();
        var groupedItems = StoreItems.GroupBy(i => i.PanelId);
        foreach (var group in groupedItems)
        {
            var panel = new StorePanel { PanelId = group.Key };
            foreach (var item in group)
            {
                panel.Items.Add(new StoreItem
                {
                    InventoryRes = item.ResRef,
                    Infinite = item.Infinite
                });
            }
            _currentStore.StoreList.Add(panel);
        }
    }

    private void UpdateBuyRestrictions()
    {
        if (_currentStore == null) return;

        _currentStore.WillOnlyBuy.Clear();
        _currentStore.WillNotBuy.Clear();

        if (BuyAllRadio.IsChecked == true)
        {
            // No restrictions - both lists empty
        }
        else if (WillOnlyBuyRadio.IsChecked == true)
        {
            // Store selected items in WillOnlyBuy
            foreach (var item in SelectableBaseItemTypes.Where(t => t.IsSelected))
            {
                _currentStore.WillOnlyBuy.Add(item.BaseItemIndex);
            }
        }
        else if (WillNotBuyRadio.IsChecked == true)
        {
            // Store selected items in WillNotBuy
            foreach (var item in SelectableBaseItemTypes.Where(t => t.IsSelected))
            {
                _currentStore.WillNotBuy.Add(item.BaseItemIndex);
            }
        }
    }

    private void OnCloseFileClick(object? sender, RoutedEventArgs e)
    {
        _currentStore = null;
        _currentFilePath = null;
        _isDirty = false;

        StoreItems.Clear();
        ClearStoreProperties();
        UpdateStatusBar("Ready");
        UpdateTitle();

        OnPropertyChanged(nameof(HasFile));
    }

    private void ClearStoreProperties()
    {
        StoreNameBox.Text = "";
        StoreTagBox.Text = "";
        StoreResRefBox.Text = "";
        SellMarkupBox.Value = 100;
        BuyMarkdownBox.Value = 50;
        IdentifyPriceBox.Value = 100;
        BlackMarketCheck.IsChecked = false;
        BlackMarketMarkdownBox.Value = 25;
        MaxBuyPriceCheck.IsChecked = false;
        MaxBuyPriceBox.Value = 0;
        LimitedGoldCheck.IsChecked = false;
        StoreGoldBox.Value = 0;

        // Clear buy restrictions
        BuyAllRadio.IsChecked = true;
        foreach (var item in SelectableBaseItemTypes)
        {
            item.IsSelected = false;
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Recent Files

    private void UpdateRecentFilesMenu()
    {
        RecentFilesMenu.Items.Clear();

        var recentFiles = SettingsService.Instance.RecentFiles;

        if (recentFiles.Count == 0)
        {
            var emptyItem = new MenuItem { Header = "(No recent files)", IsEnabled = false };
            RecentFilesMenu.Items.Add(emptyItem);
            return;
        }

        foreach (var filePath in recentFiles)
        {
            var item = new MenuItem
            {
                Header = Path.GetFileName(filePath),
                Tag = filePath
            };
            item.Click += OnRecentFileClick;
            RecentFilesMenu.Items.Add(item);
        }

        RecentFilesMenu.Items.Add(new Separator());

        var clearItem = new MenuItem { Header = "Clear Recent Files" };
        clearItem.Click += (s, e) =>
        {
            SettingsService.Instance.ClearRecentFiles();
            UpdateRecentFilesMenu();
        };
        RecentFilesMenu.Items.Add(clearItem);
    }

    private void OnRecentFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string filePath)
        {
            if (File.Exists(filePath))
            {
                LoadFile(filePath);
            }
            else
            {
                ShowError($"File not found: {filePath}");
                SettingsService.Instance.RemoveRecentFile(filePath);
                UpdateRecentFilesMenu();
            }
        }
    }

    #endregion

    #region Store Inventory Operations

    private void OnRemoveFromStore(object? sender, RoutedEventArgs e)
    {
        var selectedItems = StoreInventoryGrid.SelectedItems?.Cast<StoreItemViewModel>().ToList();
        if (selectedItems == null || selectedItems.Count == 0)
            return;

        foreach (var item in selectedItems)
        {
            StoreItems.Remove(item);
        }

        _isDirty = true;
        UpdateTitle();
        UpdateItemCount();
    }

    private void OnSetInfinite(object? sender, RoutedEventArgs e)
    {
        SetInfiniteFlag(true);
    }

    private void OnClearInfinite(object? sender, RoutedEventArgs e)
    {
        SetInfiniteFlag(false);
    }

    private void SetInfiniteFlag(bool value)
    {
        var selectedItems = StoreInventoryGrid.SelectedItems?.Cast<StoreItemViewModel>().ToList();
        if (selectedItems == null || selectedItems.Count == 0)
            return;

        foreach (var item in selectedItems)
        {
            item.Infinite = value;
        }

        _isDirty = true;
        UpdateTitle();
        StoreInventoryGrid.ItemsSource = null;
        StoreInventoryGrid.ItemsSource = StoreItems;
    }

    private void OnAddToStore(object? sender, RoutedEventArgs e)
    {
        AddSelectedPaletteItems();
    }

    private void AddSelectedPaletteItems()
    {
        var selectedItems = ItemPaletteGrid.SelectedItems?.Cast<PaletteItemViewModel>().ToList();
        if (selectedItems == null || selectedItems.Count == 0)
            return;

        foreach (var item in selectedItems)
        {
            StoreItems.Add(new StoreItemViewModel
            {
                ResRef = item.ResRef,
                DisplayName = item.DisplayName,
                Infinite = false,
                PanelId = StorePanels.Miscellaneous, // Default panel
                BaseItemType = item.BaseItemType,
                SellPrice = 0,
                BuyPrice = 0
            });
        }

        _isDirty = true;
        UpdateTitle();
        UpdateItemCount();
    }

    private void OnStoreInventoryDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Double-click removes from store
        var selectedItems = StoreInventoryGrid.SelectedItems?.Cast<StoreItemViewModel>().ToList();
        if (selectedItems == null || selectedItems.Count == 0)
            return;

        foreach (var item in selectedItems)
        {
            StoreItems.Remove(item);
        }

        _isDirty = true;
        UpdateTitle();
        UpdateItemCount();
    }

    private void OnItemPaletteDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Double-click adds to store
        AddSelectedPaletteItems();
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        OnRemoveFromStore(sender, e);
    }

    #endregion

    #region Buy Restrictions

    private void OnBuyModeChanged(object? sender, RoutedEventArgs e)
    {
        // When mode changes to "Buy All", clear selections
        if (BuyAllRadio.IsChecked == true)
        {
            foreach (var item in SelectableBaseItemTypes)
            {
                item.IsSelected = false;
            }
        }

        _isDirty = true;
        UpdateTitle();
    }

    private void OnSelectAllTypes(object? sender, RoutedEventArgs e)
    {
        foreach (var item in SelectableBaseItemTypes)
        {
            item.IsSelected = true;
        }

        _isDirty = true;
        UpdateTitle();
    }

    private void OnClearAllTypes(object? sender, RoutedEventArgs e)
    {
        foreach (var item in SelectableBaseItemTypes)
        {
            item.IsSelected = false;
        }

        _isDirty = true;
        UpdateTitle();
    }

    private void OnItemTypeCheckChanged(object? sender, RoutedEventArgs e)
    {
        _isDirty = true;
        UpdateTitle();
    }

    #endregion

    #region Search

    private void OnClearStoreSearch(object? sender, RoutedEventArgs e)
    {
        StoreSearchBox.Text = "";
        // TODO: Clear filter
    }

    private void OnClearPaletteSearch(object? sender, RoutedEventArgs e)
    {
        PaletteSearchBox.Text = "";
        // TODO: Clear filter
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

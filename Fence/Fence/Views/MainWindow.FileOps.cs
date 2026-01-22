using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MerchantEditor.Services;
using MerchantEditor.ViewModels;
using Radoub.Formats.Logging;
using Radoub.Formats.Utm;
using System;
using System.IO;
using System.Linq;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: File operations (Open, Save, SaveAs, Close, Recent Files)
/// </summary>
public partial class MainWindow
{
    #region File Operations

    private void OnNewClick(object? sender, RoutedEventArgs e)
    {
        // Create a new empty store
        _currentStore = new UtmFile
        {
            ResRef = "new_store",
            Tag = "new_store",
            MarkUp = 100,
            MarkDown = 50,
            IdentifyPrice = 100,
            StoreGold = -1,
            MaxBuyPrice = -1,
            BlackMarket = false,
            BM_MarkDown = 25
        };
        _currentStore.LocName.SetString(0, "New Store");

        _currentFilePath = null;
        _isDirty = true;

        // Clear item resolution context (no file yet)
        _itemResolutionService?.SetCurrentFilePath(null);

        // Update UI
        PopulateStoreProperties();
        StoreItems.Clear();
        Variables.Clear();
        UpdateStatusBar("New store created");
        UpdateTitle();
        UpdateItemCount();

        OnPropertyChanged(nameof(HasFile));

        UnifiedLogger.LogApplication(LogLevel.INFO, "Created new store");
    }

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

            // Update item resolution service with current file context for module-local items
            _itemResolutionService?.SetCurrentFilePath(filePath);

            // Update UI - properties and variables are fast
            PopulateStoreProperties();
            PopulateVariables();
            UpdateStatusBar($"Loading items...");
            UpdateTitle();

            // Add to recent files
            SettingsService.Instance.AddRecentFile(filePath);
            UpdateRecentFilesMenu();

            OnPropertyChanged(nameof(HasFile));

            // Load inventory async to avoid blocking UI during item resolution
            _ = PopulateStoreInventoryAsync(filePath);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load file: {ex.Message}");
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load store: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task PopulateStoreInventoryAsync(string filePath)
    {
        if (_currentStore == null) return;

        var markUp = 100;
        var markDown = 50;

        // Get markup values from UI thread
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            markUp = (int)(SellMarkupBox.Value ?? 100);
            markDown = (int)(BuyMarkdownBox.Value ?? 50);
        });

        // Collect all items to resolve
        var itemsToResolve = _currentStore.StoreList
            .SelectMany(panel => panel.Items.Select(item => new { item, panel.PanelId }))
            .ToList();

        // Resolve items on background thread
        var resolvedItems = await System.Threading.Tasks.Task.Run(() =>
        {
            var results = new System.Collections.Generic.List<StoreItemViewModel>();

            foreach (var entry in itemsToResolve)
            {
                var resolved = _itemResolutionService?.ResolveItem(entry.item.InventoryRes);

                results.Add(new StoreItemViewModel
                {
                    ResRef = entry.item.InventoryRes,
                    Tag = resolved?.Tag ?? entry.item.InventoryRes,
                    DisplayName = resolved?.DisplayName ?? entry.item.InventoryRes,
                    Infinite = entry.item.Infinite,
                    PanelId = entry.PanelId,
                    BaseItemType = resolved?.BaseItemTypeName ?? "Unknown",
                    SellPrice = resolved?.CalculateSellPrice(markUp) ?? 0,
                    BuyPrice = resolved?.CalculateBuyPrice(markDown) ?? 0
                });
            }

            return results;
        });

        // Update UI on main thread
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            StoreItems.Clear();
            foreach (var item in resolvedItems)
            {
                StoreItems.Add(item);
            }
            StoreInventoryGrid.ItemsSource = StoreItems;
            UpdateItemCount();
            UpdateStatusBar($"Loaded: {Path.GetFileName(filePath)}");
        });

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded store: {UnifiedLogger.SanitizePath(filePath)} ({resolvedItems.Count} items)");
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStore == null)
            return;

        // If no file path yet (new file), redirect to Save As
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            OnSaveAsClick(sender, e);
            return;
        }

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

            // Set ResRef from filename (Aurora Engine convention)
            var resRef = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            _currentStore.ResRef = resRef;

            // Write file on background thread to keep UI responsive
            var store = _currentStore;
            await System.Threading.Tasks.Task.Run(() => UtmWriter.Write(store, filePath));

            _currentFilePath = filePath;
            _isDirty = false;

            // Update UI to show new ResRef
            StoreResRefBox.Text = resRef;

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

        // Update category (PaletteID) - use category ID from mapping, not dropdown index
        var selectedCategoryIndex = Math.Max(0, StoreCategoryBox.SelectedIndex);
        _currentStore.PaletteID = selectedCategoryIndex < _storeCategories.Count
            ? _storeCategories[selectedCategoryIndex].Id
            : (byte)0;

        // Update scripts and comment
        _currentStore.OnOpenStore = OnOpenStoreBox.Text ?? "";
        _currentStore.OnStoreClosed = OnStoreClosedBox.Text ?? "";
        _currentStore.Comment = CommentBox.Text ?? "";

        // Update buy restrictions
        UpdateBuyRestrictions();

        // Update local variables from view model
        UpdateVarTable();

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

    private void OnCloseFileClick(object? sender, RoutedEventArgs e)
    {
        _currentStore = null;
        _currentFilePath = null;
        _isDirty = false;

        // Clear item resolution context
        _itemResolutionService?.SetCurrentFilePath(null);

        StoreItems.Clear();
        Variables.Clear();
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
        // Populate menu immediately with current list
        PopulateRecentFilesMenuItems();

        // Background validation disabled - was causing crashes
        // _ = ValidateAndRefreshRecentFilesAsync();
    }

    private void PopulateRecentFilesMenuItems()
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

    private async System.Threading.Tasks.Task ValidateAndRefreshRecentFilesAsync()
    {
        var countBefore = SettingsService.Instance.RecentFiles.Count;
        await SettingsService.Instance.ValidateRecentFilesAsync();
        var countAfter = SettingsService.Instance.RecentFiles.Count;

        // Refresh menu if files were removed - must be on UI thread
        if (countAfter < countBefore)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                PopulateRecentFilesMenuItems();
            });
        }
    }

    private async void OnRecentFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string filePath)
        {
            // Check file existence on background thread to avoid blocking on network paths
            var exists = await System.Threading.Tasks.Task.Run(() => File.Exists(filePath));

            if (exists)
            {
                LoadFile(filePath);
            }
            else
            {
                ShowError($"File not found: {filePath}");
                SettingsService.Instance.RemoveRecentFile(filePath);
                PopulateRecentFilesMenuItems();
            }
        }
    }

    #endregion
}

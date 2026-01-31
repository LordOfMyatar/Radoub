using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MerchantEditor.Services;
using MerchantEditor.ViewModels;
using Radoub.Formats.Logging;
using Radoub.Formats.Utm;
using Radoub.UI.Views;
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
        // Use custom StoreBrowserWindow for consistent UX (#1084)
        var context = new FenceScriptBrowserContext(_currentFilePath, _gameDataService);
        var browser = new StoreBrowserWindow(context);
        await browser.ShowDialog(this);

        // Check if user selected a store
        var selectedEntry = browser.SelectedEntry;
        if (selectedEntry?.FilePath != null)
        {
            LoadFile(selectedEntry.FilePath);
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

            // Update store browser panel (#1144)
            UpdateStoreBrowserCurrentFile(filePath);

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
            markUp = int.TryParse(SellMarkupBox.Text, out var mu) ? mu : 100;
            markDown = int.TryParse(BuyMarkdownBox.Text, out var md) ? md : 50;
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
                    BaseValue = resolved?.BaseCost ?? 0,
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

    /// <summary>
    /// Public async method for auto-save from store browser panel (#1144).
    /// </summary>
    private System.Threading.Tasks.Task SaveFileAsync(string filePath) => SaveFile(filePath);

    private void UpdateStoreFromUI()
    {
        if (_currentStore == null) return;

        _currentStore.LocName.SetString(0, StoreNameBox.Text ?? ""); // 0 = English male
        _currentStore.Tag = StoreTagBox.Text ?? "";

        _currentStore.MarkUp = int.TryParse(SellMarkupBox.Text, out var markUp) ? markUp : 100;
        _currentStore.MarkDown = int.TryParse(BuyMarkdownBox.Text, out var markDown) ? markDown : 50;
        _currentStore.IdentifyPrice = int.TryParse(IdentifyPriceBox.Text, out var identifyPrice) ? identifyPrice : 100;

        _currentStore.BlackMarket = BlackMarketCheck.IsChecked ?? false;
        _currentStore.BM_MarkDown = int.TryParse(BlackMarketMarkdownBox.Text, out var bmMarkDown) ? bmMarkDown : 25;

        _currentStore.MaxBuyPrice = (MaxBuyPriceCheck.IsChecked ?? false) && int.TryParse(MaxBuyPriceBox.Text, out var maxBuy) ? maxBuy : -1;
        _currentStore.StoreGold = (LimitedGoldCheck.IsChecked ?? false) && int.TryParse(StoreGoldBox.Text, out var storeGold) ? storeGold : -1;

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
        SellMarkupBox.Text = "100";
        BuyMarkdownBox.Text = "50";
        IdentifyPriceBox.Text = "100";
        BlackMarketCheck.IsChecked = false;
        BlackMarketMarkdownBox.Text = "25";
        MaxBuyPriceCheck.IsChecked = false;
        MaxBuyPriceBox.Text = "0";
        LimitedGoldCheck.IsChecked = false;
        StoreGoldBox.Text = "0";

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
        PopulateRecentFilesMenuItems();
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

    #region Rename File

    /// <summary>
    /// Handles rename button click.
    /// </summary>
    private async void OnRenameResRefClick(object? sender, RoutedEventArgs e)
    {
        await RenameCurrentFileAsync();
    }

    /// <summary>
    /// Renames the current file using a safe save-rename-reload workflow.
    /// </summary>
    private async System.Threading.Tasks.Task RenameCurrentFileAsync()
    {
        if (_currentStore == null || string.IsNullOrEmpty(_currentFilePath))
        {
            UpdateStatusBar("No file loaded to rename");
            return;
        }

        var directory = Path.GetDirectoryName(_currentFilePath) ?? "";
        var currentName = Path.GetFileNameWithoutExtension(_currentFilePath);
        var extension = Path.GetExtension(_currentFilePath);

        // Show rename dialog
        var newName = await RenameDialog.ShowAsync(this, currentName, directory, extension);
        if (string.IsNullOrEmpty(newName))
        {
            return; // User cancelled
        }

        // Check if file has unsaved changes
        if (_isDirty)
        {
            var confirmed = await ShowConfirmationDialogAsync("Save Changes",
                "The file has unsaved changes. Save before renaming?");

            if (!confirmed)
            {
                return; // User cancelled
            }

            // Save current file
            await SaveFile(_currentFilePath);
        }

        var newFilePath = Path.Combine(directory, newName + extension);

        try
        {
            // Rename file on disk
            File.Move(_currentFilePath, newFilePath);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Renamed file: {UnifiedLogger.SanitizePath(_currentFilePath)} -> {UnifiedLogger.SanitizePath(newFilePath)}");

            // Update internal ResRef to match new filename
            _currentStore.ResRef = newName;

            // Save file to persist the new ResRef
            _currentFilePath = newFilePath;
            await SaveFile(_currentFilePath);

            // Update UI
            StoreResRefBox.Text = newName;
            UpdateTitle();
            UpdateRecentFilesMenu();

            UpdateStatusBar($"Renamed to: {newName}{extension}");
        }
        catch (IOException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to rename file: {ex.Message}");
            ShowError($"Could not rename file:\n\n{ex.Message}");
        }
    }

    /// <summary>
    /// Shows a confirmation dialog and returns true if user confirms.
    /// </summary>
    private async System.Threading.Tasks.Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        var confirmed = false;
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var okButton = new Button { Content = "OK", Width = 80 };
        var cancelButton = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(10, 0, 0, 0) };

        okButton.Click += (s, e) => { confirmed = true; dialog.Close(); };
        cancelButton.Click += (s, e) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 15,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Children = { okButton, cancelButton }
                }
            }
        };

        await dialog.ShowDialog(this);
        return confirmed;
    }

    #endregion
}

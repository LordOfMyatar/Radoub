using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MerchantEditor.Services;
using MerchantEditor.ViewModels;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.Formats.Utm;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Radoub.UI.Utils;
using Radoub.UI.Views;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: File operations (Open, Save, SaveAs, Close, Recent Files)
/// </summary>
public partial class MainWindow
{
    #region File Operations

    private async void OnNewClick(object? sender, RoutedEventArgs e)
    {
        // Prompt for Name/Tag/ResRef up front (#2418). Cancel leaves the current document
        // untouched — return before mutating any state.
        var dialog = new NewStoreWindow();
        await dialog.ShowDialog(this);
        if (dialog.Result is not { } result)
            return;

        // Release lock on previous file
        if (!string.IsNullOrEmpty(_currentFilePath))
            FileSessionLockService.ReleaseLock(_currentFilePath);
        _documentState.IsReadOnly = false;

        // Create the new store from the dialog values
        _currentStore = new UtmFile
        {
            ResRef = result.ResRef,
            Tag = result.Tag,
            MarkUp = 100,
            MarkDown = 50,
            IdentifyPrice = 100,
            StoreGold = -1,
            MaxBuyPrice = -1,
            BlackMarket = false,
            BM_MarkDown = 25
        };
        _currentStore.LocName.SetString(0, result.Name);

        _currentFilePath = null;
        _documentState.IsLoading = true;

        // Clear item resolution context (no file yet)
        _itemResolutionService?.SetCurrentFilePath(null);

        // Deselect the previously-loaded file's row so the browser panel state matches
        // the new unsaved in-memory document (no on-disk file selected) (#2307).
        UpdateStoreBrowserCurrentFile(null);

        // Update UI
        PopulateStoreProperties();
        StoreItems.Clear();
        Variables.Clear();
        UpdateStatusBar("New store created");
        UpdateItemCount();

        OnPropertyChanged(nameof(HasFile));

        // Fresh undo history per document (#2255).
        _undo.Clear();

        // End loading state, then mark dirty — new files are always unsaved
        _documentState.IsLoading = false;
        _documentState.ForceDirty();

        UnifiedLogger.LogApplication(LogLevel.INFO, "Created new store");

        // Save immediately so the file exists on disk, then drop to it as the open file and
        // surface + select it in the F4 browser (#2418). SaveFile sets _currentFilePath and
        // fires the browser refresh/select via NotifyOrAddAsync. If the user cancels the save
        // picker, fall back to the unsaved in-memory buffer (they can Save later).
        await SaveAsAsync();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening file: {ex.Message}");
            ShowError($"Failed to open file: {ex.Message}");
        }
    }

    private void LoadFile(string filePath)
    {
        try
        {
            // Release lock on previous file if any
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                FileSessionLockService.ReleaseLock(_currentFilePath);
                _documentState.IsReadOnly = false;
            }

            // Check for file lock from another tool instance
            var lockResult = FileSessionLockService.AcquireLock(filePath, "Fence");
            if (lockResult == LockResult.LockedByOther)
            {
                var lockInfo = FileSessionLockService.CheckLock(filePath);
                var toolName = lockInfo?.ToolName ?? "another tool";
                UnifiedLogger.LogApplication(LogLevel.WARN, $"File locked by {toolName} — opening read-only: {UnifiedLogger.SanitizePath(filePath)}");
                UpdateStatusBar($"File is open in {toolName} — opening read-only");
                _documentState.IsReadOnly = true;
            }

            // Set IsLoading before the read so any subscriber reacting to the
            // _currentStore change is already guarded against treating the load as a
            // user edit (#2256).
            _documentState.IsLoading = true;
            _currentStore = UtmReader.Read(filePath);
            _currentFilePath = filePath;

            // Infer module path from file location (#1208)
            RadoubSettings.Instance.TryInferModuleFromFile(filePath);

            // Update item resolution service with current file context for module-local items
            _itemResolutionService?.SetCurrentFilePath(filePath);

            // Update UI - properties and variables are fast
            PopulateStoreProperties();
            PopulateVariables();
            UpdateStatusBar($"Loading items...");

            // Add to recent files
            SettingsService.Instance.AddRecentFile(filePath);
            UpdateRecentFilesMenu();

            OnPropertyChanged(nameof(HasFile));

            // Update store browser panel (#1144)
            UpdateStoreBrowserCurrentFile(filePath);

            // Update search bar file path
            this.FindControl<SearchBar>("FileSearchBar")?.UpdateFilePath(filePath);

            // Scan module directory for loose .uti files
            PopulateModuleItems();

            // Load inventory async to avoid blocking UI during item resolution.
            // Keep IsLoading true until inventory is fully populated to prevent
            // collection change events from marking the document dirty (#1743).
            // Always clear IsLoading — even if population faults — so the editor
            // can't get permanently stuck in the loading state (#2256).
            _ = LoadInventoryAndFinalizeAsync(filePath);
        }
        catch (Exception ex)
        {
            _documentState.IsLoading = false;
            ShowError($"Failed to load file: {ex.Message}");
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load store: {ex.Message}");
        }
    }

    /// <summary>
    /// Populates the store inventory and finalizes the load state. IsLoading is cleared
    /// in a finally block so a fault during population can't leave the editor stuck
    /// loading (no dirty events, ClearDirty never runs) (#2256).
    /// </summary>
    private async Task LoadInventoryAndFinalizeAsync(string filePath)
    {
        try
        {
            await PopulateStoreInventoryAsync(filePath);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Failed to populate store inventory: {ex.Message}");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                UpdateStatusBar("Loaded with errors — some inventory items may be missing."));
        }
        finally
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Fresh undo history per document (#2255).
                _undo.Clear();
                _documentState.IsLoading = false;
                _documentState.ClearDirty();
                UpdateTitle();
            });
        }
    }

    private async Task PopulateStoreInventoryAsync(string filePath)
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
        var resolvedItems = await Task.Run(() =>
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
                    BaseItemIndex = resolved?.BaseItemType ?? 0,
                    BaseValue = resolved?.BaseCost ?? 0,
                    SellPrice = resolved?.CalculateSellPrice(markUp) ?? 0,
                    BuyPrice = resolved?.CalculateBuyPrice(markDown) ?? 0,
                    SourceLocation = resolved?.SourceLocation ?? string.Empty
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

            // Populate icons on UI thread
            if (_itemIconService != null)
            {
                foreach (var item in StoreItems)
                {
                    item.IconBitmap = _itemIconService.GetItemIcon(item.BaseItemIndex);
                }
            }
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
            await SaveAsAsync();
            return;
        }

        await SaveFile(_currentFilePath);
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        await SaveAsAsync();
    }

    /// <summary>
    /// Show the Save As picker and save to the chosen path. Returns true if the file
    /// was written, false if the user cancelled or no store is loaded. Awaitable so the
    /// close path can wait for the save to finish before deciding to close (#2255) —
    /// the old fire-and-forget OnSaveAsClick let the close decision race the picker.
    /// </summary>
    private async Task<bool> SaveAsAsync()
    {
        if (_currentStore == null)
            return false;

        // Default to current module directory if available
        IStorageFolder? suggestedFolder = null;
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            var directory = Path.GetDirectoryName(_currentFilePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                suggestedFolder = await StorageProvider.TryGetFolderFromPathAsync(directory);
            }
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Store As",
            DefaultExtension = "utm",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Store Files") { Patterns = new[] { "*.utm" } }
            },
            SuggestedFileName = _currentStore.ResRef,
            SuggestedStartLocation = suggestedFolder
        });

        if (file == null)
            return false;

        await SaveFile(file.Path.LocalPath);
        return true;
    }

    private async Task SaveFile(string filePath)
    {
        if (_currentStore == null)
            return;

        if (_documentState.IsReadOnly)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Save blocked: file is read-only (locked by another tool): {UnifiedLogger.SanitizePath(filePath)}");
            UpdateStatusBar("Cannot save: file is open read-only (locked by another tool).");
            return;
        }

        // Block save if duplicate variable names exist
        var varError = ValidateVariablesForSave();
        if (varError != null)
        {
            UpdateStatusBar(varError);
            return;
        }

        try
        {
            // Update store from UI
            UpdateStoreFromUI();

            // Set ResRef from filename (Aurora Engine convention)
            var resRef = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            _currentStore.ResRef = resRef;

            // Write to a temp file beside the destination (same volume = atomic move),
            // then swap it into place with the shared cross-OS atomic helper. The helper
            // backs up the previous file to .bak and performs a single File.Move
            // (overwrite:true) so the original is never missing mid-save (#2256).
            var tempPath = filePath + ".tmp";
            var store = _currentStore;
            try
            {
                await Task.Run(() => UtmWriter.Write(store, tempPath));

                Radoub.Formats.Common.AtomicFile.Replace(tempPath, filePath, filePath + ".bak");
            }
            finally
            {
                // Clean up temp file if it still exists (write failed before the move)
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch { /* temp cleanup is best-effort */ }
                }
            }

            _currentFilePath = filePath;
            _isDirty = false;

            // Update UI to show new ResRef
            StoreResRefBox.Text = resRef;

            UpdateStatusBar($"Saved: {Path.GetFileName(filePath)}");
            UpdateTitle();

            // Add to recent files
            SettingsService.Instance.AddRecentFile(filePath);
            UpdateRecentFilesMenu();

            // Refresh the browser after save (#2200). NotifyOrAddAsync (#2413) refreshes an
            // existing row in place, OR — for a brand-new file with no row yet (New Store, Save As
            // to a new name) — reloads the list so the new row appears and selects it (#2418).
            // Fire-and-forget — save flow does not block on UI refresh.
            var storeBrowserPanel = this.FindControl<StoreBrowserPanel>("StoreBrowserPanel");
            _ = Radoub.UI.Controls.BrowserSaveNotifier.NotifyOrAddAsync(storeBrowserPanel, filePath);

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
    private Task SaveFileAsync(string filePath) => SaveFile(filePath);

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

        // Update category (PaletteID) - read selected ID from the binder, not dropdown index (#2422)
        _currentStore.PaletteID = PaletteCategoryComboBinder.GetSelectedId(StoreCategoryBox) ?? 0;

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
        _documentState.IsLoading = true;

        // Clear item resolution context
        _itemResolutionService?.SetCurrentFilePath(null);

        StoreItems.Clear();
        Variables.Clear();
        VariablesPanelControl.CanAdd = false; // no store loaded
        ClearStoreProperties();

        // Clear undo history when closing the document (#2255).
        _undo.Clear();

        _documentState.IsLoading = false;
        _documentState.ClearDirty();
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
        Radoub.UI.Services.RecentFilesMenuHelper.Populate(
            RecentFilesMenu,
            SettingsService.Instance.RecentFiles,
            async filePath =>
            {
                // Check file existence on background thread to avoid blocking on network paths
                var exists = await Task.Run(() => File.Exists(filePath));

                if (exists)
                {
                    LoadFile(filePath);
                }
                else
                {
                    ShowError($"File not found: {filePath}");
                    SettingsService.Instance.RemoveRecentFile(filePath);
                    UpdateRecentFilesMenu();
                }
            },
            () =>
            {
                SettingsService.Instance.ClearRecentFiles();
                UpdateRecentFilesMenu();
            });
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
    /// Rename the currently-open store to an already-validated path (the shared
    /// browser menu prompted + validated; #2320). Lock-aware: save-if-dirty →
    /// release lock(old) → move → update ResRef + re-save → reacquire lock(new)
    /// → refresh browser. Editor state stays bound in place (no reload).
    /// </summary>
    private async Task RenameOpenFileAsync(string oldPath, string newPath)
    {
        if (_currentStore == null || string.IsNullOrEmpty(_currentFilePath))
            return;
        if (!string.Equals(_currentFilePath, oldPath, StringComparison.OrdinalIgnoreCase))
            return; // selection changed underneath us

        try
        {
            if (_isDirty)
                await SaveFile(oldPath);

            FileSessionLockService.ReleaseLock(oldPath);
            _documentState.IsReadOnly = false;

            File.Move(oldPath, newPath);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Renamed open store: {UnifiedLogger.SanitizePath(oldPath)} -> {UnifiedLogger.SanitizePath(newPath)}");

            var newName = Path.GetFileNameWithoutExtension(newPath);
            _currentStore.ResRef = newName;
            _currentFilePath = newPath;
            await SaveFile(newPath); // persist new ResRef

            // Reacquire the session lock on the new path so the editor keeps
            // ownership and other tools see it as open.
            FileSessionLockService.AcquireLock(newPath, "Fence");

            StoreResRefBox.Text = newName;
            UpdateTitle();
            UpdateRecentFilesMenu();

            var storeBrowserPanel = this.FindControl<StoreBrowserPanel>("StoreBrowserPanel");
            if (storeBrowserPanel != null)
            {
                storeBrowserPanel.CurrentFilePath = newPath;
                storeBrowserPanel.RemoveEntryByFilePath(oldPath);
                await storeBrowserPanel.RefreshAsync();
            }

            UpdateStatusBar($"Renamed to: {Path.GetFileName(newPath)}");
        }
        catch (IOException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to rename open store: {ex.Message}");
            ShowError($"Could not rename file:\n\n{ex.Message}");
        }
    }

    /// <summary>
    /// Renames the current file using a safe save-rename-reload workflow.
    /// </summary>
    private async Task RenameCurrentFileAsync()
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
    private Task<bool> ShowConfirmationDialogAsync(string title, string message)
        => DialogHelper.ShowOkCancelAsync(this, title, message);

    #endregion
}

using ItemEditor.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace ItemEditor.Views;

public partial class MainWindow
{
    // --- Window Lifecycle ---

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;

        // Initialize game data service for base item type resolution
        await InitializeGameDataAsync();

        // Initialize item browser panel
        InitializeItemBrowserPanel();

        // Initialize spell-check service (fire-and-forget)
        _ = Radoub.UI.Services.SpellCheckService.Instance.InitializeAsync();

        // Handle startup file from command line
        var options = CommandLineService.Options;
        if (!string.IsNullOrEmpty(options.FilePath) && File.Exists(options.FilePath))
        {
            await OpenFileAsync(options.FilePath);
        }
        // Handle --new flag
        else if (options.NewItem)
        {
            await ShowNewItemWizard();
        }

        UpdateStatus("Ready");
    }

    private System.Threading.Tasks.Task InitializeGameDataAsync()
    {
        try
        {
            _gameDataService = new GameDataService();
            if (_gameDataService.IsConfigured)
            {
                // Configure module HAKs before loading 2DA data (CEP extends baseitems.2da etc.)
                var modulePath = RadoubSettings.Instance.CurrentModulePath;
                var moduleDir = GetModuleWorkingDirectory(modulePath);
                if (!string.IsNullOrEmpty(moduleDir))
                {
                    _gameDataService.ConfigureModuleHaks(moduleDir);
                }

                LoadBaseItemTypes();
                InitializePropertyServices();
                _itemIconService = new Radoub.UI.Services.ItemIconService(_gameDataService);
                UnifiedLogger.LogApplication(LogLevel.INFO, "Game data service initialized");
            }
            else
            {
                LoadBaseItemTypes(); // Will use hardcoded fallback
                UnifiedLogger.LogApplication(LogLevel.WARN, "Game data service not configured, using hardcoded base item types");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to initialize game data: {ex.Message}");
            LoadBaseItemTypes();
        }

        InitializePropertySearchHandler();
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private void InitializePropertyServices()
    {
        if (_gameDataService == null) return;

        _itemPropertyService = new ItemPropertyService(_gameDataService);
        _itemStatisticsService = new ItemStatisticsService(_itemPropertyService);
        PopulateAvailableProperties();
    }

    private void LoadBaseItemTypes()
    {
        var service = new BaseItemTypeService(_gameDataService);
        _baseItemTypes = service.GetBaseItemTypes();
        LoadPaletteCategories();
    }

    private void LoadPaletteCategories()
    {
        _paletteCategories.Clear();
        PaletteCategoryComboBox.Items.Clear();

        if (_gameDataService != null && _gameDataService.IsConfigured)
        {
            try
            {
                var categories = _gameDataService.GetPaletteCategories(Radoub.Formats.Common.ResourceTypes.Uti).ToList();
                _paletteCategories = categories;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load palette categories: {ex.Message}");
            }
        }

        // Hardcoded fallback if no categories loaded
        if (_paletteCategories.Count == 0)
        {
            _paletteCategories = GetHardcodedPaletteCategories();
        }

        foreach (var cat in _paletteCategories)
        {
            PaletteCategoryComboBox.Items.Add(new Avalonia.Controls.ComboBoxItem
            {
                Content = cat.Name,
                Tag = cat.Id
            });
        }
    }

    private static System.Collections.Generic.List<PaletteCategory> GetHardcodedPaletteCategories()
    {
        return new System.Collections.Generic.List<PaletteCategory>
        {
            new() { Id = 0, Name = "Miscellaneous" },
            new() { Id = 1, Name = "Armor" },
            new() { Id = 2, Name = "Weapons" },
            new() { Id = 3, Name = "Potions" },
            new() { Id = 4, Name = "Other" },
        };
    }

    // --- Item Browser Panel ---

    private void InitializeItemBrowserPanel()
    {
        // Set initial module path from RadoubSettings (set by Trebuchet)
        var moduleDir = GetModuleWorkingDirectory(RadoubSettings.Instance.CurrentModulePath);
        if (!string.IsNullOrEmpty(moduleDir))
        {
            ItemBrowserPanel.ModulePath = moduleDir;
            UnifiedLogger.LogUI(LogLevel.INFO, "ItemBrowserPanel initialized with module path");
        }

        // Subscribe to events
        ItemBrowserPanel.FileSelected += OnItemBrowserFileSelected;
        ItemBrowserPanel.FileDeleteRequested += OnItemBrowserFileDeleteRequested;
        ItemBrowserPanel.CollapsedChanged += (_, isCollapsed) => SetItemBrowserPanelVisible(!isCollapsed);

        UpdateItemBrowserMenuState();
        UnifiedLogger.LogUI(LogLevel.INFO, "ItemBrowserPanel initialized");
    }

    private static string? GetModuleWorkingDirectory(string? modulePath)
    {
        if (string.IsNullOrEmpty(modulePath) || !RadoubSettings.IsValidModulePath(modulePath))
            return null;

        if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
            return FindWorkingDirectory(modulePath);

        if (Directory.Exists(modulePath))
            return modulePath;

        return null;
    }

    private static string? FindWorkingDirectory(string modFilePath)
    {
        var moduleName = Path.GetFileNameWithoutExtension(modFilePath);
        var moduleDir = Path.GetDirectoryName(modFilePath);

        if (string.IsNullOrEmpty(moduleDir))
            return null;

        var candidates = new[]
        {
            Path.Combine(moduleDir, moduleName),
            Path.Combine(moduleDir, "temp0"),
            Path.Combine(moduleDir, "temp1")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private async void OnItemBrowserFileSelected(object? sender, FileSelectedEventArgs e)
    {
        try
        {
            if (e.Entry.IsFromHak)
            {
                UpdateStatus($"HAK items are read-only: {e.Entry.Name}");
                return;
            }

            if (string.IsNullOrEmpty(e.Entry.FilePath))
                return;

            // Skip if already loaded
            if (string.Equals(_currentFilePath, e.Entry.FilePath, StringComparison.OrdinalIgnoreCase))
                return;

            // Prompt save if dirty
            if (_isDirty)
            {
                var result = await PromptSaveChangesAsync();
                if (result == SavePromptResult.Cancel) return;
                if (result == SavePromptResult.Save && !await SaveCurrentFileAsync()) return;
            }

            await OpenFileAsync(e.Entry.FilePath);
            ItemBrowserPanel.CurrentFilePath = e.Entry.FilePath;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading item from browser: {ex.Message}");
            UpdateStatus($"Error: {ex.Message}");
        }
    }

    private async void OnItemBrowserFileDeleteRequested(object? sender, FileDeleteRequestedEventArgs e)
    {
        var entry = e.Entry;
        if (string.IsNullOrEmpty(entry.FilePath) || !File.Exists(entry.FilePath))
        {
            UpdateStatus("File not found on disk");
            return;
        }

        var fileName = Path.GetFileName(entry.FilePath);

        // Confirm deletion (destructive action — modal OK per CLAUDE.md)
        var dialog = new Avalonia.Controls.Window
        {
            Title = "Confirm Delete",
            Width = 380,
            Height = 150,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var confirmed = false;
        var panel = new Avalonia.Controls.StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 16 };
        panel.Children.Add(new Avalonia.Controls.TextBlock
        {
            Text = $"Delete \"{fileName}\" from disk?\n\nThis cannot be undone.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        var buttons = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8
        };
        var deleteBtn = new Avalonia.Controls.Button { Content = "Delete", Width = 80 };
        deleteBtn.Click += (_, _) => { confirmed = true; dialog.Close(); };
        var cancelBtn = new Avalonia.Controls.Button { Content = "Cancel", Width = 80 };
        cancelBtn.Click += (_, _) => dialog.Close();
        buttons.Children.Add(deleteBtn);
        buttons.Children.Add(cancelBtn);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        await dialog.ShowDialog(this);

        if (!confirmed)
            return;

        try
        {
            var isDeletingCurrent = string.Equals(_currentFilePath, entry.FilePath, StringComparison.OrdinalIgnoreCase);

            File.Delete(entry.FilePath);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Deleted item file: {fileName}");

            if (isDeletingCurrent)
            {
                if (_itemViewModel != null)
                    _itemViewModel.PropertyChanged -= OnItemPropertyChanged;

                _currentItem = null;
                _currentFilePath = null;
                _documentState.ClearDirty();
                PopulateEditor();
                OnPropertyChanged(nameof(HasFile));
            }

            UpdateStatus($"Deleted {fileName}");
            await ItemBrowserPanel.RefreshAsync();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to delete {fileName}: {ex.Message}");
            UpdateStatus($"Delete failed: {ex.Message}");
        }
    }

    private async void OnWindowClosing(object? sender, Avalonia.Controls.WindowClosingEventArgs e)
    {
        if (_isDirty)
        {
            e.Cancel = true;
            var result = await PromptSaveChangesAsync();
            if (result == SavePromptResult.Cancel)
                return;

            if (result == SavePromptResult.Save)
            {
                if (!await SaveCurrentFileAsync())
                    return;
            }

            _documentState.ClearDirty();
            Close();
        }

        RadoubSettings.Instance.PropertyChanged -= OnRadoubSettingsChanged;
        FileSessionLockService.ReleaseAllLocks();
        SaveWindowPosition();
    }

    private void OnRadoubSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RadoubSettings.CurrentModulePath))
        {
            UpdateModuleIndicator();
            var moduleDir = GetModuleWorkingDirectory(RadoubSettings.Instance.CurrentModulePath);
            if (!string.IsNullOrEmpty(moduleDir))
            {
                ItemBrowserPanel.ModulePath = moduleDir;
                UnifiedLogger.LogUI(LogLevel.INFO, "Module path updated from Trebuchet");
            }
        }
    }

    // --- Window Position ---

    private void RestoreWindowPosition()
    {
        var settings = SettingsService.Instance;
        if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
        }
        if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
        {
            Position = new Avalonia.PixelPoint((int)settings.WindowLeft, (int)settings.WindowTop);
        }
    }

    private void SaveWindowPosition()
    {
        var settings = SettingsService.Instance;
        if (WindowState == Avalonia.Controls.WindowState.Normal)
        {
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.WindowLeft = Position.X;
            settings.WindowTop = Position.Y;
        }
    }
}

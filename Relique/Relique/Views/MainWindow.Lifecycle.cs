using ItemEditor.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using System;
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
                LoadBaseItemTypes();
                InitializePropertyServices();
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
        PopulateBaseItemComboBox();
        LoadPaletteCategories();
    }

    private void PopulateBaseItemComboBox()
    {
        BaseItemComboBox.Items.Clear();
        if (_baseItemTypes == null) return;

        foreach (var type in _baseItemTypes)
        {
            BaseItemComboBox.Items.Add(new Avalonia.Controls.ComboBoxItem
            {
                Content = type.DisplayName,
                Tag = type.BaseItemIndex
            });
        }
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

        FileSessionLockService.ReleaseAllLocks();
        SaveWindowPosition();
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

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
using System.Threading.Tasks;

namespace ItemEditor.Views;

public partial class MainWindow
{
    // --- Window Lifecycle ---

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;

        // Initialize game data service on background thread (#1959)
        await InitializeGameDataAsync();

        // Initialize 3D item preview pipeline (#1908 PR3b) — needs game data ready
        InitializeItemPreview();

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

    private async Task InitializeGameDataAsync()
    {
        try
        {
            // Heavy I/O on background thread: KEY/BIF reading, HAK loading, 2DA parsing (#1959)
            var (gameDataService, baseItemTypes, paletteCategories, itemPropertyService,
                 itemStatisticsService, itemIconService, paletteColorService) =
                await Task.Run(() =>
                {
                    var gds = new GameDataService();
                    BaseItemTypeService? baseItemSvc = null;
                    System.Collections.Generic.List<PaletteCategory>? palCats = null;
                    ItemPropertyService? ipSvc = null;
                    ItemStatisticsService? isSvc = null;
                    Radoub.UI.Services.ItemIconService? iconSvc = null;
                    PaletteColorService? colorSvc = null;

                    if (gds.IsConfigured)
                    {
                        var modulePath = RadoubSettings.Instance.CurrentModulePath;
                        var moduleDir = GetModuleWorkingDirectory(modulePath);
                        if (!string.IsNullOrEmpty(moduleDir))
                        {
                            gds.ConfigureModuleHaks(moduleDir);
                        }

                        baseItemSvc = new BaseItemTypeService(gds);
                        ipSvc = new ItemPropertyService(gds);
                        isSvc = new ItemStatisticsService(ipSvc);
                        iconSvc = new Radoub.UI.Services.ItemIconService(gds);
                        colorSvc = new PaletteColorService(gds);

                        try
                        {
                            palCats = gds.GetPaletteCategories(Radoub.Formats.Common.ResourceTypes.Uti).ToList();
                        }
                        catch (Exception ex)
                        {
                            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load palette categories: {ex.Message}");
                        }

                        UnifiedLogger.LogApplication(LogLevel.INFO, "Game data service initialized");
                    }
                    else
                    {
                        baseItemSvc = new BaseItemTypeService(gds);
                        UnifiedLogger.LogApplication(LogLevel.WARN, "Game data service not configured, using hardcoded base item types");
                    }

                    return (gds, baseItemSvc.GetBaseItemTypes(),
                        palCats, ipSvc, isSvc, iconSvc, colorSvc);
                });

            // Apply results on UI thread
            _gameDataService = gameDataService;
            _baseItemTypes = baseItemTypes;
            _itemPropertyService = itemPropertyService;
            _itemStatisticsService = itemStatisticsService;
            _itemCostCalculator = new ItemCostCalculator(gameDataService); // #2235
            _itemIconService = itemIconService;
            _paletteColorService = paletteColorService;
            _armorPartCatalog = new ArmorPartCatalogService(gameDataService); // #2164
            _compositeWeaponCatalog = new CompositeWeaponPartCatalogService(gameDataService); // #2164

            // UI updates must happen on UI thread
            LoadPaletteCategories(paletteCategories);
            if (_itemPropertyService != null)
            {
                PopulateAvailableProperties();
            }
            InitializePropertySearchHandler();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to initialize game data: {ex.Message}");
            // Fallback: create base item types with null game data
            var fallbackService = new BaseItemTypeService(_gameDataService);
            _baseItemTypes = fallbackService.GetBaseItemTypes();
            LoadPaletteCategories(null);
            InitializePropertySearchHandler();
        }
    }

    private void LoadPaletteCategories(System.Collections.Generic.List<PaletteCategory>? categories)
    {
        _paletteCategories.Clear();
        PaletteCategoryComboBox.Items.Clear();

        if (categories != null && categories.Count > 0)
        {
            _paletteCategories = categories;
        }
        else
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
        // Wire GameDataService for "Base Game" (BIF) item scanning (#2106)
        ItemBrowserPanel.GameDataService = _gameDataService;

        // Wire shared UTI palette cache for Tag/Name indexing (#2186 / #2198).
        // BIF + HAK entries pull from cache (warmed by Trebuchet) so first-time
        // indexing is instant; cache miss falls back to per-file GFF read.
        ItemBrowserPanel.PaletteCache = new SharedPaletteCacheService();

        // Set initial module path from RadoubSettings (set by Trebuchet)
        var moduleDir = GetModuleWorkingDirectory(RadoubSettings.Instance.CurrentModulePath);
        if (!string.IsNullOrEmpty(moduleDir))
        {
            ItemBrowserPanel.ModulePath = moduleDir;
            UnifiedLogger.LogUI(LogLevel.INFO, "ItemBrowserPanel initialized with module path");
        }

        // Subscribe to events
        ItemBrowserPanel.FileSelected += OnItemBrowserFileSelected;
        ItemBrowserPanel.FileDeleted += OnItemBrowserFileDeleted;
        ItemBrowserPanel.CollapsedChanged += (_, isCollapsed) => SetItemBrowserPanelVisible(!isCollapsed);

        // Copy-to-Module status feedback (#1479)
        ItemBrowserPanel.FileCopiedToModule += (_, destPath) =>
        {
            UpdateStatus($"Copied to module: {Path.GetFileName(destPath)}");
        };

        // Shared browser Copy/Rename context-menu feedback (#2320). The panel
        // performs the disk op + refresh for non-open files; we just report it.
        ItemBrowserPanel.FileCopied += (_, args) =>
        {
            UpdateStatus($"Copied: {Path.GetFileName(args.NewPath)}");
        };
        ItemBrowserPanel.FileRenamed += (_, args) =>
        {
            UpdateStatus($"Renamed to: {Path.GetFileName(args.NewPath)}");
        };
        // Renaming the OPEN item: base prompted + validated; run lock-aware
        // save → move → reopen here (#2320).
        ItemBrowserPanel.FileRenameRequested += async (_, args) =>
        {
            await RenameOpenFileAsync(args.OldPath, args.NewPath);
        };

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
            // Archive items (HAK/BIF) — load read-only preview (#2106)
            var isFromBif = e.Entry is ItemBrowserEntry { IsFromBif: true };
            if (e.Entry.IsFromHak || isFromBif)
            {
                // Prompt save if dirty before swapping out _currentItem
                if (_isDirty)
                {
                    var saveResult = await PromptSaveChangesAsync();
                    if (saveResult == SavePromptResult.Cancel) return;
                    if (saveResult == SavePromptResult.Save && !await SaveCurrentFileAsync()) return;
                }

                await LoadArchiveItemAsync(e.Entry);
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

    /// <summary>
    /// Load a UTI item from a HAK or BIF archive in read-only preview mode (#2106).
    /// Mirrors Fence's LoadArchiveStore pattern.
    /// </summary>
    private async Task LoadArchiveItemAsync(FileBrowserEntry entry)
    {
        var isFromBif = entry is ItemBrowserEntry { IsFromBif: true };
        var sourceLabel = isFromBif ? "base game" : "HAK";

        try
        {
            UpdateStatus($"Loading {sourceLabel} item: {entry.Name}...");

            var bytes = await Task.Run(() => ItemBrowserPanel.ExtractItemArchiveBytes(entry, _gameDataService));
            if (bytes == null)
            {
                UpdateStatus($"Could not extract {entry.Name} from {sourceLabel} archives");
                return;
            }

            // Release lock on previous file
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                FileSessionLockService.ReleaseLock(_currentFilePath);
            }

            _currentItem = Radoub.Formats.Uti.UtiReader.Read(bytes);
            _currentFilePath = null; // No file path — read-only archive resource
            _documentState.IsReadOnly = true;
            _documentState.ClearDirty();

            PopulateEditor();
            OnPropertyChanged(nameof(HasFile));

            ItemBrowserPanel.CurrentFilePath = null;
            UpdateTitle();
            UpdateStatus($"{sourceLabel} item (read-only): {entry.Name}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load {sourceLabel} item {entry.Name}: {ex.Message}");
            UpdateStatus($"Error loading {sourceLabel} item: {ex.Message}");
        }
    }

    // The browser panel owns confirm + backup + delete + refresh (#2350). The host
    // only reacts here to close the editor if the file that was open got deleted.
    private void OnItemBrowserFileDeleted(object? sender, FileDeletedEventArgs e)
    {
        var fileName = Path.GetFileName(e.FilePath);

        if (e.WasCurrentFile)
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
    }

    private bool _cleanedUp;

    private async void OnWindowClosing(object? sender, Avalonia.Controls.WindowClosingEventArgs e)
    {
        // The dirty path cancels the close, prompts, then calls Close() re-entrantly — which
        // re-fires this handler. Both the inner and outer invocation would otherwise reach the
        // cleanup block. Guard so cleanup runs exactly once across the re-entry (#2258).
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
            Close(); // re-enters this handler with _isDirty == false → runs cleanup once
            return;  // outer invocation must not fall through to a second cleanup pass
        }

        if (_cleanedUp)
            return;
        _cleanedUp = true;

        RadoubSettings.Instance.PropertyChanged -= OnRadoubSettingsChanged;
        DisposeItemPreview();
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

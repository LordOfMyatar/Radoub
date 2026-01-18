using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Quartermaster.Services;
using Quartermaster.Views.Helpers;
using Radoub.Formats.Bic;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Quartermaster.Views;

/// <summary>
/// MainWindow partial class for file operations (Open/Save/Load/Recent files).
/// Extracted from MainWindow.axaml.cs for maintainability (#582).
/// </summary>
public partial class MainWindow
{
    #region Recent Files Menu

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
            var fileName = Path.GetFileName(filePath);
            var displayPath = UnifiedLogger.SanitizePath(filePath);

            var menuItem = new MenuItem
            {
                Header = fileName,
                Tag = filePath
            };
            ToolTip.SetTip(menuItem, displayPath);
            menuItem.Click += OnRecentFileClick;

            RecentFilesMenu.Items.Add(menuItem);
        }

        RecentFilesMenu.Items.Add(new Separator());

        var clearItem = new MenuItem { Header = "Clear Recent Files" };
        clearItem.Click += OnClearRecentFilesClick;
        RecentFilesMenu.Items.Add(clearItem);
    }

    private async void OnRecentFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string filePath)
        {
            // Close the entire menu hierarchy before async work
            // Find the top-level File menu and close it
            if (RecentFilesMenu.Parent is MenuItem fileMenu)
            {
                fileMenu.Close();
            }

            if (File.Exists(filePath))
            {
                await LoadFile(filePath);
            }
            else
            {
                UpdateStatus($"File not found: {Path.GetFileName(filePath)}");
                SettingsService.Instance.RemoveRecentFile(filePath);
                UpdateRecentFilesMenu();
            }
        }
    }

    private void OnClearRecentFilesClick(object? sender, RoutedEventArgs e)
    {
        SettingsService.Instance.ClearRecentFiles();
        UpdateRecentFilesMenu();
    }

    #endregion

    #region Menu Click Handlers

    private async void OnNewClick(object? sender, RoutedEventArgs e)
    {
        await NewFile();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        await OpenFile();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        await SaveFile();
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        await SaveFileAs();
    }

    private async void OnCloseFileClick(object? sender, RoutedEventArgs e)
    {
        await CloseFileWithCheck();
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnExportTextClick(object? sender, RoutedEventArgs e)
    {
        await ExportCharacterSheet(isMarkdown: false);
    }

    private async void OnExportMarkdownClick(object? sender, RoutedEventArgs e)
    {
        await ExportCharacterSheet(isMarkdown: true);
    }

    #endregion

    #region Export Operations

    private async Task ExportCharacterSheet(bool isMarkdown)
    {
        if (_currentCreature == null) return;

        var extension = isMarkdown ? ".md" : ".txt";
        var filterName = isMarkdown ? "Markdown Files" : "Text Files";
        var defaultName = Path.GetFileNameWithoutExtension(_currentFilePath ?? "character") + "_sheet" + extension;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Character Sheet",
            DefaultExtension = extension,
            SuggestedFileName = defaultName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(filterName) { Patterns = new[] { "*" + extension } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (file == null) return;

        try
        {
            var sheetService = new CharacterSheetService(_creatureDisplayService);
            var content = isMarkdown
                ? sheetService.GenerateMarkdownSheet(_currentCreature, _currentFilePath)
                : sheetService.GenerateTextSheet(_currentCreature, _currentFilePath);

            await File.WriteAllTextAsync(file.Path.LocalPath, content);

            UpdateStatus($"Exported: {Path.GetFileName(file.Path.LocalPath)}");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Exported character sheet: {UnifiedLogger.SanitizePath(file.Path.LocalPath)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to export character sheet: {ex.Message}");
            await ShowErrorDialog("Export Error", $"Failed to export character sheet:\n{ex.Message}");
        }
    }

    #endregion

    #region File Operations

    private async Task NewFile()
    {
        // Check for unsaved changes
        if (_isDirty)
        {
            var result = await DialogHelper.ShowUnsavedChangesDialog(this);
            if (result == "Save")
            {
                await SaveFile();
            }
            else if (result == "Cancel")
            {
                return;
            }
            // "Discard" continues to create new file
        }

        // Close current file if open
        if (_currentCreature != null)
        {
            CloseFile();
        }

        // Create new creature with sensible defaults
        _currentCreature = CreateNewCreature();
        _currentFilePath = null; // New file, not yet saved
        _isBicFile = false; // New files are UTC by default
        _isLoading = true;

        // Clear panel file contexts (no file path yet)
        CharacterPanelContent.SetCurrentFilePath(null);
        ScriptsPanelContent.SetCurrentFilePath(null);
        AdvancedPanelContent.SetModuleDirectory(null);

        // Populate UI
        ClearInventoryUI();
        UpdateCharacterHeader();
        LoadAllPanels(_currentCreature);
        UpdateInventoryCounts();
        OnPropertyChanged(nameof(HasFile));

        // Mark as dirty immediately (new unsaved file)
        _isLoading = false;
        _isDirty = true;
        UpdateTitle();
        UpdateStatus("New creature created");

        UnifiedLogger.LogCreature(LogLevel.INFO, "Created new creature blueprint");
    }

    /// <summary>
    /// Creates a new UtcFile with sensible defaults for a basic humanoid creature.
    /// </summary>
    private static UtcFile CreateNewCreature()
    {
        return new UtcFile
        {
            // Identity - StrRef = 0xFFFFFFFF means no TLK reference (use embedded string)
            FirstName = new CExoLocString { StrRef = 0xFFFFFFFF },
            LastName = new CExoLocString { StrRef = 0xFFFFFFFF },
            Tag = "new_creature",
            TemplateResRef = "new_creature",
            Description = new CExoLocString { StrRef = 0xFFFFFFFF },

            // Basic info - Human male by default
            Race = 6, // Human (racialtypes.2da)
            Gender = 0, // Male

            // Appearance - Human appearance type
            AppearanceType = 6, // Human (appearance.2da)
            Phenotype = 0, // Normal
            PortraitId = 1, // First portrait

            // Default body parts for part-based model (all set to 1 = basic)
            AppearanceHead = 1,
            BodyPart_Belt = 0,
            BodyPart_LBicep = 1,
            BodyPart_RBicep = 1,
            BodyPart_LFArm = 1,
            BodyPart_RFArm = 1,
            BodyPart_LFoot = 1,
            BodyPart_RFoot = 1,
            BodyPart_LHand = 1,
            BodyPart_RHand = 1,
            BodyPart_LShin = 1,
            BodyPart_RShin = 1,
            BodyPart_LShoul = 0,
            BodyPart_RShoul = 0,
            BodyPart_LThigh = 1,
            BodyPart_RThigh = 1,
            BodyPart_Neck = 1,
            BodyPart_Pelvis = 1,
            BodyPart_Torso = 1,

            // Colors - neutral defaults
            Color_Skin = 0,
            Color_Hair = 0,
            Color_Tattoo1 = 0,
            Color_Tattoo2 = 0,

            // Ability scores - standard array
            Str = 10,
            Dex = 10,
            Con = 10,
            Int = 10,
            Wis = 10,
            Cha = 10,

            // Hit points - minimal for level 1
            HitPoints = 4,
            CurrentHitPoints = 4,
            MaxHitPoints = 4,

            // Alignment - True Neutral
            GoodEvil = 50,
            LawfulChaotic = 50,

            // Behavior defaults
            FactionID = 1, // Commoner faction
            PerceptionRange = 11, // Default perception
            WalkRate = 4, // PC walk rate
            DecayTime = 5000, // 5 seconds

            // Interruptable by default
            Interruptable = true,

            // Must have at least one class - Commoner level 1
            ClassList = new List<CreatureClass>
            {
                new CreatureClass
                {
                    Class = 7, // Commoner (classes.2da)
                    ClassLevel = 1
                }
            },

            // Initialize empty lists
            FeatList = new List<ushort>(),
            SkillList = new List<byte>(),
            SpecAbilityList = new List<SpecialAbility>(),
            ItemList = new List<InventoryItem>(),
            EquipItemList = new List<EquippedItem>()
        };
    }

    private async Task OpenFile()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Creature File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Creature Files") { Patterns = new[] { "*.utc", "*.bic" } },
                new FilePickerFileType("Creature Blueprints") { Patterns = new[] { "*.utc" } },
                new FilePickerFileType("Player Characters") { Patterns = new[] { "*.bic" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            var file = files[0];
            await LoadFile(file.Path.LocalPath);
        }
    }

    private async Task LoadFile(string filePath)
    {
        try
        {
            // Set loading flag to prevent MarkDirty() from firing during panel population
            _isLoading = true;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            _isBicFile = extension == ".bic";

            if (_isBicFile)
            {
                _currentCreature = BicReader.Read(filePath);
                UnifiedLogger.LogCreature(LogLevel.INFO, $"Loaded BIC (player character): {UnifiedLogger.SanitizePath(filePath)}");
            }
            else
            {
                _currentCreature = UtcReader.Read(filePath);
                UnifiedLogger.LogCreature(LogLevel.INFO, $"Loaded UTC (creature blueprint): {UnifiedLogger.SanitizePath(filePath)}");
            }

            _currentFilePath = filePath;

            // Update panels with current file context for dialog/script browsing
            CharacterPanelContent.SetCurrentFilePath(_currentFilePath);
            ScriptsPanelContent.SetCurrentFilePath(_currentFilePath);

            // Update advanced panel with module directory for faction loading
            var moduleDirectory = Path.GetDirectoryName(_currentFilePath);
            AdvancedPanelContent.SetModuleDirectory(moduleDirectory);

            PopulateInventoryUI();
            UpdateCharacterHeader();
            LoadAllPanels(_currentCreature);
            UpdateInventoryCounts();
            OnPropertyChanged(nameof(HasFile));

            // Clear loading flag and reset dirty state
            _isLoading = false;
            _isDirty = false;
            UpdateTitle();
            UpdateStatus($"Loaded: {Path.GetFileName(filePath)}");

            SettingsService.Instance.AddRecentFile(filePath);
            UpdateRecentFilesMenu();
        }
        catch (Exception ex)
        {
            _isLoading = false;
            UnifiedLogger.LogCreature(LogLevel.ERROR, $"Failed to load creature: {ex.Message}");
            UpdateStatus($"Error loading file: {ex.Message}");
            await ShowErrorDialog("Load Error", $"Failed to load creature file:\n{ex.Message}");
        }
    }

    private async Task SaveFile()
    {
        if (_currentCreature == null) return;

        // New file without path - redirect to Save As
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            await SaveFileAs();
            return;
        }

        try
        {
            // Sync UI state to creature model before saving
            SyncInventoryToCreature();

            if (_isBicFile && _currentCreature is BicFile bicFile)
            {
                BicWriter.Write(bicFile, _currentFilePath);
            }
            else
            {
                UtcWriter.Write(_currentCreature, _currentFilePath);
            }

            _isDirty = false;
            UpdateTitle();
            UpdateStatus($"Saved: {Path.GetFileName(_currentFilePath)}");

            UnifiedLogger.LogCreature(LogLevel.INFO, $"Saved creature: {UnifiedLogger.SanitizePath(_currentFilePath)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogCreature(LogLevel.ERROR, $"Failed to save creature: {ex.Message}");
            UpdateStatus($"Error saving file: {ex.Message}");
            await ShowErrorDialog("Save Error", $"Failed to save creature file:\n{ex.Message}");
        }
    }

    private async Task SaveFileAs()
    {
        if (_currentCreature == null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Creature As",
            DefaultExtension = _isBicFile ? ".bic" : ".utc",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Creature Blueprint") { Patterns = new[] { "*.utc" } },
                new FilePickerFileType("Player Character") { Patterns = new[] { "*.bic" } }
            }
        });

        if (file != null)
        {
            _currentFilePath = file.Path.LocalPath;
            var newExtension = Path.GetExtension(_currentFilePath).ToLowerInvariant();
            var savingAsBic = newExtension == ".bic";

            // Validate Aurora Engine filename constraints
            if (!await ValidateAuroraFilename(_currentFilePath))
            {
                _currentFilePath = null; // Clear the path since save was cancelled
                return;
            }

            // Validate playable class for BIC files
            if (savingAsBic && !await ValidatePlayableClassForBic(_currentCreature))
            {
                _currentFilePath = null; // Clear the path since save was cancelled
                return;
            }

            // Handle format conversion
            var wasConverted = false;
            if (savingAsBic && !_isBicFile)
            {
                // Converting UTC to BIC
                _currentCreature = BicFile.FromUtcFile(_currentCreature);
                _isBicFile = true;
                wasConverted = true;
                UnifiedLogger.LogCreature(LogLevel.INFO, "Converted UTC to BIC format");
            }
            else if (!savingAsBic && _isBicFile && _currentCreature is BicFile bicFile)
            {
                // Converting BIC to UTC - pass the target filename so TemplateResRef matches
                var targetResRef = Path.GetFileNameWithoutExtension(_currentFilePath);
                _currentCreature = bicFile.ToUtcFile(targetResRef);
                _isBicFile = false;
                wasConverted = true;
                UnifiedLogger.LogCreature(LogLevel.INFO, "Converted BIC to UTC format");
            }
            else
            {
                _isBicFile = savingAsBic;
            }

            await SaveFile();
            UpdateTitle();
            UpdateCharacterHeader();
            SettingsService.Instance.AddRecentFile(_currentFilePath);
            UpdateRecentFilesMenu();

            // If format was converted, reload panels to reflect the new file type
            if (wasConverted)
            {
                _isLoading = true;
                LoadAllPanels(_currentCreature);
                _isLoading = false;
                _isDirty = false; // Reset dirty after panel reload
                UpdateTitle();
                UpdateStatus($"Converted and saved as {(savingAsBic ? "BIC" : "UTC")}: {Path.GetFileName(_currentFilePath)}");
            }
        }
    }

    /// <summary>
    /// Validates that the filename meets Aurora Engine constraints.
    /// Aurora Engine filenames must be: lowercase, max 16 characters (excluding extension),
    /// alphanumeric and underscore only.
    /// </summary>
    /// <returns>True if valid, false otherwise with error dialog shown.</returns>
    private async Task<bool> ValidateAuroraFilename(string filePath)
    {
        var filename = Path.GetFileNameWithoutExtension(filePath);

        // Check length (max 16 characters)
        if (filename.Length > 16)
        {
            await DialogHelper.ShowMessageDialog(this, "Invalid Filename",
                $"Filename is too long for Aurora Engine.\n\n" +
                $"Current: \"{filename}\" ({filename.Length} characters)\n" +
                $"Maximum: 16 characters\n\n" +
                "The Aurora Engine (Neverwinter Nights) cannot load files with names longer than 16 characters.");
            return false;
        }

        // Check for uppercase letters
        if (filename.Any(char.IsUpper))
        {
            await DialogHelper.ShowMessageDialog(this, "Invalid Filename",
                $"Filename contains uppercase letters.\n\n" +
                $"Current: \"{filename}\"\n" +
                $"Suggested: \"{filename.ToLowerInvariant()}\"\n\n" +
                "Aurora Engine filenames should be lowercase for compatibility.");
            return false;
        }

        // Check for invalid characters (only alphanumeric and underscore allowed)
        var invalidChars = filename.Where(c => !char.IsLetterOrDigit(c) && c != '_').ToList();
        if (invalidChars.Count > 0)
        {
            var invalidStr = string.Join("", invalidChars.Distinct());
            await DialogHelper.ShowMessageDialog(this, "Invalid Filename",
                $"Filename contains invalid characters.\n\n" +
                $"Current: \"{filename}\"\n" +
                $"Invalid characters: \"{invalidStr}\"\n\n" +
                "Aurora Engine filenames can only contain letters, numbers, and underscores.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a creature has at least one playable class for BIC files.
    /// </summary>
    /// <returns>True if valid for BIC, false otherwise with error dialog shown.</returns>
    private async Task<bool> ValidatePlayableClassForBic(UtcFile creature)
    {
        if (creature.ClassList == null || creature.ClassList.Count == 0)
        {
            await DialogHelper.ShowMessageDialog(this, "Invalid Character",
                "Cannot save as player character (BIC): No classes defined.\n\n" +
                "Add at least one class to the creature before saving as a BIC file.");
            return false;
        }

        // Check if any class is a playable class (PlayerClass = 1 in classes.2da)
        var hasPlayableClass = creature.ClassList.Any(c =>
        {
            var playerClass = _gameDataService.Get2DAValue("classes", c.Class, "PlayerClass");
            return playerClass == "1";
        });

        if (!hasPlayableClass)
        {
            // Get the class names for the error message
            var classNames = creature.ClassList
                .Select(c => _creatureDisplayService.GetClassName(c.Class))
                .ToList();
            var classList = string.Join(", ", classNames);

            await DialogHelper.ShowMessageDialog(this, "Invalid Character Class",
                $"Cannot save as player character (BIC): No playable class found.\n\n" +
                $"Current class(es): {classList}\n\n" +
                "Player characters require at least one playable class (Fighter, Wizard, Cleric, etc.). " +
                "NPC-only classes like Commoner or Animal cannot be used for player characters.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Close file with unsaved changes check. Returns true if file was closed.
    /// </summary>
    private async Task<bool> CloseFileWithCheck()
    {
        try
        {
            if (_isDirty)
            {
                var result = await DialogHelper.ShowUnsavedChangesDialog(this);
                if (result == "Save")
                {
                    await SaveFile();
                    // SaveFile sets _isDirty = false on success
                }
                else if (result == "Cancel")
                {
                    return false;
                }
                // "Discard" continues to close - _isDirty will be cleared in CloseFile
            }

            // Prevent any events during close from marking dirty
            _isLoading = true;
            CloseFile();
            _isLoading = false;
            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"CloseFileWithCheck failed: {ex}");
            _isLoading = false;
            throw;
        }
    }

    private void CloseFile()
    {
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "CloseFile: Starting");
        _currentCreature = null;
        _currentFilePath = null;
        _isDirty = false;
        _isBicFile = false;

        UnifiedLogger.LogApplication(LogLevel.DEBUG, "CloseFile: Clearing panel file contexts");
        // Clear panel file contexts
        CharacterPanelContent.SetCurrentFilePath(null);
        ScriptsPanelContent.SetCurrentFilePath(null);

        UnifiedLogger.LogApplication(LogLevel.DEBUG, "CloseFile: ClearInventoryUI");
        ClearInventoryUI();
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "CloseFile: UpdateCharacterHeader");
        UpdateCharacterHeader();
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "CloseFile: ClearAllPanels");
        ClearAllPanels();
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "CloseFile: UpdateTitle");
        UpdateTitle();
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "CloseFile: UpdateStatus");
        UpdateStatus("Ready");
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "CloseFile: UpdateInventoryCounts");
        UpdateInventoryCounts();
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "CloseFile: OnPropertyChanged");
        OnPropertyChanged(nameof(HasFile));
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "CloseFile: Complete");
    }

    #endregion
}

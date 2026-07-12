using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Quartermaster.Services;
using Quartermaster.Views.Dialogs;
using Quartermaster.Views.Helpers;
using Radoub.Formats.Bic;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.Formats.Utc;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Radoub.UI.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Quartermaster.Views;

/// <summary>
/// File operations: Open, Save, Load, New, Export, Recent files, Close.
/// Partial classes: FileValidation (Aurora filename, BIC class, rename).
/// </summary>
/// <remarks>
/// Module switch detection: tracks _lastLoadedModuleDir to reconfigure HAKs
/// when files are loaded from different modules (#1867, #1869).
/// </remarks>
public partial class MainWindow
{
    // Tracks the module directory of the last loaded file for HAK reconfiguration (#1867, #1869)
    private string? _lastLoadedModuleDir;

    #region Recent Files Menu

    private void UpdateRecentFilesMenu()
    {
        Radoub.UI.Services.RecentFilesMenuHelper.Populate(
            RecentFilesMenu,
            SettingsService.Instance.RecentFiles,
            async filePath =>
            {
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
            },
            () =>
            {
                SettingsService.Instance.ClearRecentFiles();
                UpdateRecentFilesMenu();
            });
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
            ShowProgress(true);
            UpdateStatus("Exporting character sheet...");

            var sheetService = new CharacterSheetService(DisplayService);
            var content = isMarkdown
                ? sheetService.GenerateMarkdownSheet(_currentCreature, _currentFilePath)
                : sheetService.GenerateTextSheet(_currentCreature, _currentFilePath);

            await File.WriteAllTextAsync(file.Path.LocalPath, content);

            ShowProgress(false);
            UpdateStatus($"Exported: {Path.GetFileName(file.Path.LocalPath)}");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Exported character sheet: {UnifiedLogger.SanitizePath(file.Path.LocalPath)}");
        }
        catch (Exception ex)
        {
            ShowProgress(false);
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to export character sheet: {ex.Message}");
            ShowErrorDialog("Export Error", $"Failed to export character sheet:\n{ex.Message}");
        }
    }

    #endregion

    #region File Operations

    private async Task NewFile()
    {
        // Check for unsaved changes using shared helper
        var dirtyResult = await Radoub.UI.Services.FileOperationsHelper.CheckDirtyAsync(this, _documentState);
        if (dirtyResult == Radoub.UI.Services.DirtyCheckResult.Cancel) return;
        if (dirtyResult == Radoub.UI.Services.DirtyCheckResult.Save) await SaveFile();

        // Close current file if open
        if (_currentCreature != null)
        {
            CloseFile();
        }

        // Launch creation wizard
        var wizard = new NewCharacterWizardWindow(DisplayService, GameData, IconService, _audioService);
        await wizard.ShowDialog(this);

        if (!wizard.Confirmed || wizard.CreatedCreature == null)
        {
            UpdateStatus("New creature cancelled");
            return;
        }

        _currentCreature = wizard.CreatedCreature;
        _currentFilePath = null; // New file, not yet saved
        _isBicFile = wizard.IsBicFile;
        _isLoading = true;

        // Clear panel file contexts (no file path yet)
        CharacterPanelContent.SetCurrentFilePath(null);
        ScriptsPanelContent.SetCurrentFilePath(null);
        AdvancedPanelContent.SetModuleDirectory(null);

        // Populate UI
        ClearInventoryUI();
        PopulateInventoryUI();
        UpdateCharacterHeader();
        LoadAllPanels(_currentCreature);
        UpdateInventoryCounts();
        OnPropertyChanged(nameof(HasFile));

        _isLoading = false;
        _documentState.ForceDirty();
        UpdateTitle();

        UnifiedLogger.LogCreature(LogLevel.INFO, "Created new creature blueprint");

        // Multi-level creation: consolidated LUW instead of loop (#1645, replaces #1431)
        if (wizard.StartingLevel > 1)
        {
            int targetLevel = wizard.StartingLevel;
            int levelsToAdd = targetLevel - 1;
            int classId = _currentCreature.ClassList.FirstOrDefault()?.Class ?? -1;

            UpdateStatus($"Multi-level creation: leveling to {targetLevel}...");

            var luw = new LevelUpWizardWindow(DisplayService, _currentCreature, _isBicFile,
                presetClassId: classId, presetLevels: levelsToAdd);
            await luw.ShowDialog(this);

            if (luw.Confirmed)
            {
                LoadAllPanels(_currentCreature);
                UpdateCharacterHeader();
                UpdateStatus($"Character created at level {targetLevel}");
            }
            else
            {
                UpdateStatus("Multi-level creation cancelled");
            }

            MarkDirty();
        }

        // Save to path chosen in wizard Step 1, or prompt if not chosen (#1476)
        if (!string.IsNullOrEmpty(wizard.ChosenSavePath))
        {
            _currentFilePath = wizard.ChosenSavePath;

            // Validate Aurora filename constraints
            if (!await ValidateAuroraFilename(_currentFilePath))
            {
                _currentFilePath = null;
                UpdateStatus("New creature created (not saved yet)");
                return;
            }

            // Convert to BIC if saving as .bic and creature is still a UtcFile
            var savingAsBic = Path.GetExtension(_currentFilePath).Equals(".bic", StringComparison.OrdinalIgnoreCase);
            if (savingAsBic && _currentCreature is not BicFile)
            {
                _currentCreature = BicFile.FromUtcFile(_currentCreature, GameData);
                _isBicFile = true;
            }

            await SaveFile();
            UpdateTitle();
            UpdateCharacterHeader();
            SettingsService.Instance.AddRecentFile(_currentFilePath);
            UpdateRecentFilesMenu();

            // Refresh creature browser so the new file appears (#1477)
            UpdateCreatureBrowserCurrentFile(_currentFilePath);
            var creatureBrowserPanel = this.FindControl<CreatureBrowserPanel>("CreatureBrowserPanel");
            if (creatureBrowserPanel != null)
                await creatureBrowserPanel.RefreshAsync();
        }
        else
        {
            await PromptSaveNewCreature();
        }
    }

    private async Task OpenFile()
    {
        // Use custom CreatureBrowserWindow for consistent UX (#1083)
        var context = new QuartermasterScriptBrowserContext(_currentFilePath, GameData);
        var browser = new CreatureBrowserWindow(context);
        await browser.ShowDialog(this);

        // Check if user selected a creature
        var selectedEntry = browser.SelectedEntry;
        if (selectedEntry?.FilePath != null)
        {
            await LoadFile(selectedEntry.FilePath);
        }
    }

    private async Task LoadFile(string filePath)
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
            var lockResult = FileSessionLockService.AcquireLock(filePath, "Quartermaster");
            if (lockResult == LockResult.LockedByOther)
            {
                var lockInfo = FileSessionLockService.CheckLock(filePath);
                var toolName = lockInfo?.ToolName ?? "another tool";
                UnifiedLogger.LogApplication(LogLevel.WARN, $"File locked by {toolName} — opening read-only: {UnifiedLogger.SanitizePath(filePath)}");
                UpdateStatus($"File is open in {toolName} — opening read-only");
                _documentState.IsReadOnly = true;
            }

            // Set loading flag to prevent MarkDirty() from firing during panel population
            _isLoading = true;
            ShowProgress(true);
            UpdateStatus($"Loading {Path.GetFileName(filePath)}...");

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            _isBicFile = extension == ".bic";

            // #2252 — actually-async parse: BicReader/UtcReader do blocking disk I/O
            // plus full GFF parse. Push to Task.Run so the UI thread stays responsive
            // (deep-inventory BIC parses were freezing the window).
            var isBic = _isBicFile;
            var creature = await Task.Run<UtcFile>(() =>
                isBic ? BicReader.Read(filePath) : UtcReader.Read(filePath));

            _currentCreature = creature;
            if (isBic)
            {
                UnifiedLogger.LogCreature(LogLevel.INFO, $"Loaded BIC (player character): {UnifiedLogger.SanitizePath(filePath)}");
            }
            else
            {
                UnifiedLogger.LogCreature(LogLevel.INFO, $"Loaded UTC (creature blueprint): {UnifiedLogger.SanitizePath(filePath)}");
            }

            _currentFilePath = filePath;

            // Infer module path from file location (#1208)
            if (RadoubSettings.Instance.TryInferModuleFromFile(filePath))
            {
                var panel = this.FindControl<CreatureBrowserPanel>("CreatureBrowserPanel");
                if (panel != null)
                    panel.ModulePath = Path.GetDirectoryName(filePath);
            }

            // Update panels with current file context for dialog/script browsing
            CharacterPanelContent.SetCurrentFilePath(_currentFilePath);
            ScriptsPanelContent.SetCurrentFilePath(_currentFilePath);

            // Update advanced panel with module directory for faction loading
            var moduleDirectory = Path.GetDirectoryName(_currentFilePath);
            AdvancedPanelContent.SetModuleDirectory(moduleDirectory);

            // Detect module switch and reconfigure HAKs (#1867, #1869)
            // When loading a file from a different module, stale HAK resources
            // (textures, models) can cause rendering issues like reversed bat wings.
            if (_gameDataService != null && moduleDirectory != null &&
                !string.Equals(_lastLoadedModuleDir, moduleDirectory, StringComparison.OrdinalIgnoreCase))
            {
                if (_lastLoadedModuleDir != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Module switch detected: reconfiguring HAKs for {Path.GetFileName(moduleDirectory)}");
                    _gameDataService.ConfigureModuleHaks(moduleDirectory);
                    AppearancePanelContent.ClearResourceCaches();
                }
                _lastLoadedModuleDir = moduleDirectory;
                UpdateModuleIndicator();
            }

            PopulateInventoryUI();
            UpdateCharacterHeader();
            LoadAllPanels(_currentCreature);
            UpdateInventoryCounts();
            OnPropertyChanged(nameof(HasFile));

            // Defer clearing the loading guard until after the panels' own deferred IsLoading resets
            // (BasePanelControl.DeferLoadingReset, Background priority) so populate-time change events
            // can't slip past the guard and mark the fresh document dirty (#2459).
            // Note: between LoadAllPanels returning and this Background callback draining, _isLoading
            // stays true, so a (practically unreachable, sub-frame) user edit would be suppressed.
            // That bounded gap is intentional — the alternative is the #2459 false-dirty race.
            Quartermaster.Services.DeferredGuardReset.Post(() =>
            {
                _isLoading = false;
                _documentState.ClearDirty();
                UpdateTitle();
            });
            ShowProgress(false);
            UpdateStatus($"Loaded: {Path.GetFileName(filePath)}");

            SettingsService.Instance.AddRecentFile(filePath);
            UpdateRecentFilesMenu();

            // Update search bar file path
            this.FindControl<SearchBar>("FileSearchBar")?.UpdateFilePath(filePath);
        }
        catch (Exception ex)
        {
            _isLoading = false;
            ShowProgress(false);
            UnifiedLogger.LogCreature(LogLevel.ERROR, $"Failed to load creature: {ex.Message}");
            UpdateStatus($"Error loading file: {ex.Message}");
            ShowErrorDialog("Load Error", $"Failed to load creature file:\n{ex.Message}");
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

        if (_documentState.IsReadOnly)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Save blocked: file is read-only (locked by another tool): {UnifiedLogger.SanitizePath(_currentFilePath)}");
            UpdateStatus("Cannot save: file is open read-only (locked by another tool).");
            return;
        }

        // Block save if duplicate variable names exist
        if (AdvancedPanelContent.HasDuplicateVariableErrors())
        {
            UpdateStatus("Cannot save: duplicate variable names. Fix variable names before saving.");
            return;
        }

        try
        {
            ShowProgress(true);
            UpdateStatus($"Saving {Path.GetFileName(_currentFilePath)}...");

            // Sync UI state to creature model before saving
            SyncInventoryToCreature();
            AdvancedPanelContent.UpdateVarTable();

            if (_isBicFile && _currentCreature is BicFile bicFile)
            {
                BicWriter.Write(bicFile, _currentFilePath);
            }
            else
            {
                UtcWriter.Write(_currentCreature, _currentFilePath);
            }

            _documentState.ClearDirty();
            UpdateTitle();
            ShowProgress(false);
            UpdateStatus($"Saved: {Path.GetFileName(_currentFilePath)}");

            // Refresh browser row Tag/Name without full reindex (#2201).
            // Fire-and-forget — save flow does not block on UI refresh.
            var creatureBrowserPanel = this.FindControl<CreatureBrowserPanel>("CreatureBrowserPanel");
            _ = Radoub.UI.Controls.BrowserSaveNotifier.NotifyAsync(creatureBrowserPanel, _currentFilePath);

            UnifiedLogger.LogCreature(LogLevel.INFO, $"Saved creature: {UnifiedLogger.SanitizePath(_currentFilePath)}");
        }
        catch (Exception ex)
        {
            ShowProgress(false);
            UnifiedLogger.LogCreature(LogLevel.ERROR, $"Failed to save creature: {ex.Message}");
            UpdateStatus($"Error saving file: {ex.Message}");
            ShowErrorDialog("Save Error", $"Failed to save creature file:\n{ex.Message}");
        }
    }

    private async Task SaveFileAs()
    {
        if (_currentCreature == null) return;

        // Convert to the shared save dialog (#2515). The dialog supports multiple extensions
        // and returns which one the user chose in Result.Extension.
        var firstExt = _isBicFile ? "bic" : "utc";
        var opts = new Radoub.UI.Services.SaveBlueprintOptions(
            Title: _isBicFile ? "Save Player Character — Quartermaster" : "Save Creature As — Quartermaster",
            Extensions: firstExt == "bic" ? new[] { "bic", "utc" } : new[] { "utc", "bic" },
            DefaultResRef: Path.GetFileNameWithoutExtension(_currentFilePath ?? "creature"),
            Context: new QuartermasterScriptBrowserContext(_currentFilePath, GameData),
            DefaultDirectoryByExtension: new Dictionary<string, string?> { ["utc"] = ResolveModuleDir(), ["bic"] = ResolveLocalVaultDir() });
        var win = new Radoub.UI.Views.SaveBlueprintWindow(opts);
        await win.ShowDialog(this);
        if (win.Result is not { } saveResult) return; // cancel — leave state untouched

        _currentFilePath = saveResult.Path;
        var savingAsBic = saveResult.Extension == "bic";

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
            _currentCreature = BicFile.FromUtcFile(_currentCreature, GameData);
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

            // ============================================================
            // PORTRAIT FIX FOR AURORA TOOLSET (BIC → UTC)
            // ============================================================
            // BIC files use Portrait string (e.g., "po_hu_m_01_") with PortraitId=0
            // Aurora Toolset shows "must specify valid portrait" error if PortraitId=0
            // Look up the PortraitId from portraits.2da using the Portrait string
            if (_currentCreature.PortraitId == 0 && !string.IsNullOrEmpty(_currentCreature.Portrait))
            {
                var foundId = DisplayService.FindPortraitIdByResRef(_currentCreature.Portrait);
                if (foundId.HasValue)
                {
                    _currentCreature.PortraitId = foundId.Value;
                    UnifiedLogger.LogCreature(LogLevel.DEBUG,
                        $"Portrait lookup: '{_currentCreature.Portrait}' → PortraitId {foundId.Value}");
                }
                else
                {
                    UnifiedLogger.LogCreature(LogLevel.WARN,
                        $"Portrait lookup failed: '{_currentCreature.Portrait}' not found in portraits.2da");
                }
            }

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

        // Refresh creature browser so the new file appears (#1690)
        UpdateCreatureBrowserCurrentFile(_currentFilePath);
        var creatureBrowserPanel = this.FindControl<CreatureBrowserPanel>("CreatureBrowserPanel");
        if (creatureBrowserPanel != null)
            await creatureBrowserPanel.RefreshAsync();

        // If format was converted, reload panels to reflect the new file type
        if (wasConverted)
        {
            _isLoading = true;
            LoadAllPanels(_currentCreature);
            _isLoading = false;
            _documentState.ClearDirty(); // Reset dirty after panel reload
            UpdateTitle();
            UpdateStatus($"Converted and saved as {(savingAsBic ? "BIC" : "UTC")}: {Path.GetFileName(_currentFilePath)}");
        }
    }

    /// <summary>
    /// Resolves the default UTC save directory: the current module directory, or the .mod
    /// working directory when the module path is a packed .mod file. Null when unresolved.
    /// </summary>
    private static string? ResolveModuleDir()
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (string.IsNullOrEmpty(modulePath)) return null;
        if (Directory.Exists(modulePath)) return modulePath;
        if (File.Exists(modulePath))
            return PathHelper.FindWorkingDirectoryWithFallbacks(modulePath);
        return null;
    }

    /// <summary>Resolves the default BIC save directory (localvault) if it exists, else null.</summary>
    private static string? ResolveLocalVaultDir()
    {
        var nwnPath = RadoubSettings.Instance.NeverwinterNightsPath;
        if (string.IsNullOrEmpty(nwnPath)) return null;
        var localVault = Path.Combine(nwnPath, "localvault");
        return Directory.Exists(localVault) ? localVault : null;
    }

    /// <summary>
    /// Prompts the user to save a newly created creature immediately after wizard creation
    /// via the shared in-app Save dialog (#2515). Per-extension defaults: BIC→localvault,
    /// UTC→module directory (overwrite confirmation is built into the dialog).
    /// </summary>
    private async Task PromptSaveNewCreature()
    {
        if (_currentCreature == null) return;

        // Generate suggested filename from creature's ResRef or tag
        var suggestedName = _currentCreature.TemplateResRef;
        if (string.IsNullOrEmpty(suggestedName) || suggestedName == "new_creature")
            suggestedName = _currentCreature.Tag ?? "new_creature";

        var moduleDir = ResolveModuleDir();
        var localVault = ResolveLocalVaultDir();
        var firstExt = _isBicFile ? "bic" : "utc";
        var opts = new Radoub.UI.Services.SaveBlueprintOptions(
            Title: _isBicFile ? "Save Player Character — Quartermaster" : "Save Creature Blueprint — Quartermaster",
            Extensions: firstExt == "bic" ? new[] { "bic", "utc" } : new[] { "utc", "bic" },
            DefaultResRef: suggestedName,
            Context: new QuartermasterScriptBrowserContext(_currentFilePath, GameData),
            DefaultDirectoryByExtension: new Dictionary<string, string?> { ["utc"] = moduleDir, ["bic"] = localVault });
        var win = new Radoub.UI.Views.SaveBlueprintWindow(opts);
        await win.ShowDialog(this);
        if (win.Result is not { } saveResult)
        {
            UpdateStatus("New creature created (not saved yet)");
            return;
        }

        _currentFilePath = saveResult.Path;
        var savingAsBic = saveResult.Extension == "bic";

        if (!await ValidateAuroraFilename(_currentFilePath))
        {
            _currentFilePath = null;
            UpdateStatus("New creature created (not saved yet)");
            return;
        }

        // Convert to BIC if saving as .bic and creature is still a UtcFile
        if (savingAsBic && _currentCreature is not BicFile)
        {
            _currentCreature = BicFile.FromUtcFile(_currentCreature, GameData);
            _isBicFile = true;
        }

        await SaveFile();
        UpdateTitle();
        UpdateCharacterHeader();
        SettingsService.Instance.AddRecentFile(_currentFilePath);
        UpdateRecentFilesMenu();

        // Refresh creature browser so the new file appears (#1477)
        UpdateCreatureBrowserCurrentFile(_currentFilePath);
        var creatureBrowserPanel = this.FindControl<CreatureBrowserPanel>("CreatureBrowserPanel");
        if (creatureBrowserPanel != null)
            await creatureBrowserPanel.RefreshAsync();
    }

    /// <summary>
    /// Close file with unsaved changes check. Returns true if file was closed.
    /// </summary>
    private async Task<bool> CloseFileWithCheck()
    {
        try
        {
            // Use shared helper for unsaved changes dialog
            var dirtyResult = await Radoub.UI.Services.FileOperationsHelper.CheckDirtyAsync(this, _documentState);
            if (dirtyResult == Radoub.UI.Services.DirtyCheckResult.Cancel) return false;
            if (dirtyResult == Radoub.UI.Services.DirtyCheckResult.Save) await SaveFile();

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
        _documentState.ClearDirty();
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

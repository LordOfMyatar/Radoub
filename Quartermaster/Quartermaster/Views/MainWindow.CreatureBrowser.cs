using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.Formats.Common;
using Radoub.Formats.Ifo;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.Formats.Utc;
using Quartermaster.Services;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using DialogHelper = Quartermaster.Views.Helpers.DialogHelper;

namespace Quartermaster.Views;

/// <summary>
/// Creature Browser Panel: initialization, visibility, file selection, and delete (#1145).
/// </summary>
public partial class MainWindow
{
    #region Creature Browser Panel (#1145)

    /// <summary>
    /// Initializes creature browser panel with context and event handlers (#1145).
    /// </summary>
    private void InitializeCreatureBrowserPanel()
    {
        var creatureBrowserPanel = this.FindControl<CreatureBrowserPanel>("CreatureBrowserPanel");
        if (creatureBrowserPanel == null)
        {
            UnifiedLogger.LogUI(LogLevel.WARN, "CreatureBrowserPanel not found");
            return;
        }

        // Note: GameDataService is set later in InitializePanels() after services are ready (#1133)

        // Set initial module path from RadoubSettings (set by Trebuchet)
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (RadoubSettings.IsValidModulePath(modulePath))
        {
            // If it's a .mod file, find the working directory
            if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
            {
                modulePath = FindWorkingDirectory(modulePath);
            }

            if (!string.IsNullOrEmpty(modulePath) && Directory.Exists(modulePath))
            {
                creatureBrowserPanel.ModulePath = modulePath;
                UnifiedLogger.LogUI(LogLevel.INFO, "CreatureBrowserPanel initialized with module path from Trebuchet");
            }
        }

        // Subscribe to file selection events
        creatureBrowserPanel.FileSelected += OnCreatureBrowserFileSelected;

        // Subscribe to file delete events (#1368)
        creatureBrowserPanel.FileDeleteRequested += OnCreatureBrowserFileDeleteRequested;

        // Subscribe to collapse/expand events
        creatureBrowserPanel.CollapsedChanged += OnCreatureBrowserCollapsedChanged;

        // Restore panel state from settings
        RestoreCreatureBrowserPanelState();

        // Update menu item checkmark
        UpdateCreatureBrowserMenuState();

        UnifiedLogger.LogUI(LogLevel.INFO, "CreatureBrowserPanel initialized");
    }

    /// <summary>
    /// Find the unpacked working directory for a .mod file.
    /// Checks for module name folder, temp0, or temp1.
    /// </summary>
    private static string? FindWorkingDirectory(string modFilePath)
    {
        var moduleName = Path.GetFileNameWithoutExtension(modFilePath);
        var moduleDir = Path.GetDirectoryName(modFilePath);

        if (string.IsNullOrEmpty(moduleDir))
            return null;

        // Check in priority order (same as Trebuchet)
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

    /// <summary>
    /// Update status bar module indicator from RadoubSettings (#1003).
    /// Shows module name or "No module selected" in warning colors.
    /// </summary>
    private void UpdateModuleIndicator()
    {
        var moduleText = this.FindControl<TextBlock>("ModuleText");
        if (moduleText == null) return;

        try
        {
            var modulePath = RadoubSettings.Instance.CurrentModulePath;

            // Validate this is a real module path, not just the modules parent directory (#1327)
            if (!RadoubSettings.IsValidModulePath(modulePath))
            {
                moduleText.Text = "No module selected";
                moduleText.Foreground = BrushManager.GetWarningBrush(this);
                return;
            }

            // Resolve .mod to working directory
            if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
                modulePath = FindWorkingDirectory(modulePath);

            if (string.IsNullOrEmpty(modulePath) || !Directory.Exists(modulePath))
            {
                moduleText.Text = "No module selected";
                moduleText.Foreground = BrushManager.GetWarningBrush(this);
                return;
            }

            // Extract module name from module.ifo
            var ifoPath = Path.Combine(modulePath, "module.ifo");
            string? moduleName = null;
            if (File.Exists(ifoPath))
            {
                var ifo = IfoReader.Read(ifoPath);
                moduleName = ifo.ModuleName.GetDefault();
            }

            moduleText.Text = $"Module: {moduleName ?? Path.GetFileName(modulePath)}";
            moduleText.Foreground = BrushManager.GetInfoBrush(this);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogUI(LogLevel.WARN, $"Failed to update module indicator: {ex.Message}");
            moduleText.Text = "No module selected";
            moduleText.Foreground = BrushManager.GetWarningBrush(this);
        }
    }

    /// <summary>
    /// Restores creature browser panel state from settings (#1145).
    /// </summary>
    private void RestoreCreatureBrowserPanelState()
    {
        var settings = SettingsService.Instance;
        var outerContentGrid = this.FindControl<Grid>("OuterContentGrid");
        var creatureBrowserPanel = this.FindControl<CreatureBrowserPanel>("CreatureBrowserPanel");
        var creatureBrowserSplitter = this.FindControl<GridSplitter>("CreatureBrowserSplitter");

        if (outerContentGrid == null || creatureBrowserPanel == null || creatureBrowserSplitter == null)
            return;

        var creatureBrowserColumn = outerContentGrid.ColumnDefinitions[0];
        var creatureBrowserSplitterColumn = outerContentGrid.ColumnDefinitions[1];

        if (settings.CreatureBrowserPanelVisible)
        {
            creatureBrowserColumn.Width = new GridLength(settings.CreatureBrowserPanelWidth, GridUnitType.Pixel);
            creatureBrowserSplitterColumn.Width = new GridLength(5, GridUnitType.Pixel);
            creatureBrowserPanel.IsVisible = true;
            creatureBrowserSplitter.IsVisible = true;
        }
        else
        {
            creatureBrowserColumn.Width = new GridLength(0, GridUnitType.Pixel);
            creatureBrowserSplitterColumn.Width = new GridLength(0, GridUnitType.Pixel);
            creatureBrowserPanel.IsVisible = false;
            creatureBrowserSplitter.IsVisible = false;
        }
    }

    /// <summary>
    /// Saves creature browser panel width to settings (#1145).
    /// </summary>
    private void SaveCreatureBrowserPanelSize()
    {
        var outerContentGrid = this.FindControl<Grid>("OuterContentGrid");
        if (outerContentGrid == null) return;

        var creatureBrowserColumn = outerContentGrid.ColumnDefinitions[0];
        if (creatureBrowserColumn.Width.IsAbsolute && creatureBrowserColumn.Width.Value > 0)
        {
            SettingsService.Instance.CreatureBrowserPanelWidth = creatureBrowserColumn.Width.Value;
        }
    }

    /// <summary>
    /// Sets creature browser panel visibility (#1145).
    /// </summary>
    private void SetCreatureBrowserPanelVisible(bool visible)
    {
        var settings = SettingsService.Instance;
        settings.CreatureBrowserPanelVisible = visible;

        var outerContentGrid = this.FindControl<Grid>("OuterContentGrid");
        var creatureBrowserPanel = this.FindControl<CreatureBrowserPanel>("CreatureBrowserPanel");
        var creatureBrowserSplitter = this.FindControl<GridSplitter>("CreatureBrowserSplitter");

        if (outerContentGrid == null || creatureBrowserPanel == null || creatureBrowserSplitter == null)
            return;

        var creatureBrowserColumn = outerContentGrid.ColumnDefinitions[0];
        var creatureBrowserSplitterColumn = outerContentGrid.ColumnDefinitions[1];

        if (visible)
        {
            creatureBrowserColumn.Width = new GridLength(settings.CreatureBrowserPanelWidth, GridUnitType.Pixel);
            creatureBrowserSplitterColumn.Width = new GridLength(5, GridUnitType.Pixel);
            creatureBrowserPanel.IsVisible = true;
            creatureBrowserSplitter.IsVisible = true;
        }
        else
        {
            // Save current width before hiding
            if (creatureBrowserColumn.Width.IsAbsolute && creatureBrowserColumn.Width.Value > 0)
            {
                settings.CreatureBrowserPanelWidth = creatureBrowserColumn.Width.Value;
            }

            creatureBrowserColumn.Width = new GridLength(0, GridUnitType.Pixel);
            creatureBrowserSplitterColumn.Width = new GridLength(0, GridUnitType.Pixel);
            creatureBrowserPanel.IsVisible = false;
            creatureBrowserSplitter.IsVisible = false;
        }

        UpdateCreatureBrowserMenuState();
    }

    /// <summary>
    /// Handles collapse/expand button clicks from CreatureBrowserPanel (#1145).
    /// </summary>
    private void OnCreatureBrowserCollapsedChanged(object? sender, bool isCollapsed)
    {
        SetCreatureBrowserPanelVisible(!isCollapsed);
    }

    /// <summary>
    /// Updates the CreatureBrowserPanel's current file highlight (#1145).
    /// Only updates ModulePath for files actually in a module directory,
    /// not for vault/HAK files which would corrupt the module scan path.
    /// </summary>
    private void UpdateCreatureBrowserCurrentFile(string? filePath)
    {
        var creatureBrowserPanel = this.FindControl<CreatureBrowserPanel>("CreatureBrowserPanel");
        if (creatureBrowserPanel != null)
        {
            creatureBrowserPanel.CurrentFilePath = filePath;

            // Only update module path for module files - vault/HAK files live
            // elsewhere and setting ModulePath to their directory would trigger
            // a full refresh that loses all module UTC entries.
            if (!string.IsNullOrEmpty(filePath))
            {
                var fileDir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(fileDir) &&
                    (string.IsNullOrEmpty(creatureBrowserPanel.ModulePath) ||
                     fileDir.Equals(creatureBrowserPanel.ModulePath, StringComparison.OrdinalIgnoreCase)))
                {
                    creatureBrowserPanel.ModulePath = fileDir;
                }
            }
        }
    }

    /// <summary>
    /// Handles file selection in the creature browser panel (#1145).
    /// </summary>
    private async void OnCreatureBrowserFileSelected(object? sender, FileSelectedEventArgs e)
    {
        // Only load on single click (per issue requirements)
        if (e.Entry is CreatureBrowserEntry cbe && (cbe.IsFromBif || e.Entry.IsFromHak))
        {
            await LoadArchiveCreature(cbe);
            return;
        }

        if (string.IsNullOrEmpty(e.Entry.FilePath))
        {
            UnifiedLogger.LogUI(LogLevel.WARN, $"CreatureBrowserPanel: No file path for {e.Entry.Name}");
            return;
        }

        // Skip if this is already the loaded file
        if (string.Equals(_currentFilePath, e.Entry.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"CreatureBrowserPanel: File already loaded: {e.Entry.Name}");
            return;
        }

        // Auto-save if dirty — browser panels use silent auto-save for fluid navigation (#1535)
        // (File > Open and window close use explicit Save/Discard/Cancel prompts instead)
        string? autoSavedFileName = null;
        if (_isDirty && _currentCreature != null && !string.IsNullOrEmpty(_currentFilePath))
        {
            autoSavedFileName = Path.GetFileName(_currentFilePath);
            await SaveFile();
        }

        // Load the selected file
        await LoadFile(e.Entry.FilePath);

        // Show combined status so user sees the auto-save happened
        if (autoSavedFileName != null)
        {
            UpdateStatus($"Auto-saved {autoSavedFileName} · Loaded: {Path.GetFileName(e.Entry.FilePath)}");
        }

        // Update the current file highlight
        UpdateCreatureBrowserCurrentFile(e.Entry.FilePath);
    }

    /// <summary>
    /// Loads a HAK or BIF creature for read-only viewing (#1133).
    /// Extracts UTC data from archive via GameDataService.FindResource
    /// (searches Override → HAK → BIF in priority order).
    /// </summary>
    private Task LoadArchiveCreature(CreatureBrowserEntry entry)
    {
        if (_gameDataService == null || !_gameDataService.IsConfigured)
        {
            UpdateStatus("Game data not configured — cannot load archive creatures");
            return Task.CompletedTask;
        }

        var sourceLabel = entry.IsFromBif ? "base game" : "HAK";

        try
        {
            _isLoading = true;
            ShowProgress(true);
            UpdateStatus($"Loading {sourceLabel} creature: {entry.Name}...");

            var data = _gameDataService.FindResource(entry.Name, ResourceTypes.Utc);
            if (data == null)
            {
                UpdateStatus($"Could not extract {entry.Name} from {sourceLabel} archives");
                return Task.CompletedTask;
            }

            _currentCreature = UtcReader.Read(data);
            _currentFilePath = null; // No file path — read-only archive resource
            _isBicFile = false;

            PopulateInventoryUI();
            UpdateCharacterHeader();
            LoadAllPanels(_currentCreature);
            UpdateInventoryCounts();
            OnPropertyChanged(nameof(HasFile));

            _isLoading = false;
            _documentState.ClearDirty();
            UpdateTitle();
            ShowProgress(false);
            UpdateStatus($"{sourceLabel} creature (read-only): {entry.Name}");
        }
        catch (Exception ex)
        {
            _isLoading = false;
            ShowProgress(false);
            UnifiedLogger.LogCreature(LogLevel.ERROR, $"Failed to load {sourceLabel} creature {entry.Name}: {ex.Message}");
            UpdateStatus($"Error loading {sourceLabel} creature: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles file delete request from creature browser panel (#1368).
    /// Shows confirmation dialog, deletes file, and refreshes list.
    /// </summary>
    private async void OnCreatureBrowserFileDeleteRequested(object? sender, FileDeleteRequestedEventArgs e)
    {
        var entry = e.Entry;
        if (string.IsNullOrEmpty(entry.FilePath) || !File.Exists(entry.FilePath))
        {
            UpdateStatus("File not found on disk");
            return;
        }

        var fileName = Path.GetFileName(entry.FilePath);

        // Modal confirmation dialog (destructive action)
        var confirmed = await DialogHelper.ShowConfirmationDialog(
            this, "Confirm Delete", $"Delete \"{fileName}\" from disk?\n\nThis cannot be undone.");
        if (!confirmed)
            return;

        try
        {
            var isDeletingCurrent = string.Equals(_currentFilePath, entry.FilePath, StringComparison.OrdinalIgnoreCase);

            File.Delete(entry.FilePath);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Deleted creature file: {fileName}");

            if (isDeletingCurrent)
            {
                CloseFile();
            }

            UpdateStatus($"Deleted {fileName}");

            // Refresh the creature browser panel
            var creatureBrowserPanel = this.FindControl<CreatureBrowserPanel>("CreatureBrowserPanel");
            if (creatureBrowserPanel != null)
            {
                await creatureBrowserPanel.RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to delete {fileName}: {ex.Message}");
            UpdateStatus($"Delete failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Toggles creature browser panel visibility from View menu (#1145).
    /// </summary>
    private void OnToggleCreatureBrowserClick(object? sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance;
        SetCreatureBrowserPanelVisible(!settings.CreatureBrowserPanelVisible);
    }

    /// <summary>
    /// Updates View menu checkmark for Creature Browser item (#1145).
    /// </summary>
    private void UpdateCreatureBrowserMenuState()
    {
        var menuItem = this.FindControl<MenuItem>("CreatureBrowserMenuItem");
        if (menuItem != null)
        {
            var isVisible = SettingsService.Instance.CreatureBrowserPanelVisible;
            menuItem.Icon = isVisible ? new TextBlock { Text = "✓" } : null;
        }
    }

    #endregion
}

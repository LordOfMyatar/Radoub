using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MerchantEditor.Services;
using Radoub.Formats.Ifo;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Controls;
using System;
using System.IO;
using System.Linq;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: Store browser panel initialization, visibility, and event handling (#1144, #1367).
/// </summary>
public partial class MainWindow
{
    #region Store Browser Panel (#1144)

    /// <summary>
    /// Initializes store browser panel with context and event handlers (#1144).
    /// </summary>
    private void InitializeStoreBrowserPanel()
    {
        var storeBrowserPanel = this.FindControl<StoreBrowserPanel>("StoreBrowserPanel");
        if (storeBrowserPanel == null)
        {
            UnifiedLogger.LogUI(LogLevel.WARN, "StoreBrowserPanel not found");
            return;
        }

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
                storeBrowserPanel.ModulePath = modulePath;
                UnifiedLogger.LogUI(LogLevel.INFO, $"StoreBrowserPanel initialized with module path from Trebuchet");
            }
        }

        // Subscribe to file selection events
        storeBrowserPanel.FileSelected += OnStoreBrowserFileSelected;

        // Subscribe to file delete events (#1367)
        storeBrowserPanel.FileDeleteRequested += OnStoreBrowserFileDeleteRequested;

        // Subscribe to collapse/expand events
        storeBrowserPanel.CollapsedChanged += OnStoreBrowserCollapsedChanged;

        // Restore panel state from settings
        RestoreStoreBrowserPanelState();

        // Update menu item checkmark
        UpdateStoreBrowserMenuState();

        UnifiedLogger.LogUI(LogLevel.INFO, "StoreBrowserPanel initialized");
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
        try
        {
            var modulePath = RadoubSettings.Instance.CurrentModulePath;

            // Validate this is a real module path, not just the modules parent directory (#1327)
            if (!RadoubSettings.IsValidModulePath(modulePath))
            {
                StatusBar.ModuleIndicator = "No module selected";
                return;
            }

            // Resolve .mod to working directory
            if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
                modulePath = FindWorkingDirectory(modulePath);

            if (string.IsNullOrEmpty(modulePath) || !Directory.Exists(modulePath))
            {
                StatusBar.ModuleIndicator = "No module selected";
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

            StatusBar.ModuleIndicator = $"Module: {moduleName ?? Path.GetFileName(modulePath)}";
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogUI(LogLevel.WARN, $"Failed to update module indicator: {ex.Message}");
            StatusBar.ModuleIndicator = "No module selected";
        }
    }

    /// <summary>
    /// Restores store browser panel state from settings (#1144).
    /// </summary>
    private void RestoreStoreBrowserPanelState()
    {
        var settings = SettingsService.Instance;
        var outerContentGrid = this.FindControl<Grid>("OuterContentGrid");
        var storeBrowserPanel = this.FindControl<StoreBrowserPanel>("StoreBrowserPanel");
        var storeBrowserSplitter = this.FindControl<GridSplitter>("StoreBrowserSplitter");

        if (outerContentGrid == null || storeBrowserPanel == null || storeBrowserSplitter == null)
            return;

        var storeBrowserColumn = outerContentGrid.ColumnDefinitions[0];
        var storeBrowserSplitterColumn = outerContentGrid.ColumnDefinitions[1];

        if (settings.StoreBrowserPanelVisible)
        {
            storeBrowserColumn.Width = new GridLength(settings.StoreBrowserPanelWidth, GridUnitType.Pixel);
            storeBrowserSplitterColumn.Width = new GridLength(5, GridUnitType.Pixel);
            storeBrowserPanel.IsVisible = true;
            storeBrowserSplitter.IsVisible = true;
        }
        else
        {
            storeBrowserColumn.Width = new GridLength(0, GridUnitType.Pixel);
            storeBrowserSplitterColumn.Width = new GridLength(0, GridUnitType.Pixel);
            storeBrowserPanel.IsVisible = false;
            storeBrowserSplitter.IsVisible = false;
        }
    }

    /// <summary>
    /// Saves store browser panel width to settings (#1144).
    /// </summary>
    private void SaveStoreBrowserPanelSize()
    {
        var outerContentGrid = this.FindControl<Grid>("OuterContentGrid");
        if (outerContentGrid == null) return;

        var storeBrowserColumn = outerContentGrid.ColumnDefinitions[0];
        if (storeBrowserColumn.Width.IsAbsolute && storeBrowserColumn.Width.Value > 0)
        {
            SettingsService.Instance.StoreBrowserPanelWidth = storeBrowserColumn.Width.Value;
        }
    }

    /// <summary>
    /// Sets store browser panel visibility (#1144).
    /// </summary>
    private void SetStoreBrowserPanelVisible(bool visible)
    {
        var settings = SettingsService.Instance;
        settings.StoreBrowserPanelVisible = visible;

        var outerContentGrid = this.FindControl<Grid>("OuterContentGrid");
        var storeBrowserPanel = this.FindControl<StoreBrowserPanel>("StoreBrowserPanel");
        var storeBrowserSplitter = this.FindControl<GridSplitter>("StoreBrowserSplitter");

        if (outerContentGrid == null || storeBrowserPanel == null || storeBrowserSplitter == null)
            return;

        var storeBrowserColumn = outerContentGrid.ColumnDefinitions[0];
        var storeBrowserSplitterColumn = outerContentGrid.ColumnDefinitions[1];

        if (visible)
        {
            storeBrowserColumn.Width = new GridLength(settings.StoreBrowserPanelWidth, GridUnitType.Pixel);
            storeBrowserSplitterColumn.Width = new GridLength(5, GridUnitType.Pixel);
            storeBrowserPanel.IsVisible = true;
            storeBrowserSplitter.IsVisible = true;
        }
        else
        {
            // Save current width before hiding
            if (storeBrowserColumn.Width.IsAbsolute && storeBrowserColumn.Width.Value > 0)
            {
                settings.StoreBrowserPanelWidth = storeBrowserColumn.Width.Value;
            }

            storeBrowserColumn.Width = new GridLength(0, GridUnitType.Pixel);
            storeBrowserSplitterColumn.Width = new GridLength(0, GridUnitType.Pixel);
            storeBrowserPanel.IsVisible = false;
            storeBrowserSplitter.IsVisible = false;
        }

        UpdateStoreBrowserMenuState();
    }

    /// <summary>
    /// Handles collapse/expand button clicks from StoreBrowserPanel (#1144).
    /// </summary>
    private void OnStoreBrowserCollapsedChanged(object? sender, bool isCollapsed)
    {
        SetStoreBrowserPanelVisible(!isCollapsed);
    }

    /// <summary>
    /// Updates the StoreBrowserPanel's current file highlight (#1144).
    /// </summary>
    private void UpdateStoreBrowserCurrentFile(string? filePath)
    {
        var storeBrowserPanel = this.FindControl<StoreBrowserPanel>("StoreBrowserPanel");
        if (storeBrowserPanel != null)
        {
            storeBrowserPanel.CurrentFilePath = filePath;

            // Update module path if we have a file
            if (!string.IsNullOrEmpty(filePath))
            {
                var modulePath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(modulePath))
                {
                    storeBrowserPanel.ModulePath = modulePath;
                }
            }
        }
    }

    /// <summary>
    /// Handles file selection in the store browser panel (#1144).
    /// </summary>
    private async void OnStoreBrowserFileSelected(object? sender, FileSelectedEventArgs e)
    {
        try
        {
            // Only load on single click (per issue requirements)
            if (e.Entry.IsFromHak)
            {
                // HAK files can't be edited directly - show info
                UpdateStatusBar($"HAK stores are read-only: {e.Entry.Name}");
                return;
            }

            if (string.IsNullOrEmpty(e.Entry.FilePath))
            {
                UnifiedLogger.LogUI(LogLevel.WARN, $"StoreBrowserPanel: No file path for {e.Entry.Name}");
                return;
            }

            // Skip if this is already the loaded file
            if (string.Equals(_currentFilePath, e.Entry.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"StoreBrowserPanel: File already loaded: {e.Entry.Name}");
                return;
            }

            // Auto-save if dirty
            if (_isDirty && _currentStore != null && !string.IsNullOrEmpty(_currentFilePath))
            {
                UpdateStatusBar("Auto-saving...");
                await SaveFileAsync(_currentFilePath);
            }

            // Load the selected file
            LoadFile(e.Entry.FilePath);

            // Update the current file highlight
            UpdateStoreBrowserCurrentFile(e.Entry.FilePath);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading store from browser: {ex.Message}");
            UpdateStatusBar($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles file delete request from store browser panel (#1367).
    /// Shows confirmation dialog, deletes file, and refreshes list.
    /// </summary>
    private async void OnStoreBrowserFileDeleteRequested(object? sender, FileDeleteRequestedEventArgs e)
    {
        var entry = e.Entry;
        if (string.IsNullOrEmpty(entry.FilePath) || !File.Exists(entry.FilePath))
        {
            UpdateStatusBar("File not found on disk");
            return;
        }

        var fileName = Path.GetFileName(entry.FilePath);

        // Modal confirmation dialog (destructive action - modal OK per CLAUDE.md)
        var confirmed = await ShowDeleteConfirmationAsync(fileName);
        if (!confirmed)
            return;

        try
        {
            // If this is the currently loaded file, clear the editor first
            var isDeletingCurrent = string.Equals(_currentFilePath, entry.FilePath, StringComparison.OrdinalIgnoreCase);

            File.Delete(entry.FilePath);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Deleted store file: {fileName}");

            if (isDeletingCurrent)
            {
                _currentStore = null;
                _currentFilePath = null;
                _isDirty = false;
                StoreItems.Clear();
                UpdateTitle();
                UpdateItemCount();
                UpdateStatusBar($"Deleted {fileName}");
            }
            else
            {
                UpdateStatusBar($"Deleted {fileName}");
            }

            // Refresh the store browser panel
            var storeBrowserPanel = this.FindControl<StoreBrowserPanel>("StoreBrowserPanel");
            if (storeBrowserPanel != null)
            {
                await storeBrowserPanel.RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to delete {fileName}: {ex.Message}");
            UpdateStatusBar($"Delete failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows a modal confirmation dialog for file deletion.
    /// Returns true if user confirms, false if cancelled.
    /// </summary>
    private async System.Threading.Tasks.Task<bool> ShowDeleteConfirmationAsync(string fileName)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();

        var dialog = new Window
        {
            Title = "Confirm Delete",
            Width = 380,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Delete \"{fileName}\" from disk?\n\nThis cannot be undone.",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            new Button { Content = "Delete", Tag = "delete", Width = 80 },
                            new Button { Content = "Cancel", Tag = "cancel", Width = 80 }
                        }
                    }
                }
            }
        };

        if (dialog.Content is StackPanel outerPanel
            && outerPanel.Children.LastOrDefault() is StackPanel buttonPanel)
        {
            foreach (var child in buttonPanel.Children)
            {
                if (child is Button btn)
                {
                    btn.Click += (s, e) =>
                    {
                        tcs.TrySetResult(btn.Tag?.ToString() == "delete");
                        dialog.Close();
                    };
                }
            }
        }

        dialog.Closed += (s, e) => tcs.TrySetResult(false);

        await dialog.ShowDialog(this);
        return await tcs.Task;
    }

    /// <summary>
    /// Toggles store browser panel visibility from View menu (#1144).
    /// </summary>
    private void OnToggleStoreBrowserClick(object? sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance;
        SetStoreBrowserPanelVisible(!settings.StoreBrowserPanelVisible);
    }

    /// <summary>
    /// Updates View menu checkmark for Store Browser item (#1144).
    /// </summary>
    private void UpdateStoreBrowserMenuState()
    {
        var menuItem = this.FindControl<MenuItem>("StoreBrowserMenuItem");
        if (menuItem != null)
        {
            var isVisible = SettingsService.Instance.StoreBrowserPanelVisible;
            menuItem.Icon = isVisible ? new TextBlock { Text = "✓" } : null;
        }
    }

    #endregion
}

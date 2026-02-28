using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogEditor.Services;
using DialogEditor.ViewModels;
using Radoub.Formats.Logging;
using Radoub.UI.Controls;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Manages dialog browser panel interactions for MainWindow.
    /// Extracted from MainWindow.Lifecycle.cs (#1267) to reduce partial file size.
    ///
    /// Handles:
    /// 1. Dialog browser panel initialization and module path discovery
    /// 2. File selection events (loading dialogs from the browser)
    /// 3. Toggle visibility and menu checkmark state
    /// 4. Current file highlight updates
    /// </summary>
    public class DialogBrowserController
    {
        private readonly Window _window;
        private readonly MainViewModel _viewModel;
        private readonly MainWindowServices _services;

        // Callbacks to MainWindow methods that remain in other partials
        private readonly Action _updateEmbeddedFlowchartAfterLoad;
        private readonly Func<string, Task> _scanCreaturesForModuleAsync;
        private readonly Action _populateRecentFilesMenu;

        public DialogBrowserController(
            Window window,
            MainViewModel viewModel,
            MainWindowServices services,
            Action updateEmbeddedFlowchartAfterLoad,
            Func<string, Task> scanCreaturesForModuleAsync,
            Action populateRecentFilesMenu)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _updateEmbeddedFlowchartAfterLoad = updateEmbeddedFlowchartAfterLoad ?? throw new ArgumentNullException(nameof(updateEmbeddedFlowchartAfterLoad));
            _scanCreaturesForModuleAsync = scanCreaturesForModuleAsync ?? throw new ArgumentNullException(nameof(scanCreaturesForModuleAsync));
            _populateRecentFilesMenu = populateRecentFilesMenu ?? throw new ArgumentNullException(nameof(populateRecentFilesMenu));
        }

        /// <summary>
        /// Initializes dialog browser panel with context and event handlers (#1143).
        /// </summary>
        public void InitializePanel()
        {
            var dialogBrowserPanel = _window.FindControl<DialogBrowserPanel>("DialogBrowserPanel");
            if (dialogBrowserPanel == null)
            {
                UnifiedLogger.LogUI(LogLevel.WARN, "DialogBrowserPanel not found");
                return;
            }

            // Create context for HAK file discovery
            var context = new ParleyScriptBrowserContext(_viewModel.CurrentFilePath, _services.Settings, _services.GameData);

            // Set initial module path from RadoubSettings
            // Validate this is a real module path, not just the modules parent directory (#1326)
            var modulePath = Radoub.Formats.Settings.RadoubSettings.Instance.CurrentModulePath;
            if (Radoub.Formats.Settings.RadoubSettings.IsValidModulePath(modulePath))
            {
                // If it's a .mod file, find the working directory
                if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
                {
                    modulePath = FindWorkingDirectory(modulePath);
                }
                else if (!Directory.Exists(modulePath))
                {
                    modulePath = null;
                }

                if (!string.IsNullOrEmpty(modulePath))
                {
                    dialogBrowserPanel.ModulePath = modulePath;
                    UnifiedLogger.LogUI(LogLevel.INFO, $"DialogBrowserPanel initialized with module path");
                }
            }

            // Subscribe to file selection events
            dialogBrowserPanel.FileSelected += OnFileSelected;

            // Subscribe to file delete events (#1509)
            dialogBrowserPanel.FileDeleteRequested += OnFileDeleteRequested;

            // Subscribe to collapse/expand events (#1143)
            dialogBrowserPanel.CollapsedChanged += OnCollapsedChanged;

            // Update menu item checkmark
            UpdateMenuState();
        }

        /// <summary>
        /// Handles collapse/expand button clicks from DialogBrowserPanel (#1143).
        /// </summary>
        public void OnCollapsedChanged(object? sender, bool isCollapsed)
        {
            _services.WindowPersistence.SetDialogBrowserPanelVisible(!isCollapsed);
            UpdateMenuState();
        }

        /// <summary>
        /// Updates the DialogBrowserPanel's current file highlight (#1143).
        /// Called by FileMenuController after File > Open loads a file.
        /// </summary>
        public void UpdateCurrentFile(string filePath)
        {
            var dialogBrowserPanel = _window.FindControl<DialogBrowserPanel>("DialogBrowserPanel");
            if (dialogBrowserPanel != null)
            {
                dialogBrowserPanel.CurrentFilePath = filePath;
            }
        }

        /// <summary>
        /// Find the unpacked working directory for a .mod file.
        /// </summary>
        internal static string? FindWorkingDirectory(string modFilePath)
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
                if (Directory.Exists(candidate) &&
                    File.Exists(Path.Combine(candidate, "module.ifo")))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Handles file delete request from dialog browser panel (#1509).
        /// Shows confirmation dialog, deletes file, and refreshes list.
        /// </summary>
        private async void OnFileDeleteRequested(object? sender, FileDeleteRequestedEventArgs e)
        {
            var entry = e.Entry;
            if (string.IsNullOrEmpty(entry.FilePath) || !File.Exists(entry.FilePath))
            {
                _viewModel.StatusMessage = "File not found on disk";
                return;
            }

            var fileName = Path.GetFileName(entry.FilePath);

            var confirmed = await Radoub.UI.Services.DialogHelper.ShowConfirmAsync(
                _window, "Confirm Delete", $"Delete \"{fileName}\" from disk?\n\nThis cannot be undone.");
            if (!confirmed)
                return;

            try
            {
                var isDeletingCurrent = string.Equals(
                    _viewModel.CurrentFilePath, entry.FilePath, StringComparison.OrdinalIgnoreCase);

                File.Delete(entry.FilePath);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Deleted dialog file: {fileName}");

                if (isDeletingCurrent)
                {
                    _viewModel.CloseDialog();
                }

                _viewModel.StatusMessage = $"Deleted {fileName}";

                var dialogBrowserPanel = _window.FindControl<DialogBrowserPanel>("DialogBrowserPanel");
                if (dialogBrowserPanel != null)
                {
                    await dialogBrowserPanel.RefreshAsync();
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to delete {fileName}: {ex.Message}");
                _viewModel.StatusMessage = $"Delete failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Handles file selection in the dialog browser panel (#1143).
        /// </summary>
        private async void OnFileSelected(object? sender, FileSelectedEventArgs e)
        {
            // Only load on single click (per issue requirements)
            // Double-click could be used for something else in the future
            if (e.Entry == null)
                return;

            var filePath = e.Entry.FilePath;

            // Handle HAK files - they need extraction first
            if (e.Entry.IsFromHak && !string.IsNullOrEmpty(e.Entry.HakPath))
            {
                _viewModel.StatusMessage = $"Cannot open dialogs from HAK directly - use HAK editor to extract first";
                return;
            }

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                _viewModel.StatusMessage = $"File not found: {e.Entry.Name}";
                return;
            }

            // Check for unsaved changes and auto-save if needed
            if (_viewModel.HasUnsavedChanges)
            {
                // Auto-save current file before loading new one
                if (!string.IsNullOrEmpty(_viewModel.CurrentFilePath))
                {
                    _viewModel.StatusMessage = "Auto-saving current dialog...";
                    await _viewModel.SaveDialogAsync(_viewModel.CurrentFilePath);
                }
            }

            // Load the selected dialog
            _viewModel.StatusMessage = $"Loading {e.Entry.Name}...";
            await _viewModel.LoadDialogAsync(filePath);

            // Update flowchart after loading (same as File menu pattern)
            _updateEmbeddedFlowchartAfterLoad();

            // Scan creatures for portrait/soundset display (same as File menu pattern)
            var moduleDir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(moduleDir))
            {
                await _scanCreaturesForModuleAsync(moduleDir);
            }

            // Update the current file highlight in the browser panel
            var dialogBrowserPanel = _window.FindControl<DialogBrowserPanel>("DialogBrowserPanel");
            if (dialogBrowserPanel != null)
            {
                dialogBrowserPanel.CurrentFilePath = filePath;
            }

            // Refresh the dialog browser to show the new file as selected
            _populateRecentFilesMenu();
        }

        /// <summary>
        /// Toggle dialog browser panel visibility (View menu) (#1143).
        /// </summary>
        public void OnToggleClick(object? sender, RoutedEventArgs e)
        {
            var settings = _services.Settings;
            _services.WindowPersistence.SetDialogBrowserPanelVisible(!settings.DialogBrowserPanelVisible);
            UpdateMenuState();
        }

        /// <summary>
        /// Updates the dialog browser menu item checkmark state (#1143).
        /// </summary>
        public void UpdateMenuState()
        {
            var menuItem = _window.FindControl<MenuItem>("DialogBrowserMenuItem");
            if (menuItem != null)
            {
                var isVisible = _services.Settings.DialogBrowserPanelVisible;
                menuItem.Icon = isVisible ? new TextBlock { Text = "\u2713" } : null;
            }
        }
    }
}

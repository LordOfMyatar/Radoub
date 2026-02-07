using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DialogEditor.Models;

using Radoub.Formats.Logging;
using Radoub.UI.Views;

namespace DialogEditor.Views
{
    /// <summary>
    /// MainWindow partial class for file menu operation handlers.
    /// Extracted from MainWindow.axaml.cs (#1225).
    /// </summary>
    public partial class MainWindow
    {
        #region File Menu Handlers

        private void OnNewClick(object? sender, RoutedEventArgs e)
            => _controllers.FileMenu.OnNewClick(sender, e);

        private void OnOpenClick(object? sender, RoutedEventArgs e)
            => _controllers.FileMenu.OnOpenClick(sender, e);

        private void OnSaveClick(object? sender, RoutedEventArgs e)
            => _controllers.FileMenu.OnSaveClick(sender, e);

        private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
        {
            await ShowSaveAsDialogAsync();
        }

        /// <summary>
        /// Issue #8: Extracted Save As logic so it can be called from close handler.
        /// Returns true if save succeeded, false if cancelled or failed.
        /// </summary>
        private async Task<bool> ShowSaveAsDialogAsync()
        {
            try
            {
                var storageProvider = StorageProvider;
                if (storageProvider == null)
                {
                    _viewModel.StatusMessage = "Storage provider not available";
                    return false;
                }

                // WORKAROUND (2025-10-23): Simplified options to avoid hang
                var options = new FilePickerSaveOptions
                {
                    Title = "Save Dialog File As",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("DLG Dialog Files")
                        {
                            Patterns = new[] { "*.dlg" }
                        }
                    }
                };

                var file = await storageProvider.SaveFilePickerAsync(options);
                if (file != null)
                {
                    var filePath = file.Path.LocalPath;

                    // #826: Validate filename length for Aurora Engine
                    if (!await _controllers.FileMenu.ValidateFilenameAsync(filePath))
                    {
                        return false;
                    }

                    // CRITICAL FIX: Save current node properties before saving file
                    SaveCurrentNodeProperties();

                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Saving file as: {UnifiedLogger.SanitizePath(filePath)}");
                    var success = await _viewModel.SaveDialogAsync(filePath);

                    // Refresh recent files menu
                    PopulateRecentFilesMenu();

                    return success;
                }

                return false; // User cancelled
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Error saving file: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save file: {ex.Message}");
                return false;
            }
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            _controllers.FileMenu.OnCloseClick(sender, e);
            _selectedNode = null; // Clear local selection reference
        }

        private async void OnRenameDialogClick(object? sender, RoutedEventArgs e)
        {
            await RenameCurrentDialogAsync();
        }

        /// <summary>
        /// Renames the current dialog file using save-rename-reload workflow (#675).
        /// </summary>
        private async Task RenameCurrentDialogAsync()
        {
            var filePath = _viewModel.CurrentFilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                _viewModel.StatusMessage = "No file loaded to rename";
                return;
            }

            var directory = Path.GetDirectoryName(filePath) ?? "";
            var currentName = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);

            // Show rename dialog
            var newName = await RenameDialog.ShowAsync(this, currentName, directory, extension);
            if (string.IsNullOrEmpty(newName))
            {
                return; // User cancelled
            }

            // Check if file has unsaved changes
            if (_viewModel.HasUnsavedChanges)
            {
                // Save before renaming
                SaveCurrentNodeProperties();
                var saved = await _viewModel.SaveDialogAsync(filePath);
                if (!saved)
                {
                    _viewModel.StatusMessage = "Failed to save file before renaming";
                    return;
                }
            }

            var newFilePath = Path.Combine(directory, newName + extension);

            try
            {
                // Rename file on disk
                File.Move(filePath, newFilePath);
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Renamed file: {UnifiedLogger.SanitizePath(filePath)} -> {UnifiedLogger.SanitizePath(newFilePath)}");

                // Update view model to point to new file
                _viewModel.CurrentFileName = newFilePath;

                // Save file to ensure internal state is consistent
                await _viewModel.SaveDialogAsync(newFilePath);

                // Update dialog name text box
                var dialogNameTextBox = this.FindControl<TextBox>("DialogNameTextBox");
                if (dialogNameTextBox != null)
                {
                    dialogNameTextBox.Text = newName;
                }

                // Update recent files
                PopulateRecentFilesMenu();

                _viewModel.StatusMessage = $"Renamed to: {newName}{extension}";
            }
            catch (IOException ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to rename file: {ex.Message}");
                _viewModel.StatusMessage = $"Failed to rename: {ex.Message}";
            }
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
            => _controllers.FileMenu.OnExitClick(sender, e);

        private void PopulateRecentFilesMenu()
            => _controllers.FileMenu.PopulateRecentFilesMenu();

        private void UpdateModuleInfo(string dialogFilePath)
            => _controllers.FileMenu.UpdateModuleInfo(dialogFilePath);

        private void ClearModuleInfo()
            => _controllers.FileMenu.ClearModuleInfo();

        #endregion

        #region Creature Scanning

        /// <summary>
        /// Scans creatures in the module directory for portrait/soundset lookup (#786, #915).
        /// Called automatically when a dialog file is loaded.
        /// </summary>
        private async Task ScanCreaturesForModuleAsync(string moduleDirectory)
        {
            try
            {
                if (string.IsNullOrEmpty(moduleDirectory) || !Directory.Exists(moduleDirectory))
                    return;

                // Skip if creatures already scanned for this directory
                if (_services.Creature.HasCachedCreatures)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Creatures already cached, skipping scan");
                    return;
                }

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Scanning creatures for portrait/soundset lookup: {UnifiedLogger.SanitizePath(moduleDirectory)}");

                // Get game data path for 2DA lookups
                var settings = _services.Settings;
                string? gameDataPath = null;
                var basePath = settings.BaseGameInstallPath;
                if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
                {
                    var dataPath = Path.Combine(basePath, "data");
                    if (Directory.Exists(dataPath))
                        gameDataPath = dataPath;
                }

                var creatures = await _services.Creature.ScanCreaturesAsync(moduleDirectory, gameDataPath);
                if (creatures.Count > 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Cached {creatures.Count} creatures for portrait/soundset lookup");

                    // Refresh the selected node's properties to show portrait/soundset (#786, #915, #916)
                    if (_selectedNode != null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => PopulatePropertiesPanel(_selectedNode));
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Error scanning creatures: {ex.Message}");
            }
        }

        #endregion

        #region Title Bar Handlers (Issue #139)

        private void OnTitleBarPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // Only drag on left mouse button
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void OnTitleBarDoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Toggle maximize/restore on double-click
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        #endregion
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DialogEditor.Parsers;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.Utils;
using DialogEditor.ViewModels;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Manages all File menu operations for MainWindow.
    /// Extracted from MainWindow to reduce method size and improve maintainability (Epic #457, Sprint 5).
    ///
    /// Handles:
    /// 1. New/Open/Save/SaveAs/Close file operations
    /// 2. Recent files menu population
    /// 3. File dialog coordination
    /// 4. Module info display updates
    /// 5. Filename validation for Aurora Engine constraints (#826)
    /// </summary>
    public class FileMenuController
    {
        /// <summary>
        /// Aurora Engine maximum filename length (excluding extension).
        /// Documented in CLAUDE.md under "Aurora Engine File Naming Constraints".
        /// </summary>
        private const int MaxAuroraFilenameLength = 16;
        private readonly Window _window;
        private readonly SafeControlFinder _controls;
        private readonly Func<MainViewModel> _getViewModel;
        private readonly Action _saveCurrentNodeProperties;
        private readonly Action _clearPropertiesPanel;
        private readonly Action _populateRecentFilesMenu;
        private readonly Action _updateEmbeddedFlowchartAfterLoad;
        private readonly Action _clearFlowcharts;
        private readonly Func<ScriptParameterUIManager> _getParameterUIManager;
        private readonly Func<Task<bool>> _showSaveAsDialogAsync;
        private readonly Func<string, Task>? _scanCreaturesForModule;

        public FileMenuController(
            Window window,
            SafeControlFinder controls,
            Func<MainViewModel> getViewModel,
            Action saveCurrentNodeProperties,
            Action clearPropertiesPanel,
            Action populateRecentFilesMenu,
            Action updateEmbeddedFlowchartAfterLoad,
            Action clearFlowcharts,
            Func<ScriptParameterUIManager> getParameterUIManager,
            Func<Task<bool>> showSaveAsDialogAsync,
            Func<string, Task>? scanCreaturesForModule = null)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _controls = controls ?? throw new ArgumentNullException(nameof(controls));
            _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));
            _saveCurrentNodeProperties = saveCurrentNodeProperties ?? throw new ArgumentNullException(nameof(saveCurrentNodeProperties));
            _clearPropertiesPanel = clearPropertiesPanel ?? throw new ArgumentNullException(nameof(clearPropertiesPanel));
            _populateRecentFilesMenu = populateRecentFilesMenu ?? throw new ArgumentNullException(nameof(populateRecentFilesMenu));
            _updateEmbeddedFlowchartAfterLoad = updateEmbeddedFlowchartAfterLoad ?? throw new ArgumentNullException(nameof(updateEmbeddedFlowchartAfterLoad));
            _clearFlowcharts = clearFlowcharts ?? throw new ArgumentNullException(nameof(clearFlowcharts));
            _getParameterUIManager = getParameterUIManager ?? throw new ArgumentNullException(nameof(getParameterUIManager));
            _showSaveAsDialogAsync = showSaveAsDialogAsync ?? throw new ArgumentNullException(nameof(showSaveAsDialogAsync));
            _scanCreaturesForModule = scanCreaturesForModule;
        }

        private MainViewModel ViewModel => _getViewModel();
        private ScriptParameterUIManager ParameterUIManager => _getParameterUIManager();

        #region File Menu Handlers

        /// <summary>
        /// Handle File > New - Create new dialog with immediate save location prompt.
        /// </summary>
        public async void OnNewClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "File → New clicked");

                var storageProvider = _window.StorageProvider;
                if (storageProvider == null)
                {
                    ViewModel.StatusMessage = "Storage provider not available";
                    return;
                }

                var options = new FilePickerSaveOptions
                {
                    Title = "Save New Dialog File As",
                    DefaultExtension = "dlg",
                    SuggestedFileName = "dialog.dlg",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("DLG Dialog Files")
                        {
                            Patterns = new[] { "*.dlg" }
                        },
                        new FilePickerFileType("JSON Files")
                        {
                            Patterns = new[] { "*.json" }
                        }
                    }
                };

                var file = await storageProvider.SaveFilePickerAsync(options);
                if (file != null)
                {
                    var filePath = file.Path.LocalPath;

                    // #826: Validate filename length for Aurora Engine
                    if (!await ValidateFilenameAsync(filePath))
                    {
                        return;
                    }

                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Creating new dialog at: {UnifiedLogger.SanitizePath(filePath)}");

                    // Create blank dialog
                    ViewModel.NewDialog();

                    // Set filename so auto-save works immediately
                    ViewModel.CurrentFileName = filePath;

                    // Save immediately to create file on disk
                    await ViewModel.SaveDialogAsync(filePath);

                    ViewModel.StatusMessage = $"New dialog created: {Path.GetFileName(filePath)}";
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"New dialog created and saved to: {UnifiedLogger.SanitizePath(filePath)}");

                    // Refresh recent files menu
                    _populateRecentFilesMenu();
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, "File → New cancelled by user");
                }
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error creating new dialog: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to create new dialog: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle File > Open - Open existing dialog file.
        /// </summary>
        public async void OnOpenClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var storageProvider = _window.StorageProvider;
                if (storageProvider == null)
                {
                    ViewModel.StatusMessage = "Storage provider not available";
                    return;
                }

                var options = new FilePickerOpenOptions
                {
                    Title = "Open Dialog File",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("DLG Dialog Files")
                        {
                            Patterns = new[] { "*.dlg" }
                        },
                        new FilePickerFileType("JSON Files")
                        {
                            Patterns = new[] { "*.json" }
                        },
                        new FilePickerFileType("All Files")
                        {
                            Patterns = new[] { "*.*" }
                        }
                    }
                };

                var files = await storageProvider.OpenFilePickerAsync(options);
                if (files != null && files.Count > 0)
                {
                    var file = files[0];
                    var filePath = file.Path.LocalPath;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Opening file: {UnifiedLogger.SanitizePath(filePath)}");
                    await ViewModel.LoadDialogAsync(filePath);

                    // Update module info bar
                    UpdateModuleInfo(filePath);

                    // Refresh recent files menu (#597)
                    _populateRecentFilesMenu();

                    // Update embedded flowchart if in side-by-side mode
                    _updateEmbeddedFlowchartAfterLoad();

                    // Scan creatures for portrait/soundset display (#786, #915)
                    if (_scanCreaturesForModule != null)
                    {
                        var moduleDir = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(moduleDir))
                        {
                            _ = _scanCreaturesForModule(moduleDir);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error opening file: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open file: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle File > Save - Save current dialog to file.
        /// </summary>
        public async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ViewModel.CurrentFileName))
            {
                await _showSaveAsDialogAsync();
                return;
            }

            // #826: Validate filename length for Aurora Engine
            if (!await ValidateFilenameAsync(ViewModel.CurrentFileName))
            {
                // Prompt Save As to allow user to choose a shorter filename
                await _showSaveAsDialogAsync();
                return;
            }

            // Block save if duplicate parameter keys exist
            if (ParameterUIManager.HasAnyDuplicateKeys())
            {
                ViewModel.StatusMessage = "⛔ Cannot save: Fix duplicate parameter keys first!";
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    "Save blocked: Duplicate parameter keys detected. User must fix before saving.");

                // Show warning dialog
                ShowDuplicateKeysWarning();
                return;
            }

            // Save any pending node changes
            _saveCurrentNodeProperties();

            // Visual feedback
            ViewModel.StatusMessage = "Saving file...";

            var success = await ViewModel.SaveDialogAsync(ViewModel.CurrentFileName);

            if (success)
            {
                ViewModel.StatusMessage = "File saved successfully";
            }
            else
            {
                // Show error dialog with Save As option
                var saveAs = await ShowSaveErrorDialog(ViewModel.StatusMessage);
                if (saveAs)
                {
                    await _showSaveAsDialogAsync();
                }
            }
        }

        /// <summary>
        /// Handle File > Close - Close current dialog.
        /// </summary>
        public void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            ViewModel.CloseDialog();
            ClearModuleInfo();
            _clearPropertiesPanel();
            _clearFlowcharts(); // #378: Clear all flowchart views when file closed
        }

        /// <summary>
        /// Handle File > Exit - Close application.
        /// </summary>
        public void OnExitClick(object? sender, RoutedEventArgs e)
        {
            _window.Close();
        }

        /// <summary>
        /// Handle recent file click - Load selected recent file.
        /// </summary>
        public async void OnRecentFileClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem menuItem && menuItem.Tag is string filePath)
                {
                    // Check if file exists before trying to load
                    if (!File.Exists(filePath))
                    {
                        var fileName = Path.GetFileName(filePath);
                        var shouldRemove = await ShowConfirmDialog(
                            "File Not Found",
                            $"The file '{fileName}' could not be found.\n\nFull path: {UnifiedLogger.SanitizePath(filePath)}\n\nRemove from recent files?");

                        if (shouldRemove)
                        {
                            SettingsService.Instance.RemoveRecentFile(filePath);
                            _populateRecentFilesMenu();
                        }
                        return;
                    }

                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading recent file: {UnifiedLogger.SanitizePath(filePath)}");
                    await ViewModel.LoadDialogAsync(filePath);

                    // Update module info bar
                    UpdateModuleInfo(filePath);

                    // Refresh recent files menu to move this file to top (#597)
                    _populateRecentFilesMenu();

                    // Update embedded flowchart
                    _updateEmbeddedFlowchartAfterLoad();
                }
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error loading recent file: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load recent file: {ex.Message}");
            }
        }

        /// <summary>
        /// Populate the recent files submenu.
        /// </summary>
        public void PopulateRecentFilesMenu()
        {
            try
            {
                var recentFilesMenuItem = _window.FindControl<MenuItem>("RecentFilesMenuItem");
                if (recentFilesMenuItem == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "RecentFilesMenuItem not found in XAML");
                    return;
                }

                var menuItems = new System.Collections.Generic.List<object>();
                var recentFiles = SettingsService.Instance.RecentFiles;

                UnifiedLogger.LogApplication(LogLevel.INFO, $"PopulateRecentFilesMenu: {recentFiles.Count} recent files from settings");

                if (recentFiles.Count == 0)
                {
                    var noFilesItem = new MenuItem { Header = "(No recent files)", IsEnabled = false };
                    menuItems.Add(noFilesItem);
                }
                else
                {
                    foreach (var file in recentFiles)
                    {
                        var fileName = Path.GetFileName(file);
                        // Escape underscores for menu display (Avalonia treats _ as mnemonic)
                        var displayName = fileName.Replace("_", "__");

                        var menuItem = new MenuItem
                        {
                            Header = displayName,
                            Tag = file
                        };
                        menuItem.Click += OnRecentFileClick;
                        ToolTip.SetTip(menuItem, file);
                        menuItems.Add(menuItem);
                    }

                    menuItems.Add(new Separator());
                    var clearItem = new MenuItem { Header = "Clear Recent Files" };
                    clearItem.Click += (s, args) =>
                    {
                        SettingsService.Instance.ClearRecentFiles();
                        PopulateRecentFilesMenu();
                    };
                    menuItems.Add(clearItem);
                }

                recentFilesMenuItem.ItemsSource = menuItems;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error building recent files menu: {ex.Message}");
            }
        }

        #endregion

        #region Module Info Display

        /// <summary>
        /// Update module info bar with current file's module information.
        /// </summary>
        public void UpdateModuleInfo(string dialogFilePath)
        {
            try
            {
                var moduleDirectory = Path.GetDirectoryName(dialogFilePath);
                if (string.IsNullOrEmpty(moduleDirectory))
                {
                    ClearModuleInfo();
                    return;
                }

                // Get module name from module.ifo
                var moduleName = ModuleInfoParser.GetModuleName(moduleDirectory);

                // Sanitize path for display
                var displayPath = PathHelper.SanitizePathForDisplay(moduleDirectory);

                // Update UI
                _controls.WithControl<TextBlock>("ModuleNameTextBlock", tb =>
                    tb.Text = moduleName ?? Path.GetFileName(moduleDirectory));
                _controls.WithControl<TextBlock>("ModulePathTextBlock", tb =>
                    tb.Text = displayPath);

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Module info updated: {moduleName ?? "(unnamed)"} | {displayPath}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to update module info: {ex.Message}");
                ClearModuleInfo();
            }
        }

        /// <summary>
        /// Clear module info display.
        /// </summary>
        public void ClearModuleInfo()
        {
            _controls.WithControl<TextBlock>("ModuleNameTextBlock", tb => tb.Text = "No module loaded");
            _controls.WithControl<TextBlock>("ModulePathTextBlock", tb => tb.Text = "");
        }

        #endregion

        #region Dialog Helpers

        private void ShowDuplicateKeysWarning()
        {
            var msgBox = new Window
            {
                Title = "Cannot Save",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 15,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Duplicate parameter keys detected.\n\nFix the duplicate keys (shown with red borders) before saving.",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        },
                        new Button
                        {
                            Content = "OK",
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                        }
                    }
                }
            };

            var okButton = ((StackPanel)msgBox.Content).Children.OfType<Button>().First();
            okButton.Click += (s, args) => msgBox.Close();
            msgBox.Show(_window);
        }

        /// <summary>
        /// Show confirmation dialog and return user's choice.
        /// </summary>
        private async Task<bool> ShowConfirmDialog(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                MinWidth = 400,
                MaxWidth = 600,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 560,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 10
            };

            var result = false;

            var yesButton = new Button { Content = "Yes", Width = 80 };
            yesButton.Click += (s, e) => { result = true; dialog.Close(); };

            var noButton = new Button { Content = "No", Width = 80 };
            noButton.Click += (s, e) => { result = false; dialog.Close(); };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            panel.Children.Add(buttonPanel);
            dialog.Content = panel;

            await dialog.ShowDialog(_window);
            return result;
        }

        /// <summary>
        /// Show save error dialog with Save As option.
        /// </summary>
        private async Task<bool> ShowSaveErrorDialog(string errorMessage)
        {
            var dialog = new Window
            {
                Title = "Save Failed",
                MinWidth = 400,
                MaxWidth = 500,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = errorMessage,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 460,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 10
            };

            var result = false;

            var saveAsButton = new Button { Content = "Save As...", Width = 100 };
            saveAsButton.Click += (s, e) => { result = true; dialog.Close(); };

            var cancelButton = new Button { Content = "Cancel", Width = 80 };
            cancelButton.Click += (s, e) => { result = false; dialog.Close(); };

            buttonPanel.Children.Add(saveAsButton);
            buttonPanel.Children.Add(cancelButton);
            panel.Children.Add(buttonPanel);
            dialog.Content = panel;

            await dialog.ShowDialog(_window);
            return result;
        }

        #endregion

        #region Filename Validation (#826)

        /// <summary>
        /// Validates filename length for Aurora Engine compatibility.
        /// Returns true if valid, false if blocked.
        /// Shows error dialog if filename exceeds 16 characters.
        /// </summary>
        public async Task<bool> ValidateFilenameAsync(string filePath)
        {
            var filename = Path.GetFileNameWithoutExtension(filePath);
            if (filename.Length <= MaxAuroraFilenameLength)
            {
                return true;
            }

            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Filename '{filename}' is {filename.Length} characters, exceeds Aurora Engine limit of {MaxAuroraFilenameLength}");

            await ShowFilenameTooLongError(filename);
            return false;
        }

        /// <summary>
        /// Shows error dialog for filename exceeding Aurora Engine limit.
        /// </summary>
        private async Task ShowFilenameTooLongError(string filename)
        {
            var dialog = new Window
            {
                Title = "Filename Too Long",
                MinWidth = 450,
                MaxWidth = 550,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = $"Filename '{filename}' is {filename.Length} characters.\n\n" +
                       $"Aurora Engine maximum is {MaxAuroraFilenameLength} characters.\n\n" +
                       "The game cannot load files with longer names.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 510,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            okButton.Click += (s, e) => dialog.Close();
            panel.Children.Add(okButton);

            dialog.Content = panel;
            await dialog.ShowDialog(_window);
        }

        #endregion
    }
}

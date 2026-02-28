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
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using DialogEditor.ViewModels;
using Radoub.UI.Services;
using Radoub.UI.Views;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Manages all File menu operations for MainWindow.
    /// Extracted from MainWindow to reduce method size and improve maintainability (Epic #457, Sprint 5).
    /// Split into partials (#1540): base + ModuleInfo + Dialogs.
    ///
    /// Handles:
    /// 1. New/Open/Save/SaveAs/Close file operations
    /// 2. Recent files menu population
    /// 3. File dialog coordination
    /// 4. Module info display updates
    /// 5. Filename validation for Aurora Engine constraints (#826)
    /// </summary>
    public partial class FileMenuController
    {
        /// <summary>
        /// Aurora Engine maximum filename length (excluding extension).
        /// Documented in CLAUDE.md under "Aurora Engine File Naming Constraints".
        /// </summary>
        private const int MaxAuroraFilenameLength = 16;
        private readonly Window _window;
        private readonly SafeControlFinder _controls;
        private readonly ISettingsService _settings;
        private readonly Func<MainViewModel> _getViewModel;
        private readonly Action _saveCurrentNodeProperties;
        private readonly Action _clearPropertiesPanel;
        private readonly Action _populateRecentFilesMenu;
        private readonly Action _updateEmbeddedFlowchartAfterLoad;
        private readonly Action _clearFlowcharts;
        private readonly Func<ScriptParameterUIManager> _getParameterUIManager;
        private readonly Func<Task<bool>> _showSaveAsDialogAsync;
        private readonly Func<string, Task>? _scanCreaturesForModule;
        private readonly Action<string>? _updateDialogBrowserCurrentFile;

        public FileMenuController(
            Window window,
            SafeControlFinder controls,
            ISettingsService settings,
            Func<MainViewModel> getViewModel,
            Action saveCurrentNodeProperties,
            Action clearPropertiesPanel,
            Action populateRecentFilesMenu,
            Action updateEmbeddedFlowchartAfterLoad,
            Action clearFlowcharts,
            Func<ScriptParameterUIManager> getParameterUIManager,
            Func<Task<bool>> showSaveAsDialogAsync,
            Func<string, Task>? scanCreaturesForModule = null,
            Action<string>? updateDialogBrowserCurrentFile = null)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _controls = controls ?? throw new ArgumentNullException(nameof(controls));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));
            _saveCurrentNodeProperties = saveCurrentNodeProperties ?? throw new ArgumentNullException(nameof(saveCurrentNodeProperties));
            _clearPropertiesPanel = clearPropertiesPanel ?? throw new ArgumentNullException(nameof(clearPropertiesPanel));
            _populateRecentFilesMenu = populateRecentFilesMenu ?? throw new ArgumentNullException(nameof(populateRecentFilesMenu));
            _updateEmbeddedFlowchartAfterLoad = updateEmbeddedFlowchartAfterLoad ?? throw new ArgumentNullException(nameof(updateEmbeddedFlowchartAfterLoad));
            _clearFlowcharts = clearFlowcharts ?? throw new ArgumentNullException(nameof(clearFlowcharts));
            _getParameterUIManager = getParameterUIManager ?? throw new ArgumentNullException(nameof(getParameterUIManager));
            _showSaveAsDialogAsync = showSaveAsDialogAsync ?? throw new ArgumentNullException(nameof(showSaveAsDialogAsync));
            _scanCreaturesForModule = scanCreaturesForModule;
            _updateDialogBrowserCurrentFile = updateDialogBrowserCurrentFile;
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
        /// Uses DialogBrowserWindow to browse module dialogs (#1082).
        /// </summary>
        public async void OnOpenClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Create context for dialog browser - uses current file's directory
                var context = new ParleyScriptBrowserContext(ViewModel.CurrentFileName, _settings);

                var browser = new DialogBrowserWindow(context);
                await browser.ShowDialog(_window);

                // Check if user selected a dialog
                var selectedEntry = browser.SelectedEntry;
                if (selectedEntry?.FilePath != null)
                {
                    var filePath = selectedEntry.FilePath;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Opening file: {UnifiedLogger.SanitizePath(filePath)}");
                    await ViewModel.LoadDialogAsync(filePath);

                    // Update module info bar
                    UpdateModuleInfo(filePath);

                    // Refresh recent files menu (#597)
                    _populateRecentFilesMenu();

                    // Update embedded flowchart if in side-by-side mode
                    _updateEmbeddedFlowchartAfterLoad();

                    // Update dialog browser panel highlight (#1143)
                    _updateDialogBrowserCurrentFile?.Invoke(filePath);

                    // Scan creatures for portrait/soundset display (#786, #915, #916)
                    if (_scanCreaturesForModule != null)
                    {
                        var moduleDir = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(moduleDir))
                        {
                            ViewModel.StatusMessage = "Loading creatures...";
                            await _scanCreaturesForModule(moduleDir);
                            ViewModel.StatusMessage = $"Opened: {Path.GetFileName(filePath)}";
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

            // #152: Check for unsupported characters (warn but don't block)
            if (ViewModel.CurrentDialog != null)
            {
                var textValidation = TextValidator.ValidateDialog(ViewModel.CurrentDialog);
                if (textValidation.HasWarnings)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Dialog contains {textValidation.TotalCharacterCount} unsupported character(s) in {textValidation.AffectedNodeCount} node(s)");

                    // Log details for debugging
                    foreach (var warning in textValidation.Warnings.Take(10))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  {warning}");
                    }

                    // Show warning dialog (non-blocking, user can proceed)
                    ShowUnsupportedCharactersWarning(textValidation);
                }
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
        /// Populate the recent files submenu using shared RecentFilesMenuHelper.
        /// </summary>
        public void PopulateRecentFilesMenu()
        {
            var recentFilesMenuItem = _window.FindControl<MenuItem>("RecentFilesMenuItem");
            if (recentFilesMenuItem == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "RecentFilesMenuItem not found in XAML");
                return;
            }

            RecentFilesMenuHelper.Populate(
                recentFilesMenuItem,
                _settings.RecentFiles,
                async filePath => await HandleRecentFileClick(filePath),
                () =>
                {
                    _settings.ClearRecentFiles();
                    PopulateRecentFilesMenu();
                });
        }

        /// <summary>
        /// Handles loading a file from the recent files menu.
        /// Extracted from OnRecentFileClick for use with RecentFilesMenuHelper.
        /// </summary>
        private async Task HandleRecentFileClick(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    var fileName = Path.GetFileName(filePath);
                    var shouldRemove = await ShowConfirmDialog(
                        "File Not Found",
                        $"The file '{fileName}' could not be found.\n\nFull path: {UnifiedLogger.SanitizePath(filePath)}\n\nRemove from recent files?");

                    if (shouldRemove)
                    {
                        _settings.RemoveRecentFile(filePath);
                        _populateRecentFilesMenu();
                    }
                    return;
                }

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading recent file: {UnifiedLogger.SanitizePath(filePath)}");
                await ViewModel.LoadDialogAsync(filePath);

                UpdateModuleInfo(filePath);
                _populateRecentFilesMenu();
                _updateEmbeddedFlowchartAfterLoad();

                if (_scanCreaturesForModule != null)
                {
                    var moduleDir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(moduleDir))
                    {
                        ViewModel.StatusMessage = "Loading creatures...";
                        await _scanCreaturesForModule(moduleDir);
                        ViewModel.StatusMessage = $"Opened: {Path.GetFileName(filePath)}";
                    }
                }
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error loading recent file: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load recent file: {ex.Message}");
            }
        }

        #endregion
    }
}

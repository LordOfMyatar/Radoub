using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DialogEditor.ViewModels;
using DialogEditor.Models;
using DialogEditor.Utils;
using DialogEditor.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using DialogEditor.Parsers;
using Parley.Services;
using Parley.Views.Helpers;
using DialogEditor.Views;
using Radoub.Formats.Ssf;
using Radoub.UI.Controls;
using Radoub.UI.Utils;
using Radoub.UI.Views;

namespace DialogEditor.Views
{
    /// <summary>
    /// Item for the soundset type dropdown (#916).
    /// </summary>
    public class SoundsetTypeItem
    {
        public string Name { get; set; } = "";
        public SsfSoundType SoundType { get; set; }
        public override string ToString() => Name;
    }

    public partial class MainWindow : Window, IKeyboardShortcutHandler
    {
        private readonly MainViewModel _viewModel;
        private readonly SafeControlFinder _controls;
        private readonly WindowLifecycleManager _windows;

        // Service and controller containers (#526)
        private readonly MainWindowServices _services;
        private readonly MainWindowControllers _controllers;

        // Auto-save timer
        private System.Timers.Timer? _autoSaveTimer;

        // UI state management
        private readonly UiStateManager _uiState = new();

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            _controls = new SafeControlFinder(this);
            _windows = new WindowLifecycleManager();
            _services = new MainWindowServices();
            _controllers = new MainWindowControllers();

            _viewModel.SelectedTreeNode = null;

            InitializeServices();
            InitializeControllers();
            InitializeLogging();
            RegisterEventHandlers();
            SetupUILayout();
        }

        /// <summary>
        /// Initializes services that require MainWindow context.
        /// Core services are created in MainWindowServices constructor.
        /// </summary>
        private void InitializeServices()
        {
            // Property services
            _services.PropertyPopulator = new PropertyPanelPopulator(this);
            _services.PropertyPopulator.SetImageService(_services.ImageService);
            _services.PropertyPopulator.SetGameDataService(_services.GameData);
            _services.PropertyPopulator.SetCurrentSoundsetId = id => _currentSoundsetId = id;
            _services.PropertyAutoSave = new PropertyAutoSaveService(
                findControl: this.FindControl<Control>,
                refreshTreeDisplay: RefreshTreeDisplayPreserveState,
                loadScriptPreview: (script, isCondition) => _ = LoadScriptPreviewAsync(script, isCondition),
                clearScriptPreview: ClearScriptPreview,
                triggerDebouncedAutoSave: TriggerDebouncedAutoSave,
                refreshSiblingValidation: RefreshSiblingValidation); // Issue #609
            _services.ParameterUI = new ScriptParameterUIManager(
                findControl: this.FindControl<Control>,
                setStatusMessage: msg => _viewModel.StatusMessage = msg,
                triggerAutoSave: () => { _viewModel.HasUnsavedChanges = true; TriggerDebouncedAutoSave(); },
                isPopulatingProperties: () => _uiState.IsPopulatingProperties,
                getSelectedNode: () => _selectedNode);

            // UI helpers
            _services.NodeCreation = new NodeCreationHelper(
                viewModel: _viewModel,
                findControl: this.FindControl<Control>,
                saveCurrentNodeProperties: SaveCurrentNodeProperties,
                triggerAutoSave: TriggerDebouncedAutoSave);
            _services.ResourceBrowser = new ResourceBrowserManager(
                audioService: _services.Audio,
                creatureService: _services.Creature,
                findControl: this.FindControl<Control>,
                setStatusMessage: msg => _viewModel.StatusMessage = msg,
                autoSaveProperty: AutoSaveProperty,
                getSelectedNode: () => _selectedNode,
                getCurrentFilePath: () => _viewModel.CurrentFilePath,
                gameDataService: _services.GameData);
            _services.KeyboardShortcuts.RegisterShortcuts(this);

            // Window services
            _services.DebugLogging = new DebugAndLoggingHandler(
                viewModel: _viewModel,
                findControl: this.FindControl<Control>,
                getStorageProvider: () => this.StorageProvider,
                setStatusMessage: msg => _viewModel.StatusMessage = msg);
            _services.WindowPersistence = new WindowPersistenceManager(
                window: this,
                findControl: this.FindControl<Control>);

            // TreeView and dialog services
            _services.DragDrop.DropCompleted += OnDragDropCompleted;
            _services.Dialog = new DialogFactory(this);

            // Sound playback - Issue #895
            _services.SoundPlayback.PlaybackStopped += OnSoundPlaybackStopped;
        }

        /// <summary>
        /// Initializes UI controllers that coordinate complex interactions.
        /// </summary>
        private void InitializeControllers()
        {
            _controllers.Flowchart = new FlowchartManager(
                window: this,
                controls: _controls,
                windows: _windows,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                setSelectedNode: node => _selectedNode = node,
                populatePropertiesPanel: PopulatePropertiesPanel,
                saveCurrentNodeProperties: SaveCurrentNodeProperties,
                getIsSettingSelectionProgrammatically: () => _uiState.IsSettingSelectionProgrammatically,
                setIsSettingSelectionProgrammatically: value => _uiState.IsSettingSelectionProgrammatically = value,
                shortcutManager: _services.KeyboardShortcuts, // #809: Enable keyboard shortcuts in FlowView
                onContextMenuAction: OnFlowchartContextMenuAction); // #461: Context menu parity

            _controllers.TreeView = new TreeViewUIController(
                window: this,
                controls: _controls,
                dragDropService: _services.DragDrop,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                setSelectedNode: node => _selectedNode = node,
                populatePropertiesPanel: PopulatePropertiesPanel,
                saveCurrentNodeProperties: SaveCurrentNodeProperties,
                clearAllFields: () => _services.PropertyPopulator.ClearAllFields(),
                getIsSettingSelectionProgrammatically: () => _uiState.IsSettingSelectionProgrammatically,
                syncSelectionToFlowcharts: node => _controllers.Flowchart.SyncSelectionToFlowcharts(node));

            _controllers.ScriptBrowser = new ScriptBrowserController(
                window: this,
                controls: _controls,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                autoSaveProperty: AutoSaveProperty,
                gameDataService: _services.GameData);

            _controllers.ParameterBrowser = new ParameterBrowserController(
                controls: _controls,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                parameterUIManager: _services.ParameterUI);

            _services.ScriptPreview = new ScriptPreviewService(_controls);

            _controllers.Quest = new QuestUIController(
                window: this,
                controls: _controls,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                isPopulatingProperties: () => _uiState.IsPopulatingProperties,
                setIsPopulatingProperties: value => _uiState.IsPopulatingProperties = value,
                triggerAutoSave: TriggerDebouncedAutoSave);

            _controllers.FileMenu = new FileMenuController(
                window: this,
                controls: _controls,
                getViewModel: () => _viewModel,
                saveCurrentNodeProperties: SaveCurrentNodeProperties,
                clearPropertiesPanel: () => _services.PropertyPopulator.ClearAllFields(),
                populateRecentFilesMenu: () => _controllers.FileMenu?.PopulateRecentFilesMenu(),
                updateEmbeddedFlowchartAfterLoad: () => _controllers.Flowchart.UpdateAfterLoad(),
                clearFlowcharts: () => _controllers.Flowchart.ClearAll(),
                getParameterUIManager: () => _services.ParameterUI,
                showSaveAsDialogAsync: ShowSaveAsDialogAsync,
                scanCreaturesForModule: ScanCreaturesForModuleAsync,
                updateDialogBrowserCurrentFile: UpdateDialogBrowserCurrentFile);

            _controllers.EditMenu = new EditMenuController(
                window: this,
                getViewModel: () => _viewModel,
                getSelectedNode: GetSelectedTreeNode);

            _controllers.SpeakerVisual = new SpeakerVisualController(
                window: this,
                isPopulatingProperties: () => _uiState.IsPopulatingProperties);
        }

        /// <summary>
        /// Initializes logging infrastructure.
        /// </summary>
        private void InitializeLogging()
        {
            DebugLogger.Initialize(this);
            UnifiedLogger.SetLogLevel(LogLevel.DEBUG);
            UnifiedLogger.LogApplication(LogLevel.INFO, "Parley MainWindow initialized");

            // Cleanup old log sessions on startup
            var retentionCount = SettingsService.Instance.LogRetentionSessions;
            UnifiedLogger.CleanupOldSessions(retentionCount);
        }

        /// <summary>
        /// Registers all event handlers for window, theme, settings, and UI events.
        /// </summary>
        private void RegisterEventHandlers()
        {
            // Subscribe to theme changes to refresh tree view colors
            ThemeManager.Instance.ThemeApplied += OnThemeApplied;

            // Subscribe to NPC tag coloring setting changes (Issue #134)
            SettingsService.Instance.PropertyChanged += OnSettingsPropertyChanged;

            // Subscribe to dialog change events for FlowView synchronization (#436, #451)
            DialogChangeEventBus.Instance.DialogChanged += OnDialogChanged;

            // Hook up window lifecycle events
            this.Opened += OnWindowOpened;
            this.Closing += OnWindowClosing;
            this.PositionChanged += (s, e) => _services.WindowPersistence.SaveWindowPosition();
            this.PropertyChanged += OnWindowPropertyChanged;
            this.Loaded += OnWindowLoaded;

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.DebugMessages.CollectionChanged += OnDebugMessagesCollectionChanged;
        }

        /// <summary>
        /// Configures initial UI layout and settings.
        /// </summary>
        private void SetupUILayout()
        {
            _services.WindowPersistence.LoadAnimationValues();
            ApplySavedTheme();
            HideDebugConsoleByDefault();
            SetupKeyboardShortcuts();
            _controllers.TreeView.SetupTreeViewDragDrop();
        }

        // Lifecycle methods extracted to MainWindow.Lifecycle.cs (#1220)

        // Speaker visual combo box initialization extracted to SpeakerVisualController (#1223)

        // OnThemeApplied moved to MainWindow.Theme.cs

        /// <summary>
        /// Handles settings changes that require tree view refresh (Issue #134)
        /// </summary>
        private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Refresh tree when NPC tag coloring or speaker preferences change (#134, #1223)
            if (e.PropertyName == nameof(SettingsService.EnableNpcTagColoring) ||
                e.PropertyName == nameof(SettingsService.NpcSpeakerPreferences))
            {
                if (_viewModel.CurrentDialog != null)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _viewModel.RefreshTreeViewColors();
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Tree view refreshed after {e.PropertyName} change");
                    });
                }
            }
        }

        /// <summary>
        /// Handles dialog change events from the DialogChangeEventBus.
        /// Updates FlowView panels when dialog structure changes (#436, #451).
        /// Syncs TreeView expand/collapse state with FlowView (#451).
        /// </summary>
        private void OnDialogChanged(object? sender, DialogChangeEventArgs e)
        {
            // Update flowchart for structure changes and node modifications
            if (e.ChangeType == DialogChangeType.DialogRefreshed ||
                e.ChangeType == DialogChangeType.NodeAdded ||
                e.ChangeType == DialogChangeType.NodeDeleted ||
                e.ChangeType == DialogChangeType.NodeMoved ||
                e.ChangeType == DialogChangeType.NodeModified)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"OnDialogChanged: {e.ChangeType} - updating flowchart panels");

                // Update all flowchart panels to reflect the change
                _controllers.Flowchart.UpdateAllPanels();
            }

            // Handle collapse/expand events from FlowView to sync TreeView (#451)
            if (e.Context == "FlowView")
            {
                _controllers.Flowchart.HandleFlowViewCollapseEvent(e);
            }
        }

        private void SetupKeyboardShortcuts()
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "SetupKeyboardShortcuts: Keyboard handler registered");

            // Tunneling event for shortcuts that intercept before TreeView (Ctrl+Shift+Up/Down)
            this.AddHandler(KeyDownEvent, (sender, e) =>
            {
                if (_services.KeyboardShortcuts.HandlePreviewKeyDown(e))
                {
                    e.Handled = true;
                }
            }, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);

            // Standard KeyDown events
            this.KeyDown += (sender, e) =>
            {
                if (_services.KeyboardShortcuts.HandleKeyDown(e))
                {
                    e.Handled = true;
                }
            };
        }

        #region IKeyboardShortcutHandler Implementation

        // File operations
        void IKeyboardShortcutHandler.OnNew() => OnNewClick(null, null!);
        void IKeyboardShortcutHandler.OnOpen() => OnOpenClick(null, null!);
        void IKeyboardShortcutHandler.OnSave() => OnSaveClick(null, null!);

        // Node operations
        void IKeyboardShortcutHandler.OnAddSmartNode() => OnAddSmartNodeClick(null, null!);
        void IKeyboardShortcutHandler.OnAddSiblingNode() => OnAddSiblingNodeClick(null, null!);
        void IKeyboardShortcutHandler.OnAddContextAwareReply() => OnAddContextAwareReply(null, null!);
        void IKeyboardShortcutHandler.OnDeleteNode() => OnDeleteNodeClick(null, null!);

        // Clipboard operations
        void IKeyboardShortcutHandler.OnCopyNode() => OnCopyNodeClick(null, null!);
        void IKeyboardShortcutHandler.OnCutNode() => OnCutNodeClick(null, null!);
        void IKeyboardShortcutHandler.OnPasteAsDuplicate() => OnPasteAsDuplicateClick(null, null!);
        void IKeyboardShortcutHandler.OnPasteAsLink() => OnPasteAsLinkClick(null, null!);

        // Advanced copy
        void IKeyboardShortcutHandler.OnCopyNodeText() => OnCopyNodeTextClick(null, null!);
        void IKeyboardShortcutHandler.OnCopyNodeProperties() => OnCopyNodePropertiesClick(null, null!);
        void IKeyboardShortcutHandler.OnCopyTreeStructure() => OnCopyTreeStructureClick(null, null!);

        // History
        void IKeyboardShortcutHandler.OnUndo() => OnUndoClick(null, null!);
        void IKeyboardShortcutHandler.OnRedo() => OnRedoClick(null, null!);

        // Tree navigation - delegated to TreeViewUIController (#463)
        void IKeyboardShortcutHandler.OnExpandSubnodes() => _controllers.TreeView.OnExpandSubnodesClick(null, null!);
        void IKeyboardShortcutHandler.OnCollapseSubnodes() => _controllers.TreeView.OnCollapseSubnodesClick(null, null!);
        void IKeyboardShortcutHandler.OnMoveNodeUp() => OnMoveNodeUpClick(null, null!);
        void IKeyboardShortcutHandler.OnMoveNodeDown() => OnMoveNodeDownClick(null, null!);
        void IKeyboardShortcutHandler.OnGoToParentNode() => _controllers.TreeView.OnGoToParentNodeClick(null, null!);

        // View operations - Issue #1143: F4 to toggle dialog browser
        void IKeyboardShortcutHandler.OnToggleDialogBrowser() => OnToggleDialogBrowserClick(null, null!);

        // View operations - Issue #339: F5 to open flowchart
        void IKeyboardShortcutHandler.OnOpenFlowchart() => OnFlowchartClick(null, null!);

        // Issue #478: F6 to open conversation simulator
        void IKeyboardShortcutHandler.OnOpenConversationSimulator() => OnConversationSimulatorClick(null, null!);

        // Text editing - Issue #753: Insert token (Ctrl+T)
        void IKeyboardShortcutHandler.OnInsertToken() => OnInsertTokenClick(null, null!);

        #endregion

        // OnAddContextAwareReply moved to MainWindow.NodeHandlers.cs (#1222)

        // OnWindowClosing and CleanupOnClose extracted to MainWindow.Lifecycle.cs (#1220)

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        
        public void AddDebugMessage(string message) => _services.DebugLogging.AddDebugMessage(message);
        public void ClearDebugOutput() => _viewModel.ClearDebugMessages();

        // File menu handlers - delegated to FileMenuController (#466)
        private void OnNewClick(object? sender, RoutedEventArgs e)
            => _controllers.FileMenu.OnNewClick(sender, e);

        private void OnOpenClick(object? sender, RoutedEventArgs e)
            => _controllers.FileMenu.OnOpenClick(sender, e);

        private void OnSaveClick(object? sender, RoutedEventArgs e)
            => _controllers.FileMenu.OnSaveClick(sender, e);

        // SaveCurrentNodeProperties moved to MainWindow.Properties.cs

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

                // 🔧 WORKAROUND (2025-10-23): Simplified options to avoid hang
                // Minimal settings - just title and file types
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

            var directory = System.IO.Path.GetDirectoryName(filePath) ?? "";
            var currentName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            var extension = System.IO.Path.GetExtension(filePath);

            // Show rename dialog
            var newName = await Radoub.UI.Views.RenameDialog.ShowAsync(this, currentName, directory, extension);
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

            var newFilePath = System.IO.Path.Combine(directory, newName + extension);

            try
            {
                // Rename file on disk
                System.IO.File.Move(filePath, newFilePath);
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
            catch (System.IO.IOException ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to rename file: {ex.Message}");
                _viewModel.StatusMessage = $"Failed to rename: {ex.Message}";
            }
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
            => _controllers.FileMenu.OnExitClick(sender, e);

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

        // Recent files - delegated to FileMenuController (#466)
        private void PopulateRecentFilesMenu()
            => _controllers.FileMenu.PopulateRecentFilesMenu();

        // Edit menu handlers - delegated to EditMenuController (#466)
        private void OnCopyNodeTextClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnCopyNodeTextClick(sender, e);

        private void OnCopyNodePropertiesClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnCopyNodePropertiesClick(sender, e);

        private void OnCopyTreeStructureClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnCopyTreeStructureClick(sender, e);

        // View menu handlers
        private void OnClearDebugClick(object? sender, RoutedEventArgs e)
        {
            ClearDebugOutput();
        }

        private void OnLogLevelFilterChanged(object? sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null)
                return;

            var selectedIndex = comboBox.SelectedIndex;
            var filterLevel = selectedIndex switch
            {
                0 => LogLevel.ERROR,
                1 => LogLevel.WARN,
                2 => LogLevel.INFO,
                3 => LogLevel.DEBUG,
                4 => LogLevel.TRACE,
                _ => LogLevel.INFO
            };

            DebugLogger.SetLogLevelFilter(filterLevel);

            // Save the filter level to settings
            SettingsService.Instance.DebugLogFilterLevel = filterLevel;
        }

        private void OnOpenLogFolderClick(object? sender, RoutedEventArgs e)
        {
            _services.DebugLogging.OpenLogFolder();
        }

        private async void OnExportLogsClick(object? sender, RoutedEventArgs e)
        {
            await _services.DebugLogging.ExportLogsAsync(this);
        }

        // Scrap tab handlers
        private void OnRestoreScrapClick(object? sender, RoutedEventArgs e)
        {
            var treeView = this.FindControl<TreeView>("DialogTreeView");
            var selectedNode = treeView?.SelectedItem as TreeViewSafeNode;
            _services.DebugLogging.RestoreFromScrap(selectedNode);
        }

        private async void OnClearScrapClick(object? sender, RoutedEventArgs e)
        {
            await _services.DebugLogging.ClearScrapAsync(this);
        }

        private void OnSwapRolesClick(object? sender, RoutedEventArgs e)
        {
            _services.DebugLogging.SwapScrapRoles();
        }

        private void HideDebugConsoleByDefault()
        {
            try
            {
                var debugTab = this.FindControl<TabItem>("DebugTab");
                if (debugTab != null)
                {
                    // Set visibility from settings (default: false)
                    debugTab.IsVisible = SettingsService.Instance.DebugWindowVisible;
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error setting debug console visibility: {ex.Message}");
            }
        }


        private void OnAboutClick(object? sender, RoutedEventArgs e)
        {
            var aboutWindow = Radoub.UI.Views.AboutWindow.Create(new AboutWindowConfig
            {
                ToolName = "Parley",
                Subtitle = "Dialog Editor for Neverwinter Nights",
                Version = Radoub.UI.Utils.VersionHelper.GetVersion(),
                IconBitmap = new Avalonia.Media.Imaging.Bitmap(
                    Avalonia.Platform.AssetLoader.Open(
                        new System.Uri("avares://Parley/Assets/parley.ico")))
            });
            aboutWindow.Show(this);
        }

        private void OnDocumentationClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var url = "https://github.com/LordOfMyatar/Radoub/wiki";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening documentation: {ex.Message}");
                _viewModel.StatusMessage = "Could not open documentation URL";
            }
        }

        private void OnReportIssueClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var url = "https://github.com/LordOfMyatar/Radoub/issues/new";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening issue page: {ex.Message}");
                _viewModel.StatusMessage = "Could not open issue page URL";
            }
        }

        // Font size is now managed via Settings window only (removed from View menu in #368)

        // Flowchart menu handlers - delegate to FlowchartManager (#457)
        private void OnFlowchartClick(object? sender, RoutedEventArgs e) => _controllers.Flowchart.OpenFloatingFlowchart();

        private void OnFlowchartLayoutClick(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string layoutValue)
            {
                _controllers.Flowchart.ApplyLayout(layoutValue);
            }
        }

        private async void OnExportFlowchartPngClick(object? sender, RoutedEventArgs e) => await _controllers.Flowchart.ExportToPngAsync();

        private async void OnExportFlowchartSvgClick(object? sender, RoutedEventArgs e) => await _controllers.Flowchart.ExportToSvgAsync();

        private void UpdateEmbeddedFlowchartAfterLoad() => _controllers.Flowchart.UpdateAfterLoad();

        /// <summary>
        /// Handles context menu actions from FlowchartPanel (#461).
        /// Routes actions to the appropriate existing handlers.
        /// </summary>
        private void OnFlowchartContextMenuAction(FlowchartContextMenuEventArgs e)
        {
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"MainWindow handling flowchart context action: {e.Action}");

            switch (e.Action)
            {
                case "AddNode":
                    OnAddSmartNodeClick(null, null!);
                    break;
                case "AddSiblingNode":
                    OnAddSiblingNodeClick(null, null!);
                    break;
                case "DeleteNode":
                    OnDeleteNodeClick(null, null!);
                    break;
                case "CutNode":
                    OnCutNodeClick(null, null!);
                    break;
                case "CopyNode":
                    OnCopyNodeClick(null, null!);
                    break;
                case "PasteNode":
                    OnPasteAsDuplicateClick(null, null!);
                    break;
                case "PasteAsLink":
                    OnPasteAsLinkClick(null, null!);
                    break;
                case "ExpandSubnodes":
                    OnExpandSubnodesClick(null, null!);
                    break;
                case "CollapseSubnodes":
                    OnCollapseSubnodesClick(null, null!);
                    break;
                case "MoveUp":
                    OnMoveNodeUpClick(null, null!);
                    break;
                case "MoveDown":
                    OnMoveNodeDownClick(null, null!);
                    break;
                case "GoToLinkTarget":
                case "GoToParent":
                    OnGoToParentNodeClick(null, null!);
                    break;
                default:
                    UnifiedLogger.LogUI(LogLevel.WARN, $"Unknown flowchart context action: {e.Action}");
                    break;
            }
        }

        // Conversation Simulator handler - Issue #478
        private void OnConversationSimulatorClick(object? sender, RoutedEventArgs e)
        {
            var dialog = DialogContextService.Instance.CurrentDialog;
            var filePath = DialogContextService.Instance.CurrentFilePath;

            if (dialog == null || string.IsNullOrEmpty(filePath))
            {
                _viewModel.StatusMessage = "No dialog loaded. Open a dialog file first.";
                return;
            }

            _windows.ShowOrActivate(
                WindowKeys.ConversationSimulator,
                () => new ConversationSimulatorWindow(dialog, filePath));
        }

        // Theme methods moved to MainWindow.Theme.cs

        // Settings handlers
        // Issue #343: Common callback for Settings window close
        private void OnSettingsWindowClosed(SettingsWindow _)
        {
            ApplySavedTheme();
            _viewModel.StatusMessage = "Settings updated";
        }

        private void OnPreferencesClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Issue #343: Use WindowLifecycleManager for Settings window
                _windows.ShowOrActivate(
                    WindowKeys.Settings,
                    () => new SettingsWindow(),
                    OnSettingsWindowClosed);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening preferences: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening preferences: {ex.Message}";
            }
        }

        private void OnGameDirectoriesClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Issue #343: Use WindowLifecycleManager - if open, just activate
                if (_windows.IsOpen(WindowKeys.Settings))
                {
                    _windows.WithWindow<SettingsWindow>(WindowKeys.Settings, w => w.Activate());
                    return;
                }

                // Open preferences with Resource Paths tab selected (tab 0)
                _windows.ShowOrActivate(
                    WindowKeys.Settings,
                    () => new SettingsWindow(initialTab: 0),
                    OnSettingsWindowClosed);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening game directories: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening settings: {ex.Message}";
            }
        }

        private void OnLogSettingsClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Issue #343: Use WindowLifecycleManager - if open, just activate
                if (_windows.IsOpen(WindowKeys.Settings))
                {
                    _windows.WithWindow<SettingsWindow>(WindowKeys.Settings, w => w.Activate());
                    return;
                }

                // Open preferences with Logging tab selected (tab 2)
                _windows.ShowOrActivate(
                    WindowKeys.Settings,
                    () => new SettingsWindow(initialTab: 2),
                    OnSettingsWindowClosed);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening logging settings: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening settings: {ex.Message}";
            }
        }

        private void OnRefreshScriptCacheClick(object? sender, RoutedEventArgs e)
        {
            ScriptService.Instance.ClearCache();
            _viewModel.StatusMessage = "Script cache refreshed";
        }

        // Tree view expand/collapse handlers moved to MainWindow.TreeOps.cs

        private TreeViewSafeNode? _selectedNode;
        // _uiState.IsSettingSelectionProgrammatically moved to _uiState (#525)

        /// <summary>
        /// Handles completion of a TreeView drag-drop operation (#450).
        /// </summary>
        private void OnDragDropCompleted(TreeViewSafeNode draggedNode, DialogNode? newParent, DropPosition position, int insertIndex)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"OnDragDropCompleted: Moving '{draggedNode.DisplayText}' to {(newParent?.DisplayText ?? "ROOT")}, position={position}, index={insertIndex}");

            // Perform the move operation
            _viewModel.MoveNodeToPosition(draggedNode, newParent, insertIndex);
        }

        // Issue #463: Delegated to TreeViewUIController
        private void OnDialogTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
            => _controllers.TreeView.OnDialogTreeViewSelectionChanged(sender, e);

        // Issue #463: Delegated to TreeViewUIController
        private void OnTreeViewItemDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
            => _controllers.TreeView.OnTreeViewItemDoubleTapped(sender, e);

        // PopulatePropertiesPanel, OnPropertyChanged, field handlers, auto-save moved to MainWindow.Properties.cs
        // RefreshTreeDisplay, RefreshTreeDisplayPreserveState, expansion state moved to MainWindow.TreeOps.cs

        // Properties panel handlers
        private void OnAddConditionsParamClick(object? sender, RoutedEventArgs e)
        {
            _services.ParameterUI.OnAddConditionsParamClick();
        }

        private void OnAddActionsParamClick(object? sender, RoutedEventArgs e)
        {
            _services.ParameterUI.OnAddActionsParamClick();
        }

        private async void OnSuggestConditionsParamClick(object? sender, RoutedEventArgs e)
            => await _controllers.ParameterBrowser.OnSuggestConditionsParamClickAsync();

        private async void OnSuggestActionsParamClick(object? sender, RoutedEventArgs e)
            => await _controllers.ParameterBrowser.OnSuggestActionsParamClickAsync();

        #region Script Browser Delegation Methods
        // These methods delegate to controllers/services but are kept as instance methods
        // because they're passed as callbacks to PropertyAutoSaveService and PropertyPanelPopulator
        // during construction before the controllers are initialized.

        private Task LoadParameterDeclarationsAsync(string scriptName, bool isCondition)
            => _controllers.ParameterBrowser.LoadParameterDeclarationsAsync(scriptName, isCondition);

        private Task LoadScriptPreviewAsync(string scriptName, bool isCondition)
            => _services.ScriptPreview.LoadScriptPreviewAsync(scriptName, isCondition);

        private void ClearScriptPreview(bool isCondition)
            => _services.ScriptPreview.ClearScriptPreview(isCondition);

        // Issue #664: Population wrapper - always passes focusNewRow=false to prevent focus stealing
        private void AddParameterRow(StackPanel parent, string key, string value, bool isCondition)
            => _services.ParameterUI.AddParameterRow(parent, key, value, isCondition, focusNewRow: false);

        #endregion

        // OnBrowseSoundClick moved to MainWindow.SoundHandlers.cs

        private async void OnBrowseCreatureClick(object? sender, RoutedEventArgs e)
        {
            await _services.ResourceBrowser.BrowseCreatureAsync(this);
        }


        private async void OnInsertTokenClick(object? sender, RoutedEventArgs e)
        {
            // Set flag to suppress tree refresh during token insertion
            _uiState.IsInsertingToken = true;
            try
            {
                // Capture cursor position BEFORE opening dialog (OnFieldLostFocus fires when button clicked)
                var textBox = this.FindControl<TextBox>("TextTextBox");
                var savedSelStart = textBox?.SelectionStart ?? 0;
                var savedSelEnd = textBox?.SelectionEnd ?? 0;
                var savedText = textBox?.Text ?? "";

                var tokenWindow = new TokenSelectorWindow();
                var result = await tokenWindow.ShowDialog<bool>(this);

                if (result && !string.IsNullOrEmpty(tokenWindow.SelectedToken) && textBox != null)
                {
                    // Use saved values since focus was lost when dialog opened
                    var selStart = savedSelStart;
                    var selLength = savedSelEnd - savedSelStart;
                    var currentText = savedText;

                    string newText;
                    int newCursorPos;

                    // Determine if we need a space before the token
                    // Add space if: not at start, and previous char is not whitespace
                    var needsSpaceBefore = selStart > 0 &&
                        !char.IsWhiteSpace(currentText[selStart - 1]);
                    var tokenToInsert = needsSpaceBefore
                        ? " " + tokenWindow.SelectedToken
                        : tokenWindow.SelectedToken;

                    if (selLength > 0)
                    {
                        // Replace selection
                        newText = currentText.Remove(selStart, selLength).Insert(selStart, tokenToInsert);
                        newCursorPos = selStart + tokenToInsert.Length;
                    }
                    else
                    {
                        // Insert at cursor
                        newText = currentText.Insert(selStart, tokenToInsert);
                        newCursorPos = selStart + tokenToInsert.Length;
                    }

                    textBox.Text = newText;

                    // Save directly to node without tree refresh (avoids focus jump)
                    if (_selectedNode?.OriginalNode?.Text != null)
                    {
                        _selectedNode.OriginalNode.Text.Strings[0] = newText;
                        _viewModel.HasUnsavedChanges = true;
                        _viewModel.StatusMessage = "Text updated with token";
                    }

                    // Restore cursor position and focus
                    textBox.SelectionStart = newCursorPos;
                    textBox.SelectionEnd = newCursorPos;
                    textBox.Focus();
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Error inserting token: {ex.Message}";
            }
            finally
            {
                _uiState.IsInsertingToken = false;
            }
        }

        private void OnRecentCreatureTagSelected(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_selectedNode == null || _selectedNode is TreeViewRootNode)
                {
                    return;
                }

                var comboBox = sender as ComboBox;
                if (comboBox?.SelectedItem is string selectedTag && !string.IsNullOrEmpty(selectedTag))
                {
                    // Populate Speaker field
                    var speakerTextBox = this.FindControl<TextBox>("SpeakerTextBox");
                    if (speakerTextBox != null)
                    {
                        speakerTextBox.Text = selectedTag;
                        AutoSaveProperty("SpeakerTextBox");
                    }

                    _viewModel.StatusMessage = $"Selected recent tag: {selectedTag}";
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Applied recent creature tag: {selectedTag}");

                    // Clear selection so same tag can be selected again
                    comboBox.SelectedItem = null;
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error selecting recent tag: {ex.Message}");
            }
        }

        // Speaker visual event handlers - delegates to SpeakerVisualController (#1223)
        private void OnSpeakerShapeChanged(object? sender, SelectionChangedEventArgs e)
            => _controllers.SpeakerVisual.OnSpeakerShapeChanged(sender, e);

        private void OnSpeakerColorChanged(object? sender, SelectionChangedEventArgs e)
            => _controllers.SpeakerVisual.OnSpeakerColorChanged(sender, e);

        // Issue #5: LoadCreaturesFromModuleDirectory removed - creature loading now done lazily
        // in ResourceBrowserManager.BrowseCreatureAsync when user opens the creature picker

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
                var settings = SettingsService.Instance;
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

        // Module info - delegated to FileMenuController (#466)
        private void UpdateModuleInfo(string dialogFilePath)
            => _controllers.FileMenu.UpdateModuleInfo(dialogFilePath);

        private void ClearModuleInfo()
            => _controllers.FileMenu.ClearModuleInfo();


        // OnPlaySoundClick, OnSoundPlaybackStopped, OnSoundsetPlayClick moved to MainWindow.SoundHandlers.cs

        // OnConversationSettingChanged moved to MainWindow.Properties.cs

        private void OnBrowseConversationScriptClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            _controllers.ScriptBrowser.OnBrowseConversationScriptClick(button.Tag?.ToString());
        }

        // Quest UI event handlers - delegated to QuestUIController (#465)
        private void OnQuestTagTextChanged(object? sender, TextChangedEventArgs e) =>
            _controllers.Quest.OnQuestTagTextChanged(sender, e);

        private void OnQuestTagLostFocus(object? sender, RoutedEventArgs e) =>
            _controllers.Quest.OnQuestTagLostFocus(sender, e);

        private void OnQuestEntryTextChanged(object? sender, TextChangedEventArgs e) =>
            _controllers.Quest.OnQuestEntryTextChanged(sender, e);

        private void OnQuestEntryLostFocus(object? sender, RoutedEventArgs e) =>
            _controllers.Quest.OnQuestEntryLostFocus(sender, e);

        private void OnBrowseQuestClick(object? sender, RoutedEventArgs e) =>
            _controllers.Quest.OnBrowseQuestClick(sender, e);

        private void OnBrowseQuestEntryClick(object? sender, RoutedEventArgs e) =>
            _controllers.Quest.OnBrowseQuestEntryClick(sender, e);

        private void OnClearQuestTagClick(object? sender, RoutedEventArgs e) =>
            _controllers.Quest.OnClearQuestTagClick(sender, e);

        private void OnClearQuestEntryClick(object? sender, RoutedEventArgs e) =>
            _controllers.Quest.OnClearQuestEntryClick(sender, e);

        // FindSoundFile removed - replaced by SoundPlaybackService (Issue #895)

        private void OnBrowseConditionalScriptClick(object? sender, RoutedEventArgs e)
            => _controllers.ScriptBrowser.OnBrowseConditionalScriptClick();

        private void OnBrowseActionScriptClick(object? sender, RoutedEventArgs e)
            => _controllers.ScriptBrowser.OnBrowseActionScriptClick();

        private void OnEditConditionalScriptClick(object? sender, RoutedEventArgs e)
            => _controllers.ScriptBrowser.OnEditConditionalScriptClick();

        private void OnEditActionScriptClick(object? sender, RoutedEventArgs e)
            => _controllers.ScriptBrowser.OnEditActionScriptClick();

        // Node handlers (OnAddSmartNodeClick, OnAddSiblingNodeClick, OnAddEntryClick,
        // OnAddPCReplyClick, OnDeleteNodeClick, OnMoveNodeUpClick, OnMoveNodeDownClick)
        // moved to MainWindow.NodeHandlers.cs (#1222)

        // ExpandToNode, FindParentNode, FindParentNodeRecursive moved to MainWindow.TreeOps.cs

        private TreeViewSafeNode? GetSelectedTreeNode()
        {
            var treeView = this.FindControl<TreeView>("DialogTreeView");
            return treeView?.SelectedItem as TreeViewSafeNode;
        }

        private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Load journal when file is loaded (CurrentFileName is set AFTER CurrentDialog)
            if (e.PropertyName == nameof(MainViewModel.CurrentFileName))
            {
                if (!string.IsNullOrEmpty(_viewModel.CurrentFileName))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"CurrentFileName changed to: {UnifiedLogger.SanitizePath(_viewModel.CurrentFileName)} - loading journal");
                    await _controllers.Quest.LoadJournalForCurrentModuleAsync();
                }
            }

            // Watch for node selection requests - ViewModel finds the node, View sets TreeView.SelectedItem
            // This is needed because TreeView binding doesn't work well with lazily-populated children
            if (e.PropertyName == nameof(MainViewModel.SelectedTreeNode))
            {
                var selectedNode = _viewModel.SelectedTreeNode;
                // Only handle non-ROOT programmatic selection
                // Skip if selection came from TreeView (flag set by respective handler)
                if (selectedNode != null && !(selectedNode is TreeViewRootNode) &&
                    !_uiState.IsSettingSelectionProgrammatically)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"View: SelectedTreeNode changed to '{selectedNode.DisplayText}', scheduling selection");

                    // Defer selection to allow visual tree to render expanded children
                    // Use Background priority (lower than Loaded) to run AFTER layout has completed
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        var treeView = this.FindControl<TreeView>("DialogTreeView");
                        if (treeView != null)
                        {
                            // Small delay to ensure TreeView has rendered expanded children
                            await Task.Delay(50);

                            // Expand ALL ancestors to ensure node is visible in visual tree
                            _services.NodeCreation.ExpandToNode(treeView, selectedNode);
                            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                $"View: Expanded ancestors for '{selectedNode.DisplayText}'");

                            // Set flag to prevent feedback loop when setting SelectedItem
                            _uiState.IsSettingSelectionProgrammatically = true;
                            try
                            {
                                // Force set TreeView selection (binding alone doesn't work for lazy-loaded children)
                                treeView.SelectedItem = selectedNode;
                                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                    $"View: Set TreeView.SelectedItem to '{selectedNode.DisplayText}'");
                            }
                            finally
                            {
                                _uiState.IsSettingSelectionProgrammatically = false;
                            }
                        }
                    }, global::Avalonia.Threading.DispatcherPriority.Background);
                }
            }
        }

        // Clipboard/Undo/Redo - delegated to EditMenuController (#466)
        private void OnUndoClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnUndoClick(sender, e);

        private void OnRedoClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnRedoClick(sender, e);

        private void OnCutNodeClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnCutNodeClick(sender, e);

        private void OnCopyNodeClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnCopyNodeClick(sender, e);

        private void OnPasteAsDuplicateClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnPasteAsDuplicateClick(sender, e);

        private void OnPasteAsLinkClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnPasteAsLinkClick(sender, e);

        // Issue #463: Expand/Collapse/Navigate delegated to TreeViewUIController
        private void OnExpandSubnodesClick(object? sender, RoutedEventArgs e)
            => _controllers.TreeView.OnExpandSubnodesClick(sender, e);

        private void OnCollapseSubnodesClick(object? sender, RoutedEventArgs e)
            => _controllers.TreeView.OnCollapseSubnodesClick(sender, e);

        private void OnGoToParentNodeClick(object? sender, RoutedEventArgs e)
            => _controllers.TreeView.OnGoToParentNodeClick(sender, e);

        // GetSuccessBrush moved to MainWindow.Theme.cs
    }
}

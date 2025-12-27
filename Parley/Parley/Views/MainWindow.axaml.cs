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
using Avalonia.VisualTree;
using DialogEditor.ViewModels;
using DialogEditor.Models;
using DialogEditor.Utils;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.Parsers;
using DialogEditor.Plugins;
using Parley.Services;
using Parley.Views.Helpers;

namespace DialogEditor.Views
{
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
            // Plugin panel requires window reference
            _services.PluginPanel = new PluginPanelManager(this);
            _services.PluginPanel.SetPluginManager(_services.Plugin);

            // Property services
            _services.PropertyPopulator = new PropertyPanelPopulator(this);
            _services.PropertyAutoSave = new PropertyAutoSaveService(
                findControl: this.FindControl<Control>,
                refreshTreeDisplay: RefreshTreeDisplayPreserveState,
                loadScriptPreview: (script, isCondition) => _ = LoadScriptPreviewAsync(script, isCondition),
                clearScriptPreview: ClearScriptPreview,
                triggerDebouncedAutoSave: TriggerDebouncedAutoSave);
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
                getCurrentFilePath: () => _viewModel.CurrentFilePath);
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
            _services.PluginSelectionSync = new PluginSelectionSyncHelper(
                viewModel: _viewModel,
                findControl: this.FindControl<Control>,
                getSelectedNode: () => _selectedNode,
                setSelectedTreeItem: node =>
                {
                    _controls.WithControl<TreeView>("DialogTreeView", tv => tv.SelectedItem = node);
                });

            // TreeView and dialog services
            _services.DragDrop.DropCompleted += OnDragDropCompleted;
            _services.Dialog = new DialogFactory(this);
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
                setIsSettingSelectionProgrammatically: value => _uiState.IsSettingSelectionProgrammatically = value);

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
                syncSelectionToFlowcharts: node => _controllers.Flowchart.SyncSelectionToFlowcharts(node),
                updatePluginSelectionSync: () => _services.PluginSelectionSync.UpdateDialogContextSelectedNode());

            _controllers.ScriptBrowser = new ScriptBrowserController(
                window: this,
                controls: _controls,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                autoSaveProperty: AutoSaveProperty,
                isPopulatingProperties: () => _uiState.IsPopulatingProperties,
                parameterUIManager: _services.ParameterUI,
                triggerAutoSave: () => { _viewModel.HasUnsavedChanges = true; TriggerDebouncedAutoSave(); });

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
                showSaveAsDialogAsync: ShowSaveAsDialogAsync);

            _controllers.EditMenu = new EditMenuController(
                window: this,
                getViewModel: () => _viewModel,
                getSelectedNode: GetSelectedTreeNode);
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

        /// <summary>
        /// Handles window opened event - restores state and starts plugins.
        /// </summary>
        private async void OnWindowOpened(object? sender, EventArgs e)
        {
            await _services.WindowPersistence.RestoreWindowPositionAsync();
            PopulateRecentFilesMenu();
            await _services.WindowPersistence.HandleStartupFileAsync(_viewModel);

            var startedPlugins = await _services.Plugin.StartEnabledPluginsAsync();
            if (startedPlugins.Count > 0)
            {
                _viewModel.StatusMessage = $"Plugins active: {string.Join(", ", startedPlugins)}";
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Started plugins: {string.Join(", ", startedPlugins)}");
            }

            if (SettingsService.Instance.FlowchartVisible)
            {
                _controllers.Flowchart.RestoreOnStartup();
            }
        }

        /// <summary>
        /// Handles window property changes for position/size persistence.
        /// </summary>
        private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!_services.WindowPersistence.IsRestoringPosition)
            {
                if (e.Property.Name == nameof(Width) || e.Property.Name == nameof(Height))
                {
                    _services.WindowPersistence.SaveWindowPosition();
                }
            }
        }

        /// <summary>
        /// Handles debug messages collection changes - auto-scrolls to latest message.
        /// </summary>
        private void OnDebugMessagesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                var debugListBox = this.FindControl<ListBox>("DebugListBox");
                if (debugListBox != null && debugListBox.ItemCount > 0)
                {
                    debugListBox.ScrollIntoView(debugListBox.ItemCount - 1);
                }
            }
        }

        private void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            // Controls are now available, restore settings
            _services.WindowPersistence.RestoreDebugSettings();
            _services.WindowPersistence.RestorePanelSizes();

            // Initialize menu checkmark states
            _controllers.Flowchart.UpdateLayoutMenuChecks();

            // Initialize NPC speaker visual preference ComboBoxes (Issue #16, #36)
            InitializeSpeakerVisualComboBoxes();
        }

        private void InitializeSpeakerVisualComboBoxes()
        {
            // Populate Shape ComboBox with NPC shapes (Triangle, Diamond, Pentagon, Star)
            var shapeComboBox = this.FindControl<ComboBox>("SpeakerShapeComboBox");
            if (shapeComboBox != null)
            {
                shapeComboBox.Items.Clear();
                shapeComboBox.Items.Add(SpeakerVisualHelper.SpeakerShape.Triangle);
                shapeComboBox.Items.Add(SpeakerVisualHelper.SpeakerShape.Diamond);
                shapeComboBox.Items.Add(SpeakerVisualHelper.SpeakerShape.Pentagon);
                shapeComboBox.Items.Add(SpeakerVisualHelper.SpeakerShape.Star);
            }

            // Populate Color ComboBox with color-blind friendly palette
            var colorComboBox = this.FindControl<ComboBox>("SpeakerColorComboBox");
            if (colorComboBox != null)
            {
                colorComboBox.Items.Clear();
                colorComboBox.Items.Add(new ComboBoxItem { Content = "Orange", Tag = SpeakerVisualHelper.ColorPalette.Orange });
                colorComboBox.Items.Add(new ComboBoxItem { Content = "Purple", Tag = SpeakerVisualHelper.ColorPalette.Purple });
                colorComboBox.Items.Add(new ComboBoxItem { Content = "Teal", Tag = SpeakerVisualHelper.ColorPalette.Teal });
                colorComboBox.Items.Add(new ComboBoxItem { Content = "Amber", Tag = SpeakerVisualHelper.ColorPalette.Amber });
                colorComboBox.Items.Add(new ComboBoxItem { Content = "Pink", Tag = SpeakerVisualHelper.ColorPalette.Pink });
            }
        }

        // OnThemeApplied moved to MainWindow.Theme.cs

        /// <summary>
        /// Handles settings changes that require tree view refresh (Issue #134)
        /// </summary>
        private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Refresh tree when NPC tag coloring setting changes
            if (e.PropertyName == nameof(SettingsService.EnableNpcTagColoring))
            {
                if (_viewModel.CurrentDialog != null)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _viewModel.RefreshTreeViewColors();
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, "Tree view refreshed after NPC tag coloring setting change");
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

        // View operations - Issue #339: F5 to open flowchart
        void IKeyboardShortcutHandler.OnOpenFlowchart() => OnFlowchartClick(null, null!);

        // Issue #478: F6 to open conversation simulator
        void IKeyboardShortcutHandler.OnOpenConversationSimulator() => OnConversationSimulatorClick(null, null!);

        #endregion

        private void OnAddContextAwareReply(object? sender, RoutedEventArgs e)
        {
            // Phase 1 Bug Fix: Format-correct node creation
            // ROOT → Entry (NPC speech)
            // Entry → PC Reply (player response)
            // PC Reply → Entry (NPC response)
            // Reply structs have NO Speaker field - all replies are PC

            var selectedNode = GetSelectedTreeNode();

            if (selectedNode == null || selectedNode is TreeViewRootNode)
            {
                // No selection or ROOT → Create Entry
                OnAddEntryClick(sender, e);
            }
            else
            {
                var parentNode = selectedNode.OriginalNode;

                if (parentNode.Type == DialogNodeType.Entry)
                {
                    // Entry → PC Reply
                    OnAddPCReplyClick(sender, e);
                }
                else // Reply node (always PC - Reply structs don't have Speaker)
                {
                    // PC Reply → Entry (NPC response)
                    OnAddEntryClick(sender, e);
                }
            }
        }

        private async void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Phase 1 Step 4: Check for unsaved changes
            if (_viewModel.HasUnsavedChanges)
            {
                // Cancel the close to show dialog
                e.Cancel = true;

                // Check if auto-save timer is running - complete it first
                if (_autoSaveTimer != null && _autoSaveTimer.Enabled)
                {
                    _viewModel.StatusMessage = "Waiting for auto-save to complete...";
                    _autoSaveTimer.Stop();
                    await AutoSaveToFileAsync();
                }

                // Show unsaved changes prompt
                var fileName = string.IsNullOrEmpty(_viewModel.CurrentFileName)
                    ? "this file"
                    : System.IO.Path.GetFileName(_viewModel.CurrentFileName);

                var shouldSave = await _services.Dialog.ShowConfirmDialogAsync(
                    "Unsaved Changes",
                    $"Do you want to save changes to {fileName}"
                );

                if (shouldSave)
                {
                    // Save before closing
                    if (string.IsNullOrEmpty(_viewModel.CurrentFileName))
                    {
                        // No filename - need Save As dialog
                        _viewModel.StatusMessage = "Cannot auto-save without filename. Use File → Save As first.";
                        return; // Don't close
                    }

                    // Issue #8: Check save result - offer Save As if save fails
                    var saveSuccess = await _viewModel.SaveDialogAsync(_viewModel.CurrentFileName);
                    if (!saveSuccess)
                    {
                        // Save failed (e.g., read-only file) - offer Save As
                        var saveAs = await _services.Dialog.ShowSaveErrorDialogAsync(_viewModel.StatusMessage);
                        if (saveAs)
                        {
                            // Show Save As dialog
                            await ShowSaveAsDialogAsync();
                            // Check if save succeeded after Save As
                            if (_viewModel.HasUnsavedChanges)
                            {
                                // User cancelled Save As or it failed - don't close
                                return;
                            }
                        }
                        else
                        {
                            // User chose Cancel - ask if they want to discard
                            var discardChanges = await _services.Dialog.ShowConfirmDialogAsync(
                                "Discard Changes?",
                                "Save failed. Do you want to discard changes and close anyway?");
                            if (!discardChanges)
                            {
                                return; // Don't close
                            }
                        }
                    }
                }

                // Now close (unhook event to prevent recursion, cleanup runs in second close)
                this.Closing -= OnWindowClosing;
                CleanupOnClose();
                this.Close();
                return;
            }

            // Clean up resources when window actually closes
            CleanupOnClose();
        }

        /// <summary>
        /// Clean up all resources when the window closes.
        /// Called from OnWindowClosing in both cancel/reclose path and normal close path.
        /// </summary>
        private void CleanupOnClose()
        {
            _autoSaveTimer?.Stop();
            _autoSaveTimer?.Dispose();
            _services.Audio.Dispose();

            // Issue #343: Close all managed windows (Settings, Flowchart)
            _windows.CloseAll();

            // Close browser windows managed by ScriptBrowserController
            _controllers.ScriptBrowser.CloseAllBrowserWindows();

            // Close plugin panel windows (Epic 3 / #225)
            _services.PluginPanel.Dispose();

            // Dispose plugin selection sync helper (Epic 40 Phase 3 / #234)
            _services.PluginSelectionSync.Dispose();

            // Save window position on close
            _services.WindowPersistence.SaveWindowPosition();
        }

        
        
        
        
        // Phase 1 Step 4: Removed UnsavedChangesDialog - auto-save provides safety

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
                    // CRITICAL FIX: Save current node properties before saving file
                    SaveCurrentNodeProperties();

                    var filePath = file.Path.LocalPath;
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


        private async void OnAboutClick(object? sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow();
            await aboutWindow.ShowDialog(this);
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

        private void OnPluginPanelsClick(object? sender, RoutedEventArgs e)
        {
            // Debug: Log all registered panels
            var allPanels = DialogEditor.Plugins.Services.PluginUIService.GetAllRegisteredPanels().ToList();
            UnifiedLogger.LogPlugin(LogLevel.INFO, $"OnPluginPanelsClick: {allPanels.Count} registered panels total");
            foreach (var p in allPanels)
            {
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"  Panel: {p.FullPanelId}, PanelId={p.PanelId}, PluginId={p.PluginId}");
            }

            var closedPanels = _services.PluginPanel.GetClosedPanels().ToList();
            UnifiedLogger.LogPlugin(LogLevel.INFO, $"OnPluginPanelsClick: {closedPanels.Count} closed panels");

            if (closedPanels.Count == 0)
            {
                if (allPanels.Count == 0)
                {
                    _viewModel.StatusMessage = "No plugin panels registered (plugin may not have started)";
                }
                else
                {
                    _viewModel.StatusMessage = "All plugin panels are already open";
                }
                return;
            }

            // Reopen all closed panels
            foreach (var panel in closedPanels)
            {
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Reopening panel: {panel.FullPanelId}");
                _services.PluginPanel.ReopenPanel(panel);
            }

            _viewModel.StatusMessage = $"Reopened {closedPanels.Count} plugin panel(s)";
        }

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
                    () => new SettingsWindow(pluginManager: _services.Plugin),
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
                    () => new SettingsWindow(initialTab: 0, pluginManager: _services.Plugin),
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
                    () => new SettingsWindow(initialTab: 2, pluginManager: _services.Plugin),
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
            => await _controllers.ScriptBrowser.OnSuggestConditionsParamClickAsync();

        private async void OnSuggestActionsParamClick(object? sender, RoutedEventArgs e)
            => await _controllers.ScriptBrowser.OnSuggestActionsParamClickAsync();

        #region Script Browser Delegation Methods
        // These methods delegate to ScriptBrowserController but are kept as instance methods
        // because they're passed as callbacks to PropertyAutoSaveService and PropertyPanelPopulator
        // during construction before the controller is initialized.

        private Task LoadParameterDeclarationsAsync(string scriptName, bool isCondition)
            => _controllers.ScriptBrowser.LoadParameterDeclarationsAsync(scriptName, isCondition);

        private Task LoadScriptPreviewAsync(string scriptName, bool isCondition)
            => _controllers.ScriptBrowser.LoadScriptPreviewAsync(scriptName, isCondition);

        private void ClearScriptPreview(bool isCondition)
            => _controllers.ScriptBrowser.ClearScriptPreview(isCondition);

        private void AddParameterRow(StackPanel parent, string key, string value, bool isCondition)
            => _services.ParameterUI.AddParameterRow(parent, key, value, isCondition);

        #endregion

        private async void OnBrowseSoundClick(object? sender, RoutedEventArgs e)
        {
            // Phase 2 Fix: Don't allow sound browser when no node selected or ROOT selected
            if (_selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a dialog node first";
                return;
            }

            if (_selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Cannot assign sounds to ROOT. Select a dialog node instead.";
                return;
            }

            try
            {
                var soundBrowser = new SoundBrowserWindow();
                var result = await soundBrowser.ShowDialog<string?>(this);

                if (!string.IsNullOrEmpty(result))
                {
                    // Update the sound field with selected sound
                    var soundTextBox = this.FindControl<TextBox>("SoundTextBox");
                    if (soundTextBox != null)
                    {
                        soundTextBox.Text = result;
                        // Trigger auto-save
                        AutoSaveProperty("SoundTextBox");
                    }
                    _viewModel.StatusMessage = $"Selected sound: {result}";
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening sound browser: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening sound browser: {ex.Message}";
            }
        }

        private async void OnBrowseCreatureClick(object? sender, RoutedEventArgs e)
        {
            await _services.ResourceBrowser.BrowseCreatureAsync(this);
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

        // NPC Speaker Visual Preferences (Issue #16, #36)
        private void OnSpeakerShapeChanged(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Don't trigger during property population
                if (_uiState.IsPopulatingProperties) return;

                var comboBox = sender as ComboBox;
                var speakerTextBox = this.FindControl<TextBox>("SpeakerTextBox");

                if (comboBox?.SelectedItem != null && speakerTextBox != null && !string.IsNullOrEmpty(speakerTextBox.Text))
                {
                    var speakerTag = speakerTextBox.Text.Trim();
                    if (Enum.TryParse<SpeakerVisualHelper.SpeakerShape>(comboBox.SelectedItem.ToString(), out var shape))
                    {
                        SettingsService.Instance.SetSpeakerPreference(speakerTag, null, shape);

                        // Refresh tree and restore selection (Issue #134)
                        if (_selectedNode?.OriginalNode != null)
                        {
                            _viewModel.RefreshTreeViewColors(_selectedNode.OriginalNode);
                        }
                        else
                        {
                            _viewModel.RefreshTreeViewColors();
                        }

                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Set speaker '{speakerTag}' shape to {shape}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error setting speaker shape: {ex.Message}");
            }
        }

        private void OnSpeakerColorChanged(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Don't trigger during property population
                if (_uiState.IsPopulatingProperties) return;

                var comboBox = sender as ComboBox;
                var speakerTextBox = this.FindControl<TextBox>("SpeakerTextBox");

                if (comboBox?.SelectedItem is ComboBoxItem item && speakerTextBox != null && !string.IsNullOrEmpty(speakerTextBox.Text))
                {
                    var speakerTag = speakerTextBox.Text.Trim();
                    var color = item.Tag as string;
                    if (!string.IsNullOrEmpty(color))
                    {
                        SettingsService.Instance.SetSpeakerPreference(speakerTag, color, null);

                        // Refresh tree and restore selection (Issue #134)
                        if (_selectedNode?.OriginalNode != null)
                        {
                            _viewModel.RefreshTreeViewColors(_selectedNode.OriginalNode);
                        }
                        else
                        {
                            _viewModel.RefreshTreeViewColors();
                        }

                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Set speaker '{speakerTag}' color to {color}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error setting speaker color: {ex.Message}");
            }
        }

        // Issue #5: LoadCreaturesFromModuleDirectory removed - creature loading now done lazily
        // in ResourceBrowserManager.BrowseCreatureAsync when user opens the creature picker

        // Module info - delegated to FileMenuController (#466)
        private void UpdateModuleInfo(string dialogFilePath)
            => _controllers.FileMenu.UpdateModuleInfo(dialogFilePath);

        private void ClearModuleInfo()
            => _controllers.FileMenu.ClearModuleInfo();


        private void OnPlaySoundClick(object? sender, RoutedEventArgs e)
        {
            var soundTextBox = this.FindControl<TextBox>("SoundTextBox");
            var soundFileName = soundTextBox?.Text?.Trim();

            if (string.IsNullOrEmpty(soundFileName))
            {
                _viewModel.StatusMessage = "No sound file specified";
                return;
            }

            try
            {
                // Find the sound file in game paths
                var soundPath = FindSoundFile(soundFileName);
                if (soundPath == null)
                {
                    _viewModel.StatusMessage = $"⚠ Sound file not found: {soundFileName}";
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound file not found: {soundFileName}");
                    return;
                }

                _services.Audio.Play(soundPath);
                _viewModel.StatusMessage = $"Playing: {soundFileName}";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Playing sound: {soundPath}");
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"❌ Error playing sound: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to play sound: {ex.Message}");
            }
        }

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

        /// <summary>
        /// Find a sound file by searching all configured paths and categories.
        /// Same logic as SoundBrowserWindow for consistency.
        /// </summary>
        private string? FindSoundFile(string filename)
        {
            // Add extension if not present
            if (!filename.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
                !filename.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                filename += ".wav"; // NWN default is WAV
            }

            var categories = new[] { "ambient", "dialog", "music", "soundset", "amb", "dlg", "mus", "sts" };
            var basePaths = new List<string>();

            // Add user Documents path
            var userPath = SettingsService.Instance.NeverwinterNightsPath;
            if (!string.IsNullOrEmpty(userPath) && System.IO.Directory.Exists(userPath))
            {
                basePaths.Add(userPath);
            }

            // Add game installation path + data subdirectory
            var installPath = SettingsService.Instance.BaseGameInstallPath;
            if (!string.IsNullOrEmpty(installPath) && System.IO.Directory.Exists(installPath))
            {
                basePaths.Add(installPath);

                var dataPath = System.IO.Path.Combine(installPath, "data");
                if (System.IO.Directory.Exists(dataPath))
                {
                    basePaths.Add(dataPath);
                }
            }

            // Search all combinations
            foreach (var basePath in basePaths)
            {
                foreach (var category in categories)
                {
                    var soundPath = System.IO.Path.Combine(basePath, category, filename);
                    if (System.IO.File.Exists(soundPath))
                    {
                        return soundPath;
                    }
                }
            }

            return null;
        }

        private void OnBrowseConditionalScriptClick(object? sender, RoutedEventArgs e)
            => _controllers.ScriptBrowser.OnBrowseConditionalScriptClick();

        private void OnBrowseActionScriptClick(object? sender, RoutedEventArgs e)
            => _controllers.ScriptBrowser.OnBrowseActionScriptClick();

        private void OnEditConditionalScriptClick(object? sender, RoutedEventArgs e)
            => _controllers.ScriptBrowser.OnEditConditionalScriptClick();

        private void OnEditActionScriptClick(object? sender, RoutedEventArgs e)
            => _controllers.ScriptBrowser.OnEditActionScriptClick();

        // Node creation handlers - Phase 1 Step 3/4
        /// <summary>
        /// Smart Add Node - Context-aware node creation with auto-focus (Phase 2)
        /// </summary>
        private async void OnAddSmartNodeClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            await _services.NodeCreation.CreateSmartNodeAsync(selectedNode);
        }

        /// <summary>
        /// Issue #150: Add sibling node - creates node at same level as current selection.
        /// Uses parent of selected node as target for new node creation.
        /// </summary>
        private async void OnAddSiblingNodeClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a node first";
                return;
            }

            // Cannot add sibling to ROOT
            if (selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Cannot add sibling to ROOT - use Add Node instead";
                return;
            }

            // Cannot add sibling to link nodes
            if (selectedNode.IsChild)
            {
                _viewModel.StatusMessage = "Cannot add sibling to link nodes - select the parent node first";
                return;
            }

            // Find the parent node in the tree
            var treeView = this.FindControl<TreeView>("DialogTreeView");
            if (treeView == null) return;

            var parentNode = _services.NodeCreation.FindParentNode(treeView, selectedNode);

            // Add node as child of parent (sibling of selected)
            // This creates a new node at the same level as the selected node
            await _services.NodeCreation.CreateSmartNodeAsync(parentNode);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Added sibling node to: {selectedNode.DisplayText}");
        }


        // ExpandToNode, FindParentNode, FindParentNodeRecursive moved to MainWindow.TreeOps.cs

        private void OnAddEntryClick(object? sender, RoutedEventArgs e)
        {
            // Phase 1 Bug Fix: Entry nodes can be root-level OR child of Reply nodes
            var selectedNode = GetSelectedTreeNode();
            _viewModel.AddEntryNode(selectedNode);

            // Trigger auto-save after node creation
            TriggerDebouncedAutoSave();
        }

        // Phase 1 Bug Fix: Removed OnAddNPCReplyClick - use OnAddEntryClick for NPC responses after PC

        private void OnAddPCReplyClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a parent node first";
                return;
            }

            // Check if ROOT selected
            if (selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Cannot add PC Reply to ROOT. Select ROOT to add Entry instead.";
                return;
            }

            _viewModel.AddPCReplyNode(selectedNode);

            // Trigger auto-save after node creation
            TriggerDebouncedAutoSave();
        }

        private async void OnDeleteNodeClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a node to delete";
                return;
            }

            // Issue #17: Block ROOT deletion silently with status message only (no dialog)
            if (selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Cannot delete ROOT node";
                return;
            }

            // Check if delete confirmation is enabled (Issue #14)
            bool confirmed = true;
            if (SettingsService.Instance.ShowDeleteConfirmation)
            {
                // Confirm deletion with "Don't show this again" option
                confirmed = await _services.Dialog.ShowConfirmDialogAsync(
                    "Delete Node",
                    $"Are you sure you want to delete this node and all its children?\n\n\"{selectedNode.DisplayText}\"",
                    showDontAskAgain: true
                );
            }

            if (confirmed)
            {
                _viewModel.DeleteNode(selectedNode);
            }
        }

        // Phase 2a: Node Reordering
        public void OnMoveNodeUpClick(object? sender, RoutedEventArgs e)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "🔼 OnMoveNodeUpClick called");
            var selectedNode = GetSelectedTreeNode();
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Selected node: {selectedNode?.DisplayText ?? "null"}");

            if (selectedNode == null || selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Select a node to move";
                UnifiedLogger.LogApplication(LogLevel.WARN, "No valid node selected for move up");
                return;
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Calling MoveNodeUp for: {selectedNode.DisplayText}");
            _viewModel.MoveNodeUp(selectedNode);
        }

        public void OnMoveNodeDownClick(object? sender, RoutedEventArgs e)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "🔽 OnMoveNodeDownClick called");
            var selectedNode = GetSelectedTreeNode();
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Selected node: {selectedNode?.DisplayText ?? "null"}");

            if (selectedNode == null || selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Select a node to move";
                UnifiedLogger.LogApplication(LogLevel.WARN, "No valid node selected for move down");
                return;
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Calling MoveNodeDown for: {selectedNode.DisplayText}");
            _viewModel.MoveNodeDown(selectedNode);
        }

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
                // Skip if selection came from TreeView or plugin sync (flags set by respective handlers)
                if (selectedNode != null && !(selectedNode is TreeViewRootNode) &&
                    !_uiState.IsSettingSelectionProgrammatically && !_services.PluginSelectionSync.IsSettingSelectionProgrammatically)
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

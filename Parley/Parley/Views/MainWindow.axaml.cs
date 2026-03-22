using System;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DialogEditor.ViewModels;
using DialogEditor.Models;
using DialogEditor.Utils;
using DialogEditor.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using Parley.Services;
using Parley.Views.Helpers;
using Radoub.Formats.Ssf;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>
    /// MainWindow core: constructor, initialization, keyboard shortcuts, and event bus handlers.
    /// All other responsibilities are in partial files:
    ///   - MainWindow.Lifecycle.cs: Window open/close/loaded events
    ///   - MainWindow.FileHandlers.cs: File menu operations (new/open/save/rename)
    ///   - MainWindow.MenuHandlers.cs: View/Flowchart/Edit/Scrap/Help/Settings menus
    ///   - MainWindow.NodeHandlers.cs: Node CRUD, selection, drag-drop, clipboard
    ///   - MainWindow.Properties.cs: Property panel, auto-save, quest, resource browsers
    ///   - MainWindow.TreeOps.cs: Tree expand/collapse, refresh, expansion state
    ///   - MainWindow.SoundHandlers.cs: Sound playback handlers
    ///   - MainWindow.Theme.cs: Theme application and switching
    /// </summary>
    public partial class MainWindow : Window, IKeyboardShortcutHandler
    {
        private readonly MainViewModel _viewModel;
        private readonly SafeControlFinder _controls;
        private readonly WindowLifecycleManager _windows;

        // Service and controller containers (#526)
        private readonly MainWindowServices _services;
        private readonly MainWindowControllers _controllers;

        // Auto-save timer
        private Timer? _autoSaveTimer;

        // UI state management
        private readonly UiStateManager _uiState = new();

        // Node selection state (shared across partial files)
        private TreeViewSafeNode? _selectedNode;

        public MainWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            _controls = new SafeControlFinder(this);
            _windows = new WindowLifecycleManager();
            _services = new MainWindowServices(serviceProvider);
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
            _services.PropertyPopulator = new PropertyPanelPopulator(this, _services.Settings, _services.Journal);
            // #1791: GameData/ImageService wired in ConnectGameDataServices() after deferred init
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
            // #1791: GameData not yet initialized — wired in ConnectGameDataServices() after deferred init
            _services.ResourceBrowser = new ResourceBrowserManager(
                audioService: _services.Audio,
                creatureService: _services.Creature,
                settings: _services.Settings,
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
                findControl: this.FindControl<Control>,
                settings: _services.Settings);

            // TreeView and dialog services
            _services.DragDrop.DropCompleted += OnDragDropCompleted;
            _services.Dialog = new DialogFactory(this, _services.Settings);

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
                settings: _services.Settings,
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

            // #1791: GameData not yet initialized — wired in ConnectGameDataServices() after deferred init
            _controllers.ScriptBrowser = new ScriptBrowserController(
                window: this,
                controls: _controls,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                autoSaveProperty: AutoSaveProperty,
                settings: _services.Settings,
                externalEditorService: Program.Services.GetRequiredService<ExternalEditorService>());

            _controllers.ParameterBrowser = new ParameterBrowserController(
                controls: _controls,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                parameterUIManager: _services.ParameterUI,
                scriptService: _services.Script);

            _services.ScriptPreview = new ScriptPreviewService(_controls, _services.Script);

            _controllers.Quest = new QuestUIController(
                window: this,
                controls: _controls,
                settings: _services.Settings,
                journalService: _services.Journal,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                isPopulatingProperties: () => _uiState.IsPopulatingProperties,
                setIsPopulatingProperties: value => _uiState.IsPopulatingProperties = value,
                triggerAutoSave: TriggerDebouncedAutoSave);

            _controllers.FileMenu = new FileMenuController(
                window: this,
                controls: _controls,
                settings: _services.Settings,
                getViewModel: () => _viewModel,
                saveCurrentNodeProperties: SaveCurrentNodeProperties,
                clearPropertiesPanel: () => _services.PropertyPopulator.ClearAllFields(),
                populateRecentFilesMenu: () => _controllers.FileMenu?.PopulateRecentFilesMenu(),
                updateEmbeddedFlowchartAfterLoad: () => _controllers.Flowchart.UpdateAfterLoad(),
                clearFlowcharts: () => _controllers.Flowchart.ClearAll(),
                getParameterUIManager: () => _services.ParameterUI,
                showSaveAsDialogAsync: ShowSaveAsDialogAsync,
                scanCreaturesForModule: ScanCreaturesForModuleAsync,
                updateDialogBrowserCurrentFile: filePath => _controllers.DialogBrowser.UpdateCurrentFile(filePath));

            _controllers.EditMenu = new EditMenuController(
                window: this,
                getViewModel: () => _viewModel,
                getSelectedNode: GetSelectedTreeNode);

            _controllers.SpeakerVisual = new SpeakerVisualController(
                window: this,
                settings: _services.Settings,
                isPopulatingProperties: () => _uiState.IsPopulatingProperties);

            _controllers.DialogBrowser = new DialogBrowserController(
                window: this,
                viewModel: _viewModel,
                services: _services,
                updateEmbeddedFlowchartAfterLoad: UpdateEmbeddedFlowchartAfterLoad,
                scanCreaturesForModuleAsync: ScanCreaturesForModuleAsync,
                populateRecentFilesMenu: PopulateRecentFilesMenu);
        }

        /// <summary>
        /// Initializes logging infrastructure.
        /// </summary>
        private void InitializeLogging()
        {
            DebugLogger.Initialize(this);
            UnifiedLogger.SetLogLevel(LogLevel.DEBUG);
            UnifiedLogger.LogApplication(LogLevel.INFO, "Parley MainWindow initialized");

            // Cleanup old log sessions on startup (#1232: use DI-resolved settings)
            var retentionCount = _services.Settings.LogRetentionSessions;
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
            // #1232: Use DI-resolved settings
            _services.Settings.PropertyChanged += OnSettingsPropertyChanged;

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
            UpdateUseRadoubThemeMenuState();
            HideDebugConsoleByDefault();
            SetupKeyboardShortcuts();
            _controllers.TreeView.SetupTreeViewDragDrop();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Wires up GameData and ImageService references to services/controllers
        /// after deferred initialization in OnWindowOpened (#1791).
        /// </summary>
        private void ConnectGameDataServices()
        {
            _services.PropertyPopulator.SetImageService(_services.ImageService);
            _services.PropertyPopulator.SetGameDataService(_services.GameData);
        }

        public void AddDebugMessage(string message) => _services.DebugLogging.AddDebugMessage(message);
        public void ClearDebugOutput() => _viewModel.ClearDebugMessages();

        #region Keyboard Shortcuts

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

        #endregion

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
        void IKeyboardShortcutHandler.OnToggleDialogBrowser() => _controllers.DialogBrowser.OnToggleClick(null, null!);

        // View operations - Issue #339: F5 to open flowchart
        void IKeyboardShortcutHandler.OnOpenFlowchart() => OnFlowchartClick(null, null!);

        // Issue #478: F6 to open conversation simulator
        void IKeyboardShortcutHandler.OnOpenConversationSimulator() => OnConversationSimulatorClick(null, null!);

        // Text editing - Issue #753: Insert token (Ctrl+T)
        void IKeyboardShortcutHandler.OnInsertToken() => OnInsertTokenClick(null, null!);

        // Search - Issue #1842: Find in dialog (Ctrl+F, F3, Shift+F3)
        void IKeyboardShortcutHandler.OnFind() => OnFindClick(null, null!);
        void IKeyboardShortcutHandler.OnFindNext() => DialogSearchBar?.FindNext();
        void IKeyboardShortcutHandler.OnFindPrevious() => DialogSearchBar?.FindPrevious();
        void IKeyboardShortcutHandler.OnSearchModule() => OnSearchModuleClick(null, null!);

        #endregion

        #region Event Bus Handlers

        /// <summary>
        /// Handles settings changes that require tree view refresh (Issue #134)
        /// </summary>
        private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Refresh tree and flowchart when NPC tag coloring or speaker preferences change (#134, #1223)
            if (e.PropertyName == nameof(SettingsService.EnableNpcTagColoring) ||
                e.PropertyName == nameof(SettingsService.NpcSpeakerPreferences))
            {
                if (_viewModel.CurrentDialog != null)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _viewModel.RefreshTreeViewColors();
                        _controllers.Flowchart.UpdateAllPanels();
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Tree + flowchart refreshed after {e.PropertyName} change");
                    });
                }
            }
        }

        /// <summary>
        /// Handles dialog change events from the DialogChangeEventBus.
        /// Updates FlowView panels when dialog structure changes (#436, #451).
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
                _controllers.Flowchart.UpdateAllPanels();
            }

            // Handle collapse/expand events from FlowView to sync TreeView (#451)
            if (e.Context == "FlowView")
            {
                _controllers.Flowchart.HandleFlowViewCollapseEvent(e);
            }
        }

        #endregion
    }
}

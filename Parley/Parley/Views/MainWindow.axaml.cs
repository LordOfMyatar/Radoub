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
using DialogEditor.Parsers;
using DialogEditor.Plugins;
using Parley.Views.Helpers;

namespace DialogEditor.Views
{
    public partial class MainWindow : Window, IKeyboardShortcutHandler
    {
        private readonly MainViewModel _viewModel;
        private readonly AudioService _audioService;
        private readonly CreatureService _creatureService;
        private readonly PluginManager _pluginManager;
        private readonly PluginPanelManager _pluginPanelManager; // Manages plugin panel windows (Epic 3 / #225)
        private readonly PropertyPanelPopulator _propertyPopulator; // Helper for populating properties panel
        private readonly PropertyAutoSaveService _propertyAutoSaveService; // Handles auto-saving of node properties
        private readonly ScriptParameterUIManager _parameterUIManager; // Manages script parameter UI and synchronization
        private readonly NodeCreationHelper _nodeCreationHelper; // Handles smart node creation and tree navigation
        private readonly ResourceBrowserManager _resourceBrowserManager; // Manages resource browser dialogs
        private readonly KeyboardShortcutManager _keyboardShortcutManager; // Manages keyboard shortcuts
        private readonly DebugAndLoggingHandler _debugAndLoggingHandler; // Handles debug and logging operations
        private readonly WindowPersistenceManager _windowPersistenceManager; // Manages window and panel persistence
        private readonly PluginSelectionSyncHelper _pluginSelectionSyncHelper; // Handles plugin ↔ tree selection sync (#234)
        private readonly SafeControlFinder _controls; // Issue #342: Safe control access with null-check elimination
        private readonly WindowLifecycleManager _windows; // Issue #343: Centralized window lifecycle management
        private readonly TreeViewDragDropService _dragDropService; // Issue #450: TreeView drag-drop support
        private readonly FlowchartManager _flowchartManager; // Issue #457: Flowchart layout and sync management
        private readonly TreeViewUIController _treeViewUIController; // Issue #463: TreeView UI interactions
        private readonly ScriptBrowserController _scriptBrowserController; // Issue #464: Script browsing UI
        private readonly QuestUIController _questUIController; // Issue #465: Quest/journal UI interactions
        private readonly FileMenuController _fileMenuController; // Issue #466: File menu operations
        private readonly EditMenuController _editMenuController; // Issue #466: Edit menu operations

        // DEBOUNCED AUTO-SAVE: Timer for file auto-save after inactivity
        private System.Timers.Timer? _autoSaveTimer;

        // Flag to prevent auto-save during programmatic updates
        private bool _isPopulatingProperties = false;

        // DEBOUNCED NODE CREATION: Moved to NodeCreationHelper service (Issue #76)

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // Issue #342: Initialize SafeControlFinder for cleaner control access
            _controls = new SafeControlFinder(this);

            // Issue #343: Initialize WindowLifecycleManager for centralized window tracking
            _windows = new WindowLifecycleManager();

            // Initialize selected tree node to null (no selection on startup)
            _viewModel.SelectedTreeNode = null;
            _audioService = new AudioService();
            _creatureService = new CreatureService();
            _pluginManager = new PluginManager();
            _pluginPanelManager = new PluginPanelManager(this);
            _pluginPanelManager.SetPluginManager(_pluginManager); // For panel reopen restart (#235)
            _propertyPopulator = new PropertyPanelPopulator(this);
            _propertyAutoSaveService = new PropertyAutoSaveService(
                findControl: this.FindControl<Control>,
                refreshTreeDisplay: RefreshTreeDisplayPreserveState,
                loadScriptPreview: (script, isCondition) => _ = LoadScriptPreviewAsync(script, isCondition),
                clearScriptPreview: ClearScriptPreview,
                triggerDebouncedAutoSave: TriggerDebouncedAutoSave);
            _parameterUIManager = new ScriptParameterUIManager(
                findControl: this.FindControl<Control>,
                setStatusMessage: msg => _viewModel.StatusMessage = msg,
                triggerAutoSave: () => { _viewModel.HasUnsavedChanges = true; TriggerDebouncedAutoSave(); },
                isPopulatingProperties: () => _isPopulatingProperties,
                getSelectedNode: () => _selectedNode);
            _nodeCreationHelper = new NodeCreationHelper(
                viewModel: _viewModel,
                findControl: this.FindControl<Control>,
                saveCurrentNodeProperties: SaveCurrentNodeProperties,
                triggerAutoSave: TriggerDebouncedAutoSave);
            _resourceBrowserManager = new ResourceBrowserManager(
                audioService: _audioService,
                creatureService: _creatureService,
                findControl: this.FindControl<Control>,
                setStatusMessage: msg => _viewModel.StatusMessage = msg,
                autoSaveProperty: AutoSaveProperty,
                getSelectedNode: () => _selectedNode,
                getCurrentFilePath: () => _viewModel.CurrentFilePath); // Issue #5: For lazy creature loading
            _keyboardShortcutManager = new KeyboardShortcutManager();
            _keyboardShortcutManager.RegisterShortcuts(this);
            _debugAndLoggingHandler = new DebugAndLoggingHandler(
                viewModel: _viewModel,
                findControl: this.FindControl<Control>,
                getStorageProvider: () => this.StorageProvider,
                setStatusMessage: msg => _viewModel.StatusMessage = msg);
            _windowPersistenceManager = new WindowPersistenceManager(
                window: this,
                findControl: this.FindControl<Control>);
            _pluginSelectionSyncHelper = new PluginSelectionSyncHelper(
                viewModel: _viewModel,
                findControl: this.FindControl<Control>,
                getSelectedNode: () => _selectedNode,
                setSelectedTreeItem: node =>
                {
                    _controls.WithControl<TreeView>("DialogTreeView", tv => tv.SelectedItem = node);
                });

            // Initialize drag-drop service for TreeView (#450)
            _dragDropService = new TreeViewDragDropService();
            _dragDropService.DropCompleted += OnDragDropCompleted;

            // Initialize FlowchartManager for layout and sync management (#457)
            _flowchartManager = new FlowchartManager(
                window: this,
                controls: _controls,
                windows: _windows,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                setSelectedNode: node => _selectedNode = node,
                populatePropertiesPanel: PopulatePropertiesPanel,
                saveCurrentNodeProperties: SaveCurrentNodeProperties,
                getIsSettingSelectionProgrammatically: () => _isSettingSelectionProgrammatically,
                setIsSettingSelectionProgrammatically: value => _isSettingSelectionProgrammatically = value);

            // Initialize TreeViewUIController for TreeView UI interactions (#463)
            _treeViewUIController = new TreeViewUIController(
                window: this,
                controls: _controls,
                dragDropService: _dragDropService,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                setSelectedNode: node => _selectedNode = node,
                populatePropertiesPanel: PopulatePropertiesPanel,
                saveCurrentNodeProperties: SaveCurrentNodeProperties,
                clearAllFields: () => _propertyPopulator.ClearAllFields(),
                getIsSettingSelectionProgrammatically: () => _isSettingSelectionProgrammatically,
                syncSelectionToFlowcharts: node => _flowchartManager.SyncSelectionToFlowcharts(node),
                updatePluginSelectionSync: () => _pluginSelectionSyncHelper.UpdateDialogContextSelectedNode());

            // Initialize ScriptBrowserController for script browsing UI (#464)
            _scriptBrowserController = new ScriptBrowserController(
                window: this,
                controls: _controls,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                autoSaveProperty: AutoSaveProperty,
                isPopulatingProperties: () => _isPopulatingProperties,
                parameterUIManager: _parameterUIManager,
                triggerAutoSave: () => { _viewModel.HasUnsavedChanges = true; TriggerDebouncedAutoSave(); });

            // Initialize QuestUIController for quest/journal UI interactions (#465)
            _questUIController = new QuestUIController(
                window: this,
                controls: _controls,
                getViewModel: () => _viewModel,
                getSelectedNode: () => _selectedNode,
                isPopulatingProperties: () => _isPopulatingProperties,
                setIsPopulatingProperties: value => _isPopulatingProperties = value,
                triggerAutoSave: TriggerDebouncedAutoSave);

            // Initialize FileMenuController for file menu operations (#466)
            _fileMenuController = new FileMenuController(
                window: this,
                controls: _controls,
                getViewModel: () => _viewModel,
                saveCurrentNodeProperties: SaveCurrentNodeProperties,
                clearPropertiesPanel: () => _propertyPopulator.ClearAllFields(),
                populateRecentFilesMenu: () => _fileMenuController?.PopulateRecentFilesMenu(),
                updateEmbeddedFlowchartAfterLoad: () => _flowchartManager.UpdateAfterLoad(),
                getParameterUIManager: () => _parameterUIManager,
                showSaveAsDialogAsync: ShowSaveAsDialogAsync);

            // Initialize EditMenuController for edit menu operations (#466)
            _editMenuController = new EditMenuController(
                window: this,
                getViewModel: () => _viewModel,
                getSelectedNode: GetSelectedTreeNode);

            DebugLogger.Initialize(this);
            UnifiedLogger.SetLogLevel(LogLevel.DEBUG);
            UnifiedLogger.LogApplication(LogLevel.INFO, "Parley MainWindow initialized");

            // Cleanup old log sessions on startup
            var retentionCount = SettingsService.Instance.LogRetentionSessions;
            UnifiedLogger.CleanupOldSessions(retentionCount);

            _windowPersistenceManager.LoadAnimationValues();

            // Apply saved theme preference
            ApplySavedTheme();

            // Subscribe to theme changes to refresh tree view colors
            ThemeManager.Instance.ThemeApplied += OnThemeApplied;

            // Subscribe to NPC tag coloring setting changes (Issue #134)
            SettingsService.Instance.PropertyChanged += OnSettingsPropertyChanged;

            // Subscribe to dialog change events for FlowView synchronization (#436, #451)
            DialogChangeEventBus.Instance.DialogChanged += OnDialogChanged;

            // Phase 0 Fix: Hide debug console by default
            HideDebugConsoleByDefault();

            // Hook up menu events
            this.Opened += async (s, e) =>
            {
                // Restore window position from settings
                await _windowPersistenceManager.RestoreWindowPositionAsync();

                PopulateRecentFilesMenu();

                // Handle command line file loading (Issue #9)
                await _windowPersistenceManager.HandleStartupFileAsync(_viewModel);

                // Start enabled plugins after window opens
                var startedPlugins = await _pluginManager.StartEnabledPluginsAsync();
                if (startedPlugins.Count > 0)
                {
                    // Show in status bar instead of popup (less intrusive)
                    _viewModel.StatusMessage = $"Plugins active: {string.Join(", ", startedPlugins)}";
                    UnifiedLogger.LogPlugin(LogLevel.INFO, $"Started plugins: {string.Join(", ", startedPlugins)}");
                }

                // Restore flowchart state on startup (#377)
                if (SettingsService.Instance.FlowchartVisible)
                {
                    _flowchartManager.RestoreOnStartup();
                }
            };
            this.Closing += OnWindowClosing;
            this.PositionChanged += (s, e) => _windowPersistenceManager.SaveWindowPosition();
            this.PropertyChanged += (s, e) =>
            {
                if (!_windowPersistenceManager.IsRestoringPosition)
                {
                    if (e.Property.Name == nameof(Width) || e.Property.Name == nameof(Height))
                    {
                        _windowPersistenceManager.SaveWindowPosition();
                    }
                }
            };

            // Phase 1 Fix: Set up keyboard shortcuts
            SetupKeyboardShortcuts();

            // Issue #450/#463: Set up TreeView drag-drop handlers via controller
            _treeViewUIController.SetupTreeViewDragDrop();

            // Phase 2a: Watch for node re-selection requests after tree refresh
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Auto-scroll debug console to end when new messages added
            _viewModel.DebugMessages.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    var debugListBox = this.FindControl<ListBox>("DebugListBox");
                    if (debugListBox != null && debugListBox.ItemCount > 0)
                    {
                        debugListBox.ScrollIntoView(debugListBox.ItemCount - 1);
                    }
                }
            };

            // Restore debug settings when window is loaded (controls are available)
            this.Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            // Controls are now available, restore settings
            _windowPersistenceManager.RestoreDebugSettings();
            _windowPersistenceManager.RestorePanelSizes();

            // Initialize menu checkmark states
            _flowchartManager.UpdateLayoutMenuChecks();

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

        /// <summary>
        /// Handler for theme changes - refreshes tree view to update node colors
        /// </summary>
        private void OnThemeApplied(object? sender, EventArgs e)
        {
            // Only refresh if a dialog is loaded
            if (_viewModel.CurrentDialog != null)
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _viewModel.RefreshTreeViewColors();
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Tree view refreshed after theme change");
                });
            }
        }

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
            // Only update flowchart for structure changes (not selection changes)
            if (e.ChangeType == DialogChangeType.DialogRefreshed ||
                e.ChangeType == DialogChangeType.NodeAdded ||
                e.ChangeType == DialogChangeType.NodeDeleted ||
                e.ChangeType == DialogChangeType.NodeMoved)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"OnDialogChanged: {e.ChangeType} - updating flowchart panels");

                // Update all flowchart panels to reflect the change
                _flowchartManager.UpdateAllPanels();
            }

            // Handle collapse/expand events from FlowView to sync TreeView (#451)
            if (e.Context == "FlowView")
            {
                _flowchartManager.HandleFlowViewCollapseEvent(e);
            }
        }

        private void SetupKeyboardShortcuts()
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "SetupKeyboardShortcuts: Keyboard handler registered");

            // Tunneling event for shortcuts that intercept before TreeView (Ctrl+Shift+Up/Down)
            this.AddHandler(KeyDownEvent, (sender, e) =>
            {
                if (_keyboardShortcutManager.HandlePreviewKeyDown(e))
                {
                    e.Handled = true;
                }
            }, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);

            // Standard KeyDown events
            this.KeyDown += (sender, e) =>
            {
                if (_keyboardShortcutManager.HandleKeyDown(e))
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
        void IKeyboardShortcutHandler.OnExpandSubnodes() => _treeViewUIController.OnExpandSubnodesClick(null, null!);
        void IKeyboardShortcutHandler.OnCollapseSubnodes() => _treeViewUIController.OnCollapseSubnodesClick(null, null!);
        void IKeyboardShortcutHandler.OnMoveNodeUp() => OnMoveNodeUpClick(null, null!);
        void IKeyboardShortcutHandler.OnMoveNodeDown() => OnMoveNodeDownClick(null, null!);
        void IKeyboardShortcutHandler.OnGoToParentNode() => _treeViewUIController.OnGoToParentNodeClick(null, null!);

        // View operations - Issue #339: F5 to open flowchart
        void IKeyboardShortcutHandler.OnOpenFlowchart() => OnFlowchartClick(null, null!);

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

                var shouldSave = await ShowConfirmDialog(
                    "Unsaved Changes",
                    $"Do you want to save changes to {fileName}?"
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
                        var saveAs = await ShowSaveErrorDialog(_viewModel.StatusMessage);
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
                            var discardChanges = await ShowConfirmDialog(
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
            _audioService.Dispose();

            // Issue #343: Close all managed windows (Settings, Flowchart)
            _windows.CloseAll();

            // Close browser windows managed by ScriptBrowserController
            _scriptBrowserController.CloseAllBrowserWindows();

            // Close plugin panel windows (Epic 3 / #225)
            _pluginPanelManager.Dispose();

            // Dispose plugin selection sync helper (Epic 40 Phase 3 / #234)
            _pluginSelectionSyncHelper.Dispose();

            // Save window position on close
            _windowPersistenceManager.SaveWindowPosition();
        }

        
        
        
        
        // Phase 1 Step 4: Removed UnsavedChangesDialog - auto-save provides safety

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        
        public void AddDebugMessage(string message) => _debugAndLoggingHandler.AddDebugMessage(message);
        public void ClearDebugOutput() => _viewModel.ClearDebugMessages();

        // File menu handlers - delegated to FileMenuController (#466)
        private void OnNewClick(object? sender, RoutedEventArgs e)
            => _fileMenuController.OnNewClick(sender, e);

        private void OnOpenClick(object? sender, RoutedEventArgs e)
            => _fileMenuController.OnOpenClick(sender, e);

        private void OnSaveClick(object? sender, RoutedEventArgs e)
            => _fileMenuController.OnSaveClick(sender, e);

        private void SaveCurrentNodeProperties()
        {
            if (_selectedNode == null || _selectedNode is TreeViewRootNode)
            {
                return;
            }

            var dialogNode = _selectedNode.OriginalNode;

            // Issue #342: Use SafeControlFinder for cleaner null-safe control access
            // Update Speaker (only if editable)
            _controls.WithControl<TextBox>("SpeakerTextBox", tb =>
            {
                if (!tb.IsReadOnly)
                    dialogNode.Speaker = tb.Text ?? "";
            });

            // Update Text
            _controls.WithControl<TextBox>("TextTextBox", tb =>
            {
                if (dialogNode.Text != null)
                    dialogNode.Text.Strings[0] = tb.Text ?? "";
            });

            // Update Comment - Issue #12: Save to LinkComment for link nodes
            _controls.WithControl<TextBox>("CommentTextBox", tb =>
            {
                if (_selectedNode.IsChild && _selectedNode.SourcePointer != null)
                    _selectedNode.SourcePointer.LinkComment = tb.Text ?? "";
                else
                    dialogNode.Comment = tb.Text ?? "";
            });

            // Update Sound
            _controls.WithControl<TextBox>("SoundTextBox", tb => dialogNode.Sound = tb.Text ?? "");

            // Update Script
            _controls.WithControl<TextBox>("ScriptActionTextBox", tb => dialogNode.ScriptAction = tb.Text ?? "");

            // Update Conditional Script (on DialogPtr)
            if (_selectedNode.SourcePointer != null)
            {
                _controls.WithControl<TextBox>("ScriptAppearsTextBox", tb =>
                    _selectedNode.SourcePointer.ScriptAppears = tb.Text ?? "");
            }

            // Update Quest
            _controls.WithControl<TextBox>("QuestTextBox", tb => dialogNode.Quest = tb.Text ?? "");

            // Update Animation
            _controls.WithControl<ComboBox>("AnimationComboBox", cb =>
            {
                if (cb.SelectedItem is DialogAnimation selectedAnimation)
                    dialogNode.Animation = selectedAnimation;
            });

            // Update AnimationLoop
            _controls.WithControl<CheckBox>("AnimationLoopCheckBox", cb =>
            {
                if (cb.IsChecked.HasValue)
                    dialogNode.AnimationLoop = cb.IsChecked.Value;
            });

            // Update Quest Entry
            _controls.WithControl<TextBox>("QuestEntryTextBox", tb =>
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                    dialogNode.QuestEntry = uint.MaxValue;
                else if (uint.TryParse(tb.Text, out uint entryId))
                    dialogNode.QuestEntry = entryId;
            });

            // Update Delay
            _controls.WithControl<TextBox>("DelayTextBox", tb =>
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                    dialogNode.Delay = uint.MaxValue;
                else if (uint.TryParse(tb.Text, out uint delayMs))
                    dialogNode.Delay = delayMs;
            });

            // CRITICAL FIX: Save script parameters from UI before saving file
            // Update action parameters (on DialogNode)
            _parameterUIManager.UpdateActionParamsFromUI(dialogNode);

            // Update condition parameters (on DialogPtr if available)
            if (_selectedNode.SourcePointer != null)
            {
                _parameterUIManager.UpdateConditionParamsFromUI(_selectedNode.SourcePointer);
            }
        }

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
            _fileMenuController.OnCloseClick(sender, e);
            _selectedNode = null; // Clear local selection reference
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
            => _fileMenuController.OnExitClick(sender, e);

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
            => _fileMenuController.PopulateRecentFilesMenu();

        // Edit menu handlers - delegated to EditMenuController (#466)
        private void OnCopyNodeTextClick(object? sender, RoutedEventArgs e)
            => _editMenuController.OnCopyNodeTextClick(sender, e);

        private void OnCopyNodePropertiesClick(object? sender, RoutedEventArgs e)
            => _editMenuController.OnCopyNodePropertiesClick(sender, e);

        private void OnCopyTreeStructureClick(object? sender, RoutedEventArgs e)
            => _editMenuController.OnCopyTreeStructureClick(sender, e);

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
            _debugAndLoggingHandler.OpenLogFolder();
        }

        private async void OnExportLogsClick(object? sender, RoutedEventArgs e)
        {
            await _debugAndLoggingHandler.ExportLogsAsync(this);
        }

        // Scrap tab handlers
        private void OnRestoreScrapClick(object? sender, RoutedEventArgs e)
        {
            var treeView = this.FindControl<TreeView>("DialogTreeView");
            var selectedNode = treeView?.SelectedItem as TreeViewSafeNode;
            _debugAndLoggingHandler.RestoreFromScrap(selectedNode);
        }

        private async void OnClearScrapClick(object? sender, RoutedEventArgs e)
        {
            await _debugAndLoggingHandler.ClearScrapAsync(this);
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

            var closedPanels = _pluginPanelManager.GetClosedPanels().ToList();
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
                _pluginPanelManager.ReopenPanel(panel);
            }

            _viewModel.StatusMessage = $"Reopened {closedPanels.Count} plugin panel(s)";
        }

        // Flowchart menu handlers - delegate to FlowchartManager (#457)
        private void OnFlowchartClick(object? sender, RoutedEventArgs e) => _flowchartManager.OpenFloatingFlowchart();

        private void OnFlowchartLayoutClick(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string layoutValue)
            {
                _flowchartManager.ApplyLayout(layoutValue);
            }
        }

        private async void OnExportFlowchartPngClick(object? sender, RoutedEventArgs e) => await _flowchartManager.ExportToPngAsync();

        private async void OnExportFlowchartSvgClick(object? sender, RoutedEventArgs e) => await _flowchartManager.ExportToSvgAsync();

        private void UpdateEmbeddedFlowchartAfterLoad() => _flowchartManager.UpdateAfterLoad();

        // Theme methods
        private void ApplySavedTheme()
        {
            try
            {
                if (Application.Current != null)
                {
                    bool isDark = SettingsService.Instance.IsDarkTheme;
                    Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
                    UpdateThemeMenuChecks(isDark);
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Applied saved theme: {(isDark ? "Dark" : "Light")}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error applying saved theme: {ex.Message}");
            }
        }

        // Theme handlers
        private void OnLightThemeClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = ThemeVariant.Light;
                    SettingsService.Instance.IsDarkTheme = false;
                    _viewModel.StatusMessage = "Light theme applied";
                    UpdateThemeMenuChecks(false);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error applying light theme: {ex.Message}");
            }
        }

        private void OnDarkThemeClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                    SettingsService.Instance.IsDarkTheme = true;
                    _viewModel.StatusMessage = "Dark theme applied";
                    UpdateThemeMenuChecks(true);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error applying dark theme: {ex.Message}");
            }
        }

        private void UpdateThemeMenuChecks(bool isDark)
        {
            var lightMenuItem = this.FindControl<MenuItem>("LightThemeMenuItem");
            var darkMenuItem = this.FindControl<MenuItem>("DarkThemeMenuItem");

            if (lightMenuItem != null && darkMenuItem != null)
            {
                // Update checkbox visibility in menu icons
                // This is simplified - proper implementation would update the CheckBox IsChecked in the Icon
                _viewModel.StatusMessage = isDark ? "Dark theme" : "Light theme";
            }
        }

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
                    () => new SettingsWindow(pluginManager: _pluginManager),
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
                    () => new SettingsWindow(initialTab: 0, pluginManager: _pluginManager),
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
                    () => new SettingsWindow(initialTab: 2, pluginManager: _pluginManager),
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

        // Tree view handlers
        private void OnExpandAllClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var treeView = this.FindControl<TreeView>("DialogTreeView");
                if (treeView != null)
                {
                    ExpandAllTreeViewItems(treeView);
                    _viewModel.StatusMessage = "Expanded all tree nodes";
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Error expanding tree: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to expand all: {ex.Message}");
            }
        }

        private void OnCollapseAllClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var treeView = this.FindControl<TreeView>("DialogTreeView");
                if (treeView != null)
                {
                    CollapseAllTreeViewItems(treeView);
                    _viewModel.StatusMessage = "Collapsed all tree nodes";
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Error collapsing tree: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to collapse all: {ex.Message}");
            }
        }

        private void ExpandAllTreeViewItems(TreeView treeView)
        {
            // Avalonia approach: Work directly with ViewModel data
            if (_viewModel.DialogNodes == null || _viewModel.DialogNodes.Count == 0) return;

            foreach (var node in _viewModel.DialogNodes)
            {
                ExpandTreeNode(node);
            }
        }

        private void ExpandTreeNode(TreeViewSafeNode node)
        {
            node.IsExpanded = true;

            // Recursively expand children
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    ExpandTreeNode(child);
                }
            }
        }

        private void CollapseAllTreeViewItems(TreeView treeView)
        {
            // Avalonia approach: Work directly with ViewModel data
            if (_viewModel.DialogNodes == null || _viewModel.DialogNodes.Count == 0) return;

            foreach (var node in _viewModel.DialogNodes)
            {
                CollapseTreeNode(node);
            }
        }

        private void CollapseTreeNode(TreeViewSafeNode node)
        {
            node.IsExpanded = false;

            // Recursively collapse children
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    CollapseTreeNode(child);
                }
            }
        }

        private TreeViewSafeNode? _selectedNode;
        private bool _isSettingSelectionProgrammatically = false;  // For ViewModel SelectedTreeNode binding feedback

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
            => _treeViewUIController.OnDialogTreeViewSelectionChanged(sender, e);

        // Issue #463: Delegated to TreeViewUIController
        private void OnTreeViewItemDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
            => _treeViewUIController.OnTreeViewItemDoubleTapped(sender, e);

        private void PopulatePropertiesPanel(TreeViewSafeNode node)
        {
            // CRITICAL FIX: Prevent auto-save during programmatic updates
            _isPopulatingProperties = true;

            // CRITICAL FIX: Clear all fields FIRST to prevent stale data
            _propertyPopulator.ClearAllFields();

            // Populate Conversation Settings (dialog-level properties) - always populate these
            _propertyPopulator.PopulateConversationSettings(_viewModel.CurrentDialog);

            // Issue #19: If ROOT node selected, keep only conversation settings enabled
            // All node-specific properties should remain disabled
            if (node is TreeViewRootNode)
            {
                _isPopulatingProperties = false;
                return; // Node fields remain disabled from ClearAllFields
            }

            var dialogNode = node.OriginalNode;

            // Debug: Log node type for Issue #12 investigation
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"🔍 PopulatePropertiesPanel: NodeType={node.GetType().Name}, IsChild={node.IsChild}, " +
                $"HasSourcePointer={node.SourcePointer != null}, DisplayText='{node.DisplayText}'");

            // Populate all node properties using helper
            _propertyPopulator.PopulateNodeType(dialogNode);
            _propertyPopulator.PopulateSpeaker(dialogNode);
            _propertyPopulator.PopulateBasicProperties(dialogNode, node);
            _propertyPopulator.PopulateAnimation(dialogNode);
            _propertyPopulator.PopulateIsChildIndicator(node);

            // Populate scripts with callbacks for async operations
            _propertyPopulator.PopulateScripts(dialogNode, node,
                (script, isCondition) => _ = LoadParameterDeclarationsAsync(script, isCondition),
                (script, isCondition) => _ = LoadScriptPreviewAsync(script, isCondition),
                (isCondition) => ClearScriptPreview(isCondition));

            // Populate quest fields
            _propertyPopulator.PopulateQuest(dialogNode);

            // Populate script parameters
            _propertyPopulator.PopulateParameterGrids(dialogNode, node.SourcePointer, AddParameterRow);

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Populated properties for node: {dialogNode.DisplayText}");

            // Re-enable auto-save after population complete
            _isPopulatingProperties = false;
        }


        private void OnPropertyChanged(object? sender, RoutedEventArgs e)
        {
            if (_selectedNode == null) return;

            var dialogNode = _selectedNode.OriginalNode;
            var textBox = sender as TextBox;

            if (textBox == null) return;

            // Determine which property changed based on control name
            switch (textBox.Name)
            {
                case "SpeakerTextBox":
                    dialogNode.Speaker = textBox.Text ?? "";
                    _viewModel.HasUnsavedChanges = true;
                    // Refresh tree to update node color if speaker changed
                    _viewModel.StatusMessage = "Speaker updated";
                    break;

                case "TextTextBox":
                    if (dialogNode.Text != null)
                    {
                        // Update the default language string (0)
                        dialogNode.Text.Strings[0] = textBox.Text ?? "";
                        _viewModel.HasUnsavedChanges = true;
                        // Refresh tree to show new text
                        RefreshTreeDisplay();
                        _viewModel.StatusMessage = "Text updated";
                    }
                    break;

                case "SoundTextBox":
                    dialogNode.Sound = textBox.Text ?? "";
                    _viewModel.HasUnsavedChanges = true;
                    _viewModel.StatusMessage = "Sound updated";
                    break;

                case "ScriptTextBox":
                    dialogNode.ScriptAction = textBox.Text ?? "";
                    _viewModel.HasUnsavedChanges = true;
                    _viewModel.StatusMessage = "Script updated";
                    break;

                case "CommentTextBox":
                    // Issue #12: Save to LinkComment for link nodes
                    if (_selectedNode.IsChild && _selectedNode.SourcePointer != null)
                    {
                        _selectedNode.SourcePointer.LinkComment = textBox.Text ?? "";
                    }
                    else
                    {
                        dialogNode.Comment = textBox.Text ?? "";
                    }
                    _viewModel.HasUnsavedChanges = true;
                    _viewModel.StatusMessage = "Comment updated";
                    break;

                case "QuestTextBox":
                    dialogNode.Quest = textBox.Text ?? "";
                    _viewModel.HasUnsavedChanges = true;
                    _viewModel.StatusMessage = "Quest updated";
                    break;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Property '{textBox.Name}' changed for node: {dialogNode.DisplayText}");
        }

        // FIELD-LEVEL AUTO-SAVE: Event handlers for immediate save
        private void OnAnimationSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"OnAnimationSelectionChanged: _selectedNode={_selectedNode != null}, _isPopulatingProperties={_isPopulatingProperties}");

            if (_selectedNode != null && !_isPopulatingProperties)
            {
                var comboBox = sender as ComboBox;
                if (comboBox != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"OnAnimationSelectionChanged: ComboBox SelectedItem type={comboBox.SelectedItem?.GetType().Name ?? "null"}, value={comboBox.SelectedItem}");
                }

                // Delay auto-save to ensure SelectedItem has fully updated
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "OnAnimationSelectionChanged: Dispatcher.Post executing AutoSaveProperty");
                    AutoSaveProperty("AnimationComboBox");
                }, global::Avalonia.Threading.DispatcherPriority.Normal);
            }
        }


        // INPUT VALIDATION: Only allow integers in Delay field
        private void OnDelayTextChanged(object? sender, global::Avalonia.Controls.TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            // Allow empty string (will be treated as 0)
            if (string.IsNullOrWhiteSpace(textBox.Text)) return;

            // Filter out non-numeric characters
            var filteredText = new string(textBox.Text.Where(char.IsDigit).ToArray());

            if (textBox.Text != filteredText)
            {
                var caretIndex = textBox.CaretIndex;
                textBox.Text = filteredText;
                // Restore caret position (or move to end if text got shorter)
                textBox.CaretIndex = Math.Min(caretIndex, filteredText.Length);
            }
        }

        // Issue #74: Track if we've already saved undo state for current edit session
        private string? _currentEditFieldName = null;
        // Issue #253: Track original value to avoid blank undo entries
        private string? _originalFieldValue = null;
        private bool _undoStateSavedForCurrentEdit = false;

        // Issue #74/#253: Track field value on focus, only save undo if value changes
        private void OnFieldGotFocus(object? sender, GotFocusEventArgs e)
        {
            if (_selectedNode == null || _isPopulatingProperties) return;
            if (_viewModel.CurrentDialog == null) return;

            var control = sender as Control;
            if (control?.Name == null) return;

            // Track original value when focus enters a new field
            if (_currentEditFieldName != control.Name)
            {
                _currentEditFieldName = control.Name;
                _originalFieldValue = GetFieldValue(control);
                _undoStateSavedForCurrentEdit = false;
            }
        }

        // Issue #253: Get the current value of a field control
        private static string? GetFieldValue(Control control)
        {
            return control switch
            {
                TextBox tb => tb.Text,
                _ => null
            };
        }

        // Issue #253: Save undo state only if the field value has actually changed
        private void SaveUndoIfValueChanged(Control control)
        {
            if (_undoStateSavedForCurrentEdit) return;
            if (_viewModel.CurrentDialog == null) return;

            var currentValue = GetFieldValue(control);
            if (currentValue != _originalFieldValue)
            {
                _viewModel.SaveUndoState($"Edit {GetFieldDisplayName(control.Name ?? "")}");
                _undoStateSavedForCurrentEdit = true;
            }
        }

        // Helper to get user-friendly field name for undo description
        private static string GetFieldDisplayName(string fieldName) => fieldName switch
        {
            "SpeakerTextBox" => "Speaker",
            "TextTextBox" => "Text",
            "CommentTextBox" => "Comment",
            "SoundTextBox" => "Sound",
            "DelayTextBox" => "Delay",
            "ScriptAppearsTextBox" => "Conditional Script",
            "ScriptActionTextBox" => "Action Script",
            "ScriptEndTextBox" => "End Script",
            "ScriptAbortTextBox" => "Abort Script",
            _ => fieldName.Replace("TextBox", "")
        };

        // FIELD-LEVEL AUTO-SAVE: Save property when field loses focus
        private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
        {
            if (_selectedNode == null || _isPopulatingProperties) return;

            var control = sender as Control;
            if (control == null) return;

            // Issue #253: Save undo state only if value actually changed
            SaveUndoIfValueChanged(control);

            // Clear the edit session tracker (Issue #74)
            _currentEditFieldName = null;
            _originalFieldValue = null;
            _undoStateSavedForCurrentEdit = false;

            // Auto-save the specific property that changed
            AutoSaveProperty(control.Name ?? "");
        }

        private void AutoSaveProperty(string propertyName)
        {
            var result = _propertyAutoSaveService.AutoSaveProperty(_selectedNode, propertyName);

            if (result.Success)
            {
                _viewModel.HasUnsavedChanges = true;
                _viewModel.StatusMessage = result.Message;
                _viewModel.AddDebugMessage(result.Message);
            }
        }

        // DEBOUNCED FILE AUTO-SAVE: Trigger file save after inactivity
        private void TriggerDebouncedAutoSave()
        {
            // Phase 1 Step 6: Check if auto-save is enabled
            if (!SettingsService.Instance.AutoSaveEnabled)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Auto-save is disabled - skipping");
                return;
            }

            // Stop and dispose existing timer
            _autoSaveTimer?.Stop();
            _autoSaveTimer?.Dispose();

            // Create new timer that fires after configured delay (Issue #62)
            var delayMs = SettingsService.Instance.EffectiveAutoSaveIntervalMs;
            _autoSaveTimer = new System.Timers.Timer(delayMs);
            _autoSaveTimer.AutoReset = false; // Only fire once
            _autoSaveTimer.Elapsed += async (s, e) => await AutoSaveToFileAsync();
            _autoSaveTimer.Start();

            var intervalDesc = SettingsService.Instance.AutoSaveIntervalMinutes > 0
                ? $"{SettingsService.Instance.AutoSaveIntervalMinutes} minute(s)"
                : $"{delayMs}ms (fast debounce)";
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Debounced auto-save scheduled in {intervalDesc}");
        }

        private async Task AutoSaveToFileAsync()
        {
            // Must run on UI thread
            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (!_viewModel.HasUnsavedChanges || string.IsNullOrEmpty(_viewModel.CurrentFileName))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Auto-save skipped: no changes or no file loaded");
                    return;
                }

                try
                {
                    // Phase 1 Step 4: Enhanced save status indicators
                    _viewModel.StatusMessage = "💾 Auto-saving...";
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Auto-save starting...");

                    // Issue #8: Check save result before showing success message
                    var success = await _viewModel.SaveDialogAsync(_viewModel.CurrentFileName);

                    if (success)
                    {
                        var timestamp = DateTime.Now.ToString("h:mm tt");
                        var fileName = System.IO.Path.GetFileName(_viewModel.CurrentFileName);
                        _viewModel.StatusMessage = $"✓ Auto-saved '{fileName}' at {timestamp}";

                        // Verify HasUnsavedChanges was cleared (Issue #18)
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"Auto-save completed. HasUnsavedChanges = {_viewModel.HasUnsavedChanges}, WindowTitle = '{_viewModel.WindowTitle}'");
                    }
                    else
                    {
                        // Issue #8: Save failed - show visible warning
                        // StatusMessage already set by SaveDialogAsync with specific error
                        // Prepend warning emoji so user notices
                        _viewModel.StatusMessage = $"⚠ {_viewModel.StatusMessage}";
                        UnifiedLogger.LogApplication(LogLevel.WARN, "Auto-save failed - check status message for details");
                    }
                }
                catch (Exception ex)
                {
                    _viewModel.StatusMessage = "⚠ Auto-save failed - click File → Save to retry";
                    UnifiedLogger.LogApplication(LogLevel.ERROR, $"Debounced auto-save failed: {ex.Message}");
                }
            });
        }

        // MANUAL SAVE: Keep for compatibility, but properties already saved by auto-save
        private async void OnSaveChangesClick(object? sender, RoutedEventArgs e)
        {
            if (_selectedNode == null)
            {
                _viewModel.StatusMessage = "No node selected";
                return;
            }

            var dialogNode = _selectedNode.OriginalNode;

            // CRITICAL FIX: Save ALL editable properties, not just Speaker and Text

            // Update Speaker
            var speakerTextBox = this.FindControl<TextBox>("SpeakerTextBox");
            if (speakerTextBox != null && !speakerTextBox.IsReadOnly)
            {
                dialogNode.Speaker = speakerTextBox.Text ?? "";
            }

            // Update Text
            var textTextBox = this.FindControl<TextBox>("TextTextBox");
            if (textTextBox != null && dialogNode.Text != null)
            {
                dialogNode.Text.Strings[0] = textTextBox.Text ?? "";
            }

            // Update Comment - Issue #12: Save to LinkComment for link nodes
            var commentTextBox = this.FindControl<TextBox>("CommentTextBox");
            if (commentTextBox != null)
            {
                if (_selectedNode.IsChild && _selectedNode.SourcePointer != null)
                {
                    _selectedNode.SourcePointer.LinkComment = commentTextBox.Text ?? "";
                }
                else
                {
                    dialogNode.Comment = commentTextBox.Text ?? "";
                }
            }

            // Update Sound
            var soundTextBox = this.FindControl<TextBox>("SoundTextBox");
            if (soundTextBox != null)
            {
                dialogNode.Sound = soundTextBox.Text ?? "";
            }

            // Update Script Action
            var scriptTextBox = this.FindControl<TextBox>("ScriptActionTextBox");
            if (scriptTextBox != null)
            {
                dialogNode.ScriptAction = scriptTextBox.Text ?? "";
            }

            // Update Quest
            var questTextBox = this.FindControl<TextBox>("QuestTextBox");
            if (questTextBox != null)
            {
                dialogNode.Quest = questTextBox.Text ?? "";
            }

            // Update Animation
            var animationComboBox = this.FindControl<ComboBox>("AnimationComboBox");
            if (animationComboBox != null && animationComboBox.SelectedItem is DialogAnimation selectedAnimation)
            {
                dialogNode.Animation = selectedAnimation;
            }

            // Update Animation Loop
            var animationLoopCheckBox = this.FindControl<CheckBox>("AnimationLoopCheckBox");
            if (animationLoopCheckBox != null && animationLoopCheckBox.IsChecked.HasValue)
            {
                dialogNode.AnimationLoop = animationLoopCheckBox.IsChecked.Value;
            }

            _viewModel.HasUnsavedChanges = true;

            // Refresh tree WITHOUT collapsing
            RefreshTreeDisplayPreserveState();

            // CRITICAL: Save to file immediately
            if (!string.IsNullOrEmpty(_viewModel.CurrentFileName))
            {
                await _viewModel.SaveDialogAsync(_viewModel.CurrentFileName);
                _viewModel.StatusMessage = "All changes saved to file";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Node properties saved: {dialogNode.DisplayText}");
            }
            else
            {
                _viewModel.StatusMessage = "Changes saved to memory (use File → Save to persist)";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Node updated: {dialogNode.DisplayText}");
            }
        }

        private void RefreshTreeDisplay()
        {
            // OLD: This method collapses tree - kept for compatibility
            RefreshTreeDisplayPreserveState();
        }

        private void RefreshTreeDisplayPreserveState()
        {
            // Phase 0 Fix: Save expansion state AND selection before refresh
            var expandedNodes = new HashSet<TreeViewSafeNode>();
            SaveExpansionState(_viewModel.DialogNodes, expandedNodes);

            var selectedNodeText = _selectedNode?.OriginalNode?.DisplayText;

            // Force refresh by re-populating
            _viewModel.PopulateDialogNodes();

            // Restore expansion state and selection after UI updates
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RestoreExpansionState(_viewModel.DialogNodes, expandedNodes);

                // Restore selection if we had one
                if (!string.IsNullOrEmpty(selectedNodeText))
                {
                    RestoreSelection(_viewModel.DialogNodes, selectedNodeText);
                }
            }, global::Avalonia.Threading.DispatcherPriority.Loaded);
        }

        private void SaveExpansionState(System.Collections.ObjectModel.ObservableCollection<TreeViewSafeNode> nodes, HashSet<TreeViewSafeNode> expandedNodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsExpanded)
                {
                    expandedNodes.Add(node);
                }
                if (node.Children != null)
                {
                    SaveExpansionState(node.Children, expandedNodes);
                }
            }
        }

        private void RestoreExpansionState(System.Collections.ObjectModel.ObservableCollection<TreeViewSafeNode> nodes, HashSet<TreeViewSafeNode> expandedNodes)
        {
            foreach (var node in nodes)
            {
                // Match by underlying node reference
                if (expandedNodes.Any(n => n.OriginalNode == node.OriginalNode))
                {
                    node.IsExpanded = true;
                }
                if (node.Children != null)
                {
                    RestoreExpansionState(node.Children, expandedNodes);
                }
            }
        }

        private void RestoreSelection(System.Collections.ObjectModel.ObservableCollection<TreeViewSafeNode> nodes, string displayText)
        {
            foreach (var node in nodes)
            {
                if (node.OriginalNode?.DisplayText == displayText)
                {
                    var treeView = this.FindControl<TreeView>("DialogTreeView");
                    if (treeView != null)
                    {
                        treeView.SelectedItem = node;
                        _selectedNode = node;
                    }
                    return;
                }
                if (node.Children != null)
                {
                    RestoreSelection(node.Children, displayText);
                }
            }
        }

        // Properties panel handlers
        private void OnAddConditionsParamClick(object? sender, RoutedEventArgs e)
        {
            _parameterUIManager.OnAddConditionsParamClick();
        }

        private void OnAddActionsParamClick(object? sender, RoutedEventArgs e)
        {
            _parameterUIManager.OnAddActionsParamClick();
        }

        private async void OnSuggestConditionsParamClick(object? sender, RoutedEventArgs e)
            => await _scriptBrowserController.OnSuggestConditionsParamClickAsync();

        private async void OnSuggestActionsParamClick(object? sender, RoutedEventArgs e)
            => await _scriptBrowserController.OnSuggestActionsParamClickAsync();

        #region Script Browser Delegation Methods
        // These methods delegate to ScriptBrowserController but are kept as instance methods
        // because they're passed as callbacks to PropertyAutoSaveService and PropertyPanelPopulator
        // during construction before the controller is initialized.

        private Task LoadParameterDeclarationsAsync(string scriptName, bool isCondition)
            => _scriptBrowserController.LoadParameterDeclarationsAsync(scriptName, isCondition);

        private Task LoadScriptPreviewAsync(string scriptName, bool isCondition)
            => _scriptBrowserController.LoadScriptPreviewAsync(scriptName, isCondition);

        private void ClearScriptPreview(bool isCondition)
            => _scriptBrowserController.ClearScriptPreview(isCondition);

        private void AddParameterRow(StackPanel parent, string key, string value, bool isCondition)
            => _parameterUIManager.AddParameterRow(parent, key, value, isCondition);

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
            await _resourceBrowserManager.BrowseCreatureAsync(this);
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
                if (_isPopulatingProperties) return;

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
                if (_isPopulatingProperties) return;

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
            => _fileMenuController.UpdateModuleInfo(dialogFilePath);

        private void ClearModuleInfo()
            => _fileMenuController.ClearModuleInfo();


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

                _audioService.Play(soundPath);
                _viewModel.StatusMessage = $"Playing: {soundFileName}";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Playing sound: {soundPath}");
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"❌ Error playing sound: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to play sound: {ex.Message}");
            }
        }

        private void OnConversationSettingChanged(object? sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentDialog == null) return;

            var preventZoomCheckBox = this.FindControl<CheckBox>("PreventZoomCheckBox");
            var scriptEndTextBox = this.FindControl<TextBox>("ScriptEndTextBox");
            var scriptAbortTextBox = this.FindControl<TextBox>("ScriptAbortTextBox");

            if (preventZoomCheckBox != null)
            {
                _viewModel.CurrentDialog.PreventZoom = preventZoomCheckBox.IsChecked ?? false;
            }

            if (scriptEndTextBox != null)
            {
                _viewModel.CurrentDialog.ScriptEnd = scriptEndTextBox.Text?.Trim() ?? string.Empty;
            }

            if (scriptAbortTextBox != null)
            {
                _viewModel.CurrentDialog.ScriptAbort = scriptAbortTextBox.Text?.Trim() ?? string.Empty;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Conversation settings updated: PreventZoom={_viewModel.CurrentDialog.PreventZoom}, " +
                $"ScriptEnd='{_viewModel.CurrentDialog.ScriptEnd}', ScriptAbort='{_viewModel.CurrentDialog.ScriptAbort}'");
        }

        private void OnBrowseConversationScriptClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            _scriptBrowserController.OnBrowseConversationScriptClick(button.Tag?.ToString());
        }

        // Quest UI event handlers - delegated to QuestUIController (#465)
        private void OnQuestTagTextChanged(object? sender, TextChangedEventArgs e) =>
            _questUIController.OnQuestTagTextChanged(sender, e);

        private void OnQuestTagLostFocus(object? sender, RoutedEventArgs e) =>
            _questUIController.OnQuestTagLostFocus(sender, e);

        private void OnQuestEntryTextChanged(object? sender, TextChangedEventArgs e) =>
            _questUIController.OnQuestEntryTextChanged(sender, e);

        private void OnQuestEntryLostFocus(object? sender, RoutedEventArgs e) =>
            _questUIController.OnQuestEntryLostFocus(sender, e);

        private void OnBrowseQuestClick(object? sender, RoutedEventArgs e) =>
            _questUIController.OnBrowseQuestClick(sender, e);

        private void OnBrowseQuestEntryClick(object? sender, RoutedEventArgs e) =>
            _questUIController.OnBrowseQuestEntryClick(sender, e);

        private void OnClearQuestTagClick(object? sender, RoutedEventArgs e) =>
            _questUIController.OnClearQuestTagClick(sender, e);

        private void OnClearQuestEntryClick(object? sender, RoutedEventArgs e) =>
            _questUIController.OnClearQuestEntryClick(sender, e);

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
            => _scriptBrowserController.OnBrowseConditionalScriptClick();

        private void OnBrowseActionScriptClick(object? sender, RoutedEventArgs e)
            => _scriptBrowserController.OnBrowseActionScriptClick();

        private void OnEditConditionalScriptClick(object? sender, RoutedEventArgs e)
            => _scriptBrowserController.OnEditConditionalScriptClick();

        private void OnEditActionScriptClick(object? sender, RoutedEventArgs e)
            => _scriptBrowserController.OnEditActionScriptClick();

        // Node creation handlers - Phase 1 Step 3/4
        /// <summary>
        /// Smart Add Node - Context-aware node creation with auto-focus (Phase 2)
        /// </summary>
        private async void OnAddSmartNodeClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            await _nodeCreationHelper.CreateSmartNodeAsync(selectedNode);
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

            var parentNode = _nodeCreationHelper.FindParentNode(treeView, selectedNode);

            // Add node as child of parent (sibling of selected)
            // This creates a new node at the same level as the selected node
            await _nodeCreationHelper.CreateSmartNodeAsync(parentNode);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Added sibling node to: {selectedNode.DisplayText}");
        }


        /// <summary>
        /// Expands all ancestor nodes to make target node visible (Issue #7)
        /// Handles "collapse all" scenario by expanding entire path from root to node
        /// </summary>
        private void ExpandToNode(TreeView treeView, TreeViewSafeNode targetNode)
        {
            // Collect all ancestors from target to root
            var ancestors = new List<TreeViewSafeNode>();
            var currentNode = targetNode;

            while (currentNode != null)
            {
                var parent = FindParentNode(treeView, currentNode);
                if (parent != null)
                {
                    ancestors.Add(parent);
                    currentNode = parent;
                }
                else
                {
                    break;
                }
            }

            // Expand from root down to target (reverse order)
            ancestors.Reverse();
            foreach (var ancestor in ancestors)
            {
                ancestor.IsExpanded = true;
            }

            if (ancestors.Count > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"OnAddSmartNodeClick: Expanded {ancestors.Count} ancestor nodes to show new child");
            }
        }

        /// <summary>
        /// Finds the parent node of a given child node in the tree (Issue #7)
        /// </summary>
        private TreeViewSafeNode? FindParentNode(TreeView treeView, TreeViewSafeNode targetNode)
        {
            if (treeView.ItemsSource == null) return null;

            foreach (var item in treeView.ItemsSource)
            {
                if (item is TreeViewSafeNode node)
                {
                    var parent = FindParentNodeRecursive(node, targetNode);
                    if (parent != null) return parent;
                }
            }
            return null;
        }

        private TreeViewSafeNode? FindParentNodeRecursive(TreeViewSafeNode currentNode, TreeViewSafeNode targetNode)
        {
            if (currentNode.Children == null) return null;

            // Check if targetNode is a direct child
            if (currentNode.Children.Contains(targetNode))
            {
                return currentNode;
            }

            // Recurse through children
            foreach (var child in currentNode.Children)
            {
                var found = FindParentNodeRecursive(child, targetNode);
                if (found != null) return found;
            }

            return null;
        }

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
                confirmed = await ShowConfirmDialog(
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
                    await _questUIController.LoadJournalForCurrentModuleAsync();
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
                    !_isSettingSelectionProgrammatically && !_pluginSelectionSyncHelper.IsSettingSelectionProgrammatically)
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
                            _nodeCreationHelper.ExpandToNode(treeView, selectedNode);
                            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                $"View: Expanded ancestors for '{selectedNode.DisplayText}'");

                            // Set flag to prevent feedback loop when setting SelectedItem
                            _isSettingSelectionProgrammatically = true;
                            try
                            {
                                // Force set TreeView selection (binding alone doesn't work for lazy-loaded children)
                                treeView.SelectedItem = selectedNode;
                                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                    $"View: Set TreeView.SelectedItem to '{selectedNode.DisplayText}'");
                            }
                            finally
                            {
                                _isSettingSelectionProgrammatically = false;
                            }
                        }
                    }, global::Avalonia.Threading.DispatcherPriority.Background);
                }
            }
        }

        private async Task<bool> ShowConfirmDialog(string title, string message, bool showDontAskAgain = false)
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
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 560, // MaxWidth - margins
                Margin = new Thickness(0, 0, 0, 20)
            });

            // "Don't show this again" checkbox (Issue #14)
            CheckBox? dontAskCheckBox = null;
            if (showDontAskAgain)
            {
                dontAskCheckBox = new CheckBox
                {
                    Content = "Don't show this again",
                    Margin = new Thickness(0, 0, 0, 20)
                };
                panel.Children.Add(dontAskCheckBox);
            }

            var buttonPanel = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 10
            };

            var result = false;

            var yesButton = new Button { Content = "Yes", Width = 80 };
            yesButton.Click += (s, e) =>
            {
                result = true;
                // Save "don't ask again" preference if checkbox is checked (Issue #14)
                if (dontAskCheckBox?.IsChecked == true)
                {
                    SettingsService.Instance.ShowDeleteConfirmation = false;
                }
                dialog.Close();
            };

            var noButton = new Button { Content = "No", Width = 80 };
            noButton.Click += (s, e) => { result = false; dialog.Close(); };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;

            await dialog.ShowDialog(this);
            return result;
        }

        /// <summary>
        /// Issue #8: Shows error dialog with option to Save As when save fails.
        /// Returns true if user wants to Save As, false to dismiss.
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
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 460,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var buttonPanel = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
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

            await dialog.ShowDialog(this);
            return result;
        }

        // Clipboard/Undo/Redo - delegated to EditMenuController (#466)
        private void OnUndoClick(object? sender, RoutedEventArgs e)
            => _editMenuController.OnUndoClick(sender, e);

        private void OnRedoClick(object? sender, RoutedEventArgs e)
            => _editMenuController.OnRedoClick(sender, e);

        private void OnCutNodeClick(object? sender, RoutedEventArgs e)
            => _editMenuController.OnCutNodeClick(sender, e);

        private void OnCopyNodeClick(object? sender, RoutedEventArgs e)
            => _editMenuController.OnCopyNodeClick(sender, e);

        private void OnPasteAsDuplicateClick(object? sender, RoutedEventArgs e)
            => _editMenuController.OnPasteAsDuplicateClick(sender, e);

        private void OnPasteAsLinkClick(object? sender, RoutedEventArgs e)
            => _editMenuController.OnPasteAsLinkClick(sender, e);

        // Issue #463: Expand/Collapse/Navigate delegated to TreeViewUIController
        private void OnExpandSubnodesClick(object? sender, RoutedEventArgs e)
            => _treeViewUIController.OnExpandSubnodesClick(sender, e);

        private void OnCollapseSubnodesClick(object? sender, RoutedEventArgs e)
            => _treeViewUIController.OnCollapseSubnodesClick(sender, e);

        private void OnGoToParentNodeClick(object? sender, RoutedEventArgs e)
            => _treeViewUIController.OnGoToParentNodeClick(sender, e);

        /// <summary>
        /// Gets the theme-aware success brush for validation feedback.
        /// Uses ThemeSuccess resource if available, falls back to LightGreen.
        /// </summary>
        private Avalonia.Media.IBrush GetSuccessBrush()
        {
            var app = Application.Current;
            if (app?.Resources.TryGetResource("ThemeSuccess", ThemeVariant.Default, out var successBrush) == true
                && successBrush is Avalonia.Media.IBrush brush)
            {
                return brush;
            }
            return Avalonia.Media.Brushes.LightGreen;
        }
    }
}

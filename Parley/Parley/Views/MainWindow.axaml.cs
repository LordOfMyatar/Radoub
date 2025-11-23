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
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
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
        private readonly PropertyPanelPopulator _propertyPopulator; // Helper for populating properties panel
        private readonly PropertyAutoSaveService _propertyAutoSaveService; // Handles auto-saving of node properties
        private readonly ScriptParameterUIManager _parameterUIManager; // Manages script parameter UI and synchronization
        private readonly NodeCreationHelper _nodeCreationHelper; // Handles smart node creation and tree navigation
        private readonly ResourceBrowserManager _resourceBrowserManager; // Manages resource browser dialogs
        private readonly KeyboardShortcutManager _keyboardShortcutManager; // Manages keyboard shortcuts
        private readonly DebugAndLoggingHandler _debugAndLoggingHandler; // Handles debug and logging operations
        private readonly WindowPersistenceManager _windowPersistenceManager; // Manages window and panel persistence

        // DEBOUNCED AUTO-SAVE: Timer for file auto-save after inactivity
        private System.Timers.Timer? _autoSaveTimer;

        // Flag to prevent auto-save during programmatic updates
        private bool _isPopulatingProperties = false;

        // DEBOUNCED NODE CREATION: Moved to NodeCreationHelper service (Issue #76)

        // Parameter autocomplete: Cache of script parameter declarations
        private ScriptParameterDeclarations? _currentConditionDeclarations;
        private ScriptParameterDeclarations? _currentActionDeclarations;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // Initialize selected tree node to null (no selection on startup)
            _viewModel.SelectedTreeNode = null;
            _audioService = new AudioService();
            _creatureService = new CreatureService();
            _pluginManager = new PluginManager();
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
                getSelectedNode: () => _selectedNode);
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

            // Phase 0 Fix: Hide debug console by default
            HideDebugConsoleByDefault();

            // Hook up menu events
            this.Opened += async (s, e) =>
            {
                // Restore window position from settings
                await _windowPersistenceManager.RestoreWindowPositionAsync();

                PopulateRecentFilesMenu();
                // Start enabled plugins after window opens
                var startedPlugins = await _pluginManager.StartEnabledPluginsAsync();
                if (startedPlugins.Count > 0)
                {
                    var message = $"Plugins started:\n• {string.Join("\n• ", startedPlugins)}";
                    var msgBox = new Window
                    {
                        Title = "Plugins Active",
                        Width = 400,
                        Height = 200,
                        Content = new TextBlock
                        {
                            Text = message,
                            Margin = new Thickness(20),
                            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                        }
                    };
                    msgBox.Show(); // Non-modal - doesn't block main window
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

        // Tree navigation
        void IKeyboardShortcutHandler.OnExpandSubnodes() => OnExpandSubnodesClick(null, null!);
        void IKeyboardShortcutHandler.OnCollapseSubnodes() => OnCollapseSubnodesClick(null, null!);
        void IKeyboardShortcutHandler.OnMoveNodeUp() => OnMoveNodeUpClick(null, null!);
        void IKeyboardShortcutHandler.OnMoveNodeDown() => OnMoveNodeDownClick(null, null!);

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
                        // For now, just save what we can
                        _viewModel.StatusMessage = "Cannot auto-save without filename. Use File → Save As first.";
                        return; // Don't close
                    }

                    await _viewModel.SaveDialogAsync(_viewModel.CurrentFileName);
                }

                // Now close (unhook event to prevent recursion)
                this.Closing -= OnWindowClosing;
                this.Close();
            }

            // Clean up resources when window actually closes
            _autoSaveTimer?.Stop();
            _autoSaveTimer?.Dispose();
            _audioService.Dispose();

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

        // File menu handlers
        private async void OnNewClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "File → New clicked");

                // Prompt for save location FIRST - save now, save often!
                var storageProvider = StorageProvider;
                if (storageProvider == null)
                {
                    _viewModel.StatusMessage = "Storage provider not available";
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
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Creating new dialog at: {UnifiedLogger.SanitizePath(filePath)}");

                    // Create blank dialog
                    _viewModel.NewDialog();

                    // Set filename so auto-save works immediately
                    _viewModel.CurrentFileName = filePath;

                    // Save immediately to create file on disk
                    await _viewModel.SaveDialogAsync(filePath);

                    _viewModel.StatusMessage = $"New dialog created: {System.IO.Path.GetFileName(filePath)}";
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"New dialog created and saved to: {UnifiedLogger.SanitizePath(filePath)}");

                    // Refresh recent files menu
                    PopulateRecentFilesMenu();
                }
                else
                {
                    // User cancelled - don't create dialog
                    UnifiedLogger.LogApplication(LogLevel.INFO, "File → New cancelled by user");
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Error creating new dialog: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to create new dialog: {ex.Message}");
            }
        }

        private async void OnOpenClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var storageProvider = StorageProvider;
                if (storageProvider == null)
                {
                    _viewModel.StatusMessage = "Storage provider not available";
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
                    await _viewModel.LoadDialogAsync(filePath);

                    // Load creatures from the same directory as the dialog file
                    await LoadCreaturesFromModuleDirectory(filePath);

                    // Update module info bar
                    UpdateModuleInfo(filePath);
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Error opening file: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open file: {ex.Message}");
            }
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_viewModel.CurrentFileName))
            {
                OnSaveAsClick(sender, e);
                return;
            }

            // First, save any pending node changes
            SaveCurrentNodeProperties();

            // Visual feedback - show saving status
            _viewModel.StatusMessage = "Saving file...";

            await _viewModel.SaveDialogAsync(_viewModel.CurrentFileName);

            _viewModel.StatusMessage = "File saved successfully";
        }

        private void SaveCurrentNodeProperties()
        {
            if (_selectedNode == null || _selectedNode is TreeViewRootNode)
            {
                return;
            }

            var dialogNode = _selectedNode.OriginalNode;

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

            // Update Comment
            var commentTextBox = this.FindControl<TextBox>("CommentTextBox");
            if (commentTextBox != null)
            {
                dialogNode.Comment = commentTextBox.Text ?? "";
            }

            // Update Sound
            var soundTextBox = this.FindControl<TextBox>("SoundTextBox");
            if (soundTextBox != null)
            {
                dialogNode.Sound = soundTextBox.Text ?? "";
            }

            // Update Script
            var scriptTextBox = this.FindControl<TextBox>("ScriptActionTextBox");
            if (scriptTextBox != null)
            {
                dialogNode.ScriptAction = scriptTextBox.Text ?? "";
            }

            // Update Conditional Script (on DialogPtr)
            var scriptAppearsTextBox = this.FindControl<TextBox>("ScriptAppearsTextBox");
            if (scriptAppearsTextBox != null && _selectedNode.SourcePointer != null)
            {
                _selectedNode.SourcePointer.ScriptAppears = scriptAppearsTextBox.Text ?? "";
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

            // Update AnimationLoop
            var animationLoopCheckBox = this.FindControl<CheckBox>("AnimationLoopCheckBox");
            if (animationLoopCheckBox != null && animationLoopCheckBox.IsChecked.HasValue)
            {
                dialogNode.AnimationLoop = animationLoopCheckBox.IsChecked.Value;
            }

            // Update Quest Entry
            var questEntryTextBox2 = this.FindControl<TextBox>("QuestEntryTextBox");
            if (questEntryTextBox2 != null)
            {
                if (string.IsNullOrWhiteSpace(questEntryTextBox2.Text))
                {
                    dialogNode.QuestEntry = uint.MaxValue;
                }
                else if (uint.TryParse(questEntryTextBox2.Text, out uint entryId))
                {
                    dialogNode.QuestEntry = entryId;
                }
            }

            // Update Delay
            var delayTextBox = this.FindControl<TextBox>("DelayTextBox");
            if (delayTextBox != null)
            {
                if (string.IsNullOrWhiteSpace(delayTextBox.Text))
                {
                    dialogNode.Delay = uint.MaxValue;
                }
                else if (uint.TryParse(delayTextBox.Text, out uint delayMs))
                {
                    dialogNode.Delay = delayMs;
                }
            }

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
            try
            {
                var storageProvider = StorageProvider;
                if (storageProvider == null)
                {
                    _viewModel.StatusMessage = "Storage provider not available";
                    return;
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
                    await _viewModel.SaveDialogAsync(filePath);

                    // Refresh recent files menu
                    PopulateRecentFilesMenu();
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Error saving file: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save file: {ex.Message}");
            }
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            _viewModel.CloseDialog();

            // Clear module info bar
            ClearModuleInfo();

            // Clear properties panel when file closed
            _selectedNode = null;
            _propertyPopulator.ClearAllFields();
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnRecentFileClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem menuItem && menuItem.Tag is string filePath)
                {
                    // Check if file exists before trying to load
                    if (!System.IO.File.Exists(filePath))
                    {
                        var fileName = System.IO.Path.GetFileName(filePath);
                        var shouldRemove = await ShowConfirmDialog(
                            "File Not Found",
                            $"The file '{fileName}' could not be found.\n\nFull path: {UnifiedLogger.SanitizePath(filePath)}\n\nRemove from recent files?");

                        if (shouldRemove)
                        {
                            SettingsService.Instance.RemoveRecentFile(filePath);
                            PopulateRecentFilesMenu(); // Refresh menu
                        }
                        return;
                    }

                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading recent file: {UnifiedLogger.SanitizePath(filePath)}");
                    await _viewModel.LoadDialogAsync(filePath);

                    // Load creatures from the same directory as the dialog file
                    await LoadCreaturesFromModuleDirectory(filePath);

                    // Update module info bar
                    UpdateModuleInfo(filePath);
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Error loading recent file: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load recent file: {ex.Message}");
            }
        }

        private void PopulateRecentFilesMenu()
        {
            try
            {
                var recentFilesMenuItem = this.FindControl<MenuItem>("RecentFilesMenuItem");
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
                        var fileName = System.IO.Path.GetFileName(file);
                        // Escape underscores for menu display (Avalonia treats _ as mnemonic)
                        var displayName = fileName.Replace("_", "__");

                        var menuItem = new MenuItem
                        {
                            Header = displayName,
                            Tag = file
                        };
                        menuItem.Click += OnRecentFileClick;
                        ToolTip.SetTip(menuItem, file); // Tooltip shows full path
                        menuItems.Add(menuItem);
                    }

                    menuItems.Add(new Separator());
                    var clearItem = new MenuItem { Header = "Clear Recent Files" };
                    clearItem.Click += (s, args) =>
                    {
                        SettingsService.Instance.ClearRecentFiles();
                        PopulateRecentFilesMenu(); // Refresh menu
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

        // Edit menu handlers - Phase 1 Step 8: Copy Operations
        private async void OnCopyNodeTextClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            var text = _viewModel.GetNodeText(selectedNode);

            if (!string.IsNullOrEmpty(text))
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                    _viewModel.StatusMessage = "Copied node text to clipboard";
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Copied node text to clipboard");
                }
            }
            else
            {
                _viewModel.StatusMessage = "No node selected or node has no text";
            }
        }

        private async void OnCopyNodePropertiesClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            var properties = _viewModel.GetNodeProperties(selectedNode);

            if (!string.IsNullOrEmpty(properties))
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(properties);
                    _viewModel.StatusMessage = "Copied node properties to clipboard";
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Copied node properties to clipboard");
                }
            }
            else
            {
                _viewModel.StatusMessage = "No node selected";
            }
        }

        private async void OnCopyTreeStructureClick(object? sender, RoutedEventArgs e)
        {
            var treeStructure = _viewModel.GetTreeStructure();

            if (!string.IsNullOrEmpty(treeStructure))
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(treeStructure);
                    _viewModel.StatusMessage = "Copied tree structure to clipboard";
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Copied tree structure to clipboard");
                }
            }
            else
            {
                _viewModel.StatusMessage = "No dialog loaded";
            }
        }

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

        // Font size handlers - Fixed in #58 (font sizing) and #59 (font selection)
        private void OnFontSizeClick(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string sizeStr)
            {
                if (double.TryParse(sizeStr, out double fontSize))
                {
                    SettingsService.Instance.FontSize = fontSize;
                    _viewModel.StatusMessage = $"Font size changed to {fontSize}pt";
                    // Global application now handled via App.xaml styles and App.ApplyFontSize()
                }
            }
        }

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
        private async void OnPreferencesClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new SettingsWindow(pluginManager: _pluginManager);
                await settingsWindow.ShowDialog(this);

                // Reload theme in case it changed
                ApplySavedTheme();

                _viewModel.StatusMessage = "Settings updated";
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening preferences: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening preferences: {ex.Message}";
            }
        }

        private async void OnGameDirectoriesClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Open preferences with Resource Paths tab selected (tab 0)
                var settingsWindow = new SettingsWindow(initialTab: 0, pluginManager: _pluginManager);
                await settingsWindow.ShowDialog(this);
                ApplySavedTheme();
                _viewModel.StatusMessage = "Settings updated";
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening game directories: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening settings: {ex.Message}";
            }
        }

        private async void OnLogSettingsClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Open preferences with Logging tab selected (tab 2)
                var settingsWindow = new SettingsWindow(initialTab: 2, pluginManager: _pluginManager);
                await settingsWindow.ShowDialog(this);
                ApplySavedTheme();
                _viewModel.StatusMessage = "Settings updated";
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

        private void OnDialogTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // CRITICAL FIX: Save the PREVIOUS node's properties before switching
            if (_selectedNode != null && !(_selectedNode is TreeViewRootNode))
            {
                SaveCurrentNodeProperties();
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Saved previous node properties before tree selection change");
            }

            var treeView = sender as TreeView;
            _selectedNode = treeView?.SelectedItem as TreeViewSafeNode;

            // Update ViewModel's selected tree node for Restore button enabling
            if (_viewModel != null)
            {
                _viewModel.SelectedTreeNode = _selectedNode;
            }

            // Show/hide panels based on node type
            var conversationSettingsPanel = this.FindControl<StackPanel>("ConversationSettingsPanel");
            var nodePropertiesPanel = this.FindControl<StackPanel>("NodePropertiesPanel");

            if (_selectedNode is TreeViewRootNode)
            {
                // ROOT node: Show conversation settings, hide node properties
                if (conversationSettingsPanel != null) conversationSettingsPanel.IsVisible = true;
                if (nodePropertiesPanel != null) nodePropertiesPanel.IsVisible = false;
            }
            else
            {
                // Regular node: Hide conversation settings, show node properties
                if (conversationSettingsPanel != null) conversationSettingsPanel.IsVisible = false;
                if (nodePropertiesPanel != null) nodePropertiesPanel.IsVisible = true;
            }

            if (_selectedNode != null)
            {
                PopulatePropertiesPanel(_selectedNode);
            }
            else
            {
                _propertyPopulator.ClearAllFields();
            }
        }

        private void OnTreeViewItemDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            // Toggle expansion of the selected node when double-tapped
            if (_selectedNode != null)
            {
                _selectedNode.IsExpanded = !_selectedNode.IsExpanded;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Double-tap toggled node expansion: {_selectedNode.IsExpanded}");
            }
        }

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

            // Populate all node properties using helper
            _propertyPopulator.PopulateNodeType(dialogNode);
            _propertyPopulator.PopulateSpeaker(dialogNode);
            _propertyPopulator.PopulateBasicProperties(dialogNode);
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
                    dialogNode.Comment = textBox.Text ?? "";
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

        // FIELD-LEVEL AUTO-SAVE: Save property when field loses focus
        private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
        {
            if (_selectedNode == null || _isPopulatingProperties) return;

            var control = sender as Control;
            if (control == null) return;

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

            // Create new timer that fires after configured delay
            var delayMs = SettingsService.Instance.AutoSaveDelayMs;
            _autoSaveTimer = new System.Timers.Timer(delayMs);
            _autoSaveTimer.AutoReset = false; // Only fire once
            _autoSaveTimer.Elapsed += async (s, e) => await AutoSaveToFileAsync();
            _autoSaveTimer.Start();

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Debounced auto-save scheduled in {delayMs}ms");
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

                    await _viewModel.SaveDialogAsync(_viewModel.CurrentFileName);

                    var timestamp = DateTime.Now.ToString("h:mm tt");
                    var fileName = System.IO.Path.GetFileName(_viewModel.CurrentFileName);
                    _viewModel.StatusMessage = $"✓ Auto-saved '{fileName}' at {timestamp}";

                    // Verify HasUnsavedChanges was cleared (Issue #18)
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Auto-save completed. HasUnsavedChanges = {_viewModel.HasUnsavedChanges}, WindowTitle = '{_viewModel.WindowTitle}'");
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

            // Update Comment
            var commentTextBox = this.FindControl<TextBox>("CommentTextBox");
            if (commentTextBox != null)
            {
                dialogNode.Comment = commentTextBox.Text ?? "";
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
        {
            // Always reload declarations from current textbox to avoid caching wrong script
            var scriptTextBox = this.FindControl<TextBox>("ScriptAppearsTextBox");
            var currentScript = scriptTextBox?.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(currentScript))
            {
                await LoadParameterDeclarationsAsync(currentScript, true);
            }

            ShowParameterBrowser(_currentConditionDeclarations, true);
        }

        private async void OnSuggestActionsParamClick(object? sender, RoutedEventArgs e)
        {
            // Always reload declarations from current textbox to avoid caching wrong script
            var scriptTextBox = this.FindControl<TextBox>("ScriptActionTextBox");
            var currentScript = scriptTextBox?.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(currentScript))
            {
                await LoadParameterDeclarationsAsync(currentScript, false);
            }

            ShowParameterBrowser(_currentActionDeclarations, false);
        }

        /// <summary>
        /// Extracts existing parameters from the UI panel for dependency resolution.
        /// Returns a dictionary of key-value pairs currently in the parameter panel.
        /// </summary>
        private Dictionary<string, string> GetExistingParametersFromPanel(bool isCondition)
        {
            var parameters = new Dictionary<string, string>();

            try
            {
                var panelName = isCondition ? "ConditionsParametersPanel" : "ActionsParametersPanel";
                var panel = this.FindControl<StackPanel>(panelName);

                if (panel == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"GetExistingParametersFromPanel: {panelName} not found");
                    return parameters;
                }

                foreach (var child in panel.Children)
                {
                    if (child is Grid paramGrid && paramGrid.Children.Count >= 2)
                    {
                        var keyTextBox = paramGrid.Children[0] as TextBox;
                        var valueTextBox = paramGrid.Children[1] as TextBox; // Value is at index 1, not 2!

                        if (keyTextBox != null && valueTextBox != null &&
                            !string.IsNullOrWhiteSpace(keyTextBox.Text))
                        {
                            string key = keyTextBox.Text.Trim();
                            string value = (valueTextBox.Text ?? "").Trim();

                            if (!parameters.ContainsKey(key))
                            {
                                parameters[key] = value;
                                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                    $"GetExistingParametersFromPanel: Found parameter '{key}' = '{value}'");
                            }
                        }
                    }
                }

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"GetExistingParametersFromPanel: Extracted {parameters.Count} existing parameters");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"GetExistingParametersFromPanel: Error extracting parameters - {ex.Message}");
            }

            return parameters;
        }

        /// <summary>
        /// Shows parameter browser window for selecting parameters
        /// </summary>
        private async void ShowParameterBrowser(ScriptParameterDeclarations? declarations, bool isCondition)
        {
            try
            {
                // Get script name from the appropriate textbox
                string scriptName = "";
                if (isCondition)
                {
                    var scriptTextBox = this.FindControl<TextBox>("ScriptAppearsTextBox");
                    scriptName = scriptTextBox?.Text ?? "";
                }
                else
                {
                    var scriptTextBox = this.FindControl<TextBox>("ScriptActionTextBox");
                    scriptName = scriptTextBox?.Text ?? "";
                }

                // Get existing parameters from the node for dependency resolution
                var existingParameters = GetExistingParametersFromPanel(isCondition);

                var browser = new ParameterBrowserWindow();
                browser.SetDeclarations(declarations, scriptName, isCondition, existingParameters);

                await browser.ShowDialog(this);

                if (browser.DialogResult && !string.IsNullOrEmpty(browser.SelectedKey))
                {
                    // Add the parameter to the appropriate panel
                    var key = browser.SelectedKey;
                    var value = browser.SelectedValue ?? "";

                    // Find the appropriate panel
                    var panelName = isCondition ? "ConditionsParametersPanel" : "ActionsParametersPanel";
                    var panel = this.FindControl<StackPanel>(panelName);

                    if (panel != null)
                    {
                        AddParameterRow(panel, key, value, isCondition);
                        OnParameterChanged(isCondition);

                        var paramType = isCondition ? "condition" : "action";
                        _viewModel.StatusMessage = $"Added {paramType} parameter: {key}={value}";
                        UnifiedLogger.LogApplication(LogLevel.INFO,
                            $"Added parameter from browser - Type: {paramType}, Key: '{key}', Value: '{value}'");
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Error showing parameter browser: {ex.Message}");
                _viewModel.StatusMessage = "Error showing parameter browser";
            }
        }

        private void AddParameterRow(StackPanel parent, string key, string value, bool isCondition)
        {
            // Create grid: [Key TextBox] [=] [Value TextBox] [Delete Button]
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Key textbox
            var keyTextBox = new TextBox
            {
                Text = key,
                Watermark = "Parameter key",
                FontFamily = new global::Avalonia.Media.FontFamily("Consolas,Courier New,monospace"),
                [Grid.ColumnProperty] = 0
            };
            keyTextBox.LostFocus += (s, e) =>
            {
                OnParameterChanged(isCondition);
            };
            grid.Children.Add(keyTextBox);

            // Value textbox
            var valueTextBox = new TextBox
            {
                Text = value,
                Watermark = "Parameter value",
                FontFamily = new global::Avalonia.Media.FontFamily("Consolas,Courier New,monospace"),
                [Grid.ColumnProperty] = 2
            };
            valueTextBox.LostFocus += (s, e) =>
            {
                OnParameterChanged(isCondition);
            };
            grid.Children.Add(valueTextBox);

            // Delete button
            var deleteButton = new Button
            {
                Content = "×",
                Width = 25,
                Height = 25,
                FontSize = 16,
                [Grid.ColumnProperty] = 4
            };
            deleteButton.Click += (s, e) =>
            {
                parent.Children.Remove(grid);
                OnParameterChanged(isCondition);
                _viewModel.StatusMessage = $"Removed {(isCondition ? "condition" : "action")} parameter";
            };
            grid.Children.Add(deleteButton);

            parent.Children.Add(grid);
        }

        private async Task LoadParameterDeclarationsAsync(string scriptName, bool isCondition)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                if (isCondition)
                {
                    _currentConditionDeclarations = null;
                }
                else
                {
                    _currentActionDeclarations = null;
                }
                return;
            }

            try
            {
                var declarations = await ScriptService.Instance.GetParameterDeclarationsAsync(scriptName);

                if (isCondition)
                {
                    _currentConditionDeclarations = declarations;
                }
                else
                {
                    _currentActionDeclarations = declarations;
                }

                if (declarations.HasDeclarations)
                {
                    var totalValues = declarations.ValuesByKey.Values.Sum(list => list.Count);
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Loaded parameter declarations for {(isCondition ? "condition" : "action")} script '{scriptName}': {declarations.Keys.Count} keys, {totalValues} values");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to load parameter declarations for '{scriptName}': {ex.Message}");
            }
        }

        private async Task LoadScriptPreviewAsync(string scriptName, bool isCondition)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                ClearScriptPreview(isCondition);
                return;
            }

            try
            {
                var previewTextBox = isCondition
                    ? this.FindControl<TextBox>("ConditionalScriptPreviewTextBox")
                    : this.FindControl<TextBox>("ActionScriptPreviewTextBox");

                if (previewTextBox == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"LoadScriptPreviewAsync: Preview TextBox not found for {(isCondition ? "conditional" : "action")} script");
                    return;
                }

                previewTextBox.Text = "Loading...";

                var scriptContent = await ScriptService.Instance.GetScriptContentAsync(scriptName);

                if (!string.IsNullOrEmpty(scriptContent))
                {
                    previewTextBox.Text = scriptContent;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"LoadScriptPreviewAsync: Loaded preview for {(isCondition ? "conditional" : "action")} script '{scriptName}'");
                }
                else
                {
                    previewTextBox.Text = $"// Script '{scriptName}.nss' not found or could not be loaded.\n" +
                                          "// Make sure the .nss file exists in your module directory.";
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"LoadScriptPreviewAsync: No content for script '{scriptName}'");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"LoadScriptPreviewAsync: Error loading preview for '{scriptName}': {ex.Message}");
                ClearScriptPreview(isCondition);
            }
        }

        private void ClearScriptPreview(bool isCondition)
        {
            var previewTextBox = isCondition
                ? this.FindControl<TextBox>("ConditionalScriptPreviewTextBox")
                : this.FindControl<TextBox>("ActionScriptPreviewTextBox");

            if (previewTextBox != null)
            {
                previewTextBox.Text = $"// {(isCondition ? "Conditional" : "Action")} script preview will appear here";
            }
        }

        private void OnParameterChanged(bool isCondition)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, $"OnParameterChanged: ENTRY - isCondition={isCondition}, _selectedNode={((_selectedNode == null) ? "null" : _selectedNode.OriginalNode.DisplayText)}, _isPopulatingProperties={_isPopulatingProperties}");

            if (_selectedNode == null || _isPopulatingProperties)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"OnParameterChanged: Early return - _selectedNode={((_selectedNode == null) ? "null" : "not null")}, _isPopulatingProperties={_isPopulatingProperties}");
                return;
            }

            var dialogNode = _selectedNode.OriginalNode;
            var sourcePtr = _selectedNode.SourcePointer;

            if (isCondition)
            {
                // Conditional parameters are on the DialogPtr
                if (sourcePtr != null)
                {
                    _parameterUIManager.UpdateConditionParamsFromUI(sourcePtr);
                    _viewModel.StatusMessage = "Condition parameters updated";
                }
                else
                {
                    _viewModel.StatusMessage = "No pointer context for conditional parameters";
                }
            }
            else
            {
                // Action parameters are on the DialogNode
                _parameterUIManager.UpdateActionParamsFromUI(dialogNode);
                _viewModel.StatusMessage = "Action parameters updated";
            }

            _viewModel.HasUnsavedChanges = true;
            TriggerDebouncedAutoSave();
        }

        /// <summary>
        /// Shows visual feedback when parameter text is trimmed.
        /// Briefly flashes the TextBox border to indicate successful trim operation.
        /// </summary>
        private async void ShowTrimFeedback(TextBox textBox)
        {
            // Store original border properties
            var originalBrush = textBox.BorderBrush;
            var originalThickness = textBox.BorderThickness;

            try
            {
                // Flash green border to indicate trim occurred
                textBox.BorderBrush = Avalonia.Media.Brushes.LightGreen;
                textBox.BorderThickness = new Thickness(2);

                // Wait briefly for visual feedback
                await Task.Delay(300);

                // Restore original appearance
                textBox.BorderBrush = originalBrush;
                textBox.BorderThickness = originalThickness;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"ShowTrimFeedback: Error showing visual feedback - {ex.Message}");
                // Ensure we restore original state even if error occurs
                textBox.BorderBrush = originalBrush;
                textBox.BorderThickness = originalThickness;
            }
        }



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

        private async Task LoadCreaturesFromModuleDirectory(string dialogFilePath)
        {
            try
            {
                // Get directory containing the dialog file (module directory)
                var moduleDirectory = Path.GetDirectoryName(dialogFilePath);
                if (string.IsNullOrEmpty(moduleDirectory))
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot determine module directory from dialog path");
                    return;
                }

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading creatures from module: {UnifiedLogger.SanitizePath(moduleDirectory)}");

                // Scan for UTC files in module directory
                var creatures = await _creatureService.ScanCreaturesAsync(moduleDirectory);

                if (creatures.Count > 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {creatures.Count} creatures from module");
                    _viewModel.StatusMessage = $"Loaded {creatures.Count} creature{(creatures.Count == 1 ? "" : "s")}";
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "No UTC files found in module directory");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load creatures: {ex.Message}");
                // Don't block dialog loading if creature loading fails
            }
        }

        private void UpdateModuleInfo(string dialogFilePath)
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

                // Sanitize path for display (replace user directory with ~)
                var displayPath = PathHelper.SanitizePathForDisplay(moduleDirectory);

                // Update UI
                var moduleNameTextBlock = this.FindControl<TextBlock>("ModuleNameTextBlock");
                var modulePathTextBlock = this.FindControl<TextBlock>("ModulePathTextBlock");

                if (moduleNameTextBlock != null)
                {
                    moduleNameTextBlock.Text = moduleName ?? Path.GetFileName(moduleDirectory);
                }

                if (modulePathTextBlock != null)
                {
                    modulePathTextBlock.Text = displayPath;
                }

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Module info updated: {moduleName ?? "(unnamed)"} | {displayPath}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to update module info: {ex.Message}");
                ClearModuleInfo();
            }
        }

        private void ClearModuleInfo()
        {
            var moduleNameTextBlock = this.FindControl<TextBlock>("ModuleNameTextBlock");
            var modulePathTextBlock = this.FindControl<TextBlock>("ModulePathTextBlock");

            if (moduleNameTextBlock != null)
            {
                moduleNameTextBlock.Text = "No module loaded";
            }

            if (modulePathTextBlock != null)
            {
                modulePathTextBlock.Text = "";
            }
        }


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

        private async void OnBrowseConversationScriptClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            var fieldName = button.Tag?.ToString();

            try
            {
                var scriptBrowser = new ScriptBrowserWindow();
                var result = await scriptBrowser.ShowDialog<string?>(this);

                if (!string.IsNullOrEmpty(result))
                {
                    if (fieldName == "ScriptEnd")
                    {
                        var scriptEndTextBox = this.FindControl<TextBox>("ScriptEndTextBox");
                        if (scriptEndTextBox != null)
                        {
                            scriptEndTextBox.Text = result;
                            if (_viewModel.CurrentDialog != null)
                            {
                                _viewModel.CurrentDialog.ScriptEnd = result;
                            }
                        }
                    }
                    else if (fieldName == "ScriptAbort")
                    {
                        var scriptAbortTextBox = this.FindControl<TextBox>("ScriptAbortTextBox");
                        if (scriptAbortTextBox != null)
                        {
                            scriptAbortTextBox.Text = result;
                            if (_viewModel.CurrentDialog != null)
                            {
                                _viewModel.CurrentDialog.ScriptAbort = result;
                            }
                        }
                    }

                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Selected conversation script for {fieldName}: {result}");
                    _viewModel.StatusMessage = $"Selected script: {result}";
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening script browser: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening script browser: {ex.Message}";
            }
        }

        private void OnQuestTagChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_selectedNode == null || _isPopulatingProperties) return;

            var questTagComboBox = sender as ComboBox;
            if (questTagComboBox?.SelectedItem is JournalCategory category)
            {
                var dialogNode = _selectedNode.OriginalNode;
                dialogNode.Quest = category.Tag;

                // Update quest name display
                var questNameTextBlock = this.FindControl<TextBlock>("QuestNameTextBlock");
                if (questNameTextBlock != null)
                {
                    var questName = category.Name?.GetDefault();
                    questNameTextBlock.Text = string.IsNullOrEmpty(questName)
                        ? ""
                        : $"Quest: {questName}";
                }

                // Update Quest Entry dropdown with entries for this quest
                var questEntryComboBox = this.FindControl<ComboBox>("QuestEntryComboBox");
                if (questEntryComboBox != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Setting Quest Entry dropdown ItemsSource to {category.Entries.Count} entries for quest '{category.Tag}'");
                    questEntryComboBox.ItemsSource = category.Entries;
                    // Try to preserve current QuestEntry if it exists in this quest
                    if (dialogNode.QuestEntry != uint.MaxValue)
                    {
                        var matchingEntry = category.Entries.FirstOrDefault(e => e.ID == dialogNode.QuestEntry);
                        questEntryComboBox.SelectedItem = matchingEntry;
                    }
                    else
                    {
                        questEntryComboBox.SelectedIndex = -1;
                    }
                }

                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Quest tag set to: {category.Tag}");
                _viewModel.StatusMessage = $"Quest: {category.DisplayName}";
            }
            else
            {
                // Cleared selection
                var dialogNode = _selectedNode.OriginalNode;
                dialogNode.Quest = string.Empty;
                dialogNode.QuestEntry = uint.MaxValue;

                // Clear quest name display
                var questNameTextBlock = this.FindControl<TextBlock>("QuestNameTextBlock");
                if (questNameTextBlock != null)
                    questNameTextBlock.Text = "";

                var questEntryComboBox = this.FindControl<ComboBox>("QuestEntryComboBox");
                if (questEntryComboBox != null)
                {
                    questEntryComboBox.ItemsSource = null;
                    questEntryComboBox.SelectedIndex = -1;
                }
            }
        }

        private void OnQuestEntryChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_selectedNode == null || _isPopulatingProperties) return;

            var questEntryComboBox = sender as ComboBox;
            if (questEntryComboBox?.SelectedItem is JournalEntry entry)
            {
                var dialogNode = _selectedNode.OriginalNode;
                dialogNode.QuestEntry = entry.ID;

                // Update text preview
                var questEntryPreviewTextBlock = this.FindControl<TextBlock>("QuestEntryPreviewTextBlock");
                if (questEntryPreviewTextBlock != null)
                {
                    questEntryPreviewTextBlock.Text = entry.TextPreview;
                }

                // Update End indicator
                var questEntryEndTextBlock = this.FindControl<TextBlock>("QuestEntryEndTextBlock");
                if (questEntryEndTextBlock != null)
                {
                    questEntryEndTextBlock.Text = entry.End ? "✓ Quest Complete" : "";
                }

                var endStatus = entry.End ? " (Quest Complete - plays reward sound)" : "";
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Quest entry set to: {entry.ID}{endStatus}");
                _viewModel.StatusMessage = $"Entry {entry.ID}: {entry.FullText}{endStatus}";
            }
            else
            {
                // Cleared selection
                var dialogNode = _selectedNode.OriginalNode;
                dialogNode.QuestEntry = uint.MaxValue;

                // Clear displays
                var questEntryPreviewTextBlock = this.FindControl<TextBlock>("QuestEntryPreviewTextBlock");
                if (questEntryPreviewTextBlock != null)
                    questEntryPreviewTextBlock.Text = "";

                var questEntryEndTextBlock = this.FindControl<TextBlock>("QuestEntryEndTextBlock");
                if (questEntryEndTextBlock != null)
                    questEntryEndTextBlock.Text = "";
            }
        }

        private void OnClearQuestTagClick(object? sender, RoutedEventArgs e)
        {
            if (_selectedNode == null) return;

            var questTagComboBox = this.FindControl<ComboBox>("QuestTagComboBox");
            if (questTagComboBox != null)
            {
                questTagComboBox.SelectedIndex = -1; // Clears selection, triggers OnQuestTagChanged
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Quest tag cleared");
            _viewModel.StatusMessage = "Quest tag cleared";
        }

        private void OnClearQuestEntryClick(object? sender, RoutedEventArgs e)
        {
            if (_selectedNode == null) return;

            var questEntryComboBox = this.FindControl<ComboBox>("QuestEntryComboBox");
            if (questEntryComboBox != null)
            {
                questEntryComboBox.SelectedIndex = -1; // Clears selection, triggers OnQuestEntryChanged
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Quest entry cleared");
            _viewModel.StatusMessage = "Quest entry cleared";
        }

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

        private async void OnBrowseConditionalScriptClick(object? sender, RoutedEventArgs e)
        {
            // Core Feature: Conditional scripts on DialogPtr
            if (_selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a dialog node first";
                return;
            }

            if (_selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Cannot assign conditional scripts to ROOT. Select a dialog node instead.";
                return;
            }

            // Check if we have a pointer context
            if (_selectedNode.SourcePointer == null)
            {
                _viewModel.StatusMessage = "No pointer context - conditional scripts only apply to linked nodes";
                return;
            }

            try
            {
                var scriptBrowser = new ScriptBrowserWindow();
                var result = await scriptBrowser.ShowDialog<string?>(this);

                if (!string.IsNullOrEmpty(result))
                {
                    // Update the conditional script field with selected script
                    var scriptTextBox = this.FindControl<TextBox>("ScriptAppearsTextBox");
                    if (scriptTextBox != null)
                    {
                        scriptTextBox.Text = result;
                        // Trigger auto-save
                        AutoSaveProperty("ScriptAppearsTextBox");
                    }
                    _viewModel.StatusMessage = $"Selected conditional script: {result}";
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening script browser: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening script browser: {ex.Message}";
            }
        }

        private async void OnBrowseActionScriptClick(object? sender, RoutedEventArgs e)
        {
            // Phase 2 Fix: Don't allow script browser when no node selected or ROOT selected
            if (_selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a dialog node first";
                return;
            }

            if (_selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Cannot assign scripts to ROOT. Select a dialog node instead.";
                return;
            }

            try
            {
                var scriptBrowser = new ScriptBrowserWindow();
                var result = await scriptBrowser.ShowDialog<string?>(this);

                if (!string.IsNullOrEmpty(result))
                {
                    // Update the script action field with selected script
                    var scriptTextBox = this.FindControl<TextBox>("ScriptActionTextBox");
                    if (scriptTextBox != null)
                    {
                        scriptTextBox.Text = result;
                        // Trigger auto-save
                        AutoSaveProperty("ScriptActionTextBox");
                    }
                    _viewModel.StatusMessage = $"Selected script: {result}";
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening script browser: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening script browser: {ex.Message}";
            }
        }

        private void OnEditConditionalScriptClick(object? sender, RoutedEventArgs e)
        {
            var scriptTextBox = this.FindControl<TextBox>("ScriptAppearsTextBox");
            string? scriptName = scriptTextBox?.Text;

            if (string.IsNullOrWhiteSpace(scriptName))
            {
                _viewModel.StatusMessage = "No conditional script assigned";
                return;
            }

            bool success = ExternalEditorService.Instance.OpenScript(scriptName, _viewModel.CurrentFileName);

            if (success)
            {
                _viewModel.StatusMessage = $"Opened '{scriptName}' in editor";
            }
            else
            {
                _viewModel.StatusMessage = $"Could not find script '{scriptName}.nss'";
            }
        }

        private void OnEditActionScriptClick(object? sender, RoutedEventArgs e)
        {
            var scriptTextBox = this.FindControl<TextBox>("ScriptActionTextBox");
            string? scriptName = scriptTextBox?.Text;

            if (string.IsNullOrWhiteSpace(scriptName))
            {
                _viewModel.StatusMessage = "No action script assigned";
                return;
            }

            bool success = ExternalEditorService.Instance.OpenScript(scriptName, _viewModel.CurrentFileName);

            if (success)
            {
                _viewModel.StatusMessage = $"Opened '{scriptName}' in editor";
            }
            else
            {
                _viewModel.StatusMessage = $"Could not find script '{scriptName}.nss'";
            }
        }

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

            // Confirm deletion
            var confirmed = await ShowConfirmDialog(
                "Delete Node",
                $"Are you sure you want to delete this node and all its children?\n\n\"{selectedNode.DisplayText}\""
            );

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

        /// <summary>
        /// Load journal file for the current module and populate quest dropdown
        /// </summary>
        private async Task LoadJournalForCurrentModuleAsync()
        {
            try
            {
                // Try to get module directory from currently loaded file
                string? modulePath = null;

                if (!string.IsNullOrEmpty(_viewModel.CurrentFileName))
                {
                    // Use directory of current .dlg file
                    modulePath = Path.GetDirectoryName(_viewModel.CurrentFileName);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Using module path from current file: {UnifiedLogger.SanitizePath(modulePath ?? "")}");
                }

                // Fallback to settings if no file loaded
                if (string.IsNullOrEmpty(modulePath))
                {
                    modulePath = SettingsService.Instance.CurrentModulePath;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Using module path from settings: {UnifiedLogger.SanitizePath(modulePath)}");
                }

                if (string.IsNullOrEmpty(modulePath) || !Directory.Exists(modulePath))
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Module path not set or doesn't exist - journal not loaded");
                    return;
                }

                var journalPath = Path.Combine(modulePath, "module.jrl");
                if (!File.Exists(journalPath))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"No module.jrl found at {UnifiedLogger.SanitizePath(journalPath)}");
                    return;
                }

                // Parse journal file
                var categories = await JournalService.Instance.ParseJournalFileAsync(journalPath);

                // Populate Quest Tag dropdown
                var questTagComboBox = this.FindControl<ComboBox>("QuestTagComboBox");
                if (questTagComboBox != null)
                {
                    questTagComboBox.ItemsSource = categories;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {categories.Count} quest categories from journal");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading journal: {ex.Message}");
            }
        }

        private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Load journal when file is loaded (CurrentFileName is set AFTER CurrentDialog)
            if (e.PropertyName == nameof(MainViewModel.CurrentFileName))
            {
                if (!string.IsNullOrEmpty(_viewModel.CurrentFileName))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"CurrentFileName changed to: {UnifiedLogger.SanitizePath(_viewModel.CurrentFileName)} - loading journal");
                    await LoadJournalForCurrentModuleAsync();
                }
            }

            // Watch for node re-selection requests after tree refresh
            if (e.PropertyName == nameof(MainViewModel.NodeToSelectAfterRefresh))
            {
                var nodeToSelect = _viewModel.NodeToSelectAfterRefresh;
                if (nodeToSelect != null)
                {
                    // Schedule selection for after tree is fully rebuilt
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        var treeNode = _viewModel.FindTreeNodeForDialogNode(nodeToSelect);
                        if (treeNode != null)
                        {
                            var treeView = this.FindControl<TreeView>("DialogTreeView");
                            if (treeView != null)
                            {
                                treeView.SelectedItem = treeNode;
                                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Re-selected node after refresh: {treeNode.DisplayText}");

                                // Focus needs to be set after selection is fully processed
                                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    treeView.Focus();
                                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"TreeView focus restored after node move");
                                }, global::Avalonia.Threading.DispatcherPriority.Background);
                            }
                        }
                        // Clear the request
                        _viewModel.NodeToSelectAfterRefresh = null;
                    }, global::Avalonia.Threading.DispatcherPriority.Loaded);
                }
            }
        }

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
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 560, // MaxWidth - margins
                Margin = new Thickness(0, 0, 0, 20)
            });

            var buttonPanel = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
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

            await dialog.ShowDialog(this);
            return result;
        }

        // Phase 1 Step 7: Copy/Paste/Cut handlers
        private void OnUndoClick(object? sender, RoutedEventArgs e)
        {
            _viewModel.Undo();
        }

        private void OnRedoClick(object? sender, RoutedEventArgs e)
        {
            _viewModel.Redo();
        }

        private void OnCutNodeClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a node to cut";
                return;
            }

            // Use proper Cut method that detaches without deleting children
            _viewModel.CutNode(selectedNode);
        }

        private void OnCopyNodeClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a node to copy";
                return;
            }

            _viewModel.CopyNode(selectedNode);
        }

        private void OnPasteAsDuplicateClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a parent node to paste under";
                return;
            }

            _viewModel.PasteAsDuplicate(selectedNode);
        }

        private void OnPasteAsLinkClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a parent node to paste link under";
                return;
            }

            _viewModel.PasteAsLink(selectedNode);
        }

        // Expand/Collapse Subnodes (Issue #39)
        private void OnExpandSubnodesClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a node to expand";
                return;
            }

            ExpandNodeRecursive(selectedNode);
            _viewModel.StatusMessage = $"Expanded node and all subnodes: {selectedNode.DisplayText}";
        }

        private void OnCollapseSubnodesClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a node to collapse";
                return;
            }

            CollapseNodeRecursive(selectedNode);
            _viewModel.StatusMessage = $"Collapsed node and all subnodes: {selectedNode.DisplayText}";
        }

        private void ExpandNodeRecursive(TreeViewSafeNode node, HashSet<TreeViewSafeNode>? visited = null)
        {
            try
            {
                // Prevent infinite loops from circular references
                visited ??= new HashSet<TreeViewSafeNode>();

                if (!visited.Add(node))
                {
                    // Already visited - circular reference detected
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Circular reference detected in expand: {node.DisplayText}");
                    return;
                }

                node.IsExpanded = true;

                // Copy children list to avoid collection modification issues
                var children = node.Children?.ToList() ?? new List<TreeViewSafeNode>();
                foreach (var child in children)
                {
                    if (child != null)
                    {
                        ExpandNodeRecursive(child, visited);
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error expanding node '{node?.DisplayText}': {ex.Message}");
                _viewModel.StatusMessage = $"Error expanding node: {ex.Message}";
            }
        }

        private void CollapseNodeRecursive(TreeViewSafeNode node, HashSet<TreeViewSafeNode>? visited = null)
        {
            try
            {
                // Prevent infinite loops from circular references
                visited ??= new HashSet<TreeViewSafeNode>();

                if (!visited.Add(node))
                {
                    // Already visited - circular reference detected
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Circular reference detected in collapse: {node.DisplayText}");
                    return;
                }

                node.IsExpanded = false;

                // Copy children list to avoid collection modification issues
                var children = node.Children?.ToList() ?? new List<TreeViewSafeNode>();
                foreach (var child in children)
                {
                    if (child != null)
                    {
                        CollapseNodeRecursive(child, visited);
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error collapsing node '{node?.DisplayText}': {ex.Message}");
                _viewModel.StatusMessage = $"Error collapsing node: {ex.Message}";
            }
        }
    }
}

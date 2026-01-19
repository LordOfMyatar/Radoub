using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using Radoub.UI.Utils;
using Parley.Models;

namespace DialogEditor.ViewModels
{
    /// <summary>
    /// MainViewModel - Core properties, constructor, and debug messaging.
    ///
    /// Split into partial classes for maintainability (#536):
    /// - MainViewModel.cs (this file) - Core properties and constructor
    /// - MainViewModel.FileOperations.cs - Load, Save, New, Close
    /// - MainViewModel.EditOperations.cs - Undo, Redo, Copy, Cut, Paste
    /// - MainViewModel.NodeOperations.cs - Add, Delete, Move nodes
    /// - MainViewModel.TreeOperations.cs - Tree population, refresh, navigation
    /// - MainViewModel.ScrapOperations.cs - Scrap restore and management
    /// </summary>
    public partial class MainViewModel : BaseViewModel
    {
        #region Private Fields

        private Dialog? _currentDialog;
        private string? _currentFileName;
        private bool _isLoading;
        private string _statusMessage = "Ready";
        private string _lastSavedTime = ""; // Issue #62 - Last saved indicator
        private ObservableCollection<string> _debugMessages = new();
        private ObservableCollection<TreeViewSafeNode> _dialogNodes = new();
        private bool _hasUnsavedChanges;
        private ScrapEntry? _selectedScrapEntry;
        private TreeViewSafeNode? _selectedTreeNode;

        // Node to re-select after tree refresh (used by View to restore selection)
        private DialogNode? _nodeToSelectAfterRefresh;

        #endregion

        #region Services

        private readonly UndoRedoService _undoRedoService = new(50); // Undo/redo service with 50 state history
        private readonly ScrapManager _scrapManager = new(); // Manages deleted/cut nodes
        private readonly DialogEditorService _editorService = new(); // Service for node editing operations
        private readonly DialogClipboardService _clipboardService = new(); // Service for clipboard operations
        private readonly OrphanNodeManager _orphanManager = new(); // Service for orphan pointer cleanup
        private readonly TreeNavigationManager _treeNavManager = new(); // Service for tree navigation and state
        private readonly NodeOperationsManager _nodeOpsManager; // Service for node add/delete/move operations
        private readonly IndexManager _indexManager = new(); // Service for pointer index management
        private readonly NodeCloningService _cloningService = new(); // Service for deep node cloning
        private readonly ReferenceManager _referenceManager = new(); // Service for reference counting and pointer operations
        private readonly PasteOperationsManager _pasteManager; // Service for paste operations
        private readonly DialogSaveService _saveService = new(); // Service for dialog save operations

        #endregion

        #region Properties

        public Dialog? CurrentDialog
        {
            get => _currentDialog;
            set
            {
                if (SetProperty(ref _currentDialog, value))
                {
                    OnPropertyChanged(nameof(CanRestoreFromScrap));
                    // Sync with DialogContextService for plugin access (#227)
                    DialogContextService.Instance.CurrentDialog = value;
                }
            }
        }

        public string? CurrentFileName
        {
            get => _currentFileName;
            set
            {
                if (SetProperty(ref _currentFileName, value))
                {
                    OnPropertyChanged(nameof(LoadedFileName));
                    OnPropertyChanged(nameof(WindowTitle));

                    // Update scrap entries to show only entries for the current file
                    _scrapManager.UpdateScrapEntriesForFile(value);
                    OnPropertyChanged(nameof(ScrapCount));
                    OnPropertyChanged(nameof(ScrapTabHeader));

                    // Sync with DialogContextService for plugin access (#227)
                    DialogContextService.Instance.CurrentFileName = value;
                    DialogContextService.Instance.CurrentFilePath = value;
                }
            }
        }

        public string LoadedFileName => CurrentFileName != null ? System.IO.Path.GetFileName(CurrentFileName) : "No file loaded";

        /// <summary>
        /// Gets the current file path (alias for CurrentFileName for external access).
        /// </summary>
        public string? CurrentFilePath => CurrentFileName;

        /// <summary>
        /// Issue #123: Gets whether the clipboard content was from a Cut operation.
        /// </summary>
        public bool ClipboardWasCut => _clipboardService.WasCutOperation;

        public string WindowTitle => CurrentFileName != null
            ? $"Parley v{VersionHelper.GetVersion()} - {System.IO.Path.GetFileName(CurrentFileName)}{(HasUnsavedChanges ? "*" : "")}"
            : $"Parley v{VersionHelper.GetVersion()}";

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set
            {
                if (SetProperty(ref _hasUnsavedChanges, value))
                {
                    // Explicitly refresh WindowTitle to ensure asterisk updates (Issue #18)
                    OnPropertyChanged(nameof(WindowTitle));

                    // Force immediate UI refresh on UI thread to prevent asterisk persistence
                    Dispatcher.UIThread.Post(() =>
                    {
                        OnPropertyChanged(nameof(WindowTitle));
                    }, DispatcherPriority.Send);

                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"HasUnsavedChanges = {value}, WindowTitle = '{WindowTitle}'");
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Last saved time display (Issue #62)
        /// </summary>
        public string LastSavedTime
        {
            get => _lastSavedTime;
            set => SetProperty(ref _lastSavedTime, value);
        }

        public ObservableCollection<string> DebugMessages
        {
            get => _debugMessages;
            set => SetProperty(ref _debugMessages, value);
        }

        public ObservableCollection<TreeViewSafeNode> DialogNodes
        {
            get => _dialogNodes;
            set => SetProperty(ref _dialogNodes, value);
        }

        /// <summary>
        /// Issue #484: Expose warning visibility setting for tree view binding
        /// </summary>
        public bool ShowDialogWarnings => SettingsService.Instance.SimulatorShowWarnings;

        public ObservableCollection<ScrapEntry> ScrapEntries => _scrapManager.ScrapEntries;

        public ScrapEntry? SelectedScrapEntry
        {
            get => _selectedScrapEntry;
            set
            {
                if (SetProperty(ref _selectedScrapEntry, value))
                {
                    OnPropertyChanged(nameof(CanRestoreFromScrap));
                }
            }
        }

        public TreeViewSafeNode? SelectedTreeNode
        {
            get => _selectedTreeNode;
            set
            {
                if (SetProperty(ref _selectedTreeNode, value))
                {
                    OnPropertyChanged(nameof(CanRestoreFromScrap));
                    OnPropertyChanged(nameof(HasNodeSelected)); // Issue #3
                }
            }
        }

        /// <summary>
        /// True if a non-root node is selected (for enabling/disabling node properties panel) - Issue #3
        /// </summary>
        public bool HasNodeSelected => _selectedTreeNode != null && !(_selectedTreeNode is TreeViewRootNode);

        public bool CanRestoreFromScrap
        {
            get
            {
                // Basic requirements
                if (SelectedScrapEntry == null || SelectedTreeNode == null || CurrentDialog == null)
                    return false;

                // Get the node from scrap to check its type
                var node = _scrapManager.GetNodeFromScrap(SelectedScrapEntry.Id);
                if (node == null)
                    return false;

                // Validate restoration target based on dialog structure rules
                if (SelectedTreeNode is TreeViewRootNode)
                {
                    // Only NPC Entry nodes can be restored to root
                    return node.Type == DialogNodeType.Entry;
                }

                // For non-root parents, check structure rules
                var parentNode = SelectedTreeNode.OriginalNode;
                if (parentNode == null)
                    return false;

                // NPC Entry can only be child of PC Reply (not another NPC Entry)
                if (node.Type == DialogNodeType.Entry && parentNode.Type == DialogNodeType.Entry)
                    return false;

                // PC Reply can be under NPC Entry OR NPC Reply (branching PC responses)
                // All other combinations are valid
                return true;
            }
        }

        public int ScrapCount => CurrentFileName != null ? _scrapManager.GetScrapCount(CurrentFileName) : 0;

        public string ScrapTabHeader => ScrapCount > 0 ? $"Scrap ({ScrapCount})" : "Scrap";

        public DialogNode? NodeToSelectAfterRefresh
        {
            get => _nodeToSelectAfterRefresh;
            set
            {
                _nodeToSelectAfterRefresh = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Constructor

        public MainViewModel()
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "Parley MainViewModel initialized");

            // Initialize NodeOperationsManager with required dependencies
            _nodeOpsManager = new NodeOperationsManager(_editorService, _scrapManager, _orphanManager);

            // Initialize PasteOperationsManager with required dependencies
            _pasteManager = new PasteOperationsManager(_clipboardService, _cloningService, _indexManager);

            // Hook up scrap count changed event
            _scrapManager.ScrapCountChanged += (s, count) =>
            {
                OnPropertyChanged(nameof(ScrapCount));
                OnPropertyChanged(nameof(ScrapTabHeader));
                UpdateScrapBadgeVisibility();
            };

            // Issue #484: Subscribe to settings changes to refresh tree when warning visibility changes
            SettingsService.Instance.PropertyChanged += OnSettingsPropertyChanged;
        }

        #endregion

        #region Debug Messages

        public void AddDebugMessage(string message)
        {
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    DebugMessages.Add(message);

                    // Keep only last 1000 messages in display to prevent memory issues
                    if (DebugMessages.Count > 1000)
                    {
                        DebugMessages.RemoveAt(0);
                    }
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to add debug message: {ex.Message}");
            }
        }

        public void ClearDebugMessages()
        {
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    DebugMessages.Clear();
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to clear debug messages: {ex.Message}");
            }
        }

        #endregion

        #region Settings Handler

        /// <summary>
        /// Issue #484: Handle settings changes that require UI refresh
        /// </summary>
        private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsService.SimulatorShowWarnings))
            {
                // Notify the tree view binding that ShowDialogWarnings changed
                OnPropertyChanged(nameof(ShowDialogWarnings));
            }
        }

        #endregion
    }
}

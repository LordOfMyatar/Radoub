using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Utils;
using Parley.Models;

namespace DialogEditor.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private Dialog? _currentDialog;
        private string? _currentFileName;
        private bool _isLoading;
        private string _statusMessage = "Ready";
        private string _lastSavedTime = ""; // Issue #62 - Last saved indicator
        private ObservableCollection<string> _debugMessages = new();
        private ObservableCollection<TreeViewSafeNode> _dialogNodes = new();
        private bool _hasUnsavedChanges;
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
        private ScrapEntry? _selectedScrapEntry;
        private TreeViewSafeNode? _selectedTreeNode;

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
        /// Reloads the current dialog file, useful when TLK settings change.
        /// </summary>
        public async Task ReloadCurrentDialogAsync()
        {
            if (string.IsNullOrEmpty(CurrentFileName)) return;

            var filePath = CurrentFileName;

            // Invalidate the resolver to force TLK reload with new settings
            GameResourceService.Instance.InvalidateResolver();

            await LoadDialogAsync(filePath);
        }

        /// <summary>
        /// Issue #123: Gets whether the clipboard content was from a Cut operation.
        /// </summary>
        public bool ClipboardWasCut => _clipboardService.WasCutOperation;

        public string WindowTitle => CurrentFileName != null
            ? $"Parley v{VersionHelper.FullVersion} - {System.IO.Path.GetFileName(CurrentFileName)}{(HasUnsavedChanges ? "*" : "")}"
            : $"Parley v{VersionHelper.FullVersion}";

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

        public async Task LoadDialogAsync(string filePath)
        {
            try
            {
                IsLoading = true;
                StatusMessage = $"Loading {System.IO.Path.GetFileName(filePath)}...";

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading dialog from: {UnifiedLogger.SanitizePath(filePath)}");

                // Ensure GameResourceService is initialized before parsing (for TLK StrRef resolution)
                _ = GameResourceService.Instance.IsAvailable;

                // Phase 4 Refactoring: Use DialogFileService facade instead of DialogParser directly
                var dialogService = new DialogFileService();
                CurrentDialog = await dialogService.LoadFromFileAsync(filePath);

                if (CurrentDialog != null)
                {
                    // Rebuild LinkRegistry for the loaded dialog
                    CurrentDialog.RebuildLinkRegistry();

                    // Reset global tracking for link detection when loading new dialog
                    TreeViewSafeNode.ResetGlobalTracking();

                    // Clear undo history when loading new file
                    _undoRedoService.Clear();

                    // Clear tree selection when loading new file
                    SelectedTreeNode = null;

                    CurrentFileName = filePath;
                    HasUnsavedChanges = false; // Clear dirty flag when loading
                    LastSavedTime = ""; // Clear last saved time on load
                    StatusMessage = $"Dialog loaded successfully: {CurrentDialog.Entries.Count} entries, {CurrentDialog.Replies.Count} replies";

                    // Add to recent files
                    SettingsService.Instance.AddRecentFile(filePath);

                    // Populate the dialog nodes for the tree view (must run on UI thread)
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        PopulateDialogNodes();
                    });

                    // Update scrap entries for the newly loaded file
                    UpdateScrapForCurrentFile();

                    // Validate the loaded dialog
                    var validation = dialogService.ValidateStructure(CurrentDialog);
                    if (validation.Warnings.Count > 0)
                    {
                        foreach (var warning in validation.Warnings)
                        {
                            UnifiedLogger.LogApplication(LogLevel.WARN, $"Dialog validation: {warning}");
                        }
                    }
                }
                else
                {
                    StatusMessage = "Failed to load dialog file";
                    DialogNodes.Clear();
                }

                UnifiedLogger.LogApplication(LogLevel.INFO, "Dialog loading completed");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading dialog: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load dialog: {ex.Message}");
                CurrentDialog = null;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Saves dialog to file. Returns true if successful, false otherwise.
        /// Issue #8: Now returns bool so callers can check result.
        /// </summary>
        public async Task<bool> SaveDialogAsync(string filePath)
        {
            if (CurrentDialog == null)
            {
                StatusMessage = "No dialog to save";
                return false;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Saving {System.IO.Path.GetFileName(filePath)}...";

                // Use DialogSaveService for all save logic
                var result = await _saveService.SaveDialogAsync(CurrentDialog, filePath);

                if (result.Success)
                {
                    CurrentFileName = filePath;
                    HasUnsavedChanges = false; // Clear dirty flag on successful save

                    // Update last saved time (Issue #62)
                    LastSavedTime = $"Last saved: {DateTime.Now:h:mm:ss tt}";

                    StatusMessage = result.StatusMessage;
                    return true;
                }
                else
                {
                    StatusMessage = result.StatusMessage;
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving dialog: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save dialog in MainViewModel: {ex.Message}");
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void PopulateDialogNodes(bool skipAutoSelect = false)
        {
            try
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🎯 ENTERING PopulateDialogNodes method (skipAutoSelect={skipAutoSelect})");

                // Create NEW collection instead of clearing to force UI refresh
                var newNodes = new ObservableCollection<TreeViewSafeNode>();
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "🎯 Created new DialogNodes collection");

                if (CurrentDialog == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "🎯 CurrentDialog is null, returning");
                    return;
                }

                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🎯 CurrentDialog has {CurrentDialog.Entries.Count} entries, {CurrentDialog.Starts.Count} starts");

                // 🔍 DEBUG: Detailed starts analysis
                UnifiedLogger.LogApplication(LogLevel.INFO, $"🔍 TREE BUILDING: Starts.Count={CurrentDialog.Starts.Count}");
                for (int i = 0; i < CurrentDialog.Starts.Count; i++)
                {
                    var start = CurrentDialog.Starts[i];
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"🔍 TREE Start[{i}]: Index={start.Index}, Node={start.Node?.Text?.GetDefault() ?? "null"}");
                }

                // First, link all pointer references to actual nodes
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "🎯 About to call LinkDialogPointers");
                LinkDialogPointers();
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "🎯 LinkDialogPointers completed");

                // Create ROOT node at top (matches GFF editor)
                var rootNode = new TreeViewRootNode(CurrentDialog);

                // Add starting entries to root's children using TreeViewSafeNode
                // CRITICAL: Pass the start DialogPtr as sourcePointer so conditional scripts work
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🎯 About to iterate through {CurrentDialog.Starts.Count} start entries");

                // Issue #484: Pre-calculate unreachable sibling warnings for root entries
                var unreachableRootIndices = TreeViewSafeNode.CalculateUnreachableSiblings(CurrentDialog.Starts);

                int startIndex = 0;
                foreach (var start in CurrentDialog.Starts)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🎯 Processing start with Index={start.Index}");
                    if (start.Index < CurrentDialog.Entries.Count)
                    {
                        var entry = CurrentDialog.Entries[(int)start.Index];
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🎯 Got entry: '{entry.DisplayText}' - about to create TreeViewSafeNode with SourcePointer");

                        // Issue #484: Check if this root entry is unreachable
                        bool isUnreachable = unreachableRootIndices.Contains(startIndex);

                        // Pass the start DialogPtr as the source pointer so conditional scripts and parameters work
                        var safeNode = new TreeViewSafeNode(entry, ancestors: null, depth: 0, sourcePointer: start, isUnreachableSibling: isUnreachable);
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🎯 Created TreeViewSafeNode with DisplayText: '{safeNode.DisplayText}', SourcePointer: {start != null}");
                        rootNode.Children?.Add(safeNode);
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added to Root: '{entry.DisplayText}'");
                    }
                    else
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"Start pointer index {start.Index} exceeds entry count {CurrentDialog.Entries.Count}");
                    }
                    startIndex++;
                }

                // If no starts found, show all entries under root
                if (rootNode.Children?.Count == 0 && CurrentDialog.Entries.Count > 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"No start nodes found, showing all {CurrentDialog.Entries.Count} entries under root");
                    foreach (var entry in CurrentDialog.Entries)
                    {
                        var safeNode = new TreeViewSafeNode(entry);
                        rootNode.Children.Add(safeNode);
                    }
                }

                // Add root to tree
                newNodes.Add(rootNode);
                rootNode.IsExpanded = true; // Auto-expand root

                // Check if we need to select a specific node after refresh (e.g., after Ctrl+D)
                if (NodeToSelectAfterRefresh != null)
                {
                    var nodeToFind = NodeToSelectAfterRefresh;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"🎯 Looking for node to select: '{nodeToFind.DisplayText}' (Type: {nodeToFind.Type})");

                    // Clear pending selection first to avoid infinite loops
                    NodeToSelectAfterRefresh = null;

                    // CRITICAL: Defer selection until AFTER TreeView has processed the new ItemsSource
                    // Setting SelectedTreeNode immediately can fail because the binding hasn't propagated yet
                    var capturedRootNode = rootNode;
                    var capturedNodeToFind = nodeToFind;

                    Dispatcher.UIThread.Post(() =>
                    {
                        var targetNode = FindTreeViewNode(capturedRootNode, capturedNodeToFind);
                        if (targetNode != null)
                        {
                            SelectedTreeNode = targetNode;
                            UnifiedLogger.LogApplication(LogLevel.INFO,
                                $"✅ Selected node after refresh: '{targetNode.DisplayText}'");
                        }
                        else
                        {
                            SelectedTreeNode = capturedRootNode;
                            UnifiedLogger.LogApplication(LogLevel.WARN,
                                $"❌ Target node '{capturedNodeToFind.DisplayText}' not found, selected ROOT");
                        }
                    }, global::Avalonia.Threading.DispatcherPriority.Loaded);
                }
                else if (!skipAutoSelect)
                {
                    // Auto-select ROOT node for consistent initial state
                    // This ensures Restore button logic works correctly and shows conversation settings
                    SelectedTreeNode = rootNode;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Auto-selected ROOT node");
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Skipped auto-select (undo/redo will restore selection)");
                }

                // Assign new collection to trigger UI update
                DialogNodes = newNodes;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Populated {DialogNodes.Count} root dialog nodes for tree view");

                // Explicitly notify Avalonia that DialogNodes collection has changed
                OnPropertyChanged(nameof(DialogNodes));

                // Update status to show tree was populated
                StatusMessage = $"Loaded {DialogNodes.Count} dialog node(s) into tree view";
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to populate dialog nodes: {ex.Message}");
            }
        }

        private void LinkDialogPointers()
        {
            if (CurrentDialog == null) return;

            // Link all starting list pointers to their target entry nodes
            foreach (var start in CurrentDialog.Starts)
            {
                if (start.Index < CurrentDialog.Entries.Count)
                {
                    start.Node = CurrentDialog.Entries[(int)start.Index];
                    start.Type = DialogNodeType.Entry;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Linked start pointer to entry {start.Index}");
                }
            }

            // Link all entry pointers to their target reply nodes
            foreach (var entry in CurrentDialog.Entries)
            {
                foreach (var pointer in entry.Pointers)
                {
                    if (pointer.Index < CurrentDialog.Replies.Count)
                    {
                        pointer.Node = CurrentDialog.Replies[(int)pointer.Index];
                        pointer.Type = DialogNodeType.Reply;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Linked entry pointer to reply {pointer.Index}");
                    }
                }
            }

            // Link all reply pointers to their target entry nodes
            foreach (var reply in CurrentDialog.Replies)
            {
                foreach (var pointer in reply.Pointers)
                {
                    if (pointer.Index < CurrentDialog.Entries.Count)
                    {
                        pointer.Node = CurrentDialog.Entries[(int)pointer.Index];
                        pointer.Type = DialogNodeType.Entry;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🔗 Linked reply pointer: Index={pointer.Index}, IsLink={pointer.IsLink}, Entry='{pointer.Node.Text?.GetDefault() ?? "empty"}'");
                    }
                }
            }

            // NO MODIFICATIONS TO ORIGINAL DIALOG - preserve for export integrity
            // Avalonia circular reference handling must be done at display layer only
        }

        private void ApplyIntelligentLoopBreaking()
        {
            if (CurrentDialog == null) return;

            // Find and break ONLY circular loops while preserving full conversation depth
            var processedPaths = new HashSet<string>();
            var currentPath = new List<DialogNode>();

            foreach (var start in CurrentDialog.Starts)
            {
                if (start.Node != null && start.Index < CurrentDialog.Entries.Count)
                {
                    var startNode = CurrentDialog.Entries[(int)start.Index];
                    currentPath.Clear();
                    BreakCircularLoopsOnly(startNode, currentPath, processedPaths);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🔄 Processed start entry: '{startNode.DisplayText}'");
                }
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"🛡️ SMART LOOP BREAKING: Broke circular references while preserving conversation depth");
        }

        private void BreakCircularLoopsOnly(DialogNode node, List<DialogNode> currentPath, HashSet<string> processedPaths)
        {
            // Check if this node is already in our current path (circular loop detected)
            if (currentPath.Contains(node))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"🔄 CIRCULAR LOOP DETECTED: Breaking loop at '{node.DisplayText}' - depth {currentPath.Count}");
                return; // Don't process this node again in this path
            }

            // Add this node to current path
            currentPath.Add(node);

            // Create a unique path signature to avoid reprocessing the same conversation branch
            var pathSignature = string.Join("→", currentPath.Select(n => $"{n.Type}:{n.DisplayText?.Take(20)}"));
            if (processedPaths.Contains(pathSignature))
            {
                currentPath.RemoveAt(currentPath.Count - 1);
                return; // Already processed this exact conversation path
            }
            processedPaths.Add(pathSignature);

            // Process all child pointers - but create a copy to avoid modification issues
            var pointersToProcess = node.Pointers.ToList();
            for (int i = 0; i < pointersToProcess.Count; i++)
            {
                var pointer = pointersToProcess[i];
                if (pointer.Node != null)
                {
                    // Check if this would create a circular loop
                    if (currentPath.Contains(pointer.Node))
                    {
                        // Remove this specific pointer to break the loop
                        node.Pointers.Remove(pointer);
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"🚫 REMOVED circular pointer from '{node.DisplayText}' to '{pointer.Node.DisplayText}' to prevent Avalonia crash");
                    }
                    else
                    {
                        // Safe to process - no circular loop
                        BreakCircularLoopsOnly(pointer.Node, currentPath, processedPaths);
                    }
                }
            }

            // Remove this node from current path when backtracking
            currentPath.RemoveAt(currentPath.Count - 1);
        }

        // REMOVED: ApplyNonDestructiveTreeSafety, CreateTreeSafeNode, ApplyDepthLimitToNode
        // These methods were part of early circular reference handling attempts.
        // Replaced by TreeViewSafeNode wrapper approach which provides better protection
        // without modifying original dialog data (Oct 2025)

        public void NewDialog()
        {
            try
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "Creating new blank dialog");

                // Create blank dialog with root structure
                CurrentDialog = new Dialog();
                CurrentFileName = null; // No filename until user saves (this will also clear scrap via setter)
                HasUnsavedChanges = false; // Start clean
                LastSavedTime = ""; // Clear last saved time
                SelectedTreeNode = null; // Clear selection
                SelectedScrapEntry = null; // Clear scrap selection

                // Populate empty tree with just ROOT node
                Dispatcher.UIThread.Post(() =>
                {
                    PopulateDialogNodes();
                });

                StatusMessage = "New blank dialog created";
                UnifiedLogger.LogApplication(LogLevel.INFO, "New blank dialog created successfully");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating new dialog: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to create new dialog: {ex.Message}");
                CurrentDialog = null;
            }
        }

        public void CloseDialog()
        {
            CurrentDialog = null;
            CurrentFileName = null;
            HasUnsavedChanges = false;
            LastSavedTime = ""; // Clear last saved time
            DialogNodes.Clear();
            SelectedScrapEntry = null; // Clear scrap selection
            StatusMessage = "Dialog closed";
            UnifiedLogger.LogApplication(LogLevel.INFO, "Dialog file closed");
        }

        // Node creation methods - Phase 1 Step 4
        // Refactored in #344 to use AddNodeWithUndoAndRefresh template method

        /// <summary>
        /// Template method for node creation operations.
        /// Handles undo state, node creation, tree refresh, and status update.
        /// Reduces duplication across AddSmartNode, AddEntryNode, AddPCReplyNode (#344).
        /// </summary>
        private DialogNode AddNodeWithUndoAndRefresh(
            string undoDescription,
            TreeViewSafeNode? selectedNode,
            Func<DialogNode?, DialogPtr?, DialogNode> createNode,
            string successMessage)
        {
            if (CurrentDialog == null)
                throw new InvalidOperationException("No dialog loaded");

            // Save undo state
            SaveUndoState(undoDescription);

            // Extract parent node and pointer from TreeViewSafeNode
            DialogNode? parentNode = null;
            DialogPtr? parentPtr = null;

            if (selectedNode != null && !(selectedNode is TreeViewRootNode))
            {
                parentNode = selectedNode.OriginalNode;
                // Expand parent in tree view
                selectedNode.IsExpanded = true;
            }

            // Create node via delegate
            var newNode = createNode(parentNode, parentPtr);

            // Focus on the newly created node after tree refresh
            NodeToSelectAfterRefresh = newNode;

            // Refresh the tree
            RefreshTreeView();

            // Update UI state
            HasUnsavedChanges = true;
            StatusMessage = successMessage;

            return newNode;
        }

        /// <summary>
        /// Saves current state to undo stack before making changes.
        /// Issue #74: Made public to allow view to save state before property edits.
        /// Issue #252: Now also saves tree UI state (selection, expansion) for proper restoration
        /// </summary>
        public void SaveUndoState(string description)
        {
            if (CurrentDialog != null && !_undoRedoService.IsRestoring)
            {
                // Issue #252: Capture current tree state to restore on undo
                var treeState = CaptureTreeState();
                _undoRedoService.SaveState(CurrentDialog, description, treeState);
            }
        }

        public void Undo()
        {
            if (CurrentDialog == null || !_undoRedoService.CanUndo)
            {
                StatusMessage = "Nothing to undo";
                return;
            }

            // Issue #252: Capture current tree state (will be saved to redo stack)
            var currentTreeState = CaptureTreeState();

            var previousState = _undoRedoService.Undo(CurrentDialog, currentTreeState);
            if (previousState.Success && previousState.RestoredDialog != null)
            {
                CurrentDialog = previousState.RestoredDialog;
                // CRITICAL: Rebuild LinkRegistry after undo to fix Issue #28 (IsLink corruption)
                CurrentDialog.RebuildLinkRegistry();

                // Issue #356: Remove scrap entries for nodes that were restored by undo
                if (!string.IsNullOrEmpty(CurrentFileName))
                {
                    _scrapManager.RemoveRestoredNodes(CurrentFileName, CurrentDialog);
                    OnPropertyChanged(nameof(ScrapCount));
                    OnPropertyChanged(nameof(ScrapTabHeader));
                }

                // CRITICAL FIX: Extend IsRestoring to cover async tree rebuild.
                // Without this, tree restoration triggers SaveUndoState causing infinite loop.
                _undoRedoService.SetRestoring(true);

                // Issue #252: Use the tree state that was SAVED with the undo state
                // This restores selection to what it was BEFORE the action that was undone
                var savedTreeState = previousState.TreeState;

                // CRITICAL FIX (Issue #28): Don't use RefreshTreeView - it tries to restore
                // expansion state using old node references that don't exist after undo.
                // Instead, rebuild tree without expansion logic, then restore using paths.
                Dispatcher.UIThread.Post(() =>
                {
                    PopulateDialogNodes(skipAutoSelect: true);

                    // Restore expansion and selection using the SAVED tree state
                    Dispatcher.UIThread.Post(() =>
                    {
                        RestoreTreeState(savedTreeState);
                        // Clear restoring flag after tree state is fully restored
                        _undoRedoService.SetRestoring(false);
                    }, global::Avalonia.Threading.DispatcherPriority.Loaded);
                });

                HasUnsavedChanges = true;
            }

            StatusMessage = previousState.StatusMessage;
        }

        public void Redo()
        {
            if (CurrentDialog == null || !_undoRedoService.CanRedo)
            {
                StatusMessage = "Nothing to redo";
                return;
            }

            // Capture current dialog state BEFORE redo to detect deleted nodes (#370)
            var dialogBeforeRedo = CurrentDialog;

            // Capture current tree state to pass to redo (will be saved on undo stack)
            var currentTreeState = CaptureTreeState();

            var nextState = _undoRedoService.Redo(CurrentDialog, currentTreeState);
            if (nextState.Success && nextState.RestoredDialog != null)
            {
                CurrentDialog = nextState.RestoredDialog;
                // CRITICAL: Rebuild LinkRegistry after redo to fix Issue #28 (IsLink corruption)
                CurrentDialog.RebuildLinkRegistry();

                // Issue #370: Re-add nodes to scrap that were deleted by redo
                if (!string.IsNullOrEmpty(CurrentFileName))
                {
                    _scrapManager.RestoreDeletedNodesToScrap(CurrentFileName, dialogBeforeRedo, CurrentDialog);
                    OnPropertyChanged(nameof(ScrapCount));
                    OnPropertyChanged(nameof(ScrapTabHeader));
                }

                // Issue #252: Use the tree state that was saved WITH the redo state
                // This restores selection/expansion to what it was AFTER the original action
                var savedTreeState = nextState.TreeState;

                // CRITICAL FIX: Extend IsRestoring to cover async tree rebuild.
                // Without this, tree restoration triggers SaveUndoState causing infinite loop.
                _undoRedoService.SetRestoring(true);

                // CRITICAL FIX (Issue #28): Don't use RefreshTreeView - it tries to restore
                // expansion state using old node references that don't exist after redo.
                // Instead, rebuild tree without expansion logic, then restore using paths.
                Dispatcher.UIThread.Post(() =>
                {
                    PopulateDialogNodes(skipAutoSelect: true);

                    // Issue #252: Restore expansion and selection from the SAVED tree state
                    Dispatcher.UIThread.Post(() =>
                    {
                        RestoreTreeState(savedTreeState);
                        // Clear restoring flag after tree state is fully restored
                        _undoRedoService.SetRestoring(false);
                    }, global::Avalonia.Threading.DispatcherPriority.Loaded);
                });

                HasUnsavedChanges = true;
            }

            StatusMessage = nextState.StatusMessage;
        }

        public bool CanUndo => _undoRedoService.CanUndo;
        public bool CanRedo => _undoRedoService.CanRedo;

        public void AddSmartNode(TreeViewSafeNode? selectedNode = null)
        {
            if (CurrentDialog == null) return;

            // Use template method to reduce duplication (#344)
            var newNode = AddNodeWithUndoAndRefresh(
                "Add Smart Node",
                selectedNode,
                (parent, ptr) => _nodeOpsManager.AddSmartNode(CurrentDialog!, parent, ptr),
                ""); // Status message set below based on node type

            StatusMessage = $"Added new {newNode.Type} node";
        }

        public void AddEntryNode(TreeViewSafeNode? parentNode = null)
        {
            if (CurrentDialog == null) return;

            // Determine status message based on parent
            bool isRoot = parentNode == null || parentNode is TreeViewRootNode;
            string statusMsg = isRoot
                ? "Added new Entry node at root level"
                : "Added new Entry node after Reply";

            // Use template method to reduce duplication (#344)
            AddNodeWithUndoAndRefresh(
                "Add Entry Node",
                parentNode,
                (parent, ptr) => _nodeOpsManager.AddEntryNode(CurrentDialog!, parent, ptr),
                statusMsg);
        }

        // Phase 1 Bug Fix: Removed AddNPCReplyNode - "NPC Reply" is actually Entry node after PC Reply

        public void AddPCReplyNode(TreeViewSafeNode parent)
        {
            if (CurrentDialog == null || parent == null) return;
            if (parent.OriginalNode == null) return;

            // Use template method to reduce duplication (#344)
            AddNodeWithUndoAndRefresh(
                "Add PC Reply",
                parent,
                (parentNode, ptr) => _nodeOpsManager.AddPCReplyNode(CurrentDialog!, parentNode!, ptr),
                "Added new PC Reply node");
        }

        public void DeleteNode(TreeViewSafeNode nodeToDelete)
        {
            if (CurrentDialog == null) return;

            // CRITICAL: Block ROOT deletion - ROOT cannot be deleted
            if (nodeToDelete is TreeViewRootNode)
            {
                StatusMessage = "Cannot delete ROOT node";
                UnifiedLogger.LogApplication(LogLevel.WARN, "Blocked attempt to delete ROOT node");
                return;
            }

            try
            {
                var node = nodeToDelete.OriginalNode;

                // Save state for undo before deleting
                SaveUndoState("Delete Node");

                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"DeleteNode: Starting delete of '{node.DisplayText}'");

                // Delegate to NodeOperationsManager
                var linkedNodes = _nodeOpsManager.DeleteNode(CurrentDialog, node, CurrentFileName);

                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"DeleteNode: NodeOperationsManager.DeleteNode completed");

                // Display warnings if there were linked nodes
                if (linkedNodes.Count > 0)
                {
                    // Check for duplicates in the linked nodes list (indicates copy/paste created duplicates)
                    var grouped = linkedNodes.GroupBy(n => n.DisplayText);
                    var hasDuplicates = grouped.Any(g => g.Count() > 1);

                    if (hasDuplicates)
                    {
                        StatusMessage = $"ERROR: Duplicate nodes detected! This may cause orphaning. See logs.";
                    }
                    else
                    {
                        StatusMessage = $"Warning: Deleted node broke {linkedNodes.Count} link(s). Check logs for details.";
                    }
                }
                else
                {
                    StatusMessage = $"Node and children deleted successfully";
                }

                UnifiedLogger.LogApplication(LogLevel.DEBUG, "DeleteNode: About to refresh tree");

                // Refresh tree
                RefreshTreeView();

                UnifiedLogger.LogApplication(LogLevel.DEBUG, "DeleteNode: Tree refresh completed");

                HasUnsavedChanges = true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"DeleteNode EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"DeleteNode stack trace: {ex.StackTrace}");
                StatusMessage = $"Error deleting node: {ex.Message}";
                throw; // Re-throw so we can see it in debug
            }
        }

        // COMPATIBILITY: Kept for existing tests that use reflection to access this method
        // TODO: Update tests to use public DeleteNode API instead
        #pragma warning disable IDE0051 // Remove unused private members
        private void DeleteNodeRecursive(DialogNode node)
        {
            if (CurrentDialog == null) return;

            // Delegate to NodeOperationsManager's internal implementation via reflection
            var managerType = _nodeOpsManager.GetType();
            var deleteMethod = managerType.GetMethod("DeleteNodeRecursive",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            deleteMethod?.Invoke(_nodeOpsManager, new object[] { CurrentDialog, node });
        }
        #pragma warning restore IDE0051

        // Phase 2a: Node Reordering
        public void MoveNodeUp(TreeViewSafeNode nodeToMove)
        {
            if (CurrentDialog == null) return;
            if (nodeToMove == null || nodeToMove is TreeViewRootNode) return;

            var node = nodeToMove.OriginalNode;

            // Save state for undo
            SaveUndoState("Move Node Up");

            // Delegate to NodeOperationsManager
            bool moved = _nodeOpsManager.MoveNodeUp(CurrentDialog, node, out string statusMessage);

            StatusMessage = statusMessage;

            if (moved)
            {
                HasUnsavedChanges = true;
                RefreshTreeViewAndSelectNode(node);
            }
        }

        public void MoveNodeDown(TreeViewSafeNode nodeToMove)
        {
            if (CurrentDialog == null) return;
            if (nodeToMove == null || nodeToMove is TreeViewRootNode) return;

            var node = nodeToMove.OriginalNode;

            // Save state for undo
            SaveUndoState("Move Node Down");

            // Delegate to NodeOperationsManager
            bool moved = _nodeOpsManager.MoveNodeDown(CurrentDialog, node, out string statusMessage);

            StatusMessage = statusMessage;

            if (moved)
            {
                HasUnsavedChanges = true;
                RefreshTreeViewAndSelectNode(node);
            }
        }

        /// <summary>
        /// Moves a node to a new position via drag-and-drop.
        /// Supports both reordering (within same parent) and reparenting (to different parent).
        /// </summary>
        /// <param name="nodeToMove">The TreeViewSafeNode being dragged.</param>
        /// <param name="newParent">The new parent node (null for root level).</param>
        /// <param name="insertIndex">Index to insert at in the new parent's children.</param>
        public void MoveNodeToPosition(TreeViewSafeNode nodeToMove, DialogNode? newParent, int insertIndex)
        {
            if (CurrentDialog == null) return;
            if (nodeToMove == null || nodeToMove is TreeViewRootNode) return;

            var node = nodeToMove.OriginalNode;
            var sourcePointer = nodeToMove.SourcePointer;

            // Save state for undo
            SaveUndoState("Move Node");

            // Delegate to NodeOperationsManager - pass sourcePointer to identify correct parent-child relationship
            bool moved = _nodeOpsManager.MoveNodeToPosition(CurrentDialog, node, sourcePointer, newParent, insertIndex, out string statusMessage);

            StatusMessage = statusMessage;

            if (moved)
            {
                HasUnsavedChanges = true;
                RefreshTreeViewAndSelectNode(node);
            }
        }

        /// <summary>
        /// Find a sibling node to focus after cutting/deleting a node.
        /// Returns previous sibling if available, otherwise next sibling, otherwise parent.
        /// </summary>
        private DialogNode? FindSiblingForFocus(DialogNode node)
        {
            if (CurrentDialog == null) return null;

            // Delegate to NodeOperationsManager
            return _nodeOpsManager.FindSiblingForFocus(CurrentDialog, node);
        }

        // DELETED: 195 lines of index management code (2025-11-19)
        // Moved to IndexManager service for better separation of concerns.
        // Methods removed: PerformMove, UpdatePointersForMove, ValidateMoveIntegrity, RecalculatePointerIndices
        // MainViewModel now uses _indexManager service for all index operations.

        /// <summary>
        /// Public method to refresh tree view (called when theme changes)
        /// </summary>
        public void RefreshTreeViewColors()
        {
            RefreshTreeView();
        }

        /// <summary>
        /// Public method to refresh tree view and restore selection to specific node (Issue #134)
        /// </summary>
        public void RefreshTreeViewColors(DialogNode nodeToSelect)
        {
            RefreshTreeViewAndSelectNode(nodeToSelect);
        }

        private void RefreshTreeView()
        {
            // Log dialog state before refresh
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"🔄 RefreshTreeView: Dialog has {CurrentDialog?.Entries.Count ?? 0} entries, " +
                $"{CurrentDialog?.Replies.Count ?? 0} replies, {CurrentDialog?.Starts.Count ?? 0} starts");

            // Save expansion state before refresh
            var expandedNodeRefs = _treeNavManager.SaveTreeExpansionState(DialogNodes);

            // Re-populate tree to reflect changes
            // CRITICAL: Run synchronously to ensure orphan removal is reflected immediately
            PopulateDialogNodes();

            // Log tree state after refresh
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"🔄 RefreshTreeView complete: DialogNodes has {DialogNodes.Count} root nodes");

            // Restore expansion state after tree is rebuilt
            // Use Dispatcher for expansion restore to ensure tree is fully rendered
            Dispatcher.UIThread.Post(() =>
            {
                _treeNavManager.RestoreTreeExpansionState(DialogNodes, expandedNodeRefs);

                // Notify subscribers that the dialog structure was refreshed
                // This allows FlowView and other components to update automatically
                DialogChangeEventBus.Instance.PublishDialogRefreshed("RefreshTreeView");
            }, global::Avalonia.Threading.DispatcherPriority.Loaded);
        }

        private void RefreshTreeViewAndSelectNode(DialogNode nodeToSelect)
        {
            // Save expansion state before refresh
            var expandedNodeRefs = _treeNavManager.SaveTreeExpansionState(DialogNodes);

            // Store the node to re-select after refresh
            NodeToSelectAfterRefresh = nodeToSelect;

            // Re-populate tree to reflect changes
            Dispatcher.UIThread.Post(() =>
            {
                PopulateDialogNodes();

                // Restore expansion state after tree is rebuilt
                Dispatcher.UIThread.Post(() =>
                {
                    _treeNavManager.RestoreTreeExpansionState(DialogNodes, expandedNodeRefs);

                    // Notify subscribers that the dialog structure was refreshed
                    DialogChangeEventBus.Instance.PublishDialogRefreshed("RefreshTreeViewAndSelectNode");
                }, global::Avalonia.Threading.DispatcherPriority.Loaded);
            });
        }

        // Node to re-select after tree refresh (used by View to restore selection)
        private DialogNode? _nodeToSelectAfterRefresh;
        public DialogNode? NodeToSelectAfterRefresh
        {
            get => _nodeToSelectAfterRefresh;
            set
            {
                _nodeToSelectAfterRefresh = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Recursively finds a TreeViewSafeNode that wraps the target DialogNode.
        /// Used to select the correct node after tree refresh (e.g., after Ctrl+D).
        /// Only expands nodes that are on the path to the target (not all searched nodes).
        /// Uses depth limit to avoid infinite recursion.
        /// </summary>
        private TreeViewSafeNode? FindTreeViewNode(TreeViewSafeNode parent, DialogNode target, int maxDepth = 10)
        {
            if (maxDepth <= 0)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🔍 FindTreeViewNode: Max depth reached");
                return null;
            }

            // Force populate children for searching (lazy loading requires this)
            // Access Children property to initialize, then call PopulateChildren
            var _ = parent.Children; // Initialize _children if null
            parent.PopulateChildren();

            int childCount = parent.Children?.Count(c => !(c is TreeViewPlaceholderNode)) ?? 0;
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"🔍 FindTreeViewNode: Searching in '{parent.DisplayText}' ({childCount} children)");

            // Check children
            if (parent.Children != null)
            {
                foreach (var child in parent.Children)
                {
                    // Skip placeholder nodes
                    if (child is TreeViewPlaceholderNode) continue;

                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"🔍 Checking child: '{child.DisplayText}' (match: {child.OriginalNode == target})");

                    if (child.OriginalNode == target)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🔍 FOUND target!");
                        // Expand parent since we found the target in this subtree
                        parent.IsExpanded = true;
                        return child;
                    }

                    // Recurse into children that have pointers (may lead to target)
                    // The depth limit protects against infinite recursion
                    int pointerCount = child.OriginalNode.Pointers.Count;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"🔍 Child '{child.DisplayText}' has {pointerCount} pointers");

                    if (pointerCount > 0)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"🔍 Recursing into '{child.DisplayText}'...");
                        var found = FindTreeViewNode(child, target, maxDepth - 1);
                        if (found != null)
                        {
                            // Expand parent since target was found in this subtree
                            parent.IsExpanded = true;
                            return found;
                        }
                    }
                }
            }
            return null;
        }

        public TreeViewSafeNode? FindTreeNodeForDialogNode(DialogNode nodeToFind)
        {
            return _treeNavManager.FindTreeNodeForDialogNode(DialogNodes, nodeToFind);
        }


        public string CaptureTreeStructure()
        {
            if (CurrentDialog == null)
                return "No dialog loaded";

            return _treeNavManager.CaptureTreeStructure(CurrentDialog);
        }


        public async Task<string> PerformRoundTripTestAsync(bool closeAppAfterTest = false)
        {
            if (CurrentDialog == null)
                return "No dialog loaded for round-trip test";

            try
            {
                var originalFileName = CurrentFileName;
                var testResults = new System.Text.StringBuilder();
                testResults.AppendLine("=== Round-Trip Test Results ===");
                testResults.AppendLine($"Original file: {System.IO.Path.GetFileName(originalFileName)}");

                // Capture original structure
                var originalStructure = CaptureTreeStructure();
                var originalEntryCount = CurrentDialog.Entries.Count;
                var originalReplyCount = CurrentDialog.Replies.Count;
                var originalStartCount = CurrentDialog.Starts.Count;

                // Export to temporary file
                if (string.IsNullOrEmpty(originalFileName))
                    return "No original file name available for round-trip test";

                var tempPath = System.IO.Path.ChangeExtension(originalFileName, ".temp.dlg");
                await SaveDialogAsync(tempPath);

                // Reload and compare
                await LoadDialogAsync(tempPath);
                var reloadedStructure = CaptureTreeStructure();
                var reloadedEntryCount = CurrentDialog?.Entries.Count ?? 0;
                var reloadedReplyCount = CurrentDialog?.Replies.Count ?? 0;
                var reloadedStartCount = CurrentDialog?.Starts.Count ?? 0;

                // Compare counts
                testResults.AppendLine($"Entry count: {originalEntryCount} -> {reloadedEntryCount} {(originalEntryCount == reloadedEntryCount ? "✓" : "✗")}");
                testResults.AppendLine($"Reply count: {originalReplyCount} -> {reloadedReplyCount} {(originalReplyCount == reloadedReplyCount ? "✓" : "✗")}");
                testResults.AppendLine($"Start count: {originalStartCount} -> {reloadedStartCount} {(originalStartCount == reloadedStartCount ? "✓" : "✗")}");

                // Compare structures
                var structureMatch = originalStructure.Equals(reloadedStructure);
                testResults.AppendLine($"Structure match: {(structureMatch ? "✓" : "✗")}");

                if (!structureMatch)
                {
                    testResults.AppendLine();
                    testResults.AppendLine("=== Original Structure ===");
                    testResults.AppendLine(originalStructure);
                    testResults.AppendLine();
                    testResults.AppendLine("=== Reloaded Structure ===");
                    testResults.AppendLine(reloadedStructure);
                }

                // Cleanup
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }

                // Reload original
                if (!string.IsNullOrEmpty(originalFileName))
                {
                    await LoadDialogAsync(originalFileName);
                }

                return testResults.ToString();
            }
            catch (Exception ex)
            {
                return $"Round-trip test failed: {ex.Message}";
            }
        }

        // Phase 1 Step 7: Copy/Paste/Cut System
        public void CopyNode(TreeViewSafeNode? nodeToCopy)
        {
            if (nodeToCopy == null || nodeToCopy is TreeViewRootNode)
            {
                StatusMessage = "Cannot copy ROOT node";
                return;
            }

            if (CurrentDialog == null) return;

            var node = nodeToCopy.OriginalNode;
            var sourcePointer = nodeToCopy.SourcePointer;
            _clipboardService.CopyNode(node, CurrentDialog, sourcePointer);

            StatusMessage = $"Node copied: {node.DisplayText}";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Copied node: {node.DisplayText}");
        }

        public void CutNode(TreeViewSafeNode? nodeToCut)
        {
            if (CurrentDialog == null) return;
            if (nodeToCut == null || nodeToCut is TreeViewRootNode)
            {
                StatusMessage = "Cannot cut ROOT node";
                return;
            }

            var node = nodeToCut.OriginalNode;
            var sourcePointer = nodeToCut.SourcePointer;

            // Find sibling to focus BEFORE cutting
            var siblingToFocus = FindSiblingForFocus(node);

            // Save state for undo before cutting
            SaveUndoState("Cut Node");

            // Store node for pasting in clipboard service (include source pointer for scripts)
            _clipboardService.CutNode(node, CurrentDialog, sourcePointer);

            // CRITICAL: Check for other references BEFORE detaching
            // We need to count while the current reference is still there
            bool hasOtherReferences = _referenceManager.HasOtherReferences(CurrentDialog, node);

            // Detach from parent
            _referenceManager.DetachNodeFromParent(CurrentDialog, node);

            // If only had 1 reference (the one we just removed), remove from dialog lists
            // If had multiple references, keep it (still linked from elsewhere)
            if (!hasOtherReferences)
            {
                if (node.Type == DialogNodeType.Entry)
                {
                    CurrentDialog!.Entries.Remove(node);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Removed cut Entry from list (was only reference): {node.DisplayText}");
                }
                else
                {
                    CurrentDialog!.Replies.Remove(node);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Removed cut Reply from list (was only reference): {node.DisplayText}");
                }
                // CRITICAL: Recalculate all pointer indices after removing from list
                _indexManager.RecalculatePointerIndices(CurrentDialog);
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Kept cut node in list (has {hasOtherReferences} other references): {node.DisplayText}");
            }

            // NOTE: Do NOT add the cut node to scrap - it's stored in clipboard service for pasting
            // Cut is a move operation, not a delete operation
            // The node is intentionally detached and will be reattached on paste

            // Refresh tree and restore focus to sibling
            if (siblingToFocus != null)
            {
                RefreshTreeViewAndSelectNode(siblingToFocus);
            }
            else
            {
                RefreshTreeView();
            }

            HasUnsavedChanges = true;
            StatusMessage = $"Cut node: {node.DisplayText}";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Cut node (detached from parent): {node.DisplayText}");
        }

        public void PasteAsDuplicate(TreeViewSafeNode? parent)
        {
            if (CurrentDialog == null) return;

            // Save state for undo before pasting
            SaveUndoState("Paste Node");

            // Delegate to PasteOperationsManager
            var result = _pasteManager.PasteAsDuplicate(CurrentDialog, parent);

            // Update UI state
            StatusMessage = result.StatusMessage;

            if (result.Success)
            {
                // Issue #122: Focus on the newly pasted node instead of sibling
                if (result.PastedNode != null)
                {
                    NodeToSelectAfterRefresh = result.PastedNode;
                }
                RefreshTreeView();
                HasUnsavedChanges = true;
            }
        }

        public void PasteAsLink(TreeViewSafeNode? parent)
        {
            if (CurrentDialog == null) return;
            if (!_clipboardService.HasClipboardContent)
            {
                StatusMessage = "No node copied. Use Copy Node first.";
                return;
            }
            if (parent == null)
            {
                StatusMessage = "Select a parent node to paste link under";
                return;
            }

            // Check if pasting to ROOT
            if (parent is TreeViewRootNode)
            {
                StatusMessage = "Cannot paste as link to ROOT - use Paste as Duplicate instead";
                UnifiedLogger.LogApplication(LogLevel.WARN, "Blocked paste as link to ROOT - links not supported at ROOT level");
                return;
            }

            // Issue #11: Check type compatibility before attempting paste
            var clipboardNode = _clipboardService.ClipboardNode;
            if (clipboardNode != null && clipboardNode.Type == parent.OriginalNode.Type)
            {
                string parentType = parent.OriginalNode.Type == DialogNodeType.Entry ? "NPC Entry" : "PC Reply";
                string nodeType = clipboardNode.Type == DialogNodeType.Entry ? "NPC Entry" : "PC Reply";
                StatusMessage = $"Invalid link: Cannot link {nodeType} under {parentType} (same types not allowed)";
                return;
            }

            // Save state for undo before creating link
            SaveUndoState("Paste as Link");

            // Delegate to clipboard service
            var linkPtr = _clipboardService.PasteAsLink(CurrentDialog, parent.OriginalNode);

            if (linkPtr == null)
            {
                // Service already logged the reason (Cut operation, different dialog, node not found, etc.)
                StatusMessage = "Cannot paste as link - operation failed";
                return;
            }

            // Register the link pointer with LinkRegistry
            CurrentDialog.LinkRegistry.RegisterLink(linkPtr);

            // Issue #122: Focus on parent node (link is under parent, not standalone)
            NodeToSelectAfterRefresh = parent.OriginalNode;
            RefreshTreeView();
            HasUnsavedChanges = true;
            StatusMessage = $"Pasted link under {parent.DisplayText}: {linkPtr.Node?.DisplayText}";
        }

        // DELETED: 103 lines of node cloning code (2025-11-19)
        // Moved to NodeCloningService for better separation of concerns.
        // Methods removed: CloneNode, CloneNodeWithDepth, CloneLocString
        // MainViewModel now uses _cloningService for all cloning operations.

        // Phase 1 Step 8: Copy Operations (Clipboard)
        public string? GetNodeText(TreeViewSafeNode? node)
        {
            if (node == null || node is TreeViewRootNode)
                return null;

            return node.OriginalNode.Text?.GetDefault() ?? "";
        }

        public string? GetNodeProperties(TreeViewSafeNode? node)
        {
            if (node == null || node is TreeViewRootNode)
                return null;

            var dialogNode = node.OriginalNode;
            var sourcePointer = node.SourcePointer;
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"=== Node Properties ===");
            sb.AppendLine($"Type: {(dialogNode.Type == DialogNodeType.Entry ? "Entry (NPC)" : "Reply")}");
            sb.AppendLine($"Speaker: {dialogNode.Speaker ?? "(none)"}");
            sb.AppendLine($"Text: {dialogNode.Text?.GetDefault() ?? "(empty)"}");
            sb.AppendLine();
            sb.AppendLine($"Animation: {dialogNode.Animation}");
            sb.AppendLine($"Animation Loop: {dialogNode.AnimationLoop}");
            sb.AppendLine($"Sound: {dialogNode.Sound ?? "(none)"}");
            sb.AppendLine($"Delay: {(dialogNode.Delay == uint.MaxValue ? "(none)" : dialogNode.Delay.ToString())}");
            sb.AppendLine();
            sb.AppendLine($"Script (Appears When): {sourcePointer?.ScriptAppears ?? "(none)"}");
            if (sourcePointer?.ConditionParams != null && sourcePointer.ConditionParams.Count > 0)
            {
                sb.AppendLine($"Condition Parameters:");
                foreach (var param in sourcePointer.ConditionParams)
                {
                    sb.AppendLine($"  {param.Key} = {param.Value}");
                }
            }
            sb.AppendLine($"Script (Action): {dialogNode.ScriptAction ?? "(none)"}");
            if (dialogNode.ActionParams != null && dialogNode.ActionParams.Count > 0)
            {
                sb.AppendLine($"Action Parameters:");
                foreach (var param in dialogNode.ActionParams)
                {
                    sb.AppendLine($"  {param.Key} = {param.Value}");
                }
            }
            sb.AppendLine();
            sb.AppendLine($"Quest: {dialogNode.Quest ?? "(none)"}");
            sb.AppendLine($"Quest Entry: {(dialogNode.QuestEntry == uint.MaxValue ? "(none)" : dialogNode.QuestEntry.ToString())}");
            sb.AppendLine();
            sb.AppendLine($"Comment: {dialogNode.Comment ?? "(none)"}");
            sb.AppendLine($"Children: {dialogNode.Pointers.Count}");

            return sb.ToString();
        }

        public string? GetTreeStructure()
        {
            if (CurrentDialog == null)
                return null;

            // Issue #197: Use screenplay format for cleaner, more readable output
            return CommandLineService.GenerateScreenplay(CurrentDialog, CurrentFileName);
        }

        /// <summary>
        /// Captures the current tree expansion state and selected node
        /// </summary>
        private TreeState CaptureTreeState()
        {
            // Capture selected node path for restoration after undo/redo
            string? selectedPath = null;
            if (SelectedTreeNode != null && !(SelectedTreeNode is TreeViewRootNode))
            {
                selectedPath = _treeNavManager.GetNodePath(SelectedTreeNode);
            }

            var state = new TreeState
            {
                ExpandedNodePaths = _treeNavManager.CaptureExpandedNodePaths(DialogNodes),
                SelectedNodePath = selectedPath
            };

            return state;
        }

        /// <summary>
        /// Restores tree expansion state and selection
        /// </summary>
        private void RestoreTreeState(TreeState state)
        {
            if (state == null) return;

            UnifiedLogger.LogApplication(LogLevel.INFO, $"🔄 RestoreTreeState: DialogNodes.Count={DialogNodes.Count}, ExpandedPaths={state.ExpandedNodePaths.Count}, SelectedPath='{state.SelectedNodePath}'");

            // Get ROOT node
            var rootNode = DialogNodes.OfType<TreeViewRootNode>().FirstOrDefault();

            // Issue #252: Always ensure ROOT is expanded - it should never be collapsed
            if (rootNode != null)
            {
                rootNode.IsExpanded = true;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"🔄 ROOT node found, Children.Count={rootNode.Children?.Count ?? 0}, IsExpanded={rootNode.IsExpanded}");
            }

            // Restore expanded nodes from captured state
            _treeNavManager.RestoreExpandedNodePaths(DialogNodes, state.ExpandedNodePaths);

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Restored {state.ExpandedNodePaths.Count} expanded nodes");

            // Restore selection if we had one captured
            if (!string.IsNullOrEmpty(state.SelectedNodePath))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"🔄 Looking for node with path: '{state.SelectedNodePath}'");
                var selectedNode = _treeNavManager.FindNodeByPath(DialogNodes, state.SelectedNodePath);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"🔄 FindNodeByPath returned: {(selectedNode != null ? selectedNode.DisplayText : "null")}");

                if (selectedNode != null)
                {
                    // Issue #252: Expand ancestors to ensure selected node is visible
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"🔄 Calling ExpandAncestors for '{selectedNode.DisplayText}'");
                    _treeNavManager.ExpandAncestors(DialogNodes, selectedNode);

                    // Also expand the selected node itself if it has children (to show restored children)
                    if (selectedNode.HasChildren)
                    {
                        selectedNode.IsExpanded = true;
                        UnifiedLogger.LogApplication(LogLevel.INFO, $"🔄 Expanded selected node '{selectedNode.DisplayText}'");
                    }

                    SelectedTreeNode = selectedNode;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"🔄 Restored selection to: '{selectedNode.DisplayText}'");
                }
                else
                {
                    // Node not found (may have been deleted by undo) - fallback to ROOT
                    // This prevents orphaned TextBox focus with no backing node
                    if (rootNode != null)
                    {
                        rootNode.IsExpanded = true; // Ensure ROOT visible when falling back
                    }
                    SelectedTreeNode = rootNode;
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"🔄 Could not find node for path: '{state.SelectedNodePath}', fallback to ROOT");
                }
            }
            else
            {
                // No selection was captured - select ROOT to ensure valid state
                SelectedTreeNode = rootNode;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "No previous selection, selected ROOT");
            }
        }

        #region Scrap Management

        /// <summary>
        /// Restore a node from the scrap back to the dialog.
        /// Automatically restores entire batch if entry is a batch root with children.
        /// </summary>
        public bool RestoreFromScrap(string entryId, TreeViewSafeNode? selectedParent)
        {
            if (CurrentDialog == null) return false;

            SaveUndoState("Restore from Scrap");

            // Check if this is a batch root with children - if so, restore entire batch
            var entry = _scrapManager.GetEntryById(entryId);
            RestoreResult result;

            if (entry != null && entry.IsBatchRoot && entry.ChildCount > 0)
            {
                // Restore entire batch (node + all children/orphans)
                result = _scrapManager.RestoreBatchFromScrap(entryId, CurrentDialog, selectedParent, _indexManager);
            }
            else
            {
                // Single node restore
                result = _scrapManager.RestoreFromScrap(entryId, CurrentDialog, selectedParent, _indexManager);
            }

            StatusMessage = result.StatusMessage;

            if (result.Success)
            {
                RefreshTreeView();
                HasUnsavedChanges = true;
            }

            return result.Success;
        }

        /// <summary>
        /// Clear all scrap entries
        /// </summary>
        public void ClearAllScrap()
        {
            _scrapManager.ClearAllScrap();
            OnPropertyChanged(nameof(ScrapCount));
            OnPropertyChanged(nameof(ScrapTabHeader));
            UpdateScrapBadgeVisibility();
        }

        /// <summary>
        /// Update scrap entries when file changes
        /// </summary>
        private void UpdateScrapForCurrentFile()
        {
            if (CurrentFileName == null) return;

            var entries = _scrapManager.GetScrapForFile(CurrentFileName);

            // Update the collection on UI thread
            Dispatcher.UIThread.Post(() =>
            {
                ScrapEntries.Clear();
                foreach (var entry in entries)
                {
                    ScrapEntries.Add(entry);
                }
                OnPropertyChanged(nameof(ScrapCount));
                OnPropertyChanged(nameof(ScrapTabHeader));
                UpdateScrapBadgeVisibility();
            });
        }

        /// <summary>
        /// Update the visibility of the scrap badge in the UI
        /// </summary>
        private void UpdateScrapBadgeVisibility()
        {
            // This will be handled by binding in the UI based on ScrapCount > 0
            // The badge visibility is controlled by the IsVisible binding in XAML
        }

        #endregion
    }
}

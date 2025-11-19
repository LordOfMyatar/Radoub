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
using Parley.Services;

namespace DialogEditor.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private Dialog? _currentDialog;
        private string? _currentFileName;
        private bool _isLoading;
        private string _statusMessage = "Ready";
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
                }
            }
        }

        public string LoadedFileName => CurrentFileName != null ? System.IO.Path.GetFileName(CurrentFileName) : "No file loaded";

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
                    OnPropertyChanged(nameof(WindowTitle));
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Unsaved changes flag: {value}");
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
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
                }
            }
        }

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

            // Hook up scrap count changed event
            _scrapManager.ScrapCountChanged += (s, count) =>
            {
                OnPropertyChanged(nameof(ScrapCount));
                OnPropertyChanged(nameof(ScrapTabHeader));
                UpdateScrapBadgeVisibility();
            };
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

        public async Task SaveDialogAsync(string filePath)
        {
            if (CurrentDialog == null)
            {
                StatusMessage = "No dialog to save";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Saving {System.IO.Path.GetFileName(filePath)}...";

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Saving dialog to: {UnifiedLogger.SanitizePath(filePath)}");

                // CLEANUP: Remove orphaned nodes before save (nodes with no incoming pointers)
                var orphanedNodes = _orphanManager.RemoveOrphanedNodes(CurrentDialog);
                if (orphanedNodes.Count > 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Removed {orphanedNodes.Count} orphaned nodes before save");
                    // Note: Orphaned nodes are removed from dialog, not added to scrap
                    // This is cleanup, not user-initiated deletion
                }

                // SAFETY VALIDATION: Validate all pointer indices before save (Issue #6 fix)
                var validationErrors = CurrentDialog.ValidatePointerIndices();
                if (validationErrors.Count > 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"⚠️ PRE-SAVE VALIDATION: Found {validationErrors.Count} index issues:");
                    foreach (var error in validationErrors)
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"  - {error}");
                    }

                    // Attempt to fix by rebuilding LinkRegistry and recalculating indices
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Attempting to auto-fix index issues...");
                    CurrentDialog.RebuildLinkRegistry();
                    RecalculatePointerIndices();

                    // Re-validate after fix attempt
                    var errorsAfterFix = CurrentDialog.ValidatePointerIndices();
                    if (errorsAfterFix.Count > 0)
                    {
                        UnifiedLogger.LogApplication(LogLevel.ERROR, $"❌ CRITICAL: {errorsAfterFix.Count} index issues remain after auto-fix!");
                        StatusMessage = $"ERROR: Dialog has {errorsAfterFix.Count} pointer index issues. Save aborted to prevent corruption.";
                        return;
                    }
                    else
                    {
                        UnifiedLogger.LogApplication(LogLevel.INFO, "✅ All index issues resolved successfully");
                    }
                }

                // Phase 4 Refactoring: Use DialogFileService facade instead of DialogParser directly
                var dialogService = new DialogFileService();

                // Determine output format based on file extension
                var extension = System.IO.Path.GetExtension(filePath).ToLower();
                bool success = false;

                // Log parameter counts before writing
                int totalActionParams = CurrentDialog.Entries.Sum(e => e.ActionParams.Count) + CurrentDialog.Replies.Sum(r => r.ActionParams.Count);
                int totalConditionParams = CurrentDialog.Entries.Sum(e => e.Pointers.Sum(p => p.ConditionParams.Count))
                                         + CurrentDialog.Replies.Sum(r => r.Pointers.Sum(p => p.ConditionParams.Count));
                UnifiedLogger.LogApplication(LogLevel.INFO, $"💾 SAVE: Dialog model has TotalActionParams={totalActionParams}, TotalConditionParams={totalConditionParams} before write");

                // Log entry order at save time
                UnifiedLogger.LogApplication(LogLevel.INFO, $"💾 SAVE: Entry list order (Count={CurrentDialog.Entries.Count}):");
                for (int i = 0; i < CurrentDialog.Entries.Count; i++)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"  Entry[{i}] = '{CurrentDialog.Entries[i].Text}'");
                }

                if (extension == ".json")
                {
                    var json = await dialogService.ConvertToJsonAsync(CurrentDialog);
                    if (!string.IsNullOrEmpty(json))
                    {
                        await System.IO.File.WriteAllTextAsync(filePath, json);
                        success = true;
                    }
                }
                else
                {
                    success = await dialogService.SaveToFileAsync(CurrentDialog, filePath);
                }

                if (success)
                {
                    CurrentFileName = filePath;
                    HasUnsavedChanges = false; // Clear dirty flag on successful save
                    StatusMessage = "Dialog saved successfully";
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Dialog saved successfully");

                    // REMOVED: Auto-export tree structure to logs (was too verbose for production use)
                    // Users can manually export tree via Debug menu if needed for troubleshooting

                    // NOTE: Auto-reload disabled during editing to preserve tree state
                    // Auto-reload the exported file to prevent cached data issues
                    // if (extension == ".dlg")
                    // {
                    //     UnifiedLogger.LogApplication(LogLevel.INFO, "Auto-reloading exported DLG file to verify integrity");
                    //     StatusMessage = "Reloading exported file...";
                    //     await LoadDialogAsync(filePath);
                    // }
                }
                else
                {
                    StatusMessage = "Failed to save dialog";
                    UnifiedLogger.LogApplication(LogLevel.ERROR, "Failed to save dialog - parser returned false");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving dialog: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save dialog: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void PopulateDialogNodes()
        {
            try
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "🎯 ENTERING PopulateDialogNodes method");

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
                foreach (var start in CurrentDialog.Starts)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🎯 Processing start with Index={start.Index}");
                    if (start.Index < CurrentDialog.Entries.Count)
                    {
                        var entry = CurrentDialog.Entries[(int)start.Index];
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🎯 Got entry: '{entry.DisplayText}' - about to create TreeViewSafeNode with SourcePointer");
                        // Pass the start DialogPtr as the source pointer so conditional scripts and parameters work
                        var safeNode = new TreeViewSafeNode(entry, ancestors: null, depth: 0, sourcePointer: start);
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🎯 Created TreeViewSafeNode with DisplayText: '{safeNode.DisplayText}', SourcePointer: {start != null}");
                        rootNode.Children?.Add(safeNode);
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added to Root: '{entry.DisplayText}'");
                    }
                    else
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"Start pointer index {start.Index} exceeds entry count {CurrentDialog.Entries.Count}");
                    }
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

                // Auto-select ROOT node for consistent initial state
                // This ensures Restore button logic works correctly and shows conversation settings
                SelectedTreeNode = rootNode;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Auto-selected ROOT node");

                // No longer creating orphan containers - using Scrap Tab instead
                // Orphaned nodes are now managed via the ScrapManager service
                // Commented out old orphan container system:
                // var orphanedNodes = FindOrphanedNodes(rootNode);
                // UnifiedLogger.LogApplication(LogLevel.DEBUG, $"PopulateDialogNodes: Found {orphanedNodes.Count} orphaned nodes");
                // CreateOrUpdateOrphanContainers(orphanedNodes, rootNode);

                // 🔧 TEMPORARY DEBUGGING DISABLED - Parser workaround now provides complete conversation tree
                // The parser workaround transfers Reply[1] and Reply[2] from Entry[1] to Entry[0] fixing the conversation flow
                /*
                if (CurrentDialog.Entries.Count > 3) // Only show if there are many entries, suggesting missing connections
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "⚠️  PARSER BUG DETECTED: Showing disconnected entries as separate roots");
                    UnifiedLogger.LogApplication(LogLevel.WARN, "⚠️  Entry[0] should have multiple reply options but parser only found 1 pointer");

                    for (int i = 1; i < CurrentDialog.Entries.Count; i++) // Skip Entry[0] since it's already shown
                    {
                        var entry = CurrentDialog.Entries[i];
                        if (entry.Type == DialogNodeType.Entry)
                        {
                            UnifiedLogger.LogApplication(LogLevel.INFO, $"📋 Adding Entry[{i}] as debugging root: '{entry.DisplayText}'");
                            var safeNode = new TreeViewSafeNode(entry);
                            newNodes.Add(safeNode);
                        }
                    }
                }
                */

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

        private void MarkReachableEntries(DialogNode entry, HashSet<DialogNode> reachableEntries)
        {
            if (reachableEntries.Contains(entry))
                return; // Already visited

            reachableEntries.Add(entry);

            // Follow all paths from this entry
            foreach (var pointer in entry.Pointers)
            {
                if (pointer.Node != null)
                {
                    // If this points to a reply, follow the reply's pointers too
                    if (pointer.Type == DialogNodeType.Reply)
                    {
                        foreach (var replyPointer in pointer.Node.Pointers)
                        {
                            if (replyPointer.Node != null && replyPointer.Type == DialogNodeType.Entry)
                            {
                                MarkReachableEntries(replyPointer.Node, reachableEntries);
                            }
                        }
                    }
                    // If this points directly to an entry, follow it
                    else if (pointer.Type == DialogNodeType.Entry)
                    {
                        MarkReachableEntries(pointer.Node, reachableEntries);
                    }
                }
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
            DialogNodes.Clear();
            SelectedScrapEntry = null; // Clear scrap selection
            StatusMessage = "Dialog closed";
            UnifiedLogger.LogApplication(LogLevel.INFO, "Dialog file closed");
        }

        // Node creation methods - Phase 1 Step 4

        /// <summary>
        /// Smart Add Node - Context-aware node creation (Phase 2)
        /// Parent determines child type:
        /// - Root → Entry (NPC speech)
        /// - Entry → Reply (PC response)
        /// - Reply → Entry (NPC response)
        /// </summary>
        /// <summary>
        /// Saves current state to undo stack before making changes
        /// </summary>
        private void SaveUndoState(string description)
        {
            if (CurrentDialog != null && !_undoRedoService.IsRestoring)
            {
                _undoRedoService.SaveState(CurrentDialog, description);
            }
        }

        public void Undo()
        {
            if (CurrentDialog == null || !_undoRedoService.CanUndo)
            {
                StatusMessage = "Nothing to undo";
                return;
            }

            // Capture tree state before undo
            var treeState = CaptureTreeState();

            var previousState = _undoRedoService.Undo(CurrentDialog, treeState);
            if (previousState.Success && previousState.RestoredDialog != null)
            {
                CurrentDialog = previousState.RestoredDialog;
                // CRITICAL: Rebuild LinkRegistry after undo to fix Issue #28 (IsLink corruption)
                CurrentDialog.RebuildLinkRegistry();

                // CRITICAL FIX (Issue #28): Don't use RefreshTreeView - it tries to restore
                // expansion state using old node references that don't exist after undo.
                // Instead, rebuild tree without expansion logic, then restore using paths.
                Dispatcher.UIThread.Post(() =>
                {
                    PopulateDialogNodes();

                    // Restore expansion after tree rebuilt using path-based state
                    Dispatcher.UIThread.Post(() =>
                    {
                        RestoreTreeState(treeState);
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

            // Capture tree state before redo
            var treeState = CaptureTreeState();

            var nextState = _undoRedoService.Redo(CurrentDialog, treeState);
            if (nextState.Success && nextState.RestoredDialog != null)
            {
                CurrentDialog = nextState.RestoredDialog;
                // CRITICAL: Rebuild LinkRegistry after redo to fix Issue #28 (IsLink corruption)
                CurrentDialog.RebuildLinkRegistry();

                // CRITICAL FIX (Issue #28): Don't use RefreshTreeView - it tries to restore
                // expansion state using old node references that don't exist after redo.
                // Instead, rebuild tree without expansion logic, then restore using paths.
                Dispatcher.UIThread.Post(() =>
                {
                    PopulateDialogNodes();

                    // Restore expansion after tree rebuilt using path-based state
                    Dispatcher.UIThread.Post(() =>
                    {
                        RestoreTreeState(treeState);
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

            // Save state for undo
            SaveUndoState("Add Smart Node");

            // Get the parent node and pointer
            DialogNode? parentNode = null;
            DialogPtr? parentPtr = null;

            if (selectedNode != null && !(selectedNode is TreeViewRootNode))
            {
                parentNode = selectedNode.OriginalNode;
                // Note: parentPtr would be needed if we're adding under a link, but for now passing null
            }

            // Delegate to NodeOperationsManager
            var newNode = _nodeOpsManager.AddSmartNode(CurrentDialog, parentNode, parentPtr);

            // Refresh the tree
            RefreshTreeView();

            // Update status message
            StatusMessage = $"Added new {newNode.Type} node";
            HasUnsavedChanges = true;
        }

        public void AddEntryNode(TreeViewSafeNode? parentNode = null)
        {
            if (CurrentDialog == null) return;

            // Save state for undo
            SaveUndoState("Add Entry Node");

            // Get the parent dialog node
            DialogNode? parentDialogNode = null;
            DialogPtr? parentPtr = null;

            if (parentNode != null && !(parentNode is TreeViewRootNode))
            {
                parentDialogNode = parentNode.OriginalNode;
                // Expand parent in tree view
                parentNode.IsExpanded = true;
            }

            // Delegate to NodeOperationsManager
            var newEntry = _nodeOpsManager.AddEntryNode(CurrentDialog, parentDialogNode, parentPtr);

            // Refresh tree display
            RefreshTreeView();

            // Update status message
            if (parentDialogNode == null)
            {
                StatusMessage = "Added new Entry node at root level";
            }
            else
            {
                StatusMessage = "Added new Entry node after Reply";
            }

            HasUnsavedChanges = true;
        }

        // Phase 1 Bug Fix: Removed AddNPCReplyNode - "NPC Reply" is actually Entry node after PC Reply

        public void AddPCReplyNode(TreeViewSafeNode parent)
        {
            if (CurrentDialog == null || parent == null) return;

            // Save state for undo
            SaveUndoState("Add PC Reply");

            // Get the parent dialog node
            var parentDialogNode = parent.OriginalNode;
            if (parentDialogNode == null) return;

            // Delegate to NodeOperationsManager
            var newReply = _nodeOpsManager.AddPCReplyNode(CurrentDialog, parentDialogNode, null);

            // Auto-expand parent node before refresh
            parent.IsExpanded = true;

            // Refresh tree display
            RefreshTreeView();

            HasUnsavedChanges = true;
            StatusMessage = "Added new PC Reply node";
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

            var node = nodeToDelete.OriginalNode;

            // Save state for undo before deleting
            SaveUndoState("Delete Node");

            // Delegate to NodeOperationsManager
            var linkedNodes = _nodeOpsManager.DeleteNode(CurrentDialog, node, CurrentFileName);

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

            // Refresh tree
            RefreshTreeView();

            HasUnsavedChanges = true;
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
        /// Find a sibling node to focus after cutting/deleting a node.
        /// Returns previous sibling if available, otherwise next sibling, otherwise parent.
        /// </summary>
        private DialogNode? FindSiblingForFocus(DialogNode node)
        {
            if (CurrentDialog == null) return null;

            // Delegate to NodeOperationsManager
            return _nodeOpsManager.FindSiblingForFocus(CurrentDialog, node);
        }

        private void PerformMove(List<DialogNode> list, DialogNodeType nodeType, uint oldIdx, uint newIdx)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Starting move operation: {nodeType} [{oldIdx}] → [{newIdx}]");

            // Create index tracker
            var tracker = new IndexUpdateTracker();
            tracker.CalculateMapping(oldIdx, newIdx, nodeType);

            UnifiedLogger.LogApplication(LogLevel.DEBUG, tracker.GetMoveDescription());

            // Save the node we're moving for later focus restoration
            var movedNode = list[(int)oldIdx];
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Moving node: '{movedNode.Text}' (type={movedNode.Type})");

            // Update all affected pointers
            UpdatePointersForMove(tracker);

            // Perform actual list reorder
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"BEFORE MOVE - List order:");
            for (int i = 0; i < list.Count; i++)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  [{i}] = '{list[i].Text}'");
            }

            list.RemoveAt((int)oldIdx);
            list.Insert((int)newIdx, movedNode);

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"AFTER MOVE - List order:");
            for (int i = 0; i < list.Count; i++)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  [{i}] = '{list[i].Text}'");
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"List reordered: removed at {oldIdx}, inserted at {newIdx}");

            // Validate integrity
            if (!ValidateMoveIntegrity())
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    "Move validation FAILED - index corruption detected!");
                StatusMessage = "Move failed - integrity check failed (use Undo to revert)";
                // User can use Undo (Ctrl+Z) to revert failed operations
                return;
            }

            // Mark as changed
            HasUnsavedChanges = true;

            // Refresh UI - need to rebuild tree structure
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Refreshing tree view...");
            PopulateDialogNodes();
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Tree view refreshed");

            StatusMessage = $"Moved {nodeType} from position {oldIdx} to {newIdx}";
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Move completed successfully: {nodeType} [{oldIdx}] → [{newIdx}]");
        }

        private void UpdatePointersForMove(IndexUpdateTracker tracker)
        {
            if (CurrentDialog == null) return;

            int updateCount = 0;

            // Update StartingList pointers (if moving entries)
            if (tracker.ListType == DialogNodeType.Entry)
            {
                foreach (var start in CurrentDialog.Starts)
                {
                    if (tracker.TryGetUpdatedIndex(start.Index, out uint newIdx))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"StartingList: Index {start.Index} → {newIdx}");
                        start.Index = newIdx;
                        updateCount++;
                    }
                }
            }

            // Update Entry → Reply pointers (if moving replies)
            if (tracker.ListType == DialogNodeType.Reply)
            {
                foreach (var entry in CurrentDialog.Entries)
                {
                    foreach (var ptr in entry.Pointers)
                    {
                        if (tracker.TryGetUpdatedIndex(ptr.Index, out uint newIdx))
                        {
                            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                $"Entry '{entry.DisplayText}' → Reply: Index {ptr.Index} → {newIdx}");
                            ptr.Index = newIdx;
                            updateCount++;
                        }
                    }
                }
            }

            // Update Reply → Entry pointers (if moving entries)
            if (tracker.ListType == DialogNodeType.Entry)
            {
                foreach (var reply in CurrentDialog.Replies)
                {
                    foreach (var ptr in reply.Pointers)
                    {
                        if (tracker.TryGetUpdatedIndex(ptr.Index, out uint newIdx))
                        {
                            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                $"Reply '{reply.DisplayText}' → Entry: Index {ptr.Index} → {newIdx}");
                            ptr.Index = newIdx;
                            updateCount++;
                        }
                    }
                }
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Updated {updateCount} pointer indices");
        }

        private bool ValidateMoveIntegrity()
        {
            if (CurrentDialog == null) return false;

            try
            {
                // Validate StartingList pointers
                foreach (var start in CurrentDialog.Starts)
                {
                    if (start.Index >= CurrentDialog.Entries.Count)
                    {
                        UnifiedLogger.LogApplication(LogLevel.ERROR,
                            $"Invalid Start Index: {start.Index} >= Entry count {CurrentDialog.Entries.Count}");
                        return false;
                    }

                    if (start.Node != CurrentDialog.Entries[(int)start.Index])
                    {
                        UnifiedLogger.LogApplication(LogLevel.ERROR,
                            $"Start pointer mismatch: Index {start.Index} does not point to expected node");
                        return false;
                    }
                }

                // Validate Entry → Reply pointers
                foreach (var entry in CurrentDialog.Entries)
                {
                    foreach (var ptr in entry.Pointers)
                    {
                        if (ptr.Index >= CurrentDialog.Replies.Count)
                        {
                            UnifiedLogger.LogApplication(LogLevel.ERROR,
                                $"Invalid Reply Index in Entry '{entry.DisplayText}': {ptr.Index} >= Reply count {CurrentDialog.Replies.Count}");
                            return false;
                        }

                        if (ptr.Node != CurrentDialog.Replies[(int)ptr.Index])
                        {
                            UnifiedLogger.LogApplication(LogLevel.ERROR,
                                $"Entry pointer mismatch: Index {ptr.Index} does not point to expected node");
                            return false;
                        }
                    }
                }

                // Validate Reply → Entry pointers
                foreach (var reply in CurrentDialog.Replies)
                {
                    foreach (var ptr in reply.Pointers)
                    {
                        if (ptr.Index >= CurrentDialog.Entries.Count)
                        {
                            UnifiedLogger.LogApplication(LogLevel.ERROR,
                                $"Invalid Entry Index in Reply '{reply.DisplayText}': {ptr.Index} >= Entry count {CurrentDialog.Entries.Count}");
                            return false;
                        }

                        if (ptr.Node != CurrentDialog.Entries[(int)ptr.Index])
                        {
                            UnifiedLogger.LogApplication(LogLevel.ERROR,
                                $"Reply pointer mismatch: Index {ptr.Index} does not point to expected node");
                            return false;
                        }
                    }
                }

                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Move integrity validation PASSED");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Validation exception: {ex.Message}");
                return false;
            }
        }

        private void RefreshTreeView()
        {
            // Save expansion state before refresh
            var expandedNodeRefs = _treeNavManager.SaveTreeExpansionState(DialogNodes);

            // Re-populate tree to reflect changes
            // CRITICAL: Run synchronously to ensure orphan removal is reflected immediately
            PopulateDialogNodes();

            // Restore expansion state after tree is rebuilt
            // Use Dispatcher for expansion restore to ensure tree is fully rendered
            Dispatcher.UIThread.Post(() =>
            {
                _treeNavManager.RestoreTreeExpansionState(DialogNodes, expandedNodeRefs);
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

                var results = testResults.ToString();

                // Phase 0: Application shutdown if requested
                // if (closeAppAfterTest)
                // {
                //     UnifiedLogger.LogApplication(LogLevel.INFO, "Round-trip test completed, closing application");
                //     Dispatcher.UIThread.Post(() =>
                //     {
                //         Application.Current.Shutdown();
                //     });
                // }

                return results;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Round-trip test failed: {ex.Message}";

                // Phase 0: Application shutdown if requested (even on error)
                // if (closeAppAfterTest)
                // {
                //     UnifiedLogger.LogApplication(LogLevel.ERROR, $"Round-trip test failed: {ex.Message}, closing application");
                //     Dispatcher.UIThread.Post(() =>
                //     {
                //         Application.Current.Shutdown();
                //     });
                // }

                return errorMessage;
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
            _clipboardService.CopyNode(node, CurrentDialog);

            StatusMessage = $"Node copied: {node.DisplayText}";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Copied node: {node.DisplayText}");
        }

        public void CutNode(TreeViewSafeNode? nodeToCut)
        {
            if (nodeToCut == null || nodeToCut is TreeViewRootNode)
            {
                StatusMessage = "Cannot cut ROOT node";
                return;
            }

            var node = nodeToCut.OriginalNode;

            // Find sibling to focus BEFORE cutting
            var siblingToFocus = FindSiblingForFocus(node);

            // Save state for undo before cutting
            SaveUndoState("Cut Node");

            // Store node for pasting in clipboard service
            if (CurrentDialog != null)
            {
                _clipboardService.CutNode(node, CurrentDialog);
            }

            // CRITICAL: Check for other references BEFORE detaching
            // We need to count while the current reference is still there
            bool hasOtherReferences = HasOtherReferences(node);

            // Detach from parent
            DetachNodeFromParent(node);

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
                RecalculatePointerIndices();
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

        private bool HasOtherReferences(DialogNode node)
        {
            if (CurrentDialog == null) return false;

            int refCount = 0;

            // Count references in Starts
            refCount += CurrentDialog.Starts.Count(s => s.Node == node);

            // Count references in all Entries
            foreach (var entry in CurrentDialog.Entries)
            {
                refCount += entry.Pointers.Count(p => p.Node == node);
            }

            // Count references in all Replies
            foreach (var reply in CurrentDialog.Replies)
            {
                refCount += reply.Pointers.Count(p => p.Node == node);
            }

            // If more than 1 reference, has other references besides the one we're cutting
            return refCount > 1;
        }

        /// <summary>
        /// CRITICAL: Recalculates all pointer indices to match current list positions
        /// This must be called after any operation that removes nodes from Entries/Replies lists
        /// </summary>
        private void RecalculatePointerIndices()
        {
            if (CurrentDialog == null) return;

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Recalculating all pointer indices using LinkRegistry");

            // Rebuild the LinkRegistry from current dialog state
            CurrentDialog.RebuildLinkRegistry();

            // Update all Entry node indices
            for (uint i = 0; i < CurrentDialog.Entries.Count; i++)
            {
                var entry = CurrentDialog.Entries[(int)i];
                CurrentDialog.LinkRegistry.UpdateNodeIndex(entry, i, DialogNodeType.Entry);
            }

            // Update all Reply node indices
            for (uint i = 0; i < CurrentDialog.Replies.Count; i++)
            {
                var reply = CurrentDialog.Replies[(int)i];
                CurrentDialog.LinkRegistry.UpdateNodeIndex(reply, i, DialogNodeType.Reply);
            }

            // Validate all indices are correct
            var errors = CurrentDialog.ValidatePointerIndices();
            if (errors.Count > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Index validation found {errors.Count} issues after recalculation:");
                foreach (var error in errors)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"  - {error}");
                }
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "All pointer indices validated successfully");
            }
        }

        private void DetachNodeFromParent(DialogNode node)
        {
            if (CurrentDialog == null) return;

            // Remove from Starts list if present
            var startToRemove = CurrentDialog.Starts.FirstOrDefault(s => s.Node == node);
            if (startToRemove != null)
            {
                CurrentDialog.Starts.Remove(startToRemove);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Detached from Starts list");
                return;
            }

            // Remove from parent's pointers
            foreach (var entry in CurrentDialog.Entries)
            {
                var ptrToRemove = entry.Pointers.FirstOrDefault(p => p.Node == node);
                if (ptrToRemove != null)
                {
                    entry.Pointers.Remove(ptrToRemove);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Detached from Entry: {entry.DisplayText}");
                    return;
                }
            }

            foreach (var reply in CurrentDialog.Replies)
            {
                var ptrToRemove = reply.Pointers.FirstOrDefault(p => p.Node == node);
                if (ptrToRemove != null)
                {
                    reply.Pointers.Remove(ptrToRemove);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Detached from Reply: {reply.DisplayText}");
                    return;
                }
            }
        }

        public void PasteAsDuplicate(TreeViewSafeNode? parent)
        {
            if (CurrentDialog == null) return;
            if (_clipboardService.ClipboardNode == null)
            {
                StatusMessage = "No node copied. Use Copy Node first.";
                return;
            }
            if (parent == null)
            {
                StatusMessage = "Select a parent node to paste under";
                return;
            }

            // Save state for undo before pasting
            SaveUndoState("Paste Node");

            // Check if pasting to ROOT
            if (parent is TreeViewRootNode)
            {
                // PC Replies can NEVER be at ROOT (they only respond to NPCs)
                if (_clipboardService.ClipboardNode.Type == DialogNodeType.Reply && string.IsNullOrEmpty(_clipboardService.ClipboardNode.Speaker))
                {
                    StatusMessage = "Cannot paste PC Reply to ROOT - PC can only respond to NPC statements";
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Blocked PC Reply paste to ROOT");
                    return;
                }

                // For Cut operation, reuse the node; for Copy, clone it
                var duplicate = _clipboardService.WasCutOperation ? _clipboardService.ClipboardNode : CloneNode(_clipboardService.ClipboardNode);

                // Convert NPC Reply nodes to Entry when pasting to ROOT (GFF requirement)
                if (duplicate.Type == DialogNodeType.Reply)
                {
                    // This is an NPC Reply (has Speaker set) - convert to Entry
                    duplicate.Type = DialogNodeType.Entry;

                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-converted NPC Reply to Entry for ROOT level");
                    StatusMessage = $"Auto-converted NPC Reply to Entry for ROOT level";
                }

                // If cut, ensure node is in the appropriate list (may have been removed during cut)
                if (_clipboardService.WasCutOperation)
                {
                    var list = duplicate.Type == DialogNodeType.Entry ? CurrentDialog.Entries : CurrentDialog.Replies;
                    if (!list.Contains(duplicate))
                    {
                        CurrentDialog.AddNodeInternal(duplicate, duplicate.Type);
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Re-added cut node to list: {duplicate.DisplayText}");
                    }
                }
                else
                {
                    // For copy, always add (will be a new clone)
                    CurrentDialog.AddNodeInternal(duplicate, duplicate.Type);
                }

                // Get the correct index after adding
                var duplicateIndex = (uint)CurrentDialog.GetNodeIndex(duplicate, duplicate.Type);

                // Create start pointer
                var startPtr = new DialogPtr
                {
                    Node = duplicate,
                    Type = DialogNodeType.Entry,
                    Index = duplicateIndex,
                    IsLink = false,
                    IsStart = true,
                    ScriptAppears = "",
                    ConditionParams = new Dictionary<string, string>(),
                    Comment = "",
                    Parent = CurrentDialog
                };

                CurrentDialog.Starts.Add(startPtr);

                // Register the start pointer with LinkRegistry
                CurrentDialog.LinkRegistry.RegisterLink(startPtr);

                // CRITICAL: Recalculate indices in case recursive cloning added multiple nodes
                RecalculatePointerIndices();

                RefreshTreeView();
                HasUnsavedChanges = true;
                var opType = _clipboardService.WasCutOperation ? "Moved" : "Pasted duplicate";
                StatusMessage = $"{opType} Entry at ROOT: {duplicate.DisplayText}";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"{opType} Entry to ROOT: {duplicate.DisplayText}");

                // Clipboard is cleared by service after cut/paste
                return;
            }

            // Normal paste to non-ROOT parent
            var parentNode = parent.OriginalNode;

            // CRITICAL: Validate parent/child type compatibility (Aurora rule)
            // Entry (NPC) can only have Reply (PC) children
            // Reply (PC) can only have Entry (NPC) children
            if (parentNode.Type == _clipboardService.ClipboardNode.Type)
            {
                string parentTypeName = parentNode.Type == DialogNodeType.Entry ? "NPC" : "PC";
                string childTypeName = _clipboardService.ClipboardNode.Type == DialogNodeType.Entry ? "NPC" : "PC";
                StatusMessage = $"Cannot paste {childTypeName} under {parentTypeName} - conversation must alternate NPC/PC";
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Blocked invalid paste: {childTypeName} node under {parentTypeName} parent");
                return;
            }

            // For Cut operation, reuse the node; for Copy, clone it
            var duplicateNode = _clipboardService.WasCutOperation ? _clipboardService.ClipboardNode : CloneNode(_clipboardService.ClipboardNode);

            // If cut, ensure node is in the appropriate list (may have been removed during cut)
            if (_clipboardService.WasCutOperation)
            {
                var list = duplicateNode.Type == DialogNodeType.Entry ? CurrentDialog.Entries : CurrentDialog.Replies;
                if (!list.Contains(duplicateNode))
                {
                    CurrentDialog.AddNodeInternal(duplicateNode, duplicateNode.Type);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Re-added cut node to list: {duplicateNode.DisplayText}");
                }
            }
            else
            {
                // For copy, always add (will be a new clone)
                CurrentDialog.AddNodeInternal(duplicateNode, duplicateNode.Type);
            }

            // Get the correct index after adding
            var nodeIndex = (uint)CurrentDialog.GetNodeIndex(duplicateNode, duplicateNode.Type);

            // Create pointer from parent to duplicate
            var newPtr = new DialogPtr
            {
                Node = duplicateNode,
                Type = duplicateNode.Type,
                Index = nodeIndex,
                IsLink = false,
                ScriptAppears = "",
                ConditionParams = new Dictionary<string, string>(),
                Comment = "",
                Parent = CurrentDialog
            };

            parent.OriginalNode.Pointers.Add(newPtr);

            // Register the new pointer with LinkRegistry
            CurrentDialog.LinkRegistry.RegisterLink(newPtr);

            // CRITICAL: Recalculate indices in case recursive cloning added multiple nodes
            RecalculatePointerIndices();

            RefreshTreeView();
            HasUnsavedChanges = true;
            var operation = _clipboardService.WasCutOperation ? "Moved" : "Pasted duplicate";
            StatusMessage = $"{operation} node under {parent.DisplayText}: {duplicateNode.DisplayText}";

            // Clipboard is cleared by service after cut/paste
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Pasted duplicate: {duplicateNode.DisplayText} under {parent.DisplayText}");
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

            // Save state for undo before creating link
            SaveUndoState("Paste as Link");

            // Delegate to clipboard service
            var linkPtr = _clipboardService.PasteAsLink(CurrentDialog, parent.OriginalNode);

            if (linkPtr == null)
            {
                // Service already logged the reason (Cut operation, different dialog, node not found, etc.)
                StatusMessage = "Cannot paste as link - check logs for details";
                return;
            }

            // Register the link pointer with LinkRegistry
            CurrentDialog.LinkRegistry.RegisterLink(linkPtr);

            RefreshTreeView();
            HasUnsavedChanges = true;
            StatusMessage = $"Pasted link under {parent.DisplayText}: {linkPtr.Node?.DisplayText}";
        }

        private DialogNode CloneNode(DialogNode original)
        {
            return CloneNodeWithDepth(original, 0, new HashSet<DialogNode>());
        }

        private DialogNode CloneNodeWithDepth(DialogNode original, int depth, HashSet<DialogNode> visited)
        {
            // Prevent infinite recursion with depth limit
            const int MAX_DEPTH = 100;
            if (depth > MAX_DEPTH)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Maximum clone depth ({MAX_DEPTH}) exceeded - possible circular reference");
                throw new InvalidOperationException($"Maximum clone depth ({MAX_DEPTH}) exceeded during node cloning");
            }

            // Prevent circular reference cloning
            if (!visited.Add(original))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Circular reference detected during clone at depth {depth} - creating link instead");
                // Return a simple node without children to break the cycle
                return new DialogNode
                {
                    Type = original.Type,
                    Text = CloneLocString(original.Text),
                    Speaker = original.Speaker,
                    Comment = original.Comment + " [CIRCULAR REF]",
                    Pointers = new List<DialogPtr>() // No children to prevent infinite loop
                };
            }

            // Deep copy of node
            var clone = new DialogNode
            {
                Type = original.Type,
                Text = CloneLocString(original.Text),
                Speaker = original.Speaker,
                Comment = original.Comment,
                Sound = original.Sound,
                ScriptAction = original.ScriptAction,
                Animation = original.Animation,
                AnimationLoop = original.AnimationLoop,
                Delay = original.Delay,
                Quest = original.Quest,
                QuestEntry = original.QuestEntry,
                Pointers = new List<DialogPtr>(), // Empty - will populate below
                ActionParams = new Dictionary<string, string>(original.ActionParams ?? new Dictionary<string, string>())
            };

            // Recursively clone all child pointers
            foreach (var ptr in original.Pointers)
            {
                // CRITICAL FIX: Null safety - skip if ptr.Node is null
                if (ptr.Node == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Skipping null pointer during clone of '{original.DisplayText}'");
                    continue;
                }

                var clonedChild = CloneNodeWithDepth(ptr.Node, depth + 1, visited);

                // Add cloned child to dialog lists using AddNodeInternal to update LinkRegistry
                CurrentDialog!.AddNodeInternal(clonedChild, clonedChild.Type);

                // Get the correct index after adding (LinkRegistry will track this)
                var nodeIndex = (uint)CurrentDialog.GetNodeIndex(clonedChild, clonedChild.Type);

                // Create pointer to cloned child
                var clonedPtr = new DialogPtr
                {
                    Node = clonedChild,
                    Type = clonedChild.Type,
                    Index = nodeIndex,
                    IsLink = ptr.IsLink,
                    ScriptAppears = ptr.ScriptAppears,
                    ConditionParams = new Dictionary<string, string>(ptr.ConditionParams ?? new Dictionary<string, string>()),
                    Comment = ptr.Comment,
                    LinkComment = ptr.LinkComment,
                    Parent = CurrentDialog
                };

                clone.Pointers.Add(clonedPtr);

                // Register the new pointer with LinkRegistry
                CurrentDialog.LinkRegistry.RegisterLink(clonedPtr);
            }

            return clone;
        }

        private LocString CloneLocString(LocString? original)
        {
            if (original == null)
                return new LocString();

            var clone = new LocString();

            foreach (var kvp in original.Strings)
            {
                clone.Strings[kvp.Key] = kvp.Value;
            }

            return clone;
        }

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

            return CaptureTreeStructure();
        }

        /// <summary>
        /// Captures the current tree expansion state and selected node
        /// </summary>
        private Parley.Services.TreeState CaptureTreeState()
        {
            var state = new Parley.Services.TreeState
            {
                ExpandedNodePaths = _treeNavManager.CaptureExpandedNodePaths(DialogNodes),
                SelectedNodePath = null
            };

            return state;
        }

        /// <summary>
        /// Finds all dialog nodes that exist but aren't reachable from any START
        /// Issue #27: These orphaned nodes need to be displayed separately
        /// </summary>
        private List<DialogNode> FindOrphanedNodes(TreeViewRootNode rootNode)
        {
            if (CurrentDialog == null) return new List<DialogNode>();

            // Collect all nodes reachable from STARTs via dialog model traversal
            var reachableNodes = new HashSet<DialogNode>();

            // Traverse directly from CurrentDialog.Starts using dialog model (not TreeView)
            // This ensures we follow ALL pointers including parent-child links (IsLink=true)
            // TreeView-based traversal would stop at IsChild nodes, causing false orphan detection
            foreach (var start in CurrentDialog.Starts)
            {
                if (start.Node != null)
                {
                    CollectReachableNodesForOrphanDetection(start.Node, reachableNodes);
                }
            }

            // Find entries that aren't reachable, EXCLUDING orphan containers and their children
            var orphanedEntries = CurrentDialog.Entries
                .Where(e => !reachableNodes.Contains(e))
                .Where(e => e.Comment?.Contains("PARLEY: Orphaned") != true)
                .ToList();

            // Find replies that aren't reachable, EXCLUDING orphan container category nodes
            var orphanedReplies = CurrentDialog.Replies
                .Where(r => !reachableNodes.Contains(r))
                .Where(r => r.Comment?.Contains("PARLEY: Orphaned") != true)
                .ToList();

            // Collect subtrees from orphaned Entries to identify descendant nodes
            // Only process Entries as roots; Replies will be found as descendants
            var orphanSubtreeNodes = new HashSet<DialogNode>();
            foreach (var orphan in orphanedEntries)
            {
                CollectDialogSubtree(orphan, orphanSubtreeNodes);
            }

            // Filter out nodes that are descendants of other orphans (keep only root orphans)
            var rootOrphanedEntries = orphanedEntries
                .Where(e => !orphanSubtreeNodes.Contains(e))
                .ToList();
            var rootOrphanedReplies = orphanedReplies
                .Where(r => !orphanSubtreeNodes.Contains(r))
                .ToList();

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found {rootOrphanedEntries.Count} root orphaned entries, {rootOrphanedReplies.Count} root orphaned replies");

            // Combine and return only root orphans
            var allOrphans = new List<DialogNode>();
            allOrphans.AddRange(rootOrphanedEntries);
            allOrphans.AddRange(rootOrphanedReplies);

            return allOrphans;
        }

        /// <summary>
        /// Collects all nodes reachable from a node via dialog pointers (not TreeView)
        /// Used to find descendants of orphaned nodes for filtering
        /// </summary>
        private void CollectDialogSubtree(DialogNode node, HashSet<DialogNode> visited)
        {
            if (node == null || visited.Contains(node)) return;
            CollectDialogSubtreeChildren(node, visited);
        }

        /// <summary>
        /// Recursively collects all descendants of a node via dialog pointers
        /// Node is marked as visited BEFORE recursing to prevent infinite loops,
        /// then we call this helper to process children without the visited check
        /// </summary>
        private void CollectDialogSubtreeChildren(DialogNode node, HashSet<DialogNode> visited)
        {
            foreach (var ptr in node.Pointers)
            {
                if (ptr.Node == null) continue;

                if (!visited.Contains(ptr.Node))
                {
                    visited.Add(ptr.Node);
                    // Recursively collect descendants (node already in visited, so use Children helper)
                    CollectDialogSubtreeChildren(ptr.Node, visited);
                }
            }
        }

        /// <summary>
        /// Creates or updates synthetic container nodes for orphaned dialog nodes
        /// These containers are saved to the dialog file with sc_false scripts
        /// Ensures identical display in Parley and Aurora
        /// Reuses existing containers to preserve any external links pointing to them
        /// </summary>
        private void CreateOrUpdateOrphanContainers(List<DialogNode> orphanedNodes, TreeViewRootNode rootNode)
        {
            if (CurrentDialog == null) return;

            // Filter out any containers from orphaned nodes (shouldn't happen, but defensive)
            var orphanedNodesFiltered = orphanedNodes
                .Where(n => n.Comment?.Contains("PARLEY: Orphaned") != true)
                .ToList();

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"CreateOrUpdateOrphanContainers: {orphanedNodes.Count} orphans detected, {orphanedNodesFiltered.Count} after filtering containers");

            // Find existing orphan containers
            var existingRootContainer = CurrentDialog.Entries
                .FirstOrDefault(e => e.Comment?.Contains("PARLEY: Orphaned nodes root container") == true);
            var existingNpcCategory = CurrentDialog.Replies
                .FirstOrDefault(r => r.Comment?.Contains("PARLEY: Orphaned NPC entries category") == true);

            // If no orphans exist, remove containers entirely
            if (orphanedNodesFiltered.Count == 0)
            {
                bool removedContainers = false;

                // Remove START pointer to orphan container
                var orphanStart = CurrentDialog.Starts
                    .FirstOrDefault(s => s.Comment?.Contains("Orphan container") == true);
                if (orphanStart != null)
                {
                    CurrentDialog.Starts.Remove(orphanStart);
                    CurrentDialog.LinkRegistry.UnregisterLink(orphanStart);
                    removedContainers = true;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Removed orphan START pointer");
                }

                // Remove NPC category reply and its link from root container
                if (existingNpcCategory != null)
                {
                    // Remove pointer from root container to NPC category
                    if (existingRootContainer != null)
                    {
                        var categoryPtr = existingRootContainer.Pointers.FirstOrDefault(p => p.Node == existingNpcCategory);
                        if (categoryPtr != null)
                        {
                            existingRootContainer.Pointers.Remove(categoryPtr);
                            CurrentDialog.LinkRegistry.UnregisterLink(categoryPtr);
                        }
                    }

                    CurrentDialog.Replies.Remove(existingNpcCategory);
                    removedContainers = true;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Removed NPC category container");
                }

                // Remove root container
                if (existingRootContainer != null)
                {
                    CurrentDialog.Entries.Remove(existingRootContainer);
                    removedContainers = true;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Removed root orphan container");
                }

                if (removedContainers)
                {
                    // Recalculate indices after removal
                    RecalculatePointerIndices();
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Removed empty orphan containers after nodes were re-linked");
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "No orphans found - no containers to remove");
                }

                return;
            }

            // Separate entries and replies
            var orphanedEntries = orphanedNodesFiltered.Where(n => n.Type == DialogNodeType.Entry).ToList();
            var orphanedReplies = orphanedNodesFiltered.Where(n => n.Type == DialogNodeType.Reply).ToList();

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Orphan breakdown: {orphanedEntries.Count} entries, {orphanedReplies.Count} replies");

            if (orphanedEntries.Count == 0 && orphanedReplies.Count == 0)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "No orphans to containerize after filtering");
                return;
            }

            // Create or reuse root container (NPC Entry)
            // ALWAYS move to end of Entries list to prevent game evaluation
            DialogNode rootContainer;
            if (existingRootContainer != null)
            {
                rootContainer = existingRootContainer;
                rootContainer.Pointers.Clear(); // Clear old pointers

                // Move to end of Entries list
                CurrentDialog.Entries.Remove(rootContainer);
                CurrentDialog.Entries.Add(rootContainer);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Reusing existing orphan root container (moved to end)");
            }
            else
            {
                rootContainer = new DialogNode
                {
                    Type = DialogNodeType.Entry,
                    Text = new LocString(),
                    Comment = "PARLEY: Orphaned nodes root container - never appears in-game (sc_false)",
                    Speaker = "",
                    Parent = CurrentDialog
                };
                rootContainer.Text.Add(0, "!!! Orphaned Nodes");
                CurrentDialog.Entries.Add(rootContainer); // Added at end automatically
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Created new orphan root container at end");
            }

            // Create or reuse PC Reply category node under root container (for orphaned NPC Entries)
            // ALWAYS move to end of Replies list to prevent game evaluation
            if (orphanedEntries.Count > 0)
            {
                DialogNode npcCategoryReply;
                if (existingNpcCategory != null)
                {
                    npcCategoryReply = existingNpcCategory;
                    npcCategoryReply.Pointers.Clear(); // Clear old pointers

                    // Move to end of Replies list
                    CurrentDialog.Replies.Remove(npcCategoryReply);
                    CurrentDialog.Replies.Add(npcCategoryReply);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Reusing existing NPC category container (moved to end)");
                }
                else
                {
                    npcCategoryReply = new DialogNode
                    {
                        Type = DialogNodeType.Reply,
                        Text = new LocString(),
                        Comment = "PARLEY: Orphaned NPC entries category",
                        Speaker = "",
                        Parent = CurrentDialog
                    };
                    npcCategoryReply.Text.Add(0, "!!! Orphaned NPC Nodes");
                    CurrentDialog.Replies.Add(npcCategoryReply); // Added at end automatically
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Created new NPC category container at end");
                }

                // Point to all orphaned entries (NOT as links - show full subtree)
                foreach (var orphan in orphanedEntries)
                {
                    var orphanIndex = (uint)CurrentDialog.Entries.IndexOf(orphan);
                    var ptr = new DialogPtr
                    {
                        Node = orphan,
                        Type = DialogNodeType.Entry,
                        Index = orphanIndex,
                        IsLink = false, // NOT a link - show full subtree
                        ScriptAppears = "",
                        ConditionParams = new Dictionary<string, string>(),
                        Comment = "Pointer to orphaned NPC entry",
                        Parent = CurrentDialog
                    };
                    npcCategoryReply.Pointers.Add(ptr);
                    CurrentDialog.LinkRegistry.RegisterLink(ptr);
                }

                // Link from root container (if not already linked)
                if (!rootContainer.Pointers.Any(p => p.Node == npcCategoryReply))
                {
                    var npcCategoryIndex = (uint)CurrentDialog.Replies.IndexOf(npcCategoryReply);
                    var npcCategoryPtr = new DialogPtr
                    {
                        Node = npcCategoryReply,
                        Type = DialogNodeType.Reply,
                        Index = npcCategoryIndex,
                        IsLink = false,
                        Parent = CurrentDialog
                    };
                    rootContainer.Pointers.Add(npcCategoryPtr);
                    CurrentDialog.LinkRegistry.RegisterLink(npcCategoryPtr);
                }
            }

            // Handle orphaned PC replies - link directly to root container
            // Root container is Entry, so it can point directly to Reply nodes (no intermediate needed)
            foreach (var orphan in orphanedReplies)
            {
                // Link directly from root container to orphaned reply
                if (!rootContainer.Pointers.Any(p => p.Node == orphan))
                {
                    var orphanIndex = (uint)CurrentDialog.Replies.IndexOf(orphan);
                    var ptr = new DialogPtr
                    {
                        Node = orphan,
                        Type = DialogNodeType.Reply,
                        Index = orphanIndex,
                        IsLink = false,
                        ScriptAppears = "",
                        ConditionParams = new Dictionary<string, string>(),
                        Comment = "Pointer to orphaned PC reply",
                        Parent = CurrentDialog
                    };
                    rootContainer.Pointers.Add(ptr);
                    CurrentDialog.LinkRegistry.RegisterLink(ptr);
                }
            }

            // Create or reuse START pointer to root container
            var existingOrphanStart = CurrentDialog.Starts
                .FirstOrDefault(s => s.Comment?.Contains("Orphan container") == true);

            DialogPtr rootStart;
            if (existingOrphanStart != null)
            {
                rootStart = existingOrphanStart;
                // Update index in case container was moved
                var rootIndex = (uint)CurrentDialog.Entries.IndexOf(rootContainer);
                rootStart.Index = rootIndex;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Reusing existing orphan START pointer");
            }
            else
            {
                var rootIndex = (uint)CurrentDialog.Entries.IndexOf(rootContainer);
                rootStart = new DialogPtr
                {
                    Node = rootContainer,
                    Type = DialogNodeType.Entry,
                    Index = rootIndex,
                    IsLink = false,
                    IsStart = true,
                    ScriptAppears = "sc_false",
                    ConditionParams = new Dictionary<string, string>(),
                    Comment = "Orphan container - requires sc_false.nss in module",
                    Parent = CurrentDialog
                };
                CurrentDialog.Starts.Add(rootStart);
                CurrentDialog.LinkRegistry.RegisterLink(rootStart);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Created new orphan START pointer");
            }

            // Add to TreeView
            var rootSafeNode = new TreeViewSafeNode(rootContainer, ancestors: null, depth: 0, sourcePointer: rootStart);
            rootNode.Children?.Add(rootSafeNode);

            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Updated orphan container with {orphanedEntries.Count} NPC entries, {orphanedReplies.Count} PC replies - requires sc_false.nss");

            StatusMessage = $"!!! {orphanedNodes.Count} orphaned node(s) - container updated (requires sc_false.nss)";
        }

        /// <summary>
        /// Synchronously detects orphans and creates/updates containers
        /// Called immediately after deletion to ensure orphans are containerized before save
        /// </summary>
        private void DetectAndContainerizeOrphansSync()
        {
            if (CurrentDialog == null) return;

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "🔍 SYNC: DetectAndContainerizeOrphansSync called");
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"🔍 SYNC: Total nodes in dialog: {CurrentDialog.Entries.Count} entries, {CurrentDialog.Replies.Count} replies");
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"🔍 SYNC: Total STARTs: {CurrentDialog.Starts.Count}");

            // Find all orphaned nodes by traversing from STARTs
            var reachableNodes = new HashSet<DialogNode>();
            foreach (var start in CurrentDialog.Starts)
            {
                if (start.Node != null)
                {
                    CollectReachableNodesForOrphanDetection(start.Node, reachableNodes);
                }
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"🔍 SYNC: Reachable from STARTs: {reachableNodes.Count} nodes");

            // Find entries that aren't reachable, EXCLUDING existing orphan containers
            var orphanedEntries = CurrentDialog.Entries
                .Where(e => !reachableNodes.Contains(e))
                .Where(e => e.Comment?.Contains("PARLEY: Orphaned") != true)
                .ToList();

            // Find replies that aren't reachable, EXCLUDING orphan container nodes
            var orphanedReplies = CurrentDialog.Replies
                .Where(r => !reachableNodes.Contains(r))
                .Where(r => r.Comment?.Contains("PARLEY: Orphaned") != true)
                .ToList();

            var allOrphans = new List<DialogNode>();
            allOrphans.AddRange(orphanedEntries);
            allOrphans.AddRange(orphanedReplies);

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"🔍 SYNC: Found {orphanedEntries.Count} orphaned entries, {orphanedReplies.Count} orphaned replies");

            if (orphanedEntries.Count > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"🔍 SYNC: Orphaned entries: {string.Join(", ", orphanedEntries.Select(e => $"'{e.DisplayText}'"))}");
            }
            if (orphanedReplies.Count > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"🔍 SYNC: Orphaned replies: {string.Join(", ", orphanedReplies.Select(r => $"'{r.DisplayText}'"))}");
            }

            if (allOrphans.Count > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Detected {allOrphans.Count} orphaned nodes after deletion ({orphanedEntries.Count} entries, {orphanedReplies.Count} replies)");

                // No longer creating orphan containers - using Scrap Tab instead
                // CreateOrUpdateOrphanContainersInModel(allOrphans);
            }
        }

        /// <summary>
        /// Helper to collect reachable nodes during orphan detection
        /// ONLY traverses regular pointers (IsLink=false) from START points
        /// IsLink=true pointers are "back references" from children to shared parents
        /// and should NOT prevent orphaning when the parent's owning START is deleted
        /// </summary>
        private void CollectReachableNodesForOrphanDetection(DialogNode node, HashSet<DialogNode> reachableNodes)
        {
            if (node == null || reachableNodes.Contains(node))
                return;

            reachableNodes.Add(node);

            // ONLY traverse regular pointers (IsLink=false) for orphan detection
            // IsLink=true pointers are back-references from link children to their shared parent
            // If we traverse IsLink pointers, link parents appear reachable even when their
            // owning START is deleted, preventing proper orphan detection
            foreach (var pointer in node.Pointers.Where(p => !p.IsLink))
            {
                if (pointer.Node != null)
                {
                    CollectReachableNodesForOrphanDetection(pointer.Node, reachableNodes);
                }
            }
        }

        /// <summary>
        /// Creates or updates orphan containers directly in the dialog model
        /// WITHOUT adding to TreeView (that happens during RefreshTreeView)
        /// </summary>
        private void CreateOrUpdateOrphanContainersInModel(List<DialogNode> orphanedNodes)
        {
            if (CurrentDialog == null || orphanedNodes.Count == 0) return;

            // Filter out any containers from orphaned nodes
            var orphanedNodesFiltered = orphanedNodes
                .Where(n => n.Comment?.Contains("PARLEY: Orphaned") != true)
                .ToList();

            if (orphanedNodesFiltered.Count == 0) return;

            // Find existing orphan containers
            var existingRootContainer = CurrentDialog.Entries
                .FirstOrDefault(e => e.Comment?.Contains("PARLEY: Orphaned nodes root container") == true);
            var existingNpcCategory = CurrentDialog.Replies
                .FirstOrDefault(r => r.Comment?.Contains("PARLEY: Orphaned NPC entries category") == true);

            // Separate entries and replies
            var orphanedEntries = orphanedNodesFiltered.Where(n => n.Type == DialogNodeType.Entry).ToList();
            var orphanedReplies = orphanedNodesFiltered.Where(n => n.Type == DialogNodeType.Reply).ToList();

            // Create or reuse root container
            DialogNode rootContainer;
            if (existingRootContainer != null)
            {
                rootContainer = existingRootContainer;
                rootContainer.Pointers.Clear();
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Sync: Reusing existing orphan root container");
            }
            else
            {
                rootContainer = new DialogNode
                {
                    Type = DialogNodeType.Entry,
                    Text = new LocString(),
                    Comment = "PARLEY: Orphaned nodes root container - never appears in-game (sc_false)",
                    Speaker = "",
                    Parent = CurrentDialog
                };
                rootContainer.Text.Add(0, "!!! Orphaned Nodes");
                CurrentDialog.Entries.Add(rootContainer);
                UnifiedLogger.LogApplication(LogLevel.INFO, "Sync: Created new orphan root container");
            }

            // Handle orphaned entries (NPC nodes)
            if (orphanedEntries.Count > 0)
            {
                DialogNode npcCategoryReply;
                if (existingNpcCategory != null)
                {
                    npcCategoryReply = existingNpcCategory;
                    npcCategoryReply.Pointers.Clear();
                }
                else
                {
                    npcCategoryReply = new DialogNode
                    {
                        Type = DialogNodeType.Reply,
                        Text = new LocString(),
                        Comment = "PARLEY: Orphaned NPC entries category",
                        Speaker = "",
                        Parent = CurrentDialog
                    };
                    npcCategoryReply.Text.Add(0, "!!! Orphaned NPC Nodes");
                    CurrentDialog.Replies.Add(npcCategoryReply);
                }

                // Only point to ROOT orphaned entries (not descendants of other orphaned entries)
                // This prevents orphaned entries from appearing multiple times in the tree
                var rootOrphanedEntries = orphanedEntries.Where(orphan =>
                {
                    // Check if this orphan appears anywhere in another orphan's subtree
                    foreach (var otherOrphan in orphanedEntries)
                    {
                        if (otherOrphan != orphan && IsNodeInSubtree(orphan, otherOrphan))
                        {
                            return false; // This orphan is a descendant of another orphan
                        }
                    }
                    return true; // This is a root orphan
                }).ToList();

                foreach (var orphan in rootOrphanedEntries)
                {
                    var orphanIndex = (uint)CurrentDialog.Entries.IndexOf(orphan);
                    var ptr = new DialogPtr
                    {
                        Node = orphan,
                        Type = DialogNodeType.Entry,
                        Index = orphanIndex,
                        IsLink = false,
                        ScriptAppears = "",
                        ConditionParams = new Dictionary<string, string>(),
                        Comment = "Pointer to orphaned NPC entry",
                        Parent = CurrentDialog
                    };
                    npcCategoryReply.Pointers.Add(ptr);
                    CurrentDialog.LinkRegistry.RegisterLink(ptr);
                }

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Filtered {orphanedEntries.Count} orphaned entries to {rootOrphanedEntries.Count} root orphans");

                // Link from root container
                if (!rootContainer.Pointers.Any(p => p.Node == npcCategoryReply))
                {
                    var npcCategoryIndex = (uint)CurrentDialog.Replies.IndexOf(npcCategoryReply);
                    var npcCategoryPtr = new DialogPtr
                    {
                        Node = npcCategoryReply,
                        Type = DialogNodeType.Reply,
                        Index = npcCategoryIndex,
                        IsLink = false,
                        Parent = CurrentDialog
                    };
                    rootContainer.Pointers.Add(npcCategoryPtr);
                    CurrentDialog.LinkRegistry.RegisterLink(npcCategoryPtr);
                }
            }

            // Handle orphaned replies (PC nodes) - link directly to root container
            // Only add ROOT orphaned replies (not descendants of other orphans)
            var rootOrphanedReplies = orphanedReplies.Where(orphan =>
            {
                // Check if this orphan reply appears in any orphaned entry's subtree
                foreach (var orphanEntry in orphanedEntries)
                {
                    if (IsNodeInSubtree(orphan, orphanEntry))
                    {
                        return false; // This orphan is a descendant of an orphaned entry
                    }
                }
                // Check if this orphan reply appears in any other orphaned reply's subtree
                foreach (var otherOrphan in orphanedReplies)
                {
                    if (otherOrphan != orphan && IsNodeInSubtree(orphan, otherOrphan))
                    {
                        return false; // This orphan is a descendant of another orphan
                    }
                }
                return true; // This is a root orphan
            }).ToList();

            foreach (var orphan in rootOrphanedReplies)
            {
                // Link directly from root container to orphaned reply
                if (!rootContainer.Pointers.Any(p => p.Node == orphan))
                {
                    var orphanIndex = (uint)CurrentDialog.Replies.IndexOf(orphan);
                    var ptr = new DialogPtr
                    {
                        Node = orphan,
                        Type = DialogNodeType.Reply,
                        Index = orphanIndex,
                        IsLink = false,
                        ScriptAppears = "",
                        ConditionParams = new Dictionary<string, string>(),
                        Comment = "Pointer to orphaned PC reply",
                        Parent = CurrentDialog
                    };
                    rootContainer.Pointers.Add(ptr);
                    CurrentDialog.LinkRegistry.RegisterLink(ptr);
                }
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Filtered {orphanedReplies.Count} orphaned replies to {rootOrphanedReplies.Count} root orphans");

            // Create or update START pointer to root container
            var existingOrphanStart = CurrentDialog.Starts
                .FirstOrDefault(s => s.Comment?.Contains("Orphan container") == true);

            if (existingOrphanStart != null)
            {
                // Update index in case container was moved
                var rootIndex = (uint)CurrentDialog.Entries.IndexOf(rootContainer);
                existingOrphanStart.Index = rootIndex;
                existingOrphanStart.Node = rootContainer;
            }
            else
            {
                var rootIndex = (uint)CurrentDialog.Entries.IndexOf(rootContainer);
                var rootStart = new DialogPtr
                {
                    Node = rootContainer,
                    Type = DialogNodeType.Entry,
                    Index = rootIndex,
                    IsLink = false,
                    IsStart = true,
                    ScriptAppears = "sc_false",
                    ConditionParams = new Dictionary<string, string>(),
                    Comment = "Orphan container - requires sc_false.nss in module",
                    Parent = CurrentDialog
                };
                CurrentDialog.Starts.Add(rootStart);
                CurrentDialog.LinkRegistry.RegisterLink(rootStart);
                UnifiedLogger.LogApplication(LogLevel.INFO, "Sync: Created orphan START pointer with sc_false");
            }

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Sync: Orphan container updated in model with {orphanedEntries.Count} entries, {orphanedReplies.Count} replies");
        }

        /// <summary>
        /// Checks if targetNode appears anywhere in the subtree rooted at rootNode
        /// Uses recursive traversal following regular pointers only (not IsLink)
        /// </summary>
        private bool IsNodeInSubtree(DialogNode targetNode, DialogNode rootNode)
        {
            if (rootNode == null || targetNode == null) return false;

            var visited = new HashSet<DialogNode>();
            return IsNodeInSubtreeRecursive(targetNode, rootNode, visited);
        }

        private bool IsNodeInSubtreeRecursive(DialogNode targetNode, DialogNode currentNode, HashSet<DialogNode> visited)
        {
            if (currentNode == null || visited.Contains(currentNode))
                return false;

            visited.Add(currentNode);

            // Check each child (following regular pointers only, not IsLink)
            foreach (var pointer in currentNode.Pointers.Where(p => !p.IsLink))
            {
                if (pointer.Node == targetNode)
                    return true; // Found it!

                // Recursively check this child's subtree
                if (pointer.Node != null && IsNodeInSubtreeRecursive(targetNode, pointer.Node, visited))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the parent Entry node that contains a pointer to this Reply
        /// Used for showing context in orphaned replies section
        /// </summary>
        private DialogNode? FindParentEntry(DialogNode replyNode)
        {
            if (CurrentDialog == null) return null;

            // Search all entries for one that has a pointer to this reply
            foreach (var entry in CurrentDialog.Entries)
            {
                if (entry.Pointers != null && entry.Pointers.Any(p => p.Node == replyNode))
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// Recursively collects all nodes reachable from the tree structure
        /// CRITICAL: Traverses dialog model pointers, not TreeView children (Issue #82 lazy loading fix)
        /// This ensures we find all reachable nodes even when TreeView children aren't populated yet
        /// Links are terminal (don't expand) but the nodes they point to ARE still reachable
        /// </summary>
        private void CollectReachableNodes(TreeViewSafeNode node, HashSet<DialogNode> reachableNodes)
        {
            if (node?.OriginalNode == null || reachableNodes.Contains(node.OriginalNode))
                return;

            // Add this node to reachable set (even if it's a link target)
            reachableNodes.Add(node.OriginalNode);

            // ISSUE #82 FIX: Traverse dialog model pointers, not TreeView children
            // With lazy loading, TreeView children aren't populated until node is expanded
            // Must traverse the underlying DialogNode.Pointers to find all reachable nodes

            // Don't traverse THROUGH link nodes (they're terminal in TreeView)
            // But the nodes they point to are still marked as reachable (above)
            if (node.IsChild)
                return;

            foreach (var pointer in node.OriginalNode.Pointers)
            {
                if (pointer.Node != null)
                {
                    // Create temporary TreeViewSafeNode with pointer to check if it's a link
                    var childSafeNode = new TreeViewSafeNode(pointer.Node, sourcePointer: pointer);
                    CollectReachableNodes(childSafeNode, reachableNodes);
                }
            }
        }

        /// <summary>
        /// Restores tree expansion state and selection
        /// </summary>
        private void RestoreTreeState(Parley.Services.TreeState state)
        {
            if (state == null) return;

            // Restore expanded nodes
            _treeNavManager.RestoreExpandedNodePaths(DialogNodes, state.ExpandedNodePaths);

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Restored {state.ExpandedNodePaths.Count} expanded nodes");
        }

        #region Scrap Management

        /// <summary>
        /// Restore a node from the scrap back to the dialog
        /// </summary>
        public bool RestoreFromScrap(string entryId, TreeViewSafeNode? selectedParent)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"RestoreFromScrap called - entryId: {entryId}");

            if (CurrentDialog == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot restore - no dialog loaded");
                return false;
            }

            // Check if a parent is selected
            if (selectedParent == null)
            {
                StatusMessage = "Select a location in the tree to restore to (root or parent node)";
                UnifiedLogger.LogApplication(LogLevel.WARN, "Restore failed - no parent selected");
                return false;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Restoring to parent: {selectedParent.DisplayText} (Type: {selectedParent.GetType().Name})");

            // Get the node from scrap WITHOUT removing it yet
            var node = _scrapManager.GetNodeFromScrap(entryId);
            if (node == null)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, "Failed to retrieve node from scrap manager");
                StatusMessage = "Failed to retrieve node from scrap";
                return false;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Node retrieved from scrap: Type={node.Type}, Text={node.Text?.Strings.Values.FirstOrDefault()}");

            // Validate restoration target BEFORE making ANY changes
            if (selectedParent is TreeViewRootNode && node.Type != DialogNodeType.Entry)
            {
                StatusMessage = "Only NPC Entry nodes can be restored to root level";
                UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot restore PC Reply to root level");
                return false;
            }

            // Validate dialog structure rules
            if (!(selectedParent is TreeViewRootNode) && selectedParent?.OriginalNode != null)
            {
                var parentNode = selectedParent.OriginalNode;

                // NPC Entry can only be child of PC Reply (not another NPC Entry)
                if (node.Type == DialogNodeType.Entry && parentNode.Type == DialogNodeType.Entry)
                {
                    StatusMessage = "NPC Entry nodes cannot be children of other NPC Entry nodes";
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Invalid structure: Entry under Entry");
                    return false;
                }

                // PC Reply can be under NPC Entry OR NPC Reply (branching PC responses)
                // No validation needed for PC Reply - both parent types are valid
            }

            // ALL validations passed - now make the changes

            // Save state for undo
            SaveUndoState("Restore from Scrap");

            // Add the restored node to the appropriate list
            if (node.Type == DialogNodeType.Entry)
            {
                CurrentDialog.Entries.Add(node);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added to Entries list (index {CurrentDialog.Entries.Count - 1})");
            }
            else
            {
                CurrentDialog.Replies.Add(node);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added to Replies list (index {CurrentDialog.Replies.Count - 1})");
            }

            // Get the index of the restored node
            var nodeIndex = (uint)CurrentDialog.GetNodeIndex(node, node.Type);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Node index: {nodeIndex}");

            // Create pointer to restored node
            var ptr = new DialogPtr
            {
                Node = node,
                Type = node.Type,
                Index = nodeIndex,
                IsLink = false,
                ScriptAppears = "",
                ConditionParams = new Dictionary<string, string>(),
                Comment = "[Restored from scrap]",
                Parent = CurrentDialog
            };

            // Add to root level or under selected parent
            if (selectedParent is TreeViewRootNode)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Restoring to root level");
                // We already validated this is an Entry node above
                ptr.IsStart = true;
                CurrentDialog.Starts.Add(ptr);
                StatusMessage = "Restored node to root level";
                UnifiedLogger.LogApplication(LogLevel.INFO, "Node restored to root level");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Restoring as child of {selectedParent.DisplayText}");
                // Restore as child of selected node
                selectedParent.OriginalNode.Pointers.Add(ptr);
                selectedParent.IsExpanded = true;
                StatusMessage = $"Restored node under {selectedParent.DisplayText}";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Node restored under {selectedParent.DisplayText}");
            }

            // Register the pointer
            CurrentDialog.LinkRegistry.RegisterLink(ptr);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Pointer registered in LinkRegistry");

            // Recalculate indices
            RecalculatePointerIndices();

            // Refresh UI
            RefreshTreeView();
            HasUnsavedChanges = true;

            // Only remove from scrap after successful restoration
            _scrapManager.RemoveFromScrap(entryId);

            UnifiedLogger.LogApplication(LogLevel.INFO, "Restore completed successfully");
            return true;
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

        /// <summary>
        /// Find orphaned nodes after a delete/cut operation
        /// </summary>
        private List<DialogNode> FindOrphanedNodes()
        {
            if (CurrentDialog == null) return new List<DialogNode>();

            var orphaned = new List<DialogNode>();
            var reachable = new HashSet<DialogNode>();

            // Mark all nodes reachable from STARTs
            foreach (var start in CurrentDialog.Starts)
            {
                if (start.Node != null)
                {
                    MarkReachable(start.Node, reachable);
                }
            }

            // Find all nodes not marked as reachable
            foreach (var entry in CurrentDialog.Entries)
            {
                if (!reachable.Contains(entry) &&
                    !entry.Comment?.Contains("PARLEY: Orphaned") == true)
                {
                    orphaned.Add(entry);
                }
            }

            foreach (var reply in CurrentDialog.Replies)
            {
                if (!reachable.Contains(reply) &&
                    !reply.Comment?.Contains("PARLEY: Orphaned") == true)
                {
                    orphaned.Add(reply);
                }
            }

            return orphaned;
        }

        private void MarkReachable(DialogNode node, HashSet<DialogNode> reachable)
        {
            if (!reachable.Add(node)) return; // Already visited

            foreach (var ptr in node.Pointers)
            {
                if (ptr.Node != null)
                {
                    MarkReachable(ptr.Node, reachable);
                }
            }
        }

        #endregion
    }
}

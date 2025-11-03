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
        private ObservableCollection<string> _debugMessages = new();
        private ObservableCollection<TreeViewSafeNode> _dialogNodes = new();
        private ObservableCollection<DialogNode> _treeSafeDialogNodes = new();
        private bool _hasUnsavedChanges;
        private DialogNode? _copiedNode = null; // Phase 1 Step 7: Copy/Paste system
        private readonly UndoManager _undoManager = new(50); // Undo/redo with 50 state history

        public Dialog? CurrentDialog
        {
            get => _currentDialog;
            set => SetProperty(ref _currentDialog, value);
        }

        public string? CurrentFileName
        {
            get => _currentFileName;
            set
            {
                SetProperty(ref _currentFileName, value);
                OnPropertyChanged(nameof(LoadedFileName));
                OnPropertyChanged(nameof(WindowTitle));
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

        public ObservableCollection<DialogNode> TreeSafeDialogNodes
        {
            get => _treeSafeDialogNodes;
            set => SetProperty(ref _treeSafeDialogNodes, value);
        }

        public MainViewModel()
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "Parley MainViewModel initialized");

            // Check for command line file loading
            _ = Task.Run(async () =>
            {
                await Task.Delay(500); // Brief delay to ensure UI is ready
                await CheckCommandLineFileAsync();
            });
        }

        private async Task CheckCommandLineFileAsync()
        {
            try
            {
                // TODO: Avalonia migration - implement command line argument handling
                // Check if a command line file was provided
                // var commandLineFile = System.Windows.Application.Current?.Properties["CommandLineFile"] as string;

                // if (!string.IsNullOrEmpty(commandLineFile))
                // {
                //     UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading command line file: {commandLineFile}");
                //     await LoadDialogAsync(commandLineFile);
                // }
                // else
                // {
                //     UnifiedLogger.LogApplication(LogLevel.DEBUG, "No command line file provided");
                // }

                await Task.CompletedTask; // Temporary placeholder
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load command line file: {ex.Message}");
            }
        }

        private async Task AutoLoadTestFileAsync()
        {
            try
            {
                var testFilePath = @"D:\LOM\Tools\LNS_DLG\TestFiles\chef.dlg";
                if (System.IO.File.Exists(testFilePath))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-loading test file: {UnifiedLogger.SanitizePath(testFilePath)}");
                    await LoadDialogAsync(testFilePath);
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Test file not found: {UnifiedLogger.SanitizePath(testFilePath)}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to auto-load test file: {ex.Message}");
            }
        }

        public void AddDebugMessage(string message)
        {
            try
            {
                Console.WriteLine($"[AddDebugMessage CALLED] {message}"); // Explicit console verification
                Dispatcher.UIThread.Post(() =>
                {
                    DebugMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                    Console.WriteLine($"[AddDebugMessage UI] Added to collection. Count={DebugMessages.Count}");

                    // Keep only last 1000 messages to prevent memory issues
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
                    _undoManager.Clear();

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
                        rootNode.Children.Add(safeNode);
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added to Root: '{entry.DisplayText}'");
                    }
                    else
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"Start pointer index {start.Index} exceeds entry count {CurrentDialog.Entries.Count}");
                    }
                }

                // If no starts found, show all entries under root
                if (rootNode.Children.Count == 0 && CurrentDialog.Entries.Count > 0)
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
                CurrentFileName = null; // No filename until user saves
                HasUnsavedChanges = false; // Start clean

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
            if (CurrentDialog != null && !_undoManager.IsRestoring)
            {
                _undoManager.SaveState(CurrentDialog, description);
            }
        }

        public void Undo()
        {
            if (CurrentDialog == null || !_undoManager.CanUndo)
            {
                StatusMessage = "Nothing to undo";
                return;
            }

            // Capture tree state before undo
            var treeState = CaptureTreeState();

            var previousState = _undoManager.Undo(CurrentDialog);
            if (previousState != null)
            {
                CurrentDialog = previousState;
                RefreshTreeView();

                // Restore tree state after refresh
                RestoreTreeState(treeState);

                HasUnsavedChanges = true;
                StatusMessage = "Undo successful";
                UnifiedLogger.LogApplication(LogLevel.INFO, "Undo performed");
            }
            else
            {
                StatusMessage = "Undo failed";
            }
        }

        public void Redo()
        {
            if (CurrentDialog == null || !_undoManager.CanRedo)
            {
                StatusMessage = "Nothing to redo";
                return;
            }

            // Capture tree state before redo
            var treeState = CaptureTreeState();

            var nextState = _undoManager.Redo(CurrentDialog);
            if (nextState != null)
            {
                CurrentDialog = nextState;
                RefreshTreeView();

                // Restore tree state after refresh
                RestoreTreeState(treeState);

                HasUnsavedChanges = true;
                StatusMessage = "Redo successful";
                UnifiedLogger.LogApplication(LogLevel.INFO, "Redo performed");
            }
            else
            {
                StatusMessage = "Redo failed";
            }
        }

        public bool CanUndo => _undoManager.CanUndo;
        public bool CanRedo => _undoManager.CanRedo;

        public void AddSmartNode(TreeViewSafeNode? selectedNode = null)
        {
            if (CurrentDialog == null) return;

            // Save state for undo
            SaveUndoState("Add Smart Node");

            // Determine what type of node to create based on selection
            if (selectedNode == null || selectedNode is TreeViewRootNode)
            {
                // Root level → Create Entry
                AddEntryNode(selectedNode);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Smart Add: Created Entry at root");
            }
            else
            {
                var parentNode = selectedNode.OriginalNode;

                if (parentNode.Type == DialogNodeType.Entry)
                {
                    // Entry → PC Reply
                    AddPCReplyNode(selectedNode);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Smart Add: Created Reply after Entry");
                }
                else // Reply node
                {
                    // Reply → Entry (NPC response)
                    AddEntryNode(selectedNode);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Smart Add: Created Entry after Reply");
                }
            }

        }

        public void AddEntryNode(TreeViewSafeNode? parentNode = null)
        {
            if (CurrentDialog == null) return;

            // Save state for undo
            SaveUndoState("Add Entry Node");

            var newEntry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Speaker = "",
                Comment = "",
                Sound = "",
                ScriptAction = "",
                Delay = uint.MaxValue,
                Animation = DialogAnimation.Default,
                AnimationLoop = false,
                Quest = "",
                QuestEntry = uint.MaxValue,
                Pointers = new List<DialogPtr>(),
                ActionParams = new Dictionary<string, string>()
            };

            newEntry.Text.Add(0, ""); // Empty text - user will type immediately

            // Add to dialog's entry list
            CurrentDialog.Entries.Add(newEntry);

            // Create pointer for this entry
            var entryPtr = new DialogPtr
            {
                Node = newEntry,
                Type = DialogNodeType.Entry,
                Index = (uint)(CurrentDialog.Entries.Count - 1),
                IsLink = false,
                ScriptAppears = "",
                ConditionParams = new Dictionary<string, string>(),
                Comment = ""
            };

            if (parentNode == null || parentNode is TreeViewRootNode)
            {
                // Root level - add to starting list
                entryPtr.IsStart = true;
                CurrentDialog.Starts.Add(entryPtr);
                StatusMessage = $"Added new Entry node at root level";
                UnifiedLogger.LogApplication(LogLevel.INFO, "Created new Entry node at root");
            }
            else
            {
                // Child of Reply node - add to parent's EntriesList
                var parentDialogNode = parentNode.OriginalNode;

                // Validate: Entries should only come after PC Replies
                if (parentDialogNode.Type == DialogNodeType.Reply && string.IsNullOrEmpty(parentDialogNode.Speaker))
                {
                    parentDialogNode.Pointers.Add(entryPtr);
                    parentNode.IsExpanded = true;
                    StatusMessage = $"Added new Entry node after Reply";
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Created new Entry node as child of Reply");
                }
                else
                {
                    StatusMessage = "Cannot add Entry after Entry node. Use PC Reply instead.";
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Invalid: Entry after Entry node");
                    return;
                }
            }

            // Refresh tree display
            RefreshTreeView();

            HasUnsavedChanges = true;
        }

        // Phase 1 Bug Fix: Removed AddNPCReplyNode - "NPC Reply" is actually Entry node after PC Reply

        public void AddPCReplyNode(TreeViewSafeNode parent)
        {
            if (CurrentDialog == null) return;

            // Save state for undo
            SaveUndoState("Add PC Reply");

            var newReply = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString(),
                Speaker = "",
                Comment = "",
                Sound = "",
                ScriptAction = "",
                Delay = uint.MaxValue,
                Animation = DialogAnimation.Default,
                AnimationLoop = false,
                Quest = "",
                QuestEntry = uint.MaxValue,
                Pointers = new List<DialogPtr>(),
                ActionParams = new Dictionary<string, string>()
            };

            newReply.Text.Add(0, ""); // Empty text - user will type immediately

            // Add to parent's underlying DialogNode
            var newPtr = new DialogPtr
            {
                Node = newReply,
                Type = DialogNodeType.Reply,
                Index = (uint)CurrentDialog.Replies.Count, // Temporary index
                IsLink = false,
                ScriptAppears = "",
                ConditionParams = new Dictionary<string, string>(),
                Comment = ""
            };

            parent.OriginalNode.Pointers.Add(newPtr);

            // Add to dialog's reply list
            CurrentDialog.Replies.Add(newReply);

            // Auto-expand parent node before refresh
            parent.IsExpanded = true;

            // Refresh tree display
            RefreshTreeView();

            HasUnsavedChanges = true;
            StatusMessage = $"Added new PC Reply node";
            UnifiedLogger.LogApplication(LogLevel.INFO, "Created new PC Reply node");
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

            // Check if node or its children have incoming links
            var linkedNodes = CheckForIncomingLinks(node);
            if (linkedNodes.Count > 0)
            {
                // Check for duplicates in the linked nodes list (indicates copy/paste created duplicates)
                var grouped = linkedNodes.GroupBy(n => n.DisplayText);
                var hasDuplicates = grouped.Any(g => g.Count() > 1);

                if (hasDuplicates)
                {
                    foreach (var group in grouped.Where(g => g.Count() > 1))
                    {
                        UnifiedLogger.LogApplication(LogLevel.ERROR,
                            $"DUPLICATE NODE DETECTED: '{group.Key}' appears {group.Count()} times - likely from copy/paste of linked content");
                    }
                    StatusMessage = $"ERROR: Duplicate nodes detected! This may cause orphaning. See logs.";
                }

                var nodeList = string.Join(", ", linkedNodes.Select(n => $"'{n.DisplayText}'"));
                UnifiedLogger.LogApplication(LogLevel.WARN, $"DELETE WARNING: Deleting node will break links to: {nodeList}");
                StatusMessage = $"Warning: Deleting will break {linkedNodes.Count} link(s). Check logs for details.";
                // Continue with delete - user was warned
            }

            // CRITICAL: Recursively delete all children - even if linked elsewhere
            // This matches Aurora behavior - deleting parent removes entire subtree
            DeleteNodeRecursive(node);

            // CRITICAL: After deletion, recalculate indices AND check for orphaned links
            RecalculatePointerIndices();
            RemoveOrphanedPointers();

            // Refresh tree
            RefreshTreeView();

            HasUnsavedChanges = true;
            StatusMessage = $"Node and children deleted successfully";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Deleted node tree: {node.DisplayText}");
        }

        private List<DialogNode> CheckForIncomingLinks(DialogNode node)
        {
            var linkedNodes = new List<DialogNode>();

            // Check this node and all descendants for incoming links using LinkRegistry
            CheckNodeForLinks(node, linkedNodes);

            return linkedNodes;
        }

        private void CheckNodeForLinks(DialogNode node, List<DialogNode> linkedNodes)
        {
            // Use LinkRegistry to check for incoming links
            var incomingLinks = CurrentDialog.LinkRegistry.GetLinksTo(node);

            // If there are multiple incoming links or any are marked as IsLink, this node is referenced elsewhere
            if (incomingLinks.Count > 1 || incomingLinks.Any(ptr => ptr.IsLink))
            {
                linkedNodes.Add(node);
            }

            // Recursively check all children
            if (node.Pointers != null)
            {
                foreach (var ptr in node.Pointers)
                {
                    if (ptr.Node != null)
                    {
                        CheckNodeForLinks(ptr.Node, linkedNodes);
                    }
                }
            }
        }

        private void DeleteNodeRecursive(DialogNode node)
        {
            // Recursively delete ALL children first (depth-first)
            // Even if children are linked elsewhere - Aurora behavior
            if (node.Pointers != null && node.Pointers.Count > 0)
            {
                // Make a copy of the pointers list to avoid modification during iteration
                var pointersToDelete = node.Pointers.ToList();

                foreach (var ptr in pointersToDelete)
                {
                    if (ptr.Node != null)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Recursively deleting child: {ptr.Node.DisplayText}");
                        DeleteNodeRecursive(ptr.Node);
                    }
                }

                // Unregister and clear the pointers list after deleting children
                foreach (var ptr in pointersToDelete)
                {
                    CurrentDialog.LinkRegistry.UnregisterLink(ptr);
                }
                node.Pointers.Clear();
            }

            // Get all incoming pointers to this node from LinkRegistry
            var incomingPointers = CurrentDialog.LinkRegistry.GetLinksTo(node).ToList();
            int removedCount = 0;

            // Remove all incoming pointers using LinkRegistry data
            foreach (var incomingPtr in incomingPointers)
            {
                // Unregister from LinkRegistry first
                CurrentDialog.LinkRegistry.UnregisterLink(incomingPtr);

                // Remove from Starts if it's a start pointer
                if (CurrentDialog.Starts.Contains(incomingPtr))
                {
                    CurrentDialog.Starts.Remove(incomingPtr);
                    removedCount++;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Removed from Starts list");
                }

                // Find and remove from parent node's pointers
                foreach (var entry in CurrentDialog.Entries)
                {
                    if (entry.Pointers.Contains(incomingPtr))
                    {
                        entry.Pointers.Remove(incomingPtr);
                        removedCount++;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Removed from Entry '{entry.DisplayText}' pointers");
                    }
                }

                foreach (var reply in CurrentDialog.Replies)
                {
                    if (reply.Pointers.Contains(incomingPtr))
                    {
                        reply.Pointers.Remove(incomingPtr);
                        removedCount++;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Removed from Reply '{reply.DisplayText}' pointers");
                    }
                }
            }

            if (removedCount > 1)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Removed node '{node.DisplayText}' from {removedCount} parent references");
            }

            // Use RemoveNodeInternal which handles LinkRegistry cleanup
            CurrentDialog.RemoveNodeInternal(node, node.Type);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Removed {node.Type} from list: {node.DisplayText}");
        }

        // Phase 2a: Node Reordering
        public void MoveNodeUp(TreeViewSafeNode nodeToMove)
        {
            if (CurrentDialog == null) return;
            if (nodeToMove == null || nodeToMove is TreeViewRootNode) return;

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"MoveNodeUp: node={nodeToMove.DisplayText}");
            var node = nodeToMove.OriginalNode;

            // Check if root-level node (in StartingList)
            int startIndex = CurrentDialog.Starts.FindIndex(s => s.Node == node);

            if (startIndex != -1)
            {
                // Root-level node
                if (startIndex == 0)
                {
                    StatusMessage = "Node is already first";
                    return;
                }

                var temp = CurrentDialog.Starts[startIndex];
                CurrentDialog.Starts[startIndex] = CurrentDialog.Starts[startIndex - 1];
                CurrentDialog.Starts[startIndex - 1] = temp;

                HasUnsavedChanges = true;
                RefreshTreeViewAndSelectNode(node);
                StatusMessage = $"Moved '{node.Text?.GetDefault()}' up";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Moved root node up: {startIndex} → {startIndex - 1}");
                return;
            }

            // Child node - find parent and reorder within parent's Pointers
            DialogNode? parent = FindParentNode(node);
            if (parent != null)
            {
                int ptrIndex = parent.Pointers.FindIndex(p => p.Node == node);
                if (ptrIndex > 0)
                {
                    var temp = parent.Pointers[ptrIndex];
                    parent.Pointers[ptrIndex] = parent.Pointers[ptrIndex - 1];
                    parent.Pointers[ptrIndex - 1] = temp;

                    HasUnsavedChanges = true;
                    RefreshTreeViewAndSelectNode(node);
                    StatusMessage = $"Moved '{node.Text?.GetDefault()}' up";
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Moved child node up in parent '{parent.Text?.GetDefault()}': {ptrIndex} → {ptrIndex - 1}");
                }
                else
                {
                    StatusMessage = "Node is already first in parent";
                }
            }
            else
            {
                StatusMessage = "Cannot find parent node";
                UnifiedLogger.LogApplication(LogLevel.WARN, $"No parent found for '{node.Text?.GetDefault()}'");
            }
        }

        public void MoveNodeDown(TreeViewSafeNode nodeToMove)
        {
            if (CurrentDialog == null) return;
            if (nodeToMove == null || nodeToMove is TreeViewRootNode) return;

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"MoveNodeDown: node={nodeToMove.DisplayText}");
            var node = nodeToMove.OriginalNode;

            // Check if root-level node (in StartingList)
            int startIndex = CurrentDialog.Starts.FindIndex(s => s.Node == node);

            if (startIndex != -1)
            {
                // Root-level node
                if (startIndex >= CurrentDialog.Starts.Count - 1)
                {
                    StatusMessage = "Node is already last";
                    return;
                }

                var temp = CurrentDialog.Starts[startIndex];
                CurrentDialog.Starts[startIndex] = CurrentDialog.Starts[startIndex + 1];
                CurrentDialog.Starts[startIndex + 1] = temp;

                HasUnsavedChanges = true;
                RefreshTreeViewAndSelectNode(node);
                StatusMessage = $"Moved '{node.Text?.GetDefault()}' down";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Moved root node down: {startIndex} → {startIndex + 1}");
                return;
            }

            // Child node - find parent and reorder within parent's Pointers
            DialogNode? parent = FindParentNode(node);
            if (parent != null)
            {
                int ptrIndex = parent.Pointers.FindIndex(p => p.Node == node);
                if (ptrIndex >= 0 && ptrIndex < parent.Pointers.Count - 1)
                {
                    var temp = parent.Pointers[ptrIndex];
                    parent.Pointers[ptrIndex] = parent.Pointers[ptrIndex + 1];
                    parent.Pointers[ptrIndex + 1] = temp;

                    HasUnsavedChanges = true;
                    RefreshTreeViewAndSelectNode(node);
                    StatusMessage = $"Moved '{node.Text?.GetDefault()}' down";
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Moved child node down in parent '{parent.Text?.GetDefault()}': {ptrIndex} → {ptrIndex + 1}");
                }
                else
                {
                    StatusMessage = "Node is already last in parent";
                }
            }
            else
            {
                StatusMessage = "Cannot find parent node";
                UnifiedLogger.LogApplication(LogLevel.WARN, $"No parent found for '{node.Text?.GetDefault()}'");
            }
        }

        private DialogNode? FindParentNode(DialogNode childNode)
        {
            // Search all entries for this child in their Pointers
            foreach (var entry in CurrentDialog.Entries)
            {
                if (entry.Pointers.Any(p => p.Node == childNode))
                    return entry;
            }

            // Search all replies for this child in their Pointers
            foreach (var reply in CurrentDialog.Replies)
            {
                if (reply.Pointers.Any(p => p.Node == childNode))
                    return reply;
            }

            return null;
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
            var expandedNodeRefs = new HashSet<DialogNode>();
            SaveTreeExpansionState(DialogNodes, expandedNodeRefs);

            // Re-populate tree to reflect changes
            Dispatcher.UIThread.Post(() =>
            {
                PopulateDialogNodes();

                // Restore expansion state after tree is rebuilt
                Dispatcher.UIThread.Post(() =>
                {
                    RestoreTreeExpansionState(DialogNodes, expandedNodeRefs);
                }, global::Avalonia.Threading.DispatcherPriority.Loaded);
            });
        }

        private void RefreshTreeViewAndSelectNode(DialogNode nodeToSelect)
        {
            // Save expansion state before refresh
            var expandedNodeRefs = new HashSet<DialogNode>();
            SaveTreeExpansionState(DialogNodes, expandedNodeRefs);

            // Store the node to re-select after refresh
            NodeToSelectAfterRefresh = nodeToSelect;

            // Re-populate tree to reflect changes
            Dispatcher.UIThread.Post(() =>
            {
                PopulateDialogNodes();

                // Restore expansion state after tree is rebuilt
                Dispatcher.UIThread.Post(() =>
                {
                    RestoreTreeExpansionState(DialogNodes, expandedNodeRefs);
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
            TreeViewSafeNode? FindNodeRecursive(ObservableCollection<TreeViewSafeNode> nodes)
            {
                foreach (var node in nodes)
                {
                    if (node.OriginalNode == nodeToFind)
                        return node;

                    if (node.Children != null && node.Children.Count > 0)
                    {
                        var found = FindNodeRecursive(node.Children);
                        if (found != null)
                            return found;
                    }
                }
                return null;
            }

            return FindNodeRecursive(DialogNodes);
        }

        private void SaveTreeExpansionState(ObservableCollection<TreeViewSafeNode> nodes, HashSet<DialogNode> expandedRefs)
        {
            foreach (var node in nodes)
            {
                if (node.IsExpanded)
                {
                    expandedRefs.Add(node.OriginalNode);
                }
                if (node.Children != null && node.Children.Count > 0)
                {
                    SaveTreeExpansionState(node.Children, expandedRefs);
                }
            }
        }

        private void RestoreTreeExpansionState(ObservableCollection<TreeViewSafeNode> nodes, HashSet<DialogNode> expandedRefs)
        {
            foreach (var node in nodes)
            {
                if (expandedRefs.Contains(node.OriginalNode))
                {
                    node.IsExpanded = true;
                }
                if (node.Children != null && node.Children.Count > 0)
                {
                    RestoreTreeExpansionState(node.Children, expandedRefs);
                }
            }
        }

        public string CaptureTreeStructure()
        {
            if (CurrentDialog == null)
                return "No dialog loaded";

            var treeText = new System.Text.StringBuilder();
            treeText.AppendLine($"=== Dialog Tree Structure for {System.IO.Path.GetFileName(CurrentFileName)} ===");
            treeText.AppendLine($"Entries: {CurrentDialog.Entries.Count}, Replies: {CurrentDialog.Replies.Count}, Starts: {CurrentDialog.Starts.Count}");
            treeText.AppendLine();

            // Track visited nodes to prevent infinite recursion
            var visitedNodes = new HashSet<DialogNode>();

            // Capture starting nodes
            foreach (var start in CurrentDialog.Starts)
            {
                if (start.Index < CurrentDialog.Entries.Count)
                {
                    var entry = CurrentDialog.Entries[(int)start.Index];
                    visitedNodes.Clear(); // Reset for each start tree
                    CaptureNodeStructure(entry, treeText, 0, $"START[{start.Index}]", visitedNodes);
                }
            }

            // If no starts, show all entries
            if (CurrentDialog.Starts.Count == 0)
            {
                for (int i = 0; i < CurrentDialog.Entries.Count; i++)
                {
                    visitedNodes.Clear(); // Reset for each entry tree
                    CaptureNodeStructure(CurrentDialog.Entries[i], treeText, 0, $"ENTRY[{i}]", visitedNodes);
                }
            }

            return treeText.ToString();
        }

        private void CaptureNodeStructure(DialogNode node, System.Text.StringBuilder sb, int depth, string prefix, HashSet<DialogNode> visitedNodes)
        {
            var indent = new string(' ', depth * 2);

            // Check for circular reference
            if (visitedNodes.Contains(node))
            {
                sb.AppendLine($"{indent}{prefix}: [CIRCULAR REFERENCE - Already visited]");
                return;
            }

            // Add max depth protection as well
            if (depth > 50)  // Increased for long official campaign conversations
            {
                sb.AppendLine($"{indent}{prefix}: [MAX DEPTH REACHED]");
                return;
            }

            // Mark this node as visited
            visitedNodes.Add(node);

            var typeDisplay = node.TypeDisplay;
            var text = node.DisplayText?.Trim();
            // No truncation - show full conversation text

            sb.AppendLine($"{indent}{prefix} {typeDisplay}: \"{text}\"");

            // Show pointers/children
            foreach (var pointer in node.Pointers)
            {
                if (pointer.Node != null)
                {
                    var childPrefix = pointer.Type == DialogNodeType.Reply ? $"REPLY[{pointer.Index}]" : $"ENTRY[{pointer.Index}]";
                    if (pointer.IsLink)
                        childPrefix += " (LINK)";
                    CaptureNodeStructure(pointer.Node, sb, depth + 1, childPrefix, visitedNodes);
                }
                else
                {
                    var childPrefix = pointer.Type == DialogNodeType.Reply ? $"REPLY[{pointer.Index}]" : $"ENTRY[{pointer.Index}]";
                    sb.AppendLine($"{indent}  {childPrefix}: [UNLINKED]");
                }
            }

            // Remove this node from visited set when backtracking (allows for legitimate revisits in different branches)
            visitedNodes.Remove(node);
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

            _copiedNode = nodeToCopy.OriginalNode;
            StatusMessage = $"Node copied: {_copiedNode.DisplayText}";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Copied node: {_copiedNode.DisplayText}");
        }

        public void CutNode(TreeViewSafeNode? nodeToCut)
        {
            if (nodeToCut == null || nodeToCut is TreeViewRootNode)
            {
                StatusMessage = "Cannot cut ROOT node";
                return;
            }

            var node = nodeToCut.OriginalNode;

            // Save state for undo before cutting
            SaveUndoState("Cut Node");

            // Store node for pasting (Paste will clone it)
            _copiedNode = node;

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

            RefreshTreeView();
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

        /// <summary>
        /// Removes pointers that reference nodes no longer in the Entries/Replies lists
        /// This prevents orphaned pointers after deletion operations
        /// </summary>
        private void RemoveOrphanedPointers()
        {
            if (CurrentDialog == null) return;

            int removedCount = 0;

            // Clean Start pointers
            var startsToRemove = new List<DialogPtr>();
            foreach (var start in CurrentDialog.Starts)
            {
                if (start.Node != null && !CurrentDialog.Entries.Contains(start.Node))
                {
                    startsToRemove.Add(start);
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Removing orphaned Start pointer to '{start.Node.DisplayText}'");
                }
            }
            foreach (var start in startsToRemove)
            {
                CurrentDialog.Starts.Remove(start);
                removedCount++;
            }

            // Clean Entry pointers
            foreach (var entry in CurrentDialog.Entries)
            {
                var ptrsToRemove = new List<DialogPtr>();
                foreach (var ptr in entry.Pointers)
                {
                    if (ptr.Node != null)
                    {
                        var list = ptr.Type == DialogNodeType.Entry ? CurrentDialog.Entries : CurrentDialog.Replies;
                        if (!list.Contains(ptr.Node))
                        {
                            ptrsToRemove.Add(ptr);
                            UnifiedLogger.LogApplication(LogLevel.WARN, $"Removing orphaned pointer from Entry '{entry.DisplayText}' to '{ptr.Node.DisplayText}'");
                        }
                    }
                }
                foreach (var ptr in ptrsToRemove)
                {
                    entry.Pointers.Remove(ptr);
                    removedCount++;
                }
            }

            // Clean Reply pointers
            foreach (var reply in CurrentDialog.Replies)
            {
                var ptrsToRemove = new List<DialogPtr>();
                foreach (var ptr in reply.Pointers)
                {
                    if (ptr.Node != null)
                    {
                        var list = ptr.Type == DialogNodeType.Entry ? CurrentDialog.Entries : CurrentDialog.Replies;
                        if (!list.Contains(ptr.Node))
                        {
                            ptrsToRemove.Add(ptr);
                            UnifiedLogger.LogApplication(LogLevel.WARN, $"Removing orphaned pointer from Reply '{reply.DisplayText}' to '{ptr.Node.DisplayText}'");
                        }
                    }
                }
                foreach (var ptr in ptrsToRemove)
                {
                    reply.Pointers.Remove(ptr);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Removed {removedCount} orphaned pointers");
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
            if (_copiedNode == null)
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
                if (_copiedNode.Type == DialogNodeType.Reply && string.IsNullOrEmpty(_copiedNode.Speaker))
                {
                    StatusMessage = "Cannot paste PC Reply to ROOT - PC can only respond to NPC statements";
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Blocked PC Reply paste to ROOT");
                    return;
                }

                // Clone the node (deep copy)
                var duplicate = CloneNode(_copiedNode);

                // Convert NPC Reply nodes to Entry when pasting to ROOT (GFF requirement)
                if (duplicate.Type == DialogNodeType.Reply)
                {
                    // This is an NPC Reply (has Speaker set) - convert to Entry
                    duplicate.Type = DialogNodeType.Entry;

                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-converted NPC Reply to Entry for ROOT level");
                    StatusMessage = $"Auto-converted NPC Reply to Entry for ROOT level";
                }

                // Add to entries list using AddNodeInternal for LinkRegistry tracking
                CurrentDialog.AddNodeInternal(duplicate, duplicate.Type);

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
                StatusMessage = $"Pasted duplicate Entry at ROOT: {duplicate.DisplayText}";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Pasted duplicate Entry to ROOT: {duplicate.DisplayText}");
                return;
            }

            // Normal paste to non-ROOT parent
            // Clone the node (deep copy)
            var duplicateNode = CloneNode(_copiedNode);

            // Add to appropriate list using AddNodeInternal for LinkRegistry tracking
            CurrentDialog.AddNodeInternal(duplicateNode, duplicateNode.Type);

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
            StatusMessage = $"Pasted duplicate node under {parent.DisplayText}: {duplicateNode.DisplayText}";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Pasted duplicate: {duplicateNode.DisplayText} under {parent.DisplayText}");
        }

        public void PasteAsLink(TreeViewSafeNode? parent)
        {
            if (CurrentDialog == null) return;
            if (_copiedNode == null)
            {
                StatusMessage = "No node copied. Use Copy Node first.";
                return;
            }
            if (parent == null)
            {
                StatusMessage = "Select a parent node to paste link under";
                return;
            }

            // NOTE: User cannot paste a node as a link under itself (would create immediate circular reference).
            // This is expected behavior - links must point to different nodes to maintain valid conversation flow.
            // GFF handles this naturally by creating a pointer structure that would result in an infinite loop
            // if not prevented. Users should paste as duplicate if they want the same content in multiple places
            // within the same branch.

            // Check if pasting to ROOT
            if (parent is TreeViewRootNode)
            {
                // Format requirement: Cannot paste as link to ROOT level at all
                // ROOT can only have new Entry nodes or duplicates, not links
                StatusMessage = "Cannot paste as link to ROOT - use Paste as Duplicate instead";
                UnifiedLogger.LogApplication(LogLevel.WARN, "Blocked paste as link to ROOT - links not supported at ROOT level");
                return;
            }

            // Save state for undo before creating link
            SaveUndoState("Paste as Link");

            // Normal paste link to non-ROOT parent
            // Get the current index of copied node (LinkRegistry ensures it's accurate)
            var nodeIndex = (uint)CurrentDialog.GetNodeIndex(_copiedNode, _copiedNode.Type);

            // Validate index is valid
            if ((int)nodeIndex == -1)
            {
                StatusMessage = "Error: Copied node no longer exists in dialog";
                UnifiedLogger.LogApplication(LogLevel.ERROR, "Copied node not found in dialog during paste as link");
                return;
            }

            // Create link pointer (references original node)
            var linkPtr = new DialogPtr
            {
                Node = _copiedNode,
                Type = _copiedNode.Type,
                Index = nodeIndex,
                IsLink = true, // Mark as link
                ScriptAppears = "",
                ConditionParams = new Dictionary<string, string>(),
                Comment = "",
                LinkComment = "[Link to original]",
                Parent = CurrentDialog
            };

            parent.OriginalNode.Pointers.Add(linkPtr);

            // Register the link pointer with LinkRegistry
            CurrentDialog.LinkRegistry.RegisterLink(linkPtr);

            RefreshTreeView();
            HasUnsavedChanges = true;
            StatusMessage = $"Pasted link under {parent.DisplayText}: {_copiedNode.DisplayText}";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Pasted link to: {_copiedNode.DisplayText} under {parent.DisplayText}");
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
        private TreeState CaptureTreeState()
        {
            var state = new TreeState
            {
                ExpandedNodePaths = new HashSet<string>(),
                SelectedNodePath = null
            };

            // Capture expanded nodes by their unique path (text chain from root)
            CaptureExpandedNodes(DialogNodes, "", state.ExpandedNodePaths);

            return state;
        }

        private void CaptureExpandedNodes(ObservableCollection<TreeViewSafeNode> nodes, string parentPath, HashSet<string> expandedPaths, HashSet<TreeViewSafeNode>? visited = null)
        {
            if (nodes == null) return;

            // Circular reference protection
            visited ??= new HashSet<TreeViewSafeNode>();

            foreach (var node in nodes)
            {
                if (node == null) continue;

                // Circular reference check - skip if already visited
                if (!visited.Add(node))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Circular reference detected in tree state capture: {node.DisplayText}");
                    continue;
                }

                // Create unique path using display text + link status
                var nodePath = string.IsNullOrEmpty(parentPath)
                    ? GetNodeIdentifier(node)
                    : $"{parentPath}|{GetNodeIdentifier(node)}";

                // If expanded, record it
                if (node.IsExpanded)
                {
                    expandedPaths.Add(nodePath);
                }

                // Recurse into children (even for links - they can be expanded)
                if (node.Children != null && node.Children.Count > 0)
                {
                    CaptureExpandedNodes(node.Children, nodePath, expandedPaths, visited);
                }

                // Remove from visited after processing this branch (allows same node in different branches)
                visited.Remove(node);
            }
        }

        private string GetNodeIdentifier(TreeViewSafeNode node)
        {
            // Use display text + type + link status as identifier
            // This distinguishes between link nodes and duplicate nodes with same text
            if (node is TreeViewRootNode)
                return "ROOT";

            var displayText = node.DisplayText ?? "UNKNOWN";
            var nodeType = node.OriginalNode?.Type.ToString() ?? "UNKNOWN";
            var isLink = node.IsChild ? "LINK" : "NODE";

            return $"{displayText}[{nodeType}:{isLink}]";
        }

        /// <summary>
        /// Restores tree expansion state and selection
        /// </summary>
        private void RestoreTreeState(TreeState state)
        {
            if (state == null) return;

            // Restore expanded nodes
            RestoreExpandedNodes(DialogNodes, "", state.ExpandedNodePaths);

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Restored {state.ExpandedNodePaths.Count} expanded nodes");
        }

        private void RestoreExpandedNodes(ObservableCollection<TreeViewSafeNode> nodes, string parentPath, HashSet<string> expandedPaths, HashSet<TreeViewSafeNode>? visited = null)
        {
            if (nodes == null) return;

            // Circular reference protection
            visited ??= new HashSet<TreeViewSafeNode>();

            foreach (var node in nodes)
            {
                if (node == null) continue;

                // Circular reference check - skip if already visited
                if (!visited.Add(node))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Circular reference detected in tree state restore: {node.DisplayText}");
                    continue;
                }

                var nodePath = string.IsNullOrEmpty(parentPath)
                    ? GetNodeIdentifier(node)
                    : $"{parentPath}|{GetNodeIdentifier(node)}";

                // Restore expansion state
                if (expandedPaths.Contains(nodePath))
                {
                    node.IsExpanded = true;
                }

                // Recurse into children (even for links - they can be expanded)
                if (node.Children != null && node.Children.Count > 0)
                {
                    RestoreExpandedNodes(node.Children, nodePath, expandedPaths, visited);
                }

                // Remove from visited after processing this branch (allows same node in different branches)
                visited.Remove(node);
            }
        }

        /// <summary>
        /// Tree state for undo/redo preservation
        /// </summary>
        private class TreeState
        {
            public HashSet<string> ExpandedNodePaths { get; set; } = new();
            public string? SelectedNodePath { get; set; }
        }
    }
}

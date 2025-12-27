using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using Parley.Models;

namespace DialogEditor.ViewModels
{
    /// <summary>
    /// MainViewModel partial - Tree Operations (Populate, Refresh, Navigate, State)
    /// </summary>
    public partial class MainViewModel
    {
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

        /// <summary>
        /// Refreshes tree view and marks dialog as having unsaved changes.
        /// Common pattern after node operations that modify the dialog structure.
        /// </summary>
        private void RefreshTreeViewAndMarkDirty()
        {
            RefreshTreeView();
            HasUnsavedChanges = true;
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
    }
}

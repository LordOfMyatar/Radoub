using System;
using System.Linq;
using DialogEditor.Models;
using Radoub.Formats.Logging;

namespace DialogEditor.ViewModels
{
    /// <summary>
    /// MainViewModel partial - Node Operations (Add, Delete, Move)
    /// </summary>
    public partial class MainViewModel
    {
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

            try
            {
                // Focus on the newly created node after tree refresh
                CoordinatedRefreshAndSelect(newNode);
            }
            catch (Exception ex)
            {
                // Refresh threw after the node was attached. Roll back the model so it is
                // not left half-attached with HasUnsavedChanges flipped (Relique #2166 pattern, #2260).
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"AddNodeWithUndoAndRefresh: refresh failed, rolling back '{undoDescription}': {ex.Message}");
                DetachCreatedNode(CurrentDialog!, newNode);
                _undoRedoService.DiscardLastSavedState();
                StatusMessage = $"Failed to add node: {ex.Message}";
                return newNode;
            }

            HasUnsavedChanges = true;
            StatusMessage = successMessage;

            return newNode;
        }

        /// <summary>
        /// Removes a just-created leaf node from the dialog model. Used to roll back a failed
        /// add when the tree refresh throws (#2260). The node has no children yet, so it is
        /// enough to drop it from Entries/Replies and remove any pointer that references it.
        /// </summary>
        private static void DetachCreatedNode(Dialog dialog, DialogNode newNode)
        {
            dialog.Starts.RemoveAll(ptr => ptr.Node == newNode);
            foreach (var entry in dialog.Entries)
                entry.Pointers.RemoveAll(ptr => ptr.Node == newNode);
            foreach (var reply in dialog.Replies)
                reply.Pointers.RemoveAll(ptr => ptr.Node == newNode);

            dialog.Entries.Remove(newNode);
            dialog.Replies.Remove(newNode);
        }

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

                // Find sibling to focus BEFORE deleting (tree structure changes after deletion)
                var siblingToFocus = FindSiblingForFocus(node);

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

                // Refresh tree and focus sibling (or parent, or root if no sibling)
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "DeleteNode: About to refresh tree");
                if (siblingToFocus != null)
                {
                    CoordinatedRefreshAndSelect(siblingToFocus);
                }
                else
                {
                    CoordinatedRefreshToRoot();
                }
                HasUnsavedChanges = true;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "DeleteNode: Tree refresh completed");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"DeleteNode EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"DeleteNode stack trace: {ex.StackTrace}");
                StatusMessage = $"Error deleting node: {ex.Message}";
                throw; // Re-throw so we can see it in debug
            }
        }

        // #2324: Test seam for the recursive-delete behavior that bypasses the full DeleteNode
        // flow (no orphan cleanup). internal + typed delegation — no reflection.
        internal void DeleteNodeRecursive(DialogNode node)
        {
            if (CurrentDialog == null) return;
            _nodeOpsManager.DeleteNodeRecursive(CurrentDialog, node);
        }

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
                CoordinatedRefreshAndSelect(node);
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
                CoordinatedRefreshAndSelect(node);
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
                CoordinatedRefreshAndSelect(node);
            }
        }

        /// <summary>
        /// Moves a node to a new position via drag-and-drop from flowchart (#1965).
        /// Takes DialogNode/DialogPtr directly (no TreeViewSafeNode wrapper).
        /// Caller is responsible for saving undo state.
        /// </summary>
        public bool MoveNodeToPositionDirect(DialogNode node, DialogPtr? sourcePointer, DialogNode? newParent, int insertIndex)
        {
            if (CurrentDialog == null) return false;

            bool moved = _nodeOpsManager.MoveNodeToPosition(CurrentDialog, node, sourcePointer, newParent, insertIndex, out string statusMessage);

            StatusMessage = statusMessage;

            if (moved)
            {
                HasUnsavedChanges = true;
                CoordinatedRefreshAndSelect(node);
            }

            return moved;
        }

        /// <summary>
        /// Reorders a sibling node within its parent's pointer list (#240).
        /// Called from flowchart drag-drop. Handles undo state + execution.
        /// </summary>
        public void ReorderSibling(DialogNode node, DialogNode? parent, int fromIndex, int toIndex)
        {
            if (CurrentDialog == null) return;

            SaveUndoState("Reorder Node");

            // ReorderSibling publishes DialogChangeEventBus.NodeMoved, which triggers view refresh
            bool moved = _nodeOpsManager.ReorderSibling(CurrentDialog, node, parent, fromIndex, toIndex, out string statusMessage);

            StatusMessage = statusMessage;

            if (moved)
            {
                HasUnsavedChanges = true;
                CoordinatedRefreshAndSelect(node);
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
    }
}

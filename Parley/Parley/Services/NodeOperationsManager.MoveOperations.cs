using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// NodeOperationsManager partial: Move and reorder operations.
    /// Split from NodeOperationsManager.cs (#1540).
    /// </summary>
    public partial class NodeOperationsManager
    {
        /// <summary>
        /// Moves a node to a new position (reorder or reparent).
        /// Used for drag-and-drop operations.
        /// </summary>
        /// <param name="dialog">The dialog containing the node.</param>
        /// <param name="node">The node to move.</param>
        /// <param name="sourcePointer">The pointer from the source parent to this node (for identifying correct parent).</param>
        /// <param name="newParent">The new parent (null for root level).</param>
        /// <param name="insertIndex">Index to insert at in the new parent's children.</param>
        /// <param name="statusMessage">Output status message.</param>
        /// <returns>True if moved successfully.</returns>
        public bool MoveNodeToPosition(Dialog dialog, DialogNode node, DialogPtr? sourcePointer, DialogNode? newParent, int insertIndex, out string statusMessage)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"[MoveNodeToPosition] Moving '{node.DisplayText}' to parent={newParent?.DisplayText ?? "ROOT"}, index={insertIndex}, hasSourcePointer={sourcePointer != null}");

            // CRITICAL: Validate type compatibility BEFORE removing from old parent
            // Previous bug: validation happened after removal, so if validation failed,
            // the node was already gone and couldn't be recovered
            if (newParent == null)
            {
                // Moving to root - only Entry nodes allowed
                if (node.Type != DialogNodeType.Entry)
                {
                    statusMessage = "Only NPC Entry nodes can be at root level";
                    return false;
                }
            }
            else
            {
                // Validate parent-child type alternation
                if (newParent.Type == DialogNodeType.Entry && node.Type != DialogNodeType.Reply)
                {
                    statusMessage = "NPC Entry nodes can only have PC Reply children";
                    return false;
                }
                if (newParent.Type == DialogNodeType.Reply && node.Type != DialogNodeType.Entry)
                {
                    statusMessage = "PC Reply nodes can only have NPC Entry children";
                    return false;
                }

                // CRITICAL: Validate new parent is reachable from dialog's Starts
                // This prevents moving nodes to orphaned branches which would make them disappear
                if (!IsNodeReachableFromStarts(dialog, newParent))
                {
                    statusMessage = "Cannot move to this location - target node is not reachable from dialog root";
                    UnifiedLogger.LogApplication(LogLevel.ERROR,
                        $"[MoveNodeToPosition] ABORT: New parent '{newParent.DisplayText}' is not reachable from Starts");
                    return false;
                }
            }

            // Find old parent and pointer using sourcePointer if available
            // This ensures we remove from the correct parent when a node appears multiple times
            DialogNode? oldParent = null;
            DialogPtr? oldPtr = sourcePointer;

            if (sourcePointer != null)
            {
                // Use sourcePointer to find the exact parent that contains it
                oldParent = FindParentContainingPointer(dialog, sourcePointer);
                if (oldParent == null)
                {
                    // Check if it's in Starts
                    if (dialog.Starts.Contains(sourcePointer))
                    {
                        oldParent = null; // Root level
                        oldPtr = sourcePointer;
                    }
                    else
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN,
                            $"[MoveNodeToPosition] sourcePointer not found in any parent, falling back to FindParentNode");
                        oldParent = FindParentNode(dialog, node);
                        oldPtr = oldParent?.Pointers.FirstOrDefault(p => p.Node == node)
                                 ?? dialog.Starts.FirstOrDefault(s => s.Node == node);
                    }
                }
            }
            else
            {
                // No sourcePointer - fall back to original behavior
                oldParent = FindParentNode(dialog, node);
                if (oldParent != null)
                {
                    oldPtr = oldParent.Pointers.FirstOrDefault(p => p.Node == node);
                }
                else
                {
                    // Node was at root level - check Starts
                    oldPtr = dialog.Starts.FirstOrDefault(s => s.Node == node);
                }
            }

            // Validate we found the pointer BEFORE removing anything
            if (oldPtr == null)
            {
                statusMessage = "Could not find node's pointer in old parent";
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"[MoveNodeToPosition] ABORT: Could not find pointer for '{node.DisplayText}' - no removal performed");
                return false;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"[MoveNodeToPosition] Removing from oldParent={oldParent?.DisplayText ?? "ROOT"}");

            // Now safe to remove from old location
            bool removed = false;
            if (oldParent != null)
            {
                int countBefore = oldParent.Pointers.Count;
                removed = oldParent.Pointers.Remove(oldPtr);
                int countAfter = oldParent.Pointers.Count;
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"[MoveNodeToPosition] Remove result: {removed}, Pointers count: {countBefore} -> {countAfter}");
            }
            else
            {
                int countBefore = dialog.Starts.Count;
                removed = dialog.Starts.Remove(oldPtr);
                int countAfter = dialog.Starts.Count;
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"[MoveNodeToPosition] Remove from Starts result: {removed}, Starts count: {countBefore} -> {countAfter}");
            }

            // CRITICAL: If Remove failed, don't insert (would cause duplication)
            if (!removed)
            {
                statusMessage = "Failed to remove node from old parent - aborting to prevent duplication";
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"[MoveNodeToPosition] ABORT: Remove returned false for '{node.DisplayText}' - pointer not found in expected location");
                return false;
            }

            // Insert into new position
            if (newParent == null)
            {
                // Insert at root level
                oldPtr.IsStart = true;
                insertIndex = Math.Min(insertIndex, dialog.Starts.Count);
                dialog.Starts.Insert(insertIndex, oldPtr);
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"[MoveNodeToPosition] Inserted at ROOT index {insertIndex}, Starts count now: {dialog.Starts.Count}");
            }
            else
            {
                // Insert into new parent's pointers
                oldPtr.IsStart = false;
                insertIndex = Math.Min(insertIndex, newParent.Pointers.Count);
                newParent.Pointers.Insert(insertIndex, oldPtr);
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"[MoveNodeToPosition] Inserted at parent '{newParent.DisplayText}' index {insertIndex}, Pointers count now: {newParent.Pointers.Count}");
            }

            // Publish event for view synchronization
            DialogChangeEventBus.Instance.PublishNodeMoved(node, newParent, oldParent);

            statusMessage = $"Moved '{node.DisplayText}' to {(newParent == null ? "root" : newParent.DisplayText)}";
            UnifiedLogger.LogApplication(LogLevel.INFO, statusMessage);
            return true;
        }

        /// <summary>
        /// Finds the parent DialogNode that contains the given pointer.
        /// </summary>
        private DialogNode? FindParentContainingPointer(Dialog dialog, DialogPtr pointer)
        {
            foreach (var entry in dialog.Entries)
            {
                if (entry.Pointers.Contains(pointer))
                    return entry;
            }
            foreach (var reply in dialog.Replies)
            {
                if (reply.Pointers.Contains(pointer))
                    return reply;
            }
            return null;
        }

        /// <summary>
        /// Moves a node up in its parent's child list or in the START list.
        /// Returns true if moved, false if already first.
        /// </summary>
        public bool MoveNodeUp(Dialog dialog, DialogNode node, out string statusMessage)
        {
            // Check if root-level node (in StartingList)
            int startIndex = dialog.Starts.FindIndex(s => s.Node == node);

            if (startIndex != -1)
            {
                // Root-level node
                if (startIndex == 0)
                {
                    statusMessage = "Node is already first";
                    return false;
                }

                var temp = dialog.Starts[startIndex];
                dialog.Starts[startIndex] = dialog.Starts[startIndex - 1];
                dialog.Starts[startIndex - 1] = temp;

                statusMessage = $"Moved '{node.Text?.GetDefault()}' up";
                return true;
            }

            // Child node - find parent and use service
            DialogNode? parent = FindParentNode(dialog, node);
            if (parent != null)
            {
                bool moved = _editorService.MoveNodeUp(parent, node);
                if (moved)
                {
                    statusMessage = $"Moved '{node.Text?.GetDefault()}' up";
                    return true;
                }
                else
                {
                    statusMessage = "Node is already first in parent";
                    return false;
                }
            }
            else
            {
                statusMessage = "Cannot find parent node";
                return false;
            }
        }

        /// <summary>
        /// Moves a node down in its parent's child list or in the START list.
        /// Returns true if moved, false if already last.
        /// </summary>
        public bool MoveNodeDown(Dialog dialog, DialogNode node, out string statusMessage)
        {
            // Check if root-level node (in StartingList)
            int startIndex = dialog.Starts.FindIndex(s => s.Node == node);

            if (startIndex != -1)
            {
                // Root-level node
                if (startIndex >= dialog.Starts.Count - 1)
                {
                    statusMessage = "Node is already last";
                    return false;
                }

                var temp = dialog.Starts[startIndex];
                dialog.Starts[startIndex] = dialog.Starts[startIndex + 1];
                dialog.Starts[startIndex + 1] = temp;

                statusMessage = $"Moved '{node.Text?.GetDefault()}' down";
                return true;
            }

            // Child node - find parent and use service
            DialogNode? parent = FindParentNode(dialog, node);
            if (parent != null)
            {
                bool moved = _editorService.MoveNodeDown(parent, node);
                if (moved)
                {
                    statusMessage = $"Moved '{node.Text?.GetDefault()}' down";
                    return true;
                }
                else
                {
                    statusMessage = "Node is already last in parent";
                    return false;
                }
            }
            else
            {
                statusMessage = "Cannot find parent node";
                return false;
            }
        }

        /// <summary>
        /// Checks if a node is reachable from the dialog's Starts (root entries).
        /// This prevents moving nodes to orphaned branches that would make them disappear from the tree.
        /// </summary>
        private bool IsNodeReachableFromStarts(Dialog dialog, DialogNode targetNode)
        {
            var visited = new HashSet<DialogNode>();

            foreach (var start in dialog.Starts)
            {
                if (start.Node != null && IsNodeReachableFrom(start.Node, targetNode, visited))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Recursively checks if targetNode is reachable from the given node.
        /// </summary>
        private bool IsNodeReachableFrom(DialogNode current, DialogNode target, HashSet<DialogNode> visited)
        {
            if (current == target)
                return true;

            if (visited.Contains(current))
                return false;

            visited.Add(current);

            foreach (var ptr in current.Pointers)
            {
                if (ptr.Node != null && IsNodeReachableFrom(ptr.Node, target, visited))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Reorders a sibling within its parent's pointer list or in Dialog.Starts (#240).
        /// Moves the pointer at fromIndex to toIndex. No alternation validation needed
        /// since all siblings under the same parent are the same type.
        /// </summary>
        public bool ReorderSibling(Dialog dialog, DialogNode node, DialogNode? parent, int fromIndex, int toIndex, out string statusMessage)
        {
            var pointerList = parent?.Pointers ?? dialog.Starts;

            // Validate indices
            if (fromIndex < 0 || fromIndex >= pointerList.Count ||
                toIndex < 0 || toIndex >= pointerList.Count)
            {
                statusMessage = "Invalid index for reorder";
                return false;
            }

            // Same position — no-op
            if (fromIndex == toIndex)
            {
                statusMessage = "Already at target position";
                return false;
            }

            // Remove from old position and insert at new
            var ptr = pointerList[fromIndex];
            pointerList.RemoveAt(fromIndex);
            pointerList.Insert(toIndex, ptr);

            statusMessage = $"Reordered '{node.Text?.GetDefault()}' from position {fromIndex} to {toIndex}";
            UnifiedLogger.LogApplication(LogLevel.INFO, statusMessage);

            // Publish event for view sync
            DialogChangeEventBus.Instance.PublishNodeMoved(node, parent, parent);

            return true;
        }
    }
}

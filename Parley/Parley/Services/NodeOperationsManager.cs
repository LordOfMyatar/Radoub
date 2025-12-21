using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service responsible for node add/delete/move operations.
    /// Extracted from MainViewModel to improve separation of concerns.
    ///
    /// Handles:
    /// 1. Adding nodes (AddSmartNode, AddEntryNode, AddPCReplyNode)
    /// 2. Deleting nodes with link checking and scrap management
    /// 3. Moving nodes up/down in parent's child list
    /// 4. Finding parent nodes and siblings
    /// </summary>
    public class NodeOperationsManager
    {
        private readonly DialogEditorService _editorService;
        private readonly ScrapManager _scrapManager;
        private readonly OrphanNodeManager _orphanManager;

        public NodeOperationsManager(
            DialogEditorService editorService,
            ScrapManager scrapManager,
            OrphanNodeManager orphanManager)
        {
            _editorService = editorService;
            _scrapManager = scrapManager;
            _orphanManager = orphanManager;
        }

        /// <summary>
        /// Adds a smart node (Entry or Reply based on context).
        /// Returns the newly created node.
        /// </summary>
        public DialogNode AddSmartNode(Dialog dialog, DialogNode? parentNode, DialogPtr? parentPtr)
        {
            return _editorService.AddSmartNode(dialog, parentNode, parentPtr);
        }

        /// <summary>
        /// Adds an Entry node (NPC dialog).
        /// Returns the newly created Entry node.
        /// </summary>
        public DialogNode AddEntryNode(Dialog dialog, DialogNode? parentNode, DialogPtr? parentPtr)
        {
            return _editorService.AddEntryNode(dialog, parentNode, parentPtr);
        }

        /// <summary>
        /// Adds a PC Reply node.
        /// Returns the newly created Reply node.
        /// </summary>
        public DialogNode AddPCReplyNode(Dialog dialog, DialogNode parentNode, DialogPtr? parentPtr)
        {
            return _editorService.AddPCReplyNode(dialog, parentNode, parentPtr);
        }

        /// <summary>
        /// Deletes a node and its children from the dialog tree.
        /// Checks for incoming links and adds deleted nodes to scrap.
        /// Returns list of nodes that had incoming links (for warning display).
        /// </summary>
        public List<DialogNode> DeleteNode(Dialog dialog, DialogNode node, string? currentFileName)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"[DeleteNode] ENTER: '{node.DisplayText}'");

            // Check if node or its children have incoming links
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "[DeleteNode] Step 1: CheckForIncomingLinks");
            var linkedNodes = CheckForIncomingLinks(dialog, node);
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
                }

                var nodeList = string.Join(", ", linkedNodes.Select(n => $"'{n.DisplayText}'"));
                UnifiedLogger.LogApplication(LogLevel.WARN, $"DELETE WARNING: Deleting node will break links to: {nodeList}");
            }

            // Collect all nodes that will be deleted (including the node and its children)
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "[DeleteNode] Step 2: CollectNodeAndChildren");
            var nodesToDelete = new List<DialogNode>();
            var hierarchyInfo = new Dictionary<DialogNode, (int level, DialogNode? parent)>();
            CollectNodeAndChildren(node, nodesToDelete, hierarchyInfo);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"[DeleteNode] Collected {nodesToDelete.Count} nodes to delete");

            // Add deleted nodes to scrap BEFORE deleting them
            // Track the batchId so orphaned nodes can be added to the same batch
            string? deletionBatchId = null;
            if (nodesToDelete.Count > 0 && currentFileName != null)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "[DeleteNode] Step 3: AddToScrap");
                deletionBatchId = _scrapManager.AddToScrap(currentFileName, nodesToDelete, "deleted", hierarchyInfo);
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Added {nodesToDelete.Count} deleted nodes to scrap");
            }

            // CRITICAL: Identify and remove orphaned link children (PR #132 "evil twin" fix)
            // These are nodes that have ONLY child link references and would become orphaned
            // when their parent is deleted. They must be removed from Entries/Replies lists
            // to prevent child link corruption in Aurora.
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "[DeleteNode] Step 4: IdentifyOrphanedLinkChildren");
            var orphanedLinkChildren = _orphanManager.IdentifyOrphanedLinkChildren(dialog, node, nodesToDelete.ToHashSet());
            if (orphanedLinkChildren.Count > 0 && currentFileName != null)
            {
                // Add orphaned link children to scrap before removing (same batch as main deletion)
                var orphanHierarchy = new Dictionary<DialogNode, (int level, DialogNode? parent)>();
                foreach (var orphan in orphanedLinkChildren)
                {
                    orphanHierarchy[orphan] = (0, null); // Orphans have no parent after deletion
                }
                deletionBatchId = _scrapManager.AddToScrap(currentFileName, orphanedLinkChildren, "orphaned link child", orphanHierarchy, deletionBatchId);

                // Remove from Entries/Replies lists to prevent index corruption
                _orphanManager.RemoveOrphanedLinkChildrenFromLists(dialog, orphanedLinkChildren);

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Identified and removed {orphanedLinkChildren.Count} orphaned link children");
            }

            // CRITICAL: Recursively delete all children - even if linked elsewhere
            // This matches Aurora behavior - deleting parent removes entire subtree
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "[DeleteNode] Step 5: DeleteNodeRecursive");
            DeleteNodeRecursive(dialog, node);

            // CRITICAL: After deletion, recalculate indices AND check for orphaned links
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "[DeleteNode] Step 6: RecalculatePointerIndices");
            RecalculatePointerIndices(dialog);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "[DeleteNode] Step 7: RemoveOrphanedPointers (first)");
            _orphanManager.RemoveOrphanedPointers(dialog);

            // CRITICAL: Remove any remaining orphaned nodes (nodes with no incoming pointers)
            // This catches nodes that were orphaned by the deletion (e.g., nodes with only child links)
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "[DeleteNode] Step 8: RemoveOrphanedNodes");
            var additionalOrphans = _orphanManager.RemoveOrphanedNodes(dialog);
            if (additionalOrphans.Count > 0 && currentFileName != null)
            {
                // Add orphaned nodes to scrap (same batch as main deletion)
                var orphanHierarchy = new Dictionary<DialogNode, (int level, DialogNode? parent)>();
                foreach (var orphan in additionalOrphans)
                {
                    orphanHierarchy[orphan] = (0, null); // Orphans have no parent
                }
                _scrapManager.AddToScrap(currentFileName, additionalOrphans, "orphaned after deletion", orphanHierarchy, deletionBatchId);

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Removed {additionalOrphans.Count} orphaned nodes after deletion");
            }

            // CRITICAL: Clean up pointers to nodes that were removed by RemoveOrphanedNodes
            // This ensures TreeView lazy loading doesn't show orphaned nodes
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "[DeleteNode] Step 9: RemoveOrphanedPointers (second)");
            _orphanManager.RemoveOrphanedPointers(dialog);

            UnifiedLogger.LogApplication(LogLevel.INFO, $"[DeleteNode] EXIT: Deleted node tree: {node.DisplayText}");

            return linkedNodes;
        }

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
        /// Finds the parent node of a given child node.
        /// Returns null if node is a START node or not found.
        /// </summary>
        public DialogNode? FindParentNode(Dialog dialog, DialogNode childNode)
        {
            // Search all entries for this child in their Pointers
            foreach (var entry in dialog.Entries)
            {
                if (entry.Pointers.Any(p => p.Node == childNode))
                    return entry;
            }

            // Search all replies for this child in their Pointers
            foreach (var reply in dialog.Replies)
            {
                if (reply.Pointers.Any(p => p.Node == childNode))
                    return reply;
            }

            return null;
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
        /// Finds a sibling node to focus after cutting/deleting a node.
        /// Returns previous sibling if available, otherwise next sibling, otherwise parent.
        /// </summary>
        public DialogNode? FindSiblingForFocus(Dialog dialog, DialogNode node)
        {
            // Find parent to get siblings
            var parent = FindParentNode(dialog, node);

            if (parent != null)
            {
                // Node is a child - find sibling in parent's pointers
                var siblings = parent.Pointers.Where(p => p.Node != null).Select(p => p.Node!).ToList();
                int index = siblings.IndexOf(node);

                if (index >= 0)
                {
                    // Try previous sibling first
                    if (index > 0)
                        return siblings[index - 1];

                    // Try next sibling
                    if (index < siblings.Count - 1)
                        return siblings[index + 1];
                }

                // No siblings - return parent
                return parent;
            }
            else
            {
                // Node is a START node - find sibling in START nodes
                var startNodes = dialog.Starts.Where(p => p.Node != null).Select(p => p.Node!).ToList();
                int index = startNodes.IndexOf(node);

                if (index >= 0)
                {
                    // Try previous START node
                    if (index > 0)
                        return startNodes[index - 1];

                    // Try next START node
                    if (index < startNodes.Count - 1)
                        return startNodes[index + 1];
                }

                // No siblings - return null (will default to ROOT in calling code)
                return null;
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Checks a node and its children for incoming links from other parts of the tree.
        /// Returns list of nodes that have incoming links.
        /// </summary>
        private List<DialogNode> CheckForIncomingLinks(Dialog dialog, DialogNode node)
        {
            var linkedNodes = new List<DialogNode>();
            CheckNodeForLinks(dialog, node, linkedNodes);
            return linkedNodes;
        }

        /// <summary>
        /// Recursively checks a node for incoming links using LinkRegistry.
        /// Only traverses non-link children (links are bookmarks, not owned children).
        /// </summary>
        private void CheckNodeForLinks(Dialog dialog, DialogNode node, List<DialogNode> linkedNodes)
        {
            // Use LinkRegistry to check for incoming links
            var incomingLinks = dialog.LinkRegistry.GetLinksTo(node);

            // If there are multiple incoming links or any are marked as IsLink, this node is referenced elsewhere
            if (incomingLinks.Count > 1 || incomingLinks.Any(ptr => ptr.IsLink))
            {
                linkedNodes.Add(node);
            }

            // Recursively check only non-link children (links are bookmarks, don't traverse them)
            if (node.Pointers != null)
            {
                foreach (var ptr in node.Pointers)
                {
                    // CRITICAL: Only follow non-link children - links point to nodes owned elsewhere
                    if (ptr.Node != null && !ptr.IsLink)
                    {
                        CheckNodeForLinks(dialog, ptr.Node, linkedNodes);
                    }
                }
            }
        }

        /// <summary>
        /// Collects a node and all its non-link children for deletion or scrap.
        /// Builds hierarchy information for scrap display.
        /// </summary>
        private void CollectNodeAndChildren(DialogNode node, List<DialogNode> collected,
            Dictionary<DialogNode, (int level, DialogNode? parent)>? hierarchyInfo = null,
            int level = 0, DialogNode? parent = null)
        {
            collected.Add(node);

            if (hierarchyInfo != null)
            {
                hierarchyInfo[node] = (level, parent);
            }

            foreach (var ptr in node.Pointers)
            {
                if (ptr.Node != null && !ptr.IsLink && !collected.Contains(ptr.Node))
                {
                    CollectNodeAndChildren(ptr.Node, collected, hierarchyInfo, level + 1, node);
                }
            }
        }

        /// <summary>
        /// Recursively deletes a node and its children.
        /// Preserves nodes that have incoming links from elsewhere.
        /// </summary>
        private void DeleteNodeRecursive(Dialog dialog, DialogNode node)
        {
            // Recursively delete children, but only if they're not shared by other nodes
            if (node.Pointers != null && node.Pointers.Count > 0)
            {
                // Make a copy of the pointers list to avoid modification during iteration
                var pointersToDelete = node.Pointers.ToList();

                foreach (var ptr in pointersToDelete)
                {
                    if (ptr.Node != null)
                    {
                        // Check if this child node has other incoming links besides the one we're about to delete
                        var incomingLinks = dialog.LinkRegistry.GetLinksTo(ptr.Node);

                        // Count how many of the incoming links are NOT from the node we're deleting
                        var otherIncomingLinks = incomingLinks.Where(link =>
                        {
                            // Find which node contains this link
                            DialogNode? linkParent = null;
                            foreach (var entry in dialog.Entries)
                            {
                                if (entry.Pointers.Contains(link))
                                {
                                    linkParent = entry;
                                    break;
                                }
                            }
                            if (linkParent == null)
                            {
                                foreach (var reply in dialog.Replies)
                                {
                                    if (reply.Pointers.Contains(link))
                                    {
                                        linkParent = reply;
                                        break;
                                    }
                                }
                            }
                            // Check if it's in Starts
                            if (linkParent == null && dialog.Starts.Contains(link))
                            {
                                linkParent = null; // Start link, not from a node
                            }

                            return linkParent != node;
                        }).Count();

                        // CRITICAL: Check if this node is a parent in parent-child link(s)
                        // Child links (IsLink=true) point TO the parent, and children should not be deleted
                        // when their parent loses a regular incoming pointer
                        var hasChildLinks = incomingLinks.Any(link => link.IsLink);

                        if (otherIncomingLinks == 0 && !hasChildLinks)
                        {
                            // No other nodes reference this child, safe to delete recursively
                            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                $"Recursively deleting child (not shared): {ptr.Node.DisplayText}");
                            DeleteNodeRecursive(dialog, ptr.Node);
                        }
                        else
                        {
                            // This child is shared by other nodes OR is a parent in parent-child links
                            var reason = hasChildLinks ?
                                $"is parent in parent-child link(s) ({incomingLinks.Count(link => link.IsLink)} child links)" :
                                $"has {otherIncomingLinks} other references";
                            UnifiedLogger.LogApplication(LogLevel.INFO,
                                $"🔒 PRESERVING node (will become orphaned): '{ptr.Node.DisplayText}' ({reason})");
                        }
                    }
                }

                // Unregister and clear the pointers list after handling children
                foreach (var ptr in pointersToDelete)
                {
                    dialog.LinkRegistry.UnregisterLink(ptr);
                }
                node.Pointers.Clear();
            }

            // Get all incoming pointers to this node from LinkRegistry
            var incomingPointers = dialog.LinkRegistry.GetLinksTo(node).ToList();
            int removedCount = 0;

            // Remove all incoming pointers using LinkRegistry data
            foreach (var incomingPtr in incomingPointers)
            {
                // Unregister from LinkRegistry first
                dialog.LinkRegistry.UnregisterLink(incomingPtr);

                // Remove from Starts if it's a start pointer
                if (dialog.Starts.Contains(incomingPtr))
                {
                    dialog.Starts.Remove(incomingPtr);
                    removedCount++;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Removed from Starts list");
                }

                // Find and remove from parent node's pointers
                foreach (var entry in dialog.Entries)
                {
                    if (entry.Pointers.Contains(incomingPtr))
                    {
                        entry.Pointers.Remove(incomingPtr);
                        removedCount++;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Removed from Entry '{entry.DisplayText}' pointers");
                    }
                }

                foreach (var reply in dialog.Replies)
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
            dialog.RemoveNodeInternal(node, node.Type);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Removed {node.Type} from list: {node.DisplayText}");
        }

        /// <summary>
        /// Recalculates all pointer indices to match current Entries/Replies list positions.
        /// Critical after deletions to maintain dialog integrity.
        /// </summary>
        private void RecalculatePointerIndices(Dialog dialog)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Recalculating all pointer indices using LinkRegistry");

            // Rebuild the LinkRegistry from current dialog state
            dialog.RebuildLinkRegistry();

            // Update all Entry node indices
            for (uint i = 0; i < dialog.Entries.Count; i++)
            {
                var entry = dialog.Entries[(int)i];
                dialog.LinkRegistry.UpdateNodeIndex(entry, i, DialogNodeType.Entry);
            }

            // Update all Reply node indices
            for (uint i = 0; i < dialog.Replies.Count; i++)
            {
                var reply = dialog.Replies[(int)i];
                dialog.LinkRegistry.UpdateNodeIndex(reply, i, DialogNodeType.Reply);
            }

            // Validate all indices are correct
            var errors = dialog.ValidatePointerIndices();
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

        #endregion
    }
}

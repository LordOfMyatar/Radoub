using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// NodeOperationsManager partial: Tree traversal, search, and recursive operations.
    /// Split from NodeOperationsManager.cs (#1540).
    /// </summary>
    public partial class NodeOperationsManager
    {
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

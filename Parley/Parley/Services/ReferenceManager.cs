using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using Parley.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service responsible for managing dialog node references and pointers.
    /// Extracted from MainViewModel to improve separation of concerns.
    ///
    /// Handles:
    /// - Reference counting (HasOtherReferences)
    /// - Pointer detachment from parents (DetachNodeFromParent)
    /// - Reachable node collection for tree traversal (CollectReachableNodes)
    /// </summary>
    public class ReferenceManager
    {
        /// <summary>
        /// Checks if a node has references from other parts of the dialog besides one link.
        /// Used during cut operations to determine if node can be safely moved.
        /// </summary>
        /// <param name="dialog">The dialog to search</param>
        /// <param name="node">The node to check</param>
        /// <returns>True if node has more than 1 reference, false otherwise</returns>
        public bool HasOtherReferences(Dialog dialog, DialogNode node)
        {
            if (dialog == null) return false;

            int refCount = 0;

            // Count references in Starts
            refCount += dialog.Starts.Count(s => s.Node == node);

            // Count references in all Entries
            foreach (var entry in dialog.Entries)
            {
                refCount += entry.Pointers.Count(p => p.Node == node);
            }

            // Count references in all Replies
            foreach (var reply in dialog.Replies)
            {
                refCount += reply.Pointers.Count(p => p.Node == node);
            }

            // If more than 1 reference, has other references besides the one we're cutting
            return refCount > 1;
        }

        /// <summary>
        /// Detaches a node from its parent by removing the pointer reference.
        /// Searches Starts list first, then Entry/Reply pointers.
        /// Only removes the FIRST matching pointer found.
        /// </summary>
        /// <param name="dialog">The dialog containing the node</param>
        /// <param name="node">The node to detach</param>
        public void DetachNodeFromParent(Dialog dialog, DialogNode node)
        {
            if (dialog == null) return;

            // Remove from Starts list if present
            var startToRemove = dialog.Starts.FirstOrDefault(s => s.Node == node);
            if (startToRemove != null)
            {
                dialog.Starts.Remove(startToRemove);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Detached from Starts list");
                return;
            }

            // Remove from parent's pointers
            foreach (var entry in dialog.Entries)
            {
                var ptrToRemove = entry.Pointers.FirstOrDefault(p => p.Node == node);
                if (ptrToRemove != null)
                {
                    entry.Pointers.Remove(ptrToRemove);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Detached from Entry: {entry.DisplayText}");
                    return;
                }
            }

            foreach (var reply in dialog.Replies)
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

        /// <summary>
        /// Recursively collects all nodes reachable from the tree structure.
        /// CRITICAL: Traverses dialog model pointers, not TreeView children (Issue #82 lazy loading fix)
        /// This ensures we find all reachable nodes even when TreeView children aren't populated yet.
        /// Links are terminal (don't expand) but the nodes they point to ARE still reachable.
        /// </summary>
        /// <param name="node">The starting tree node</param>
        /// <param name="reachableNodes">Set to accumulate reachable dialog nodes</param>
        public void CollectReachableNodes(TreeViewSafeNode node, HashSet<DialogNode> reachableNodes)
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
    }
}

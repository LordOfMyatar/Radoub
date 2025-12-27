using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service responsible for orphan node detection and cleanup.
    /// Extracted from MainViewModel to improve separation of concerns.
    ///
    /// Handles three types of orphaning:
    /// 1. Orphaned pointers - pointers to deleted nodes (removed by RemoveOrphanedPointers)
    /// 2. Orphaned nodes - nodes with no incoming pointers (removed by RemoveOrphanedNodes)
    /// 3. Orphaned link children - nodes referenced only by child links (handled during deletion)
    /// </summary>
    public class OrphanNodeManager
    {
        /// <summary>
        /// Removes pointers that reference deleted nodes (orphaned pointers).
        /// This prevents dangling references after deletion operations.
        /// Called after node deletions to maintain dialog integrity.
        /// </summary>
        public int RemoveOrphanedPointers(Dialog dialog)
        {
            if (dialog == null) return 0;

            int removedCount = 0;

            // Clean Start pointers
            var startsToRemove = new List<DialogPtr>();
            foreach (var start in dialog.Starts)
            {
                if (start.Node != null && !dialog.Entries.Contains(start.Node))
                {
                    startsToRemove.Add(start);
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Removing orphaned Start pointer to '{start.Node.DisplayText}'");
                }
            }
            foreach (var start in startsToRemove)
            {
                dialog.Starts.Remove(start);
                removedCount++;
            }

            // Clean Entry pointers
            foreach (var entry in dialog.Entries)
            {
                var ptrsToRemove = new List<DialogPtr>();
                foreach (var ptr in entry.Pointers)
                {
                    if (ptr.Node != null)
                    {
                        var list = ptr.Type == DialogNodeType.Entry ? dialog.Entries : dialog.Replies;
                        if (!list.Contains(ptr.Node))
                        {
                            ptrsToRemove.Add(ptr);
                            UnifiedLogger.LogApplication(LogLevel.WARN,
                                $"Removing orphaned pointer from Entry '{entry.DisplayText}' to '{ptr.Node.DisplayText}'");
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
            foreach (var reply in dialog.Replies)
            {
                var ptrsToRemove = new List<DialogPtr>();
                foreach (var ptr in reply.Pointers)
                {
                    if (ptr.Node != null)
                    {
                        var list = ptr.Type == DialogNodeType.Entry ? dialog.Entries : dialog.Replies;
                        if (!list.Contains(ptr.Node))
                        {
                            ptrsToRemove.Add(ptr);
                            UnifiedLogger.LogApplication(LogLevel.WARN,
                                $"Removing orphaned pointer from Reply '{reply.DisplayText}' to '{ptr.Node.DisplayText}'");
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

            return removedCount;
        }

        /// <summary>
        /// Removes nodes that have no incoming pointers (truly orphaned nodes).
        /// This handles the case where nodes exist in Entries/Replies but nothing points to them.
        /// Returns list of removed nodes for potential scrap handling.
        /// </summary>
        public List<DialogNode> RemoveOrphanedNodes(Dialog dialog)
        {
            if (dialog == null) return new List<DialogNode>();

            var removedNodes = new List<DialogNode>();

            // Collect all nodes that have incoming pointers
            var reachableNodes = new HashSet<DialogNode>();

            // Mark nodes reachable from START pointers
            foreach (var start in dialog.Starts)
            {
                if (start.Node != null)
                {
                    CollectReachableNodes(start.Node, reachableNodes);
                }
            }

            // Find Entries with no incoming pointers
            var orphanedEntries = dialog.Entries
                .Where(e => !reachableNodes.Contains(e))
                .ToList();

            // Find Replies with no incoming pointers
            var orphanedReplies = dialog.Replies
                .Where(r => !reachableNodes.Contains(r))
                .ToList();

            // Remove orphaned entries
            foreach (var orphan in orphanedEntries)
            {
                dialog.Entries.Remove(orphan);
                removedNodes.Add(orphan);
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Removed orphaned Entry node: '{orphan.DisplayText}' (no incoming pointers)");
            }

            // Remove orphaned replies
            foreach (var orphan in orphanedReplies)
            {
                dialog.Replies.Remove(orphan);
                removedNodes.Add(orphan);
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Removed orphaned Reply node: '{orphan.DisplayText}' (no incoming pointers)");
            }

            if (removedNodes.Count > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Removed {removedNodes.Count} orphaned nodes ({orphanedEntries.Count} entries, {orphanedReplies.Count} replies)");
            }

            return removedNodes;
        }

        /// <summary>
        /// Recursively collects all nodes reachable from a starting node.
        /// ONLY traverses regular pointers (IsLink=false) from START points.
        /// IsLink=true pointers are back-references and should NOT prevent orphaning.
        ///
        /// CRITICAL: If we traverse IsLink pointers, nodes with only child link references
        /// will appear reachable even when they're orphaned, preventing proper cleanup.
        /// </summary>
        private void CollectReachableNodes(DialogNode node, HashSet<DialogNode> reachable)
        {
            if (node == null || !reachable.Add(node))
                return; // Already visited

            // ONLY traverse regular pointers (IsLink=false) for orphan detection
            // IsLink=true pointers are back-references from link children to their shared parent
            // If we traverse IsLink pointers, link parents appear reachable even when their
            // owning START is deleted, preventing proper orphan detection
            foreach (var ptr in node.Pointers.Where(p => !p.IsLink))
            {
                if (ptr.Node != null)
                {
                    CollectReachableNodes(ptr.Node, reachable);
                }
            }
        }

        /// <summary>
        /// Identifies nodes that will become orphaned when a parent node is deleted.
        /// These are nodes that have ONLY child link references (IsLink=true) pointing to them
        /// and no regular parent pointers, so they would become unreachable after deletion.
        /// </summary>
        public List<DialogNode> IdentifyOrphanedLinkChildren(Dialog dialog, DialogNode nodeBeingDeleted, HashSet<DialogNode> nodesToDelete)
        {
            var orphanedLinkChildren = new List<DialogNode>();

            // Check all children of nodes being deleted
            foreach (var node in nodesToDelete)
            {
                foreach (var ptr in node.Pointers)
                {
                    if (ptr.Node != null && !nodesToDelete.Contains(ptr.Node))
                    {
                        // This child node is not being deleted with its parent
                        // Check if it will become orphaned (only has child link references)
                        var incomingLinks = GetIncomingPointers(dialog, ptr.Node);

                        // Filter out links from nodes that are being deleted
                        var remainingLinks = incomingLinks.Where(link =>
                        {
                            var linkParent = FindPointerParent(dialog, link);
                            return linkParent == null || !nodesToDelete.Contains(linkParent);
                        }).ToList();

                        // If all remaining incoming pointers are child links, this node will be orphaned
                        if (remainingLinks.Count > 0 && remainingLinks.All(link => link.IsLink))
                        {
                            if (!orphanedLinkChildren.Contains(ptr.Node))
                            {
                                orphanedLinkChildren.Add(ptr.Node);
                                UnifiedLogger.LogApplication(LogLevel.INFO,
                                    $"Identified orphaned link child: '{ptr.Node.DisplayText}' (has {remainingLinks.Count} child link(s) only)");
                            }
                        }
                    }
                }
            }

            return orphanedLinkChildren;
        }

        /// <summary>
        /// Removes orphaned link children from the dialog's Entries/Replies lists.
        /// These nodes are preserved in scrap but must be removed from lists to prevent
        /// their child links from being saved with incorrect indices.
        /// 2025-11-18: Fix for child link corruption causing "evil twin" nodes in Aurora
        /// </summary>
        public void RemoveOrphanedLinkChildrenFromLists(Dialog dialog, List<DialogNode> orphanedLinkChildren)
        {
            foreach (var orphan in orphanedLinkChildren)
            {
                if (orphan.Type == DialogNodeType.Entry)
                {
                    dialog.Entries.Remove(orphan);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Removed orphaned link child Entry from list: '{orphan.DisplayText}'");
                }
                else
                {
                    dialog.Replies.Remove(orphan);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Removed orphaned link child Reply from list: '{orphan.DisplayText}'");
                }

                // Note: Pointers from/to this orphaned node will be cleaned up by RemoveOrphanedPointers
            }
        }

        /// <summary>
        /// Gets all incoming pointers to a specific node from anywhere in the dialog.
        /// </summary>
        private List<DialogPtr> GetIncomingPointers(Dialog dialog, DialogNode targetNode)
        {
            var incomingPointers = new List<DialogPtr>();

            // Check Starts
            foreach (var start in dialog.Starts)
            {
                if (start.Node == targetNode)
                    incomingPointers.Add(start);
            }

            // Check Entry pointers
            foreach (var entry in dialog.Entries)
            {
                foreach (var ptr in entry.Pointers)
                {
                    if (ptr.Node == targetNode)
                        incomingPointers.Add(ptr);
                }
            }

            // Check Reply pointers
            foreach (var reply in dialog.Replies)
            {
                foreach (var ptr in reply.Pointers)
                {
                    if (ptr.Node == targetNode)
                        incomingPointers.Add(ptr);
                }
            }

            return incomingPointers;
        }

        /// <summary>
        /// Finds which node contains a specific pointer.
        /// </summary>
        private DialogNode? FindPointerParent(Dialog dialog, DialogPtr pointer)
        {
            // Check Entries
            foreach (var entry in dialog.Entries)
            {
                if (entry.Pointers.Contains(pointer))
                    return entry;
            }

            // Check Replies
            foreach (var reply in dialog.Replies)
            {
                if (reply.Pointers.Contains(pointer))
                    return reply;
            }

            // Pointer is in Starts or doesn't exist
            return null;
        }

        // NOTE: The following methods are DEPRECATED and preserved for reference only.
        // Orphan container functionality has been replaced by ScrapManager (Scrap Tab).
        // These methods are no longer called but kept for potential future reference.

        /*
        /// <summary>
        /// DEPRECATED: Finds orphaned nodes (nodes not reachable from START pointers).
        /// Replaced by ScrapManager - orphaned nodes now handled via Scrap Tab.
        /// </summary>
        private List<DialogNode> FindOrphanedNodes(Dialog dialog)
        {
            // Implementation preserved in MainViewModel git history
            // Deprecated: 2025-11 in favor of ScrapManager
            throw new NotImplementedException("Deprecated - use ScrapManager instead");
        }

        /// <summary>
        /// DEPRECATED: Creates synthetic container nodes for orphaned dialog nodes.
        /// Replaced by ScrapManager - orphaned nodes now moved to Scrap Tab.
        /// </summary>
        private void CreateOrUpdateOrphanContainers(Dialog dialog, List<DialogNode> orphanedNodes)
        {
            // Implementation preserved in MainViewModel git history
            // Deprecated: 2025-11 in favor of ScrapManager
            throw new NotImplementedException("Deprecated - use ScrapManager instead");
        }

        /// <summary>
        /// DEPRECATED: Synchronously detects and containerizes orphans.
        /// Replaced by ScrapManager - orphaned nodes now moved to Scrap Tab.
        /// </summary>
        private void DetectAndContainerizeOrphansSync(Dialog dialog)
        {
            // Implementation preserved in MainViewModel git history
            // Deprecated: 2025-11 in favor of ScrapManager
            throw new NotImplementedException("Deprecated - use ScrapManager instead");
        }

        /// <summary>
        /// DEPRECATED: Collects reachable nodes for orphan detection.
        /// Replaced by ScrapManager - orphan detection no longer needed.
        /// </summary>
        private void CollectReachableNodesForOrphanDetection(DialogNode node, HashSet<DialogNode> reachableNodes)
        {
            // Implementation preserved in MainViewModel git history
            // Deprecated: 2025-11 in favor of ScrapManager
            throw new NotImplementedException("Deprecated - use ScrapManager instead");
        }
        */
    }
}

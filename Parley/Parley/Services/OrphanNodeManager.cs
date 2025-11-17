using System.Collections.Generic;
using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Services
{
    /// <summary>
    /// Service responsible for orphan node detection and cleanup.
    /// Extracted from MainViewModel to improve separation of concerns.
    ///
    /// NOTE: Most orphan container functionality has been deprecated in favor of ScrapManager.
    /// This service primarily handles orphaned pointer cleanup after deletions.
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

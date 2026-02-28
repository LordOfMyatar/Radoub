using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service responsible for node add/delete/move operations.
    /// Extracted from MainViewModel to improve separation of concerns.
    /// Split into partials (#1540): base + MoveOperations + TreeHelpers.
    ///
    /// Handles:
    /// 1. Adding nodes (AddSmartNode, AddEntryNode, AddPCReplyNode)
    /// 2. Deleting nodes with link checking and scrap management
    /// 3. Moving nodes up/down in parent's child list
    /// 4. Finding parent nodes and siblings
    /// </summary>
    public partial class NodeOperationsManager
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
    }
}

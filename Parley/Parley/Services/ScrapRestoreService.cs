using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using Parley.Models;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Handles restoration of scrapped nodes back into dialog trees.
    /// Extracted from ScrapManager for single responsibility (#1271).
    /// </summary>
    public class ScrapRestoreService
    {
        private readonly ScrapSerializationService _serialization;

        public ScrapRestoreService(ScrapSerializationService serialization)
        {
            _serialization = serialization;
        }

        /// <summary>
        /// Restores a single node from scrap to the specified parent location.
        /// </summary>
        public RestoreResult RestoreSingleNode(
            ScrapEntry entry,
            Dialog dialog,
            TreeViewSafeNode? selectedParent,
            IndexManager indexManager)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"RestoreSingleNode called - entryId: {entry.Id}");

            if (dialog == null)
            {
                return new RestoreResult { Success = false, StatusMessage = "No dialog loaded" };
            }

            if (selectedParent == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Restore failed - no parent selected");
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "Select a location in the tree to restore to (root or parent node)"
                };
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Restoring to parent: {selectedParent.DisplayText} (Type: {selectedParent.GetType().Name})");

            var node = _serialization.DeserializeNode(entry.SerializedNode);
            if (node == null)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, "Failed to retrieve node from scrap manager");
                return new RestoreResult { Success = false, StatusMessage = "Failed to retrieve node from scrap" };
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Node retrieved from scrap: Type={node.Type}, Text={node.Text?.Strings.Values.FirstOrDefault()}");

            var validationResult = ValidateRestoreTarget(node, selectedParent);
            if (validationResult != null)
                return validationResult;

            AddNodeToDialog(node, dialog);
            var nodeIndex = (uint)dialog.GetNodeIndex(node, node.Type);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Node index: {nodeIndex}");

            var ptr = CreatePointer(node, nodeIndex, dialog);
            var statusMessage = AttachToParent(ptr, dialog, selectedParent);

            dialog.LinkRegistry.RegisterLink(ptr);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Pointer registered in LinkRegistry");

            indexManager.RecalculatePointerIndices(dialog);

            UnifiedLogger.LogApplication(LogLevel.INFO, "Restore completed successfully");
            return new RestoreResult
            {
                Success = true,
                StatusMessage = statusMessage,
                RestoredNode = node
            };
        }

        /// <summary>
        /// Restores an entire batch from scrap, reconstructing parent-child relationships.
        /// Issue #458, #124: "Restore with descendants" operation.
        /// </summary>
        public RestoreResult RestoreBatch(
            List<ScrapEntry> batchEntries,
            ScrapEntry batchRoot,
            Dialog dialog,
            TreeViewSafeNode? selectedParent,
            IndexManager indexManager)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Restoring batch with {batchEntries.Count} nodes from scrap");

            if (dialog == null)
            {
                return new RestoreResult { Success = false, StatusMessage = "No dialog loaded" };
            }

            if (selectedParent == null)
            {
                return new RestoreResult { Success = false, StatusMessage = "Select a location in the tree to restore to" };
            }

            var rootNode = _serialization.DeserializeNode(batchRoot.SerializedNode);
            if (rootNode == null)
            {
                return new RestoreResult { Success = false, StatusMessage = "Failed to deserialize batch root node" };
            }

            var validationResult = ValidateRestoreTarget(rootNode, selectedParent);
            if (validationResult != null)
                return validationResult;

            var entryToNode = DeserializeAndAddAll(batchEntries, dialog);
            ReconstructRelationships(batchEntries, batchRoot, entryToNode, dialog, selectedParent);

            indexManager.RecalculatePointerIndices(dialog);

            var message = $"Restored subtree ({batchEntries.Count} nodes)";
            UnifiedLogger.LogApplication(LogLevel.INFO, message);

            return new RestoreResult
            {
                Success = true,
                StatusMessage = message,
                RestoredNode = entryToNode.TryGetValue(batchRoot.Id, out var restoredRoot) ? restoredRoot : null
            };
        }

        /// <summary>
        /// Restores a selected entry and all its descendants from scrap.
        /// Unlike RestoreBatch, this only restores the selected subtree, not the entire batch.
        /// </summary>
        public RestoreResult RestoreSubtree(
            List<ScrapEntry> subtreeEntries,
            ScrapEntry rootEntry,
            Dialog? dialog,
            TreeViewSafeNode? selectedParent,
            IndexManager? indexManager)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Restoring subtree with {subtreeEntries.Count} nodes from scrap");

            if (dialog == null)
            {
                return new RestoreResult { Success = false, StatusMessage = "No dialog loaded" };
            }

            if (selectedParent == null)
            {
                return new RestoreResult { Success = false, StatusMessage = "Select a location in the tree to restore to" };
            }

            var rootNode = _serialization.DeserializeNode(rootEntry.SerializedNode);
            if (rootNode == null)
            {
                return new RestoreResult { Success = false, StatusMessage = "Failed to deserialize node" };
            }

            var validationResult = ValidateRestoreTarget(rootNode, selectedParent);
            if (validationResult != null)
                return validationResult;

            var entryToNode = DeserializeAndAddAll(subtreeEntries, dialog);
            ReconstructRelationships(subtreeEntries, rootEntry, entryToNode, dialog, selectedParent);

            indexManager?.RecalculatePointerIndices(dialog);

            var message = $"Restored subtree ({subtreeEntries.Count} node{(subtreeEntries.Count > 1 ? "s" : "")})";
            UnifiedLogger.LogApplication(LogLevel.INFO, message);

            return new RestoreResult
            {
                Success = true,
                StatusMessage = message,
                RestoredNode = entryToNode.TryGetValue(rootEntry.Id, out var restored) ? restored : null
            };
        }

        /// <summary>
        /// Validates that a node can be placed at the selected location.
        /// Returns null if valid, or a failure RestoreResult if invalid.
        /// </summary>
        private RestoreResult? ValidateRestoreTarget(DialogNode node, TreeViewSafeNode selectedParent)
        {
            if (selectedParent is TreeViewRootNode && node.Type != DialogNodeType.Entry)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot restore PC Reply to root level");
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "Only NPC Entry nodes can be restored to root level"
                };
            }

            if (!(selectedParent is TreeViewRootNode) && selectedParent?.OriginalNode != null)
            {
                var parentNode = selectedParent.OriginalNode;
                if (node.Type == DialogNodeType.Entry && parentNode.Type == DialogNodeType.Entry)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Invalid structure: Entry under Entry");
                    return new RestoreResult
                    {
                        Success = false,
                        StatusMessage = "NPC Entry nodes cannot be children of other NPC Entry nodes"
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Deserializes all entries and adds the resulting nodes to the dialog.
        /// Returns a mapping from entry ID to deserialized node.
        /// </summary>
        private Dictionary<string, DialogNode> DeserializeAndAddAll(
            List<ScrapEntry> entries, Dialog dialog)
        {
            var entryToNode = new Dictionary<string, DialogNode>();

            foreach (var scrapEntry in entries)
            {
                var node = _serialization.DeserializeNode(scrapEntry.SerializedNode);
                if (node == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Failed to deserialize node: {scrapEntry.NodeText}");
                    continue;
                }

                AddNodeToDialog(node, dialog);
                entryToNode[scrapEntry.Id] = node;
            }

            return entryToNode;
        }

        /// <summary>
        /// Reconstructs parent-child pointer relationships for a set of restored entries.
        /// </summary>
        private void ReconstructRelationships(
            List<ScrapEntry> entries,
            ScrapEntry rootEntry,
            Dictionary<string, DialogNode> entryToNode,
            Dialog dialog,
            TreeViewSafeNode selectedParent)
        {
            foreach (var scrapEntry in entries)
            {
                if (!entryToNode.TryGetValue(scrapEntry.Id, out var node))
                    continue;

                var nodeIndex = (uint)dialog.GetNodeIndex(node, node.Type);
                var ptr = CreatePointer(node, nodeIndex, dialog);

                if (scrapEntry.Id == rootEntry.Id)
                {
                    AttachToParent(ptr, dialog, selectedParent);
                }
                else if (!string.IsNullOrEmpty(scrapEntry.ParentEntryId) &&
                         entryToNode.TryGetValue(scrapEntry.ParentEntryId, out var parentNode))
                {
                    parentNode.Pointers.Add(ptr);
                }
                else
                {
                    // Orphan — add under root entry
                    if (entryToNode.TryGetValue(rootEntry.Id, out var rootNode))
                    {
                        rootNode.Pointers.Add(ptr);
                    }
                }

                dialog.LinkRegistry.RegisterLink(ptr);
            }
        }

        private void AddNodeToDialog(DialogNode node, Dialog dialog)
        {
            if (node.Type == DialogNodeType.Entry)
            {
                dialog.Entries.Add(node);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added to Entries list (index {dialog.Entries.Count - 1})");
            }
            else
            {
                dialog.Replies.Add(node);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added to Replies list (index {dialog.Replies.Count - 1})");
            }
        }

        private DialogPtr CreatePointer(DialogNode node, uint nodeIndex, Dialog dialog)
        {
            return new DialogPtr
            {
                Node = node,
                Type = node.Type,
                Index = nodeIndex,
                IsLink = false,
                ScriptAppears = "",
                ConditionParams = new Dictionary<string, string>(),
                Comment = "[Restored from scrap]",
                Parent = dialog
            };
        }

        private string AttachToParent(DialogPtr ptr, Dialog dialog, TreeViewSafeNode selectedParent)
        {
            if (selectedParent is TreeViewRootNode)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Restoring to root level");
                ptr.IsStart = true;
                dialog.Starts.Add(ptr);
                UnifiedLogger.LogApplication(LogLevel.INFO, "Node restored to root level");
                return "Restored node to root level";
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Restoring as child of {selectedParent!.DisplayText}");
                selectedParent.OriginalNode.Pointers.Add(ptr);
                selectedParent.IsExpanded = true;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Node restored under {selectedParent.DisplayText}");
                return $"Restored node under {selectedParent.DisplayText}";
            }
        }
    }
}

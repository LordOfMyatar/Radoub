using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using Parley.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Result of a restore operation with status information
    /// </summary>
    public class RestoreResult
    {
        public bool Success { get; set; }
        public string StatusMessage { get; set; } = "";
        public DialogNode? RestoredNode { get; set; }
    }

    /// <summary>
    /// Manages the Scrap Tab functionality - storing deleted/cut nodes in user preferences
    /// rather than polluting the DLG file with orphan containers
    /// </summary>
    public class ScrapManager
    {
        private readonly string _scrapFilePath;
        private ScrapData _scrapData;
        private readonly JsonSerializerOptions _jsonOptions;

        public ObservableCollection<ScrapEntry> ScrapEntries { get; }
        public event EventHandler<int>? ScrapCountChanged;

        public ScrapManager()
        {
            // Store in user's ~/Radoub/Parley/Cache folder
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub", "Parley", "Cache"
            );
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"ScrapManager: Cache folder = {cachePath}");

            Directory.CreateDirectory(cachePath);
            _scrapFilePath = Path.Combine(cachePath, "scrap.json");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"ScrapManager: Scrap file path = {_scrapFilePath}");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            ScrapEntries = new ObservableCollection<ScrapEntry>();
            _scrapData = LoadScrapData();
            UpdateScrapEntries();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"ScrapManager initialized with {ScrapEntries.Count} entries");
        }

        /// <summary>
        /// Add nodes to the scrap for a specific file.
        /// Returns the batchId used, allowing related operations to share the same batch.
        /// </summary>
        public string AddToScrap(string filePath, List<DialogNode> nodes, string operation = "deleted",
            Dictionary<DialogNode, (int level, DialogNode? parent)>? hierarchyInfo = null,
            string? existingBatchId = null)
        {
            if (nodes == null || nodes.Count == 0) return existingBatchId ?? Guid.NewGuid().ToString();

            var sanitizedPath = SanitizePath(filePath);
            var timestamp = DateTime.UtcNow;

            // Use existing batch ID if provided (for related operations like orphan cleanup)
            // Otherwise generate a new one for subtree tracking (#458, #124)
            var batchId = existingBatchId ?? Guid.NewGuid().ToString();
            var isFirstInBatch = existingBatchId == null;

            // First pass: create entries and build node-to-entry mapping
            var nodeToEntry = new Dictionary<DialogNode, ScrapEntry>();
            var entries = new List<ScrapEntry>();

            foreach (var node in nodes)
            {
                var entry = new ScrapEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    FilePath = sanitizedPath,
                    Timestamp = timestamp,
                    Operation = operation,
                    NodeType = node.Type.ToString(),
                    NodeText = GetNodePreviewText(node),
                    Speaker = node.Speaker,
                    OriginalIndex = GetNodeIndex(node),
                    SerializedNode = SerializeNode(node),
                    DeletionBatchId = batchId
                };

                // Add hierarchy information if available
                if (hierarchyInfo != null && hierarchyInfo.TryGetValue(node, out var info))
                {
                    entry.NestingLevel = info.level;
                    entry.ParentNodeText = info.parent != null ? GetNodePreviewText(info.parent) : null;
                }

                nodeToEntry[node] = entry;
                entries.Add(entry);
            }

            // Second pass: set parent-child relationships and identify batch root
            if (hierarchyInfo != null)
            {
                // Find the root of the batch (node with no parent in this batch)
                var childCounts = new Dictionary<string, int>();

                foreach (var node in nodes)
                {
                    var entry = nodeToEntry[node];

                    if (hierarchyInfo.TryGetValue(node, out var info) && info.parent != null)
                    {
                        // Check if parent is also in this batch
                        if (nodeToEntry.TryGetValue(info.parent, out var parentEntry))
                        {
                            entry.ParentEntryId = parentEntry.Id;

                            // Count children for each parent
                            if (!childCounts.ContainsKey(parentEntry.Id))
                            {
                                childCounts[parentEntry.Id] = 0;
                            }
                            childCounts[parentEntry.Id]++;
                        }
                    }
                }

                // Update child counts
                foreach (var entry in entries)
                {
                    if (childCounts.TryGetValue(entry.Id, out var count))
                    {
                        entry.ChildCount = count;
                    }
                }

                // Mark the batch root (first entry with no parent in this batch)
                // Only if this is a new batch (not adding to existing)
                if (isFirstInBatch)
                {
                    var batchRoot = entries.FirstOrDefault(e => e.ParentEntryId == null);
                    if (batchRoot != null)
                    {
                        batchRoot.IsBatchRoot = true;
                        // Update child count to reflect total descendants
                        batchRoot.ChildCount = entries.Count - 1;
                    }
                }
            }
            else if (isFirstInBatch)
            {
                // No hierarchy info and this is a new batch
                if (entries.Count == 1)
                {
                    // Single node deletion - it's the batch root
                    entries[0].IsBatchRoot = true;
                }
                else if (entries.Count > 0)
                {
                    // No hierarchy info - mark first as root for display purposes
                    entries[0].IsBatchRoot = true;
                }
            }
            // If adding to existing batch, don't mark any as batch root

            // Add all entries to scrap data
            _scrapData.Entries.AddRange(entries);

            // If adding to an existing batch, update the batch root's child count
            if (!isFirstInBatch)
            {
                var existingRoot = _scrapData.Entries.FirstOrDefault(e => e.DeletionBatchId == batchId && e.IsBatchRoot);
                if (existingRoot != null)
                {
                    // Count all entries in this batch (excluding the root)
                    existingRoot.ChildCount = _scrapData.Entries.Count(e => e.DeletionBatchId == batchId) - 1;
                }
            }

            SaveScrapData();
            UpdateScrapEntriesForFile(filePath);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Added {nodes.Count} nodes to scrap from {sanitizedPath} ({operation}), batch={batchId[..8]}");

            return batchId;
        }

        /// <summary>
        /// Get a scrap entry by ID (does not remove from scrap)
        /// </summary>
        public ScrapEntry? GetEntryById(string entryId)
        {
            return _scrapData.Entries.FirstOrDefault(e => e.Id == entryId);
        }

        /// <summary>
        /// Get a node from scrap by ID (does not remove from scrap)
        /// </summary>
        public DialogNode? GetNodeFromScrap(string entryId)
        {
            var entry = _scrapData.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry == null) return null;

            try
            {
                var node = DeserializeNode(entry.SerializedNode);
                if (node != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Retrieved node from scrap: {entry.NodeText}");
                }
                return node;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to get node from scrap: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Remove an entry from scrap after successful restoration
        /// </summary>
        public void RemoveFromScrap(string entryId)
        {
            var entry = _scrapData.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry != null)
            {
                var filePath = entry.FilePath;
                _scrapData.Entries.Remove(entry);
                SaveScrapData();
                UpdateScrapEntriesForFile(filePath);

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Removed restored node from scrap: {entry.NodeText}");
            }
        }

        /// <summary>
        /// Remove an entire batch from scrap (for "Restore with descendants")
        /// </summary>
        public void RemoveBatchFromScrap(string batchId)
        {
            if (string.IsNullOrEmpty(batchId)) return;

            var batchEntries = _scrapData.Entries.Where(e => e.DeletionBatchId == batchId).ToList();
            if (batchEntries.Count == 0) return;

            var filePath = batchEntries.First().FilePath;
            foreach (var entry in batchEntries)
            {
                _scrapData.Entries.Remove(entry);
            }

            SaveScrapData();
            UpdateScrapEntriesForFile(filePath);

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Removed batch of {batchEntries.Count} nodes from scrap, batchId={batchId[..8]}");
        }

        /// <summary>
        /// Get all entries in a deletion batch, ordered for reconstruction (parent before children)
        /// </summary>
        public List<ScrapEntry> GetBatchEntries(string batchId)
        {
            if (string.IsNullOrEmpty(batchId))
                return new List<ScrapEntry>();

            var batchEntries = _scrapData.Entries
                .Where(e => e.DeletionBatchId == batchId)
                .ToList();

            // Sort by nesting level to ensure parents come before children
            return batchEntries.OrderBy(e => e.NestingLevel).ToList();
        }

        /// <summary>
        /// Get the batch root entry for a given batch ID
        /// </summary>
        public ScrapEntry? GetBatchRoot(string batchId)
        {
            if (string.IsNullOrEmpty(batchId))
                return null;

            return _scrapData.Entries.FirstOrDefault(e =>
                e.DeletionBatchId == batchId && e.IsBatchRoot);
        }

        /// <summary>
        /// Get child entries within a batch that have the given parent entry ID
        /// </summary>
        public List<ScrapEntry> GetBatchChildren(string parentEntryId)
        {
            if (string.IsNullOrEmpty(parentEntryId))
                return new List<ScrapEntry>();

            return _scrapData.Entries
                .Where(e => e.ParentEntryId == parentEntryId)
                .OrderBy(e => e.NodeText) // Stable ordering for predictable restoration
                .ToList();
        }

        /// <summary>
        /// Check if an entry has descendants in the same batch
        /// </summary>
        public bool HasBatchDescendants(string entryId)
        {
            return _scrapData.Entries.Any(e => e.ParentEntryId == entryId);
        }

        /// <summary>
        /// Clear all scrap entries for a specific file
        /// </summary>
        public void ClearScrapForFile(string filePath)
        {
            var sanitizedPath = SanitizePath(filePath);
            var removed = _scrapData.Entries.RemoveAll(e => e.FilePath == sanitizedPath);

            if (removed > 0)
            {
                SaveScrapData();
                UpdateScrapEntriesForFile(filePath);
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Cleared {removed} scrap entries for {sanitizedPath}");
            }
        }

        /// <summary>
        /// Clear all scrap entries
        /// </summary>
        public void ClearAllScrap()
        {
            var count = _scrapData.Entries.Count;
            _scrapData.Entries.Clear();
            SaveScrapData();
            UpdateScrapEntries();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Cleared all {count} scrap entries");
        }

        /// <summary>
        /// Get scrap entries for the current file
        /// </summary>
        public List<ScrapEntry> GetScrapForFile(string filePath)
        {
            var sanitizedPath = SanitizePath(filePath);
            return _scrapData.Entries
                .Where(e => e.FilePath == sanitizedPath)
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }

        /// <summary>
        /// Get count of scrap entries for a specific file
        /// </summary>
        public int GetScrapCount(string filePath)
        {
            var sanitizedPath = SanitizePath(filePath);
            return _scrapData.Entries.Count(e => e.FilePath == sanitizedPath);
        }

        /// <summary>
        /// Remove scrap entries that match nodes now present in the dialog.
        /// Called after undo to clean up entries for restored nodes.
        /// </summary>
        public void RemoveRestoredNodes(string filePath, Dialog dialog)
        {
            if (dialog == null || string.IsNullOrEmpty(filePath)) return;

            var sanitizedPath = SanitizePath(filePath);
            var entriesToRemove = new List<ScrapEntry>();

            // Get all dialog node texts for matching
            var entryTexts = new HashSet<string>(
                dialog.Entries.Select(e => GetNodePreviewText(e))
            );
            var replyTexts = new HashSet<string>(
                dialog.Replies.Select(r => GetNodePreviewText(r))
            );

            foreach (var entry in _scrapData.Entries.Where(e => e.FilePath == sanitizedPath))
            {
                // Check if a node with matching text and type exists in the dialog
                var matchingSet = entry.NodeType == "Entry" ? entryTexts : replyTexts;
                if (matchingSet.Contains(entry.NodeText))
                {
                    entriesToRemove.Add(entry);
                }
            }

            if (entriesToRemove.Count > 0)
            {
                foreach (var entry in entriesToRemove)
                {
                    _scrapData.Entries.Remove(entry);
                }
                SaveScrapData();
                UpdateScrapEntriesForFile(filePath);
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Removed {entriesToRemove.Count} restored nodes from scrap after undo");
            }
        }

        /// <summary>
        /// Re-add nodes to scrap that were deleted again via Redo.
        /// Issue #370: Redo should restore deleted nodes to scrap panel.
        /// </summary>
        /// <param name="filePath">Current file path</param>
        /// <param name="dialogBefore">Dialog state before redo (from undo stack)</param>
        /// <param name="dialogAfter">Dialog state after redo</param>
        public void RestoreDeletedNodesToScrap(string filePath, Dialog dialogBefore, Dialog dialogAfter)
        {
            if (dialogBefore == null || dialogAfter == null || string.IsNullOrEmpty(filePath)) return;

            // Find nodes that were in dialogBefore but not in dialogAfter
            var nodesRemovedByRedo = new List<DialogNode>();

            // Check entries
            var afterEntryTexts = new HashSet<string>(
                dialogAfter.Entries.Select(e => GetNodePreviewText(e))
            );
            foreach (var node in dialogBefore.Entries)
            {
                var text = GetNodePreviewText(node);
                if (!afterEntryTexts.Contains(text))
                {
                    nodesRemovedByRedo.Add(node);
                }
            }

            // Check replies
            var afterReplyTexts = new HashSet<string>(
                dialogAfter.Replies.Select(r => GetNodePreviewText(r))
            );
            foreach (var node in dialogBefore.Replies)
            {
                var text = GetNodePreviewText(node);
                if (!afterReplyTexts.Contains(text))
                {
                    nodesRemovedByRedo.Add(node);
                }
            }

            if (nodesRemovedByRedo.Count > 0)
            {
                // Re-add these nodes to scrap with "redone" operation
                AddToScrap(filePath, nodesRemovedByRedo, "redone");
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Restored {nodesRemovedByRedo.Count} nodes to scrap after redo");
            }
        }

        /// <summary>
        /// Update the visible scrap entries for a specific file.
        /// Builds hierarchical tree structure for TreeView display.
        /// </summary>
        public void UpdateScrapEntriesForFile(string? filePath)
        {
            ScrapEntries.Clear();

            if (string.IsNullOrEmpty(filePath))
            {
                // No file loaded, show no entries
                ScrapCountChanged?.Invoke(this, 0);
                return;
            }

            var sanitizedPath = SanitizePath(filePath);

            // Get all entries for this file
            var fileEntries = _scrapData.Entries
                .Where(e => e.FilePath == sanitizedPath)
                .ToList();

            // Build tree structure: batch roots with children populated
            foreach (var rootEntry in fileEntries
                .Where(e => e.IsBatchRoot)
                .OrderByDescending(e => e.Timestamp))
            {
                // Clear and rebuild children collection
                rootEntry.Children.Clear();
                BuildChildrenRecursive(rootEntry, fileEntries);
                ScrapEntries.Add(rootEntry);
            }

            ScrapCountChanged?.Invoke(this, GetScrapCount(filePath));
        }

        /// <summary>
        /// Recursively build children for a scrap entry from the batch.
        /// </summary>
        private void BuildChildrenRecursive(ScrapEntry parent, List<ScrapEntry> allEntries)
        {
            var children = allEntries
                .Where(e => e.ParentEntryId == parent.Id)
                .OrderBy(e => e.OriginalIndex)
                .ToList();

            foreach (var child in children)
            {
                child.Children.Clear();
                BuildChildrenRecursive(child, allEntries);
                parent.Children.Add(child);
            }
        }

        /// <summary>
        /// Restores a node from scrap to the specified parent location.
        /// Handles validation, pointer creation, and index recalculation.
        /// </summary>
        public RestoreResult RestoreFromScrap(
            string entryId,
            Dialog dialog,
            TreeViewSafeNode? selectedParent,
            IndexManager indexManager)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"RestoreFromScrap called - entryId: {entryId}");

            if (dialog == null)
            {
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "No dialog loaded"
                };
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

            // Get the node from scrap WITHOUT removing it yet
            var node = GetNodeFromScrap(entryId);
            if (node == null)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, "Failed to retrieve node from scrap manager");
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "Failed to retrieve node from scrap"
                };
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Node retrieved from scrap: Type={node.Type}, Text={node.Text?.Strings.Values.FirstOrDefault()}");

            // Validate restoration target BEFORE making ANY changes
            if (selectedParent is TreeViewRootNode && node.Type != DialogNodeType.Entry)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot restore PC Reply to root level");
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "Only NPC Entry nodes can be restored to root level"
                };
            }

            // Validate dialog structure rules
            if (!(selectedParent is TreeViewRootNode) && selectedParent?.OriginalNode != null)
            {
                var parentNode = selectedParent.OriginalNode;

                // NPC Entry can only be child of PC Reply (not another NPC Entry)
                if (node.Type == DialogNodeType.Entry && parentNode.Type == DialogNodeType.Entry)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Invalid structure: Entry under Entry");
                    return new RestoreResult
                    {
                        Success = false,
                        StatusMessage = "NPC Entry nodes cannot be children of other NPC Entry nodes"
                    };
                }

                // PC Reply can be under NPC Entry OR NPC Reply (branching PC responses)
                // No validation needed for PC Reply - both parent types are valid
            }

            // ALL validations passed - now make the changes

            // Add the restored node to the appropriate list
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

            // Get the index of the restored node
            var nodeIndex = (uint)dialog.GetNodeIndex(node, node.Type);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Node index: {nodeIndex}");

            // Create pointer to restored node
            var ptr = new DialogPtr
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

            // Add to root level or under selected parent
            string statusMessage;
            if (selectedParent is TreeViewRootNode)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Restoring to root level");
                ptr.IsStart = true;
                dialog.Starts.Add(ptr);
                statusMessage = "Restored node to root level";
                UnifiedLogger.LogApplication(LogLevel.INFO, "Node restored to root level");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Restoring as child of {selectedParent!.DisplayText}");
                selectedParent.OriginalNode.Pointers.Add(ptr);
                selectedParent.IsExpanded = true;
                statusMessage = $"Restored node under {selectedParent.DisplayText}";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Node restored under {selectedParent.DisplayText}");
            }

            // Register the pointer
            dialog.LinkRegistry.RegisterLink(ptr);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Pointer registered in LinkRegistry");

            // Recalculate indices
            indexManager.RecalculatePointerIndices(dialog);

            // Only remove from scrap after successful restoration
            RemoveFromScrap(entryId);

            UnifiedLogger.LogApplication(LogLevel.INFO, "Restore completed successfully");
            return new RestoreResult
            {
                Success = true,
                StatusMessage = statusMessage,
                RestoredNode = node
            };
        }

        /// <summary>
        /// Restores an entire batch (subtree) from scrap to the specified parent location.
        /// Reconstructs the original parent-child relationships between nodes.
        /// Issue #458, #124: "Restore with descendants" operation.
        /// </summary>
        public RestoreResult RestoreBatchFromScrap(
            string entryId,
            Dialog dialog,
            TreeViewSafeNode? selectedParent,
            IndexManager indexManager)
        {
            var entry = _scrapData.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry == null)
            {
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "Entry not found in scrap"
                };
            }

            var batchId = entry.DeletionBatchId;
            if (string.IsNullOrEmpty(batchId))
            {
                // No batch - fall back to single node restore
                return RestoreFromScrap(entryId, dialog, selectedParent, indexManager);
            }

            // Get all entries in the batch, ordered by nesting level (parents first)
            var batchEntries = GetBatchEntries(batchId);
            if (batchEntries.Count <= 1)
            {
                // Single node batch - use normal restore
                return RestoreFromScrap(entryId, dialog, selectedParent, indexManager);
            }

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Restoring batch with {batchEntries.Count} nodes from scrap");

            if (dialog == null)
            {
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "No dialog loaded"
                };
            }

            if (selectedParent == null)
            {
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "Select a location in the tree to restore to"
                };
            }

            // Get the batch root node
            var batchRoot = batchEntries.FirstOrDefault(e => e.IsBatchRoot) ?? batchEntries.First();
            var rootNode = DeserializeNode(batchRoot.SerializedNode);
            if (rootNode == null)
            {
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "Failed to deserialize batch root node"
                };
            }

            // Validate root node can be placed at selected location
            if (selectedParent is TreeViewRootNode && rootNode.Type != DialogNodeType.Entry)
            {
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "Only NPC Entry nodes can be restored to root level"
                };
            }

            if (!(selectedParent is TreeViewRootNode) && selectedParent?.OriginalNode != null)
            {
                var parentNode = selectedParent.OriginalNode;
                if (rootNode.Type == DialogNodeType.Entry && parentNode.Type == DialogNodeType.Entry)
                {
                    return new RestoreResult
                    {
                        Success = false,
                        StatusMessage = "NPC Entry nodes cannot be children of other NPC Entry nodes"
                    };
                }
            }

            // Build mapping from entry ID to restored node
            var entryToNode = new Dictionary<string, DialogNode>();

            // First pass: restore all nodes to their respective lists
            foreach (var scrapEntry in batchEntries)
            {
                var node = DeserializeNode(scrapEntry.SerializedNode);
                if (node == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Failed to deserialize node in batch: {scrapEntry.NodeText}");
                    continue;
                }

                // Add to appropriate list
                if (node.Type == DialogNodeType.Entry)
                {
                    dialog.Entries.Add(node);
                }
                else
                {
                    dialog.Replies.Add(node);
                }

                entryToNode[scrapEntry.Id] = node;
            }

            // Second pass: reconstruct parent-child relationships
            foreach (var scrapEntry in batchEntries)
            {
                if (!entryToNode.TryGetValue(scrapEntry.Id, out var node))
                    continue;

                var nodeIndex = (uint)dialog.GetNodeIndex(node, node.Type);

                // Create pointer to this node
                var ptr = new DialogPtr
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

                // Determine parent
                if (scrapEntry.Id == batchRoot.Id)
                {
                    // This is the batch root - add under selected parent
                    if (selectedParent is TreeViewRootNode)
                    {
                        ptr.IsStart = true;
                        dialog.Starts.Add(ptr);
                    }
                    else
                    {
                        selectedParent!.OriginalNode.Pointers.Add(ptr);
                        selectedParent.IsExpanded = true;
                    }
                }
                else if (!string.IsNullOrEmpty(scrapEntry.ParentEntryId) &&
                         entryToNode.TryGetValue(scrapEntry.ParentEntryId, out var parentNode))
                {
                    // Add as child of the parent node in the batch
                    parentNode.Pointers.Add(ptr);
                }
                else
                {
                    // Orphan in batch - add under batch root
                    if (entryToNode.TryGetValue(batchRoot.Id, out var batchRootNode))
                    {
                        batchRootNode.Pointers.Add(ptr);
                    }
                }

                // Register the pointer
                dialog.LinkRegistry.RegisterLink(ptr);
            }

            // Recalculate indices
            indexManager.RecalculatePointerIndices(dialog);

            // Remove entire batch from scrap
            RemoveBatchFromScrap(batchId);

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
        /// Unlike RestoreBatchFromScrap, this only restores the selected subtree, not the entire batch.
        /// </summary>
        public RestoreResult RestoreSubtreeFromScrap(string entryId, Dialog? dialog, TreeViewSafeNode? selectedParent, IndexManager? indexManager)
        {
            var rootEntry = GetEntryById(entryId);
            if (rootEntry == null)
            {
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "Entry not found in scrap"
                };
            }

            // Collect all entries in the subtree (root + descendants)
            var subtreeEntries = new List<ScrapEntry> { rootEntry };
            CollectDescendants(rootEntry, subtreeEntries);

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Restoring subtree with {subtreeEntries.Count} nodes from scrap");

            if (dialog == null)
            {
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "No dialog loaded"
                };
            }

            if (selectedParent == null)
            {
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "Select a location in the tree to restore to"
                };
            }

            // Deserialize root node for validation
            var rootNode = DeserializeNode(rootEntry.SerializedNode);
            if (rootNode == null)
            {
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "Failed to deserialize node"
                };
            }

            // Validate root node can be placed at selected location
            if (selectedParent is TreeViewRootNode && rootNode.Type != DialogNodeType.Entry)
            {
                return new RestoreResult
                {
                    Success = false,
                    StatusMessage = "Only NPC Entry nodes can be restored to root level"
                };
            }

            if (!(selectedParent is TreeViewRootNode) && selectedParent?.OriginalNode != null)
            {
                var parentNode = selectedParent.OriginalNode;
                if (rootNode.Type == DialogNodeType.Entry && parentNode.Type == DialogNodeType.Entry)
                {
                    return new RestoreResult
                    {
                        Success = false,
                        StatusMessage = "NPC Entry nodes cannot be children of other NPC Entry nodes"
                    };
                }
            }

            // Build mapping from entry ID to restored node
            var entryToNode = new Dictionary<string, DialogNode>();

            // First pass: restore all nodes to their respective lists
            foreach (var scrapEntry in subtreeEntries)
            {
                var node = DeserializeNode(scrapEntry.SerializedNode);
                if (node == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Failed to deserialize node in subtree: {scrapEntry.NodeText}");
                    continue;
                }

                // Add to appropriate list
                if (node.Type == DialogNodeType.Entry)
                {
                    dialog.Entries.Add(node);
                }
                else
                {
                    dialog.Replies.Add(node);
                }

                entryToNode[scrapEntry.Id] = node;
            }

            // Second pass: reconstruct parent-child relationships
            foreach (var scrapEntry in subtreeEntries)
            {
                if (!entryToNode.TryGetValue(scrapEntry.Id, out var node))
                    continue;

                var nodeIndex = (uint)dialog.GetNodeIndex(node, node.Type);

                // Create pointer to this node
                var ptr = new DialogPtr
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

                // Determine parent
                if (scrapEntry.Id == rootEntry.Id)
                {
                    // This is the subtree root - add under selected parent
                    if (selectedParent is TreeViewRootNode)
                    {
                        ptr.IsStart = true;
                        dialog.Starts.Add(ptr);
                    }
                    else
                    {
                        selectedParent!.OriginalNode.Pointers.Add(ptr);
                        selectedParent.IsExpanded = true;
                    }
                }
                else if (!string.IsNullOrEmpty(scrapEntry.ParentEntryId) &&
                         entryToNode.TryGetValue(scrapEntry.ParentEntryId, out var parentNode))
                {
                    // Add as child of the parent node in the subtree
                    parentNode.Pointers.Add(ptr);
                }
                else
                {
                    // Orphan in subtree - add under subtree root
                    if (entryToNode.TryGetValue(rootEntry.Id, out var subtreeRoot))
                    {
                        subtreeRoot.Pointers.Add(ptr);
                    }
                }

                // Register the pointer
                dialog.LinkRegistry.RegisterLink(ptr);
            }

            // Recalculate indices
            indexManager?.RecalculatePointerIndices(dialog);

            // Remove only the restored entries from scrap
            foreach (var entry in subtreeEntries)
            {
                _scrapData.Entries.Remove(entry);
            }

            // Check if batch root needs to be updated (if we restored a partial subtree)
            var batchId = rootEntry.DeletionBatchId;
            if (!string.IsNullOrEmpty(batchId))
            {
                UpdateBatchAfterPartialRestore(batchId);
            }

            SaveScrapData();
            UpdateScrapEntriesForFile(GetCurrentFilePath());

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
        /// Recursively collect all descendants of an entry using its Children collection.
        /// </summary>
        private void CollectDescendants(ScrapEntry parent, List<ScrapEntry> results)
        {
            foreach (var child in parent.Children)
            {
                results.Add(child);
                CollectDescendants(child, results);
            }
        }

        /// <summary>
        /// Update batch structure after a partial restore (when only some entries were restored).
        /// If the batch root was restored, promote another entry to be the new root.
        /// </summary>
        private void UpdateBatchAfterPartialRestore(string batchId)
        {
            var remainingEntries = _scrapData.Entries
                .Where(e => e.DeletionBatchId == batchId)
                .ToList();

            if (remainingEntries.Count == 0)
                return; // Entire batch was restored

            // Check if we need to promote a new batch root
            var hasRoot = remainingEntries.Any(e => e.IsBatchRoot);
            if (!hasRoot && remainingEntries.Count > 0)
            {
                // Promote the first remaining entry without a parent in the batch as new root
                var newRoot = remainingEntries.FirstOrDefault(e =>
                    string.IsNullOrEmpty(e.ParentEntryId) ||
                    !remainingEntries.Any(r => r.Id == e.ParentEntryId));

                if (newRoot != null)
                {
                    newRoot.IsBatchRoot = true;
                    newRoot.ChildCount = remainingEntries.Count - 1;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Promoted new batch root: {newRoot.NodeText}");
                }
            }
            else if (hasRoot)
            {
                // Update the existing root's child count
                var root = remainingEntries.First(e => e.IsBatchRoot);
                root.ChildCount = remainingEntries.Count - 1;
            }
        }

        /// <summary>
        /// Get the current file path from the first entry in ScrapEntries.
        /// Used for refreshing after partial restore.
        /// </summary>
        private string GetCurrentFilePath()
        {
            return ScrapEntries.FirstOrDefault()?.FilePath ?? "";
        }

        /// <summary>
        /// Swap NPC/PC roles for a scrap entry and all its children.
        /// Entry nodes become Reply nodes and vice versa.
        /// </summary>
        public bool SwapRoles(ScrapEntry entry)
        {
            if (entry == null) return false;

            try
            {
                // Collect all entries to swap (entry + descendants)
                var entriesToSwap = new List<ScrapEntry> { entry };
                CollectDescendants(entry, entriesToSwap);

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Swapping roles for {entriesToSwap.Count} node(s)");

                foreach (var scrapEntry in entriesToSwap)
                {
                    SwapEntryRole(scrapEntry);
                }

                SaveScrapData();
                UpdateScrapEntriesForFile(GetCurrentFilePath());

                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to swap roles: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Swap the role of a single scrap entry (Entry ↔ Reply).
        /// Updates both the metadata and the serialized node.
        /// </summary>
        private void SwapEntryRole(ScrapEntry entry)
        {
            // Swap the NodeType display
            var wasEntry = entry.NodeType == "Entry";
            entry.NodeType = wasEntry ? "Reply" : "Entry";

            // Also swap the underlying serialized node
            try
            {
                var node = DeserializeNode(entry.SerializedNode);
                if (node != null)
                {
                    // Swap the actual node type
                    node.Type = wasEntry ? DialogNodeType.Reply : DialogNodeType.Entry;
                    entry.SerializedNode = SerializeNode(node);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Failed to swap serialized node: {ex.Message}");
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Swapped: {entry.NodeText} -> {entry.NodeType}");
        }

        private void UpdateScrapEntries()
        {
            // INTERNAL USE ONLY - Called during initialization before any file is loaded.
            // After initialization, always use UpdateScrapEntriesForFile() to filter by current file.
            // This method loads ALL batch roots which is needed at startup.
            ScrapEntries.Clear();
            foreach (var entry in _scrapData.Entries
                .Where(e => e.IsBatchRoot)
                .OrderByDescending(e => e.Timestamp))
            {
                ScrapEntries.Add(entry);
            }
            ScrapCountChanged?.Invoke(this, ScrapEntries.Count);
        }

        private string SanitizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";

            // Replace user home directory with ~
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.StartsWith(homeDir, StringComparison.OrdinalIgnoreCase))
            {
                path = "~" + path.Substring(homeDir.Length);
            }

            // Normalize path separators
            return path.Replace('\\', '/');
        }

        private string GetNodePreviewText(DialogNode node)
        {
            // Get text from the LocString dictionary
            var text = "";
            if (node.Text != null && node.Text.Strings.Count > 0)
            {
                // Try to get English (0) or first available language
                if (node.Text.Strings.TryGetValue(0, out var englishText))
                {
                    text = englishText;
                }
                else
                {
                    text = node.Text.Strings.Values.FirstOrDefault() ?? "";
                }
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                text = node.Comment ?? "[No text]";
            }

            // Truncate for preview
            if (text.Length > 50)
            {
                text = text.Substring(0, 47) + "...";
            }

            return text;
        }

        private int GetNodeIndex(DialogNode node)
        {
            // This would need access to the current dialog to get the actual index
            // For now, return -1 as unknown
            return -1;
        }

        private string SerializeNode(DialogNode node)
        {
            try
            {
                // Create a simplified version for serialization to avoid circular references
                var simplified = new
                {
                    Type = node.Type.ToString(),
                    Text = node.Text?.Strings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Speaker = node.Speaker,
                    Comment = node.Comment,
                    Sound = node.Sound,
                    ScriptAction = node.ScriptAction,
                    Animation = node.Animation,
                    AnimationLoop = node.AnimationLoop,
                    Delay = node.Delay,
                    Quest = node.Quest,
                    QuestEntry = node.QuestEntry,
                    ActionParams = node.ActionParams,
                    // Don't serialize pointers to avoid circular references
                };

                return JsonSerializer.Serialize(simplified, _jsonOptions);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to serialize node for scrap: {ex.Message}");
                return "{}";
            }
        }

        private DialogNode? DeserializeNode(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var node = new DialogNode
                {
                    Type = Enum.Parse<DialogNodeType>(root.GetProperty("type").GetString() ?? "Entry"),
                    Speaker = root.TryGetProperty("speaker", out var speaker) ? speaker.GetString() ?? "" : "",
                    Comment = root.TryGetProperty("comment", out var comment) ? comment.GetString() ?? "" : "",
                    Sound = root.TryGetProperty("sound", out var sound) ? sound.GetString() ?? "" : "",
                    ScriptAction = root.TryGetProperty("scriptAction", out var scriptAction) ? scriptAction.GetString() ?? "" : "",
                    Animation = root.TryGetProperty("animation", out var animation) ? (DialogAnimation)animation.GetInt32() : DialogAnimation.None,
                    AnimationLoop = root.TryGetProperty("animationLoop", out var animationLoop) ? animationLoop.GetBoolean() : false,
                    Delay = root.TryGetProperty("delay", out var delay) ? delay.GetUInt32() : 0u,
                    Quest = root.TryGetProperty("quest", out var quest) ? quest.GetString() ?? "" : "",
                    QuestEntry = root.TryGetProperty("questEntry", out var questEntry) ? questEntry.GetUInt32() : 0u,
                    Pointers = new List<DialogPtr>() // Empty pointers for restored node
                };

                // Restore text if present
                if (root.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.Object)
                {
                    node.Text = new LocString();
                    foreach (var kvp in textProp.EnumerateObject())
                    {
                        if (int.TryParse(kvp.Name, out var langId))
                        {
                            node.Text.Strings[langId] = kvp.Value.GetString() ?? "";
                        }
                    }
                }

                // Restore action params if present
                if (root.TryGetProperty("actionParams", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Object)
                {
                    node.ActionParams = new Dictionary<string, string>();
                    foreach (var kvp in paramsProp.EnumerateObject())
                    {
                        node.ActionParams[kvp.Name] = kvp.Value.GetString() ?? "";
                    }
                }

                return node;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to deserialize node from scrap: {ex.Message}");
                return null;
            }
        }

        private ScrapData LoadScrapData()
        {
            try
            {
                if (File.Exists(_scrapFilePath))
                {
                    var json = File.ReadAllText(_scrapFilePath);
                    var data = JsonSerializer.Deserialize<ScrapData>(json, _jsonOptions);
                    if (data != null)
                    {
                        MigrateLegacyEntries(data);
                        return data;
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load scrap data: {ex.Message}");
            }

            return new ScrapData();
        }

        /// <summary>
        /// Migrates and repairs scrap entries with inconsistent batch tracking.
        /// - Legacy entries (pre-0.1.78) without DeletionBatchId are treated as batch roots
        /// - Entries with ParentEntryId should never be batch roots (#476 fix)
        /// - Ensures each batch has exactly one root
        /// </summary>
        private void MigrateLegacyEntries(ScrapData data)
        {
            var migrated = 0;
            var repaired = 0;

            // First pass: fix legacy entries without batch ID
            foreach (var entry in data.Entries)
            {
                // Legacy entries won't have DeletionBatchId set
                // Treat each legacy entry as its own batch root
                if (string.IsNullOrEmpty(entry.DeletionBatchId))
                {
                    entry.DeletionBatchId = entry.Id; // Use entry ID as batch ID
                    entry.IsBatchRoot = true;
                    migrated++;
                }
            }

            // Second pass: fix inconsistent batch roots (#476)
            // Entries with ParentEntryId are children, never roots
            foreach (var entry in data.Entries)
            {
                if (!string.IsNullOrEmpty(entry.ParentEntryId) && entry.IsBatchRoot)
                {
                    entry.IsBatchRoot = false;
                    repaired++;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Fixed child entry incorrectly marked as root: {entry.NodeText}");
                }
            }

            // Third pass: ensure each batch has a root
            var batches = data.Entries
                .Where(e => !string.IsNullOrEmpty(e.DeletionBatchId))
                .GroupBy(e => e.DeletionBatchId);

            foreach (var batch in batches)
            {
                if (!batch.Any(e => e.IsBatchRoot))
                {
                    // Find the entry with no parent in this batch
                    var newRoot = batch.FirstOrDefault(e =>
                        string.IsNullOrEmpty(e.ParentEntryId) ||
                        !batch.Any(b => b.Id == e.ParentEntryId));

                    if (newRoot != null)
                    {
                        newRoot.IsBatchRoot = true;
                        newRoot.ChildCount = batch.Count() - 1;
                        repaired++;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"Assigned batch root: {newRoot.NodeText}");
                    }
                }
            }

            if (migrated > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Migrated {migrated} legacy scrap entries to batch format");
            }
            if (repaired > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Repaired {repaired} scrap entries with inconsistent batch tracking");
            }
        }

        private void SaveScrapData()
        {
            try
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"SaveScrapData: Saving {_scrapData.Entries.Count} entries to {_scrapFilePath}");

                // Clean up old entries (older than 30 days)
                var cutoffDate = DateTime.UtcNow.AddDays(-30);
                var removed = _scrapData.Entries.RemoveAll(e => e.Timestamp < cutoffDate);
                if (removed > 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"SaveScrapData: Removed {removed} old entries");
                }

                var json = JsonSerializer.Serialize(_scrapData, _jsonOptions);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"SaveScrapData: Serialized {json.Length} characters");

                File.WriteAllText(_scrapFilePath, json);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"SaveScrapData: Successfully saved {_scrapData.Entries.Count} entries");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save scrap data: {ex.Message}");
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Stack trace: {ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Container for all scrap data
    /// </summary>
    public class ScrapData
    {
        public List<ScrapEntry> Entries { get; set; } = new List<ScrapEntry>();
        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// Represents a single scrapped node entry
    /// </summary>
    public class ScrapEntry
    {
        public string Id { get; set; } = "";
        public string FilePath { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Operation { get; set; } = "";
        public string NodeType { get; set; } = "";
        public string NodeText { get; set; } = "";
        public string? Speaker { get; set; }
        public int OriginalIndex { get; set; }
        public string SerializedNode { get; set; } = "";
        public int NestingLevel { get; set; } = 0;
        public string? ParentNodeText { get; set; }

        // Batch tracking for subtree restoration (#458, #124)
        /// <summary>
        /// Groups nodes deleted in the same operation (e.g., deleting a node with children).
        /// All entries with the same DeletionBatchId were deleted together.
        /// </summary>
        public string? DeletionBatchId { get; set; }

        /// <summary>
        /// Links to the parent entry's Id within the same deletion batch.
        /// Used to reconstruct parent-child relationships during restoration.
        /// Null for root nodes in the batch.
        /// </summary>
        public string? ParentEntryId { get; set; }

        /// <summary>
        /// Number of direct children in the deletion batch.
        /// Used for display purposes (e.g., "[3 children]").
        /// </summary>
        public int ChildCount { get; set; } = 0;

        /// <summary>
        /// True if this is the root node of the deletion batch (first node deleted).
        /// Used to identify the entry to show in batch-grouped UI.
        /// </summary>
        public bool IsBatchRoot { get; set; } = false;

        [JsonIgnore]
        public string DisplayText
        {
            get
            {
                var childInfo = ChildCount > 0 ? $" [+{ChildCount}]" : "";
                return $"[{NodeTypeDisplay}] {NodeText}{childInfo}";
            }
        }

        [JsonIgnore]
        public string NodeTypeDisplay
        {
            get
            {
                // Better labels for node types
                return NodeType switch
                {
                    "Entry" when NestingLevel == 0 => "NPC Entry",  // Root level entry
                    "Entry" => "NPC Reply",  // Nested entry (NPC response)
                    "Reply" => "PC Reply",   // Player response
                    _ => NodeType
                };
            }
        }

        /// <summary>
        /// Display indicator for child count in the deletion batch.
        /// </summary>
        [JsonIgnore]
        public string ChildCountDisplay => ChildCount > 0 ? $"+{ChildCount} children" : "";

        /// <summary>
        /// True if this entry has children in the deletion batch.
        /// </summary>
        [JsonIgnore]
        public bool HasBatchChildren => ChildCount > 0;

        [JsonIgnore]
        public string HierarchyInfo
        {
            get
            {
                // Show batch info for batch roots with children
                if (IsBatchRoot && ChildCount > 0)
                {
                    return $"Subtree ({ChildCount + 1} nodes)";
                }

                if (NestingLevel == 0) return "Root level";
                if (!string.IsNullOrEmpty(ParentNodeText))
                {
                    var parentPreview = ParentNodeText?.Length > 30
                        ? ParentNodeText.Substring(0, 27) + "..."
                        : ParentNodeText;
                    return $"Under: {parentPreview}";
                }
                return $"Depth: {NestingLevel}";
            }
        }

        [JsonIgnore]
        public string TimeAgo
        {
            get
            {
                var elapsed = DateTime.UtcNow - Timestamp;
                if (elapsed.TotalMinutes < 1) return "just now";
                if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
                if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
                if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";
                return Timestamp.ToString("yyyy-MM-dd");
            }
        }

        /// <summary>
        /// Children of this entry within the same deletion batch.
        /// Populated by ScrapManager.BuildScrapTree() for TreeView display.
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<ScrapEntry> Children { get; } = new ObservableCollection<ScrapEntry>();

        /// <summary>
        /// Whether this entry is expanded in the TreeView.
        /// </summary>
        [JsonIgnore]
        public bool IsExpanded { get; set; } = false;

        /// <summary>
        /// Color for node type display (matches dialog tree styling).
        /// </summary>
        [JsonIgnore]
        public string NodeColor => NodeType == "Entry" ? "#2196F3" : "#4CAF50";
    }
}
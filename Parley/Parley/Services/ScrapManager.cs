using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using Parley.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages the Scrap Tab functionality - storing deleted/cut nodes in user preferences
    /// rather than polluting the DLG file with orphan containers.
    /// Delegates to ScrapSerializationService and ScrapRestoreService (#1271).
    /// </summary>
    public class ScrapManager
    {
        private ScrapData _scrapData;
        private readonly ScrapSerializationService _serialization;
        private readonly ScrapRestoreService _restoreService;

        public ObservableCollection<ScrapEntry> ScrapEntries { get; }
        public event EventHandler<int>? ScrapCountChanged;

        public ScrapManager()
        {
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub", "Parley", "Cache"
            );
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"ScrapManager: Cache folder = {cachePath}");

            Directory.CreateDirectory(cachePath);
            var scrapFilePath = Path.Combine(cachePath, "scrap.json");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"ScrapManager: Scrap file path = {scrapFilePath}");

            _serialization = new ScrapSerializationService(scrapFilePath);
            _restoreService = new ScrapRestoreService(_serialization);

            ScrapEntries = new ObservableCollection<ScrapEntry>();
            _scrapData = _serialization.LoadScrapData();
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
                    SerializedNode = _serialization.SerializeNode(node),
                    DeletionBatchId = batchId
                };

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
                var childCounts = new Dictionary<string, int>();

                foreach (var node in nodes)
                {
                    var entry = nodeToEntry[node];

                    if (hierarchyInfo.TryGetValue(node, out var info) && info.parent != null)
                    {
                        if (nodeToEntry.TryGetValue(info.parent, out var parentEntry))
                        {
                            entry.ParentEntryId = parentEntry.Id;

                            if (!childCounts.ContainsKey(parentEntry.Id))
                            {
                                childCounts[parentEntry.Id] = 0;
                            }
                            childCounts[parentEntry.Id]++;
                        }
                    }
                }

                foreach (var entry in entries)
                {
                    if (childCounts.TryGetValue(entry.Id, out var count))
                    {
                        entry.ChildCount = count;
                    }
                }

                if (isFirstInBatch)
                {
                    var batchRoot = entries.FirstOrDefault(e => e.ParentEntryId == null);
                    if (batchRoot != null)
                    {
                        batchRoot.IsBatchRoot = true;
                        batchRoot.ChildCount = entries.Count - 1;
                    }
                }
            }
            else if (isFirstInBatch)
            {
                if (entries.Count >= 1)
                {
                    entries[0].IsBatchRoot = true;
                }
            }

            _scrapData.Entries.AddRange(entries);

            if (!isFirstInBatch)
            {
                var existingRoot = _scrapData.Entries.FirstOrDefault(e => e.DeletionBatchId == batchId && e.IsBatchRoot);
                if (existingRoot != null)
                {
                    existingRoot.ChildCount = _scrapData.Entries.Count(e => e.DeletionBatchId == batchId) - 1;
                }
            }

            _serialization.SaveScrapData(_scrapData);
            UpdateScrapEntriesForFile(filePath);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Added {nodes.Count} nodes to scrap from {sanitizedPath} ({operation}), batch={batchId[..8]}");

            return batchId;
        }

        public ScrapEntry? GetEntryById(string entryId)
        {
            return _scrapData.Entries.FirstOrDefault(e => e.Id == entryId);
        }

        public DialogNode? GetNodeFromScrap(string entryId)
        {
            var entry = _scrapData.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry == null) return null;

            try
            {
                var node = _serialization.DeserializeNode(entry.SerializedNode);
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

        public void RemoveFromScrap(string entryId)
        {
            var entry = _scrapData.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry != null)
            {
                var filePath = entry.FilePath;
                _scrapData.Entries.Remove(entry);
                _serialization.SaveScrapData(_scrapData);
                UpdateScrapEntriesForFile(filePath);

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Removed restored node from scrap: {entry.NodeText}");
            }
        }

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

            _serialization.SaveScrapData(_scrapData);
            UpdateScrapEntriesForFile(filePath);

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Removed batch of {batchEntries.Count} nodes from scrap, batchId={batchId[..8]}");
        }

        public List<ScrapEntry> GetBatchEntries(string batchId)
        {
            if (string.IsNullOrEmpty(batchId))
                return new List<ScrapEntry>();

            return _scrapData.Entries
                .Where(e => e.DeletionBatchId == batchId)
                .OrderBy(e => e.NestingLevel)
                .ToList();
        }

        public ScrapEntry? GetBatchRoot(string batchId)
        {
            if (string.IsNullOrEmpty(batchId))
                return null;

            return _scrapData.Entries.FirstOrDefault(e =>
                e.DeletionBatchId == batchId && e.IsBatchRoot);
        }

        public List<ScrapEntry> GetBatchChildren(string parentEntryId)
        {
            if (string.IsNullOrEmpty(parentEntryId))
                return new List<ScrapEntry>();

            return _scrapData.Entries
                .Where(e => e.ParentEntryId == parentEntryId)
                .OrderBy(e => e.NodeText)
                .ToList();
        }

        public bool HasBatchDescendants(string entryId)
        {
            return _scrapData.Entries.Any(e => e.ParentEntryId == entryId);
        }

        public void ClearScrapForFile(string filePath)
        {
            var sanitizedPath = SanitizePath(filePath);
            var removed = _scrapData.Entries.RemoveAll(e => e.FilePath == sanitizedPath);

            if (removed > 0)
            {
                _serialization.SaveScrapData(_scrapData);
                UpdateScrapEntriesForFile(filePath);
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Cleared {removed} scrap entries for {sanitizedPath}");
            }
        }

        public void ClearAllScrap()
        {
            var count = _scrapData.Entries.Count;
            _scrapData.Entries.Clear();
            _serialization.SaveScrapData(_scrapData);
            UpdateScrapEntries();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Cleared all {count} scrap entries");
        }

        public List<ScrapEntry> GetScrapForFile(string filePath)
        {
            var sanitizedPath = SanitizePath(filePath);
            return _scrapData.Entries
                .Where(e => e.FilePath == sanitizedPath)
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }

        public int GetScrapCount(string filePath)
        {
            var sanitizedPath = SanitizePath(filePath);
            return _scrapData.Entries.Count(e => e.FilePath == sanitizedPath);
        }

        public void RemoveRestoredNodes(string filePath, Dialog dialog)
        {
            if (dialog == null || string.IsNullOrEmpty(filePath)) return;

            var sanitizedPath = SanitizePath(filePath);
            var entriesToRemove = new List<ScrapEntry>();

            var entryTexts = new HashSet<string>(
                dialog.Entries.Select(e => GetNodePreviewText(e))
            );
            var replyTexts = new HashSet<string>(
                dialog.Replies.Select(r => GetNodePreviewText(r))
            );

            foreach (var entry in _scrapData.Entries.Where(e => e.FilePath == sanitizedPath))
            {
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
                _serialization.SaveScrapData(_scrapData);
                UpdateScrapEntriesForFile(filePath);
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Removed {entriesToRemove.Count} restored nodes from scrap after undo");
            }
        }

        public void RestoreDeletedNodesToScrap(string filePath, Dialog dialogBefore, Dialog dialogAfter)
        {
            if (dialogBefore == null || dialogAfter == null || string.IsNullOrEmpty(filePath)) return;

            var nodesRemovedByRedo = new List<DialogNode>();

            var afterEntryTexts = new HashSet<string>(
                dialogAfter.Entries.Select(e => GetNodePreviewText(e))
            );
            foreach (var node in dialogBefore.Entries)
            {
                if (!afterEntryTexts.Contains(GetNodePreviewText(node)))
                {
                    nodesRemovedByRedo.Add(node);
                }
            }

            var afterReplyTexts = new HashSet<string>(
                dialogAfter.Replies.Select(r => GetNodePreviewText(r))
            );
            foreach (var node in dialogBefore.Replies)
            {
                if (!afterReplyTexts.Contains(GetNodePreviewText(node)))
                {
                    nodesRemovedByRedo.Add(node);
                }
            }

            if (nodesRemovedByRedo.Count > 0)
            {
                AddToScrap(filePath, nodesRemovedByRedo, "redone");
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Restored {nodesRemovedByRedo.Count} nodes to scrap after redo");
            }
        }

        /// <summary>
        /// Restores a node from scrap to the specified parent location.
        /// </summary>
        public RestoreResult RestoreFromScrap(
            string entryId,
            Dialog dialog,
            TreeViewSafeNode? selectedParent,
            IndexManager indexManager)
        {
            var entry = _scrapData.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry == null)
            {
                return new RestoreResult { Success = false, StatusMessage = "Entry not found in scrap" };
            }

            var result = _restoreService.RestoreSingleNode(entry, dialog, selectedParent, indexManager);
            if (result.Success)
            {
                RemoveFromScrap(entryId);
            }
            return result;
        }

        /// <summary>
        /// Restores an entire batch (subtree) from scrap to the specified parent location.
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
                return new RestoreResult { Success = false, StatusMessage = "Entry not found in scrap" };
            }

            var batchId = entry.DeletionBatchId;
            if (string.IsNullOrEmpty(batchId))
            {
                return RestoreFromScrap(entryId, dialog, selectedParent, indexManager);
            }

            var batchEntries = GetBatchEntries(batchId);
            if (batchEntries.Count <= 1)
            {
                return RestoreFromScrap(entryId, dialog, selectedParent, indexManager);
            }

            var batchRoot = batchEntries.FirstOrDefault(e => e.IsBatchRoot) ?? batchEntries.First();
            var result = _restoreService.RestoreBatch(batchEntries, batchRoot, dialog, selectedParent, indexManager);
            if (result.Success)
            {
                RemoveBatchFromScrap(batchId);
            }
            return result;
        }

        /// <summary>
        /// Restores a selected entry and all its descendants from scrap.
        /// </summary>
        public RestoreResult RestoreSubtreeFromScrap(
            string entryId, Dialog? dialog, TreeViewSafeNode? selectedParent, IndexManager? indexManager)
        {
            var rootEntry = GetEntryById(entryId);
            if (rootEntry == null)
            {
                return new RestoreResult { Success = false, StatusMessage = "Entry not found in scrap" };
            }

            var subtreeEntries = new List<ScrapEntry> { rootEntry };
            CollectDescendants(rootEntry, subtreeEntries);

            if (subtreeEntries.Count <= 1 && indexManager != null)
            {
                return RestoreFromScrap(entryId, dialog!, selectedParent, indexManager);
            }

            var result = _restoreService.RestoreSubtree(subtreeEntries, rootEntry, dialog, selectedParent, indexManager);
            if (result.Success)
            {
                foreach (var e in subtreeEntries)
                {
                    _scrapData.Entries.Remove(e);
                }

                var batchId = rootEntry.DeletionBatchId;
                if (!string.IsNullOrEmpty(batchId))
                {
                    UpdateBatchAfterPartialRestore(batchId);
                }

                _serialization.SaveScrapData(_scrapData);
                UpdateScrapEntriesForFile(GetCurrentFilePath());
            }
            return result;
        }

        public void UpdateScrapEntriesForFile(string? filePath)
        {
            ScrapEntries.Clear();

            if (string.IsNullOrEmpty(filePath))
            {
                ScrapCountChanged?.Invoke(this, 0);
                return;
            }

            var sanitizedPath = SanitizePath(filePath);
            var fileEntries = _scrapData.Entries
                .Where(e => e.FilePath == sanitizedPath)
                .ToList();

            foreach (var rootEntry in fileEntries
                .Where(e => e.IsBatchRoot)
                .OrderByDescending(e => e.Timestamp))
            {
                rootEntry.Children.Clear();
                BuildChildrenRecursive(rootEntry, fileEntries);
                ScrapEntries.Add(rootEntry);
            }

            ScrapCountChanged?.Invoke(this, GetScrapCount(filePath));
        }

        /// <summary>
        /// Swap NPC/PC roles for a scrap entry and all its children.
        /// </summary>
        public bool SwapRoles(ScrapEntry entry)
        {
            if (entry == null) return false;

            try
            {
                var entriesToSwap = new List<ScrapEntry> { entry };
                CollectDescendants(entry, entriesToSwap);

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Swapping roles for {entriesToSwap.Count} node(s)");

                foreach (var scrapEntry in entriesToSwap)
                {
                    SwapEntryRole(scrapEntry);
                }

                _serialization.SaveScrapData(_scrapData);
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

        private void SwapEntryRole(ScrapEntry entry)
        {
            var wasEntry = entry.NodeType == "Entry";
            entry.NodeType = wasEntry ? "Reply" : "Entry";

            try
            {
                var node = _serialization.DeserializeNode(entry.SerializedNode);
                if (node != null)
                {
                    node.Type = wasEntry ? DialogNodeType.Reply : DialogNodeType.Entry;
                    entry.SerializedNode = _serialization.SerializeNode(node);
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

        private void CollectDescendants(ScrapEntry parent, List<ScrapEntry> results)
        {
            foreach (var child in parent.Children)
            {
                results.Add(child);
                CollectDescendants(child, results);
            }
        }

        private void UpdateBatchAfterPartialRestore(string batchId)
        {
            var remainingEntries = _scrapData.Entries
                .Where(e => e.DeletionBatchId == batchId)
                .ToList();

            if (remainingEntries.Count == 0)
                return;

            var hasRoot = remainingEntries.Any(e => e.IsBatchRoot);
            if (!hasRoot && remainingEntries.Count > 0)
            {
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
                var root = remainingEntries.First(e => e.IsBatchRoot);
                root.ChildCount = remainingEntries.Count - 1;
            }
        }

        private string GetCurrentFilePath()
        {
            return ScrapEntries.FirstOrDefault()?.FilePath ?? "";
        }

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

        private void UpdateScrapEntries()
        {
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

            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.StartsWith(homeDir, StringComparison.OrdinalIgnoreCase))
            {
                path = "~" + path.Substring(homeDir.Length);
            }

            return path.Replace('\\', '/');
        }

        internal static string GetNodePreviewText(DialogNode node)
        {
            var text = "";
            if (node.Text != null && node.Text.Strings.Count > 0)
            {
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

            if (text.Length > 50)
            {
                text = text.Substring(0, 47) + "...";
            }

            return text;
        }

        private int GetNodeIndex(DialogNode node)
        {
            return -1;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for ScrapRestoreService: single node restore, batch restore,
    /// subtree restore, and validation rules.
    /// </summary>
    public class ScrapRestoreServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ScrapSerializationService _serialization;
        private readonly ScrapRestoreService _service;
        private readonly IndexManager _indexManager;

        public ScrapRestoreServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"RestoreTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            var scrapPath = Path.Combine(_tempDir, "scrap.json");
            _serialization = new ScrapSerializationService(scrapPath);
            _service = new ScrapRestoreService(_serialization);
            _indexManager = new IndexManager();
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        #region RestoreSingleNode

        [Fact]
        public void RestoreSingleNode_NullDialog_Fails()
        {
            var entry = CreateScrapEntry(DialogNodeType.Entry, "Test");
            var parent = CreateParentNode(DialogNodeType.Reply);

            var result = _service.RestoreSingleNode(entry, null!, parent, _indexManager);

            Assert.False(result.Success);
            Assert.Contains("No dialog", result.StatusMessage);
        }

        [Fact]
        public void RestoreSingleNode_NullParent_Fails()
        {
            var entry = CreateScrapEntry(DialogNodeType.Entry, "Test");
            var dialog = new Dialog();

            var result = _service.RestoreSingleNode(entry, dialog, null, _indexManager);

            Assert.False(result.Success);
            Assert.Contains("Select a location", result.StatusMessage);
        }

        [Fact]
        public void RestoreSingleNode_InvalidSerializedNode_Fails()
        {
            var entry = new ScrapEntry
            {
                Id = "bad",
                SerializedNode = "not valid json"
            };
            var dialog = new Dialog();
            var parent = new TreeViewRootNode(dialog);

            var result = _service.RestoreSingleNode(entry, dialog, parent, _indexManager);

            Assert.False(result.Success);
            Assert.Contains("Failed to retrieve", result.StatusMessage);
        }

        [Fact]
        public void RestoreSingleNode_EntryToRoot_Succeeds()
        {
            var dialog = new Dialog();
            var rootNode = new TreeViewRootNode(dialog);
            var entry = CreateScrapEntry(DialogNodeType.Entry, "Restored greeting");

            var result = _service.RestoreSingleNode(entry, dialog, rootNode, _indexManager);

            Assert.True(result.Success);
            Assert.NotNull(result.RestoredNode);
            Assert.Equal(DialogNodeType.Entry, result.RestoredNode!.Type);
            Assert.Single(dialog.Entries);
            Assert.Single(dialog.Starts);
            Assert.Contains("root level", result.StatusMessage);
        }

        [Fact]
        public void RestoreSingleNode_ReplyUnderEntry_Succeeds()
        {
            var dialog = new Dialog();
            var entryNode = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Pointers = new List<DialogPtr>()
            };
            entryNode.Text.Add(0, "Parent entry");
            dialog.Entries.Add(entryNode);
            var parentWrapper = new TreeViewSafeNode(entryNode);

            var entry = CreateScrapEntry(DialogNodeType.Reply, "Player response");

            var result = _service.RestoreSingleNode(entry, dialog, parentWrapper, _indexManager);

            Assert.True(result.Success);
            Assert.NotNull(result.RestoredNode);
            Assert.Equal(DialogNodeType.Reply, result.RestoredNode!.Type);
            Assert.Single(dialog.Replies);
            Assert.Single(entryNode.Pointers);
        }

        [Fact]
        public void RestoreSingleNode_ReplyToRoot_Fails()
        {
            var dialog = new Dialog();
            var rootNode = new TreeViewRootNode(dialog);
            var entry = CreateScrapEntry(DialogNodeType.Reply, "PC reply at root");

            var result = _service.RestoreSingleNode(entry, dialog, rootNode, _indexManager);

            Assert.False(result.Success);
            Assert.Contains("Only NPC Entry", result.StatusMessage);
        }

        [Fact]
        public void RestoreSingleNode_EntryUnderEntry_Fails()
        {
            var dialog = new Dialog();
            var parentEntry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Pointers = new List<DialogPtr>()
            };
            dialog.Entries.Add(parentEntry);
            var parentWrapper = new TreeViewSafeNode(parentEntry);

            var entry = CreateScrapEntry(DialogNodeType.Entry, "Entry under entry");

            var result = _service.RestoreSingleNode(entry, dialog, parentWrapper, _indexManager);

            Assert.False(result.Success);
            Assert.Contains("cannot be children", result.StatusMessage);
        }

        #endregion

        #region RestoreBatch

        [Fact]
        public void RestoreBatch_NullDialog_Fails()
        {
            var entries = new List<ScrapEntry>();
            var root = CreateScrapEntry(DialogNodeType.Entry, "Root");

            var result = _service.RestoreBatch(entries, root, null!, null, _indexManager);

            Assert.False(result.Success);
        }

        [Fact]
        public void RestoreBatch_TwoNodeBatch_RestoresBoth()
        {
            var dialog = new Dialog();
            var rootNode = new TreeViewRootNode(dialog);

            var parentEntry = CreateScrapEntry(DialogNodeType.Entry, "NPC says hello");
            var childEntry = new ScrapEntry
            {
                Id = "child1",
                SerializedNode = _serialization.SerializeNode(new DialogNode
                {
                    Type = DialogNodeType.Reply,
                    Text = new LocString(),
                    Pointers = new List<DialogPtr>()
                }),
                DeletionBatchId = "batch1",
                ParentEntryId = parentEntry.Id,
                IsBatchRoot = false
            };

            // Set text on the child
            var childNode = _serialization.DeserializeNode(childEntry.SerializedNode);
            childNode!.Text!.Add(0, "Player responds");
            childEntry.SerializedNode = _serialization.SerializeNode(childNode);

            parentEntry.DeletionBatchId = "batch1";
            parentEntry.IsBatchRoot = true;
            parentEntry.ChildCount = 1;

            var batch = new List<ScrapEntry> { parentEntry, childEntry };

            var result = _service.RestoreBatch(batch, parentEntry, dialog, rootNode, _indexManager);

            Assert.True(result.Success);
            Assert.Contains("2 nodes", result.StatusMessage);
            Assert.Single(dialog.Entries);
            Assert.Single(dialog.Replies);
        }

        #endregion

        #region RestoreSubtree

        [Fact]
        public void RestoreSubtree_SingleNode_RestoresSuccessfully()
        {
            var dialog = new Dialog();
            var rootNode = new TreeViewRootNode(dialog);
            var entry = CreateScrapEntry(DialogNodeType.Entry, "Lone node");

            var result = _service.RestoreSubtree(
                new List<ScrapEntry> { entry }, entry, dialog, rootNode, _indexManager);

            Assert.True(result.Success);
            Assert.Single(dialog.Entries);
        }

        [Fact]
        public void RestoreSubtree_NullDialog_Fails()
        {
            var entry = CreateScrapEntry(DialogNodeType.Entry, "Test");

            var result = _service.RestoreSubtree(
                new List<ScrapEntry> { entry }, entry, null, null, null);

            Assert.False(result.Success);
            Assert.Contains("No dialog", result.StatusMessage);
        }

        [Fact]
        public void RestoreSubtree_NullParent_Fails()
        {
            var dialog = new Dialog();
            var entry = CreateScrapEntry(DialogNodeType.Entry, "Test");

            var result = _service.RestoreSubtree(
                new List<ScrapEntry> { entry }, entry, dialog, null, _indexManager);

            Assert.False(result.Success);
            Assert.Contains("Select a location", result.StatusMessage);
        }

        #endregion

        #region Helpers

        private ScrapEntry CreateScrapEntry(DialogNodeType type, string text)
        {
            var node = new DialogNode
            {
                Type = type,
                Text = new LocString(),
                Pointers = new List<DialogPtr>()
            };
            node.Text.Add(0, text);

            return new ScrapEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                SerializedNode = _serialization.SerializeNode(node),
                NodeType = type.ToString(),
                NodeText = text,
                Timestamp = DateTime.UtcNow
            };
        }

        private TreeViewSafeNode CreateParentNode(DialogNodeType type)
        {
            var node = new DialogNode
            {
                Type = type,
                Text = new LocString(),
                Pointers = new List<DialogPtr>()
            };
            node.Text.Add(0, "Parent");
            return new TreeViewSafeNode(node);
        }

        #endregion
    }
}

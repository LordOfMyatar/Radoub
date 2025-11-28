using System.Collections.Generic;
using Xunit;
using DialogEditor.Models;
using DialogEditor.Services;
using Parley.Models;

namespace Parley.Tests
{
    /// <summary>
    /// Unit tests for PasteOperationsManager.
    /// Tests paste operations including script preservation (Issue #196).
    /// </summary>
    public class PasteOperationsManagerTests
    {
        private readonly DialogClipboardService _clipboardService;
        private readonly NodeCloningService _cloningService;
        private readonly IndexManager _indexManager;
        private readonly PasteOperationsManager _pasteManager;

        public PasteOperationsManagerTests()
        {
            _clipboardService = new DialogClipboardService();
            _cloningService = new NodeCloningService();
            _indexManager = new IndexManager();
            _pasteManager = new PasteOperationsManager(_clipboardService, _cloningService, _indexManager);
        }

        #region Basic Paste Tests

        [Fact]
        public void PasteAsDuplicate_NoClipboardContent_ReturnsFailure()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var parent = CreateTreeViewNode(DialogNodeType.Entry, "Parent");

            // Act
            var result = _pasteManager.PasteAsDuplicate(dialog, parent);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("No node copied", result.StatusMessage);
        }

        [Fact]
        public void PasteAsDuplicate_NoParent_ReturnsFailure()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var node = CreateTestNode(DialogNodeType.Entry, "Test");
            dialog.Entries.Add(node);
            _clipboardService.CopyNode(node, dialog);

            // Act
            var result = _pasteManager.PasteAsDuplicate(dialog, null);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Select a parent", result.StatusMessage);
        }

        #endregion

        #region Script Preservation Tests (Issue #196)

        [Fact]
        public void PasteAsDuplicate_ToParent_PreservesScriptAppears()
        {
            // Arrange
            var dialog = CreateTestDialog();

            // Create parent (Entry) and child (Reply) nodes
            var parent = CreateTestNode(DialogNodeType.Entry, "Parent");
            var child = CreateTestNode(DialogNodeType.Reply, "Child with script");
            dialog.Entries.Add(parent);
            dialog.Replies.Add(child);

            // Create source pointer with ScriptAppears
            var sourcePointer = new DialogPtr
            {
                Node = child,
                Type = DialogNodeType.Reply,
                Index = 0,
                ScriptAppears = "lompqj_sc"
            };

            // Copy the node with source pointer
            _clipboardService.CopyNode(child, dialog, sourcePointer);

            // Create TreeView wrapper for parent
            var treeViewParent = CreateTreeViewNode(parent);

            // Act
            var result = _pasteManager.PasteAsDuplicate(dialog, treeViewParent);

            // Assert
            Assert.True(result.Success);
            Assert.Single(parent.Pointers);
            Assert.Equal("lompqj_sc", parent.Pointers[0].ScriptAppears);
        }

        [Fact]
        public void PasteAsDuplicate_ToParent_PreservesConditionParams()
        {
            // Arrange
            var dialog = CreateTestDialog();

            var parent = CreateTestNode(DialogNodeType.Entry, "Parent");
            var child = CreateTestNode(DialogNodeType.Reply, "Child with params");
            dialog.Entries.Add(parent);
            dialog.Replies.Add(child);

            var sourcePointer = new DialogPtr
            {
                Node = child,
                Type = DialogNodeType.Reply,
                Index = 0,
                ScriptAppears = "test_script",
                ConditionParams = new Dictionary<string, string>
                {
                    ["RATS"] = "1",
                    ["CATS"] = "2"
                }
            };

            _clipboardService.CopyNode(child, dialog, sourcePointer);
            var treeViewParent = CreateTreeViewNode(parent);

            // Act
            var result = _pasteManager.PasteAsDuplicate(dialog, treeViewParent);

            // Assert
            Assert.True(result.Success);
            Assert.Single(parent.Pointers);
            Assert.Equal(2, parent.Pointers[0].ConditionParams.Count);
            Assert.Equal("1", parent.Pointers[0].ConditionParams["RATS"]);
            Assert.Equal("2", parent.Pointers[0].ConditionParams["CATS"]);
        }

        [Fact]
        public void PasteAsDuplicate_ToRoot_PreservesScriptAppears()
        {
            // Arrange
            var dialog = CreateTestDialog();

            var entry = CreateTestNode(DialogNodeType.Entry, "Entry with script");
            dialog.Entries.Add(entry);

            var sourcePointer = new DialogPtr
            {
                Node = entry,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsStart = true,
                ScriptAppears = "root_condition"
            };

            _clipboardService.CopyNode(entry, dialog, sourcePointer);

            // Create ROOT node wrapper
            var rootNode = new TreeViewRootNode(dialog);

            // Act
            var result = _pasteManager.PasteAsDuplicate(dialog, rootNode);

            // Assert
            Assert.True(result.Success);
            Assert.Single(dialog.Starts);
            Assert.Equal("root_condition", dialog.Starts[0].ScriptAppears);
        }

        [Fact]
        public void PasteAsDuplicate_ToRoot_PreservesConditionParams()
        {
            // Arrange
            var dialog = CreateTestDialog();

            var entry = CreateTestNode(DialogNodeType.Entry, "Entry with params");
            dialog.Entries.Add(entry);

            var sourcePointer = new DialogPtr
            {
                Node = entry,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsStart = true,
                ConditionParams = new Dictionary<string, string>
                {
                    ["START_PARAM"] = "yes"
                }
            };

            _clipboardService.CopyNode(entry, dialog, sourcePointer);
            var rootNode = new TreeViewRootNode(dialog);

            // Act
            var result = _pasteManager.PasteAsDuplicate(dialog, rootNode);

            // Assert
            Assert.True(result.Success);
            Assert.Single(dialog.Starts);
            Assert.Single(dialog.Starts[0].ConditionParams);
            Assert.Equal("yes", dialog.Starts[0].ConditionParams["START_PARAM"]);
        }

        [Fact]
        public void PasteAsDuplicate_WithoutSourcePointer_HasEmptyScripts()
        {
            // Arrange
            var dialog = CreateTestDialog();

            var parent = CreateTestNode(DialogNodeType.Entry, "Parent");
            var child = CreateTestNode(DialogNodeType.Reply, "Child no scripts");
            dialog.Entries.Add(parent);
            dialog.Replies.Add(child);

            // Copy without source pointer
            _clipboardService.CopyNode(child, dialog);
            var treeViewParent = CreateTreeViewNode(parent);

            // Act
            var result = _pasteManager.PasteAsDuplicate(dialog, treeViewParent);

            // Assert
            Assert.True(result.Success);
            Assert.Single(parent.Pointers);
            Assert.Equal(string.Empty, parent.Pointers[0].ScriptAppears);
            Assert.Empty(parent.Pointers[0].ConditionParams);
        }

        [Fact]
        public void PasteAsDuplicate_MultiplePastes_EachHasIndependentParams()
        {
            // Arrange
            var dialog = CreateTestDialog();

            var parent = CreateTestNode(DialogNodeType.Entry, "Parent");
            var child = CreateTestNode(DialogNodeType.Reply, "Child");
            dialog.Entries.Add(parent);
            dialog.Replies.Add(child);

            var sourcePointer = new DialogPtr
            {
                Node = child,
                Type = DialogNodeType.Reply,
                Index = 0,
                ConditionParams = new Dictionary<string, string>
                {
                    ["KEY"] = "original"
                }
            };

            _clipboardService.CopyNode(child, dialog, sourcePointer);
            var treeViewParent = CreateTreeViewNode(parent);

            // Act - First paste
            _pasteManager.PasteAsDuplicate(dialog, treeViewParent);

            // Modify first pasted pointer's params
            parent.Pointers[0].ConditionParams["KEY"] = "modified";
            parent.Pointers[0].ConditionParams["NEW"] = "added";

            // Second paste
            _clipboardService.CopyNode(child, dialog, sourcePointer);
            _pasteManager.PasteAsDuplicate(dialog, treeViewParent);

            // Assert - Second paste should have original values, not modified
            Assert.Equal(2, parent.Pointers.Count);
            Assert.Equal("modified", parent.Pointers[0].ConditionParams["KEY"]);
            Assert.Equal("original", parent.Pointers[1].ConditionParams["KEY"]);
            Assert.False(parent.Pointers[1].ConditionParams.ContainsKey("NEW"));
        }

        #endregion

        #region Type Validation Tests

        [Fact]
        public void PasteAsDuplicate_SameTypeAsParent_ReturnsFailure()
        {
            // Arrange - Aurora rule: Entry can only have Reply children, Reply can only have Entry children
            var dialog = CreateTestDialog();

            var parent = CreateTestNode(DialogNodeType.Entry, "Parent Entry");
            var child = CreateTestNode(DialogNodeType.Entry, "Child Entry"); // Same type!
            dialog.Entries.Add(parent);
            dialog.Entries.Add(child);

            _clipboardService.CopyNode(child, dialog);
            var treeViewParent = CreateTreeViewNode(parent);

            // Act
            var result = _pasteManager.PasteAsDuplicate(dialog, treeViewParent);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("alternate", result.StatusMessage.ToLower());
        }

        [Fact]
        public void PasteAsDuplicate_PCReplyToRoot_ReturnsFailure()
        {
            // Arrange - PC Replies (no speaker) cannot be at ROOT
            var dialog = CreateTestDialog();

            var pcReply = CreateTestNode(DialogNodeType.Reply, "PC Reply");
            pcReply.Speaker = string.Empty; // PC Reply (no speaker)
            dialog.Replies.Add(pcReply);

            _clipboardService.CopyNode(pcReply, dialog);
            var rootNode = new TreeViewRootNode(dialog);

            // Act
            var result = _pasteManager.PasteAsDuplicate(dialog, rootNode);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("PC Reply", result.StatusMessage);
        }

        [Fact]
        public void PasteAsDuplicate_NPCReplyToRoot_ConvertsToEntry()
        {
            // Arrange - NPC Replies (has speaker) convert to Entry when pasted to ROOT
            var dialog = CreateTestDialog();

            var npcReply = CreateTestNode(DialogNodeType.Reply, "NPC Reply");
            npcReply.Speaker = "Chef"; // NPC Reply (has speaker)
            dialog.Replies.Add(npcReply);

            _clipboardService.CopyNode(npcReply, dialog);
            var rootNode = new TreeViewRootNode(dialog);

            // Act
            var result = _pasteManager.PasteAsDuplicate(dialog, rootNode);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.PastedNode);
            Assert.Equal(DialogNodeType.Entry, result.PastedNode.Type);
        }

        #endregion

        #region Helper Methods

        private Dialog CreateTestDialog()
        {
            return new Dialog();
        }

        private DialogNode CreateTestNode(DialogNodeType type, string text)
        {
            var node = new DialogNode
            {
                Type = type,
                Text = new LocString()
            };
            node.Text.Add(0, text);
            return node;
        }

        private TreeViewSafeNode CreateTreeViewNode(DialogNode node)
        {
            return new TreeViewSafeNode(node, null, 0, null);
        }

        private TreeViewSafeNode CreateTreeViewNode(DialogNodeType type, string text)
        {
            var node = CreateTestNode(type, text);
            return new TreeViewSafeNode(node, null, 0, null);
        }

        #endregion
    }
}

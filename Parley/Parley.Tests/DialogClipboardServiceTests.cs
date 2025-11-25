using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Tests
{
    /// <summary>
    /// Unit tests for DialogClipboardService (Issue #111)
    /// Tests copy/cut/paste operations after refactoring from MainViewModel
    /// </summary>
    public class DialogClipboardServiceTests
    {
        private readonly DialogClipboardService _clipboardService;

        public DialogClipboardServiceTests()
        {
            _clipboardService = new DialogClipboardService();
        }

        #region Basic Copy Tests

        [Fact]
        public void CopyNode_SimpleNode_CreatesDeepClone()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var node = CreateTestNode(DialogNodeType.Entry, "Test entry");
            dialog.Entries.Add(node);

            // Act
            _clipboardService.CopyNode(node, dialog);

            // Assert
            Assert.True(_clipboardService.HasClipboardContent);
            Assert.False(_clipboardService.WasCutOperation);
            Assert.NotNull(_clipboardService.ClipboardNode);

            // Verify deep clone (different instance)
            Assert.NotSame(node, _clipboardService.ClipboardNode);

            // Verify properties copied
            Assert.Equal(node.Type, _clipboardService.ClipboardNode.Type);
            Assert.Equal(node.Speaker, _clipboardService.ClipboardNode.Speaker);
        }

        [Fact]
        public void CopyNode_WithAllProperties_PreservesAllFields()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var node = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Speaker = "TestNPC",
                Comment = "Test comment",
                Sound = "test.wav",
                ScriptAction = "test_script",
                Animation = DialogAnimation.Bow,
                AnimationLoop = true,
                Delay = 5000,
                Quest = "TestQuest",
                QuestEntry = 42,
                ActionParams = new Dictionary<string, string>
                {
                    ["param1"] = "value1",
                    ["param2"] = "value2"
                }
            };
            node.Text.Add(0, "Test text");
            node.Text.Add(4, "German text");
            dialog.Replies.Add(node);

            // Act
            _clipboardService.CopyNode(node, dialog);
            var clonedNode = _clipboardService.ClipboardNode;

            // Assert - All properties preserved
            Assert.NotNull(clonedNode);
            Assert.Equal(DialogNodeType.Reply, clonedNode.Type);
            Assert.Equal("TestNPC", clonedNode.Speaker);
            Assert.Equal("Test comment", clonedNode.Comment);
            Assert.Equal("test.wav", clonedNode.Sound);
            Assert.Equal("test_script", clonedNode.ScriptAction);
            Assert.Equal(DialogAnimation.Bow, clonedNode.Animation);
            Assert.True(clonedNode.AnimationLoop);
            Assert.Equal(5000u, clonedNode.Delay);
            Assert.Equal("TestQuest", clonedNode.Quest);
            Assert.Equal(42u, clonedNode.QuestEntry);

            // Assert - LocString cloned correctly
            Assert.Equal("Test text", clonedNode.Text.GetDefault());
            Assert.Equal("German text", clonedNode.Text.Get(4));
            Assert.Equal(2, clonedNode.Text.Strings.Count);

            // Assert - ActionParams cloned
            Assert.Equal(2, clonedNode.ActionParams.Count);
            Assert.Equal("value1", clonedNode.ActionParams["param1"]);
            Assert.Equal("value2", clonedNode.ActionParams["param2"]);

            // Assert - Dictionaries are different instances
            Assert.NotSame(node.Text.Strings, clonedNode.Text.Strings);
            Assert.NotSame(node.ActionParams, clonedNode.ActionParams);
        }

        [Fact]
        public void CopyNode_WithChildren_ClonesEntireSubtree()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var parent = CreateTestNode(DialogNodeType.Entry, "Parent");
            var child1 = CreateTestNode(DialogNodeType.Reply, "Child 1");
            var child2 = CreateTestNode(DialogNodeType.Reply, "Child 2");

            dialog.Entries.Add(parent);
            dialog.Replies.Add(child1);
            dialog.Replies.Add(child2);

            parent.Pointers.Add(CreatePointer(child1, DialogNodeType.Reply, 0));
            parent.Pointers.Add(CreatePointer(child2, DialogNodeType.Reply, 1));

            // Act
            _clipboardService.CopyNode(parent, dialog);
            var clonedParent = _clipboardService.ClipboardNode;

            // Assert
            Assert.NotNull(clonedParent);
            Assert.Equal(2, clonedParent.Pointers.Count);

            // Verify children are clones (different instances)
            Assert.NotSame(child1, clonedParent.Pointers[0].Node);
            Assert.NotSame(child2, clonedParent.Pointers[1].Node);

            // Verify child properties preserved
            Assert.Equal("Child 1", clonedParent.Pointers[0].Node!.Text.GetDefault());
            Assert.Equal("Child 2", clonedParent.Pointers[1].Node!.Text.GetDefault());
        }

        [Fact]
        public void CopyNode_DeepTree_HandlesDepth11Plus()
        {
            // Arrange - Create deep tree (depth 11 like __hicks.dlg)
            var dialog = CreateTestDialog();
            var root = CreateTestNode(DialogNodeType.Entry, "Level 0");
            dialog.Entries.Add(root);

            var currentNode = root;
            for (int i = 1; i <= 11; i++)
            {
                var nodeType = i % 2 == 0 ? DialogNodeType.Entry : DialogNodeType.Reply;
                var child = CreateTestNode(nodeType, $"Level {i}");

                if (nodeType == DialogNodeType.Entry)
                    dialog.Entries.Add(child);
                else
                    dialog.Replies.Add(child);

                currentNode.Pointers.Add(CreatePointer(child, nodeType, 0));
                currentNode = child;
            }

            // Act - This previously crashed with JSON serialization
            _clipboardService.CopyNode(root, dialog);

            // Assert - No crash, clone created
            Assert.NotNull(_clipboardService.ClipboardNode);

            // Verify depth preserved
            var clonedRoot = _clipboardService.ClipboardNode;
            var depth = 0;
            var current = clonedRoot;
            while (current != null && current.Pointers.Count > 0)
            {
                depth++;
                current = current.Pointers[0].Node;
            }
            Assert.Equal(11, depth);
        }

        [Fact]
        public void CopyNode_CircularReference_HandledWithCloneMap()
        {
            // Arrange - Create circular reference: A -> B -> A
            var dialog = CreateTestDialog();
            var nodeA = CreateTestNode(DialogNodeType.Entry, "Node A");
            var nodeB = CreateTestNode(DialogNodeType.Reply, "Node B");

            dialog.Entries.Add(nodeA);
            dialog.Replies.Add(nodeB);

            nodeA.Pointers.Add(CreatePointer(nodeB, DialogNodeType.Reply, 0));
            nodeB.Pointers.Add(CreatePointer(nodeA, DialogNodeType.Entry, 0));

            // Act
            _clipboardService.CopyNode(nodeA, dialog);

            // Assert - Clone created without infinite loop
            Assert.NotNull(_clipboardService.ClipboardNode);

            // Verify structure preserved
            var clonedA = _clipboardService.ClipboardNode;
            Assert.Single(clonedA.Pointers);

            var clonedB = clonedA.Pointers[0].Node;
            Assert.NotNull(clonedB);
            Assert.Single(clonedB!.Pointers);

            // Verify circular reference handled (cloneMap prevents duplicate cloning)
            // The circular ref might not point back to the exact same clone instance
            // but it should exist and have the same text
            var circularRef = clonedB.Pointers[0].Node;
            Assert.NotNull(circularRef);
            Assert.Equal("Node A", circularRef!.Text.GetDefault());
        }

        [Fact]
        public void CopyNode_MaxDepth100_StopsRecursionWithWarning()
        {
            // Arrange - Create artificially deep tree (>100 levels)
            var dialog = CreateTestDialog();
            var root = CreateTestNode(DialogNodeType.Entry, "Root");
            dialog.Entries.Add(root);

            var currentNode = root;
            for (int i = 1; i <= 105; i++)
            {
                var nodeType = i % 2 == 0 ? DialogNodeType.Entry : DialogNodeType.Reply;
                var child = CreateTestNode(nodeType, $"Level {i}");

                if (nodeType == DialogNodeType.Entry)
                    dialog.Entries.Add(child);
                else
                    dialog.Replies.Add(child);

                currentNode.Pointers.Add(CreatePointer(child, nodeType, 0));
                currentNode = child;
            }

            // Act
            _clipboardService.CopyNode(root, dialog);

            // Assert - Clone created but truncated at MAX_DEPTH
            Assert.NotNull(_clipboardService.ClipboardNode);

            // Count depth of cloned tree
            var depth = 0;
            var current = _clipboardService.ClipboardNode;
            while (current != null && current.Pointers.Count > 0 && depth < 110)
            {
                depth++;
                current = current.Pointers[0].Node;
            }

            // Should stop at MAX_DEPTH (100)
            Assert.True(depth <= 100, $"Clone depth {depth} exceeds MAX_DEPTH 100");
        }

        #endregion

        #region Cut Tests

        [Fact]
        public void CutNode_CreatesClone_SameAsCopy()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var node = CreateTestNode(DialogNodeType.Entry, "Test entry");
            dialog.Entries.Add(node);

            // Act
            _clipboardService.CutNode(node, dialog);

            // Assert
            Assert.True(_clipboardService.HasClipboardContent);
            Assert.True(_clipboardService.WasCutOperation);

            // Cut now creates clone (consistent with Copy)
            Assert.NotSame(node, _clipboardService.ClipboardNode);
            Assert.Same(node, _clipboardService.OriginalNode);
        }

        [Fact]
        public void CutNode_ModifyOriginal_DoesNotAffectClipboard()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var node = CreateTestNode(DialogNodeType.Entry, "Original text");
            dialog.Entries.Add(node);

            // Act
            _clipboardService.CutNode(node, dialog);

            // Modify original after cut
            node.Text.Add(0, "Modified text");

            // Assert - Clipboard does NOT reflect changes (now cloned)
            Assert.Equal("Original text", _clipboardService.ClipboardNode!.Text.GetDefault());
        }

        #endregion

        #region Copy vs Cut Behavior

        [Fact]
        public void CopyVsCut_ModifyOriginal_BothIndependent()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var node1 = CreateTestNode(DialogNodeType.Entry, "Original text");
            var node2 = CreateTestNode(DialogNodeType.Reply, "Original text");
            dialog.Entries.Add(node1);
            dialog.Replies.Add(node2);

            // Act - Copy node1
            _clipboardService.CopyNode(node1, dialog);
            var copiedNode = _clipboardService.ClipboardNode;

            // Clear and cut node2
            _clipboardService.ClearClipboard();
            _clipboardService.CutNode(node2, dialog);
            var cutNode = _clipboardService.ClipboardNode;

            // Modify originals
            node1.Text.Add(0, "Modified text");
            node2.Text.Add(0, "Modified text");

            // Assert - Both are independent (both clone now)
            Assert.Equal("Original text", copiedNode!.Text.GetDefault());
            Assert.Equal("Original text", cutNode!.Text.GetDefault());
        }

        #endregion

        #region Paste Tests

        [Fact]
        public void PasteAsDuplicate_AfterCopy_CreatesNewNode()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var original = CreateTestNode(DialogNodeType.Entry, "Original");
            dialog.Entries.Add(original);

            _clipboardService.CopyNode(original, dialog);

            // Act
            var pasted = _clipboardService.PasteAsDuplicate(dialog, null, null);

            // Assert
            Assert.NotNull(pasted);
            Assert.Equal(2, dialog.Entries.Count); // Original + pasted
            Assert.NotSame(original, pasted); // Different instances
        }

        [Fact]
        public void PasteAsDuplicate_AfterCut_CreatesNewNode()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var original = CreateTestNode(DialogNodeType.Entry, "Original");
            dialog.Entries.Add(original);

            _clipboardService.CutNode(original, dialog);

            // Act
            var pasted = _clipboardService.PasteAsDuplicate(dialog, null, null);

            // Assert
            Assert.NotNull(pasted);
            Assert.NotSame(original, pasted); // Cut now creates clone (consistent with Copy)
            Assert.Equal(2, dialog.Entries.Count); // Original + pasted clone
        }

        [Fact]
        public void PasteAsDuplicate_AfterCut_ClearsClipboard()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var node = CreateTestNode(DialogNodeType.Entry, "Test");
            dialog.Entries.Add(node);

            _clipboardService.CutNode(node, dialog);

            // Act
            _clipboardService.PasteAsDuplicate(dialog, null, null);

            // Assert - Clipboard cleared after cut+paste
            Assert.False(_clipboardService.HasClipboardContent);
            Assert.Null(_clipboardService.ClipboardNode);
        }

        [Fact]
        public void PasteAsDuplicate_AfterCopy_PreservesClipboard()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var node = CreateTestNode(DialogNodeType.Entry, "Test");
            dialog.Entries.Add(node);

            _clipboardService.CopyNode(node, dialog);

            // Act
            _clipboardService.PasteAsDuplicate(dialog, null, null);

            // Assert - Clipboard NOT cleared after copy+paste
            Assert.True(_clipboardService.HasClipboardContent);
            Assert.NotNull(_clipboardService.ClipboardNode);
        }

        #endregion

        #region Paste As Link Tests

        [Fact]
        public void PasteAsLink_AfterCut_ReturnsNull()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var original = CreateTestNode(DialogNodeType.Reply, "Shared reply");
            var parent = CreateTestNode(DialogNodeType.Entry, "Parent");

            // Add nodes to dialog
            dialog.Replies.Add(original);
            dialog.Entries.Add(parent);

            // Cut now blocks PasteAsLink (source will be deleted)
            _clipboardService.CutNode(original, dialog);

            // Act - Try paste as link
            var linkPtr = _clipboardService.PasteAsLink(dialog, parent);

            // Assert - Cannot link after Cut (source will be deleted)
            Assert.Null(linkPtr);
            Assert.Empty(parent.Pointers);
        }

        [Fact]
        public void PasteAsLink_AfterCopy_CreatesLinkPointer()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var original = CreateTestNode(DialogNodeType.Reply, "Shared reply");
            var parent = CreateTestNode(DialogNodeType.Entry, "Parent");

            // Add nodes to dialog
            dialog.Replies.Add(original);
            dialog.Entries.Add(parent);

            // Copy (not Cut) preserves the original node for linking
            _clipboardService.CopyNode(original, dialog);

            // Act - Paste as link
            var linkPtr = _clipboardService.PasteAsLink(dialog, parent);

            // Assert
            Assert.NotNull(linkPtr);
            Assert.True(linkPtr!.IsLink);
            Assert.Same(original, linkPtr.Node); // Links to original node
            Assert.Single(parent.Pointers);
            Assert.Same(linkPtr, parent.Pointers[0]);
        }

        [Fact]
        public void PasteAsLink_DifferentDialog_ReturnsNull()
        {
            // Arrange
            var dialog1 = CreateTestDialog();
            var dialog2 = CreateTestDialog();
            var node = CreateTestNode(DialogNodeType.Reply, "Node");
            var parent = CreateTestNode(DialogNodeType.Entry, "Parent");

            dialog1.Replies.Add(node);
            dialog2.Entries.Add(parent);

            _clipboardService.CopyNode(node, dialog1);

            // Act
            var linkPtr = _clipboardService.PasteAsLink(dialog2, parent);

            // Assert - Cannot link across dialogs
            Assert.Null(linkPtr);
        }

        #endregion

        #region Clipboard State Tests

        [Fact]
        public void ClearClipboard_ResetsAllState()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var node = CreateTestNode(DialogNodeType.Entry, "Test");
            dialog.Entries.Add(node);

            _clipboardService.CopyNode(node, dialog);

            // Act
            _clipboardService.ClearClipboard();

            // Assert
            Assert.False(_clipboardService.HasClipboardContent);
            Assert.False(_clipboardService.WasCutOperation);
            Assert.Null(_clipboardService.ClipboardNode);
        }

        [Fact]
        public void HasClipboardContent_InitiallyFalse()
        {
            // Assert
            Assert.False(_clipboardService.HasClipboardContent);
        }

        #endregion

        #region Helper Methods

        private Dialog CreateTestDialog()
        {
            // Dialog properties are read-only, initialized automatically
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

        private DialogPtr CreatePointer(DialogNode node, DialogNodeType type, uint index)
        {
            return new DialogPtr
            {
                Node = node,
                Type = type,
                Index = index,
                IsLink = false
            };
        }

        #endregion
    }
}

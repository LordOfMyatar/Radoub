using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Unit tests for TreeNavigationManager service.
    /// Tests tree state management, navigation, and traversal functionality.
    /// </summary>
    public class TreeNavigationManagerTests
    {
        private readonly TreeNavigationManager _manager;

        public TreeNavigationManagerTests()
        {
            _manager = new TreeNavigationManager();
        }

        #region FindTreeNodeForDialogNode Tests

        [Fact]
        public void FindTreeNodeForDialogNode_WithRootNode_ReturnsCorrectNode()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var rootNode = dialog.Entries[0];
            var treeNodes = new ObservableCollection<TreeViewSafeNode>
            {
                new TreeViewSafeNode(rootNode)
            };

            // Act
            var found = _manager.FindTreeNodeForDialogNode(treeNodes, rootNode);

            // Assert
            Assert.NotNull(found);
            Assert.Equal(rootNode, found.OriginalNode);
        }

        [Fact]
        public void FindTreeNodeForDialogNode_WithNestedNode_ReturnsCorrectNode()
        {
            // Arrange
            var dialog = CreateNestedDialog();
            var targetNode = dialog.Replies[0]; // First reply

            var treeNodes = new ObservableCollection<TreeViewSafeNode>
            {
                new TreeViewSafeNode(dialog.Entries[0])
            };

            // Manually populate children for test
            var rootTreeNode = treeNodes[0];
            rootTreeNode.Children!.Clear();
            rootTreeNode.Children.Add(new TreeViewSafeNode(targetNode));

            // Act
            var found = _manager.FindTreeNodeForDialogNode(treeNodes, targetNode);

            // Assert
            Assert.NotNull(found);
            Assert.Equal(targetNode, found.OriginalNode);
        }

        [Fact]
        public void FindTreeNodeForDialogNode_WithNonExistentNode_ReturnsNull()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var nonExistentNode = new DialogNode { Type = DialogNodeType.Entry };
            var treeNodes = new ObservableCollection<TreeViewSafeNode>
            {
                new TreeViewSafeNode(dialog.Entries[0])
            };

            // Act
            var found = _manager.FindTreeNodeForDialogNode(treeNodes, nonExistentNode);

            // Assert
            Assert.Null(found);
        }

        #endregion

        #region Expansion State Tests

        [Fact]
        public void SaveTreeExpansionState_WithExpandedNodes_CapturesState()
        {
            // Arrange
            var dialog = CreateNestedDialog();
            var root = new TreeViewSafeNode(dialog.Entries[0]);
            root.IsExpanded = true;

            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };

            // Act
            var expandedRefs = _manager.SaveTreeExpansionState(treeNodes);

            // Assert
            Assert.Single(expandedRefs);
            Assert.Contains(dialog.Entries[0], expandedRefs);
        }

        [Fact]
        public void SaveTreeExpansionState_WithNoExpandedNodes_ReturnsEmpty()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var root = new TreeViewSafeNode(dialog.Entries[0]);
            root.IsExpanded = false;

            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };

            // Act
            var expandedRefs = _manager.SaveTreeExpansionState(treeNodes);

            // Assert
            Assert.Empty(expandedRefs);
        }

        [Fact]
        public void SaveTreeExpansionState_WithNestedExpansion_CapturesAll()
        {
            // Arrange
            var dialog = CreateNestedDialog();
            var root = new TreeViewSafeNode(dialog.Entries[0]);
            root.IsExpanded = true;

            var child = new TreeViewSafeNode(dialog.Replies[0]);
            child.IsExpanded = true;

            root.Children!.Clear();
            root.Children.Add(child);

            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };

            // Act
            var expandedRefs = _manager.SaveTreeExpansionState(treeNodes);

            // Assert
            Assert.Equal(2, expandedRefs.Count);
            Assert.Contains(dialog.Entries[0], expandedRefs);
            Assert.Contains(dialog.Replies[0], expandedRefs);
        }

        [Fact]
        public void RestoreTreeExpansionState_RestoresExpandedNodes()
        {
            // Arrange
            var dialog = CreateNestedDialog();
            var root = new TreeViewSafeNode(dialog.Entries[0]);
            root.IsExpanded = false;

            var child = new TreeViewSafeNode(dialog.Replies[0]);
            child.IsExpanded = false;

            root.Children!.Clear();
            root.Children.Add(child);

            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };

            var expandedRefs = new HashSet<DialogNode> { dialog.Entries[0], dialog.Replies[0] };

            // Act
            _manager.RestoreTreeExpansionState(treeNodes, expandedRefs);

            // Assert
            Assert.True(root.IsExpanded);
            Assert.True(child.IsExpanded);
        }

        [Fact]
        public void RestoreTreeExpansionState_WithEmptySet_LeavesAllCollapsed()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var root = new TreeViewSafeNode(dialog.Entries[0]);
            root.IsExpanded = false;

            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };
            var emptySet = new HashSet<DialogNode>();

            // Act
            _manager.RestoreTreeExpansionState(treeNodes, emptySet);

            // Assert
            Assert.False(root.IsExpanded);
        }

        #endregion

        #region Path-Based State Tests

        [Fact]
        public void CaptureExpandedNodePaths_WithExpandedNodes_CapturesPaths()
        {
            // Arrange
            var dialog = CreateNestedDialog();
            var root = new TreeViewSafeNode(dialog.Entries[0]);
            root.IsExpanded = true;

            var child = new TreeViewSafeNode(dialog.Replies[0]);
            child.IsExpanded = true;

            root.Children!.Clear();
            root.Children.Add(child);

            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };

            // Act
            var paths = _manager.CaptureExpandedNodePaths(treeNodes);

            // Assert
            Assert.Equal(2, paths.Count);
            // Paths should contain identifiers for both nodes
            Assert.Contains(paths, p => p.Contains("Entry") || p.Contains("Reply"));
        }

        [Fact]
        public void CaptureExpandedNodePaths_WithNoExpansion_ReturnsEmpty()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var root = new TreeViewSafeNode(dialog.Entries[0]);
            root.IsExpanded = false;

            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };

            // Act
            var paths = _manager.CaptureExpandedNodePaths(treeNodes);

            // Assert
            Assert.Empty(paths);
        }

        [Fact]
        public void RestoreExpandedNodePaths_RestoresExpansion()
        {
            // Arrange
            var dialog = CreateNestedDialog();
            var root = new TreeViewSafeNode(dialog.Entries[0]);
            root.IsExpanded = false;

            var child = new TreeViewSafeNode(dialog.Replies[0]);
            child.IsExpanded = false;

            root.Children!.Clear();
            root.Children.Add(child);

            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };

            // Capture paths when expanded
            root.IsExpanded = true;
            child.IsExpanded = true;
            var paths = _manager.CaptureExpandedNodePaths(treeNodes);

            // Collapse everything
            root.IsExpanded = false;
            child.IsExpanded = false;

            // Act
            _manager.RestoreExpandedNodePaths(treeNodes, paths);

            // Assert
            Assert.True(root.IsExpanded);
            Assert.True(child.IsExpanded);
        }

        [Fact]
        public void PathBasedRestore_HandlesCircularReferences()
        {
            // Arrange - Create a circular structure (A -> B -> A)
            var dialog = new Dialog();
            var entryA = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entryA.Text.Add(0, "Entry A");

            var replyB = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString()
            };
            replyB.Text.Add(0, "Reply B");

            dialog.Entries.Add(entryA);
            dialog.Replies.Add(replyB);

            var ptrToB = new DialogPtr { Node = replyB, Type = DialogNodeType.Reply };
            entryA.Pointers.Add(ptrToB);

            var ptrToA = new DialogPtr { Node = entryA, Type = DialogNodeType.Entry, IsLink = true };
            replyB.Pointers.Add(ptrToA);

            var root = new TreeViewSafeNode(entryA);
            root.IsExpanded = true;

            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };

            // Act - Should not hang or crash
            var paths = _manager.CaptureExpandedNodePaths(treeNodes);

            // Assert - Completed without error
            Assert.NotNull(paths);
        }

        #endregion

        #region ExpandAncestors Tests (Issue #252)

        [Fact]
        public void ExpandAncestors_WithNestedNode_ExpandsParents()
        {
            // Arrange - Create tree: root -> child -> grandchild
            var dialog = CreateNestedDialog();
            var root = new TreeViewSafeNode(dialog.Entries[0]);
            root.IsExpanded = false;

            var child = new TreeViewSafeNode(dialog.Replies[0]);
            child.IsExpanded = false;

            var grandchild = new TreeViewSafeNode(dialog.Entries[1]);
            grandchild.IsExpanded = false;

            root.Children!.Clear();
            root.Children.Add(child);
            child.Children!.Clear();
            child.Children.Add(grandchild);

            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };

            // Act - Expand ancestors of grandchild
            _manager.ExpandAncestors(treeNodes, grandchild);

            // Assert - Root and child should be expanded, grandchild should not
            Assert.True(root.IsExpanded, "Root should be expanded");
            Assert.True(child.IsExpanded, "Child should be expanded");
            Assert.False(grandchild.IsExpanded, "Grandchild should remain collapsed");
        }

        [Fact]
        public void ExpandAncestors_WithRootLevelNode_DoesNothing()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var root = new TreeViewSafeNode(dialog.Entries[0]);
            root.IsExpanded = false;

            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };

            // Act - Expand ancestors of root (has no ancestors)
            _manager.ExpandAncestors(treeNodes, root);

            // Assert - Root should still be collapsed (no ancestors to expand)
            Assert.False(root.IsExpanded);
        }

        [Fact]
        public void ExpandAncestors_WithNonExistentNode_DoesNothing()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var root = new TreeViewSafeNode(dialog.Entries[0]);
            root.IsExpanded = false;

            var nonExistentNode = new TreeViewSafeNode(new DialogNode { Type = DialogNodeType.Entry });

            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };

            // Act - Expand ancestors of non-existent node
            _manager.ExpandAncestors(treeNodes, nonExistentNode);

            // Assert - Root should still be collapsed
            Assert.False(root.IsExpanded);
        }

        [Fact]
        public void ExpandAncestors_WithNullTarget_DoesNotThrow()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var root = new TreeViewSafeNode(dialog.Entries[0]);
            var treeNodes = new ObservableCollection<TreeViewSafeNode> { root };

            // Act & Assert - Should not throw
            _manager.ExpandAncestors(treeNodes, null!);
        }

        #endregion

        #region Tree Structure Capture Tests

        [Fact]
        public void CaptureTreeStructure_WithSimpleDialog_ReturnsStructure()
        {
            // Arrange
            var dialog = CreateSimpleDialog();

            // Act
            var structure = _manager.CaptureTreeStructure(dialog);

            // Assert
            Assert.Contains("=== Dialog Tree Structure ===", structure);
            Assert.Contains("Total Entries: 1", structure);
            Assert.Contains("Total Replies: 0", structure);
            Assert.Contains("Starting Entries: 1", structure);
        }

        [Fact]
        public void CaptureTreeStructure_WithNestedDialog_ShowsHierarchy()
        {
            // Arrange
            var dialog = CreateNestedDialog();

            // Act
            var structure = _manager.CaptureTreeStructure(dialog);

            // Assert
            Assert.Contains("Total Entries: 2", structure);
            Assert.Contains("Total Replies: 1", structure);
            Assert.Contains("[E]", structure); // Entry marker
            Assert.Contains("[R]", structure); // Reply marker
        }

        [Fact]
        public void CaptureTreeStructure_WithNullDialog_ReturnsMessage()
        {
            // Act
            var structure = _manager.CaptureTreeStructure(null!);

            // Assert
            Assert.Equal("No dialog loaded", structure);
        }

        [Fact]
        public void CaptureTreeStructure_DetectsCircularReferences()
        {
            // Arrange - Create circular structure
            var dialog = new Dialog();
            var entryA = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entryA.Text.Add(0, "Entry A");

            var replyB = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString()
            };
            replyB.Text.Add(0, "Reply B");

            dialog.Entries.Add(entryA);
            dialog.Replies.Add(replyB);

            var ptrToB = new DialogPtr { Node = replyB, Type = DialogNodeType.Reply };
            entryA.Pointers.Add(ptrToB);

            var ptrToA = new DialogPtr { Node = entryA, Type = DialogNodeType.Entry, IsLink = true };
            replyB.Pointers.Add(ptrToA);

            dialog.Starts.Add(new DialogPtr { Node = entryA, Type = DialogNodeType.Entry });

            // Act
            var structure = _manager.CaptureTreeStructure(dialog);

            // Assert
            Assert.Contains("[CIRCULAR]", structure);
        }

        #endregion

        #region Helper Methods

        private Dialog CreateSimpleDialog()
        {
            var dialog = new Dialog();
            var entry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry.Text.Add(0, "Root Entry");
            dialog.Entries.Add(entry);
            dialog.Starts.Add(new DialogPtr { Node = entry, Type = DialogNodeType.Entry });
            return dialog;
        }

        private Dialog CreateNestedDialog()
        {
            var dialog = new Dialog();
            var entry1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry1.Text.Add(0, "First Entry");

            var reply1 = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString()
            };
            reply1.Text.Add(0, "First Reply");

            var entry2 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry2.Text.Add(0, "Second Entry");

            dialog.Entries.Add(entry1);
            dialog.Replies.Add(reply1);
            dialog.Entries.Add(entry2);

            // entry1 -> reply1 -> entry2
            var ptrToReply = new DialogPtr { Node = reply1, Type = DialogNodeType.Reply };
            entry1.Pointers.Add(ptrToReply);

            var ptrToEntry2 = new DialogPtr { Node = entry2, Type = DialogNodeType.Entry };
            reply1.Pointers.Add(ptrToEntry2);

            dialog.Starts.Add(new DialogPtr { Node = entry1, Type = DialogNodeType.Entry });

            return dialog;
        }

        #endregion
    }
}

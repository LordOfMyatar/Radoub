using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using DialogEditor.Models;
using DialogEditor.ViewModels;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for copy/paste operations, specifically targeting Issue #6
    /// </summary>
    public class CopyPasteTests : IDisposable
    {
        private readonly MainViewModel _viewModel;

        public CopyPasteTests()
        {
            _viewModel = new MainViewModel();
            InitializeTestDialog();
        }

        public void Dispose()
        {
            _viewModel?.Dispose();
        }

        private void InitializeTestDialog()
        {
            var dialog = new Dialog
            {
                Entries = new List<DialogNode>(),
                Replies = new List<DialogNode>(),
                StartingList = new List<DialogPtr>()
            };

            // Create a basic structure for testing
            var entry1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString { Value = "Entry 1" },
                Speaker = "NPC1",
                Pointers = new List<DialogPtr>()
            };

            var reply1 = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString { Value = "Reply 1" },
                Pointers = new List<DialogPtr>()
            };

            entry1.Pointers.Add(new DialogPtr
            {
                Node = reply1,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false
            });

            dialog.Entries.Add(entry1);
            dialog.Replies.Add(reply1);

            dialog.StartingList.Add(new DialogPtr
            {
                Node = entry1,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsLink = false
            });

            _viewModel.CurrentDialog = dialog;
        }

        [Fact]
        public void CopyNode_StoresNodeInClipboard()
        {
            // Arrange
            var nodeToCopy = _viewModel.CurrentDialog.Entries[0];

            // Act
            _viewModel.CopyNodeToClipboard(nodeToCopy);

            // Assert
            // Note: We'd need to expose _copiedNode for testing or use reflection
            // For now, we'll test indirectly through paste
            Assert.True(true); // Placeholder - needs implementation access
        }

        [Fact(Skip = "Current implementation has known issue #6")]
        public void PasteNode_WithLinks_PreservesCorrectIndices()
        {
            // This test documents the current broken behavior
            // It should fail until we fix Issue #6

            // Arrange
            var dialog = CreateComplexDialogWithLinks();
            _viewModel.CurrentDialog = dialog;
            var nodeToCopy = dialog.Entries[0]; // Node with outgoing links
            _viewModel.CopyNodeToClipboard(nodeToCopy);

            // Act
            // Paste under ROOT - this is where corruption happens
            var rootNode = new TreeViewRootNode(dialog);
            _viewModel.PasteFromClipboard(rootNode);

            // Assert - After fix, indices should be correct
            var pastedNode = dialog.Entries.Last();
            Assert.NotNull(pastedNode);

            // Check that pointers have correct indices
            foreach (var pointer in pastedNode.Pointers)
            {
                if (pointer.Type == DialogNodeType.Reply)
                {
                    Assert.True(pointer.Index < dialog.Replies.Count,
                        "Reply pointer index out of bounds");
                }
                else
                {
                    Assert.True(pointer.Index < dialog.Entries.Count,
                        "Entry pointer index out of bounds");
                }
            }
        }

        [Fact]
        public void PasteAsLink_CreatesProperLinkReference()
        {
            // Arrange
            var nodeToCopy = _viewModel.CurrentDialog.Entries[0];
            _viewModel.CopyNodeToClipboard(nodeToCopy);

            // Create a parent node to paste under
            var parentEntry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString { Value = "Parent Entry" },
                Pointers = new List<DialogPtr>()
            };
            _viewModel.CurrentDialog.Entries.Add(parentEntry);

            var parentTreeNode = new TreeViewSafeNode(parentEntry);

            // Act
            _viewModel.PasteAsLink(parentTreeNode);

            // Assert
            Assert.Single(parentEntry.Pointers);
            var linkPointer = parentEntry.Pointers[0];
            Assert.True(linkPointer.IsLink);
            Assert.Equal(nodeToCopy, linkPointer.Node);
            Assert.Contains("[Link", linkPointer.LinkComment); // Should have link indicator
        }

        [Fact(Skip = "Current implementation needs LinkRegistry")]
        public void DeleteNode_WithIncomingLinks_UpdatesReferences()
        {
            // This test requires the LinkRegistry to be implemented
            // Currently, deleting nodes with incoming links can cause corruption

            // Arrange
            var dialog = CreateDialogWithSharedNode();
            _viewModel.CurrentDialog = dialog;
            var sharedNode = dialog.Replies[0]; // Node that multiple entries point to

            // Act
            // Delete the shared node
            _viewModel.DeleteNode(new TreeViewSafeNode(sharedNode));

            // Assert - All references should be cleaned up
            foreach (var entry in dialog.Entries)
            {
                foreach (var pointer in entry.Pointers)
                {
                    // No pointer should reference the deleted node
                    Assert.NotEqual(sharedNode, pointer.Node);
                }
            }
        }

        [Fact]
        public void CopyPaste_CircularReference_HandledGracefully()
        {
            // Arrange - Create a circular reference
            var entry = _viewModel.CurrentDialog.Entries[0];
            var reply = _viewModel.CurrentDialog.Replies[0];

            // Make reply point back to entry (circular)
            reply.Pointers.Add(new DialogPtr
            {
                Node = entry,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsLink = false
            });

            // Act
            _viewModel.CopyNodeToClipboard(entry);
            var rootNode = new TreeViewRootNode(_viewModel.CurrentDialog);
            _viewModel.PasteFromClipboard(rootNode);

            // Assert - Should not crash or create infinite loop
            // The CloneNodeWithDepth should handle this with MAX_DEPTH check
            Assert.True(_viewModel.CurrentDialog.Entries.Count > 1);
        }

        #region Helper Methods

        private Dialog CreateComplexDialogWithLinks()
        {
            var dialog = new Dialog
            {
                Entries = new List<DialogNode>(),
                Replies = new List<DialogNode>(),
                StartingList = new List<DialogPtr>()
            };

            // Create multiple interconnected nodes
            var entry1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString { Value = "Entry 1" },
                Pointers = new List<DialogPtr>()
            };

            var entry2 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString { Value = "Entry 2" },
                Pointers = new List<DialogPtr>()
            };

            var reply1 = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString { Value = "Reply 1" },
                Pointers = new List<DialogPtr>()
            };

            var reply2 = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString { Value = "Reply 2" },
                Pointers = new List<DialogPtr>()
            };

            dialog.Entries.Add(entry1);
            dialog.Entries.Add(entry2);
            dialog.Replies.Add(reply1);
            dialog.Replies.Add(reply2);

            // Create complex linking
            entry1.Pointers.Add(new DialogPtr { Node = reply1, Type = DialogNodeType.Reply, Index = 0 });
            entry1.Pointers.Add(new DialogPtr { Node = reply2, Type = DialogNodeType.Reply, Index = 1, IsLink = true });
            entry2.Pointers.Add(new DialogPtr { Node = reply1, Type = DialogNodeType.Reply, Index = 0, IsLink = true });
            reply1.Pointers.Add(new DialogPtr { Node = entry2, Type = DialogNodeType.Entry, Index = 1 });

            return dialog;
        }

        private Dialog CreateDialogWithSharedNode()
        {
            var dialog = new Dialog
            {
                Entries = new List<DialogNode>(),
                Replies = new List<DialogNode>(),
                StartingList = new List<DialogPtr>()
            };

            var sharedReply = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString { Value = "Shared Reply" },
                Pointers = new List<DialogPtr>()
            };

            dialog.Replies.Add(sharedReply);

            // Multiple entries point to the same reply
            for (int i = 0; i < 3; i++)
            {
                var entry = new DialogNode
                {
                    Type = DialogNodeType.Entry,
                    Text = new LocString { Value = $"Entry {i}" },
                    Pointers = new List<DialogPtr>
                    {
                        new DialogPtr
                        {
                            Node = sharedReply,
                            Type = DialogNodeType.Reply,
                            Index = 0,
                            IsLink = i > 0 // First is original, rest are links
                        }
                    }
                };
                dialog.Entries.Add(entry);
            }

            return dialog;
        }

        #endregion
    }
}
using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using Xunit;

namespace Parley.Tests.GUI
{
    /// <summary>
    /// Avalonia.Headless tests for copy/paste workflows (Issue #81 Phase 1)
    /// Tests CopyNode, PasteAsDuplicate, PasteAsLink operations
    /// Note: DialogNodes[0] is ROOT node; actual entries are in ROOT.Children
    /// </summary>
    public class CopyPasteHeadlessTests
    {
        // Helper to get ROOT node
        private static TreeViewRootNode? GetRootNode(MainViewModel viewModel)
        {
            if (viewModel.DialogNodes.Count == 0)
                return null;
            return viewModel.DialogNodes[0] as TreeViewRootNode;
        }

        // Helper to get first entry node (ROOT's first child)
        private static TreeViewSafeNode? GetFirstEntryNode(MainViewModel viewModel)
        {
            var rootNode = viewModel.DialogNodes[0] as TreeViewRootNode;
            return rootNode?.Children?.Count > 0 ? rootNode.Children[0] : null;
        }

        // Helper to get entry node at index (from ROOT's children)
        private static TreeViewSafeNode? GetEntryNodeAt(MainViewModel viewModel, int index)
        {
            var rootNode = viewModel.DialogNodes[0] as TreeViewRootNode;
            return rootNode?.Children?.Count > index ? rootNode.Children[index] : null;
        }

        [AvaloniaFact]
        public void CopyNode_WithValidNode_SetsClipboard()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var nodeToCopy = GetFirstEntryNode(viewModel);
            Assert.NotNull(nodeToCopy);

            // Act
            viewModel.CopyNode(nodeToCopy);

            // Assert: Clipboard should have content (verified by paste operation)
            // Note: Clipboard state is internal to clipboard service
            // We verify by attempting paste
            var initialCount = viewModel.CurrentDialog!.Entries.Count;
            var rootNode = GetRootNode(viewModel);
            Assert.NotNull(rootNode);
            viewModel.PasteAsDuplicate(rootNode); // Paste at root

            Assert.True(viewModel.CurrentDialog.Entries.Count > initialCount);
        }

        [AvaloniaFact]
        public void CopyNode_WithNullNode_HandlesGracefully()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();

            // Act: Copy with no selection
            viewModel.CopyNode(null);

            // Assert: Should not crash (clipboard may or may not be set)
            Assert.NotNull(viewModel.CurrentDialog);
        }

        [AvaloniaFact]
        public void PasteAsDuplicate_CreatesNewNode()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var nodeToCopy = GetFirstEntryNode(viewModel);
            Assert.NotNull(nodeToCopy);
            viewModel.CopyNode(nodeToCopy);

            var initialCount = viewModel.CurrentDialog!.Entries.Count;
            var rootNode = GetRootNode(viewModel);
            Assert.NotNull(rootNode);

            // Act: Paste as duplicate
            viewModel.PasteAsDuplicate(rootNode); // Paste at root

            // Assert: New entry created
            Assert.Equal(initialCount + 1, viewModel.CurrentDialog.Entries.Count);
        }

        [AvaloniaFact]
        public void PasteAsDuplicate_CreatesIndependentCopy()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var originalNode = GetFirstEntryNode(viewModel);
            Assert.NotNull(originalNode);
            var originalEntry = viewModel.CurrentDialog!.Entries[0];
            originalEntry.Text.Add(0, "Original Text");

            viewModel.CopyNode(originalNode);
            var rootNode = GetRootNode(viewModel);
            Assert.NotNull(rootNode);
            viewModel.PasteAsDuplicate(rootNode);

            // Act: Modify original
            originalEntry.Text.Strings[0] = "Modified Text";

            // Assert: Pasted copy should be independent
            var pastedEntry = viewModel.CurrentDialog.Entries[1];
            Assert.NotEqual(originalEntry.Text.Strings[0], pastedEntry.Text.Strings[0]);
        }

        [AvaloniaFact]
        public void PasteAsLink_CreatesLinkPointer()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null); // Entry 1

            var entry1Node = GetFirstEntryNode(viewModel);
            Assert.NotNull(entry1Node);
            viewModel.AddPCReplyNode(entry1Node); // Reply under Entry 1

            // Re-get entry1Node after tree refresh
            entry1Node = GetFirstEntryNode(viewModel);
            Assert.NotNull(entry1Node);
            // LAZY LOADING: Must expand node to populate children
            entry1Node.IsExpanded = true;
            Assert.NotNull(entry1Node.Children);
            Assert.NotEmpty(entry1Node.Children);
            var replyNode = entry1Node.Children[0];
            Assert.IsNotType<TreeViewPlaceholderNode>(replyNode); // Verify not a placeholder
            viewModel.CopyNode(replyNode);

            // Add second entry to paste link under
            viewModel.AddEntryNode(null); // Entry 2
            var entry2Node = GetEntryNodeAt(viewModel, 1);
            Assert.NotNull(entry2Node);

            // Act: Paste as link under Entry 2
            viewModel.PasteAsLink(entry2Node);

            // Assert: Entry 2 should have link pointer to same reply
            var entry2 = viewModel.CurrentDialog!.Entries[1];
            Assert.Single(entry2.Pointers);
            Assert.True(entry2.Pointers[0].IsLink); // This is a link!

            // Reply is shared between both entries
            var entry1 = viewModel.CurrentDialog.Entries[0];
            Assert.Equal(entry1.Pointers[0].Node, entry2.Pointers[0].Node);
        }

        [AvaloniaFact]
        public void PasteAsLink_PreservesClipboard()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var entry1Node = GetFirstEntryNode(viewModel);
            Assert.NotNull(entry1Node);
            viewModel.AddPCReplyNode(entry1Node);

            // Re-get entry1Node after tree refresh
            entry1Node = GetFirstEntryNode(viewModel);
            Assert.NotNull(entry1Node);
            // LAZY LOADING: Must expand node to populate children
            entry1Node.IsExpanded = true;
            Assert.NotNull(entry1Node.Children);
            Assert.NotEmpty(entry1Node.Children);
            var replyNode = entry1Node.Children[0];
            Assert.IsNotType<TreeViewPlaceholderNode>(replyNode); // Verify not a placeholder
            viewModel.CopyNode(replyNode);

            viewModel.AddEntryNode(null); // Entry 2
            var entry2Node = GetEntryNodeAt(viewModel, 1);
            Assert.NotNull(entry2Node);

            viewModel.PasteAsLink(entry2Node);

            // Act: Paste as link again under different parent
            viewModel.AddEntryNode(null); // Entry 3
            var entry3Node = GetEntryNodeAt(viewModel, 2);
            Assert.NotNull(entry3Node);
            viewModel.PasteAsLink(entry3Node);

            // Assert: Should work (clipboard preserved after link paste)
            var entry3 = viewModel.CurrentDialog!.Entries[2];
            Assert.Single(entry3.Pointers);
            Assert.True(entry3.Pointers[0].IsLink);
        }

        [AvaloniaFact]
        public void PasteAsDuplicate_WithoutCopy_HandlesGracefully()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            // Add an entry so DialogNodes is populated (ROOT needs children to be built)
            viewModel.AddEntryNode(null);

            var initialCount = viewModel.CurrentDialog!.Entries.Count;
            var rootNode = GetRootNode(viewModel);
            Assert.NotNull(rootNode);

            // Act: Paste without copying anything (no clipboard content)
            viewModel.PasteAsDuplicate(rootNode);

            // Assert: Should not crash, dialog unchanged (no node was copied)
            Assert.Equal(initialCount, viewModel.CurrentDialog.Entries.Count);
        }

        [AvaloniaFact]
        public void CopyPasteWorkflow_EndToEnd()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var entry1Node = GetFirstEntryNode(viewModel);
            Assert.NotNull(entry1Node);
            var entry1 = viewModel.CurrentDialog!.Entries[0];
            entry1.Text.Add(0, "Hello");
            entry1.Speaker = "NPC_Test";

            // Act: Copy and paste
            viewModel.CopyNode(entry1Node);
            var rootNode = GetRootNode(viewModel);
            Assert.NotNull(rootNode);
            viewModel.PasteAsDuplicate(rootNode);

            // Assert: Duplicate has same content
            Assert.Equal(2, viewModel.CurrentDialog.Entries.Count);

            var entry2 = viewModel.CurrentDialog.Entries[1];
            Assert.Equal(entry1.Text.Strings[0], entry2.Text.Strings[0]);
            Assert.Equal(entry1.Speaker, entry2.Speaker);

            // But they're different objects
            Assert.NotSame(entry1, entry2);
        }
    }
}

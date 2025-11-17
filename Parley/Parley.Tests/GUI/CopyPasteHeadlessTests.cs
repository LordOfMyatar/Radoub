using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.ViewModels;
using Xunit;

namespace Parley.Tests.GUI
{
    /// <summary>
    /// Avalonia.Headless tests for copy/paste workflows (Issue #81 Phase 1)
    /// Tests CopyNode, PasteAsDuplicate, PasteAsLink operations
    /// </summary>
    public class CopyPasteHeadlessTests
    {
        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void CopyNode_WithValidNode_SetsClipboard()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var nodeToCopy = viewModel.DialogNodes[0];

            // Act
            viewModel.CopyNode(nodeToCopy);

            // Assert: Clipboard should have content (verified by paste operation)
            // Note: Clipboard state is internal to clipboard service
            // We verify by attempting paste
            var initialCount = viewModel.CurrentDialog!.Entries.Count;
            viewModel.PasteAsDuplicate(null); // Paste at root

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

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void PasteAsDuplicate_CreatesNewNode()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var nodeToCopy = viewModel.DialogNodes[0];
            viewModel.CopyNode(nodeToCopy);

            var initialCount = viewModel.CurrentDialog!.Entries.Count;

            // Act: Paste as duplicate
            viewModel.PasteAsDuplicate(null); // Paste at root

            // Assert: New entry created
            Assert.Equal(initialCount + 1, viewModel.CurrentDialog.Entries.Count);
        }

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void PasteAsDuplicate_CreatesIndependentCopy()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var originalNode = viewModel.DialogNodes[0];
            var originalEntry = viewModel.CurrentDialog!.Entries[0];
            originalEntry.Text.Add(0, "Original Text");

            viewModel.CopyNode(originalNode);
            viewModel.PasteAsDuplicate(null);

            // Act: Modify original
            originalEntry.Text.Strings[0] = "Modified Text";

            // Assert: Pasted copy should be independent
            var pastedEntry = viewModel.CurrentDialog.Entries[1];
            Assert.NotEqual(originalEntry.Text.Strings[0], pastedEntry.Text.Strings[0]);
        }

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void PasteAsLink_CreatesLinkPointer()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null); // Entry 1

            var entry1Node = viewModel.DialogNodes[0];
            viewModel.AddPCReplyNode(entry1Node); // Reply under Entry 1

            var replyNode = entry1Node.Children[0];
            viewModel.CopyNode(replyNode);

            // Add second entry to paste link under
            viewModel.AddEntryNode(null); // Entry 2
            var entry2Node = viewModel.DialogNodes[1];

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

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void PasteAsLink_PreservesClipboard()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var entry1Node = viewModel.DialogNodes[0];
            viewModel.AddPCReplyNode(entry1Node);

            var replyNode = entry1Node.Children[0];
            viewModel.CopyNode(replyNode);

            viewModel.AddEntryNode(null); // Entry 2
            var entry2Node = viewModel.DialogNodes[1];

            viewModel.PasteAsLink(entry2Node);

            // Act: Paste as link again under different parent
            viewModel.AddEntryNode(null); // Entry 3
            var entry3Node = viewModel.DialogNodes[2];
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

            var initialCount = viewModel.CurrentDialog!.Entries.Count;

            // Act: Paste without copying anything
            viewModel.PasteAsDuplicate(null);

            // Assert: Should not crash, dialog unchanged
            Assert.Equal(initialCount, viewModel.CurrentDialog.Entries.Count);
        }

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void CopyPasteWorkflow_EndToEnd()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var entry1Node = viewModel.DialogNodes[0];
            var entry1 = viewModel.CurrentDialog!.Entries[0];
            entry1.Text.Add(0, "Hello");
            entry1.Speaker = "NPC_Test";

            // Act: Copy and paste
            viewModel.CopyNode(entry1Node);
            viewModel.PasteAsDuplicate(null);

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

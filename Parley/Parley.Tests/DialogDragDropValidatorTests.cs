using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests the shared drag-drop validator that governs "drop as child of target"
    /// (Into) and "drop to root" (target == null) decisions for both TreeView and
    /// FlowView (#2060).
    ///
    /// Scoped to the Into + null-target case; Before/After sibling-reorder remains
    /// in each view's own path for now (see follow-up #2109).
    /// </summary>
    public class DialogDragDropValidatorTests
    {
        // --- Helpers -----------------------------------------------------------

        private static Dialog BuildDialog(out DialogNode entry, out DialogNode reply)
        {
            var dialog = new Dialog();

            entry = dialog.CreateNode(DialogNodeType.Entry)!;
            entry.Text.Add(0, "Entry");
            dialog.AddNodeInternal(entry, entry.Type);

            var start = dialog.CreatePtr()!;
            start.Node = entry;
            start.Type = DialogNodeType.Entry;
            start.Index = 0;
            start.IsStart = true;
            dialog.Starts.Add(start);

            reply = dialog.CreateNode(DialogNodeType.Reply)!;
            reply.Text.Add(0, "Reply");
            dialog.AddNodeInternal(reply, reply.Type);

            var ptr = dialog.CreatePtr()!;
            ptr.Node = reply;
            ptr.Type = DialogNodeType.Reply;
            ptr.Index = 0;
            entry.Pointers.Add(ptr);

            return dialog;
        }

        // --- Drop-to-root (target == null) ------------------------------------

        [Fact]
        public void DropEntryOnRoot_IsValid()
        {
            // Arrange
            var dialog = BuildDialog(out var entry, out _);
            var newEntry = dialog.CreateNode(DialogNodeType.Entry)!;
            newEntry.Text.Add(0, "New Entry");
            dialog.AddNodeInternal(newEntry, newEntry.Type);

            // Act
            var result = DialogDragDropValidator.ValidateReparent(newEntry, target: null, dialog);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(DropTargetKind.Root, result.TargetKind);
            Assert.Null(result.RejectReason);
        }

        [Fact]
        public void DropReplyOnRoot_IsInvalid_WithReason()
        {
            // Arrange
            var dialog = BuildDialog(out _, out var reply);

            // Act: Reply cannot be a root — only NPC Entries are valid starts
            var result = DialogDragDropValidator.ValidateReparent(reply, target: null, dialog);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(DropTargetKind.Root, result.TargetKind);
            Assert.NotNull(result.RejectReason);
        }

        // --- Drop-on-self ------------------------------------------------------

        [Fact]
        public void DropOnSelf_IsInvalid()
        {
            // Arrange
            var dialog = BuildDialog(out var entry, out _);

            // Act
            var result = DialogDragDropValidator.ValidateReparent(entry, target: entry, dialog);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.RejectReason);
        }

        // --- Circular reference -----------------------------------------------

        [Fact]
        public void DropOnDescendant_IsInvalid_CircularReference()
        {
            // Arrange: entry → reply → childEntry (3-deep chain)
            var dialog = BuildDialog(out var entry, out var reply);
            var childEntry = dialog.CreateNode(DialogNodeType.Entry)!;
            childEntry.Text.Add(0, "Child Entry");
            dialog.AddNodeInternal(childEntry, childEntry.Type);
            var ptr = dialog.CreatePtr()!;
            ptr.Node = childEntry;
            ptr.Type = DialogNodeType.Entry;
            ptr.Index = 0;
            reply.Pointers.Add(ptr);

            // Act: dragging entry onto its own descendant childEntry would create a cycle
            var result = DialogDragDropValidator.ValidateReparent(entry, target: childEntry, dialog);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.RejectReason);
        }

        // --- NPC / PC placement rules -----------------------------------------

        [Fact]
        public void DropEntryOnEntry_IsInvalid()
        {
            // Arrange: two root-level Entry nodes
            var dialog = BuildDialog(out var entry, out _);
            var other = dialog.CreateNode(DialogNodeType.Entry)!;
            other.Text.Add(0, "Other Entry");
            dialog.AddNodeInternal(other, other.Type);

            // Act: Entry cannot be a child of an Entry (parent/child types must alternate)
            var result = DialogDragDropValidator.ValidateReparent(entry, target: other, dialog);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.RejectReason);
        }

        [Fact]
        public void DropReplyOnEntry_IsValid()
        {
            // Arrange: new Reply, existing Entry target
            var dialog = BuildDialog(out var entry, out _);
            var newReply = dialog.CreateNode(DialogNodeType.Reply)!;
            newReply.Text.Add(0, "New Reply");
            dialog.AddNodeInternal(newReply, newReply.Type);

            // Act
            var result = DialogDragDropValidator.ValidateReparent(newReply, target: entry, dialog);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(DropTargetKind.Node, result.TargetKind);
        }

        [Fact]
        public void DropEntryOnReply_IsValid()
        {
            // Arrange
            var dialog = BuildDialog(out _, out var reply);
            var newEntry = dialog.CreateNode(DialogNodeType.Entry)!;
            newEntry.Text.Add(0, "New Entry");
            dialog.AddNodeInternal(newEntry, newEntry.Type);

            // Act
            var result = DialogDragDropValidator.ValidateReparent(newEntry, target: reply, dialog);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(DropTargetKind.Node, result.TargetKind);
        }
    }
}

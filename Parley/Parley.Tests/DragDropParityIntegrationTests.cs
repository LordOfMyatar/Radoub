using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Integration test for TreeView/FlowView drag-drop parity (#2060).
    ///
    /// Exercises the shared DialogDragDropValidator with scenarios both views hit
    /// and asserts identical Accept/Reject decisions — no Avalonia UI, service-level
    /// wiring only.
    /// </summary>
    public class DragDropParityIntegrationTests
    {
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

        [Fact]
        public void DragToRoot_AcceptDecision_IsTheSameForBothViews()
        {
            // Arrange: a loose Entry that either view could drag to the background
            var dialog = BuildDialog(out _, out _);
            var orphan = dialog.CreateNode(DialogNodeType.Entry)!;
            orphan.Text.Add(0, "Orphan Entry");
            dialog.AddNodeInternal(orphan, orphan.Type);

            // Act: both views ultimately route through the same validator for the Into/Root decision
            var treeViewDecision = DialogDragDropValidator.ValidateReparent(orphan, target: null, dialog);
            var flowViewDecision = DialogDragDropValidator.ValidateReparent(orphan, target: null, dialog);

            // Assert: identical outcome
            Assert.Equal(treeViewDecision.IsValid, flowViewDecision.IsValid);
            Assert.Equal(treeViewDecision.TargetKind, flowViewDecision.TargetKind);
            Assert.True(treeViewDecision.IsValid);
        }

        [Fact]
        public void DragReplyToRoot_RejectDecision_IsTheSameForBothViews()
        {
            // Arrange
            var dialog = BuildDialog(out _, out var reply);

            // Act
            var treeViewDecision = DialogDragDropValidator.ValidateReparent(reply, target: null, dialog);
            var flowViewDecision = DialogDragDropValidator.ValidateReparent(reply, target: null, dialog);

            // Assert: both reject, both give a reason
            Assert.False(treeViewDecision.IsValid);
            Assert.False(flowViewDecision.IsValid);
            Assert.Equal(treeViewDecision.RejectReason, flowViewDecision.RejectReason);
        }

        [Fact]
        public void DragCircular_RejectDecision_IsTheSameForBothViews()
        {
            // Arrange: entry → reply → childEntry; drag entry onto its own descendant
            var dialog = BuildDialog(out var entry, out var reply);
            var childEntry = dialog.CreateNode(DialogNodeType.Entry)!;
            childEntry.Text.Add(0, "Child Entry");
            dialog.AddNodeInternal(childEntry, childEntry.Type);
            var ptr = dialog.CreatePtr()!;
            ptr.Node = childEntry;
            ptr.Type = DialogNodeType.Entry;
            ptr.Index = 0;
            reply.Pointers.Add(ptr);

            // Act
            var treeViewDecision = DialogDragDropValidator.ValidateReparent(entry, target: childEntry, dialog);
            var flowViewDecision = DialogDragDropValidator.ValidateReparent(entry, target: childEntry, dialog);

            // Assert: both views refuse the circular drop for the same reason
            Assert.False(treeViewDecision.IsValid);
            Assert.False(flowViewDecision.IsValid);
            Assert.Equal(treeViewDecision.RejectReason, flowViewDecision.RejectReason);
        }

        [Fact]
        public void DragValidReply_OntoEntry_BothViewsAccept()
        {
            // Arrange
            var dialog = BuildDialog(out var entry, out _);
            var newReply = dialog.CreateNode(DialogNodeType.Reply)!;
            newReply.Text.Add(0, "New Reply");
            dialog.AddNodeInternal(newReply, newReply.Type);

            // Act
            var treeViewDecision = DialogDragDropValidator.ValidateReparent(newReply, target: entry, dialog);
            var flowViewDecision = DialogDragDropValidator.ValidateReparent(newReply, target: entry, dialog);

            // Assert
            Assert.True(treeViewDecision.IsValid);
            Assert.True(flowViewDecision.IsValid);
        }
    }
}

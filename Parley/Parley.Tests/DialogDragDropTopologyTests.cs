using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for the shared DialogNode-level topology helpers on DialogDragDropValidator
    /// (#2109): parent resolution, sibling detection, and sibling-reorder index math.
    /// These replace the per-view duplicates (TreeView's GetParentNodeForDialogNode /
    /// GetSiblingIndexForDialogNode, FlowView's AreSiblings / ExecuteSiblingReorder).
    /// </summary>
    public class DialogDragDropTopologyTests
    {
        // entry (root start) -> reply -> childEntry
        private static Dialog BuildChain(out DialogNode entry, out DialogNode reply, out DialogNode childEntry)
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

            childEntry = dialog.CreateNode(DialogNodeType.Entry)!;
            childEntry.Text.Add(0, "Child Entry");
            dialog.AddNodeInternal(childEntry, childEntry.Type);
            var ptr2 = dialog.CreatePtr()!;
            ptr2.Node = childEntry;
            ptr2.Type = DialogNodeType.Entry;
            ptr2.Index = 0;
            reply.Pointers.Add(ptr2);

            return dialog;
        }

        private static DialogNode AddReplyChild(Dialog dialog, DialogNode parent, string text)
        {
            var node = dialog.CreateNode(DialogNodeType.Reply)!;
            node.Text.Add(0, text);
            dialog.AddNodeInternal(node, node.Type);
            var ptr = dialog.CreatePtr()!;
            ptr.Node = node;
            ptr.Type = DialogNodeType.Reply;
            ptr.Index = (uint)parent.Pointers.Count;
            parent.Pointers.Add(ptr);
            return node;
        }

        [Fact]
        public void ResolveParent_RootEntry_ReturnsNull()
        {
            var dialog = BuildChain(out var entry, out _, out _);
            Assert.Null(DialogDragDropValidator.ResolveParent(entry, dialog));
        }

        [Fact]
        public void ResolveParent_Reply_ReturnsOwningEntry()
        {
            var dialog = BuildChain(out var entry, out var reply, out _);
            Assert.Same(entry, DialogDragDropValidator.ResolveParent(reply, dialog));
        }

        [Fact]
        public void ResolveParent_ChildEntry_ReturnsOwningReply()
        {
            var dialog = BuildChain(out _, out var reply, out var childEntry);
            Assert.Same(reply, DialogDragDropValidator.ResolveParent(childEntry, dialog));
        }

        [Fact]
        public void AreSiblings_TwoRootEntries_True()
        {
            var dialog = BuildChain(out var entry, out _, out _);
            var entry2 = dialog.CreateNode(DialogNodeType.Entry)!;
            entry2.Text.Add(0, "Entry2");
            dialog.AddNodeInternal(entry2, entry2.Type);
            var start = dialog.CreatePtr()!;
            start.Node = entry2;
            start.Type = DialogNodeType.Entry;
            start.Index = 1;
            start.IsStart = true;
            dialog.Starts.Add(start);

            Assert.True(DialogDragDropValidator.AreSiblings(entry, entry2, dialog));
        }

        [Fact]
        public void AreSiblings_TwoRepliesUnderSameEntry_True()
        {
            var dialog = BuildChain(out var entry, out var reply, out _);
            var reply2 = AddReplyChild(dialog, entry, "Reply2");
            Assert.True(DialogDragDropValidator.AreSiblings(reply, reply2, dialog));
        }

        [Fact]
        public void AreSiblings_NodesUnderDifferentParents_False()
        {
            var dialog = BuildChain(out var entry, out var reply, out var childEntry);
            // reply is under entry; childEntry is under reply — not siblings
            Assert.False(DialogDragDropValidator.AreSiblings(reply, childEntry, dialog));
        }

        [Fact]
        public void AreSiblings_RootAndNonRoot_False()
        {
            var dialog = BuildChain(out var entry, out var reply, out _);
            Assert.False(DialogDragDropValidator.AreSiblings(entry, reply, dialog));
        }

        [Fact]
        public void ResolveSiblingReorder_RepliesUnderEntry_FindsParentAndIndices()
        {
            var dialog = BuildChain(out var entry, out var reply, out _);
            var reply2 = AddReplyChild(dialog, entry, "Reply2");

            var result = DialogDragDropValidator.ResolveSiblingReorder(reply, reply2, dialog);

            Assert.True(result.Found);
            Assert.Same(entry, result.Parent);
            Assert.Equal(0, result.FromIndex);
            Assert.Equal(1, result.ToIndex);
        }

        [Fact]
        public void ResolveSiblingReorder_RootEntries_ParentNullIndicesFromStarts()
        {
            var dialog = BuildChain(out var entry, out _, out _);
            var entry2 = dialog.CreateNode(DialogNodeType.Entry)!;
            entry2.Text.Add(0, "Entry2");
            dialog.AddNodeInternal(entry2, entry2.Type);
            var start = dialog.CreatePtr()!;
            start.Node = entry2;
            start.Type = DialogNodeType.Entry;
            start.Index = 1;
            start.IsStart = true;
            dialog.Starts.Add(start);

            var result = DialogDragDropValidator.ResolveSiblingReorder(entry2, entry, dialog);

            Assert.True(result.Found);
            Assert.Null(result.Parent);
            Assert.Equal(1, result.FromIndex);
            Assert.Equal(0, result.ToIndex);
        }

        [Fact]
        public void ResolveSiblingReorder_NotSiblings_NotFound()
        {
            var dialog = BuildChain(out _, out var reply, out var childEntry);
            var result = DialogDragDropValidator.ResolveSiblingReorder(reply, childEntry, dialog);
            Assert.False(result.Found);
        }

        [Fact]
        public void GetSiblingInsertIndex_ChildBefore_ReturnsTargetIndex()
        {
            var dialog = BuildChain(out var entry, out var reply, out _);
            var reply2 = AddReplyChild(dialog, entry, "Reply2"); // index 1 under entry
            Assert.Equal(1, DialogDragDropValidator.GetSiblingInsertIndex(reply2, entry, dialog, insertAfter: false));
        }

        [Fact]
        public void GetSiblingInsertIndex_ChildAfter_ReturnsTargetIndexPlusOne()
        {
            var dialog = BuildChain(out var entry, out var reply, out _);
            AddReplyChild(dialog, entry, "Reply2");
            Assert.Equal(1, DialogDragDropValidator.GetSiblingInsertIndex(reply, entry, dialog, insertAfter: true));
        }

        [Fact]
        public void GetSiblingInsertIndex_RootLevel_UsesStartsIndex()
        {
            var dialog = BuildChain(out var entry, out _, out _);
            Assert.Equal(0, DialogDragDropValidator.GetSiblingInsertIndex(entry, null, dialog, insertAfter: false));
            Assert.Equal(1, DialogDragDropValidator.GetSiblingInsertIndex(entry, null, dialog, insertAfter: true));
        }
    }
}

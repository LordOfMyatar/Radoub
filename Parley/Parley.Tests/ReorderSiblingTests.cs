using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// TDD tests for ReorderSibling operation (#240).
    /// Tests arbitrary sibling reorder within parent's Pointers or Dialog.Starts.
    /// </summary>
    public class ReorderSiblingTests
    {
        #region Root-Level Reorder (Dialog.Starts)

        [Fact]
        public void ReorderSibling_RootLevel_FirstToLast()
        {
            // Arrange
            var dialog = CreateDialogWithThreeRootEntries();
            var node0 = dialog.Starts[0].Node!;
            var ops = CreateMoveOperations();

            // Act — move first root node to last position
            var result = ops.ReorderSibling(dialog, node0, null, 0, 2, out var message);

            // Assert
            Assert.True(result);
            Assert.Same(node0, dialog.Starts[2].Node);
        }

        [Fact]
        public void ReorderSibling_RootLevel_LastToFirst()
        {
            var dialog = CreateDialogWithThreeRootEntries();
            var node2 = dialog.Starts[2].Node!;
            var ops = CreateMoveOperations();

            var result = ops.ReorderSibling(dialog, node2, null, 2, 0, out var message);

            Assert.True(result);
            Assert.Same(node2, dialog.Starts[0].Node);
        }

        [Fact]
        public void ReorderSibling_RootLevel_MiddleToFirst()
        {
            var dialog = CreateDialogWithThreeRootEntries();
            var node1 = dialog.Starts[1].Node!;
            var ops = CreateMoveOperations();

            var result = ops.ReorderSibling(dialog, node1, null, 1, 0, out var message);

            Assert.True(result);
            Assert.Same(node1, dialog.Starts[0].Node);
        }

        [Fact]
        public void ReorderSibling_RootLevel_SamePosition_ReturnsFalse()
        {
            var dialog = CreateDialogWithThreeRootEntries();
            var node1 = dialog.Starts[1].Node!;
            var ops = CreateMoveOperations();

            var result = ops.ReorderSibling(dialog, node1, null, 1, 1, out var message);

            Assert.False(result);
        }

        [Fact]
        public void ReorderSibling_RootLevel_PreservesOtherNodes()
        {
            var dialog = CreateDialogWithThreeRootEntries();
            var node0 = dialog.Starts[0].Node!;
            var node1 = dialog.Starts[1].Node!;
            var node2 = dialog.Starts[2].Node!;
            var ops = CreateMoveOperations();

            // Move first to last
            ops.ReorderSibling(dialog, node0, null, 0, 2, out _);

            // Original order: 0,1,2 → After moving 0 to pos 2: 1,2,0
            Assert.Same(node1, dialog.Starts[0].Node);
            Assert.Same(node2, dialog.Starts[1].Node);
            Assert.Same(node0, dialog.Starts[2].Node);
        }

        #endregion

        #region Child-Level Reorder (Parent.Pointers)

        [Fact]
        public void ReorderSibling_ChildLevel_FirstToLast()
        {
            var dialog = CreateDialogWithThreeReplies();
            var parent = dialog.Starts[0].Node!;
            var reply0 = parent.Pointers[0].Node!;
            var ops = CreateMoveOperations();

            var result = ops.ReorderSibling(dialog, reply0, parent, 0, 2, out var message);

            Assert.True(result);
            Assert.Same(reply0, parent.Pointers[2].Node);
        }

        [Fact]
        public void ReorderSibling_ChildLevel_LastToFirst()
        {
            var dialog = CreateDialogWithThreeReplies();
            var parent = dialog.Starts[0].Node!;
            var reply2 = parent.Pointers[2].Node!;
            var ops = CreateMoveOperations();

            var result = ops.ReorderSibling(dialog, reply2, parent, 2, 0, out var message);

            Assert.True(result);
            Assert.Same(reply2, parent.Pointers[0].Node);
        }

        [Fact]
        public void ReorderSibling_ChildLevel_PreservesOtherNodes()
        {
            var dialog = CreateDialogWithThreeReplies();
            var parent = dialog.Starts[0].Node!;
            var reply0 = parent.Pointers[0].Node!;
            var reply1 = parent.Pointers[1].Node!;
            var reply2 = parent.Pointers[2].Node!;
            var ops = CreateMoveOperations();

            // Move first to last: 0,1,2 → 1,2,0
            ops.ReorderSibling(dialog, reply0, parent, 0, 2, out _);

            Assert.Same(reply1, parent.Pointers[0].Node);
            Assert.Same(reply2, parent.Pointers[1].Node);
            Assert.Same(reply0, parent.Pointers[2].Node);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void ReorderSibling_SingleChild_ReturnsFalse()
        {
            var dialog = new Dialog();
            var entry = dialog.CreateNode(DialogNodeType.Entry);
            dialog.AddNodeInternal(entry, DialogNodeType.Entry);
            var startPtr = dialog.CreatePtr();
            startPtr!.Node = entry;
            startPtr.Type = DialogNodeType.Entry;
            dialog.Starts.Add(startPtr);

            var reply = dialog.CreateNode(DialogNodeType.Reply);
            dialog.AddNodeInternal(reply, DialogNodeType.Reply);
            var replyPtr = dialog.CreatePtr();
            replyPtr!.Node = reply;
            replyPtr.Type = DialogNodeType.Reply;
            entry.Pointers.Add(replyPtr);

            var ops = CreateMoveOperations();

            // Only one child — can't reorder
            var result = ops.ReorderSibling(dialog, reply, entry, 0, 0, out var message);

            Assert.False(result);
        }

        [Fact]
        public void ReorderSibling_InvalidFromIndex_ReturnsFalse()
        {
            var dialog = CreateDialogWithThreeRootEntries();
            var node0 = dialog.Starts[0].Node!;
            var ops = CreateMoveOperations();

            // fromIndex out of range
            var result = ops.ReorderSibling(dialog, node0, null, 99, 0, out var message);

            Assert.False(result);
        }

        [Fact]
        public void ReorderSibling_LinkNodeAmongSiblings_Succeeds()
        {
            // Create parent with 3 children: regular, link, regular
            var dialog = CreateDialogWithThreeReplies();
            var parent = dialog.Starts[0].Node!;

            // Make middle pointer a link
            parent.Pointers[1].IsLink = true;

            var linkNode = parent.Pointers[1].Node!;
            var ops = CreateMoveOperations();

            // Move link node from middle to first
            var result = ops.ReorderSibling(dialog, linkNode, parent, 1, 0, out var message);

            Assert.True(result);
            Assert.Same(linkNode, parent.Pointers[0].Node);
        }

        [Fact]
        public void ReorderSibling_MixedRegularAndLinkSiblings_PreservesAll()
        {
            var dialog = CreateDialogWithThreeReplies();
            var parent = dialog.Starts[0].Node!;

            // Make first pointer a link
            parent.Pointers[0].IsLink = true;

            var link0 = parent.Pointers[0].Node!;
            var regular1 = parent.Pointers[1].Node!;
            var regular2 = parent.Pointers[2].Node!;
            var ops = CreateMoveOperations();

            // Move regular node (index 2) past link node to first position
            ops.ReorderSibling(dialog, regular2, parent, 2, 0, out _);

            Assert.Same(regular2, parent.Pointers[0].Node);
            Assert.Same(link0, parent.Pointers[1].Node);
            Assert.Same(regular1, parent.Pointers[2].Node);
            Assert.True(parent.Pointers[1].IsLink); // Link flag preserved
        }

        #endregion

        #region Event Publishing

        [Fact]
        public void ReorderSibling_PublishesNodeMovedEvent()
        {
            var dialog = CreateDialogWithThreeRootEntries();
            var node0 = dialog.Starts[0].Node!;
            var ops = CreateMoveOperations();

            bool eventFired = false;
            DialogChangeEventBus.Instance.DialogChanged += (sender, args) =>
            {
                if (args.ChangeType == DialogChangeType.NodeMoved)
                    eventFired = true;
            };

            ops.ReorderSibling(dialog, node0, null, 0, 2, out _);

            Assert.True(eventFired);
        }

        #endregion

        #region Helpers

        private NodeOperationsManager CreateMoveOperations()
        {
            return new NodeOperationsManager(new DialogEditorService(), new ScrapManager(), new OrphanNodeManager());
        }

        private Dialog CreateDialogWithThreeRootEntries()
        {
            var dialog = new Dialog();

            for (int i = 0; i < 3; i++)
            {
                var entry = dialog.CreateNode(DialogNodeType.Entry);
                entry.Text.Add(0, $"Entry {i}");
                dialog.AddNodeInternal(entry, DialogNodeType.Entry);

                var ptr = dialog.CreatePtr();
                ptr!.Node = entry;
                ptr.Type = DialogNodeType.Entry;
                ptr.IsStart = true;
                dialog.Starts.Add(ptr);
            }

            return dialog;
        }

        private Dialog CreateDialogWithThreeReplies()
        {
            var dialog = new Dialog();

            // Create root entry
            var entry = dialog.CreateNode(DialogNodeType.Entry);
            entry.Text.Add(0, "Root entry");
            dialog.AddNodeInternal(entry, DialogNodeType.Entry);
            var startPtr = dialog.CreatePtr();
            startPtr!.Node = entry;
            startPtr.Type = DialogNodeType.Entry;
            startPtr.IsStart = true;
            dialog.Starts.Add(startPtr);

            // Add 3 replies as children
            for (int i = 0; i < 3; i++)
            {
                var reply = dialog.CreateNode(DialogNodeType.Reply);
                reply.Text.Add(0, $"Reply {i}");
                dialog.AddNodeInternal(reply, DialogNodeType.Reply);

                var replyPtr = dialog.CreatePtr();
                replyPtr!.Node = reply;
                replyPtr.Type = DialogNodeType.Reply;
                entry.Pointers.Add(replyPtr);
            }

            return dialog;
        }

        #endregion
    }
}

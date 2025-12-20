using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for drag-drop functionality via NodeOperationsManager.MoveNodeToPosition.
    /// These tests verify the core move logic without UI dependencies.
    /// </summary>
    public class DragDropMoveNodeTests
    {
        #region Test Helpers

        private (Dialog dialog, NodeOperationsManager nodeOps) CreateTestSetup()
        {
            var dialog = new Dialog();

            // Create 3 root entries: Entry0, Entry1, Entry2
            var entry0 = dialog.CreateNode(DialogNodeType.Entry);
            entry0!.Text.Add(0, "Entry 0");
            dialog.AddNodeInternal(entry0, entry0.Type);

            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            entry1!.Text.Add(0, "Entry 1");
            dialog.AddNodeInternal(entry1, entry1.Type);

            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            entry2!.Text.Add(0, "Entry 2");
            dialog.AddNodeInternal(entry2, entry2.Type);

            // Create start pointers for all entries
            var start0 = dialog.CreatePtr();
            start0!.Node = entry0;
            start0.Type = DialogNodeType.Entry;
            start0.Index = 0;
            start0.IsStart = true;
            dialog.Starts.Add(start0);

            var start1 = dialog.CreatePtr();
            start1!.Node = entry1;
            start1.Type = DialogNodeType.Entry;
            start1.Index = 1;
            start1.IsStart = true;
            dialog.Starts.Add(start1);

            var start2 = dialog.CreatePtr();
            start2!.Node = entry2;
            start2.Type = DialogNodeType.Entry;
            start2.Index = 2;
            start2.IsStart = true;
            dialog.Starts.Add(start2);

            // Add a reply under Entry0
            var reply0 = dialog.CreateNode(DialogNodeType.Reply);
            reply0!.Text.Add(0, "Reply 0");
            dialog.AddNodeInternal(reply0, reply0.Type);

            var ptr0 = dialog.CreatePtr();
            ptr0!.Node = reply0;
            ptr0.Type = DialogNodeType.Reply;
            ptr0.Index = 0;
            entry0.Pointers.Add(ptr0);

            // Add a second reply under Entry0
            var reply1 = dialog.CreateNode(DialogNodeType.Reply);
            reply1!.Text.Add(0, "Reply 1");
            dialog.AddNodeInternal(reply1, reply1.Type);

            var ptr1 = dialog.CreatePtr();
            ptr1!.Node = reply1;
            ptr1.Type = DialogNodeType.Reply;
            ptr1.Index = 1;
            entry0.Pointers.Add(ptr1);

            // Rebuild link registry
            dialog.LinkRegistry.RebuildFromDialog(dialog);

            // Create NodeOperationsManager with mock dependencies
            var editorService = new DialogEditorService();
            var scrapManager = new ScrapManager();
            var orphanManager = new OrphanNodeManager();
            var nodeOps = new NodeOperationsManager(editorService, scrapManager, orphanManager);

            return (dialog, nodeOps);
        }

        private string GetNodeText(DialogNode? node)
        {
            return node?.Text.GetDefault() ?? "";
        }

        #endregion

        #region Reorder Within Same Parent (Root Level)

        [Fact]
        public void MoveNodeToPosition_ReorderRootEntryDown_MovesCorrectly()
        {
            // Arrange: [Entry0, Entry1, Entry2]
            var (dialog, nodeOps) = CreateTestSetup();
            var entry0 = dialog.Entries[0];
            var sourcePtr = dialog.Starts[0]; // Pointer to Entry0

            // Act: Move Entry0 to after Entry2 (index 3, which becomes 2 after removal)
            bool result = nodeOps.MoveNodeToPosition(dialog, entry0, sourcePtr, null, 3, out string status);

            // Assert: [Entry1, Entry2, Entry0]
            Assert.True(result, status);
            Assert.Equal(3, dialog.Starts.Count);
            Assert.Equal("Entry 1", GetNodeText(dialog.Starts[0].Node));
            Assert.Equal("Entry 2", GetNodeText(dialog.Starts[1].Node));
            Assert.Equal("Entry 0", GetNodeText(dialog.Starts[2].Node));
        }

        [Fact]
        public void MoveNodeToPosition_ReorderRootEntryUp_MovesCorrectly()
        {
            // Arrange: [Entry0, Entry1, Entry2]
            var (dialog, nodeOps) = CreateTestSetup();
            var entry2 = dialog.Entries[2];
            var sourcePtr = dialog.Starts[2]; // Pointer to Entry2

            // Act: Move Entry2 to before Entry0 (index 0)
            bool result = nodeOps.MoveNodeToPosition(dialog, entry2, sourcePtr, null, 0, out string status);

            // Assert: [Entry2, Entry0, Entry1]
            Assert.True(result, status);
            Assert.Equal(3, dialog.Starts.Count);
            Assert.Equal("Entry 2", GetNodeText(dialog.Starts[0].Node));
            Assert.Equal("Entry 0", GetNodeText(dialog.Starts[1].Node));
            Assert.Equal("Entry 1", GetNodeText(dialog.Starts[2].Node));
        }

        [Fact]
        public void MoveNodeToPosition_ReorderRootEntryToMiddle_MovesCorrectly()
        {
            // Arrange: [Entry0, Entry1, Entry2]
            var (dialog, nodeOps) = CreateTestSetup();
            var entry2 = dialog.Entries[2];
            var sourcePtr = dialog.Starts[2]; // Pointer to Entry2

            // Act: Move Entry2 to after Entry0 (index 1)
            bool result = nodeOps.MoveNodeToPosition(dialog, entry2, sourcePtr, null, 1, out string status);

            // Assert: [Entry0, Entry2, Entry1]
            Assert.True(result, status);
            Assert.Equal(3, dialog.Starts.Count);
            Assert.Equal("Entry 0", GetNodeText(dialog.Starts[0].Node));
            Assert.Equal("Entry 2", GetNodeText(dialog.Starts[1].Node));
            Assert.Equal("Entry 1", GetNodeText(dialog.Starts[2].Node));
        }

        #endregion

        #region Reorder Within Same Parent (Child Level)

        [Fact]
        public void MoveNodeToPosition_ReorderChildDown_MovesCorrectly()
        {
            // Arrange: Entry0 has [Reply0, Reply1]
            var (dialog, nodeOps) = CreateTestSetup();
            var entry0 = dialog.Entries[0];
            var reply0 = dialog.Replies[0];
            var sourcePtr = entry0.Pointers[0]; // Pointer to Reply0

            // Act: Move Reply0 to after Reply1 (index 2, which becomes 1 after removal)
            bool result = nodeOps.MoveNodeToPosition(dialog, reply0, sourcePtr, entry0, 2, out string status);

            // Assert: Entry0 has [Reply1, Reply0]
            Assert.True(result, status);
            Assert.Equal(2, entry0.Pointers.Count);
            Assert.Equal("Reply 1", GetNodeText(entry0.Pointers[0].Node));
            Assert.Equal("Reply 0", GetNodeText(entry0.Pointers[1].Node));
        }

        [Fact]
        public void MoveNodeToPosition_ReorderChildUp_MovesCorrectly()
        {
            // Arrange: Entry0 has [Reply0, Reply1]
            var (dialog, nodeOps) = CreateTestSetup();
            var entry0 = dialog.Entries[0];
            var reply1 = dialog.Replies[1];
            var sourcePtr = entry0.Pointers[1]; // Pointer to Reply1

            // Act: Move Reply1 to before Reply0 (index 0)
            bool result = nodeOps.MoveNodeToPosition(dialog, reply1, sourcePtr, entry0, 0, out string status);

            // Assert: Entry0 has [Reply1, Reply0]
            Assert.True(result, status);
            Assert.Equal(2, entry0.Pointers.Count);
            Assert.Equal("Reply 1", GetNodeText(entry0.Pointers[0].Node));
            Assert.Equal("Reply 0", GetNodeText(entry0.Pointers[1].Node));
        }

        #endregion

        #region Reparent (Move Between Parents)

        [Fact]
        public void MoveNodeToPosition_MoveReplyToDifferentEntry_MovesCorrectly()
        {
            // Arrange: Entry0 has [Reply0, Reply1], Entry1 has no children
            var (dialog, nodeOps) = CreateTestSetup();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];
            var reply0 = dialog.Replies[0];
            var sourcePtr = entry0.Pointers[0]; // Pointer to Reply0

            // Act: Move Reply0 from Entry0 to Entry1
            bool result = nodeOps.MoveNodeToPosition(dialog, reply0, sourcePtr, entry1, 0, out string status);

            // Assert
            Assert.True(result, status);
            Assert.Single(entry0.Pointers); // Entry0 now has only Reply1
            Assert.Equal("Reply 1", GetNodeText(entry0.Pointers[0].Node));
            Assert.Single(entry1.Pointers); // Entry1 now has Reply0
            Assert.Equal("Reply 0", GetNodeText(entry1.Pointers[0].Node));
        }

        [Fact]
        public void MoveNodeToPosition_MoveEntryFromRootToReply_MovesCorrectly()
        {
            // Arrange: Move Entry1 (root) to be a child of Reply0
            var (dialog, nodeOps) = CreateTestSetup();
            var entry1 = dialog.Entries[1];
            var reply0 = dialog.Replies[0];
            var sourcePtr = dialog.Starts[1]; // Pointer to Entry1

            // Act: Move Entry1 from root to under Reply0
            bool result = nodeOps.MoveNodeToPosition(dialog, entry1, sourcePtr, reply0, 0, out string status);

            // Assert
            Assert.True(result, status);
            Assert.Equal(2, dialog.Starts.Count); // Only Entry0 and Entry2 at root
            Assert.Single(reply0.Pointers); // Reply0 now has Entry1 as child
            Assert.Equal("Entry 1", GetNodeText(reply0.Pointers[0].Node));
        }

        #endregion

        #region Type Validation

        [Fact]
        public void MoveNodeToPosition_ReplyToRoot_Fails()
        {
            // Arrange
            var (dialog, nodeOps) = CreateTestSetup();
            var entry0 = dialog.Entries[0];
            var reply0 = dialog.Replies[0];
            var sourcePtr = entry0.Pointers[0];

            // Act: Try to move Reply to root (not allowed)
            bool result = nodeOps.MoveNodeToPosition(dialog, reply0, sourcePtr, null, 0, out string status);

            // Assert
            Assert.False(result);
            Assert.Contains("Entry", status); // Should mention only Entry allowed at root
        }

        [Fact]
        public void MoveNodeToPosition_EntryUnderEntry_Fails()
        {
            // Arrange
            var (dialog, nodeOps) = CreateTestSetup();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];
            var sourcePtr = dialog.Starts[1];

            // Act: Try to move Entry1 under Entry0 (not allowed - Entry can only have Reply children)
            bool result = nodeOps.MoveNodeToPosition(dialog, entry1, sourcePtr, entry0, 0, out string status);

            // Assert
            Assert.False(result);
            Assert.Contains("Reply", status); // Should mention Entry can only have Reply children
        }

        [Fact]
        public void MoveNodeToPosition_ReplyUnderReply_Fails()
        {
            // Arrange
            var (dialog, nodeOps) = CreateTestSetup();
            var entry0 = dialog.Entries[0];
            var reply0 = dialog.Replies[0];
            var reply1 = dialog.Replies[1];
            var sourcePtr = entry0.Pointers[1]; // Pointer to Reply1

            // Act: Try to move Reply1 under Reply0 (not allowed - Reply can only have Entry children)
            bool result = nodeOps.MoveNodeToPosition(dialog, reply1, sourcePtr, reply0, 0, out string status);

            // Assert
            Assert.False(result);
            Assert.Contains("Entry", status); // Should mention Reply can only have Entry children
        }

        #endregion

        #region SourcePointer Handling

        [Fact]
        public void MoveNodeToPosition_WithSourcePointer_RemovesFromCorrectParent()
        {
            // Arrange: Create a node that appears in multiple places (Entry0 and Entry1 both point to same Reply)
            var (dialog, nodeOps) = CreateTestSetup();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];
            var entry2 = dialog.Entries[2];
            var reply0 = dialog.Replies[0];

            // Add a link from Entry1 to Reply0 (so Reply0 appears under both Entry0 and Entry1)
            var linkPtr = dialog.CreatePtr();
            linkPtr!.Node = reply0;
            linkPtr.Type = DialogNodeType.Reply;
            linkPtr.Index = 0;
            linkPtr.IsLink = true;
            entry1.Pointers.Add(linkPtr);

            // Get the original pointer from Entry0
            var originalPtr = entry0.Pointers[0];

            // Act: Move Reply0 from Entry0 (using sourcePtr to identify which occurrence)
            // Move it to Entry2
            bool result = nodeOps.MoveNodeToPosition(dialog, reply0, originalPtr, entry2, 0, out string status);

            // Assert: Reply0 should be removed from Entry0 but link in Entry1 should remain
            Assert.True(result, status);
            Assert.Single(entry0.Pointers); // Only Reply1 remains
            Assert.Equal("Reply 1", GetNodeText(entry0.Pointers[0].Node));
            Assert.Single(entry1.Pointers); // Link to Reply0 still exists
            Assert.Equal("Reply 0", GetNodeText(entry1.Pointers[0].Node));
            Assert.Single(entry2.Pointers); // Reply0 moved here
            Assert.Equal("Reply 0", GetNodeText(entry2.Pointers[0].Node));
        }

        [Fact]
        public void MoveNodeToPosition_WithoutSourcePointer_FallsBackToFindParent()
        {
            // Arrange
            var (dialog, nodeOps) = CreateTestSetup();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];
            var reply0 = dialog.Replies[0];

            // Act: Move Reply0 without sourcePtr (null)
            bool result = nodeOps.MoveNodeToPosition(dialog, reply0, null, entry1, 0, out string status);

            // Assert: Should still work by finding parent via fallback
            Assert.True(result, status);
            Assert.Single(entry0.Pointers); // Only Reply1 remains
            Assert.Single(entry1.Pointers); // Reply0 moved here
        }

        #endregion

        #region Remove Failure Handling

        [Fact]
        public void MoveNodeToPosition_PointerNotInExpectedParent_UseFallbackSearch()
        {
            // Arrange: Create a scenario where sourcePointer doesn't match reality
            // The system should use fallback search to find the actual parent
            var (dialog, nodeOps) = CreateTestSetup();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];
            var reply0 = dialog.Replies[0];

            // Create a fake pointer that's not in any parent's Pointers list
            var fakePtr = dialog.CreatePtr();
            fakePtr!.Node = reply0;
            fakePtr.Type = DialogNodeType.Reply;
            // Note: Don't add it to any parent's Pointers

            // Act: Move with the fake pointer - should use fallback to find actual parent (Entry0)
            bool result = nodeOps.MoveNodeToPosition(dialog, reply0, fakePtr, entry1, 0, out string status);

            // Assert: Should succeed because fallback search finds Reply0 under Entry0
            Assert.True(result, status);
            Assert.Single(entry0.Pointers); // Only Reply1 remains
            Assert.Equal("Reply 1", GetNodeText(entry0.Pointers[0].Node));
            Assert.Single(entry1.Pointers); // Reply0 moved here
            Assert.Equal("Reply 0", GetNodeText(entry1.Pointers[0].Node));
        }

        #endregion

        #region Index Clamping

        [Fact]
        public void MoveNodeToPosition_IndexBeyondEnd_ClampsToEnd()
        {
            // Arrange
            var (dialog, nodeOps) = CreateTestSetup();
            var entry0 = dialog.Entries[0];
            var sourcePtr = dialog.Starts[0];

            // Act: Move Entry0 to index 100 (way beyond the 3 items)
            bool result = nodeOps.MoveNodeToPosition(dialog, entry0, sourcePtr, null, 100, out string status);

            // Assert: Should clamp to end
            Assert.True(result, status);
            Assert.Equal(3, dialog.Starts.Count);
            Assert.Equal("Entry 0", GetNodeText(dialog.Starts[2].Node)); // Should be at end
        }

        #endregion

        #region IsStart Flag Management

        [Fact]
        public void MoveNodeToPosition_MoveToRoot_SetsIsStartTrue()
        {
            // Arrange: Move Entry from under a Reply to root
            var (dialog, nodeOps) = CreateTestSetup();
            var entry1 = dialog.Entries[1];
            var reply0 = dialog.Replies[0];
            var sourcePtr = dialog.Starts[1];

            // First move Entry1 under Reply0
            nodeOps.MoveNodeToPosition(dialog, entry1, sourcePtr, reply0, 0, out _);

            // Get the pointer that's now under Reply0
            var ptrUnderReply = reply0.Pointers[0];
            Assert.False(ptrUnderReply.IsStart);

            // Act: Move Entry1 back to root
            bool result = nodeOps.MoveNodeToPosition(dialog, entry1, ptrUnderReply, null, 0, out string status);

            // Assert
            Assert.True(result, status);
            var movedPtr = dialog.Starts.FirstOrDefault(s => s.Node == entry1);
            Assert.NotNull(movedPtr);
            Assert.True(movedPtr.IsStart);
        }

        [Fact]
        public void MoveNodeToPosition_MoveFromRoot_SetsIsStartFalse()
        {
            // Arrange
            var (dialog, nodeOps) = CreateTestSetup();
            var entry1 = dialog.Entries[1];
            var reply0 = dialog.Replies[0];
            var sourcePtr = dialog.Starts[1];

            Assert.True(sourcePtr.IsStart);

            // Act: Move Entry1 from root to under Reply0
            bool result = nodeOps.MoveNodeToPosition(dialog, entry1, sourcePtr, reply0, 0, out string status);

            // Assert
            Assert.True(result, status);
            var movedPtr = reply0.Pointers.FirstOrDefault(p => p.Node == entry1);
            Assert.NotNull(movedPtr);
            Assert.False(movedPtr.IsStart);
        }

        #endregion
    }
}

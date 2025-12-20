using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for TreeViewDragDropService validation logic.
    /// Tests the ValidateDrop method's decision making without UI.
    /// </summary>
    public class DragDropValidationTests
    {
        #region Test Helpers

        private Dialog CreateTestDialog()
        {
            var dialog = new Dialog();

            // Create entries
            var entry0 = dialog.CreateNode(DialogNodeType.Entry);
            entry0!.Text.Add(0, "Entry 0");
            dialog.AddNodeInternal(entry0, entry0.Type);

            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            entry1!.Text.Add(0, "Entry 1");
            dialog.AddNodeInternal(entry1, entry1.Type);

            // Create start pointers
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

            // Create replies under Entry0
            var reply0 = dialog.CreateNode(DialogNodeType.Reply);
            reply0!.Text.Add(0, "Reply 0");
            dialog.AddNodeInternal(reply0, reply0.Type);

            var ptr0 = dialog.CreatePtr();
            ptr0!.Node = reply0;
            ptr0.Type = DialogNodeType.Reply;
            ptr0.Index = 0;
            entry0.Pointers.Add(ptr0);

            var reply1 = dialog.CreateNode(DialogNodeType.Reply);
            reply1!.Text.Add(0, "Reply 1");
            dialog.AddNodeInternal(reply1, reply1.Type);

            var ptr1 = dialog.CreatePtr();
            ptr1!.Node = reply1;
            ptr1.Type = DialogNodeType.Reply;
            ptr1.Index = 1;
            entry0.Pointers.Add(ptr1);

            // Create entry under Reply0 (for deeper nesting tests)
            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            entry2!.Text.Add(0, "Entry 2 (under Reply0)");
            dialog.AddNodeInternal(entry2, entry2.Type);

            var ptr2 = dialog.CreatePtr();
            ptr2!.Node = entry2;
            ptr2.Type = DialogNodeType.Entry;
            ptr2.Index = 2;
            reply0.Pointers.Add(ptr2);

            dialog.LinkRegistry.RebuildFromDialog(dialog);

            return dialog;
        }

        private TreeViewSafeNode CreateTreeViewNode(DialogNode node, DialogPtr? sourcePtr = null, bool isLink = false)
        {
            if (isLink && sourcePtr != null)
            {
                sourcePtr.IsLink = true;
            }
            return new TreeViewSafeNode(node, null, 0, sourcePtr);
        }

        #endregion

        #region Self-Drop Rejection

        [Fact]
        public void ValidateDrop_SameSourceAndTarget_Rejects()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry0 = dialog.Entries[0];
            var sourcePtr = dialog.Starts[0];
            var sourceNode = CreateTreeViewNode(entry0, sourcePtr);

            // Act
            var result = service.ValidateDrop(sourceNode, sourceNode, DropPosition.Into);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("itself", result.ErrorMessage?.ToLower() ?? "");
        }

        #endregion

        #region Link Node Rejection

        [Fact]
        public void ValidateDrop_LinkNodeAsSource_Rejects()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];

            // Create a link node (IsChild = true based on IsLink)
            var linkPtr = dialog.CreatePtr();
            linkPtr!.Node = entry0;
            linkPtr.IsLink = true;
            var linkNode = CreateTreeViewNode(entry0, linkPtr, isLink: true);
            var targetNode = CreateTreeViewNode(entry1, dialog.Starts[1]);

            // Act
            var result = service.ValidateDrop(linkNode, targetNode, DropPosition.Into);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("link", result.ErrorMessage?.ToLower() ?? "");
        }

        #endregion

        #region Type Validation for Drop Positions

        [Fact]
        public void ValidateDrop_EntryIntoReply_Accepts()
        {
            // Arrange: Entry can go Into Reply (as child)
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry1 = dialog.Entries[1];
            var reply0 = dialog.Replies[0];
            var sourceNode = CreateTreeViewNode(entry1, dialog.Starts[1]);
            var targetNode = CreateTreeViewNode(reply0, dialog.Entries[0].Pointers[0]);

            // Act
            var result = service.ValidateDrop(sourceNode, targetNode, DropPosition.Into);

            // Assert
            Assert.True(result.IsValid, result.ErrorMessage);
        }

        [Fact]
        public void ValidateDrop_ReplyIntoEntry_Accepts()
        {
            // Arrange: Reply can go Into Entry (as child)
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];
            var reply0 = dialog.Replies[0];
            var sourceNode = CreateTreeViewNode(reply0, entry0.Pointers[0]);
            var targetNode = CreateTreeViewNode(entry1, dialog.Starts[1]);

            // Act
            var result = service.ValidateDrop(sourceNode, targetNode, DropPosition.Into);

            // Assert
            Assert.True(result.IsValid, result.ErrorMessage);
        }

        [Fact]
        public void ValidateDrop_EntryIntoEntry_Rejects()
        {
            // Arrange: Entry cannot go Into Entry (same type)
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];
            var sourceNode = CreateTreeViewNode(entry0, dialog.Starts[0]);
            var targetNode = CreateTreeViewNode(entry1, dialog.Starts[1]);

            // Act
            var result = service.ValidateDrop(sourceNode, targetNode, DropPosition.Into);

            // Assert
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateDrop_ReplyIntoReply_Rejects()
        {
            // Arrange: Reply cannot go Into Reply (same type)
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry0 = dialog.Entries[0];
            var reply0 = dialog.Replies[0];
            var reply1 = dialog.Replies[1];
            var sourceNode = CreateTreeViewNode(reply0, entry0.Pointers[0]);
            var targetNode = CreateTreeViewNode(reply1, entry0.Pointers[1]);

            // Act
            var result = service.ValidateDrop(sourceNode, targetNode, DropPosition.Into);

            // Assert
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateDrop_SameTypeBeforeAfter_Accepts()
        {
            // Arrange: Same type nodes can be reordered with Before/After
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];
            var sourceNode = CreateTreeViewNode(entry0, dialog.Starts[0]);
            var targetNode = CreateTreeViewNode(entry1, dialog.Starts[1]);

            // Act - Before
            var resultBefore = service.ValidateDrop(sourceNode, targetNode, DropPosition.Before);
            // Act - After
            var resultAfter = service.ValidateDrop(sourceNode, targetNode, DropPosition.After);

            // Assert
            Assert.True(resultBefore.IsValid, resultBefore.ErrorMessage);
            Assert.True(resultAfter.IsValid, resultAfter.ErrorMessage);
        }

        [Fact]
        public void ValidateDrop_DifferentTypeBeforeAfter_Rejects()
        {
            // Arrange: Different type nodes cannot use Before/After
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry0 = dialog.Entries[0];
            var reply0 = dialog.Replies[0];
            var sourceNode = CreateTreeViewNode(entry0, dialog.Starts[0]);
            var targetNode = CreateTreeViewNode(reply0, entry0.Pointers[0]);

            // Act - Before
            var resultBefore = service.ValidateDrop(sourceNode, targetNode, DropPosition.Before);
            // Act - After
            var resultAfter = service.ValidateDrop(sourceNode, targetNode, DropPosition.After);

            // Assert
            Assert.False(resultBefore.IsValid);
            Assert.False(resultAfter.IsValid);
        }

        #endregion

        #region Ancestor Chain Detection

        [Fact]
        public void ValidateDrop_IntoOwnChild_Rejects()
        {
            // Note: Full descendant detection requires UI tree structure (FindParentTreeNode).
            // In unit tests without UI, the type validation catches this first.
            // Entry -> Reply -> Entry chain: trying to drop Reply into its child Entry
            // is blocked by type rules (Reply can only have Entry children, but Entry2
            // already has Reply0 as parent, so the type hierarchy is enforced).
            //
            // For unit testing, we verify same-type rejection works (which prevents
            // Entry into Entry or Reply into Reply regardless of descendant status).
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry0 = dialog.Entries[0];
            var entry2 = dialog.Entries[2];
            var sourceNode = CreateTreeViewNode(entry0, dialog.Starts[0]);
            var targetNode = CreateTreeViewNode(entry2, dialog.Replies[0].Pointers[0]);

            // Act: Try to drop Entry0 into Entry2 (same type = rejected)
            var result = service.ValidateDrop(sourceNode, targetNode, DropPosition.Into);

            // Assert - rejected because same type can't be parent/child
            Assert.False(result.IsValid);
            // Error mentions siblings or type mismatch
            Assert.NotNull(result.ErrorMessage);
        }

        #endregion

        #region Root Level Validation

        [Fact]
        public void ValidateDrop_ReplyBeforeRootEntry_Rejects()
        {
            // Arrange: Reply cannot be placed at root level (before/after root entries)
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];
            var reply0 = dialog.Replies[0];
            var sourceNode = CreateTreeViewNode(reply0, entry0.Pointers[0]);
            var targetNode = CreateTreeViewNode(entry1, dialog.Starts[1]);

            // Act - same type check will fail first (Reply vs Entry)
            var result = service.ValidateDrop(sourceNode, targetNode, DropPosition.Before);

            // Assert
            Assert.False(result.IsValid);
        }

        #endregion

        #region InsertIndex Calculation

        [Fact]
        public void ValidateDrop_BeforePosition_ReturnsCorrectIndex()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];
            var sourceNode = CreateTreeViewNode(entry0, dialog.Starts[0]);
            var targetNode = CreateTreeViewNode(entry1, dialog.Starts[1]);

            // Act: Drop Before Entry1 (which is at index 1)
            var result = service.ValidateDrop(sourceNode, targetNode, DropPosition.Before);

            // Assert: InsertIndex should be 1 (the index of Entry1)
            Assert.True(result.IsValid);
            Assert.Equal(1, result.InsertIndex);
        }

        [Fact]
        public void ValidateDrop_AfterPosition_ReturnsCorrectIndex()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];
            var sourceNode = CreateTreeViewNode(entry0, dialog.Starts[0]);
            var targetNode = CreateTreeViewNode(entry1, dialog.Starts[1]);

            // Act: Drop After Entry1 (which is at index 1)
            var result = service.ValidateDrop(sourceNode, targetNode, DropPosition.After);

            // Assert: InsertIndex should be 2 (index + 1)
            Assert.True(result.IsValid);
            Assert.Equal(2, result.InsertIndex);
        }

        [Fact]
        public void ValidateDrop_IntoPosition_ReturnsChildCount()
        {
            // Arrange
            var dialog = CreateTestDialog();
            var service = new TreeViewDragDropService();
            var entry0 = dialog.Entries[0];
            var entry1 = dialog.Entries[1];
            var reply0 = dialog.Replies[0];
            var sourceNode = CreateTreeViewNode(entry1, dialog.Starts[1]);
            var targetNode = CreateTreeViewNode(reply0, entry0.Pointers[0]);

            // Reply0 already has 1 child (Entry2)
            Assert.Single(reply0.Pointers);

            // Act: Drop Entry1 Into Reply0
            var result = service.ValidateDrop(sourceNode, targetNode, DropPosition.Into);

            // Assert: InsertIndex should be the child count (1)
            Assert.True(result.IsValid);
            Assert.Equal(1, result.InsertIndex);
        }

        #endregion
    }
}

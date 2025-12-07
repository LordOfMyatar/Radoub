using DialogEditor.Models;
using Parley.Models;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for undo/redo operations (Issue #28)
    /// Validates that IsLink flags and dialog structure are preserved during undo/redo
    /// </summary>
    public class UndoRedoTests
    {
        [Fact]
        public void Undo_PreservesIsLinkFlags_AfterDelete()
        {
            // Arrange: Create dialog with shared reply structure
            var dialog = new Dialog();
            var undoManager = new UndoManager();

            // Entry 1: "What do you sell?"
            var entry1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry1.Text.Add(0, "What do you sell?");
            dialog.Entries.Add(entry1);

            // Reply 1: "I sell potions" (shared reply)
            var reply1 = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString()
            };
            reply1.Text.Add(0, "I sell potions");
            dialog.Replies.Add(reply1);

            // Pointer from Entry1 to Reply1 (original, IsLink=false)
            var ptrEntry1ToReply1 = new DialogPtr
            {
                Node = reply1,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false,
                Parent = dialog
            };
            entry1.Pointers.Add(ptrEntry1ToReply1);

            // Entry 2: "Anything else?"
            var entry2 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry2.Text.Add(0, "Anything else?");
            dialog.Entries.Add(entry2);

            // Pointer from Entry2 to Reply1 (link, IsLink=true)
            var ptrEntry2ToReply1 = new DialogPtr
            {
                Node = reply1,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = true,
                Parent = dialog
            };
            entry2.Pointers.Add(ptrEntry2ToReply1);

            // Rebuild LinkRegistry to track all pointers
            dialog.RebuildLinkRegistry();

            // Verify initial state
            Assert.False(ptrEntry1ToReply1.IsLink, "Entry1->Reply1 should be original (IsLink=false)");
            Assert.True(ptrEntry2ToReply1.IsLink, "Entry2->Reply1 should be link (IsLink=true)");

            // Save state before delete
            undoManager.SaveState(dialog, "Before delete");

            // Act: Delete Entry2
            dialog.RemoveNodeInternal(entry2, DialogNodeType.Entry);
            dialog.RebuildLinkRegistry();

            // Verify Entry2 is deleted
            Assert.Single(dialog.Entries);
            Assert.Equal("What do you sell?", dialog.Entries[0].DisplayText);

            // Undo the delete
            var restoredState = undoManager.Undo(dialog);
            Assert.NotNull(restoredState);
            dialog = restoredState!.Dialog;
            dialog.RebuildLinkRegistry();

            // Assert: Verify structure restored
            Assert.Equal(2, dialog.Entries.Count);
            Assert.Equal("What do you sell?", dialog.Entries[0].DisplayText);
            Assert.Equal("Anything else?", dialog.Entries[1].DisplayText);
            Assert.Single(dialog.Replies);
            Assert.Equal("I sell potions", dialog.Replies[0].DisplayText);

            // CRITICAL: Verify IsLink flags preserved (Issue #28 fix)
            var restoredEntry1 = dialog.Entries[0];
            var restoredEntry2 = dialog.Entries[1];
            var restoredReply1 = dialog.Replies[0];

            Assert.Single(restoredEntry1.Pointers);
            Assert.Single(restoredEntry2.Pointers);

            var restoredPtr1 = restoredEntry1.Pointers[0];
            var restoredPtr2 = restoredEntry2.Pointers[0];

            Assert.False(restoredPtr1.IsLink, "After undo: Entry1->Reply1 should still be original (IsLink=false)");
            Assert.True(restoredPtr2.IsLink, "After undo: Entry2->Reply1 should still be link (IsLink=true)");
            Assert.Same(restoredReply1, restoredPtr1.Node);
            Assert.Same(restoredReply1, restoredPtr2.Node);
        }

        [Fact]
        public void Redo_PreservesIsLinkFlags_AfterDeleteAndUndo()
        {
            // Arrange: Create dialog with shared reply structure
            var dialog = new Dialog();
            var undoManager = new UndoManager();

            // Entry 1 -> Reply 1 (original)
            var entry1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry1.Text.Add(0, "Entry 1");
            dialog.Entries.Add(entry1);

            var reply1 = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString()
            };
            reply1.Text.Add(0, "Reply 1");
            dialog.Replies.Add(reply1);

            var ptrOriginal = new DialogPtr
            {
                Node = reply1,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false,
                Parent = dialog
            };
            entry1.Pointers.Add(ptrOriginal);

            // Entry 2 -> Reply 1 (link)
            var entry2 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry2.Text.Add(0, "Entry 2");
            dialog.Entries.Add(entry2);

            var ptrLink = new DialogPtr
            {
                Node = reply1,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = true,
                Parent = dialog
            };
            entry2.Pointers.Add(ptrLink);

            dialog.RebuildLinkRegistry();

            // Save initial state
            undoManager.SaveState(dialog, "Initial state");

            // Delete Entry2
            dialog.RemoveNodeInternal(entry2, DialogNodeType.Entry);
            dialog.RebuildLinkRegistry();

            // Verify deleted
            Assert.Single(dialog.Entries);

            // Undo delete
            var undoneState = undoManager.Undo(dialog);
            Assert.NotNull(undoneState);
            dialog = undoneState!.Dialog;
            dialog.RebuildLinkRegistry();

            // Verify restored
            Assert.Equal(2, dialog.Entries.Count);

            // Act: Redo the delete
            var redoneState = undoManager.Redo(dialog);
            Assert.NotNull(redoneState);
            dialog = redoneState!.Dialog;
            dialog.RebuildLinkRegistry();

            // Assert: After redo, should be back to deleted state
            Assert.Single(dialog.Entries);
            Assert.Equal("Entry 1", dialog.Entries[0].DisplayText);
            Assert.Single(dialog.Replies);

            // Verify IsLink flag preserved on remaining pointer
            var remainingPtr = dialog.Entries[0].Pointers[0];
            Assert.False(remainingPtr.IsLink, "After redo: Entry1->Reply1 should still be original (IsLink=false)");
        }

        [Fact]
        public void Undo_PreservesNodeStructure_ComplexDialog()
        {
            // Arrange: Create complex dialog tree
            var dialog = new Dialog();
            var undoManager = new UndoManager();

            // Entry1 -> Reply1 -> Entry2
            //        -> Reply2 (link to same Reply1)
            var entry1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry1.Text.Add(0, "Entry 1");
            dialog.Entries.Add(entry1);

            var reply1 = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString()
            };
            reply1.Text.Add(0, "Reply 1");
            dialog.Replies.Add(reply1);

            var entry2 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry2.Text.Add(0, "Entry 2");
            dialog.Entries.Add(entry2);

            var reply2 = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString()
            };
            reply2.Text.Add(0, "Reply 2");
            dialog.Replies.Add(reply2);

            // Entry1 -> Reply1 (original)
            entry1.Pointers.Add(new DialogPtr
            {
                Node = reply1,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false,
                Parent = dialog
            });

            // Reply1 -> Entry2 (original)
            reply1.Pointers.Add(new DialogPtr
            {
                Node = entry2,
                Type = DialogNodeType.Entry,
                Index = 1,
                IsLink = false,
                Parent = dialog
            });

            // Entry1 -> Reply2 (link to same Reply1)
            entry1.Pointers.Add(new DialogPtr
            {
                Node = reply2,
                Type = DialogNodeType.Reply,
                Index = 1,
                IsLink = false,
                Parent = dialog
            });

            dialog.RebuildLinkRegistry();

            // Save initial state
            undoManager.SaveState(dialog, "Initial complex state");

            // Add new entry
            var entry3 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry3.Text.Add(0, "Entry 3");
            dialog.Entries.Add(entry3);
            dialog.RebuildLinkRegistry();

            // Verify added
            Assert.Equal(3, dialog.Entries.Count);

            // Act: Undo the add
            var restoredState = undoManager.Undo(dialog);
            Assert.NotNull(restoredState);
            dialog = restoredState!.Dialog;
            dialog.RebuildLinkRegistry();

            // Assert: Verify structure restored exactly
            Assert.Equal(2, dialog.Entries.Count);
            Assert.Equal(2, dialog.Replies.Count);
            Assert.Equal("Entry 1", dialog.Entries[0].DisplayText);
            Assert.Equal("Entry 2", dialog.Entries[1].DisplayText);
            Assert.Equal("Reply 1", dialog.Replies[0].DisplayText);
            Assert.Equal("Reply 2", dialog.Replies[1].DisplayText);

            // Verify pointer structure preserved
            var restoredEntry1 = dialog.Entries[0];
            var restoredReply1 = dialog.Replies[0];

            Assert.Equal(2, restoredEntry1.Pointers.Count);
            Assert.Single(restoredReply1.Pointers);
            Assert.False(restoredEntry1.Pointers[0].IsLink);
            Assert.False(restoredEntry1.Pointers[1].IsLink);
        }
    }
}

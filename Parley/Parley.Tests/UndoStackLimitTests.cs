using DialogEditor.Models;
using Parley.Models;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for undo stack limit validation (Issue #75)
    /// Verifies that the undo stack respects the configured limit of 50 operations
    /// </summary>
    public class UndoStackLimitTests
    {

        [Fact]
        public void UndoManager_LimitsStackTo50_WhenExcessiveStatesSaved()
        {
            // Arrange: Create UndoManager with default limit (50)
            var undoManager = new UndoManager();
            var dialog = new Dialog();

            // Create initial entry
            var entry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry.Text.Add(0, "Initial entry");
            dialog.Entries.Add(entry);

            // Act: Save 55 states to undo stack
            for (int i = 0; i < 55; i++)
            {
                undoManager.SaveState(dialog, $"State {i}");

                // Modify dialog slightly between saves to create distinct states
                entry.Text.Strings[0] = $"Modified entry {i}";
            }

            // Assert: Count available undo operations
            int undoCount = 0;
            Dialog? currentDialog = dialog;

            while (undoManager.CanUndo && undoCount < 60) // Safety limit
            {
                var undoState = undoManager.Undo(currentDialog!);
                if (undoState != null)
                {
                    currentDialog = undoState.Dialog;
                    undoCount++;
                }
                else
                {
                    break;
                }
            }

            // Should have exactly 50 undo operations (the configured limit)
            // Oldest 5 states should have been discarded
            Assert.Equal(50, undoCount);
        }

        [Fact]
        public void UndoManager_PreservesLifoOrder_AfterTrim()
        {
            // Arrange: maxStackSize = 3 so trimming kicks in quickly (#2260 LinkedList refactor).
            var undoManager = new UndoManager(maxStackSize: 3);
            var dialog = new Dialog();
            var entry = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };
            entry.Text.Add(0, "v0");
            dialog.Entries.Add(entry);

            // Act: save 5 states "v0".."v4"; only the newest 3 (v2,v3,v4) survive.
            for (int i = 0; i < 5; i++)
            {
                entry.Text.Strings[0] = $"v{i}";
                undoManager.SaveState(dialog, $"State {i}");
            }

            // Assert: undo returns newest-first (LIFO): v4, v3, v2, then empty.
            Dialog? current = dialog;
            var seen = new System.Collections.Generic.List<string>();
            while (undoManager.CanUndo)
            {
                var state = undoManager.Undo(current!);
                Assert.NotNull(state);
                current = state!.Dialog;
                seen.Add(current.Entries[0].Text.Strings[0]);
            }

            Assert.Equal(new[] { "v4", "v3", "v2" }, seen);
        }

        [Fact]
        public void UndoManager_DiscardLastSavedState_RemovesNewestOnly()
        {
            // Arrange
            var undoManager = new UndoManager();
            var dialog = new Dialog();
            var entry = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };
            entry.Text.Add(0, "a");
            dialog.Entries.Add(entry);

            undoManager.SaveState(dialog, "first");
            entry.Text.Strings[0] = "b";
            undoManager.SaveState(dialog, "second");

            // Act: discard the most recent ("second")
            bool discarded = undoManager.DiscardLastSavedState();

            // Assert: one state discarded, one remains
            Assert.True(discarded);
            Assert.True(undoManager.CanUndo);
            var state = undoManager.Undo(dialog);
            Assert.NotNull(state);
            Assert.False(undoManager.CanUndo); // only "first" was left
        }

        [Fact]
        public void UndoManager_DiscardLastSavedState_OnEmpty_ReturnsFalse()
        {
            var undoManager = new UndoManager();
            Assert.False(undoManager.DiscardLastSavedState());
        }

        [Fact]
        public void UndoManager_PreservesIsLinkFlags_AtStackLimit()
        {
            // Arrange: Create UndoManager and dialog with linked structure
            var undoManager = new UndoManager();
            var dialog = new Dialog();

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

            // Act: Fill undo stack to limit by saving 52 states
            for (int i = 0; i < 52; i++)
            {
                undoManager.SaveState(dialog, $"State {i}");

                // Make minor modifications between saves
                entry1.Text.Strings[0] = $"Entry 1 - version {i}";
            }

            // Undo back to an earlier state (beyond the first 2 discarded states)
            Dialog? restoredDialog = dialog;
            for (int i = 0; i < 10; i++)
            {
                var undoState = undoManager.Undo(restoredDialog!);
                Assert.NotNull(undoState);
                restoredDialog = undoState!.Dialog;
            }

            restoredDialog!.RebuildLinkRegistry();

            // Assert: Verify IsLink flags still correct after stack limit enforcement
            Assert.Equal(2, restoredDialog.Entries.Count);
            var restoredEntry1 = restoredDialog.Entries[0];
            var restoredEntry2 = restoredDialog.Entries[1];

            Assert.Single(restoredEntry1.Pointers);
            Assert.Single(restoredEntry2.Pointers);

            var restoredPtr1 = restoredEntry1.Pointers[0];
            var restoredPtr2 = restoredEntry2.Pointers[0];

            Assert.False(restoredPtr1.IsLink, "Entry1->Reply1 should be original (IsLink=false)");
            Assert.True(restoredPtr2.IsLink, "Entry2->Reply1 should be link (IsLink=true)");
        }
    }
}

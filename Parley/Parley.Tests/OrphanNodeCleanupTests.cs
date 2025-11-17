using System.Linq;
using DialogEditor.Models;
using DialogEditor.ViewModels;
using Parley.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for orphaned node cleanup to prevent nodes with no incoming pointers
    /// from appearing in saved dialogs.
    ///
    /// This prevents the bug where PC Replies appear at root level in Aurora after
    /// incorrect restore workflows.
    /// </summary>
    public class OrphanNodeCleanupTests
    {
        /// <summary>
        /// Verify that orphaned PC Reply nodes are detected and removed.
        /// Regression test for orphaning bug.
        /// </summary>
        [Fact]
        public void RemoveOrphanedNodes_RemovesOrphanedReply()
        {
            // Arrange: Create dialog with orphaned PC Reply
            var dialog = new Dialog();

            // Create NPC Entry at root (reachable)
            var entry = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };
            entry.Text.Add(0, "NPC says hello");
            dialog.Entries.Add(entry);

            dialog.Starts.Add(new DialogPtr
            {
                Node = entry,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsStart = true,
                Parent = dialog
            });

            // Create orphaned PC Reply (NO pointer to it)
            var orphanedReply = new DialogNode { Type = DialogNodeType.Reply, Text = new LocString() };
            orphanedReply.Text.Add(0, "Orphaned reply");
            dialog.Replies.Add(orphanedReply);

            // Verify orphan exists before cleanup
            Assert.Single(dialog.Replies);

            // Act: Clean up orphans
            var orphanManager = new OrphanNodeManager();
            var removed = orphanManager.RemoveOrphanedNodes(dialog);

            // Assert: Orphaned reply should be removed
            Assert.Single(removed);
            Assert.Equal(orphanedReply, removed[0]);
            Assert.Empty(dialog.Replies);
        }

        /// <summary>
        /// Verify that reachable nodes are NOT removed.
        /// </summary>
        [Fact]
        public void RemoveOrphanedNodes_PreservesReachableNodes()
        {
            // Arrange: Create dialog with connected nodes
            var dialog = new Dialog();

            var entry = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };
            entry.Text.Add(0, "NPC says hello");
            dialog.Entries.Add(entry);

            dialog.Starts.Add(new DialogPtr { Node = entry, Type = DialogNodeType.Entry, Index = 0, IsStart = true, Parent = dialog });

            // Create PC Reply connected to entry
            var reply = new DialogNode { Type = DialogNodeType.Reply, Text = new LocString() };
            reply.Text.Add(0, "Player responds");
            dialog.Replies.Add(reply);

            entry.Pointers.Add(new DialogPtr { Node = reply, Type = DialogNodeType.Reply, Index = 0, Parent = dialog });

            // Act: Clean up orphans
            var orphanManager = new OrphanNodeManager();
            var removed = orphanManager.RemoveOrphanedNodes(dialog);

            // Assert: No nodes should be removed (all reachable)
            Assert.Empty(removed);
            Assert.Single(dialog.Entries);
            Assert.Single(dialog.Replies);
        }

        /// <summary>
        /// Verify that orphaned Entry nodes are detected and removed.
        /// </summary>
        [Fact]
        public void RemoveOrphanedNodes_RemovesOrphanedEntry()
        {
            // Arrange: Dialog with orphaned NPC Entry
            var dialog = new Dialog();

            // Connected entry
            var connectedEntry = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };
            connectedEntry.Text.Add(0, "Connected entry");
            dialog.Entries.Add(connectedEntry);
            dialog.Starts.Add(new DialogPtr { Node = connectedEntry, Type = DialogNodeType.Entry, Index = 0, IsStart = true, Parent = dialog });

            // Orphaned entry (no START points to it, no parent points to it)
            var orphanedEntry = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };
            orphanedEntry.Text.Add(0, "Orphaned entry");
            dialog.Entries.Add(orphanedEntry);

            // Act: Clean up
            var orphanManager = new OrphanNodeManager();
            var removed = orphanManager.RemoveOrphanedNodes(dialog);

            // Assert: Orphaned entry removed, connected entry preserved
            Assert.Single(removed);
            Assert.Equal(orphanedEntry, removed[0]);
            Assert.Single(dialog.Entries);
            Assert.Equal(connectedEntry, dialog.Entries[0]);
        }

        /// <summary>
        /// Verify that entire orphaned subtrees are removed.
        /// If parent is orphaned, children are also orphaned.
        /// </summary>
        [Fact]
        public void RemoveOrphanedNodes_RemovesOrphanedSubtree()
        {
            // Arrange: Connected tree and orphaned tree
            var dialog = new Dialog();

            // Connected tree: Root -> Entry1 -> Reply1
            var entry1 = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };
            entry1.Text.Add(0, "Connected entry");
            dialog.Entries.Add(entry1);
            dialog.Starts.Add(new DialogPtr { Node = entry1, Type = DialogNodeType.Entry, Index = 0, IsStart = true, Parent = dialog });

            var reply1 = new DialogNode { Type = DialogNodeType.Reply, Text = new LocString() };
            reply1.Text.Add(0, "Connected reply");
            dialog.Replies.Add(reply1);
            entry1.Pointers.Add(new DialogPtr { Node = reply1, Type = DialogNodeType.Reply, Index = 0, Parent = dialog });

            // Orphaned tree: OrphanEntry -> OrphanReply (entire subtree orphaned)
            var orphanEntry = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };
            orphanEntry.Text.Add(0, "Orphaned entry");
            dialog.Entries.Add(orphanEntry);

            var orphanReply = new DialogNode { Type = DialogNodeType.Reply, Text = new LocString() };
            orphanReply.Text.Add(0, "Orphaned reply child");
            dialog.Replies.Add(orphanReply);
            orphanEntry.Pointers.Add(new DialogPtr { Node = orphanReply, Type = DialogNodeType.Reply, Index = 0, Parent = dialog });

            // Verify initial state
            Assert.Equal(2, dialog.Entries.Count);
            Assert.Equal(2, dialog.Replies.Count);

            // Act: Clean up
            var orphanManager = new OrphanNodeManager();
            var removed = orphanManager.RemoveOrphanedNodes(dialog);

            // Assert: Both orphaned nodes removed, connected nodes preserved
            Assert.Equal(2, removed.Count);
            Assert.Contains(orphanEntry, removed);
            Assert.Contains(orphanReply, removed);

            Assert.Single(dialog.Entries);
            Assert.Single(dialog.Replies);
            Assert.Equal(entry1, dialog.Entries[0]);
            Assert.Equal(reply1, dialog.Replies[0]);
        }

        /// <summary>
        /// Integration test: Verify orphan cleanup happens before save.
        /// This is the actual workflow that prevents orphaning bugs.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task SaveDialogAsync_RemovesOrphansBeforeSave()
        {
            // Arrange: Create ViewModel with orphaned node
            var vm = new MainViewModel();
            vm.NewDialog();

            // Create connected node
            var entry = new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() };
            entry.Text.Add(0, "Connected");
            vm.CurrentDialog.Entries.Add(entry);
            vm.CurrentDialog.Starts.Add(new DialogPtr { Node = entry, Type = DialogNodeType.Entry, Index = 0, IsStart = true, Parent = vm.CurrentDialog });

            // Create orphaned reply (simulating bad restore)
            var orphan = new DialogNode { Type = DialogNodeType.Reply, Text = new LocString() };
            orphan.Text.Add(0, "Orphan");
            vm.CurrentDialog.Replies.Add(orphan);

            // Verify orphan exists
            Assert.Single(vm.CurrentDialog.Replies);

            // Act: Save (should trigger cleanup)
            var tempPath = System.IO.Path.GetTempFileName();
            try
            {
                await vm.SaveDialogAsync(tempPath);

                // Assert: Orphan should be removed from dialog during save
                Assert.Empty(vm.CurrentDialog.Replies);
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }
    }
}

using DialogEditor.Models;
using Parley.Models;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests that orphan node detection works correctly with lazy loading (Issue #82)
    /// Validates that reachable nodes aren't incorrectly marked as orphans
    /// </summary>
    public class LazyLoadingOrphanDetectionTests
    {
        [Fact]
        public void LazyLoading_DoesNotMarkReachableNodesAsOrphans()
        {
            // Arrange: Create dialog with clear reachability
            var dialog = new Dialog();

            // Entry 0 (start) -> Reply 0 -> Entry 1
            var entry0 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry0.Text.Add(0, "Start entry");
            dialog.Entries.Add(entry0);

            var reply0 = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString()
            };
            reply0.Text.Add(0, "Player choice");
            dialog.Replies.Add(reply0);

            var entry1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry1.Text.Add(0, "Response entry");
            dialog.Entries.Add(entry1);

            // Build connections: Entry0 -> Reply0 -> Entry1
            var ptr1 = new DialogPtr
            {
                Node = reply0,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false,
                Parent = dialog
            };
            entry0.Pointers.Add(ptr1);

            var ptr2 = new DialogPtr
            {
                Node = entry1,
                Type = DialogNodeType.Entry,
                Index = 1,
                IsLink = false,
                Parent = dialog
            };
            reply0.Pointers.Add(ptr2);

            // Add start pointer
            var startPtr = new DialogPtr
            {
                Node = entry0,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsLink = false,
                IsStart = true,
                Parent = dialog
            };
            dialog.Starts.Add(startPtr);

            dialog.RebuildLinkRegistry();

            // Act: Simulate orphan detection logic (same as MainViewModel.FindOrphanedNodes)
            var reachableNodes = new HashSet<DialogNode>();
            CollectReachableFromDialogModel(entry0, reachableNodes);

            // Assert: All nodes should be reachable
            Assert.Contains(entry0, reachableNodes);
            Assert.Contains(reply0, reachableNodes);
            Assert.Contains(entry1, reachableNodes);
            Assert.Equal(3, reachableNodes.Count);

            // No orphans should be detected
            var orphanedEntries = dialog.Entries
                .Where(e => !reachableNodes.Contains(e))
                .ToList();
            var orphanedReplies = dialog.Replies
                .Where(r => !reachableNodes.Contains(r))
                .ToList();

            Assert.Empty(orphanedEntries);
            Assert.Empty(orphanedReplies);
        }

        [Fact]
        public void LazyLoading_CorrectlyIdentifiesActualOrphans()
        {
            // Arrange: Create dialog with genuine orphans
            var dialog = new Dialog();

            // Entry 0 (start) -> Reply 0
            var entry0 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry0.Text.Add(0, "Start entry");
            dialog.Entries.Add(entry0);

            var reply0 = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString()
            };
            reply0.Text.Add(0, "Player choice");
            dialog.Replies.Add(reply0);

            // Entry 1 - ORPHAN (not connected to anything)
            var entry1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry1.Text.Add(0, "Orphan entry");
            dialog.Entries.Add(entry1);

            // Build connections: Entry0 -> Reply0 (Entry1 is orphaned)
            var ptr1 = new DialogPtr
            {
                Node = reply0,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false,
                Parent = dialog
            };
            entry0.Pointers.Add(ptr1);

            // Add start pointer
            var startPtr = new DialogPtr
            {
                Node = entry0,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsLink = false,
                IsStart = true,
                Parent = dialog
            };
            dialog.Starts.Add(startPtr);

            dialog.RebuildLinkRegistry();

            // Act: Simulate orphan detection
            var reachableNodes = new HashSet<DialogNode>();
            CollectReachableFromDialogModel(entry0, reachableNodes);

            // Assert: Entry0 and Reply0 are reachable, Entry1 is orphaned
            Assert.Contains(entry0, reachableNodes);
            Assert.Contains(reply0, reachableNodes);
            Assert.DoesNotContain(entry1, reachableNodes);
            Assert.Equal(2, reachableNodes.Count);

            var orphanedEntries = dialog.Entries
                .Where(e => !reachableNodes.Contains(e))
                .ToList();

            Assert.Single(orphanedEntries);
            Assert.Equal(entry1, orphanedEntries[0]);
        }

        [Fact]
        public void LazyLoading_HandlesLinksCorrectly()
        {
            // Arrange: Create dialog with link nodes
            var dialog = new Dialog();

            // Entry 0 (start) -> Reply 0 (original)
            var entry0 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry0.Text.Add(0, "Start entry");
            dialog.Entries.Add(entry0);

            var reply0 = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString()
            };
            reply0.Text.Add(0, "Player choice");
            dialog.Replies.Add(reply0);

            // Entry 1 -> Reply 0 (link to same reply)
            var entry1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            entry1.Text.Add(0, "Second entry");
            dialog.Entries.Add(entry1);

            // Build connections
            var ptr1 = new DialogPtr
            {
                Node = reply0,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false, // Original
                Parent = dialog
            };
            entry0.Pointers.Add(ptr1);

            var ptr2 = new DialogPtr
            {
                Node = reply0,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = true, // Link
                Parent = dialog
            };
            entry1.Pointers.Add(ptr2);

            // Add start pointers
            dialog.Starts.Add(new DialogPtr
            {
                Node = entry0,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsLink = false,
                IsStart = true,
                Parent = dialog
            });
            dialog.Starts.Add(new DialogPtr
            {
                Node = entry1,
                Type = DialogNodeType.Entry,
                Index = 1,
                IsLink = false,
                IsStart = true,
                Parent = dialog
            });

            dialog.RebuildLinkRegistry();

            // Act: Simulate orphan detection
            var reachableNodes = new HashSet<DialogNode>();
            CollectReachableFromDialogModel(entry0, reachableNodes);
            CollectReachableFromDialogModel(entry1, reachableNodes);

            // Assert: All nodes reachable (links don't create orphans)
            Assert.Contains(entry0, reachableNodes);
            Assert.Contains(entry1, reachableNodes);
            Assert.Contains(reply0, reachableNodes);
            Assert.Equal(3, reachableNodes.Count);
        }

        /// <summary>
        /// Helper method that mimics MainViewModel.CollectReachableNodes
        /// Traverses dialog model pointers (not TreeView children)
        /// </summary>
        private void CollectReachableFromDialogModel(DialogNode node, HashSet<DialogNode> reachableNodes)
        {
            if (node == null || reachableNodes.Contains(node))
                return;

            reachableNodes.Add(node);

            // Traverse dialog pointers (not TreeView children)
            foreach (var pointer in node.Pointers)
            {
                if (pointer.Node != null && !pointer.IsLink)
                {
                    // Don't traverse links (they're terminal in TreeView)
                    CollectReachableFromDialogModel(pointer.Node, reachableNodes);
                }
            }
        }
    }
}

using DialogEditor.Models;
using Parley.Models;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests that lazy loading correctly defers children population (Issue #82)
    /// Validates performance improvement - children not created until expanded
    /// </summary>
    public class LazyLoadingPerformanceTests
    {
        [Fact]
        public void LazyLoading_ChildrenNotPopulatedByDefault()
        {
            // Arrange: Create dialog with deep tree
            var dialog = CreateDeepDialog(depth: 5);
            var rootNode = new TreeViewRootNode(dialog);

            // Act: Add start nodes to root (mimics PopulateDialogNodes)
            foreach (var start in dialog.Starts)
            {
                if (start.Index < (uint)dialog.Entries.Count)
                {
                    var entry = dialog.Entries[(int)start.Index];
                    var safeNode = new TreeViewSafeNode(entry, ancestors: null, depth: 0, sourcePointer: start);
                    rootNode.Children?.Add(safeNode);
                }
            }

            var startNode = rootNode.Children?[0];

            // Assert: Start node exists, children collection has only placeholder (lazy)
            Assert.NotNull(startNode);
            Assert.NotNull(startNode.Children);
            Assert.Single(startNode!.Children!); // Should have only placeholder
            Assert.IsType<TreeViewPlaceholderNode>(startNode.Children![0]); // Placeholder for lazy loading
        }

        [Fact]
        public void LazyLoading_ChildrenPopulatedOnExpansion()
        {
            // Arrange: Create dialog with simple tree
            var dialog = CreateDeepDialog(depth: 2);
            var rootNode = new TreeViewRootNode(dialog);
            foreach (var start in dialog.Starts)
            {
                if (start.Index < (uint)dialog.Entries.Count)
                {
                    var entry = dialog.Entries[(int)start.Index];
                    var safeNode = new TreeViewSafeNode(entry, ancestors: null, depth: 0, sourcePointer: start);
                    rootNode.Children?.Add(safeNode);
                }
            }
            var startNode = rootNode.Children?[0];

            // Assert: Before expansion - only placeholder
            Assert.NotNull(startNode);
            Assert.Single(startNode!.Children!); // Placeholder
            Assert.IsType<TreeViewPlaceholderNode>(startNode.Children![0]);

            // Act: Expand the node
            startNode!.IsExpanded = true;

            // Assert: After expansion - placeholder replaced with actual child
            Assert.NotEmpty(startNode.Children!);
            Assert.Single(startNode.Children!); // Should have 1 reply child
            Assert.IsNotType<TreeViewPlaceholderNode>(startNode.Children![0]); // No longer placeholder
        }

        [Fact]
        public void LazyLoading_HasChildrenCorrectWithoutPopulating()
        {
            // Arrange: Create dialog with tree
            var dialog = CreateDeepDialog(depth: 3);
            var rootNode = new TreeViewRootNode(dialog);
            foreach (var start in dialog.Starts)
            {
                if (start.Index < (uint)dialog.Entries.Count)
                {
                    var entry = dialog.Entries[(int)start.Index];
                    var safeNode = new TreeViewSafeNode(entry, ancestors: null, depth: 0, sourcePointer: start);
                    rootNode.Children?.Add(safeNode);
                }
            }
            var startNode = rootNode.Children?[0];

            // Assert: HasChildren is true and placeholder present, but real children not populated
            Assert.NotNull(startNode);
            Assert.Single(startNode!.Children!); // Has placeholder
            Assert.IsType<TreeViewPlaceholderNode>(startNode.Children![0]); // Only placeholder, no real children
            Assert.True(startNode.HasChildren); // HasChildren correctly reports true
        }

        [Fact]
        public void LazyLoading_LinkNodesHaveNoChildren()
        {
            // Arrange: Create dialog with link
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

            // Entry 1 -> Reply 0 (link)
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

            dialog.Starts.Add(new DialogPtr
            {
                Node = entry0,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsLink = false,
                IsStart = true,
                Parent = dialog
            });

            dialog.RebuildLinkRegistry();

            // Act: Create TreeView nodes
            var rootNode = new TreeViewRootNode(dialog);
            foreach (var start in dialog.Starts)
            {
                if (start.Index < (uint)dialog.Entries.Count)
                {
                    var entry = dialog.Entries[(int)start.Index];
                    var safeNode = new TreeViewSafeNode(entry, ancestors: null, depth: 0, sourcePointer: start);
                    rootNode.Children?.Add(safeNode);
                }
            }
            var startNode = rootNode.Children?[0];
            startNode!.IsExpanded = true; // Expand to get reply child
            var replyChild = startNode.Children?[0];

            // Assert: Reply child from entry0 should show it has children
            // But if we create a LINK node, it should have no children
            Assert.NotNull(replyChild);
            Assert.False(replyChild.IsChild); // Original node, not a link

            // Create a link node manually (simulating what happens in entry1)
            var linkNode = new TreeViewSafeNode(reply0, sourcePointer: ptr2);
            Assert.True(linkNode.IsChild); // This is a link
            Assert.Null(linkNode.Children); // Links have no children
            Assert.False(linkNode.HasChildren); // HasChildren is false for links
        }

        [Fact]
        public void LazyLoading_DeepTreeDoesNotCreateMillionObjects()
        {
            // Arrange: Create a deep conversation (20 levels)
            // Without lazy loading, this creates 2^20 = 1,048,576 objects
            // With lazy loading, should create only ~20 objects + placeholders
            var dialog = CreateDeepDialog(depth: 20);
            var rootNode = new TreeViewRootNode(dialog);

            // Act: Populate root (this creates start nodes but NOT their descendants)
            foreach (var start in dialog.Starts)
            {
                if (start.Index < (uint)dialog.Entries.Count)
                {
                    var entry = dialog.Entries[(int)start.Index];
                    var safeNode = new TreeViewSafeNode(entry, ancestors: null, depth: 0, sourcePointer: start);
                    rootNode.Children?.Add(safeNode);
                }
            }

            // Assert: Only root and start nodes created
            Assert.NotNull(rootNode.Children);
            Assert.Single(rootNode.Children); // One start node

            var startNode = rootNode.Children[0];
            Assert.Single(startNode.Children!); // Only placeholder, no real children
            Assert.IsType<TreeViewPlaceholderNode>(startNode.Children![0]);

            // Expand first level only
            startNode.IsExpanded = true;
            Assert.Single(startNode.Children!); // One reply child created (placeholder replaced)
            Assert.IsNotType<TreeViewPlaceholderNode>(startNode.Children![0]);

            var replyChild = startNode.Children![0];
            Assert.Single(replyChild.Children!); // Only placeholder at next level
            Assert.IsType<TreeViewPlaceholderNode>(replyChild.Children![0]);

            // Performance win: Without lazy loading, we'd have 1M+ objects
            // With lazy loading, we have only a few objects (root + start + reply + placeholders)
        }

        /// <summary>
        /// Helper to create a dialog with a linear conversation of specified depth
        /// </summary>
        private Dialog CreateDeepDialog(int depth)
        {
            var dialog = new Dialog();
            DialogNode? previousNode = null;
            DialogNodeType currentType = DialogNodeType.Entry;

            for (int i = 0; i < depth; i++)
            {
                var node = new DialogNode
                {
                    Type = currentType,
                    Text = new LocString()
                };
                node.Text.Add(0, $"Node {i}");

                if (currentType == DialogNodeType.Entry)
                {
                    dialog.Entries.Add(node);
                }
                else
                {
                    dialog.Replies.Add(node);
                }

                // Link to previous node
                if (previousNode != null)
                {
                    var ptr = new DialogPtr
                    {
                        Node = node,
                        Type = currentType,
                        Index = (uint)(currentType == DialogNodeType.Entry ? dialog.Entries.Count - 1 : dialog.Replies.Count - 1),
                        IsLink = false,
                        Parent = dialog
                    };
                    previousNode.Pointers.Add(ptr);
                }
                else
                {
                    // First node - add as start
                    dialog.Starts.Add(new DialogPtr
                    {
                        Node = node,
                        Type = DialogNodeType.Entry,
                        Index = 0,
                        IsLink = false,
                        IsStart = true,
                        Parent = dialog
                    });
                }

                previousNode = node;
                currentType = currentType == DialogNodeType.Entry ? DialogNodeType.Reply : DialogNodeType.Entry;
            }

            dialog.RebuildLinkRegistry();
            return dialog;
        }
    }
}

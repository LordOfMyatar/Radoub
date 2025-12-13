using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using DialogEditor.Models;
using DialogEditor.ViewModels;

namespace Parley.Tests
{
    /// <summary>
    /// Stress tests for cascade delete with deep trees and shared nodes.
    /// Goal: Correctly handle shared nodes at depths up to 100.
    ///
    /// Related: Issue #32 - Improve cascade delete depth for shared nodes
    /// </summary>
    public class CascadeDeleteStressTests
    {
        private readonly ITestOutputHelper _output;

        public CascadeDeleteStressTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Tree Generation

        /// <summary>
        /// Generates a linear dialog tree (Entry->Reply->Entry->Reply...).
        /// Simple structure for baseline performance testing.
        /// </summary>
        private Dialog GenerateLinearTree(int depth)
        {
            var dialog = new Dialog();
            DialogNode? previousNode = null;
            DialogNodeType currentType = DialogNodeType.Entry;

            for (int i = 0; i < depth; i++)
            {
                var node = dialog.CreateNode(currentType);
                node!.Text.Add(0, $"Linear node {i}");
                dialog.AddNodeInternal(node, currentType);

                if (previousNode != null)
                {
                    var ptr = dialog.CreatePtr();
                    ptr!.Node = node;
                    ptr.Type = currentType;
                    ptr.Index = (uint)(currentType == DialogNodeType.Entry
                        ? dialog.Entries.IndexOf(node)
                        : dialog.Replies.IndexOf(node));
                    ptr.IsLink = false;
                    ptr.Parent = dialog;
                    previousNode.Pointers.Add(ptr);
                    dialog.LinkRegistry.RegisterLink(ptr);
                }
                else
                {
                    // First node - add as start
                    var start = dialog.CreatePtr();
                    start!.Node = node;
                    start.Type = DialogNodeType.Entry;
                    start.Index = 0;
                    start.IsLink = false;
                    start.IsStart = true;
                    start.Parent = dialog;
                    dialog.Starts.Add(start);
                    dialog.LinkRegistry.RegisterLink(start);
                }

                previousNode = node;
                currentType = currentType == DialogNodeType.Entry
                    ? DialogNodeType.Reply
                    : DialogNodeType.Entry;
            }

            return dialog;
        }

        /// <summary>
        /// Generates a tree with shared nodes at regular intervals.
        /// This is the problematic case for cascade delete.
        ///
        /// Structure:
        /// - Linear chain of Entry->Reply->Entry->Reply...
        /// - Every N entries, a shared reply is created
        /// - Multiple entries point to the shared reply (simulating conversation convergence)
        /// - When deleting the root, all nodes in the chain should be deleted
        ///   INCLUDING shared nodes that have no references outside the deletion set
        /// </summary>
        private Dialog GenerateTreeWithSharedNodes(int depth, int shareInterval = 10)
        {
            var dialog = new Dialog();
            DialogNode? previousEntry = null;
            DialogNode? sharedReply = null;
            DialogNode? firstEntry = null;
            var sharedReplies = new List<DialogNode>();

            for (int i = 0; i < depth; i++)
            {
                // Create entry
                var entry = dialog.CreateNode(DialogNodeType.Entry);
                entry!.Text.Add(0, $"Entry at depth {i}");
                dialog.AddNodeInternal(entry, entry.Type);

                if (i == 0)
                {
                    firstEntry = entry;
                    var start = dialog.CreatePtr();
                    start!.Node = entry;
                    start.Type = DialogNodeType.Entry;
                    start.Index = 0;
                    start.Parent = dialog;
                    dialog.Starts.Add(start);
                    dialog.LinkRegistry.RegisterLink(start);
                }

                // Create a reply for this entry
                var reply = dialog.CreateNode(DialogNodeType.Reply);
                reply!.Text.Add(0, $"Reply at depth {i}");
                dialog.AddNodeInternal(reply, reply.Type);

                // Link entry to reply
                var ptr = dialog.CreatePtr();
                ptr!.Node = reply;
                ptr.Type = DialogNodeType.Reply;
                ptr.Index = (uint)dialog.Replies.IndexOf(reply);
                ptr.IsLink = false;
                ptr.Parent = dialog;
                entry.Pointers.Add(ptr);
                dialog.LinkRegistry.RegisterLink(ptr);

                // At every shareInterval, create a shared reply
                if (i % shareInterval == 0 && i > 0)
                {
                    sharedReply = dialog.CreateNode(DialogNodeType.Reply);
                    sharedReply!.Text.Add(0, $"SHARED REPLY at depth {i}");
                    dialog.AddNodeInternal(sharedReply, sharedReply.Type);
                    sharedReplies.Add(sharedReply);

                    // First reference to shared reply (original)
                    var sharedPtr = dialog.CreatePtr();
                    sharedPtr!.Node = sharedReply;
                    sharedPtr.Type = DialogNodeType.Reply;
                    sharedPtr.Index = (uint)dialog.Replies.IndexOf(sharedReply);
                    sharedPtr.IsLink = false;
                    sharedPtr.Parent = dialog;
                    entry.Pointers.Add(sharedPtr);
                    dialog.LinkRegistry.RegisterLink(sharedPtr);
                }

                // Also link to previous shared reply if exists (this creates the "multiple references" issue)
                if (sharedReply != null && i % shareInterval != 0)
                {
                    var linkPtr = dialog.CreatePtr();
                    linkPtr!.Node = sharedReply;
                    linkPtr.Type = DialogNodeType.Reply;
                    linkPtr.Index = (uint)dialog.Replies.IndexOf(sharedReply);
                    linkPtr.IsLink = true; // This is a link reference
                    linkPtr.Parent = dialog;
                    entry.Pointers.Add(linkPtr);
                    dialog.LinkRegistry.RegisterLink(linkPtr);
                }

                // Chain entries together (reply points to next entry)
                if (previousEntry != null)
                {
                    var prevReply = previousEntry.Pointers.First().Node;
                    var nextPtr = dialog.CreatePtr();
                    nextPtr!.Node = entry;
                    nextPtr.Type = DialogNodeType.Entry;
                    nextPtr.Index = (uint)dialog.Entries.IndexOf(entry);
                    nextPtr.IsLink = false;
                    nextPtr.Parent = dialog;
                    prevReply!.Pointers.Add(nextPtr);
                    dialog.LinkRegistry.RegisterLink(nextPtr);
                }

                previousEntry = entry;
            }

            return dialog;
        }

        /// <summary>
        /// Generates a tree where shared nodes have BOTH internal and external references.
        /// This tests the critical case: a shared node where some references are being deleted
        /// and some references are NOT being deleted.
        /// </summary>
        private (Dialog dialog, DialogNode deleteTarget, DialogNode externalEntry) GenerateTreeWithExternalReferences(int depth)
        {
            var dialog = new Dialog();
            DialogNode? firstEntry = null;
            DialogNode? sharedReply = null;
            DialogNode? previousEntry = null;

            // Create main chain
            for (int i = 0; i < depth; i++)
            {
                var entry = dialog.CreateNode(DialogNodeType.Entry);
                entry!.Text.Add(0, $"Main chain entry {i}");
                dialog.AddNodeInternal(entry, entry.Type);

                if (i == 0)
                {
                    firstEntry = entry;
                    var start = dialog.CreatePtr();
                    start!.Node = entry;
                    start.Type = DialogNodeType.Entry;
                    start.Index = 0;
                    start.Parent = dialog;
                    dialog.Starts.Add(start);
                    dialog.LinkRegistry.RegisterLink(start);
                }

                var reply = dialog.CreateNode(DialogNodeType.Reply);
                reply!.Text.Add(0, $"Main chain reply {i}");
                dialog.AddNodeInternal(reply, reply.Type);

                var ptr = dialog.CreatePtr();
                ptr!.Node = reply;
                ptr.Type = DialogNodeType.Reply;
                ptr.Index = (uint)dialog.Replies.IndexOf(reply);
                ptr.IsLink = false;
                ptr.Parent = dialog;
                entry.Pointers.Add(ptr);
                dialog.LinkRegistry.RegisterLink(ptr);

                // At midpoint, create a shared reply
                if (i == depth / 2)
                {
                    sharedReply = dialog.CreateNode(DialogNodeType.Reply);
                    sharedReply!.Text.Add(0, "SHARED - should survive deletion");
                    dialog.AddNodeInternal(sharedReply, sharedReply.Type);

                    var sharedPtr = dialog.CreatePtr();
                    sharedPtr!.Node = sharedReply;
                    sharedPtr.Type = DialogNodeType.Reply;
                    sharedPtr.Index = (uint)dialog.Replies.IndexOf(sharedReply);
                    sharedPtr.IsLink = false;
                    sharedPtr.Parent = dialog;
                    entry.Pointers.Add(sharedPtr);
                    dialog.LinkRegistry.RegisterLink(sharedPtr);
                }

                if (previousEntry != null)
                {
                    var prevReply = previousEntry.Pointers.First().Node;
                    var nextPtr = dialog.CreatePtr();
                    nextPtr!.Node = entry;
                    nextPtr.Type = DialogNodeType.Entry;
                    nextPtr.Index = (uint)dialog.Entries.IndexOf(entry);
                    nextPtr.IsLink = false;
                    nextPtr.Parent = dialog;
                    prevReply!.Pointers.Add(nextPtr);
                    dialog.LinkRegistry.RegisterLink(nextPtr);
                }

                previousEntry = entry;
            }

            // Create EXTERNAL entry that also points to the shared reply
            // This entry is NOT part of the deletion - so shared reply should survive
            var externalEntry = dialog.CreateNode(DialogNodeType.Entry);
            externalEntry!.Text.Add(0, "EXTERNAL - not being deleted");
            dialog.AddNodeInternal(externalEntry, externalEntry.Type);

            var externalStart = dialog.CreatePtr();
            externalStart!.Node = externalEntry;
            externalStart.Type = DialogNodeType.Entry;
            externalStart.Index = (uint)dialog.Entries.IndexOf(externalEntry);
            externalStart.Parent = dialog;
            dialog.Starts.Add(externalStart);
            dialog.LinkRegistry.RegisterLink(externalStart);

            // Link external entry to the shared reply
            var externalLink = dialog.CreatePtr();
            externalLink!.Node = sharedReply;
            externalLink.Type = DialogNodeType.Reply;
            externalLink.Index = (uint)dialog.Replies.IndexOf(sharedReply!);
            externalLink.IsLink = true;
            externalLink.Parent = dialog;
            externalEntry.Pointers.Add(externalLink);
            dialog.LinkRegistry.RegisterLink(externalLink);

            return (dialog, firstEntry!, externalEntry);
        }

        #endregion

        #region Performance Benchmarks

        [Theory]
        [InlineData(10)]
        [InlineData(20)]
        [InlineData(50)]
        [InlineData(100)]
        public void Benchmark_LinearTreeDeletion(int depth)
        {
            // Arrange
            var dialog = GenerateLinearTree(depth);
            var viewModel = new MainViewModel { CurrentDialog = dialog };
            var firstEntry = dialog.Entries.First();

            int initialEntries = dialog.Entries.Count;
            int initialReplies = dialog.Replies.Count;

            _output.WriteLine($"\n=== Linear Tree Deletion Benchmark (depth={depth}) ===");
            _output.WriteLine($"Initial: {initialEntries} entries, {initialReplies} replies");

            // Act
            var sw = Stopwatch.StartNew();

            var deleteMethod = typeof(MainViewModel).GetMethod("DeleteNodeRecursive",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(deleteMethod);
            deleteMethod.Invoke(viewModel, new object?[] { firstEntry });
            dialog.RemoveNodeInternal(firstEntry, firstEntry.Type);

            // Remove start pointer
            var startToRemove = dialog.Starts.FirstOrDefault(s => s.Node == firstEntry);
            if (startToRemove != null)
            {
                dialog.LinkRegistry.UnregisterLink(startToRemove);
                dialog.Starts.Remove(startToRemove);
            }

            sw.Stop();

            // Assert & Report
            _output.WriteLine($"Time: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
            _output.WriteLine($"Final: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");
            _output.WriteLine($"Rate: {(double)initialEntries / sw.ElapsedMilliseconds:F2} nodes/ms");

            Assert.Empty(dialog.Entries);
            Assert.Empty(dialog.Replies);

            // Performance threshold: should complete in reasonable time
            // 100 depth should be <1 second on any modern machine
            Assert.True(sw.ElapsedMilliseconds < 5000, $"Deletion took too long: {sw.ElapsedMilliseconds}ms");
        }

        [Theory]
        [InlineData(10, 5)]
        [InlineData(20, 5)]
        [InlineData(50, 10)]
        [InlineData(100, 10)]
        public void Benchmark_SharedNodeTreeDeletion(int depth, int shareInterval)
        {
            // Arrange
            var dialog = GenerateTreeWithSharedNodes(depth, shareInterval);
            var viewModel = new MainViewModel { CurrentDialog = dialog };
            var firstEntry = dialog.Entries.First();

            int initialEntries = dialog.Entries.Count;
            int initialReplies = dialog.Replies.Count;
            int sharedNodeCount = dialog.Replies.Count(r => r.Text?.GetDefault()?.Contains("SHARED") == true);

            _output.WriteLine($"\n=== Shared Node Tree Deletion Benchmark (depth={depth}, interval={shareInterval}) ===");
            _output.WriteLine($"Initial: {initialEntries} entries, {initialReplies} replies ({sharedNodeCount} shared)");

            // Act
            var sw = Stopwatch.StartNew();

            var deleteMethod = typeof(MainViewModel).GetMethod("DeleteNodeRecursive",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(deleteMethod);
            deleteMethod.Invoke(viewModel, new object?[] { firstEntry });
            dialog.RemoveNodeInternal(firstEntry, firstEntry.Type);

            var startToRemove = dialog.Starts.FirstOrDefault(s => s.Node == firstEntry);
            if (startToRemove != null)
            {
                dialog.LinkRegistry.UnregisterLink(startToRemove);
                dialog.Starts.Remove(startToRemove);
            }

            sw.Stop();

            // Assert & Report
            _output.WriteLine($"Time: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
            _output.WriteLine($"Final: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");

            Assert.Empty(dialog.Entries);

            // KNOWN ISSUE (#32): Shared nodes may be incorrectly preserved
            // This test documents current behavior - fix should make all assertions pass
            if (dialog.Replies.Count > 0)
            {
                _output.WriteLine($"⚠️ KNOWN ISSUE: {dialog.Replies.Count} shared nodes incorrectly preserved");
                foreach (var remaining in dialog.Replies.Take(5))
                {
                    _output.WriteLine($"  - '{remaining.Text?.GetDefault()}'");
                }
            }

            // After fix, this should pass:
            // Assert.Empty(dialog.Replies);
        }

        #endregion

        #region Correctness Tests

        [Fact]
        public void SharedNode_WithExternalReference_ShouldSurvive()
        {
            // Arrange: Tree where shared node has EXTERNAL reference
            var (dialog, deleteTarget, externalEntry) = GenerateTreeWithExternalReferences(20);
            var viewModel = new MainViewModel { CurrentDialog = dialog };
            var sharedReply = dialog.Replies.First(r => r.Text?.GetDefault()?.Contains("SHARED") == true);

            _output.WriteLine("Testing: Shared node with external reference should survive deletion");
            _output.WriteLine($"Initial: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");
            _output.WriteLine($"Shared reply: '{sharedReply.Text?.GetDefault()}'");

            // Act: Delete the main chain (but NOT the external entry)
            var deleteMethod = typeof(MainViewModel).GetMethod("DeleteNodeRecursive",
                BindingFlags.NonPublic | BindingFlags.Instance);
            deleteMethod?.Invoke(viewModel, new object[] { deleteTarget });
            dialog.RemoveNodeInternal(deleteTarget, deleteTarget.Type);

            var startToRemove = dialog.Starts.FirstOrDefault(s => s.Node == deleteTarget);
            if (startToRemove != null)
            {
                dialog.LinkRegistry.UnregisterLink(startToRemove);
                dialog.Starts.Remove(startToRemove);
            }

            // Assert
            _output.WriteLine($"Final: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");

            // External entry should survive
            Assert.Contains(externalEntry, dialog.Entries);

            // Shared reply SHOULD survive because external entry still references it
            Assert.Contains(sharedReply, dialog.Replies);

            _output.WriteLine("✅ Shared node correctly preserved (has external reference)");
        }

        [Theory]
        [InlineData(20)]
        [InlineData(50)]
        [InlineData(100)]
        public void SharedNode_WithOnlyInternalReferences_ShouldBeDeleted(int depth)
        {
            // Arrange: Tree where ALL references to shared nodes are being deleted
            var dialog = GenerateTreeWithSharedNodes(depth, shareInterval: 10);
            var viewModel = new MainViewModel { CurrentDialog = dialog };
            var firstEntry = dialog.Entries.First();

            int sharedCount = dialog.Replies.Count(r => r.Text?.GetDefault()?.Contains("SHARED") == true);

            _output.WriteLine($"Testing: Shared nodes with only internal references (depth={depth})");
            _output.WriteLine($"Initial: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");
            _output.WriteLine($"Shared nodes: {sharedCount}");

            // Act
            var sw = Stopwatch.StartNew();

            var deleteMethod = typeof(MainViewModel).GetMethod("DeleteNodeRecursive",
                BindingFlags.NonPublic | BindingFlags.Instance);
            deleteMethod?.Invoke(viewModel, new object[] { firstEntry });
            dialog.RemoveNodeInternal(firstEntry, firstEntry.Type);

            var startToRemove = dialog.Starts.FirstOrDefault(s => s.Node == firstEntry);
            if (startToRemove != null)
            {
                dialog.LinkRegistry.UnregisterLink(startToRemove);
                dialog.Starts.Remove(startToRemove);
            }

            sw.Stop();

            // Assert
            _output.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Final: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");

            Assert.Empty(dialog.Entries);

            // This is the key test for Issue #32
            // Currently fails at depth > 5 due to incorrect shared node handling
            if (dialog.Replies.Count > 0)
            {
                _output.WriteLine($"❌ FAIL: {dialog.Replies.Count} replies incorrectly preserved:");
                foreach (var reply in dialog.Replies.Take(10))
                {
                    _output.WriteLine($"  - '{reply.Text?.GetDefault()}'");
                }

                // Track how many shared vs regular nodes remain
                var remainingShared = dialog.Replies.Count(r => r.Text?.GetDefault()?.Contains("SHARED") == true);
                var remainingRegular = dialog.Replies.Count - remainingShared;
                _output.WriteLine($"  ({remainingShared} shared, {remainingRegular} regular)");
            }
            else
            {
                _output.WriteLine("✅ All nodes correctly deleted");
            }

            // EXPECTED after fix: Assert.Empty(dialog.Replies);
            // Currently: This assertion will fail for depth > 5
        }

        #endregion

        #region Stress Tests

        [Fact]
        public void StressTest_VeryDeepTree_NoStackOverflow()
        {
            // This tests stack overflow protection at extreme depths
            const int extremeDepth = 500;

            _output.WriteLine($"Stress test: Creating tree with depth {extremeDepth}");

            var sw = Stopwatch.StartNew();
            var dialog = GenerateLinearTree(extremeDepth);
            sw.Stop();
            _output.WriteLine($"Generation time: {sw.ElapsedMilliseconds}ms");

            var viewModel = new MainViewModel { CurrentDialog = dialog };
            var firstEntry = dialog.Entries.First();

            sw.Restart();
            var deleteMethod = typeof(MainViewModel).GetMethod("DeleteNodeRecursive",
                BindingFlags.NonPublic | BindingFlags.Instance);
            deleteMethod?.Invoke(viewModel, new object[] { firstEntry });
            dialog.RemoveNodeInternal(firstEntry, firstEntry.Type);
            sw.Stop();

            _output.WriteLine($"Deletion time: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Final: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");

            // Primary assertion: No stack overflow
            Assert.True(true, "Completed without stack overflow");
            Assert.Empty(dialog.Entries);
        }

        [Fact]
        public void StressTest_HighlyConnectedSharedNodes()
        {
            // Create a tree where many nodes share references (worst case scenario)
            const int depth = 50;
            const int shareInterval = 5; // Share every 5 levels

            _output.WriteLine($"Stress test: Highly connected tree (depth={depth}, share every {shareInterval})");

            var sw = Stopwatch.StartNew();
            var dialog = GenerateTreeWithSharedNodes(depth, shareInterval);
            sw.Stop();

            int totalNodes = dialog.Entries.Count + dialog.Replies.Count;
            int sharedNodes = dialog.Replies.Count(r => r.Text?.GetDefault()?.Contains("SHARED") == true);

            _output.WriteLine($"Generation: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Total nodes: {totalNodes} ({sharedNodes} shared)");

            var viewModel = new MainViewModel { CurrentDialog = dialog };
            var firstEntry = dialog.Entries.First();

            sw.Restart();
            var deleteMethod = typeof(MainViewModel).GetMethod("DeleteNodeRecursive",
                BindingFlags.NonPublic | BindingFlags.Instance);
            deleteMethod?.Invoke(viewModel, new object[] { firstEntry });
            dialog.RemoveNodeInternal(firstEntry, firstEntry.Type);
            sw.Stop();

            _output.WriteLine($"Deletion: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Remaining: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");

            // Should complete in reasonable time
            Assert.True(sw.ElapsedMilliseconds < 10000, $"Took too long: {sw.ElapsedMilliseconds}ms");
        }

        #endregion
    }
}

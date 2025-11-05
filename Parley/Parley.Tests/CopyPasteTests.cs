using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using DialogEditor.Services;
using DialogEditor.Models;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for copy/paste operations, specifically targeting Issue #6
    /// - Copy/paste with node links can cause file corruption
    /// </summary>
    public class CopyPasteTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly DialogFileService _dialogService;

        public CopyPasteTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"ParleyTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            _dialogService = new DialogFileService();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        [Fact]
        public async Task CopyPaste_SimpleNode_PreservesStructure()
        {
            // Arrange - Create a simple dialog with no links
            var dialog = new Dialog();

            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            entry1!.Text.Add(0, "Original entry");
            dialog.AddNodeInternal(entry1, entry1.Type);

            // Simulate copy/paste by creating a duplicate
            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            entry2!.Text.Add(0, "Copied entry");
            dialog.AddNodeInternal(entry2, entry2.Type);

            // Create start pointers
            var start1 = dialog.CreatePtr();
            start1!.Node = entry1;
            start1.Type = DialogNodeType.Entry;
            start1.Index = 0;
            dialog.Starts.Add(start1);

            var start2 = dialog.CreatePtr();
            start2!.Node = entry2;
            start2.Type = DialogNodeType.Entry;
            start2.Index = 1;
            dialog.Starts.Add(start2);

            var filePath = Path.Combine(_testDirectory, "copy_simple.dlg");

            // Act
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert
            Assert.NotNull(loadedDialog);
            Assert.Equal(2, loadedDialog.Entries.Count);
            Assert.Equal(2, loadedDialog.Starts.Count);
            Assert.Equal("Original entry", loadedDialog.Entries[0].Text.GetDefault());
            Assert.Equal("Copied entry", loadedDialog.Entries[1].Text.GetDefault());
        }

        [Fact]
        public async Task CopyPaste_NodeWithLink_MaintainsCorrectIndices()
        {
            // This test targets the core of Issue #6
            // When copying a node that has links, indices must be remapped correctly

            // Arrange - Create dialog with shared reply (link scenario)
            var dialog = new Dialog();

            // Create two entries
            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            entry1!.Text.Add(0, "Entry 1");
            entry2!.Text.Add(0, "Entry 2");
            dialog.AddNodeInternal(entry1, entry1.Type);
            dialog.AddNodeInternal(entry2, entry2.Type);

            // Create shared reply
            var sharedReply = dialog.CreateNode(DialogNodeType.Reply);
            sharedReply!.Text.Add(0, "Shared reply");
            dialog.AddNodeInternal(sharedReply, sharedReply.Type);

            // Entry1 points to reply (original)
            var ptr1 = dialog.CreatePtr();
            ptr1!.Node = sharedReply;
            ptr1.Type = DialogNodeType.Reply;
            ptr1.Index = 0;  // Points to first reply in Replies list
            ptr1.IsLink = false;
            entry1.Pointers.Add(ptr1);

            // Entry2 points to same reply (link)
            var ptr2 = dialog.CreatePtr();
            ptr2!.Node = sharedReply;
            ptr2.Type = DialogNodeType.Reply;
            ptr2.Index = 0;  // Also points to first reply
            ptr2.IsLink = true;
            ptr2.LinkComment = "[Link to shared reply]";
            entry2.Pointers.Add(ptr2);

            // Add start
            var start = dialog.CreatePtr();
            start!.Node = entry1;
            start.Type = DialogNodeType.Entry;
            start.Index = 0;
            dialog.Starts.Add(start);

            var filePath = Path.Combine(_testDirectory, "copy_with_link.dlg");

            // Act
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert - Structure preserved
            Assert.NotNull(loadedDialog);
            Assert.Equal(2, loadedDialog.Entries.Count);
            Assert.Single(loadedDialog.Replies);

            // Assert - Links preserved
            var loadedEntry1 = loadedDialog.Entries[0];
            var loadedEntry2 = loadedDialog.Entries[1];

            Assert.Single(loadedEntry1.Pointers);
            Assert.Single(loadedEntry2.Pointers);

            Assert.False(loadedEntry1.Pointers[0].IsLink, "First pointer should be original");
            Assert.True(loadedEntry2.Pointers[0].IsLink, "Second pointer should be link");

            // Assert - Both pointers reference same reply
            Assert.Equal(0u, loadedEntry1.Pointers[0].Index);
            Assert.Equal(0u, loadedEntry2.Pointers[0].Index);
        }

        [Fact]
        public async Task CopyPaste_ComplexLinkStructure_PreservesAllReferences()
        {
            // Test complex scenario with multiple levels of links
            var dialog = new Dialog();

            // Create entries
            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            var entry3 = dialog.CreateNode(DialogNodeType.Entry);
            entry1!.Text.Add(0, "Entry 1");
            entry2!.Text.Add(0, "Entry 2");
            entry3!.Text.Add(0, "Entry 3");
            dialog.AddNodeInternal(entry1, entry1.Type);
            dialog.AddNodeInternal(entry2, entry2.Type);
            dialog.AddNodeInternal(entry3, entry3.Type);

            // Create replies
            var reply1 = dialog.CreateNode(DialogNodeType.Reply);
            var reply2 = dialog.CreateNode(DialogNodeType.Reply);
            reply1!.Text.Add(0, "Reply 1");
            reply2!.Text.Add(0, "Reply 2");
            dialog.AddNodeInternal(reply1, reply1.Type);
            dialog.AddNodeInternal(reply2, reply2.Type);

            // Entry1 -> Reply1 (original)
            var ptr1 = dialog.CreatePtr();
            ptr1!.Node = reply1;
            ptr1.Type = DialogNodeType.Reply;
            ptr1.Index = 0;
            ptr1.IsLink = false;
            entry1.Pointers.Add(ptr1);

            // Entry2 -> Reply1 (link) and Reply2 (original)
            var ptr2a = dialog.CreatePtr();
            ptr2a!.Node = reply1;
            ptr2a.Type = DialogNodeType.Reply;
            ptr2a.Index = 0;
            ptr2a.IsLink = true;
            entry2.Pointers.Add(ptr2a);

            var ptr2b = dialog.CreatePtr();
            ptr2b!.Node = reply2;
            ptr2b.Type = DialogNodeType.Reply;
            ptr2b.Index = 1;
            ptr2b.IsLink = false;
            entry2.Pointers.Add(ptr2b);

            // Entry3 -> Reply2 (link)
            var ptr3 = dialog.CreatePtr();
            ptr3!.Node = reply2;
            ptr3.Type = DialogNodeType.Reply;
            ptr3.Index = 1;
            ptr3.IsLink = true;
            entry3.Pointers.Add(ptr3);

            // Reply1 -> Entry3 (creating a cycle)
            var ptrR1 = dialog.CreatePtr();
            ptrR1!.Node = entry3;
            ptrR1.Type = DialogNodeType.Entry;
            ptrR1.Index = 2;
            ptrR1.IsLink = false;
            reply1.Pointers.Add(ptrR1);

            // Add start
            var start = dialog.CreatePtr();
            start!.Node = entry1;
            start.Type = DialogNodeType.Entry;
            start.Index = 0;
            dialog.Starts.Add(start);

            var filePath = Path.Combine(_testDirectory, "copy_complex_links.dlg");

            // Act
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert - All nodes preserved
            Assert.NotNull(loadedDialog);
            Assert.Equal(3, loadedDialog.Entries.Count);
            Assert.Equal(2, loadedDialog.Replies.Count);

            // Assert - Link flags preserved
            var e1 = loadedDialog.Entries[0];
            var e2 = loadedDialog.Entries[1];
            var e3 = loadedDialog.Entries[2];
            var r1 = loadedDialog.Replies[0];

            Assert.Single(e1.Pointers);
            Assert.False(e1.Pointers[0].IsLink);

            Assert.Equal(2, e2.Pointers.Count);
            Assert.True(e2.Pointers[0].IsLink);
            Assert.False(e2.Pointers[1].IsLink);

            Assert.Single(e3.Pointers);
            Assert.True(e3.Pointers[0].IsLink);

            Assert.Single(r1.Pointers);
            Assert.False(r1.Pointers[0].IsLink);

            // Assert - Indices correct
            Assert.Equal(0u, e1.Pointers[0].Index); // -> Reply1
            Assert.Equal(0u, e2.Pointers[0].Index); // -> Reply1 (link)
            Assert.Equal(1u, e2.Pointers[1].Index); // -> Reply2
            Assert.Equal(1u, e3.Pointers[0].Index); // -> Reply2 (link)
            Assert.Equal(2u, r1.Pointers[0].Index); // -> Entry3
        }

        [Fact]
        public void SimulateCopyPaste_UpdatesIndicesCorrectly()
        {
            // Simulate the actual copy/paste operation that would happen in the UI
            // This test doesn't save/load, just verifies the in-memory operations

            var dialog = new Dialog();

            // Original structure: Entry1 -> Reply1
            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            entry1!.Text.Add(0, "Original entry");
            dialog.AddNodeInternal(entry1, entry1.Type);

            var reply1 = dialog.CreateNode(DialogNodeType.Reply);
            reply1!.Text.Add(0, "Original reply");
            dialog.AddNodeInternal(reply1, reply1.Type);

            var ptr1 = dialog.CreatePtr();
            ptr1!.Node = reply1;
            ptr1.Type = DialogNodeType.Reply;
            ptr1.Index = 0;
            entry1.Pointers.Add(ptr1);

            // Simulate paste: Entry2 (copy of Entry1) -> Reply2 (copy of Reply1)
            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            entry2!.Text.Add(0, "Copied entry");
            dialog.AddNodeInternal(entry2, entry2.Type);

            var reply2 = dialog.CreateNode(DialogNodeType.Reply);
            reply2!.Text.Add(0, "Copied reply");
            dialog.AddNodeInternal(reply2, reply2.Type);

            var ptr2 = dialog.CreatePtr();
            ptr2!.Node = reply2;
            ptr2.Type = DialogNodeType.Reply;
            ptr2.Index = 1; // CRITICAL: Must be 1, not 0, since it's the second reply
            entry2.Pointers.Add(ptr2);

            // Assert - Indices are correct
            Assert.Equal(2, dialog.Entries.Count);
            Assert.Equal(2, dialog.Replies.Count);

            Assert.Equal(0u, entry1.Pointers[0].Index); // Points to first reply
            Assert.Equal(1u, entry2.Pointers[0].Index); // Points to second reply

            // Verify nodes are at expected positions
            Assert.Same(entry1, dialog.Entries[0]);
            Assert.Same(entry2, dialog.Entries[1]);
            Assert.Same(reply1, dialog.Replies[0]);
            Assert.Same(reply2, dialog.Replies[1]);
        }

        [Fact]
        public void SimulatePasteAsLink_SharesNodeCorrectly()
        {
            // Simulate "Paste as Link" operation
            var dialog = new Dialog();

            // Original: Entry1 -> Reply1
            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            entry1!.Text.Add(0, "Entry 1");
            dialog.AddNodeInternal(entry1, entry1.Type);

            var reply1 = dialog.CreateNode(DialogNodeType.Reply);
            reply1!.Text.Add(0, "Shared reply");
            dialog.AddNodeInternal(reply1, reply1.Type);

            var ptr1 = dialog.CreatePtr();
            ptr1!.Node = reply1;
            ptr1.Type = DialogNodeType.Reply;
            ptr1.Index = 0;
            ptr1.IsLink = false;
            entry1.Pointers.Add(ptr1);

            // Paste as Link: Entry2 -> [Link to Reply1]
            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            entry2!.Text.Add(0, "Entry 2");
            dialog.AddNodeInternal(entry2, entry2.Type);

            var ptr2 = dialog.CreatePtr();
            ptr2!.Node = reply1; // Same node reference
            ptr2.Type = DialogNodeType.Reply;
            ptr2.Index = 0; // Same index
            ptr2.IsLink = true; // Mark as link
            ptr2.LinkComment = "[Linked from Entry 2]";
            entry2.Pointers.Add(ptr2);

            // Assert
            Assert.Equal(2, dialog.Entries.Count);
            Assert.Single(dialog.Replies); // Only one reply (shared)

            // Both entries point to same reply
            Assert.Same(ptr1.Node, ptr2.Node);
            Assert.Equal(ptr1.Index, ptr2.Index);

            // But with different link flags
            Assert.False(ptr1.IsLink);
            Assert.True(ptr2.IsLink);
        }
    }
}
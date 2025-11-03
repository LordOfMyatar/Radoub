using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using DialogEditor.Services;
using DialogEditor.Models;
using DialogEditor.ViewModels;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for delete operations, specifically testing the bug where deleting
    /// one node causes other nodes to disappear when they share replies
    /// </summary>
    public class DeleteOperationTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly DialogFileService _dialogService;

        public DeleteOperationTests()
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
        public void Delete_NodeWithSharedReplies_PreservesOtherNodes()
        {
            // This test reproduces the bug from LNS_DLG where deleting "Anything else?"
            // causes "Sure, what kind?" to disappear

            // Arrange - Create dialog structure matching LNS_DLG
            var dialog = new Dialog();

            // Create main entries
            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            entry1!.Text.Add(0, "[Owner] Hey, keep it down and I'll show you my wares.");
            entry1.Speaker = "Owner";
            dialog.AddNodeInternal(entry1, entry1.Type);

            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            entry2!.Text.Add(0, "[Owner] Sure, what kind?");
            entry2.Speaker = "Owner";
            dialog.AddNodeInternal(entry2, entry2.Type);

            var entry3 = dialog.CreateNode(DialogNodeType.Entry);
            entry3!.Text.Add(0, "[Owner] Anything else?");
            entry3.Speaker = "Owner";
            dialog.AddNodeInternal(entry3, entry3.Type);

            // Create shared reply that multiple entries point to
            var sharedReply = dialog.CreateNode(DialogNodeType.Reply);
            sharedReply!.Text.Add(0, "[PC] I need gear, lots and lots of gear!");
            dialog.AddNodeInternal(sharedReply, sharedReply.Type);

            // Entry1 -> SharedReply
            var ptr1 = dialog.CreatePtr();
            ptr1!.Node = sharedReply;
            ptr1.Type = DialogNodeType.Reply;
            ptr1.Index = 0;
            ptr1.IsLink = false;
            ptr1.Parent = dialog;
            entry1.Pointers.Add(ptr1);
            dialog.LinkRegistry.RegisterLink(ptr1);

            // Entry2 has its own replies but also links to shared reply
            var ptr2 = dialog.CreatePtr();
            ptr2!.Node = sharedReply;
            ptr2.Type = DialogNodeType.Reply;
            ptr2.Index = 0;
            ptr2.IsLink = true; // This is a link
            ptr2.Parent = dialog;
            entry2.Pointers.Add(ptr2);
            dialog.LinkRegistry.RegisterLink(ptr2);

            // Entry3 -> SharedReply (link)
            var ptr3 = dialog.CreatePtr();
            ptr3!.Node = sharedReply;
            ptr3.Type = DialogNodeType.Reply;
            ptr3.Index = 0;
            ptr3.IsLink = true; // This is a link
            ptr3.Parent = dialog;
            entry3.Pointers.Add(ptr3);
            dialog.LinkRegistry.RegisterLink(ptr3);

            // Add start pointers
            var start1 = dialog.CreatePtr();
            start1!.Node = entry1;
            start1.Type = DialogNodeType.Entry;
            start1.Index = 0;
            start1.Parent = dialog;
            dialog.Starts.Add(start1);
            dialog.LinkRegistry.RegisterLink(start1);

            var start2 = dialog.CreatePtr();
            start2!.Node = entry2;
            start2.Type = DialogNodeType.Entry;
            start2.Index = 1;
            start2.Parent = dialog;
            dialog.Starts.Add(start2);
            dialog.LinkRegistry.RegisterLink(start2);

            var start3 = dialog.CreatePtr();
            start3!.Node = entry3;
            start3.Type = DialogNodeType.Entry;
            start3.Index = 2;
            start3.Parent = dialog;
            dialog.Starts.Add(start3);
            dialog.LinkRegistry.RegisterLink(start3);

            // Verify initial state
            Assert.Equal(3, dialog.Entries.Count);
            Assert.Single(dialog.Replies);
            Assert.Equal(3, dialog.Starts.Count);

            // Act - Use MainViewModel's DeleteNodeRecursive to simulate actual deletion
            // This mimics what happens in the UI
            var viewModel = new MainViewModel();
            viewModel.CurrentDialog = dialog;

            // Manually call DeleteNodeRecursive to test the fix
            // First unregister start pointer
            var startToRemove = dialog.Starts.FirstOrDefault(s => s.Node == entry3);
            if (startToRemove != null)
            {
                dialog.LinkRegistry.UnregisterLink(startToRemove);
                dialog.Starts.Remove(startToRemove);
            }

            // Use reflection to call the private DeleteNodeRecursive method
            var deleteMethod = typeof(MainViewModel).GetMethod("DeleteNodeRecursive",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            deleteMethod?.Invoke(viewModel, new object[] { entry3 });

            // Remove from collection
            dialog.RemoveNodeInternal(entry3, entry3.Type);

            // Update indices
            dialog.RebuildLinkRegistry();
            for (uint i = 0; i < dialog.Entries.Count; i++)
            {
                dialog.LinkRegistry.UpdateNodeIndex(dialog.Entries[(int)i], i, DialogNodeType.Entry);
            }

            // Assert - entry2 should still exist!
            Assert.Equal(2, dialog.Entries.Count);
            Assert.Single(dialog.Replies); // Shared reply should still exist
            Assert.Equal(2, dialog.Starts.Count);

            // Verify entry2 ("Sure, what kind?") still exists
            var remainingEntry2 = dialog.Entries.FirstOrDefault(e => e.Text.GetDefault().Contains("Sure, what kind?"));
            Assert.NotNull(remainingEntry2);

            // Verify shared reply still exists
            Assert.Single(dialog.Replies);
            Assert.Equal("[PC] I need gear, lots and lots of gear!", dialog.Replies[0].Text.GetDefault());
        }

        [Fact]
        public void Delete_NodeWithSharedReplies_UpdatesIndicesCorrectly()
        {
            // Test that indices are properly updated after deletion

            var dialog = new Dialog();

            // Create 3 entries
            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            entry1!.Text.Add(0, "Entry 1");
            dialog.AddNodeInternal(entry1, entry1.Type);

            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            entry2!.Text.Add(0, "Entry 2");
            dialog.AddNodeInternal(entry2, entry2.Type);

            var entry3 = dialog.CreateNode(DialogNodeType.Entry);
            entry3!.Text.Add(0, "Entry 3");
            dialog.AddNodeInternal(entry3, entry3.Type);

            // Create shared reply
            var reply = dialog.CreateNode(DialogNodeType.Reply);
            reply!.Text.Add(0, "Shared Reply");
            dialog.AddNodeInternal(reply, reply.Type);

            // All entries point to the same reply
            for (int i = 0; i < 3; i++)
            {
                var entry = dialog.Entries[i];
                var ptr = dialog.CreatePtr();
                ptr!.Node = reply;
                ptr.Type = DialogNodeType.Reply;
                ptr.Index = 0;
                ptr.IsLink = i > 0; // First is original, others are links
                ptr.Parent = dialog;
                entry.Pointers.Add(ptr);
                dialog.LinkRegistry.RegisterLink(ptr);
            }

            // Delete middle entry (entry2)
            dialog.RemoveNodeInternal(entry2, entry2.Type);

            // Rebuild and update indices
            dialog.RebuildLinkRegistry();

            // Assert
            Assert.Equal(2, dialog.Entries.Count);
            Assert.Equal("Entry 1", dialog.Entries[0].Text.GetDefault());
            Assert.Equal("Entry 3", dialog.Entries[1].Text.GetDefault());

            // Verify pointers still work
            Assert.Single(dialog.Entries[0].Pointers);
            Assert.Equal(0u, dialog.Entries[0].Pointers[0].Index);
            Assert.Single(dialog.Entries[1].Pointers);
            Assert.Equal(0u, dialog.Entries[1].Pointers[0].Index);
        }

        [Fact]
        public async Task Delete_WithSharedReplies_RoundTripPreservesStructure()
        {
            // Test that save/load after deletion preserves the correct structure

            var dialog = new Dialog();

            // Create structure similar to LNS_DLG
            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            entry1!.Text.Add(0, "First entry");
            dialog.AddNodeInternal(entry1, entry1.Type);

            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            entry2!.Text.Add(0, "Second entry");
            dialog.AddNodeInternal(entry2, entry2.Type);

            var entry3 = dialog.CreateNode(DialogNodeType.Entry);
            entry3!.Text.Add(0, "Third entry to delete");
            dialog.AddNodeInternal(entry3, entry3.Type);

            // Shared reply
            var reply = dialog.CreateNode(DialogNodeType.Reply);
            reply!.Text.Add(0, "Shared reply");
            dialog.AddNodeInternal(reply, reply.Type);

            // Link all entries to reply
            foreach (var entry in dialog.Entries)
            {
                var ptr = dialog.CreatePtr();
                ptr!.Node = reply;
                ptr.Type = DialogNodeType.Reply;
                ptr.Index = 0;
                ptr.IsLink = entry != entry1;
                ptr.Parent = dialog;
                entry.Pointers.Add(ptr);
                dialog.LinkRegistry.RegisterLink(ptr);
            }

            // Add starts
            for (int i = 0; i < dialog.Entries.Count; i++)
            {
                var start = dialog.CreatePtr();
                start!.Node = dialog.Entries[i];
                start.Type = DialogNodeType.Entry;
                start.Index = (uint)i;
                start.Parent = dialog;
                dialog.Starts.Add(start);
                dialog.LinkRegistry.RegisterLink(start);
            }

            // Delete entry3
            dialog.RemoveNodeInternal(entry3, entry3.Type);

            // Remove its start pointer
            var startToRemove = dialog.Starts.FirstOrDefault(s => s.Node == entry3);
            if (startToRemove != null)
            {
                dialog.LinkRegistry.UnregisterLink(startToRemove);
                dialog.Starts.Remove(startToRemove);
            }

            // Save and reload
            var filePath = Path.Combine(_testDirectory, "delete_test.dlg");
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert
            Assert.NotNull(loadedDialog);
            Assert.Equal(2, loadedDialog.Entries.Count);
            Assert.Single(loadedDialog.Replies);
            Assert.Equal(2, loadedDialog.Starts.Count);

            // Verify second entry still exists
            Assert.Equal("Second entry", loadedDialog.Entries[1].Text.GetDefault());
        }
    }
}
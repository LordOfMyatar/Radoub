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
    /// Basic tests to verify parser functionality with the actual Dialog structure
    /// </summary>
    public class BasicParserTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly DialogFileService _dialogService;

        public BasicParserTests()
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
        public void Dialog_CanBeCreated()
        {
            // Arrange & Act
            var dialog = new Dialog();

            // Assert
            Assert.NotNull(dialog);
            Assert.NotNull(dialog.Entries);
            Assert.NotNull(dialog.Replies);
            Assert.NotNull(dialog.Starts);
            Assert.Empty(dialog.Entries);
            Assert.Empty(dialog.Replies);
            Assert.Empty(dialog.Starts);
        }

        [Fact]
        public void Dialog_CanAddStartingNode()
        {
            // Arrange
            var dialog = new Dialog();

            // Act
            var startPtr = dialog.Add();

            // The Add() method creates a node but doesn't add it to Entries
            // We need to manually add it
            if (startPtr?.Node != null)
            {
                dialog.AddNodeInternal(startPtr.Node, startPtr.Node.Type);
            }

            // Assert
            Assert.NotNull(startPtr);
            Assert.NotNull(startPtr.Node);
            Assert.Single(dialog.Starts);
            Assert.Single(dialog.Entries);
            Assert.Equal(DialogNodeType.Entry, startPtr.Type);
        }

        [Fact]
        public async Task Dialog_BasicRoundTrip_Succeeds()
        {
            // Arrange
            var dialog = new Dialog();

            // Create an entry node
            var entryNode = dialog.CreateNode(DialogNodeType.Entry);
            Assert.NotNull(entryNode);
            entryNode!.Text.Add(0, "Hello, world!");
            entryNode.Speaker = "NPC_TEST";

            // Add node to Entries collection first
            dialog.AddNodeInternal(entryNode, entryNode.Type);

            // Create start pointer with correct index
            var startPtr = dialog.CreatePtr();
            Assert.NotNull(startPtr);
            startPtr!.Type = DialogNodeType.Entry;
            startPtr.Node = entryNode;
            startPtr.Index = 0; // Index in Entries list
            dialog.Starts.Add(startPtr);

            var filePath = Path.Combine(_testDirectory, "basic.dlg");

            // Act - Save
            await _dialogService.SaveToFileAsync(dialog, filePath);

            // Assert - File exists
            Assert.True(File.Exists(filePath));

            // Act - Load
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert - Basic structure preserved
            Assert.NotNull(loadedDialog);
            Assert.Single(loadedDialog.Entries);
            Assert.Single(loadedDialog.Starts);

            // Check text preservation
            var loadedText = loadedDialog.Entries.First().Text.GetDefault();
            Assert.Equal("Hello, world!", loadedText);
        }

        [Fact]
        public async Task Dialog_WithReply_RoundTrip_Succeeds()
        {
            // Arrange
            var dialog = new Dialog();

            // Create Entry
            var entryNode = dialog.CreateNode(DialogNodeType.Entry);
            Assert.NotNull(entryNode);
            entryNode!.Text.Add(0, "NPC speaks");
            entryNode.Speaker = "NPC_TEST";
            dialog.AddNodeInternal(entryNode, entryNode.Type);

            // Create Reply
            var replyNode = dialog.CreateNode(DialogNodeType.Reply);
            Assert.NotNull(replyNode);
            replyNode!.Text.Add(0, "Player responds");
            dialog.AddNodeInternal(replyNode, replyNode.Type);

            // Link Entry to Reply
            var ptr = dialog.CreatePtr();
            Assert.NotNull(ptr);
            ptr!.Type = DialogNodeType.Reply;
            ptr.Node = replyNode;
            ptr.Index = 0;
            entryNode.Pointers.Add(ptr);

            // Add starting point
            var startPtr = dialog.CreatePtr();
            Assert.NotNull(startPtr);
            startPtr!.Type = DialogNodeType.Entry;
            startPtr.Node = entryNode;
            startPtr.Index = 0;
            dialog.Starts.Add(startPtr);

            var filePath = Path.Combine(_testDirectory, "with_reply.dlg");

            // Act
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert
            Assert.NotNull(loadedDialog);
            Assert.Single(loadedDialog.Entries);
            Assert.Single(loadedDialog.Replies);
            Assert.Single(loadedDialog.Starts);

            // Check link preservation
            var loadedEntry = loadedDialog.Entries.First();
            Assert.Single(loadedEntry.Pointers);
            Assert.Equal(DialogNodeType.Reply, loadedEntry.Pointers.First().Type);
        }

        [Fact]
        public async Task EmptyDialog_RoundTrip_Succeeds()
        {
            // Arrange
            var dialog = new Dialog();
            var filePath = Path.Combine(_testDirectory, "empty.dlg");

            // Act
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert
            Assert.NotNull(loadedDialog);
            Assert.Empty(loadedDialog.Entries);
            Assert.Empty(loadedDialog.Replies);
            Assert.Empty(loadedDialog.Starts);
        }

        [Fact]
        public void DialogNode_CanBeCreated()
        {
            // Arrange
            var dialog = new Dialog();

            // Act
            var node = dialog.CreateNode(DialogNodeType.Entry);
            dialog.AddNodeInternal(node!, node!.Type);

            // Assert
            Assert.NotNull(node);
            Assert.Equal(DialogNodeType.Entry, node.Type);
            Assert.NotNull(node.Text);
            Assert.NotNull(node.Pointers);
            Assert.Single(dialog.Entries);
        }

        [Fact]
        public void LocString_StoresAndRetrievesText()
        {
            // Arrange
            var locString = new LocString();

            // Act
            locString.Add(0, "English text");
            locString.Add(1, "French text");

            // Assert
            Assert.Equal("English text", locString.Get(0));
            Assert.Equal("French text", locString.Get(1));
            Assert.Equal("English text", locString.GetDefault());
        }

        [Fact]
        public async Task Dialog_WithLink_PreservesIsLinkFlag()
        {
            // This test specifically targets Issue #6
            // We're testing that the IsLink flag is preserved through save/load

            // Arrange
            var dialog = new Dialog();

            // Create two entries that share a reply
            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            var sharedReply = dialog.CreateNode(DialogNodeType.Reply);

            Assert.NotNull(entry1);
            Assert.NotNull(entry2);
            Assert.NotNull(sharedReply);

            entry1!.Text.Add(0, "Entry 1");
            entry2!.Text.Add(0, "Entry 2");
            sharedReply!.Text.Add(0, "Shared Reply");

            // Add nodes to collections
            dialog.AddNodeInternal(entry1, entry1.Type);
            dialog.AddNodeInternal(entry2, entry2.Type);
            dialog.AddNodeInternal(sharedReply, sharedReply.Type);

            // Entry1 has original pointer to reply
            var ptr1 = dialog.CreatePtr();
            Assert.NotNull(ptr1);
            ptr1!.Node = sharedReply;
            ptr1.Type = DialogNodeType.Reply;
            ptr1.Index = 0;
            ptr1.IsLink = false;  // Original
            entry1.Pointers.Add(ptr1);

            // Entry2 has link to same reply
            var ptr2 = dialog.CreatePtr();
            Assert.NotNull(ptr2);
            ptr2!.Node = sharedReply;
            ptr2.Type = DialogNodeType.Reply;
            ptr2.Index = 0;
            ptr2.IsLink = true;  // Link
            ptr2.LinkComment = "[Link to shared reply]";
            entry2.Pointers.Add(ptr2);

            // Add starts
            var start1 = dialog.CreatePtr();
            Assert.NotNull(start1);
            start1!.Node = entry1;
            start1.Type = DialogNodeType.Entry;
            start1.Index = 0;
            dialog.Starts.Add(start1);

            var filePath = Path.Combine(_testDirectory, "with_links.dlg");

            // Act
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert - Check that IsLink flags are preserved
            Assert.NotNull(loadedDialog);
            Assert.Equal(2, loadedDialog.Entries.Count);

            var loadedEntry1 = loadedDialog.Entries[0];
            var loadedEntry2 = loadedDialog.Entries[1];

            Assert.False(loadedEntry1.Pointers.First().IsLink, "First pointer should not be a link");
            Assert.True(loadedEntry2.Pointers.First().IsLink, "Second pointer should be a link");
        }
    }
}
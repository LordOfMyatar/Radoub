using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Xunit;
using DialogEditor.Services;
using DialogEditor.Models;
using DialogEditor.Parsers;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for parser round-trip operations to ensure file integrity
    /// </summary>
    public class ParserRoundTripTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly DialogFileService _dialogService;

        public ParserRoundTripTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"ParleyTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            _dialogService = new DialogFileService();
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Fact]
        public async Task SimpleDialog_RoundTrip_PreservesStructure()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var filePath = Path.Combine(_testDirectory, "simple.dlg");

            // Act - Save
            await _dialogService.SaveToFileAsync(dialog, filePath);

            // Assert - File exists
            Assert.True(File.Exists(filePath));

            // Act - Load
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert - Structure preserved
            Assert.NotNull(loadedDialog);
            Assert.Equal(dialog.Entries.Count, loadedDialog.Entries.Count);
            Assert.Equal(dialog.Replies.Count, loadedDialog.Replies.Count);
            Assert.Equal(dialog.StartingList.Count, loadedDialog.StartingList.Count);
        }

        [Fact]
        public async Task Dialog_WithLinks_RoundTrip_PreservesLinks()
        {
            // Arrange
            var dialog = CreateDialogWithLinks();
            var filePath = Path.Combine(_testDirectory, "with_links.dlg");

            // Act
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert - Links preserved
            Assert.NotNull(loadedDialog);

            // Check that link pointers are preserved
            var firstEntry = loadedDialog.Entries[0];
            var linkPointer = firstEntry.Pointers.FirstOrDefault(p => p.IsLink);
            Assert.NotNull(linkPointer);
            Assert.True(linkPointer.IsLink);
            Assert.Equal(0u, linkPointer.Index); // Should point to first reply
        }

        [Fact]
        public async Task Dialog_RoundTrip_PreservesNodeText()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var originalText = dialog.Entries[0].Text.Value;
            var filePath = Path.Combine(_testDirectory, "text_test.dlg");

            // Act
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert
            Assert.Equal(originalText, loadedDialog.Entries[0].Text.Value);
        }

        [Fact]
        public async Task EmptyDialog_RoundTrip_Succeeds()
        {
            // Arrange
            var dialog = new Dialog
            {
                Entries = new List<DialogNode>(),
                Replies = new List<DialogNode>(),
                StartingList = new List<DialogPtr>()
            };
            var filePath = Path.Combine(_testDirectory, "empty.dlg");

            // Act
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert
            Assert.NotNull(loadedDialog);
            Assert.Empty(loadedDialog.Entries);
            Assert.Empty(loadedDialog.Replies);
            Assert.Empty(loadedDialog.StartingList);
        }

        #region Helper Methods

        private Dialog CreateSimpleDialog()
        {
            var dialog = new Dialog
            {
                Entries = new List<DialogNode>(),
                Replies = new List<DialogNode>(),
                StartingList = new List<DialogPtr>()
            };

            // Create a simple Entry -> Reply structure
            var entry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString { Value = "Hello, adventurer!" },
                Speaker = "NPC_MERCHANT",
                Pointers = new List<DialogPtr>()
            };

            var reply = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString { Value = "Hello, merchant!" },
                Pointers = new List<DialogPtr>()
            };

            // Link entry to reply
            entry.Pointers.Add(new DialogPtr
            {
                Node = reply,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false
            });

            dialog.Entries.Add(entry);
            dialog.Replies.Add(reply);

            // Add starting pointer
            dialog.StartingList.Add(new DialogPtr
            {
                Node = entry,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsLink = false
            });

            return dialog;
        }

        private Dialog CreateDialogWithLinks()
        {
            var dialog = new Dialog
            {
                Entries = new List<DialogNode>(),
                Replies = new List<DialogNode>(),
                StartingList = new List<DialogPtr>()
            };

            // Create nodes
            var entry1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString { Value = "Entry 1" },
                Speaker = "NPC1",
                Pointers = new List<DialogPtr>()
            };

            var entry2 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString { Value = "Entry 2" },
                Speaker = "NPC2",
                Pointers = new List<DialogPtr>()
            };

            var sharedReply = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString { Value = "Shared Reply" },
                Pointers = new List<DialogPtr>()
            };

            dialog.Entries.Add(entry1);
            dialog.Entries.Add(entry2);
            dialog.Replies.Add(sharedReply);

            // Both entries link to the same reply
            entry1.Pointers.Add(new DialogPtr
            {
                Node = sharedReply,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false
            });

            // This is a link to the same reply
            entry2.Pointers.Add(new DialogPtr
            {
                Node = sharedReply,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = true,  // Mark as link
                LinkComment = "[Link to shared reply]"
            });

            dialog.StartingList.Add(new DialogPtr
            {
                Node = entry1,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsLink = false
            });

            return dialog;
        }

        #endregion
    }
}
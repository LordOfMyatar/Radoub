using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for DialogSaveService validation, cleanup, and error handling.
    /// Tests the service layer that sits above DialogFileService.
    /// </summary>
    public class DialogSaveServiceTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly DialogSaveService _saveService;
        private readonly DialogFileService _fileService;

        public DialogSaveServiceTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"ParleyTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            _saveService = new DialogSaveService();
            _fileService = new DialogFileService();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        #region Input Validation Tests

        [Fact]
        public async Task SaveDialogAsync_NullDialog_ReturnsFailed()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "test.dlg");

            // Act
            var result = await _saveService.SaveDialogAsync(null!, filePath);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("No dialog to save", result.StatusMessage);
            Assert.Contains("Dialog is null", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveDialogAsync_NullFilePath_ReturnsFailed()
        {
            // Arrange
            var dialog = new Dialog();

            // Act
            var result = await _saveService.SaveDialogAsync(dialog, null!);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("No file path specified", result.StatusMessage);
            Assert.Contains("null or empty", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveDialogAsync_EmptyFilePath_ReturnsFailed()
        {
            // Arrange
            var dialog = new Dialog();

            // Act
            var result = await _saveService.SaveDialogAsync(dialog, "");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("No file path specified", result.StatusMessage);
        }

        #endregion

        #region Basic Save Tests

        [Fact]
        public async Task SaveDialogAsync_EmptyDialog_ReturnsSuccess()
        {
            // Arrange
            var dialog = new Dialog();
            var filePath = Path.Combine(_testDirectory, "empty.dlg");

            // Act
            var result = await _saveService.SaveDialogAsync(dialog, filePath);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("saved successfully", result.StatusMessage);
            Assert.True(File.Exists(filePath));
        }

        [Fact]
        public async Task SaveDialogAsync_SimpleDialog_ReturnsSuccess()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var filePath = Path.Combine(_testDirectory, "simple.dlg");

            // Act
            var result = await _saveService.SaveDialogAsync(dialog, filePath);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("saved successfully", result.StatusMessage);
            Assert.True(File.Exists(filePath));
        }

        [Fact]
        public async Task SaveDialogAsync_DlgFormat_CreatesValidFile()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var filePath = Path.Combine(_testDirectory, "test.dlg");

            // Act
            var saveResult = await _saveService.SaveDialogAsync(dialog, filePath);
            var reloadedDialog = await _fileService.LoadFromFileAsync(filePath);

            // Assert
            Assert.True(saveResult.Success);
            Assert.NotNull(reloadedDialog);
            Assert.Single(reloadedDialog.Entries);
            Assert.Equal("Hello, world!", reloadedDialog.Entries.First().Text.GetDefault());
        }

        [Fact]
        public async Task SaveDialogAsync_JsonFormat_CreatesValidJson()
        {
            // Arrange
            var dialog = CreateSimpleDialog();
            var filePath = Path.Combine(_testDirectory, "test.json");

            // Act
            var result = await _saveService.SaveDialogAsync(dialog, filePath);

            // Assert
            Assert.True(result.Success);
            Assert.True(File.Exists(filePath));
            var json = await File.ReadAllTextAsync(filePath);
            Assert.Contains("\"Entries\"", json);
        }

        #endregion

        #region Orphan Cleanup Tests

        [Fact]
        public async Task SaveDialogAsync_WithOrphanedEntry_RemovesOrphan()
        {
            // Arrange - Dialog with orphaned entry (no incoming pointers)
            var dialog = new Dialog();

            // Create entry that's reachable from start
            var reachableEntry = dialog.CreateNode(DialogNodeType.Entry);
            Assert.NotNull(reachableEntry);
            reachableEntry!.Text.Add(0, "Reachable");
            dialog.AddNodeInternal(reachableEntry, DialogNodeType.Entry);

            // Create orphaned entry (not in starting list, no pointers to it)
            var orphanEntry = dialog.CreateNode(DialogNodeType.Entry);
            Assert.NotNull(orphanEntry);
            orphanEntry!.Text.Add(0, "Orphaned");
            dialog.AddNodeInternal(orphanEntry, DialogNodeType.Entry);

            // Only add reachable entry to starts
            var startPtr = dialog.CreatePtr();
            Assert.NotNull(startPtr);
            startPtr!.Type = DialogNodeType.Entry;
            startPtr.Node = reachableEntry;
            startPtr.Index = 0;
            dialog.Starts.Add(startPtr);

            var initialEntryCount = dialog.Entries.Count;
            var filePath = Path.Combine(_testDirectory, "orphan_cleanup.dlg");

            // Act
            var result = await _saveService.SaveDialogAsync(dialog, filePath);

            // Assert
            Assert.True(result.Success);
            Assert.True(dialog.Entries.Count < initialEntryCount, "Orphan should be removed");
        }

        [Fact]
        public async Task SaveDialogAsync_ValidStructure_PreservesAllNodes()
        {
            // Arrange - Dialog where all nodes are reachable
            var dialog = new Dialog();

            // Entry → Reply → Entry structure (all reachable)
            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            Assert.NotNull(entry1);
            entry1!.Text.Add(0, "Entry 1");
            dialog.AddNodeInternal(entry1, DialogNodeType.Entry);

            var reply = dialog.CreateNode(DialogNodeType.Reply);
            Assert.NotNull(reply);
            reply!.Text.Add(0, "Reply 1");
            dialog.AddNodeInternal(reply, DialogNodeType.Reply);

            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            Assert.NotNull(entry2);
            entry2!.Text.Add(0, "Entry 2");
            dialog.AddNodeInternal(entry2, DialogNodeType.Entry);

            // Link structure
            var startPtr = dialog.CreatePtr();
            Assert.NotNull(startPtr);
            startPtr!.Type = DialogNodeType.Entry;
            startPtr.Node = entry1;
            startPtr.Index = 0;
            dialog.Starts.Add(startPtr);

            var ptr1 = dialog.CreatePtr();
            Assert.NotNull(ptr1);
            ptr1!.Type = DialogNodeType.Reply;
            ptr1.Node = reply;
            ptr1.Index = 0;
            entry1.Pointers.Add(ptr1);

            var ptr2 = dialog.CreatePtr();
            Assert.NotNull(ptr2);
            ptr2!.Type = DialogNodeType.Entry;
            ptr2.Node = entry2;
            ptr2.Index = 1;
            reply.Pointers.Add(ptr2);

            var initialEntryCount = dialog.Entries.Count;
            var initialReplyCount = dialog.Replies.Count;
            var filePath = Path.Combine(_testDirectory, "valid_structure.dlg");

            // Act
            var result = await _saveService.SaveDialogAsync(dialog, filePath);

            // Assert - No nodes should be removed
            Assert.True(result.Success);
            Assert.Equal(initialEntryCount, dialog.Entries.Count);
            Assert.Equal(initialReplyCount, dialog.Replies.Count);
        }

        #endregion

        #region Pointer Index Validation Tests

        [Fact]
        public async Task SaveDialogAsync_WithValidIndices_Succeeds()
        {
            // Arrange
            var dialog = CreateDialogWithLinks();
            var filePath = Path.Combine(_testDirectory, "valid_indices.dlg");

            // Act
            var result = await _saveService.SaveDialogAsync(dialog, filePath);

            // Assert - Should succeed without index fixes
            Assert.True(result.Success);
        }

        [Fact]
        public async Task SaveDialogAsync_RebuildLinkRegistry_FixesIndices()
        {
            // Arrange - Dialog where RebuildLinkRegistry is needed
            var dialog = CreateDialogWithLinks();

            // Corrupt an index (this simulates what happens if indices get out of sync)
            if (dialog.Entries.Count > 0 && dialog.Entries[0].Pointers.Count > 0)
            {
                dialog.Entries[0].Pointers[0].Index = 999; // Invalid index
            }

            var filePath = Path.Combine(_testDirectory, "rebuild_links.dlg");

            // Act - SaveDialogAsync should call RebuildLinkRegistry and fix this
            var result = await _saveService.SaveDialogAsync(dialog, filePath);

            // Assert - Should succeed after auto-fix
            Assert.True(result.Success);
        }

        #endregion

        #region Round-Trip Tests

        [Fact]
        public async Task SaveDialogAsync_RoundTrip_PreservesStructure()
        {
            // Arrange
            var originalDialog = CreateDialogWithLinks();
            var filePath = Path.Combine(_testDirectory, "roundtrip.dlg");

            var originalEntryCount = originalDialog.Entries.Count;
            var originalReplyCount = originalDialog.Replies.Count;

            // Act
            var saveResult = await _saveService.SaveDialogAsync(originalDialog, filePath);
            var reloadedDialog = await _fileService.LoadFromFileAsync(filePath);

            // Assert
            Assert.True(saveResult.Success);
            Assert.NotNull(reloadedDialog);
            Assert.Equal(originalEntryCount, reloadedDialog.Entries.Count);
            Assert.Equal(originalReplyCount, reloadedDialog.Replies.Count);
        }

        [Fact]
        public async Task SaveDialogAsync_RoundTripJson_PreservesStructure()
        {
            // Arrange
            var originalDialog = CreateDialogWithLinks();
            var filePath = Path.Combine(_testDirectory, "roundtrip.json");

            var originalEntryCount = originalDialog.Entries.Count;

            // Act
            var saveResult = await _saveService.SaveDialogAsync(originalDialog, filePath);
            var reloadedDialog = await _fileService.LoadFromJsonAsync(await File.ReadAllTextAsync(filePath));

            // Assert
            Assert.True(saveResult.Success);
            Assert.NotNull(reloadedDialog);
            Assert.Equal(originalEntryCount, reloadedDialog.Entries.Count);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task SaveDialogAsync_InvalidPath_ReturnsFailed()
        {
            // Arrange
            var dialog = new Dialog();
            var invalidPath = Path.Combine(_testDirectory, "invalid\0path.dlg"); // Invalid filename character

            // Act
            var result = await _saveService.SaveDialogAsync(dialog, invalidPath);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task SaveDialogAsync_ReadOnlyFile_ReturnsFailed()
        {
            // Arrange - Issue #8: Test read-only file detection
            var dialog = new Dialog();
            var filePath = Path.Combine(_testDirectory, "readonly_test.dlg");

            // Create file and set read-only
            await File.WriteAllTextAsync(filePath, "test");
            File.SetAttributes(filePath, FileAttributes.ReadOnly);

            try
            {
                // Act
                var result = await _saveService.SaveDialogAsync(dialog, filePath);

                // Assert
                Assert.False(result.Success);
                Assert.Contains("read-only", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                // Cleanup - remove read-only attribute so file can be deleted
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
            }
        }

        #endregion

        #region Helper Methods

        private Dialog CreateSimpleDialog()
        {
            var dialog = new Dialog();

            var entry = dialog.CreateNode(DialogNodeType.Entry);
            Assert.NotNull(entry);
            entry!.Text.Add(0, "Hello, world!");
            entry.Speaker = "NPC_TEST";

            dialog.AddNodeInternal(entry, DialogNodeType.Entry);

            var startPtr = dialog.CreatePtr();
            Assert.NotNull(startPtr);
            startPtr!.Type = DialogNodeType.Entry;
            startPtr.Node = entry;
            startPtr.Index = 0;
            dialog.Starts.Add(startPtr);

            return dialog;
        }

        private Dialog CreateDialogWithLinks()
        {
            var dialog = new Dialog();

            // Create Entry → Reply → Entry structure
            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            Assert.NotNull(entry1);
            entry1!.Text.Add(0, "What do you want?");
            dialog.AddNodeInternal(entry1, DialogNodeType.Entry);

            var reply = dialog.CreateNode(DialogNodeType.Reply);
            Assert.NotNull(reply);
            reply!.Text.Add(0, "I need help.");
            dialog.AddNodeInternal(reply, DialogNodeType.Reply);

            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            Assert.NotNull(entry2);
            entry2!.Text.Add(0, "How can I help?");
            dialog.AddNodeInternal(entry2, DialogNodeType.Entry);

            // Create links
            var startPtr = dialog.CreatePtr();
            Assert.NotNull(startPtr);
            startPtr!.Type = DialogNodeType.Entry;
            startPtr.Node = entry1;
            startPtr.Index = 0;
            dialog.Starts.Add(startPtr);

            var ptr1 = dialog.CreatePtr();
            Assert.NotNull(ptr1);
            ptr1!.Type = DialogNodeType.Reply;
            ptr1.Node = reply;
            ptr1.Index = 0;
            entry1.Pointers.Add(ptr1);

            var ptr2 = dialog.CreatePtr();
            Assert.NotNull(ptr2);
            ptr2!.Type = DialogNodeType.Entry;
            ptr2.Node = entry2;
            ptr2.Index = 1;
            reply.Pointers.Add(ptr2);

            return dialog;
        }

        #endregion
    }
}

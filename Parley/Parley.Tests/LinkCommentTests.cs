using System.IO;
using System.Threading.Tasks;
using Xunit;
using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for Issue #12: LinkComment vs Comment separation
    /// Per BioWare spec:
    /// - Original nodes show/edit DialogNode.Comment
    /// - Link nodes show/edit DialogPtr.LinkComment
    /// These should be completely independent fields.
    /// </summary>
    public class LinkCommentTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly DialogFileService _dialogService;
        private readonly DialogClipboardService _clipboardService;

        public LinkCommentTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"ParleyLinkTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            _dialogService = new DialogFileService();
            _clipboardService = new DialogClipboardService();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try { Directory.Delete(_testDirectory, true); }
                catch { /* Ignore cleanup errors */ }
            }
        }

        #region LinkComment Independence Tests

        [Fact]
        public void LinkComment_IsSeparateFromNodeComment()
        {
            // Arrange
            var dialog = new Dialog();

            var entry = dialog.CreateNode(DialogNodeType.Entry);
            entry!.Text.Add(0, "Original entry");
            entry.Comment = "Original node comment";
            dialog.AddNodeInternal(entry, entry.Type);

            var reply = dialog.CreateNode(DialogNodeType.Reply);
            reply!.Text.Add(0, "Reply");
            dialog.AddNodeInternal(reply, reply.Type);

            // Create original pointer (IsLink=false)
            var originalPtr = dialog.CreatePtr();
            originalPtr!.Node = entry;
            originalPtr.Type = DialogNodeType.Entry;
            originalPtr.Index = 0;
            originalPtr.IsLink = false;
            reply.Pointers.Add(originalPtr);

            // Create link pointer (IsLink=true) with different LinkComment
            var linkPtr = dialog.CreatePtr();
            linkPtr!.Node = entry;
            linkPtr.Type = DialogNodeType.Entry;
            linkPtr.Index = 0;
            linkPtr.IsLink = true;
            linkPtr.LinkComment = "This is the LINK comment";

            // Add start
            var start = dialog.CreatePtr();
            start!.Node = reply;
            start.Type = DialogNodeType.Reply;
            start.Index = 0;
            dialog.Starts.Add(start);

            // Assert - Node comment and LinkComment are independent
            Assert.Equal("Original node comment", entry.Comment);
            Assert.Equal("This is the LINK comment", linkPtr.LinkComment);
            Assert.NotEqual(entry.Comment, linkPtr.LinkComment);
        }

        [Fact]
        public async Task LinkComment_PreservedOnRoundTrip()
        {
            // Arrange
            var dialog = new Dialog();

            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            var entry2 = dialog.CreateNode(DialogNodeType.Entry);
            entry1!.Text.Add(0, "Entry 1");
            entry1.Comment = "Entry 1 node comment";
            entry2!.Text.Add(0, "Entry 2");
            dialog.AddNodeInternal(entry1, entry1.Type);
            dialog.AddNodeInternal(entry2, entry2.Type);

            var sharedReply = dialog.CreateNode(DialogNodeType.Reply);
            sharedReply!.Text.Add(0, "Shared reply");
            sharedReply.Comment = "Shared reply node comment";
            dialog.AddNodeInternal(sharedReply, sharedReply.Type);

            // Entry1 has original pointer to reply
            var ptr1 = dialog.CreatePtr();
            ptr1!.Node = sharedReply;
            ptr1.Type = DialogNodeType.Reply;
            ptr1.Index = 0;
            ptr1.IsLink = false;
            entry1.Pointers.Add(ptr1);

            // Entry2 has link pointer to same reply with different LinkComment
            var ptr2 = dialog.CreatePtr();
            ptr2!.Node = sharedReply;
            ptr2.Type = DialogNodeType.Reply;
            ptr2.Index = 0;
            ptr2.IsLink = true;
            ptr2.LinkComment = "Link-specific comment from Entry2";
            entry2.Pointers.Add(ptr2);

            // Add start
            var start = dialog.CreatePtr();
            start!.Node = entry1;
            start.Type = DialogNodeType.Entry;
            start.Index = 0;
            dialog.Starts.Add(start);

            var start2 = dialog.CreatePtr();
            start2!.Node = entry2;
            start2.Type = DialogNodeType.Entry;
            start2.Index = 1;
            dialog.Starts.Add(start2);

            var filePath = Path.Combine(_testDirectory, "link_comment_roundtrip.dlg");

            // Act
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loadedDialog = await _dialogService.LoadFromFileAsync(filePath);

            // Assert
            Assert.NotNull(loadedDialog);

            // Node comments preserved
            Assert.Equal("Entry 1 node comment", loadedDialog.Entries[0].Comment);
            Assert.Equal("Shared reply node comment", loadedDialog.Replies[0].Comment);

            // Link structure preserved
            Assert.False(loadedDialog.Entries[0].Pointers[0].IsLink);
            Assert.True(loadedDialog.Entries[1].Pointers[0].IsLink);

            // LinkComment preserved separately from node Comment
            Assert.Equal("Link-specific comment from Entry2", loadedDialog.Entries[1].Pointers[0].LinkComment);
        }

        [Fact]
        public void LinkComment_DefaultsToEmptyString()
        {
            // Arrange
            var dialog = new Dialog();
            var linkPtr = dialog.CreatePtr();
            linkPtr!.IsLink = true;

            // Assert - LinkComment should default to empty, not null
            Assert.NotNull(linkPtr.LinkComment);
            Assert.Equal(string.Empty, linkPtr.LinkComment);
        }

        [Fact]
        public void MultipleLinks_EachHaveIndependentLinkComment()
        {
            // Arrange
            var dialog = new Dialog();

            var sharedEntry = dialog.CreateNode(DialogNodeType.Entry);
            sharedEntry!.Text.Add(0, "Shared entry");
            sharedEntry.Comment = "Shared entry's own comment";
            dialog.AddNodeInternal(sharedEntry, sharedEntry.Type);

            var reply1 = dialog.CreateNode(DialogNodeType.Reply);
            var reply2 = dialog.CreateNode(DialogNodeType.Reply);
            var reply3 = dialog.CreateNode(DialogNodeType.Reply);
            reply1!.Text.Add(0, "Reply 1");
            reply2!.Text.Add(0, "Reply 2");
            reply3!.Text.Add(0, "Reply 3");
            dialog.AddNodeInternal(reply1, reply1.Type);
            dialog.AddNodeInternal(reply2, reply2.Type);
            dialog.AddNodeInternal(reply3, reply3.Type);

            // Each reply links to same entry with different LinkComment
            var link1 = dialog.CreatePtr();
            link1!.Node = sharedEntry;
            link1.Type = DialogNodeType.Entry;
            link1.Index = 0;
            link1.IsLink = true;
            link1.LinkComment = "Link 1 comment";
            reply1.Pointers.Add(link1);

            var link2 = dialog.CreatePtr();
            link2!.Node = sharedEntry;
            link2.Type = DialogNodeType.Entry;
            link2.Index = 0;
            link2.IsLink = true;
            link2.LinkComment = "Link 2 comment";
            reply2.Pointers.Add(link2);

            var link3 = dialog.CreatePtr();
            link3!.Node = sharedEntry;
            link3.Type = DialogNodeType.Entry;
            link3.Index = 0;
            link3.IsLink = true;
            link3.LinkComment = "Link 3 comment";
            reply3.Pointers.Add(link3);

            // Assert - Each link has its own independent comment
            Assert.Equal("Link 1 comment", link1.LinkComment);
            Assert.Equal("Link 2 comment", link2.LinkComment);
            Assert.Equal("Link 3 comment", link3.LinkComment);

            // Original node comment unchanged
            Assert.Equal("Shared entry's own comment", sharedEntry.Comment);
        }

        #endregion

        #region Issue #11: Link Type Validation Tests

        [Fact]
        public void PasteAsLink_EntryUnderEntry_ReturnsNull()
        {
            // Arrange - Cannot create Entry → Entry link
            var dialog = new Dialog();

            var parentEntry = dialog.CreateNode(DialogNodeType.Entry);
            parentEntry!.Text.Add(0, "Parent Entry");
            dialog.AddNodeInternal(parentEntry, parentEntry.Type);

            var childEntry = dialog.CreateNode(DialogNodeType.Entry);
            childEntry!.Text.Add(0, "Child Entry");
            dialog.AddNodeInternal(childEntry, childEntry.Type);

            _clipboardService.CopyNode(childEntry, dialog);

            // Act
            var result = _clipboardService.PasteAsLink(dialog, parentEntry);

            // Assert - Should return null because Entry under Entry is invalid
            Assert.Null(result);
        }

        [Fact]
        public void PasteAsLink_ReplyUnderReply_ReturnsNull()
        {
            // Arrange - Cannot create Reply → Reply link
            var dialog = new Dialog();

            var parentReply = dialog.CreateNode(DialogNodeType.Reply);
            parentReply!.Text.Add(0, "Parent Reply");
            dialog.AddNodeInternal(parentReply, parentReply.Type);

            var childReply = dialog.CreateNode(DialogNodeType.Reply);
            childReply!.Text.Add(0, "Child Reply");
            dialog.AddNodeInternal(childReply, childReply.Type);

            _clipboardService.CopyNode(childReply, dialog);

            // Act
            var result = _clipboardService.PasteAsLink(dialog, parentReply);

            // Assert - Should return null because Reply under Reply is invalid
            Assert.Null(result);
        }

        [Fact]
        public void PasteAsLink_EntryUnderReply_Succeeds()
        {
            // Arrange - Entry under Reply is valid (alternating pattern)
            var dialog = new Dialog();

            var parentReply = dialog.CreateNode(DialogNodeType.Reply);
            parentReply!.Text.Add(0, "Parent Reply");
            dialog.AddNodeInternal(parentReply, parentReply.Type);

            var childEntry = dialog.CreateNode(DialogNodeType.Entry);
            childEntry!.Text.Add(0, "Child Entry");
            dialog.AddNodeInternal(childEntry, childEntry.Type);

            _clipboardService.CopyNode(childEntry, dialog);

            // Act
            var result = _clipboardService.PasteAsLink(dialog, parentReply);

            // Assert - Should succeed
            Assert.NotNull(result);
            Assert.True(result.IsLink);
            Assert.Equal(childEntry, result.Node);
        }

        [Fact]
        public void PasteAsLink_ReplyUnderEntry_Succeeds()
        {
            // Arrange - Reply under Entry is valid (alternating pattern)
            var dialog = new Dialog();

            var parentEntry = dialog.CreateNode(DialogNodeType.Entry);
            parentEntry!.Text.Add(0, "Parent Entry");
            dialog.AddNodeInternal(parentEntry, parentEntry.Type);

            var childReply = dialog.CreateNode(DialogNodeType.Reply);
            childReply!.Text.Add(0, "Child Reply");
            dialog.AddNodeInternal(childReply, childReply.Type);

            _clipboardService.CopyNode(childReply, dialog);

            // Act
            var result = _clipboardService.PasteAsLink(dialog, parentEntry);

            // Assert - Should succeed
            Assert.NotNull(result);
            Assert.True(result.IsLink);
            Assert.Equal(childReply, result.Node);
        }

        #endregion

        #region TreeView Display Tests

        [Fact]
        public void TreeViewSafeLinkNode_EmptyText_UsesContinuePrefix()
        {
            // Arrange
            var node = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            node.Text.Add(0, ""); // Empty text

            var pointer = new DialogPtr
            {
                Node = node,
                Type = DialogNodeType.Entry,
                IsLink = true
            };

            var linkNode = new TreeViewSafeLinkNode(node, 0, "Link", pointer);

            // Assert - Empty text should show as [CONTINUE] with speaker prefix
            Assert.Contains("[CONTINUE]", linkNode.DisplayText);
            Assert.Contains("[Owner]", linkNode.DisplayText); // Entry without speaker = Owner
        }

        [Fact]
        public void TreeViewSafeLinkNode_PCReplyEmpty_ShowsPCPrefix()
        {
            // Arrange
            var node = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Speaker = "", // PC (no speaker)
                Text = new LocString()
            };
            node.Text.Add(0, ""); // Empty text

            var pointer = new DialogPtr
            {
                Node = node,
                Type = DialogNodeType.Reply,
                IsLink = true
            };

            var linkNode = new TreeViewSafeLinkNode(node, 0, "Link", pointer);

            // Assert - PC reply with empty text should show [PC] prefix
            Assert.Contains("[PC]", linkNode.DisplayText);
        }

        [Fact]
        public void TreeViewSafeLinkNode_WithSpeaker_ShowsSpeakerPrefix()
        {
            // Arrange
            var node = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Speaker = "merchant",
                Text = new LocString()
            };
            node.Text.Add(0, "Hello traveler");

            var pointer = new DialogPtr
            {
                Node = node,
                Type = DialogNodeType.Entry,
                IsLink = true
            };

            var linkNode = new TreeViewSafeLinkNode(node, 0, "Link", pointer);

            // Assert - Should show speaker prefix
            Assert.Contains("[merchant]", linkNode.DisplayText);
            Assert.Contains("Hello traveler", linkNode.DisplayText);
        }

        #endregion
    }
}

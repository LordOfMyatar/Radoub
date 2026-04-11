using DialogEditor.Models;

namespace DialogEditor.Tests
{
    public class TreeSelectionKeyTests
    {
        [Fact]
        public void FromDialogNode_Entry_ExtractsCorrectKey()
        {
            var dialog = new Dialog();
            var node = new DialogNode { Type = DialogNodeType.Entry };
            dialog.Entries.Add(node);

            var key = TreeSelectionKey.FromDialogNode(node, dialog, focusedField: null, cursorPosition: null);

            Assert.NotNull(key);
            Assert.Equal(0, key.NodeIndex);
            Assert.True(key.IsEntry);
            Assert.Null(key.FocusedFieldName);
            Assert.Null(key.CursorPosition);
        }

        [Fact]
        public void FromDialogNode_Reply_ExtractsCorrectKey()
        {
            var dialog = new Dialog();
            var node = new DialogNode { Type = DialogNodeType.Reply };
            dialog.Replies.Add(node);

            var key = TreeSelectionKey.FromDialogNode(node, dialog, focusedField: null, cursorPosition: null);

            Assert.NotNull(key);
            Assert.Equal(0, key.NodeIndex);
            Assert.False(key.IsEntry);
        }

        [Fact]
        public void FromDialogNode_SecondEntry_ReturnsIndex1()
        {
            var dialog = new Dialog();
            dialog.Entries.Add(new DialogNode { Type = DialogNodeType.Entry });
            var secondNode = new DialogNode { Type = DialogNodeType.Entry };
            dialog.Entries.Add(secondNode);

            var key = TreeSelectionKey.FromDialogNode(secondNode, dialog, focusedField: null, cursorPosition: null);

            Assert.NotNull(key);
            Assert.Equal(1, key.NodeIndex);
        }

        [Fact]
        public void FromDialogNode_WithCursorInfo_CapturesFocusState()
        {
            var dialog = new Dialog();
            var node = new DialogNode { Type = DialogNodeType.Entry };
            dialog.Entries.Add(node);

            var key = TreeSelectionKey.FromDialogNode(node, dialog, focusedField: "TextBox_Text", cursorPosition: 42);

            Assert.NotNull(key);
            Assert.Equal("TextBox_Text", key.FocusedFieldName);
            Assert.Equal(42, key.CursorPosition);
        }

        [Fact]
        public void FromDialogNode_NodeNotInDialog_ReturnsNull()
        {
            var dialog = new Dialog();
            var orphanNode = new DialogNode { Type = DialogNodeType.Entry };

            var key = TreeSelectionKey.FromDialogNode(orphanNode, dialog, focusedField: null, cursorPosition: null);

            Assert.Null(key);
        }

        [Fact]
        public void ClampCursorPosition_TextShorterThanCursor_ClampsToEnd()
        {
            var key = new TreeSelectionKey(0, true, "TextBox_Text", 50);

            var clamped = key.ClampCursorPosition(30);

            Assert.Equal(30, clamped);
        }

        [Fact]
        public void ClampCursorPosition_TextLongerThanCursor_ReturnsOriginal()
        {
            var key = new TreeSelectionKey(0, true, "TextBox_Text", 10);

            var clamped = key.ClampCursorPosition(30);

            Assert.Equal(10, clamped);
        }

        [Fact]
        public void ClampCursorPosition_NoCursor_ReturnsNull()
        {
            var key = new TreeSelectionKey(0, true, null, null);

            var clamped = key.ClampCursorPosition(30);

            Assert.Null(clamped);
        }
    }
}

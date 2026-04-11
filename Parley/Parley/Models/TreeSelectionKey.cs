using System;
using Radoub.Formats.Logging;

namespace DialogEditor.Models
{
    public record TreeSelectionKey(
        int NodeIndex,
        bool IsEntry,
        string? FocusedFieldName,
        int? CursorPosition
    )
    {
        public static TreeSelectionKey? FromDialogNode(
            DialogNode node,
            Dialog dialog,
            string? focusedField,
            int? cursorPosition)
        {
            int index = dialog.GetNodeIndex(node, node.Type);
            if (index < 0)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"TreeSelectionKey: Node not found in dialog (Type={node.Type})");
                return null;
            }

            return new TreeSelectionKey(
                index,
                node.Type == DialogNodeType.Entry,
                focusedField,
                cursorPosition);
        }

        public int? ClampCursorPosition(int textLength)
        {
            if (CursorPosition == null) return null;
            return Math.Min(CursorPosition.Value, textLength);
        }
    }
}

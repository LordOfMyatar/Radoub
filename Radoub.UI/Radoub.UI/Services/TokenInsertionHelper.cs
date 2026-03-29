using Avalonia.Controls;

namespace Radoub.UI.Services;

public static class TokenInsertionHelper
{
    public record InsertionResult(string NewText, int NewCaretPosition);

    /// <summary>
    /// Compute the new text and caret position after inserting a token.
    /// Pure function -- no UI dependencies, fully testable.
    /// </summary>
    public static InsertionResult ComputeInsertion(
        string currentText, int selectionStart, int selectionLength, string token)
    {
        var needsSpace = selectionStart > 0
            && selectionLength == 0
            && !char.IsWhiteSpace(currentText[selectionStart - 1]);

        var prefix = needsSpace ? " " : "";
        var insertText = prefix + token;

        var before = currentText[..selectionStart];
        var afterStart = selectionStart + selectionLength;

        // If we added a leading space and the character after insertion is also a space,
        // consume the trailing space to avoid double-spacing.
        if (needsSpace && afterStart < currentText.Length && currentText[afterStart] == ' ')
        {
            afterStart++;
        }

        var after = currentText[afterStart..];
        var newText = before + insertText + after;
        var newCaret = selectionStart + insertText.Length;

        return new InsertionResult(newText, newCaret);
    }

    /// <summary>
    /// Insert a token into a TextBox at its current cursor position.
    /// Call from UI thread.
    /// </summary>
    public static void InsertToken(TextBox textBox, string token)
    {
        var result = ComputeInsertion(
            textBox.Text ?? "",
            textBox.SelectionStart,
            textBox.SelectionEnd - textBox.SelectionStart,
            token);

        textBox.Text = result.NewText;
        textBox.CaretIndex = result.NewCaretPosition;
        textBox.Focus();
    }

    // TODO: OpenTokenWindow will be added after TokenInsertionWindow is created (Task 5)
}

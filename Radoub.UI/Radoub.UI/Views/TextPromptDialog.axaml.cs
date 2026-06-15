using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Radoub.UI.Views;

/// <summary>
/// Minimal single-line text prompt (OK/Cancel). Used by the palette editor for category add/rename,
/// where the value is a free-form category name (not an Aurora filename, so no filename validation).
/// </summary>
public partial class TextPromptDialog : Window
{
    /// <summary>The entered text if confirmed, or null if cancelled.</summary>
    public string? Result { get; private set; }

    public TextPromptDialog()
    {
        InitializeComponent();
    }

    /// <summary>Show the prompt and return the entered text, or null if cancelled.</summary>
    public static async Task<string?> ShowAsync(Window owner, string title, string prompt, string initial = "")
    {
        var dialog = new TextPromptDialog();
        dialog.Title = title;
        dialog.PromptLabel.Text = prompt;
        dialog.InputBox.Text = initial;
        dialog.InputBox.SelectAll();
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnOkClick(sender, e);
        else if (e.Key == Key.Escape) OnCancelClick(sender, e);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var text = InputBox.Text?.Trim();
        Result = string.IsNullOrEmpty(text) ? null : text;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}

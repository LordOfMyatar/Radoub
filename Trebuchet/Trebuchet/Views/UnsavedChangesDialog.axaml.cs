using Avalonia.Controls;
using Avalonia.Interactivity;
using RadoubLauncher.Services;

namespace RadoubLauncher.Views;

/// <summary>
/// Three-way Save / Discard / Cancel confirmation shown when the user closes the
/// window with unsaved editor changes (#2453). A destructive-action confirmation
/// is the sanctioned exception to the non-modal rule.
/// </summary>
public partial class UnsavedChangesDialog : Window
{
    /// <summary>The user's choice. Defaults to Cancel so a closed-via-X dialog aborts the close.</summary>
    public ClosePromptResult Result { get; private set; } = ClosePromptResult.Cancel;

    public UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    public UnsavedChangesDialog(string message) : this()
    {
        MessageText.Text = message;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        Result = ClosePromptResult.Save;
        Close();
    }

    private void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        Result = ClosePromptResult.Discard;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = ClosePromptResult.Cancel;
        Close();
    }
}

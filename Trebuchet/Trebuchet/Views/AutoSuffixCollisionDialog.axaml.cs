using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RadoubLauncher.Views;

/// <summary>
/// Result returned by the auto-suffix collision confirmation dialog.
/// See spec Section 6 (validator returns AutoSuffixApplied=true).
/// </summary>
public enum AutoSuffixDialogResult
{
    /// <summary>User accepted the auto-suffixed name; proceed with rename.</summary>
    Continue,

    /// <summary>User wants to pick a different name; abort and ask them to retry.</summary>
    PickAnother,

    /// <summary>User cancelled the entire rename operation.</summary>
    Cancel
}

/// <summary>
/// Confirmation dialog shown when ResRefValidator auto-suffixes a colliding ResRef
/// (e.g., "louis" already exists, validator returns "louis_2"). User picks among:
/// Continue (use suffixed name), Pick a different name (retry), Cancel (abort).
/// </summary>
public partial class AutoSuffixCollisionDialog : Window
{
    public AutoSuffixDialogResult Result { get; private set; } = AutoSuffixDialogResult.Cancel;

    public AutoSuffixCollisionDialog()
    {
        InitializeComponent();
    }

    public AutoSuffixCollisionDialog(string originalProposedName, string actualName, string sourceFilePath)
        : this()
    {
        var ext = Path.GetExtension(sourceFilePath);
        HeaderText.Text = $"\"{originalProposedName}{ext}\" already exists.";
        DetailText.Text =
            $"To avoid a collision, the file would be renamed to \"{actualName}{ext}\" instead.\n\n" +
            "Continue with the auto-suffixed name, pick a different name, or cancel?";
    }

    private void OnContinueClick(object? sender, RoutedEventArgs e)
    {
        Result = AutoSuffixDialogResult.Continue;
        Close();
    }

    private void OnPickAnotherClick(object? sender, RoutedEventArgs e)
    {
        Result = AutoSuffixDialogResult.PickAnother;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = AutoSuffixDialogResult.Cancel;
        Close();
    }
}

using System;
using Avalonia.Controls;
using Radoub.UI.Utils;
using Radoub.UI.Views;
using System.Threading.Tasks;
using SharedDialogHelper = Radoub.UI.Services.DialogHelper;

namespace Quartermaster.Views.Helpers;

/// <summary>
/// Static helper class for creating common dialogs.
/// Delegates to shared DialogHelper from Radoub.UI with Quartermaster-specific helpers.
/// </summary>
public static class DialogHelper
{
    /// <summary>
    /// Shows a dialog asking user what to do with unsaved changes.
    /// </summary>
    /// <param name="parent">Parent window for centering</param>
    /// <returns>"Save", "Discard", or "Cancel"</returns>
    public static Task<string> ShowUnsavedChangesDialog(Window parent)
        => SharedDialogHelper.ShowUnsavedChangesAsync(parent);

    /// <summary>
    /// Shows an informational message dialog with a title and message (non-modal).
    /// Supports longer messages with text wrapping.
    /// </summary>
    public static void ShowMessageDialog(Window parent, string title, string message)
        => SharedDialogHelper.ShowMessage(parent, title, message);

    /// <summary>
    /// Shows an error dialog with a title and message (non-modal).
    /// </summary>
    public static void ShowErrorDialog(Window parent, string title, string message)
        => SharedDialogHelper.ShowError(parent, title, message);

    /// <summary>
    /// Shows the About dialog for Quartermaster.
    /// Uses shared AboutWindow from Radoub.UI.
    /// </summary>
    public static void ShowAboutDialog(Window parent)
    {
        var aboutWindow = AboutWindow.Create(new AboutWindowConfig
        {
            ToolName = "Quartermaster",
            Subtitle = "Creature and Inventory Editor for Neverwinter Nights",
            Version = VersionHelper.GetVersion(),
            IconBitmap = new Avalonia.Media.Imaging.Bitmap(
                Avalonia.Platform.AssetLoader.Open(
                    new Uri("avares://Quartermaster/Assets/Quartermaster.ico")))
        });
        aboutWindow.Show(parent);
    }

    /// <summary>
    /// Shows an OK/Cancel confirmation dialog with a warning icon.
    /// </summary>
    /// <param name="parent">Parent window for centering</param>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    /// <returns>True if user clicked OK, false otherwise</returns>
    public static Task<bool> ShowConfirmationDialog(Window parent, string title, string message)
        => SharedDialogHelper.ShowWarningConfirmAsync(parent, title, message);
}

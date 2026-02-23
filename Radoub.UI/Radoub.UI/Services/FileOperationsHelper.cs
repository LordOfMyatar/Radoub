using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace Radoub.UI.Services;

/// <summary>
/// Result of a dirty-check prompt.
/// </summary>
public enum DirtyCheckResult
{
    /// <summary>User chose to save first (caller should save, then proceed).</summary>
    Save,
    /// <summary>User chose to discard changes (caller should proceed without saving).</summary>
    Discard,
    /// <summary>User cancelled the operation (caller should abort).</summary>
    Cancel,
    /// <summary>Document was not dirty - safe to proceed immediately.</summary>
    Clean
}

/// <summary>
/// Helper for common file operation workflows shared across Radoub tools.
/// Provides the "check dirty before destructive action" pattern that
/// eliminates duplicated code in New/Open/Close handlers.
///
/// Usage:
///   var result = await FileOperationsHelper.CheckDirtyAsync(parentWindow, isDirty);
///   if (result == DirtyCheckResult.Cancel) return;
///   if (result == DirtyCheckResult.Save) await SaveFile();
///   // proceed with the destructive action (new/open/close)
/// </summary>
public static class FileOperationsHelper
{
    /// <summary>
    /// Checks if the document is dirty and prompts the user if so.
    /// Returns the user's choice or Clean if no prompt was needed.
    /// </summary>
    /// <param name="parent">Parent window for dialog centering</param>
    /// <param name="isDirty">Whether the document has unsaved changes</param>
    /// <returns>The result indicating what the caller should do</returns>
    public static async Task<DirtyCheckResult> CheckDirtyAsync(Window parent, bool isDirty)
    {
        if (!isDirty)
            return DirtyCheckResult.Clean;

        var result = await DialogHelper.ShowUnsavedChangesAsync(parent);

        return result switch
        {
            "Save" => DirtyCheckResult.Save,
            "Discard" => DirtyCheckResult.Discard,
            _ => DirtyCheckResult.Cancel
        };
    }

    /// <summary>
    /// Checks if the document is dirty using an IDocumentState instance.
    /// </summary>
    /// <param name="parent">Parent window for dialog centering</param>
    /// <param name="documentState">The document state tracker</param>
    /// <returns>The result indicating what the caller should do</returns>
    public static Task<DirtyCheckResult> CheckDirtyAsync(Window parent, IDocumentState documentState)
    {
        return CheckDirtyAsync(parent, documentState.IsDirty);
    }

    /// <summary>
    /// Handles the window closing event with dirty-check logic.
    /// This is the standard pattern for OnWindowClosing handlers.
    ///
    /// Usage in OnWindowClosing:
    ///   var shouldClose = await FileOperationsHelper.HandleClosingAsync(
    ///       this, e, _isDirty, async () => await SaveFile());
    ///   if (shouldClose) { /* cleanup */ }
    /// </summary>
    /// <param name="window">The window being closed</param>
    /// <param name="args">The closing event args (will be cancelled if user chooses Save or Cancel)</param>
    /// <param name="isDirty">Whether the document has unsaved changes</param>
    /// <param name="saveAction">Action to execute if user chooses Save. Should return true if save succeeded.</param>
    /// <returns>True if the window should proceed with closing, false if cancelled</returns>
    public static async Task<bool> HandleClosingAsync(
        Window window,
        WindowClosingEventArgs args,
        bool isDirty,
        Func<Task<bool>>? saveAction = null)
    {
        if (!isDirty)
            return true;

        args.Cancel = true;

        var result = await CheckDirtyAsync(window, isDirty);

        switch (result)
        {
            case DirtyCheckResult.Save:
                if (saveAction != null)
                {
                    var saved = await saveAction();
                    if (!saved) return false; // Save failed, don't close
                }
                return true;

            case DirtyCheckResult.Discard:
                return true;

            default: // Cancel
                return false;
        }
    }
}

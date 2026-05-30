using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Radoub.UI.Services;

namespace Radoub.UI.Views;

/// <summary>
/// Dialog for renaming Aurora Engine resource files.
/// Validates names against Aurora Engine constraints and checks for duplicates.
/// </summary>
public partial class RenameDialog : Window
{
    private string _directory = "";
    private string _extension = "";
    private string _currentName = "";

    /// <summary>
    /// The new filename (without extension) if user confirmed, null if cancelled.
    /// </summary>
    public string? NewName { get; private set; }

    public RenameDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the rename/copy name dialog and returns the chosen filename if confirmed.
    /// </summary>
    /// <param name="owner">Parent window</param>
    /// <param name="currentName">Current filename without extension</param>
    /// <param name="directory">Directory containing the file</param>
    /// <param name="extension">File extension including dot (e.g., ".dlg")</param>
    /// <param name="actionLabel">Confirm-button label and dialog verb. "Rename"
    /// (default) or "Copy" — the Copy flow reuses this validated-input dialog
    /// (#2320) so both actions share one source of name validation.</param>
    /// <param name="allowUnchanged">When true (Copy), an unchanged-from-source
    /// name is permitted as long as no duplicate file exists; when false
    /// (Rename), an unchanged name is rejected as a no-op.</param>
    /// <returns>New filename without extension, or null if cancelled</returns>
    public static async Task<string?> ShowAsync(
        Window owner, string currentName, string directory, string extension,
        string actionLabel = "Rename", bool allowUnchanged = false)
    {
        var dialog = new RenameDialog();
        dialog.Configure(currentName, directory, extension, actionLabel, allowUnchanged);
        await dialog.ShowDialog(owner);
        return dialog.NewName;
    }

    private string _actionLabel = "Rename";
    private bool _allowUnchanged;

    private void Configure(string currentName, string directory, string extension,
        string actionLabel, bool allowUnchanged)
    {
        _currentName = currentName;
        _directory = directory;
        _extension = extension;
        _actionLabel = actionLabel;
        _allowUnchanged = allowUnchanged;

        Title = $"{actionLabel} {extension.TrimStart('.')} File";
        RenameButton.Content = actionLabel;
        CurrentNameBox.Text = currentName;
        NewNameBox.Text = currentName;
        NewNameBox.SelectAll();

        if (allowUnchanged)
        {
            // Copy flow: the "you must update references" rename warning doesn't apply.
            WarningText.Text = "This creates a duplicate file. The copy starts with the " +
                "same contents as the source; edit it after copying.";
        }

        UpdateValidation();
    }

    private void OnNewNameChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateValidation();
    }

    private void OnNewNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && RenameButton.IsEnabled)
        {
            OnRenameClick(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            OnCancelClick(sender, e);
        }
    }

    private void UpdateValidation()
    {
        var newName = NewNameBox.Text?.Trim() ?? "";

        // Update character count
        var count = newName.Length;
        CharCountText.Text = $"{count} / {AuroraFilenameValidator.MaxFilenameLength} characters";

        if (count > AuroraFilenameValidator.MaxFilenameLength)
        {
            CharCountText.Foreground = BrushManager.GetErrorBrush();
        }
        else
        {
            CharCountText.Foreground = this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush
                ?? BrushManager.GetDisabledBrush(); // theme-ok: fallback when resource unavailable
        }

        // Check if unchanged. For Rename this is a no-op and is rejected.
        // For Copy (allowUnchanged) the same stem in the same directory still
        // collides, so let the duplicate-file check below handle it.
        if (!_allowUnchanged && newName.Equals(_currentName, StringComparison.OrdinalIgnoreCase))
        {
            ShowValidation("Name is unchanged.", isError: false);
            RenameButton.IsEnabled = false;
            return;
        }

        // Validate against Aurora Engine rules
        var result = AuroraFilenameValidator.Validate(newName);
        if (!result.IsValid)
        {
            ShowValidation(result.GetErrorMessage(), isError: true);
            RenameButton.IsEnabled = false;
            return;
        }

        // Check for duplicate file
        var newPath = Path.Combine(_directory, newName + _extension);
        if (File.Exists(newPath))
        {
            ShowValidation($"A file named \"{newName}{_extension}\" already exists in this directory.", isError: true);
            RenameButton.IsEnabled = false;
            return;
        }

        // All good
        HideValidation();
        RenameButton.IsEnabled = true;
    }

    private void ShowValidation(string message, bool isError)
    {
        ValidationText.Text = message;
        ValidationBorder.IsVisible = true;

        if (isError)
        {
            ValidationBorder.Background = BrushManager.GetErrorBrush();
        }
        else
        {
            ValidationBorder.Background = this.FindResource("SystemControlBackgroundBaseLowBrush") as IBrush
                ?? BrushManager.GetDisabledBrush(); // theme-ok: fallback when resource unavailable
        }
    }

    private void HideValidation()
    {
        ValidationBorder.IsVisible = false;
    }

    private void OnRenameClick(object? sender, RoutedEventArgs e)
    {
        NewName = NewNameBox.Text?.Trim();
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        NewName = null;
        Close();
    }
}

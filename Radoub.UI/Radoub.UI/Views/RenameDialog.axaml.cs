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
    /// Shows the rename dialog and returns the new filename if confirmed.
    /// </summary>
    /// <param name="owner">Parent window</param>
    /// <param name="currentName">Current filename without extension</param>
    /// <param name="directory">Directory containing the file</param>
    /// <param name="extension">File extension including dot (e.g., ".dlg")</param>
    /// <returns>New filename without extension, or null if cancelled</returns>
    public static async Task<string?> ShowAsync(Window owner, string currentName, string directory, string extension)
    {
        var dialog = new RenameDialog();
        dialog.Configure(currentName, directory, extension);
        await dialog.ShowDialog(owner);
        return dialog.NewName;
    }

    private void Configure(string currentName, string directory, string extension)
    {
        _currentName = currentName;
        _directory = directory;
        _extension = extension;

        Title = $"Rename {extension.TrimStart('.')} File";
        CurrentNameBox.Text = currentName;
        NewNameBox.Text = currentName;
        NewNameBox.SelectAll();

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
            CharCountText.Foreground = Brushes.Red;
        }
        else
        {
            CharCountText.Foreground = this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush ?? Brushes.Gray;
        }

        // Check if unchanged
        if (newName.Equals(_currentName, StringComparison.OrdinalIgnoreCase))
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
            ValidationBorder.Background = new SolidColorBrush(Color.FromRgb(200, 50, 50));
        }
        else
        {
            ValidationBorder.Background = this.FindResource("SystemControlBackgroundBaseLowBrush") as IBrush
                ?? new SolidColorBrush(Color.FromRgb(100, 100, 100));
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

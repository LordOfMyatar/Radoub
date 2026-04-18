using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Radoub.UI.Services;

namespace Radoub.UI.Views;

/// <summary>
/// Dialog shown when copying an archive resource (BIF/HAK) into the module directory.
/// Lets the user edit the new ResRef, Tag, and Name before the copy is written.
/// Parley and other formats without Tag/Name pass showTagAndName: false to get a
/// ResRef-only variant.
/// </summary>
public partial class CopyToModuleDialog : Window
{
    private string _originalResRef = "";
    private string _moduleDirectory = "";
    private string _extension = "";
    private bool _showTagAndName = true;

    /// <summary>
    /// The user's choices if they confirmed the copy; null if they cancelled.
    /// </summary>
    public CopyToModuleResult? Result { get; private set; }

    public CopyToModuleDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Show the dialog and return the user's choices, or null if cancelled.
    /// </summary>
    /// <param name="owner">Parent window.</param>
    /// <param name="currentResRef">ResRef of the source resource.</param>
    /// <param name="currentTag">Tag of the source resource (ignored when showTagAndName is false).</param>
    /// <param name="currentName">Display name of the source resource (ignored when showTagAndName is false).</param>
    /// <param name="moduleDirectory">Directory the copy will be written to (used for duplicate detection).</param>
    /// <param name="extension">File extension including the dot (e.g. ".utm", ".dlg").</param>
    /// <param name="showTagAndName">True for formats with editable Tag/Name (UTM/UTC/UTI); false for ResRef-only (DLG).</param>
    public static async Task<CopyToModuleResult?> ShowAsync(
        Window owner,
        string currentResRef,
        string currentTag,
        string currentName,
        string moduleDirectory,
        string extension,
        bool showTagAndName = true)
    {
        var dialog = new CopyToModuleDialog();
        dialog.Configure(currentResRef, currentTag, currentName, moduleDirectory, extension, showTagAndName);
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }

    private void Configure(string currentResRef, string currentTag, string currentName,
        string moduleDirectory, string extension, bool showTagAndName)
    {
        _originalResRef = currentResRef ?? "";
        _moduleDirectory = moduleDirectory ?? "";
        _extension = extension ?? "";
        _showTagAndName = showTagAndName;

        var extLabel = _extension.TrimStart('.').ToUpperInvariant();
        Title = showTagAndName
            ? $"Copy {extLabel} to Module"
            : $"Copy {extLabel} to Module (ResRef only)";

        HeaderText.Text = $"Copy \"{currentResRef}{_extension}\" to module";

        ResRefBox.Text = currentResRef;
        ResRefBox.SelectAll();
        TagBox.Text = currentTag ?? "";
        NameBox.Text = currentName ?? "";

        TagPanel.IsVisible = showTagAndName;
        TagCharCountText.IsVisible = showTagAndName;
        NamePanel.IsVisible = showTagAndName;
        InfoTagLine.IsVisible = showTagAndName;

        UpdateValidation();
    }

    private void OnFieldChanged(object? sender, TextChangedEventArgs e) => UpdateValidation();

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && CopyButton.IsEnabled)
        {
            OnCopyClick(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            OnCancelClick(sender, e);
        }
    }

    private void UpdateValidation()
    {
        var resRef = ResRefBox.Text?.Trim() ?? "";
        var tag = _showTagAndName ? TagBox.Text ?? "" : null;

        // ResRef char count
        ResRefCharCountText.Text = $"{resRef.Length} / {AuroraFilenameValidator.MaxFilenameLength} characters";
        ResRefCharCountText.Foreground = resRef.Length > AuroraFilenameValidator.MaxFilenameLength
            ? BrushManager.GetErrorBrush()
            : (this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush ?? BrushManager.GetDisabledBrush());

        // Tag char count (only meaningful when shown)
        if (_showTagAndName)
        {
            var tagLen = tag?.Length ?? 0;
            TagCharCountText.Text = $"{tagLen} / {CopyToModuleValidator.MaxTagLength} characters";
            TagCharCountText.Foreground = tagLen > CopyToModuleValidator.MaxTagLength
                ? BrushManager.GetErrorBrush()
                : (this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush ?? BrushManager.GetDisabledBrush());
        }

        var result = CopyToModuleValidator.Validate(
            resRef, tag, _originalResRef, _moduleDirectory, _extension, _showTagAndName);

        if (result.State == CopyToModuleValidationState.Valid)
        {
            HideValidation();
            CopyButton.IsEnabled = true;
            return;
        }

        if (result.State == CopyToModuleValidationState.Unchanged)
        {
            // Soft state — the user may legitimately want to copy with the same resref
            // if they're copying into a different context, but duplicate check already
            // forbids writing over an existing file in this directory. So allow if no dup.
            ShowValidation(result.Message ?? "", isError: false);
            CopyButton.IsEnabled = true;
            return;
        }

        ShowValidation(result.Message ?? "Invalid input.", isError: true);
        CopyButton.IsEnabled = false;
    }

    private void ShowValidation(string message, bool isError)
    {
        ValidationText.Text = message;
        ValidationBorder.IsVisible = true;
        ValidationBorder.Background = isError
            ? BrushManager.GetErrorBrush()
            : (this.FindResource("SystemControlBackgroundBaseLowBrush") as IBrush ?? BrushManager.GetDisabledBrush());
    }

    private void HideValidation() => ValidationBorder.IsVisible = false;

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        var newResRef = ResRefBox.Text?.Trim() ?? "";
        Result = new CopyToModuleResult(
            NewResRef: newResRef,
            NewTag: _showTagAndName ? (TagBox.Text ?? "") : null,
            NewName: _showTagAndName ? (NameBox.Text ?? "") : null);
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}

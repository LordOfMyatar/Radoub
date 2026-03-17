using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Radoub.Formats.Settings;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 1: File Type selection (UTC/BIC), starting level, and save location.
/// </summary>
public partial class NewCharacterWizardWindow
{
    #region Step 1: File Type

    private void OnUtcCardClick(object? sender, PointerPressedEventArgs e)
    {
        _isBicFile = false;
        UpdateFileTypeCards();
    }

    private void OnBicCardClick(object? sender, PointerPressedEventArgs e)
    {
        _isBicFile = true;
        UpdateFileTypeCards();
    }

    private void UpdateFileTypeCards()
    {
        _utcCard.Classes.Clear();
        _utcCard.Classes.Add("file-type-card");
        if (!_isBicFile)
            _utcCard.Classes.Add("selected");

        _bicCard.Classes.Clear();
        _bicCard.Classes.Add("file-type-card");
        if (_isBicFile)
            _bicCard.Classes.Add("selected");

        // Show default scripts option for UTC only
        _defaultScriptsPanel.IsVisible = !_isBicFile;

        // Clear save location when file type changes (extension changes)
        ChosenSavePath = null;
        _saveLocationTextBox.Text = "";

        UpdateSidebarSummary();
    }

    private void OnStartingLevelChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        _startingLevel = (int)(e.NewValue ?? 1);
        if (_startingLevel < 1) _startingLevel = 1;

        if (_startingLevel == 1)
        {
            _startingLevelNote.Text = "Level 1 character.";
            _startingLevelNote.ClearValue(Avalonia.Controls.TextBlock.ForegroundProperty);
        }
        else
        {
            _startingLevelNote.Text = $"⚠ Level {_startingLevel} — single class only. The Level Up Wizard will open after creation to complete {_startingLevel - 1} level(s) in one pass.";
            _startingLevelNote.Foreground = Radoub.UI.Services.BrushManager.GetWarningBrush(this);
        }
    }

    private async void OnBrowseSaveLocationClick(object? sender, RoutedEventArgs e)
    {
        var extension = _isBicFile ? "bic" : "utc";
        var title = _isBicFile ? "Save Player Character" : "Save Creature Blueprint";

        IStorageFolder? suggestedFolder = null;
        try
        {
            if (_isBicFile)
            {
                var nwnPath = RadoubSettings.Instance.NeverwinterNightsPath;
                if (!string.IsNullOrEmpty(nwnPath))
                {
                    var localVault = Path.Combine(nwnPath, "localvault");
                    if (Directory.Exists(localVault))
                        suggestedFolder = await StorageProvider.TryGetFolderFromPathAsync(localVault);
                }
            }
            else
            {
                var modulePath = RadoubSettings.Instance.CurrentModulePath;
                if (!string.IsNullOrEmpty(modulePath))
                {
                    // Unpacked module directory — save directly into it
                    if (Directory.Exists(modulePath))
                        suggestedFolder = await StorageProvider.TryGetFolderFromPathAsync(modulePath);
                    // .mod file — suggest the parent directory
                    else if (File.Exists(modulePath))
                    {
                        var parentDir = Path.GetDirectoryName(modulePath);
                        if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                            suggestedFolder = await StorageProvider.TryGetFolderFromPathAsync(parentDir);
                    }
                }
            }
        }
        catch { /* fallback to no suggestion */ }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = extension,
            FileTypeChoices = _isBicFile
                ? new[] { new FilePickerFileType("Player Character") { Patterns = new[] { "*.bic" } } }
                : new[] { new FilePickerFileType("Creature Blueprint") { Patterns = new[] { "*.utc" } } },
            SuggestedFileName = "new_creature",
            SuggestedStartLocation = suggestedFolder
        });

        if (file != null)
        {
            ChosenSavePath = file.Path.LocalPath;
            _saveLocationTextBox.Text = ChosenSavePath;
        }
    }

    #endregion
}

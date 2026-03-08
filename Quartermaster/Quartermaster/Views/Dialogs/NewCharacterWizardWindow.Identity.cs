using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Quartermaster.Services;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 3: Identity - Name, portrait, voice set, age, description, filename validation.
/// </summary>
public partial class NewCharacterWizardWindow
{
    #region Step 3: Identity

    private bool _step3IdentityLoaded;

    private void PrepareStep3()
    {
        // Update Age visibility (BIC only)
        _identityAgeLabelText.IsVisible = _isBicFile;
        _identityAgeNumericUpDown.IsVisible = _isBicFile;
        _identityAgeNote.IsVisible = _isBicFile;

        // Tag visibility (UTC only — BIC tags are set by engine)
        _identityTagLabelText.IsVisible = !_isBicFile;
        _identityGeneratedTagLabel.IsVisible = !_isBicFile;

        // UTC-only fields visibility
        _identityUtcFieldsPanel.IsVisible = !_isBicFile;

        if (_step3IdentityLoaded) return;
        _step3IdentityLoaded = true;

        // Set default portrait based on gender
        var defaultPortraitResRef = _selectedGender == 0 ? "hu_m_99_" : "hu_f_99_";
        var defaultPortraitId = _displayService.FindPortraitIdByResRef(defaultPortraitResRef);
        if (defaultPortraitId.HasValue)
            _selectedPortraitId = defaultPortraitId.Value;
        UpdatePortraitDisplay();
    }

    private void OnIdentityNameChanged(object? sender, TextChangedEventArgs e)
    {
        _characterName = _identityFirstNameTextBox.Text?.Trim() ?? "";

        var sanitized = SanitizeForResRef(_characterName);
        var resRef = string.IsNullOrEmpty(sanitized) ? "new_creature" : sanitized;
        _identityGeneratedTagLabel.Text = resRef;
        _identityGeneratedResRefLabel.Text = resRef;

        // Filename validation (#1595)
        ValidateFilename(resRef);
    }

    private void ValidateFilename(string resRef)
    {
        if (string.IsNullOrEmpty(resRef) || resRef == "new_creature")
        {
            _identityFilenameWarning.IsVisible = false;
            return;
        }

        if (resRef.Length > 16)
        {
            _identityFilenameWarning.IsVisible = true;
            _identityFilenameWarning.Text =
                $"Filename will be truncated to 16 characters: \"{resRef[..16]}\" " +
                $"(Aurora Engine limit). Current: {resRef.Length} chars.";
            _identityFilenameWarning.Foreground = BrushManager.GetWarningBrush(this);
        }
        else if (resRef != resRef.ToLowerInvariant())
        {
            _identityFilenameWarning.IsVisible = true;
            _identityFilenameWarning.Text = "Filename will be lowercased for Aurora Engine compatibility.";
            _identityFilenameWarning.Foreground = BrushManager.GetDisabledBrush(this);
        }
        else
        {
            _identityFilenameWarning.IsVisible = false;
        }
    }

    private async void OnBrowsePortraitClick(object? sender, RoutedEventArgs e)
    {
        if (_itemIconService == null)
            return;

        var browser = new PortraitBrowserWindow(_gameDataService, _itemIconService);
        browser.SetInitialFilters(_selectedRaceId, _selectedGender);

        var result = await browser.ShowDialog<ushort?>(this);

        if (result.HasValue)
        {
            _selectedPortraitId = result.Value;
            UpdatePortraitDisplay();
        }
    }

    private void UpdatePortraitDisplay()
    {
        var resRef = _displayService.GetPortraitResRef(_selectedPortraitId);
        _identityPortraitNameLabel.Text = resRef ?? $"Portrait {_selectedPortraitId}";

        if (_itemIconService != null && resRef != null)
        {
            var image = _itemIconService.GetPortrait(resRef);
            _identityPortraitPreviewImage.Source = image;
        }
        else
        {
            _identityPortraitPreviewImage.Source = null;
        }
    }

    #endregion
}

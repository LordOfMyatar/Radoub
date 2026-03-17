using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Quartermaster.Services;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step navigation, validation, finish/cancel, and sidebar summary.
/// </summary>
public partial class NewCharacterWizardWindow
{
    #region Step Navigation

    private void UpdateStepDisplay()
    {
        // Update step indicators
        for (int i = 0; i < TotalSteps; i++)
        {
            _stepBorders[i].Classes.Clear();
            _stepBorders[i].Classes.Add("step-indicator");

            if (i + 1 < _currentStep)
                _stepBorders[i].Classes.Add("completed");
            else if (i + 1 == _currentStep)
                _stepBorders[i].Classes.Add("current");
        }

        // Show/hide panels
        for (int i = 0; i < TotalSteps; i++)
        {
            _stepPanels[i].IsVisible = (i + 1 == _currentStep);
        }

        // Button state
        _backButton.IsEnabled = _currentStep > 1;
        _nextButton.IsVisible = _currentStep < TotalSteps;
        _finishButton.IsVisible = _currentStep == TotalSteps;

        ValidateCurrentStep();
        UpdateSidebarSummary();
    }

    private void OnValidationLevelChanged(object? sender, SelectionChangedEventArgs e)
    {
        var level = (ValidationLevel)_validationLevelComboBox.SelectedIndex;
        SettingsService.Instance.ValidationLevel = level;

        // Refresh step-specific UI when validation level changes
        switch (_currentStep)
        {
            case 6 when _step5Loaded:
                UpdateAbilityDisplay(); // Also calls ValidateCurrentStep
                break;
            case 8 when _step7Loaded:
                RenderSkillRows(); // Rebuild buttons with new enabled state; also calls ValidateCurrentStep
                break;
            default:
                ValidateCurrentStep();
                break;
        }
    }

    private void ValidateCurrentStep()
    {
        // Strict validation checks (applies to both Warning and Strict modes for status display)
        bool strictValid = _currentStep switch
        {
            1 => true,
            2 => _selectedRaceId != 255,
            3 => !_isBicFile || _voiceSetSelected,
            4 => true,
            5 => _selectedClassId >= 0 && !IsFamiliarNameRequired(),
            6 => GetAbilityPointsRemaining() == 0 || !_isBicFile,
            7 => IsFeatSelectionComplete(),
            8 => GetSkillPointsRemaining() >= 0,
            9 => !_needsSpellSelection || _isDivineCaster || IsSpellSelectionComplete(),
            10 => true,
            11 => true,
            _ => true
        };

        bool canProceed = _validationLevel switch
        {
            // Chaotic Evil: only require basic selections
            ValidationLevel.None => _currentStep switch
            {
                2 => _selectedRaceId != 255,
                5 => _selectedClassId >= 0,
                _ => true
            },
            // True Neutral: warn but allow proceeding (except hard requirements)
            ValidationLevel.Warning => _currentStep switch
            {
                2 => _selectedRaceId != 255,
                5 => _selectedClassId >= 0 && !IsFamiliarNameRequired(),
                _ => true
            },
            // Lawful Good: enforce all rules
            _ => strictValid
        };

        _nextButton.IsEnabled = canProceed;
        _finishButton.IsEnabled = canProceed;

        // Status message: Warning mode shows yellow warnings, Strict mode shows blocking messages
        if (_validationLevel == ValidationLevel.Warning && !strictValid)
        {
            _statusLabel.Foreground = BrushManager.GetWarningBrush(this);
            _statusLabel.Text = _currentStep switch
            {
                3 when _isBicFile && !_voiceSetSelected => "⚠ No voice set selected. BIC files need a voice set for in-game dialog.",
                5 when IsFamiliarNameRequired() => "⚠ Familiar name is empty.",
                6 => $"⚠ {GetAbilityPointsRemaining()} ability point(s) unspent.",
                7 => $"⚠ {_featsToChoose - _chosenFeatIds.Count} feat(s) not selected.",
                9 => "⚠ Spell selection incomplete.",
                _ => ""
            };
        }
        else if (!canProceed)
        {
            // Voice set uses warning brush (advisory, not an error); other blocks use default foreground
            if (_currentStep == 3 && _isBicFile && !_voiceSetSelected)
                _statusLabel.Foreground = BrushManager.GetWarningBrush(this);
            else
                _statusLabel.ClearValue(Avalonia.Controls.TextBlock.ForegroundProperty);
            _statusLabel.Text = _currentStep switch
            {
                2 => "Select a race to continue.",
                3 => "Select a voice set for your BIC character.",
                5 when _selectedClassId < 0 => "Select a class to continue.",
                5 when IsFamiliarNameRequired() => "Enter a name for your familiar.",
                6 => $"Spend all {_pointBuyTotal} ability points to continue.",
                7 => $"Select {_featsToChoose - _chosenFeatIds.Count} more feat(s) to continue.",
                9 => "Select all required spells to continue.",
                _ => ""
            };
        }
        else
        {
            _statusLabel.ClearValue(Avalonia.Controls.TextBlock.ForegroundProperty);
            _statusLabel.Text = "";
        }
    }

    /// <summary>
    /// Returns true when class grants a familiar but no name has been entered.
    /// Uses class check rather than panel visibility to avoid rendering timing issues.
    /// </summary>
    private bool IsFamiliarNameRequired()
    {
        return _selectedClassId >= 0
            && _displayService.ClassGrantsFamiliar(_selectedClassId)
            && string.IsNullOrWhiteSpace(_familiarNameTextBox.Text);
    }

    private void PrepareCurrentStep()
    {
        switch (_currentStep)
        {
            case 2:
                PrepareStep2();
                break;
            case 3:
                PrepareStep3();
                break;
            case 4:
                PrepareStep4();
                break;
            case 5:
                PrepareStep5();
                break;
            case 6:
                PrepareStep6();
                break;
            case 7:
                PrepareStep7();
                break;
            case 8:
                PrepareStep8();
                break;
            case 9:
                PrepareStep9();
                if (!_needsSpellSelection)
                {
                    _currentStep++;
                    PrepareCurrentStep(); // Skip to step 10
                }
                break;
            case 10:
                PrepareStep10();
                break;
            case 11:
                PrepareStep11();
                break;
        }
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep < TotalSteps)
        {
            _currentStep++;
            PrepareCurrentStep();
            UpdateStepDisplay();
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;

            // Skip spell step when going back if non-caster
            if (_currentStep == 9 && !_needsSpellSelection)
                _currentStep--;

            UpdateStepDisplay();
        }
    }

    private void OnFinishClick(object? sender, RoutedEventArgs e)
    {
        // Read palette ID from UI before building input
        _paletteId = (_identityPaletteIdComboBox.SelectedItem is ComboBoxItem item && item.Tag is byte id) ? id : (byte)1;

        var input = new CharacterCreationService.CharacterCreationInput
        {
            IsBicFile = _isBicFile,
            ApplyDefaultScripts = _defaultScriptsCheckBox.IsChecked == true,
            RaceId = _selectedRaceId,
            Gender = _selectedGender,
            AppearanceId = _selectedAppearanceId,
            Phenotype = _selectedPhenotype,
            PortraitId = _selectedPortraitId,
            BodyParts = new CharacterCreationService.BodyPartVariations
            {
                Head = _headVariation,
                Neck = _neckVariation,
                Torso = _torsoVariation,
                Pelvis = _pelvisVariation,
                Belt = _beltVariation,
                LShoulder = _lShoulVariation,
                RShoulder = _rShoulVariation,
                LBicep = _lBicepVariation,
                RBicep = _rBicepVariation,
                LForearm = _lFArmVariation,
                RForearm = _rFArmVariation,
                LHand = _lHandVariation,
                RHand = _rHandVariation,
                LThigh = _lThighVariation,
                RThigh = _rThighVariation,
                LShin = _lShinVariation,
                RShin = _rShinVariation,
                LFoot = _lFootVariation,
                RFoot = _rFootVariation
            },
            Colors = new CharacterCreationService.ColorSelections
            {
                Skin = _skinColor,
                Hair = _hairColor,
                Tattoo1 = _tattoo1Color,
                Tattoo2 = _tattoo2Color
            },
            ClassId = _selectedClassId,
            PackageId = _selectedPackageId,
            GoodEvil = _selectedGoodEvil,
            LawChaos = _selectedLawChaos,
            Domain1 = GetSelectedDomainId(_domain1ComboBox),
            Domain2 = GetSelectedDomainId(_domain2ComboBox),
            FamiliarType = GetSelectedFamiliarType(),
            FamiliarName = _familiarNameTextBox.Text ?? "",
            AbilityBaseScores = new Dictionary<string, int>(_abilityBaseScores),
            ChosenFeatIds = new List<int>(_chosenFeatIds),
            SkillRanksAllocated = new Dictionary<int, int>(_skillRanksAllocated),
            SelectedSpellsByLevel = _selectedSpellsByLevel.ToDictionary(
                kvp => kvp.Key,
                kvp => new List<int>(kvp.Value)),
            NeedsSpellSelection = _needsSpellSelection,
            IsDivineCaster = _isDivineCaster,
            EquipmentItems = _equipmentItems.Select(e => new CharacterCreationService.EquipmentItem
            {
                ResRef = e.ResRef,
                Name = e.Name,
                SlotFlags = e.SlotFlags
            }).ToList(),
            CharacterName = _characterName,
            LastName = _identityLastNameTextBox.Text?.Trim() ?? "",
            Description = _identityDescriptionTextBox.Text?.Trim() ?? "",
            PaletteId = _paletteId,
            FactionId = _selectedFactionId,
            VoiceSetId = _selectedVoiceSetId,
            Age = (int)(_identityAgeNumericUpDown.Value ?? 25)
        };

        var service = new CharacterCreationService(_displayService, _gameDataService);
        CreatedCreature = service.BuildCreature(input);
        Confirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private void UpdateSidebarSummary()
    {
        var parts = new List<string>();

        if (_currentStep >= 2 || _isBicFile)
            parts.Add(_isBicFile ? "BIC" : "UTC");

        if (_currentStep >= 2 && _selectedRaceId != 255)
        {
            var raceName = _displayService.GetRaceName(_selectedRaceId);
            var genderName = _selectedGender == 0 ? "Male" : "Female";
            parts.Add($"{genderName} {raceName}");
        }

        if (_currentStep >= 5 && _selectedClassId >= 0)
        {
            parts.Add(_displayService.GetClassName(_selectedClassId));
        }

        _sidebarSummary.Text = parts.Count > 0
            ? string.Join(" | ", parts)
            : "Choose your path...";
    }

    #endregion
}

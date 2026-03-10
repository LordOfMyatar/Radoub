using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Quartermaster.Services;
using Radoub.UI.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using Radoub.Formats.Uti;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Steps 10-11: Equipment selection and character summary review.
/// </summary>
public partial class NewCharacterWizardWindow
{
    #region Step 10: Equipment

    private void PrepareStep10()
    {
        if (_step9Loaded) return;
        _step9Loaded = true;

        // Equipment step is optional — show empty state by default
        UpdateEquipmentDisplay();
    }

    private void OnLoadPackageEquipmentClick(object? sender, RoutedEventArgs e)
    {
        _equipmentItems.Clear();

        if (_selectedPackageId == 255)
        {
            UpdateEquipmentDisplay();
            return;
        }

        // Read equipment from packeq*.2da referenced by packages.2da Equip2DA column
        var equip2da = _gameDataService.Get2DAValue("packages", _selectedPackageId, "Equip2DA");
        if (string.IsNullOrEmpty(equip2da) || equip2da == "****")
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"No Equip2DA for package {_selectedPackageId}", "NewCharWiz", "📦");
            UpdateEquipmentDisplay();
            return;
        }

        // Read equipment entries from the package equipment table (packeq*.2da uses "Label" column)
        int equipRowCount = _gameDataService.Get2DA(equip2da)?.RowCount ?? 50;
        for (int row = 0; row < equipRowCount; row++)
        {
            var resRef = _gameDataService.Get2DAValue(equip2da, row, "Label");
            if (string.IsNullOrEmpty(resRef) || resRef == "****")
                continue;

            // Get display name and slot info from the UTI resource
            var displayName = GetItemDisplayName(resRef);
            int slotFlags = GetItemSlotFlags(resRef);
            var slotName = slotFlags != 0 ? GetPrimarySlotName(slotFlags) : "Backpack";

            _equipmentItems.Add(new EquipmentDisplayItem
            {
                ResRef = resRef,
                Name = displayName,
                SlotName = slotName,
                SlotFlags = slotFlags
            });
        }

        UnifiedLogger.Log(LogLevel.DEBUG, $"Loaded {_equipmentItems.Count} equipment items from {equip2da}", "NewCharWiz", "📦");
        UpdateEquipmentDisplay();
    }

    private void OnClearEquipmentClick(object? sender, RoutedEventArgs e)
    {
        _equipmentItems.Clear();
        UpdateEquipmentDisplay();
    }

    private void UpdateEquipmentDisplay()
    {
        _equipmentItemsPanel.Children.Clear();
        _equipmentEmptyLabel.IsVisible = _equipmentItems.Count == 0;
        _equipmentCountLabel.Text = $"{_equipmentItems.Count} items";

        foreach (var item in _equipmentItems)
        {
            var row = new Grid
            {
                ColumnDefinitions = Avalonia.Controls.ColumnDefinitions.Parse("*,120"),
                Margin = new Avalonia.Thickness(0, 2)
            };

            var nameLabel = new TextBlock
            {
                Text = item.Name,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(8, 4)
            };
            Grid.SetColumn(nameLabel, 0);
            row.Children.Add(nameLabel);

            var slotLabel = new TextBlock
            {
                Text = item.SlotName,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = BrushManager.GetDisabledBrush(this),
                FontSize = this.FindResource("FontSizeXSmall") as double? ?? 12
            };
            Grid.SetColumn(slotLabel, 1);
            row.Children.Add(slotLabel);

            _equipmentItemsPanel.Children.Add(row);
        }
    }

    private string GetItemDisplayName(string resRef)
    {
        try
        {
            var utiData = _gameDataService.FindResource(resRef, ResourceTypes.Uti);
            if (utiData != null)
            {
                var uti = UtiReader.Read(utiData);
                var locName = uti.LocalizedName;
                if (locName != null)
                {
                    // Try localized string first
                    if (locName.LocalizedStrings.TryGetValue(0, out var engName) && !string.IsNullOrEmpty(engName))
                        return engName;
                    // Try StrRef
                    if (locName.StrRef != 0xFFFFFFFF)
                    {
                        var tlkName = _gameDataService.GetString(locName.StrRef.ToString());
                        if (!string.IsNullOrEmpty(tlkName))
                            return tlkName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"Failed to read item name for {resRef}: {ex.Message}", "NewCharWiz", "📦");
        }

        return resRef; // Fallback to resref
    }

    /// <summary>
    /// Gets a display-friendly slot name from an EquipableSlots bitmask.
    /// Picks the first recognized slot bit (e.g., 0x30 → "Right Hand").
    /// </summary>
    private static string GetPrimarySlotName(int slotFlags)
    {
        // Check each known slot bit from lowest to highest
        int[] knownSlots =
        [
            EquipmentSlots.Head, EquipmentSlots.Chest, EquipmentSlots.Boots,
            EquipmentSlots.Arms, EquipmentSlots.RightHand, EquipmentSlots.LeftHand,
            EquipmentSlots.Cloak, EquipmentSlots.LeftRing, EquipmentSlots.RightRing,
            EquipmentSlots.Neck, EquipmentSlots.Belt, EquipmentSlots.Arrows,
            EquipmentSlots.Bullets, EquipmentSlots.Bolts
        ];

        foreach (var slot in knownSlots)
        {
            if ((slotFlags & slot) != 0)
                return EquipmentSlots.GetSlotName(slot);
        }

        return $"Slot ({slotFlags:X})";
    }

    private int GetItemSlotFlags(string resRef)
    {
        try
        {
            var utiData = _gameDataService.FindResource(resRef, ResourceTypes.Uti);
            if (utiData != null)
            {
                var uti = UtiReader.Read(utiData);
                int baseItem = uti.BaseItem;
                var slotsStr = _gameDataService.Get2DAValue("baseitems", baseItem, "EquipableSlots");
                if (!string.IsNullOrEmpty(slotsStr) && slotsStr != "****")
                {
                    // Parse decimal first, then hex with 0x prefix (same as EquipmentSlotValidator)
                    if (int.TryParse(slotsStr, out int slots))
                        return slots;
                    if (slotsStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(slotsStr[2..], System.Globalization.NumberStyles.HexNumber, null, out slots))
                        return slots;
                }
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"Failed to read slot for {resRef}: {ex.Message}", "NewCharWiz", "📦");
        }
        return 0;
    }

    #endregion

    #region Step 11: Summary (read-only review)

    private void PrepareStep11()
    {
        // Populate summary fields
        _summaryFileTypeLabel.Text = _isBicFile ? "Player Character (BIC)" : "Creature Blueprint (UTC)";

        var raceName = _displayService.GetRaceName(_selectedRaceId);
        var genderName = _selectedGender == 0 ? "Male" : "Female";
        _summaryRaceLabel.Text = $"{genderName} {raceName}";

        // Identity
        var identityParts = new List<string>();
        if (!string.IsNullOrEmpty(_characterName))
            identityParts.Add(_characterName);
        var lastName = _identityLastNameTextBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(lastName))
            identityParts.Add(lastName);
        if (identityParts.Count == 0)
            identityParts.Add("(unnamed)");

        var portraitResRef = _displayService.GetPortraitResRef(_selectedPortraitId);
        identityParts.Add($"Portrait: {portraitResRef ?? _selectedPortraitId.ToString()}");

        if (_selectedVoiceSetId > 0)
        {
            var voiceName = _displayService.GetSoundSetName(_selectedVoiceSetId);
            identityParts.Add($"Voice: {voiceName}");
        }

        _summaryIdentityLabel.Text = string.Join(" | ", identityParts);

        // Appearance
        var appName = _selectedAppearanceId > 0
            ? _displayService.GetAppearanceName(_selectedAppearanceId)
            : _displayService.GetAppearanceName(GetDefaultAppearanceForRace(_selectedRaceId));
        _summaryAppearanceLabel.Text = appName;

        // Class
        if (_selectedClassId >= 0)
        {
            var className = _displayService.GetClassName(_selectedClassId);
            var classText = $"{className} (Level 1)";

            // Append domains if applicable
            if (_classNeedsDomains)
            {
                var d1Name = (_domain1ComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                var d2Name = (_domain2ComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                if (!string.IsNullOrEmpty(d1Name) && !string.IsNullOrEmpty(d2Name))
                    classText += $" — Domains: {d1Name}, {d2Name}";
            }

            _summaryClassLabel.Text = classText;
        }

        // Alignment
        _summaryAlignmentLabel.Text = GetAlignmentName(_selectedGoodEvil, _selectedLawChaos);

        // Familiar
        bool hasFamiliar = _selectedClassId >= 0 && _displayService.ClassGrantsFamiliar(_selectedClassId);
        _summaryFamiliarSection.IsVisible = hasFamiliar;
        if (hasFamiliar)
        {
            var familiarType = GetSelectedFamiliarType();
            var familiars = _displayService.GetAllFamiliars();
            var familiarName = familiars.FirstOrDefault(f => f.Id == familiarType).Name ?? $"Type {familiarType}";
            var customName = _familiarNameTextBox.Text?.Trim();
            _summaryFamiliarLabel.Text = !string.IsNullOrEmpty(customName)
                ? $"{familiarName} — \"{customName}\""
                : familiarName;
        }

        // Abilities
        var racialMods = _displayService.GetRacialModifiers(_selectedRaceId);
        var abilityParts = new List<string>();
        foreach (var ability in AbilityNames)
        {
            int baseScore = _abilityBaseScores[ability];
            int racialMod = GetRacialModForAbility(racialMods, ability);
            int total = baseScore + racialMod;
            abilityParts.Add($"{ability} {total}");
        }
        _summaryAbilitiesLabel.Text = string.Join("  |  ", abilityParts);

        // Skills
        int skillCount = _skillRanksAllocated.Count(kvp => kvp.Value > 0);
        int skillPointsSpent = _skillPointsTotal - GetSkillPointsRemaining();
        if (skillCount > 0)
        {
            var topSkills = _skillRanksAllocated
                .Where(kvp => kvp.Value > 0)
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .Select(kvp => $"{_displayService.Skills.GetSkillName(kvp.Key)} ({kvp.Value})")
                .ToList();
            _summarySkillsLabel.Text = $"{skillPointsSpent} points in {skillCount} skills: {string.Join(", ", topSkills)}" +
                (skillCount > 5 ? $" +{skillCount - 5} more" : "");
        }
        else
        {
            _summarySkillsLabel.Text = "No skills allocated";
        }

        // Spells
        if (_needsSpellSelection)
        {
            _summarySpellsSection.IsVisible = true;
            if (_isDivineCaster)
            {
                _summarySpellsLabel.Text = "All spells granted by deity (divine caster)";
            }
            else
            {
                int totalSpells = _selectedSpellsByLevel.Values.Sum(list => list.Count);
                _summarySpellsLabel.Text = $"{totalSpells} spells selected";
            }
        }
        else
        {
            _summarySpellsSection.IsVisible = false;
        }

        // Feats (granted + chosen)
        var grantedFeats = GetGrantedFeatIds();
        var allFeatIds = new HashSet<int>(grantedFeats);
        allFeatIds.UnionWith(_chosenFeatIds);

        if (allFeatIds.Count > 0)
        {
            var grantedNames = grantedFeats
                .Select(id => _displayService.GetFeatName(id))
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();
            var chosenNames = _chosenFeatIds
                .Select(id => _displayService.GetFeatName(id))
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();

            var parts2 = new List<string>();
            if (chosenNames.Count > 0) parts2.Add($"{chosenNames.Count} chosen ({string.Join(", ", chosenNames)})");
            if (grantedNames.Count > 0) parts2.Add($"{grantedNames.Count} granted");
            _summaryFeatsLabel.Text = string.Join(" + ", parts2);
        }
        else
        {
            _summaryFeatsLabel.Text = "None";
        }

        // Equipment
        if (_equipmentItems.Count > 0)
        {
            _summaryEquipmentSection.IsVisible = true;
            _summaryEquipmentLabel.Text = $"{_equipmentItems.Count} items";
        }
        else
        {
            _summaryEquipmentSection.IsVisible = true;
            _summaryEquipmentLabel.Text = "None (can be added later in editor)";
        }

        // Scripts (UTC only)
        bool isUtc = !_isBicFile;
        _summaryScriptsDivider.IsVisible = isUtc;
        _summaryScriptsSection.IsVisible = isUtc;
        if (isUtc)
        {
            _summaryScriptsLabel.Text = _defaultScriptsCheckBox.IsChecked == true
                ? "Default NWN scripts (nw_c2_default*)"
                : "None (can be added later in editor)";
        }
    }

    private void OnSummaryEditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string stepStr && int.TryParse(stepStr, out int targetStep))
        {
            _currentStep = targetStep;
            UpdateStepDisplay();
        }
    }

    // Delegates to CharacterCreationService for testability
    private static string SanitizeForResRef(string name) =>
        CharacterCreationService.SanitizeForResRef(name);

    private HashSet<int> GetGrantedFeatIds()
    {
        var creationService = new CharacterCreationService(_displayService, _gameDataService);
        return creationService.GetGrantedFeatIds(_selectedRaceId, _selectedClassId);
    }

    private HashSet<int> GetAllFeatIdsForCreature()
    {
        var all = GetGrantedFeatIds();
        foreach (var featId in _chosenFeatIds)
            all.Add(featId);
        return all;
    }

    #endregion

    #region Alignment Helpers

    private static string GetAlignmentName(byte goodEvil, byte lawChaos) =>
        CharacterCreationService.GetAlignmentName(goodEvil, lawChaos);

    #endregion
}

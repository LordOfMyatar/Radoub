// AppearancePanel - Data loading partial class
// Methods for loading data from services and populating UI

using System;
using Avalonia.Controls;
using Avalonia.Media;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

public partial class AppearancePanel
{
    private void LoadAppearanceData()
    {
        if (_displayService == null) return;

        _isLoading = true;

        // Load appearances from 2DA
        _appearances = _displayService.GetAllAppearances();
        RefreshFilteredAppearanceList();

        // Load genders (Male=0, Female=1)
        LoadGenderData();

        // Load phenotypes from 2DA
        _phenotypes = _displayService.GetAllPhenotypes();
        if (_phenotypeComboBox != null)
        {
            _phenotypeComboBox.Items.Clear();
            foreach (var pheno in _phenotypes)
            {
                _phenotypeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = pheno.Name,
                    Tag = pheno.PhenotypeId
                });
            }
        }

        _isLoading = false;
    }

    /// <summary>
    /// Rebuilds the appearance ListBox based on current search text and source filters.
    /// Preserves current selection if it's still in the filtered list.
    /// </summary>
    private void RefreshFilteredAppearanceList()
    {
        if (_appearanceListBox == null || _appearances == null) return;

        var searchText = _appearanceSearchBox?.Text;
        var showBif = _showBifCheckBox?.IsChecked ?? true;
        var showHak = _showHakCheckBox?.IsChecked ?? true;
        var showOverride = _showOverrideCheckBox?.IsChecked ?? true;

        var excludePatterns = _excludePatternBox?.Text ?? SettingsService.Instance.AppearanceExcludeFilter;
        var filtered = AppearanceFilterHelper.Filter(_appearances, searchText, showBif, showHak, showOverride, excludePatterns);

        // Remember current selection
        ushort? selectedId = null;
        if (_appearanceListBox.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag is ushort id)
            selectedId = id;

        _appearanceListBox.Items.Clear();
        foreach (var app in filtered)
        {
            var baseName = app.IsPartBased && !app.Name.Contains("(Dynamic)")
                ? $"(Dynamic) {app.Name}"
                : app.Name;
            var modelRef = !string.IsNullOrEmpty(app.Race) ? app.Race : app.Label;
            var displayText = $"[{app.AppearanceId}] {baseName} ({modelRef})";
            var item = new ListBoxItem
            {
                Content = displayText,
                Tag = app.AppearanceId
            };
            // Context menu is attached once to the parent ListBox (see WireEvents),
            // not per-item — #2058.
            Avalonia.Controls.ToolTip.SetTip(item, $"ID: {app.AppearanceId} | Model: {modelRef} | Type: {(app.IsPartBased ? "Part-Based" : "Static")} | Label: {app.Label}");
            _appearanceListBox.Items.Add(item);
        }

        // Restore selection if still present
        if (selectedId.HasValue)
        {
            for (int i = 0; i < _appearanceListBox.Items.Count; i++)
            {
                if (_appearanceListBox.Items[i] is ListBoxItem item && item.Tag is ushort itemId && itemId == selectedId.Value)
                {
                    _appearanceListBox.SelectedIndex = i;
                    break;
                }
            }
        }

        // Update count text
        if (_appearanceCountText != null)
        {
            _appearanceCountText.Text = filtered.Count == _appearances.Count
                ? $"({filtered.Count})"
                : $"({filtered.Count}/{_appearances.Count})";
        }
    }

    private void LoadGenderData()
    {
        if (_displayService == null || _genderComboBox == null) return;

        _genderComboBox.Items.Clear();

        // NWN gender values: 0=Male, 1=Female, 2=Both, 3=Other, 4=None
        // For creature editing, only Male and Female are relevant
        var maleName = _displayService.GetGenderName(0);
        var femaleName = _displayService.GetGenderName(1);

        _genderComboBox.Items.Add(new ComboBoxItem { Content = maleName, Tag = (byte)0 });
        _genderComboBox.Items.Add(new ComboBoxItem { Content = femaleName, Tag = (byte)1 });
    }

    private void LoadBodyPartData()
    {
        if (_displayService == null) return;

        // For now, populate with numeric values 0-20
        // TODO: Load from model_*.2da files when available
        void PopulateBodyPartCombo(ComboBox? combo, int max = 20)
        {
            if (combo == null) return;
            combo.Items.Clear();
            for (int i = 0; i <= max; i++)
            {
                combo.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = (byte)i });
            }
        }

        PopulateBodyPartCombo(_headComboBox, 30);
        PopulateBodyPartCombo(_neckComboBox);
        PopulateBodyPartCombo(_torsoComboBox);
        PopulateBodyPartCombo(_pelvisComboBox);
        PopulateBodyPartCombo(_beltComboBox);

        // Tail/Wings from 2DA
        LoadTailWingsData();

        // Limbs
        PopulateBodyPartCombo(_lShoulComboBox);
        PopulateBodyPartCombo(_rShoulComboBox);
        PopulateBodyPartCombo(_lBicepComboBox);
        PopulateBodyPartCombo(_rBicepComboBox);
        PopulateBodyPartCombo(_lFArmComboBox);
        PopulateBodyPartCombo(_rFArmComboBox);
        PopulateBodyPartCombo(_lHandComboBox);
        PopulateBodyPartCombo(_rHandComboBox);
        PopulateBodyPartCombo(_lThighComboBox);
        PopulateBodyPartCombo(_rThighComboBox);
        PopulateBodyPartCombo(_lShinComboBox);
        PopulateBodyPartCombo(_rShinComboBox);
        PopulateBodyPartCombo(_lFootComboBox);
        PopulateBodyPartCombo(_rFootComboBox);
    }

    private void LoadTailWingsData()
    {
        if (_displayService == null) return;

        if (_tailComboBox != null)
        {
            _tailComboBox.Items.Clear();
            var tails = _displayService.GetAllTails();
            foreach (var (id, name) in tails)
            {
                _tailComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
            }
        }

        if (_wingsComboBox != null)
        {
            _wingsComboBox.Items.Clear();
            var wings = _displayService.GetAllWings();
            foreach (var (id, name) in wings)
            {
                _wingsComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
            }
        }
    }

    private void UpdateModelPreview()
    {
        if (_modelService == null || _currentCreature == null || _modelPreviewGL == null)
            return;

        try
        {
            // Set body colors
            _modelPreviewGL.SetCharacterColors(
                _currentCreature.Color_Skin,
                _currentCreature.Color_Hair,
                _currentCreature.Color_Tattoo1,
                _currentCreature.Color_Tattoo2);

            // Load armor colors from equipped chest armor
            var armorColors = _modelService.GetArmorColors(_currentCreature);
            if (armorColors != null)
            {
                _modelPreviewGL.SetArmorColors(
                    armorColors.Value.metal1, armorColors.Value.metal2,
                    armorColors.Value.cloth1, armorColors.Value.cloth2,
                    armorColors.Value.leather1, armorColors.Value.leather2);
            }

            // Prefer BIF textures for creature models to avoid CEP texture
            // incompatibilities (e.g., reversed bat wings from CEP texture) (#1867)
            _modelPreviewGL.PreferBifTextures = true;

            var model = _modelService.LoadCreatureModel(_currentCreature);
            _modelPreviewGL.Model = model;
            RefreshAnimationList(model);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Model preview failed: {ex.GetType().Name}: {ex.Message}");
            _modelPreviewGL.Model = null;
            RefreshAnimationList(null);
        }
    }

    private void UpdateBodyPartsEnabledState(bool isPartBased)
    {
        if (_bodyPartsContent != null)
            _bodyPartsContent.IsEnabled = isPartBased;

        if (_bodyPartsStatusText != null)
        {
            _bodyPartsStatusText.Text = isPartBased
                ? "(Dynamic Appearance)"
                : "(Static Appearance - body parts not editable)";
        }

        if (_bodyPartsContent != null)
            _bodyPartsContent.Opacity = isPartBased ? 1.0 : 0.5;
    }

    private void LoadBodyPartValues(UtcFile creature)
    {
        SelectComboByTag(_headComboBox, creature.AppearanceHead);
        SelectComboByTag(_neckComboBox, creature.BodyPart_Neck);
        SelectComboByTag(_torsoComboBox, creature.BodyPart_Torso);
        SelectComboByTag(_pelvisComboBox, creature.BodyPart_Pelvis);
        SelectComboByTag(_beltComboBox, creature.BodyPart_Belt);
        SelectComboByTag(_tailComboBox, creature.Tail);
        SelectComboByTag(_wingsComboBox, creature.Wings);

        SelectComboByTag(_lShoulComboBox, creature.BodyPart_LShoul);
        SelectComboByTag(_rShoulComboBox, creature.BodyPart_RShoul);
        SelectComboByTag(_lBicepComboBox, creature.BodyPart_LBicep);
        SelectComboByTag(_rBicepComboBox, creature.BodyPart_RBicep);
        SelectComboByTag(_lFArmComboBox, creature.BodyPart_LFArm);
        SelectComboByTag(_rFArmComboBox, creature.BodyPart_RFArm);
        SelectComboByTag(_lHandComboBox, creature.BodyPart_LHand);
        SelectComboByTag(_rHandComboBox, creature.BodyPart_RHand);
        SelectComboByTag(_lThighComboBox, creature.BodyPart_LThigh);
        SelectComboByTag(_rThighComboBox, creature.BodyPart_RThigh);
        SelectComboByTag(_lShinComboBox, creature.BodyPart_LShin);
        SelectComboByTag(_rShinComboBox, creature.BodyPart_RShin);
        SelectComboByTag(_lFootComboBox, creature.BodyPart_LFoot);
        SelectComboByTag(_rFootComboBox, creature.BodyPart_RFoot);

        // Colors
        UpdateAllColorSwatches();
    }

    private void UpdateAllColorSwatches()
    {
        if (_currentCreature == null) return;

        UpdateColorSwatch(_skinColorSwatch, PaletteColorService.Palettes.Skin, _currentCreature.Color_Skin);
        UpdateColorSwatch(_hairColorSwatch, PaletteColorService.Palettes.Hair, _currentCreature.Color_Hair);
        UpdateColorSwatch(_tattoo1ColorSwatch, PaletteColorService.Palettes.Tattoo1, _currentCreature.Color_Tattoo1);
        UpdateColorSwatch(_tattoo2ColorSwatch, PaletteColorService.Palettes.Tattoo2, _currentCreature.Color_Tattoo2);
    }

    private void UpdateColorSwatch(Border? swatch, string paletteName, byte colorIndex)
    {
        if (swatch == null) return;

        if (_paletteColorService != null)
        {
            var color = _paletteColorService.GetPaletteColor(paletteName, colorIndex);
            swatch.Background = new SolidColorBrush(color);
        }
        else
        {
            // Fallback uses theme border color (set in XAML, no need to override)
        }
    }

    private void SelectComboByTag(ComboBox? combo, byte value)
    {
        if (combo == null) return;

        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag is byte id && id == value)
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        combo.Items.Add(new ComboBoxItem { Content = value.ToString(), Tag = value });
        combo.SelectedIndex = combo.Items.Count - 1;
    }

    private void SelectAppearance(ushort appearanceId)
    {
        if (_appearanceListBox == null || _appearances == null) return;

        for (int i = 0; i < _appearanceListBox.Items.Count; i++)
        {
            if (_appearanceListBox.Items[i] is ListBoxItem item &&
                item.Tag is ushort id && id == appearanceId)
            {
                _appearanceListBox.SelectedIndex = i;
                return;
            }
        }

        // If not found in filtered list, clear search and try again
        if (_appearanceSearchBox != null && !string.IsNullOrEmpty(_appearanceSearchBox.Text))
        {
            _appearanceSearchBox.Text = "";
            RefreshFilteredAppearanceList();
            for (int i = 0; i < _appearanceListBox.Items.Count; i++)
            {
                if (_appearanceListBox.Items[i] is ListBoxItem item &&
                    item.Tag is ushort id && id == appearanceId)
                {
                    _appearanceListBox.SelectedIndex = i;
                    return;
                }
            }
        }

        // Still not found, add it with consistent format. Shared ContextMenu
        // on the parent ListBox handles the copy actions for all rows (#2058).
        var fallbackItem = new ListBoxItem
        {
            Content = $"[{appearanceId}] Appearance {appearanceId}",
            Tag = appearanceId
        };
        _appearanceListBox.Items.Add(fallbackItem);
        _appearanceListBox.SelectedIndex = _appearanceListBox.Items.Count - 1;
    }

    private void SelectGender(byte genderId)
    {
        if (_genderComboBox == null) return;

        for (int i = 0; i < _genderComboBox.Items.Count; i++)
        {
            if (_genderComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is byte id && id == genderId)
            {
                _genderComboBox.SelectedIndex = i;
                return;
            }
        }

        // If not found (e.g., genderId > 1), add it
        var name = _displayService?.GetGenderName(genderId) ?? $"Gender {genderId}";
        _genderComboBox.Items.Add(new ComboBoxItem
        {
            Content = name,
            Tag = genderId
        });
        _genderComboBox.SelectedIndex = _genderComboBox.Items.Count - 1;
    }

    private void SelectPhenotype(int phenotypeId)
    {
        if (_phenotypeComboBox == null || _phenotypes == null) return;

        for (int i = 0; i < _phenotypeComboBox.Items.Count; i++)
        {
            if (_phenotypeComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is int id && id == phenotypeId)
            {
                _phenotypeComboBox.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        _phenotypeComboBox.Items.Add(new ComboBoxItem
        {
            Content = $"Phenotype {phenotypeId}",
            Tag = phenotypeId
        });
        _phenotypeComboBox.SelectedIndex = _phenotypeComboBox.Items.Count - 1;
    }
}

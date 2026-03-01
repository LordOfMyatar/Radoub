using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Quartermaster.Services;
using Radoub.Formats.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 4: Class &amp; Package selection, domain configuration, and prestige planning.
/// </summary>
public partial class NewCharacterWizardWindow
{
    #region Step 4: Class & Package

    private void PrepareStep4()
    {
        // Reload favored class (may have changed if race changed)
        _favoredClassId = _displayService.GetFavoredClass(_selectedRaceId);

        if (!_step4Loaded)
        {
            _step4Loaded = true;
            LoadPrestigeClasses();
        }

        LoadClassList();
    }

    private void LoadClassList()
    {
        var allMetadata = _displayService.Classes.GetAllClassMetadata();

        // For BIC: player classes only, base classes only (no prestige at level 1)
        // For UTC: all classes
        _allClasses = allMetadata
            .Where(c => !_isBicFile || (c.IsPlayerClass && !c.IsPrestige))
            .Select(c => new ClassDisplayItem
            {
                Id = c.ClassId,
                Name = c.Name,
                IsFavored = _favoredClassId >= 0 && c.ClassId == _favoredClassId
            })
            .OrderByDescending(c => c.IsFavored)
            .ThenBy(c => c.Name)
            .ToList();

        _filteredClasses = new List<ClassDisplayItem>(_allClasses);
        _classListBox.ItemsSource = _filteredClasses;

        // If previously selected class is still in list, re-select it
        if (_selectedClassId >= 0)
        {
            var existing = _filteredClasses.FirstOrDefault(c => c.Id == _selectedClassId);
            if (existing != null)
            {
                _classListBox.SelectedItem = existing;
                return;
            }
        }

        // Select favored class by default, or first class
        var favored = _filteredClasses.FirstOrDefault(c => c.IsFavored);
        if (favored != null)
            _classListBox.SelectedItem = favored;
        else if (_filteredClasses.Count > 0)
            _classListBox.SelectedItem = _filteredClasses[0];
    }

    private void OnClassSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var filter = _classSearchBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(filter))
        {
            _filteredClasses = new List<ClassDisplayItem>(_allClasses);
        }
        else
        {
            _filteredClasses = _allClasses
                .Where(c => c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        _classListBox.ItemsSource = _filteredClasses;
    }

    private void OnClassSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_classListBox.SelectedItem is not ClassDisplayItem selected)
            return;

        _selectedClassId = selected.Id;
        UpdateClassDetailPanel();
        LoadPackagesForClass();
        UpdateDomainVisibility();
        UpdateFamiliarVisibility();
        UpdateAlignmentButtonStates();
        ValidateCurrentStep();
        UpdateSidebarSummary();
    }

    private void UpdateClassDetailPanel()
    {
        var metadata = _displayService.Classes.GetClassMetadata(_selectedClassId);
        _selectedClassNameLabel.Text = metadata.Name;
        _classStatsPanel.IsVisible = true;

        // Stats
        _classHitDieLabel.Text = $"Hit Die: d{metadata.HitDie}";
        _classSkillPointsLabel.Text = $"Skill Points: {metadata.SkillPointsPerLevel} + INT";

        // Primary ability
        var primaryAbility = metadata.PrimaryAbility;
        _classPrimaryAbilityLabel.Text = string.IsNullOrEmpty(primaryAbility) || primaryAbility == "****"
            ? "Primary: —"
            : $"Primary: {FormatAbilityName(primaryAbility)}";

        // Spellcasting info
        if (metadata.IsCaster)
        {
            var casterType = metadata.IsSpontaneousCaster ? "Spontaneous" : "Prepared";
            _classCasterLabel.Text = $"Spellcasting: {casterType}";
        }
        else
        {
            _classCasterLabel.Text = "Spellcasting: None";
        }

        // Alignment restrictions
        if (metadata.AlignmentRestriction != null)
        {
            var alignDesc = FormatAlignmentRestriction(metadata.AlignmentRestriction);
            _classAlignmentLabel.Text = alignDesc;
            _classAlignmentLabel.IsVisible = !string.IsNullOrEmpty(alignDesc);
        }
        else
        {
            _classAlignmentLabel.IsVisible = false;
        }

        // Description
        var desc = _displayService.Classes.GetClassDescription(_selectedClassId);
        if (!string.IsNullOrEmpty(desc))
        {
            _classDescriptionLabel.Text = desc;
            _classDescriptionLabel.Foreground = null;
            _classDescSeparator.IsVisible = true;
        }
        else
        {
            _classDescriptionLabel.Text = "No description available.";
            _classDescSeparator.IsVisible = false;
        }
    }

    private void LoadPackagesForClass()
    {
        var packages = _displayService.GetPackagesForClass(_selectedClassId);

        if (packages.Count > 0)
        {
            var packageItems = packages.Select(p => new PackageDisplayItem
            {
                Id = p.Id,
                Name = p.Name
            }).ToList();

            _packageComboBox.ItemsSource = packageItems;

            // Select the class's default package from classes.2da "Package" column
            var defaultPkgStr = _gameDataService.Get2DAValue("classes", _selectedClassId, "Package");
            PackageDisplayItem? defaultItem = null;
            if (int.TryParse(defaultPkgStr, out int defaultPkgId))
                defaultItem = packageItems.FirstOrDefault(p => p.Id == defaultPkgId);
            _packageComboBox.SelectedItem = defaultItem ?? packageItems[0];

            _packageSection.IsVisible = true;
        }
        else
        {
            _packageComboBox.ItemsSource = null;
            _packageSection.IsVisible = false;
            _selectedPackageId = 255;
        }
    }

    private void OnPackageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_packageComboBox.SelectedItem is not PackageDisplayItem selected)
            return;

        _selectedPackageId = selected.Id;

        // Update domain defaults from package if domains are visible
        if (_classNeedsDomains)
            LoadPackageDomainDefaults();
    }

    /// <summary>
    /// Checks if the selected class uses domains by looking at its packages.
    /// Shows/hides the domain picker panel accordingly.
    /// </summary>
    private void UpdateDomainVisibility()
    {
        _classNeedsDomains = false;

        if (_selectedClassId < 0)
        {
            _domainSelectionPanel.IsVisible = false;
            return;
        }

        // Check if any package for this class has Domain1 set (non-****)
        var packages = _displayService.GetPackagesForClass(_selectedClassId);
        foreach (var pkg in packages)
        {
            var domain1Str = _gameDataService.Get2DAValue("packages", pkg.Id, "Domain1");
            if (!string.IsNullOrEmpty(domain1Str) && domain1Str != "****")
            {
                _classNeedsDomains = true;
                break;
            }
        }

        _domainSelectionPanel.IsVisible = _classNeedsDomains;

        if (_classNeedsDomains)
        {
            PopulateDomains();
            LoadPackageDomainDefaults();
        }
    }

    /// <summary>
    /// Populates domain ComboBoxes from domains.2da.
    /// </summary>
    private void PopulateDomains()
    {
        _domainList.Clear();
        _domain1ComboBox.Items.Clear();
        _domain2ComboBox.Items.Clear();

        // Read domains.2da — row 0 is typically "Air", rows go up to ~40+
        for (int row = 0; row < 50; row++)
        {
            var label = _gameDataService.Get2DAValue("domains", row, "Label");
            if (string.IsNullOrEmpty(label) || label == "****")
                continue;

            // Get display name from TLK
            var nameStrRef = _gameDataService.Get2DAValue("domains", row, "Name");
            var name = label; // fallback
            if (!string.IsNullOrEmpty(nameStrRef) && nameStrRef != "****" && int.TryParse(nameStrRef, out int strRef))
            {
                var tlkName = _gameDataService.GetString(strRef.ToString());
                if (!string.IsNullOrEmpty(tlkName))
                    name = tlkName;
            }

            _domainList.Add(new DomainDisplayItem { Id = row, Name = name });
        }

        foreach (var domain in _domainList)
        {
            _domain1ComboBox.Items.Add(new ComboBoxItem { Content = domain.Name, Tag = domain.Id });
            _domain2ComboBox.Items.Add(new ComboBoxItem { Content = domain.Name, Tag = domain.Id });
        }

        // Default to first two different domains
        if (_domain1ComboBox.Items.Count > 0)
            _domain1ComboBox.SelectedIndex = 0;
        if (_domain2ComboBox.Items.Count > 1)
            _domain2ComboBox.SelectedIndex = 1;
        else if (_domain2ComboBox.Items.Count > 0)
            _domain2ComboBox.SelectedIndex = 0;
    }

    /// <summary>
    /// Sets domain ComboBox selections from the current package defaults.
    /// </summary>
    private void LoadPackageDomainDefaults()
    {
        var domain1Str = _gameDataService.Get2DAValue("packages", _selectedPackageId, "Domain1");
        var domain2Str = _gameDataService.Get2DAValue("packages", _selectedPackageId, "Domain2");

        if (!string.IsNullOrEmpty(domain1Str) && domain1Str != "****" && int.TryParse(domain1Str, out int d1))
        {
            for (int i = 0; i < _domain1ComboBox.Items.Count; i++)
            {
                if (_domain1ComboBox.Items[i] is ComboBoxItem item && item.Tag is int tagId && tagId == d1)
                {
                    _domain1ComboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(domain2Str) && domain2Str != "****" && int.TryParse(domain2Str, out int d2))
        {
            for (int i = 0; i < _domain2ComboBox.Items.Count; i++)
            {
                if (_domain2ComboBox.Items[i] is ComboBoxItem item && item.Tag is int tagId && tagId == d2)
                {
                    _domain2ComboBox.SelectedIndex = i;
                    break;
                }
            }
        }
    }

    private static byte GetSelectedDomainId(ComboBox comboBox)
    {
        if (comboBox.SelectedItem is ComboBoxItem item && item.Tag is int domainId)
            return (byte)domainId;
        return 0;
    }

    /// <summary>
    /// Shows/hides the familiar selection panel based on whether the class grants a familiar.
    /// </summary>
    private void UpdateFamiliarVisibility()
    {
        bool showFamiliar = _selectedClassId >= 0 && _displayService.ClassGrantsFamiliar(_selectedClassId);
        _familiarSelectionPanel.IsVisible = showFamiliar;

        if (showFamiliar)
            PopulateFamiliars();
        else
            _selectedFamiliarType = 0;
    }

    private void PopulateFamiliars()
    {
        _familiarComboBox.Items.Clear();
        var familiars = _displayService.GetAllFamiliars();

        foreach (var (id, name) in familiars)
        {
            _familiarComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        }

        // Try to set default from package's Associate column
        int defaultFamiliar = 0;
        if (_selectedPackageId != 255)
        {
            var assocStr = _gameDataService.Get2DAValue("packages", _selectedPackageId, "Associate");
            if (!string.IsNullOrEmpty(assocStr) && assocStr != "****" && int.TryParse(assocStr, out int assocId))
                defaultFamiliar = assocId;
        }

        // Select the default familiar
        for (int i = 0; i < _familiarComboBox.Items.Count; i++)
        {
            if (_familiarComboBox.Items[i] is ComboBoxItem item && item.Tag is int tagId && tagId == defaultFamiliar)
            {
                _familiarComboBox.SelectedIndex = i;
                return;
            }
        }

        if (_familiarComboBox.Items.Count > 0)
            _familiarComboBox.SelectedIndex = 0;
    }

    private void OnFamiliarSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_familiarComboBox.SelectedItem is ComboBoxItem item && item.Tag is int familiarId)
            _selectedFamiliarType = familiarId;
    }

    private static string FormatAbilityName(string abilityCode) => abilityCode.ToUpperInvariant() switch
    {
        "STR" => "Strength",
        "DEX" => "Dexterity",
        "CON" => "Constitution",
        "INT" => "Intelligence",
        "WIS" => "Wisdom",
        "CHA" => "Charisma",
        _ => abilityCode
    };

    private static string FormatAlignmentRestriction(AlignmentRestriction restriction)
    {
        var parts = new List<string>();
        if ((restriction.RestrictionMask & 0x02) != 0) parts.Add("Lawful");
        if ((restriction.RestrictionMask & 0x04) != 0) parts.Add("Chaotic");
        if ((restriction.RestrictionMask & 0x08) != 0) parts.Add("Good");
        if ((restriction.RestrictionMask & 0x10) != 0) parts.Add("Evil");
        if ((restriction.RestrictionMask & 0x01) != 0) parts.Add("Neutral");

        if (parts.Count == 0) return "";

        string verb = restriction.Inverted ? "Cannot be" : "Must be";
        return $"{verb}: {string.Join(" or ", parts)}";
    }

    #endregion

    #region Alignment Selection

    // Alignment grid: LG NG CG / LN TN CN / LE NE CE
    // Values: GoodEvil (0=Evil, 50=Neutral, 100=Good), LawChaos (0=Chaotic, 50=Neutral, 100=Lawful)
    private static readonly (byte GoodEvil, byte LawChaos)[] AlignmentValues =
    {
        (100, 100), (100, 50), (100, 0),   // LG, NG, CG
        (50, 100),  (50, 50),  (50, 0),    // LN, TN, CN
        (0, 100),   (0, 50),   (0, 0)      // LE, NE, CE
    };

    private void OnAlignmentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked) return;

        // Find which button was clicked
        int index = Array.IndexOf(_alignmentButtons, clicked);
        if (index < 0) return;

        // Set values
        _selectedGoodEvil = AlignmentValues[index].GoodEvil;
        _selectedLawChaos = AlignmentValues[index].LawChaos;

        // Update button states - only one can be checked
        for (int i = 0; i < _alignmentButtons.Length; i++)
        {
            _alignmentButtons[i].IsChecked = (i == index);
        }

        // Validate against class alignment restrictions
        UpdateAlignmentRestrictionWarning();
    }

    private void UpdateAlignmentRestrictionWarning()
    {
        if (_selectedClassId < 0)
        {
            _alignmentRestrictionWarning.IsVisible = false;
            return;
        }

        var metadata = _displayService.Classes.GetClassMetadata(_selectedClassId);
        if (metadata.AlignmentRestriction == null)
        {
            _alignmentRestrictionWarning.IsVisible = false;
            return;
        }

        bool allowed = IsAlignmentAllowed(metadata.AlignmentRestriction, _selectedGoodEvil, _selectedLawChaos);
        if (!allowed)
        {
            var restrictionText = FormatAlignmentRestriction(metadata.AlignmentRestriction);
            _alignmentRestrictionWarning.Text = $"Warning: {metadata.Name} requires {restrictionText}";
            _alignmentRestrictionWarning.IsVisible = true;
        }
        else
        {
            _alignmentRestrictionWarning.IsVisible = false;
        }
    }

    private void UpdateAlignmentButtonStates()
    {
        if (_selectedClassId < 0) return;

        var metadata = _displayService.Classes.GetClassMetadata(_selectedClassId);

        for (int i = 0; i < _alignmentButtons.Length; i++)
        {
            if (metadata.AlignmentRestriction != null)
            {
                bool allowed = IsAlignmentAllowed(metadata.AlignmentRestriction,
                    AlignmentValues[i].GoodEvil, AlignmentValues[i].LawChaos);
                _alignmentButtons[i].IsEnabled = allowed;
            }
            else
            {
                _alignmentButtons[i].IsEnabled = true;
            }
        }

        // If current selection is now disabled, auto-select first valid alignment
        int currentIndex = GetCurrentAlignmentIndex();
        if (currentIndex >= 0 && !_alignmentButtons[currentIndex].IsEnabled)
        {
            for (int i = 0; i < _alignmentButtons.Length; i++)
            {
                if (_alignmentButtons[i].IsEnabled)
                {
                    _selectedGoodEvil = AlignmentValues[i].GoodEvil;
                    _selectedLawChaos = AlignmentValues[i].LawChaos;
                    for (int j = 0; j < _alignmentButtons.Length; j++)
                        _alignmentButtons[j].IsChecked = (j == i);
                    break;
                }
            }
        }

        UpdateAlignmentRestrictionWarning();
    }

    private int GetCurrentAlignmentIndex()
    {
        for (int i = 0; i < AlignmentValues.Length; i++)
        {
            if (AlignmentValues[i].GoodEvil == _selectedGoodEvil &&
                AlignmentValues[i].LawChaos == _selectedLawChaos)
                return i;
        }
        return 4; // Default to TN
    }

    private static bool IsAlignmentAllowed(AlignmentRestriction restriction, byte goodEvil, byte lawChaos)
    {
        // Determine alignment on each axis
        bool isLawful = lawChaos > 70;
        bool isChaotic = lawChaos < 30;
        bool isNeutralLC = !isLawful && !isChaotic;

        bool isGood = goodEvil > 70;
        bool isEvil = goodEvil < 30;
        bool isNeutralGE = !isGood && !isEvil;

        int mask = restriction.RestrictionMask;
        int type = restriction.RestrictionType;

        // Extract restriction bits per axis (neutral bit 0x01 is shared)
        bool maskHasLawful = (mask & 0x02) != 0;
        bool maskHasChaotic = (mask & 0x04) != 0;
        bool maskHasGood = (mask & 0x08) != 0;
        bool maskHasEvil = (mask & 0x10) != 0;
        bool maskHasNeutral = (mask & 0x01) != 0;

        bool matches;
        if (type == 0x03)
        {
            // Both axes: non-neutral bits must match per-axis (AND);
            // neutral matches either axis (OR) — e.g. Druid "must have neutral on at least one axis"
            bool lcMatch = (maskHasLawful && isLawful) || (maskHasChaotic && isChaotic);
            bool geMatch = (maskHasGood && isGood) || (maskHasEvil && isEvil);
            bool neutralMatch = maskHasNeutral && (isNeutralLC || isNeutralGE);

            // Paladin (0x0A, both axes): must be Lawful AND Good → lcMatch && geMatch
            // Druid (0x01, both axes): must be neutral on any axis → neutralMatch only
            bool hasLcRestriction = maskHasLawful || maskHasChaotic;
            bool hasGeRestriction = maskHasGood || maskHasEvil;

            if (hasLcRestriction && hasGeRestriction)
                matches = lcMatch && geMatch || neutralMatch;
            else if (hasLcRestriction)
                matches = lcMatch || neutralMatch;
            else if (hasGeRestriction)
                matches = geMatch || neutralMatch;
            else
                matches = neutralMatch; // Only neutral bit set
        }
        else if (type == 0x01)
        {
            // Law-Chaos axis only
            matches = (maskHasLawful && isLawful) || (maskHasChaotic && isChaotic)
                || (maskHasNeutral && isNeutralLC);
        }
        else if (type == 0x02)
        {
            // Good-Evil axis only
            matches = (maskHasGood && isGood) || (maskHasEvil && isEvil)
                || (maskHasNeutral && isNeutralGE);
        }
        else
        {
            // No type specified: simple bitmask OR check (legacy fallback)
            int alignBits = 0;
            if (isGood) alignBits |= 0x08;
            if (isEvil) alignBits |= 0x10;
            if (isLawful) alignBits |= 0x02;
            if (isChaotic) alignBits |= 0x04;
            if (isNeutralLC || isNeutralGE) alignBits |= 0x01;
            matches = (alignBits & mask) != 0;
        }

        return restriction.Inverted ? !matches : matches;
    }

    #endregion

    #region Prestige Planning

    private void LoadPrestigeClasses()
    {
        var allMetadata = _displayService.Classes.GetAllClassMetadata();
        var prestigeClasses = allMetadata
            .Where(c => c.IsPrestige && c.IsPlayerClass)
            .Select(c => new ClassDisplayItem { Id = c.ClassId, Name = c.Name })
            .OrderBy(c => c.Name)
            .ToList();

        _prestigeClassComboBox.ItemsSource = prestigeClasses;
    }

    private void OnPrestigePlanningToggle(object? sender, PointerPressedEventArgs e)
    {
        _prestigePlanningExpanded = !_prestigePlanningExpanded;
        _prestigePlanningContent.IsVisible = _prestigePlanningExpanded;
        _prestigeToggleArrow.Text = _prestigePlanningExpanded ? "▾" : "▸";
    }

    private void OnPrestigeClassSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_prestigeClassComboBox.SelectedItem is not ClassDisplayItem selected)
            return;

        var prereqs = _displayService.Classes.GetPrestigePrerequisites(selected.Id);

        if (prereqs.Count == 0)
        {
            _prestigePrereqLabel.Text = "No prerequisites listed.";
            return;
        }

        var lines = new List<string>();
        foreach (var prereq in prereqs)
        {
            string desc = prereq.Type switch
            {
                PrereqType.Feat => $"Feat: {_displayService.GetFeatName(prereq.Param1)}",
                PrereqType.FeatOr => $"  or: {_displayService.GetFeatName(prereq.Param1)}",
                PrereqType.Skill => $"Skill: {_displayService.Skills.GetSkillName(prereq.Param1)} ({prereq.Param2}+ ranks)",
                PrereqType.Bab => $"Base Attack Bonus: +{prereq.Param1}",
                PrereqType.Race => $"Race: {_displayService.GetRaceName((byte)prereq.Param1)}",
                PrereqType.ArcaneSpell => prereq.Param1 > 0
                    ? $"Can cast arcane spells level {prereq.Param1}+"
                    : "Can cast arcane spells",
                PrereqType.DivineSpell => prereq.Param1 > 0
                    ? $"Can cast divine spells level {prereq.Param1}+"
                    : "Can cast divine spells",
                _ => prereq.Label
            };
            lines.Add(desc);
        }

        _prestigePrereqLabel.Text = string.Join("\n", lines);
    }

    #endregion
}

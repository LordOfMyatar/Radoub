using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 6: Summary display, level-up application, and display item classes.
/// </summary>
public partial class LevelUpWizardWindow
{
    #region Step 6: Summary

    private void PrepareStep6()
    {
        // Class summary
        var className = _displayService.GetClassName(_selectedClassId);
        if (_isNewClass)
        {
            _summaryClassLabel.Text = $"Taking level 1 in {className}";
        }
        else
        {
            var existingClass = _creature.ClassList.FirstOrDefault(c => c.Class == _selectedClassId);
            if (existingClass != null)
            {
                int oldLevel = existingClass.ClassLevel;
                _summaryClassLabel.Text = $"{className} {oldLevel} -> {oldLevel + 1}";
            }
            else
            {
                // Fallback - shouldn't happen but be safe
                _summaryClassLabel.Text = $"Taking level {_newClassLevel} in {className}";
            }
        }

        // Ability score increase summary
        var allAbilityIncreases = new List<int>();
        if (_selectedAbilityIncrease >= 0) allAbilityIncreases.Add(_selectedAbilityIncrease);
        foreach (var extra in _ceAbilityIncreases.Where(i => i != _selectedAbilityIncrease))
            allAbilityIncreases.Add(extra);

        if (_needsAbilityIncrease && allAbilityIncreases.Count > 0)
        {
            _summaryAbilityPanel.IsVisible = true;
            byte[] scores = { _creature.Str, _creature.Dex, _creature.Con, _creature.Int, _creature.Wis, _creature.Cha };
            var parts = allAbilityIncreases.Select(idx =>
            {
                var name = AbilityNames[idx];
                var old = scores[idx];
                return $"{name} {old} -> {old + 1}";
            });
            _summaryAbilityLabel.Text = string.Join(", ", parts);
        }
        else
        {
            _summaryAbilityPanel.IsVisible = false;
        }

        // Feats summary (player-selected + auto-granted)
        var grantedFeatIds = _displayService.Feats.GetClassFeatsGrantedAtLevel(_selectedClassId, _newClassLevel);
        var featSummary = _selectedFeats.Select(f => _displayService.GetFeatName(f)).ToList();
        foreach (var gf in grantedFeatIds)
            featSummary.Add($"{_displayService.GetFeatName(gf)} (granted)");

        if (featSummary.Count > 0)
        {
            _summaryFeatsPanel.IsVisible = true;
            _summaryFeatsList.ItemsSource = featSummary;
        }
        else
        {
            _summaryFeatsPanel.IsVisible = false;
        }

        // Skills summary
        var skillChanges = _skillPointsAdded
            .Where(kv => kv.Value > 0)
            .Select(kv => $"{_displayService.GetSkillName(kv.Key)} +{kv.Value}")
            .ToList();
        _summarySkillsList.ItemsSource = skillChanges.Count > 0 ? skillChanges : new[] { "(No skills allocated)" };

        // Spells summary
        var allSelectedSpells = _selectedSpellsByLevel
            .SelectMany(kv => kv.Value)
            .Distinct()
            .ToList();
        if (allSelectedSpells.Count > 0)
        {
            _summarySpellsPanel.IsVisible = true;
            _summarySpellsList.ItemsSource = allSelectedSpells.Select(s => _displayService.GetSpellName(s));
        }
        else if (_isDivineCaster)
        {
            _summarySpellsPanel.IsVisible = true;
            _summarySpellsList.ItemsSource = new[] { "(Divine caster - spells granted automatically)" };
        }
        else
        {
            _summarySpellsPanel.IsVisible = false;
        }

        // Derived stats - show totals with change indicators
        int hitDie = _displayService.Classes.GetClassMetadata(_selectedClassId).HitDie;
        // Account for ability increase if CON was selected (index 2)
        byte effectiveCon = _creature.Con;
        if (_needsAbilityIncrease && _selectedAbilityIncrease == 2)
            effectiveCon = (byte)Math.Min(255, effectiveCon + 1);
        _hpIncrease = LevelUpApplicationService.CalculateHpIncrease(hitDie, effectiveCon);

        // CON retroactivity: if CON modifier changes, all previous levels gain/lose HP
        int previousLevels = _creature.ClassList.Sum(c => c.ClassLevel);
        _conRetroactiveHp = _needsAbilityIncrease
            ? LevelUpApplicationService.CalculateConRetroactiveHp(_selectedAbilityIncrease, _creature.Con, previousLevels)
            : 0;

        int totalHpGain = _hpIncrease + _conRetroactiveHp;
        int newHp = _creature.MaxHitPoints + totalHpGain;
        string hpText = $"{_creature.MaxHitPoints} -> {newHp} (+{_hpIncrease}";
        if (_conRetroactiveHp > 0)
            hpText += $" +{_conRetroactiveHp} retro CON";
        else if (_conRetroactiveHp < 0)
            hpText += $" {_conRetroactiveHp} retro CON";
        hpText += ")";
        _summaryHpLabel.Text = hpText;

        // Calculate total BAB (current + new level's contribution)
        int currentTotalBab = _displayService.CalculateBaseAttackBonus(_creature);
        int oldClassBab = _displayService.GetClassBab(_selectedClassId, _newClassLevel - 1);
        int newClassBab = _displayService.GetClassBab(_selectedClassId, _newClassLevel);
        int babChange = newClassBab - oldClassBab;
        int newTotalBab = currentTotalBab + babChange;
        _summaryBabLabel.Text = babChange > 0
            ? $"{currentTotalBab} -> {newTotalBab} (+{babChange})"
            : $"{currentTotalBab}";

        // Calculate total saves (current + new level's contribution)
        var currentSaves = _displayService.CalculateBaseSavingThrows(_creature);
        var oldClassSaves = _displayService.GetClassSaves(_selectedClassId, _newClassLevel - 1);
        var newClassSaves = _displayService.GetClassSaves(_selectedClassId, _newClassLevel);
        int fortChange = newClassSaves.Fortitude - oldClassSaves.Fortitude;
        int refChange = newClassSaves.Reflex - oldClassSaves.Reflex;
        int willChange = newClassSaves.Will - oldClassSaves.Will;
        var saveParts = new List<string>();
        saveParts.Add(fortChange > 0
            ? $"Fort {currentSaves.Fortitude}->{currentSaves.Fortitude + fortChange}"
            : $"Fort {currentSaves.Fortitude}");
        saveParts.Add(refChange > 0
            ? $"Ref {currentSaves.Reflex}->{currentSaves.Reflex + refChange}"
            : $"Ref {currentSaves.Reflex}");
        saveParts.Add(willChange > 0
            ? $"Will {currentSaves.Will}->{currentSaves.Will + willChange}"
            : $"Will {currentSaves.Will}");
        _summarySavesLabel.Text = string.Join(", ", saveParts);
    }

    #endregion

    #region Apply Level-Up

    // Delegates to LevelUpApplicationService for testability
    private void ApplyLevelUp()
    {
        var service = new LevelUpApplicationService(_displayService);
        var settings = SettingsService.Instance;

        service.ApplyLevelUp(_creature, new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = _selectedClassId,
            NewClassLevel = _newClassLevel,
            IsNewClass = _isNewClass,
            SelectedFeats = _selectedFeats,
            SkillPointsAdded = _skillPointsAdded,
            SelectedSpellsByLevel = _selectedSpellsByLevel,
            AbilityIncrease = _selectedAbilityIncrease,
            ExtraAbilityIncreases = _ceAbilityIncreases
                .Where(i => i != _selectedAbilityIncrease)
                .ToList(),
            HpIncrease = _hpIncrease,
            ConRetroactiveHp = _conRetroactiveHp,
            RecordHistory = settings.RecordLevelHistory,
            HistoryEncoding = settings.LevelHistoryEncoding
        });
    }

    #endregion

    #region Display Classes

    private class ClassDisplayItem
    {
        public int ClassId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsPrestige { get; set; }
        public int HitDie { get; set; }
        public int SkillPoints { get; set; }
        public int MaxLevel { get; set; }
        public int CurrentLevel { get; set; }
        public ClassQualification Qualification { get; set; }
        public ClassPrereqResult? PrerequisiteResult { get; set; }
        public bool CanSelect { get; set; }

        public string Icon => IsPrestige ? "*" : " ";
        public string DisplayName => CurrentLevel > 0 ? $"{Name} (Lvl {CurrentLevel})" : Name;
        public string Badge => CanSelect ? "" : Qualification switch
        {
            ClassQualification.PrerequisitesNotMet => "(prereqs)",
            ClassQualification.AlignmentRestricted => "(alignment)",
            ClassQualification.MaxLevelReached => "(max)",
            _ => ""
        };
    }

    private class PrereqDisplayItem
    {
        public string Icon { get; set; } = "";
        public string Text { get; set; } = "";
    }

    private class SkillDisplayItem
    {
        public int SkillId { get; set; }
        public string Name { get; set; } = "";
        public int CurrentRanks { get; set; }
        public int AddedRanks { get; set; }
        public bool IsClassSkill { get; set; }
        public bool IsUnavailable { get; set; }
        public int MaxRanks { get; set; }
        public int Cost { get; set; } = 1;

        public string ClassSkillIndicator => IsUnavailable ? "(unavailable)" : IsClassSkill ? "(class skill, 1 pt)" : "(cross-class, 2 pts)";
        public bool CanIncrease => !IsUnavailable && CurrentRanks + AddedRanks < MaxRanks;
        public bool CanDecrease => AddedRanks > 0;
        public double RowOpacity => IsUnavailable ? 0.4 : 1.0;
    }

    private class FeatDisplayItem
    {
        public int FeatId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public FeatCategory Category { get; set; }
        public bool MeetsPrereqs { get; set; }
        public FeatPrereqResult? PrereqResult { get; set; }
        public bool IsClassFeat { get; set; }
        public bool CanSelect { get; set; }

        public string DisplayName => Name;
        public string PrereqTooltip => PrereqResult?.GetTooltip() ?? "No prerequisites";
        public string Badge => !MeetsPrereqs ? "(prereqs)" : IsClassFeat ? "(class)" : "";
    }

    private class SpellDisplayItem
    {
        public int SpellId { get; set; }
        public string Name { get; set; } = "";
        public string SchoolAbbrev { get; set; } = "";

        public string DisplayName => string.IsNullOrEmpty(SchoolAbbrev) ? Name : $"{Name} [{SchoolAbbrev}]";
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Models;
using Quartermaster.Services;
using Radoub.Formats.Logging;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 6: Summary display, level-up application, and display item classes.
/// Supports consolidated multi-level mode (#1645).
/// </summary>
public partial class LevelUpWizardWindow
{
    #region Step 6: Summary

    private void PrepareStep6()
    {
        // Unapply tentative ability increments — summary displays base→projected (#1737)
        UnapplyAbilityIncrementsFromCreature();

        var className = _displayService.GetClassName(_selectedClassId);

        // Ability map for the HP/skill preview, built from the increment source of truth via the
        // same tested helper the apply path uses (#2575 / 3f).
        _abilityIncreasesByLevel = LevelUpApplicationService.BuildAbilityIncreasesByLevel(
            _abilityIncrements, _abilityIncreaseLevels);

        // Class summary
        if (_levelsToAdd > 1)
        {
            var existingClass = _creature.ClassList.FirstOrDefault(c => c.Class == _selectedClassId);
            int oldLevel = existingClass?.ClassLevel ?? 0;
            _summaryClassLabel.Text = $"{className} {oldLevel} -> {oldLevel + _levelsToAdd} (+{_levelsToAdd} levels)";
        }
        else if (_isNewClass)
        {
            _summaryClassLabel.Text = $"Taking level 1 in {className}";
        }
        else
        {
            var existingClass = _creature.ClassList.FirstOrDefault(c => c.Class == _selectedClassId);
            if (existingClass != null)
                _summaryClassLabel.Text = $"{className} {existingClass.ClassLevel} -> {existingClass.ClassLevel + 1}";
            else
                _summaryClassLabel.Text = $"Taking level {_newClassLevel} in {className}";
        }

        // Ability score increase summary — uses increment-based tracking
        int totalIncrements = _abilityIncrements.Sum();
        if (_needsAbilityIncrease && totalIncrements > 0)
        {
            _summaryAbilityPanel.IsVisible = true;
            byte[] scores = { _creature.Str, _creature.Dex, _creature.Con, _creature.Int, _creature.Wis, _creature.Cha };
            var parts = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                if (_abilityIncrements[i] > 0)
                    parts.Add($"{AbilityNames[i]} {scores[i]} -> {scores[i] + _abilityIncrements[i]} (+{_abilityIncrements[i]})");
            }
            _summaryAbilityLabel.Text = string.Join(", ", parts);
        }
        else
        {
            _summaryAbilityPanel.IsVisible = false;
        }

        // Feats summary — auto-granted across ALL levels in range (#1645)
        var allGrantedFeats = new List<int>();
        for (int lvl = _fromClassLevel; lvl <= _newClassLevel; lvl++)
            allGrantedFeats.AddRange(_displayService.Feats.GetClassFeatsGrantedAtLevel(_selectedClassId, lvl));

        var featSummary = _selectedFeats.Select(f => _displayService.GetFeatName(f)).ToList();
        foreach (var gf in allGrantedFeats.Distinct())
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

        // HP calculation
        int hitDie = _displayService.Classes.GetClassMetadata(_selectedClassId).HitDie;
        int previousLevels = _creature.ClassList.Sum(c => c.ClassLevel);

        if (_levelsToAdd > 1)
        {
            // Consolidated: use iterative HP calculator with CON retroactivity (#1645)
            // Find character levels where CON is increased (from _abilityIncreasesByLevel)
            var conIncreaseLevels = _abilityIncreasesByLevel
                .Where(kv => kv.Value == 2) // CON = index 2
                .Select(kv => kv.Key)
                .ToList();

            int totalHpGain = LevelUpApplicationService.CalculateConsolidatedHp(
                hitDie, _creature.Con, previousLevels, _levelsToAdd, conIncreaseLevels);
            _hpIncrease = totalHpGain;
            _conRetroactiveHp = 0; // Already included in consolidated calc

            int newHp = _creature.MaxHitPoints + totalHpGain;
            _summaryHpLabel.Text = $"{_creature.MaxHitPoints} -> {newHp} (+{totalHpGain})";
        }
        else
        {
            // Single-level HP
            byte effectiveCon = _creature.Con;
            if (_needsAbilityIncrease && _abilityIncrements[2] > 0) // CON = index 2
                effectiveCon = (byte)Math.Min(255, effectiveCon + 1);
            _hpIncrease = LevelUpApplicationService.CalculateHpIncrease(hitDie, effectiveCon);

            _conRetroactiveHp = (_needsAbilityIncrease && _abilityIncrements[2] > 0)
                ? LevelUpApplicationService.CalculateConRetroactiveHp(2, _creature.Con, previousLevels)
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
        }

        // BAB — use _fromClassLevel - 1 as base for consolidated (#1645)
        int currentTotalBab = _displayService.CalculateBaseAttackBonus(_creature);
        int oldClassBab = _displayService.GetClassBab(_selectedClassId, _fromClassLevel - 1);
        int newClassBab = _displayService.GetClassBab(_selectedClassId, _newClassLevel);
        int babChange = newClassBab - oldClassBab;
        int newTotalBab = currentTotalBab + babChange;
        _summaryBabLabel.Text = babChange > 0
            ? $"{currentTotalBab} -> {newTotalBab} (+{babChange})"
            : $"{currentTotalBab}";

        // Saves — use _fromClassLevel - 1 as base for consolidated (#1645)
        var currentSaves = _displayService.CalculateBaseSavingThrows(_creature);
        var oldClassSaves = _displayService.GetClassSaves(_selectedClassId, _fromClassLevel - 1);
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

    private void ApplyLevelUp()
    {
        // Unapply tentative ability increments before final apply to avoid double-applying (#1737)
        UnapplyAbilityIncrementsFromCreature();

        var service = new LevelUpApplicationService(_displayService);
        var settings = SettingsService.Instance;

        // Build the per-level ability map straight from the increment source of truth, and the
        // full increase list for CE overflow — no hand-synced projections (#2575 / 3f).
        var abilityIncreasesByLevel = LevelUpApplicationService.BuildAbilityIncreasesByLevel(
            _abilityIncrements, _abilityIncreaseLevels);
        var allAbilityIncreases = new List<int>();
        for (int ability = 0; ability < _abilityIncrements.Length; ability++)
            for (int n = 0; n < _abilityIncrements[ability]; n++)
                allAbilityIncreases.Add(ability);

        // One span-aware apply path for both single- and multi-level. FromClassLevel == NewClassLevel
        // for a single level, so the orchestrator loops once (#2575).
        service.ApplyLevelUp(_creature, new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = _selectedClassId,
            NewClassLevel = _newClassLevel,
            FromClassLevel = _fromClassLevel,
            IsNewClass = _isNewClass,
            SelectedFeats = _selectedFeats,
            SkillPointsAdded = _skillPointsAdded,
            SelectedSpellsByLevel = _selectedSpellsByLevel,
            AbilityIncreasesByLevel = abilityIncreasesByLevel,
            ExtraAbilityIncreases = allAbilityIncreases,
            HpIncrease = _hpIncrease,
            ConRetroactiveHp = _conRetroactiveHp,
            RecordHistory = settings.RecordLevelHistory,
            HistoryEncoding = settings.LevelHistoryEncoding
        });

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Level-up: {_displayService.GetClassName(_selectedClassId)} {_fromClassLevel}-{_newClassLevel} ({_levelsToAdd} level(s))");
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

    // SkillDisplayItem moved to Quartermaster.Models.WizardDisplayItems (#1798)

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

        // MASTERFEAT grouping (#1734): master-feat rows open the subtype picker on add.
        public bool IsMasterFeat { get; set; }
        public System.Collections.Generic.List<int> SubtypeIds { get; set; } = new();

        public string DisplayName => Name;
        public string PrereqTooltip => PrereqResult?.GetTooltip() ?? "No prerequisites";
        public string Badge => IsMasterFeat ? "(choose)" : !MeetsPrereqs ? "(prereqs)" : IsClassFeat ? "(class)" : "";
    }

    // SpellDisplayItem moved to Quartermaster.Models.WizardDisplayItems (#1798)

    #endregion
}

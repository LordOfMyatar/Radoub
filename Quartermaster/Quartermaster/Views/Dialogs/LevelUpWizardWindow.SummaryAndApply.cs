using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 5: Summary display, level-up application, and display item classes.
/// </summary>
public partial class LevelUpWizardWindow
{
    #region Step 5: Summary

    private void PrepareStep5()
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

        // Derived stats
        int hitDie = _displayService.Classes.GetClassMetadata(_selectedClassId).HitDie;
        _summaryHpLabel.Text = $"+d{hitDie}";

        // Calculate BAB change
        int oldBab = _displayService.GetClassBab(_selectedClassId, _newClassLevel - 1);
        int newBab = _displayService.GetClassBab(_selectedClassId, _newClassLevel);
        int babChange = newBab - oldBab;
        _summaryBabLabel.Text = babChange > 0 ? $"+{babChange}" : "0";

        // Calculate save changes
        var oldSaves = _displayService.GetClassSaves(_selectedClassId, _newClassLevel - 1);
        var newSaves = _displayService.GetClassSaves(_selectedClassId, _newClassLevel);
        var saveChanges = new List<string>();
        if (newSaves.Fortitude > oldSaves.Fortitude) saveChanges.Add($"Fort +{newSaves.Fortitude - oldSaves.Fortitude}");
        if (newSaves.Reflex > oldSaves.Reflex) saveChanges.Add($"Ref +{newSaves.Reflex - oldSaves.Reflex}");
        if (newSaves.Will > oldSaves.Will) saveChanges.Add($"Will +{newSaves.Will - oldSaves.Will}");
        _summarySavesLabel.Text = saveChanges.Count > 0 ? string.Join(", ", saveChanges) : "(none)";
    }

    #endregion

    #region Apply Level-Up

    private void ApplyLevelUp()
    {
        // Find or create class entry
        var classEntry = _creature.ClassList.FirstOrDefault(c => c.Class == _selectedClassId);
        if (classEntry != null)
        {
            classEntry.ClassLevel++;
        }
        else
        {
            _creature.ClassList.Add(new CreatureClass
            {
                Class = _selectedClassId,
                ClassLevel = 1
            });
        }

        // Add player-selected feats
        foreach (var featId in _selectedFeats)
        {
            // GAINMULTIPLE feats can appear multiple times in the feat list
            if (_displayService.CanFeatBeGainedMultipleTimes(featId) ||
                !_creature.FeatList.Contains((ushort)featId))
            {
                _creature.FeatList.Add((ushort)featId);
            }
        }

        // Add automatically granted class feats (List=3 at this class level, List=-1 at level 1)
        var grantedFeats = _displayService.Feats.GetClassFeatsGrantedAtLevel(_selectedClassId, _newClassLevel);
        foreach (var featId in grantedFeats)
        {
            if (_displayService.CanFeatBeGainedMultipleTimes(featId) ||
                !_creature.FeatList.Contains((ushort)featId))
            {
                _creature.FeatList.Add((ushort)featId);
            }
        }

        // Add skill points
        foreach (var (skillId, points) in _skillPointsAdded)
        {
            while (_creature.SkillList.Count <= skillId)
                _creature.SkillList.Add(0);
            _creature.SkillList[skillId] = (byte)Math.Min(255, _creature.SkillList[skillId] + points);
        }

        // Add spells to creature's known spell list
        var spellClass = _creature.ClassList.FirstOrDefault(c => c.Class == _selectedClassId);
        if (spellClass != null)
        {
            foreach (var (spellLevel, spellIds) in _selectedSpellsByLevel)
            {
                if (spellLevel < spellClass.KnownSpells.Length)
                {
                    foreach (var spellId in spellIds)
                    {
                        if (!spellClass.KnownSpells[spellLevel].Any(s => s.Spell == (ushort)spellId))
                        {
                            spellClass.KnownSpells[spellLevel].Add(new KnownSpell
                            {
                                Spell = (ushort)spellId,
                                SpellFlags = 0x01, // Readied
                                SpellMetaMagic = 0x00
                            });
                        }
                    }
                }
            }
        }

        // Record level history if enabled
        RecordLevelHistory();
    }

    private void RecordLevelHistory()
    {
        var settings = SettingsService.Instance;
        if (!settings.RecordLevelHistory)
            return;

        // Build this level's record
        var record = new LevelRecord
        {
            TotalLevel = _creature.ClassList.Sum(c => c.ClassLevel),
            ClassId = _selectedClassId,
            ClassLevel = _newClassLevel,
            Feats = _selectedFeats.ToList(),
            Skills = _skillPointsAdded.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value),
            AbilityIncrease = -1 // TODO (#1598): Track ability increases when implemented
        };

        // Get existing history or create new
        var existingHistory = LevelHistoryService.Decode(_creature.Comment) ?? new List<LevelRecord>();
        existingHistory.Add(record);

        // Encode and update comment
        _creature.Comment = LevelHistoryService.AppendToComment(
            _creature.Comment,
            existingHistory,
            settings.LevelHistoryEncoding);
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

using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// Handles level-up application logic extracted from LevelUpWizardWindow.SummaryAndApply.cs.
/// Applies level-up choices to a creature without UI dependencies.
/// </summary>
public class LevelUpApplicationService
{
    private readonly CreatureDisplayService _displayService;

    public LevelUpApplicationService(CreatureDisplayService displayService)
    {
        ArgumentNullException.ThrowIfNull(displayService);
        _displayService = displayService;
    }

    /// <summary>
    /// Complete set of inputs needed to apply a level-up.
    /// </summary>
    public class LevelUpInput
    {
        public int SelectedClassId { get; set; }
        public int NewClassLevel { get; set; }
        public bool IsNewClass { get; set; }
        public List<int> SelectedFeats { get; set; } = new();
        public Dictionary<int, int> SkillPointsAdded { get; set; } = new();
        public Dictionary<int, List<int>> SelectedSpellsByLevel { get; set; } = new();
        public bool RecordHistory { get; set; }
        public LevelHistoryEncoding HistoryEncoding { get; set; } = LevelHistoryEncoding.Readable;
    }

    /// <summary>
    /// Applies level-up choices to a creature. Mutates the creature in place.
    /// </summary>
    /// <exception cref="InvalidOperationException">If level-up cannot be applied.</exception>
    public void ApplyLevelUp(UtcFile creature, LevelUpInput input)
    {
        ApplyClassLevel(creature, input.SelectedClassId);
        ApplyFeats(creature, input.SelectedClassId, input.NewClassLevel, input.SelectedFeats);
        ApplySkills(creature, input.SkillPointsAdded);
        ApplySpells(creature, input.SelectedClassId, input.SelectedSpellsByLevel);

        if (input.RecordHistory)
            RecordLevelHistory(creature, input);
    }

    /// <summary>
    /// Find or create class entry, increment level.
    /// </summary>
    public static void ApplyClassLevel(UtcFile creature, int classId)
    {
        var classEntry = creature.ClassList.FirstOrDefault(c => c.Class == classId);
        if (classEntry != null)
        {
            classEntry.ClassLevel++;
        }
        else
        {
            creature.ClassList.Add(new CreatureClass
            {
                Class = classId,
                ClassLevel = 1
            });
        }
    }

    /// <summary>
    /// Adds player-selected feats and auto-granted class feats.
    /// </summary>
    public void ApplyFeats(UtcFile creature, int classId, int classLevel, List<int> selectedFeats)
    {
        // Add player-selected feats
        foreach (var featId in selectedFeats)
        {
            if (_displayService.CanFeatBeGainedMultipleTimes(featId) ||
                !creature.FeatList.Contains((ushort)featId))
            {
                creature.FeatList.Add((ushort)featId);
            }
        }

        // Add auto-granted class feats
        var grantedFeats = _displayService.Feats.GetClassFeatsGrantedAtLevel(classId, classLevel);
        foreach (var featId in grantedFeats)
        {
            if (_displayService.CanFeatBeGainedMultipleTimes(featId) ||
                !creature.FeatList.Contains((ushort)featId))
            {
                creature.FeatList.Add((ushort)featId);
            }
        }
    }

    /// <summary>
    /// Adds skill point allocations. Capped at 255 per skill (byte limit).
    /// </summary>
    public static void ApplySkills(UtcFile creature, Dictionary<int, int> skillPointsAdded)
    {
        foreach (var (skillId, points) in skillPointsAdded)
        {
            while (creature.SkillList.Count <= skillId)
                creature.SkillList.Add(0);
            creature.SkillList[skillId] = (byte)Math.Min(255, creature.SkillList[skillId] + points);
        }
    }

    /// <summary>
    /// Adds selected spells to the creature's known spell lists.
    /// </summary>
    public static void ApplySpells(UtcFile creature, int classId,
        Dictionary<int, List<int>> selectedSpellsByLevel)
    {
        var spellClass = creature.ClassList.FirstOrDefault(c => c.Class == classId);
        if (spellClass == null) return;

        foreach (var (spellLevel, spellIds) in selectedSpellsByLevel)
        {
            if (spellLevel < 0 || spellLevel >= spellClass.KnownSpells.Length)
                continue;

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

    /// <summary>
    /// Calculates skill points for a level-up.
    /// D&amp;D 3.5/NWN: (basePoints + intMod) at level 2+, (basePoints + intMod) * 4 at level 1.
    /// Racial bonus skill points apply per level, or * 4 at level 1.
    /// </summary>
    public int CalculateLevelUpSkillPoints(UtcFile creature, int classId)
    {
        int intMod = CreatureDisplayService.CalculateAbilityBonus(creature.Int);
        int basePoints = _displayService.GetClassSkillPointBase(classId);
        int totalLevel = creature.ClassList.Sum(c => c.ClassLevel) + 1;

        int racialExtra = _displayService.GetRacialExtraSkillPointsPerLevel(creature.Race);

        if (totalLevel == 1)
        {
            // Level 1 gets 4x multiplier (NWN engine rule)
            const int FirstLevelMultiplier = 4;
            return (Math.Max(1, basePoints + intMod) + racialExtra) * FirstLevelMultiplier;
        }

        return Math.Max(1, basePoints + intMod) + racialExtra;
    }

    /// <summary>
    /// Calculates max skill ranks for a skill at a given character level.
    /// Class skill: level + 3. Cross-class: (level + 3) / 2.
    /// </summary>
    public static int CalculateMaxSkillRanks(bool isClassSkill, int totalCharacterLevel)
    {
        return isClassSkill
            ? totalCharacterLevel + 3
            : (totalCharacterLevel + 3) / 2;
    }

    /// <summary>
    /// Calculates the remaining skill points after allocations.
    /// </summary>
    public static int CalculateRemainingSkillPoints(
        int totalPoints,
        Dictionary<int, int> allocations,
        HashSet<int> classSkillIds)
    {
        int spent = 0;
        foreach (var (skillId, ranks) in allocations)
        {
            int cost = classSkillIds.Contains(skillId) ? 1 : 2;
            spent += ranks * cost;
        }
        return totalPoints - spent;
    }

    private void RecordLevelHistory(UtcFile creature, LevelUpInput input)
    {
        var record = new LevelRecord
        {
            TotalLevel = creature.ClassList.Sum(c => c.ClassLevel),
            ClassId = input.SelectedClassId,
            ClassLevel = input.NewClassLevel,
            Feats = input.SelectedFeats.ToList(),
            Skills = input.SkillPointsAdded
                .Where(kv => kv.Value > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            AbilityIncrease = -1
        };

        var existingHistory = LevelHistoryService.Decode(creature.Comment) ?? new List<LevelRecord>();
        existingHistory.Add(record);

        creature.Comment = LevelHistoryService.AppendToComment(
            creature.Comment,
            existingHistory,
            input.HistoryEncoding);
    }
}

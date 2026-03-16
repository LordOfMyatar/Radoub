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
        public int AbilityIncrease { get; set; } = -1; // -1=none, 0=STR, 1=DEX, 2=CON, 3=INT, 4=WIS, 5=CHA
        public List<int> ExtraAbilityIncreases { get; set; } = new(); // CE mode: additional ability increases
        public int HpIncrease { get; set; } // Hit die roll + CON modifier (pre-calculated)
        public int ConRetroactiveHp { get; set; } // Retroactive HP from CON modifier change
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
        ApplyAbilityIncrease(creature, input.AbilityIncrease);
        // CE mode: apply additional ability increases
        foreach (var extraAbility in input.ExtraAbilityIncreases)
            ApplyAbilityIncrease(creature, extraAbility);
        ApplyHitPoints(creature, input.HpIncrease + input.ConRetroactiveHp);
        ApplyFeats(creature, input.SelectedClassId, input.NewClassLevel, input.SelectedFeats);
        ApplySkills(creature, input.SkillPointsAdded);
        ApplySpells(creature, input.SelectedClassId, input.SelectedSpellsByLevel);
        UpdateSavingThrows(creature);

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
    /// Applies +1 ability score increase at levels 4/8/12/16/20/24/28/32/36/40.
    /// </summary>
    public static void ApplyAbilityIncrease(UtcFile creature, int abilityIndex)
    {
        if (abilityIndex < 0 || abilityIndex > 5)
            return;

        switch (abilityIndex)
        {
            case 0: creature.Str = (byte)Math.Min(255, creature.Str + 1); break;
            case 1: creature.Dex = (byte)Math.Min(255, creature.Dex + 1); break;
            case 2: creature.Con = (byte)Math.Min(255, creature.Con + 1); break;
            case 3: creature.Int = (byte)Math.Min(255, creature.Int + 1); break;
            case 4: creature.Wis = (byte)Math.Min(255, creature.Wis + 1); break;
            case 5: creature.Cha = (byte)Math.Min(255, creature.Cha + 1); break;
        }
    }

    /// <summary>
    /// Adds HP increase to both HitPoints and MaxHitPoints.
    /// Uses max hit die roll + CON modifier (pre-calculated by caller).
    /// Minimum 1 HP per level.
    /// </summary>
    public static void ApplyHitPoints(UtcFile creature, int hpIncrease)
    {
        int gain = Math.Max(1, hpIncrease);
        creature.HitPoints = (short)Math.Min(short.MaxValue, creature.HitPoints + gain);
        creature.MaxHitPoints = (short)Math.Min(short.MaxValue, creature.MaxHitPoints + gain);
        creature.CurrentHitPoints = creature.MaxHitPoints;
    }

    /// <summary>
    /// Calculates HP increase for a level-up: max hit die + CON modifier.
    /// </summary>
    public static int CalculateHpIncrease(int hitDie, int conScore)
    {
        int conMod = CreatureDisplayService.CalculateAbilityBonus(conScore);
        return Math.Max(1, hitDie + conMod);
    }

    /// <summary>
    /// Calculates retroactive HP adjustment when CON modifier changes.
    /// In NWN, CON is retroactive: if CON modifier increases by 1, ALL previous levels
    /// gain 1 HP each. Returns 0 if CON wasn't selected or modifier didn't change.
    /// </summary>
    public static int CalculateConRetroactiveHp(int abilityIncreaseIndex, int currentCon, int previousLevelCount)
    {
        if (abilityIncreaseIndex != 2 || previousLevelCount <= 0)
            return 0;

        int oldMod = CreatureDisplayService.CalculateAbilityBonus(currentCon);
        int newMod = CreatureDisplayService.CalculateAbilityBonus(Math.Min(255, currentCon + 1));

        int modChange = newMod - oldMod;
        if (modChange == 0)
            return 0;

        return modChange * previousLevelCount;
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
    /// Recalculates base saving throws from class levels and writes to creature fields.
    /// Must be called after class levels change to keep FortBonus/RefBonus/WillBonus in sync.
    /// </summary>
    public void UpdateSavingThrows(UtcFile creature)
    {
        var saves = _displayService.CalculateBaseSavingThrows(creature);
        creature.FortBonus = (short)saves.Fortitude;
        creature.RefBonus = (short)saves.Reflex;
        creature.WillBonus = (short)saves.Will;
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

    #region Consolidated Level-Up Helpers (#1645)

    /// <summary>
    /// Returns character levels within the range that get +1 ability increase (every 4th level).
    /// </summary>
    public static List<int> GetAbilityIncreaseLevels(int currentTotalLevel, int levelsToAdd)
    {
        var result = new List<int>();
        for (int i = 1; i <= levelsToAdd; i++)
        {
            int charLevel = currentTotalLevel + i;
            if (charLevel % 4 == 0)
                result.Add(charLevel);
        }
        return result;
    }

    /// <summary>
    /// Calculates total HP gain for multiple levels, accounting for CON increases and retroactive HP.
    /// conIncreaseLevels: character levels where CON was increased (within the range).
    /// </summary>
    public static int CalculateConsolidatedHp(
        int hitDie, int baseCon, int previousLevelCount,
        int levelsToAdd, List<int> conIncreaseLevels)
    {
        int totalHp = 0;
        int effectiveCon = baseCon;
        int levelsBeforeCurrent = previousLevelCount;

        for (int i = 1; i <= levelsToAdd; i++)
        {
            int charLevel = previousLevelCount + i;

            // Check if CON increases this level (applied BEFORE HP calc for this level)
            if (conIncreaseLevels.Contains(charLevel))
            {
                int oldMod = CreatureDisplayService.CalculateAbilityBonus(effectiveCon);
                effectiveCon = Math.Min(255, effectiveCon + 1);
                int newMod = CreatureDisplayService.CalculateAbilityBonus(effectiveCon);
                int modChange = newMod - oldMod;

                if (modChange > 0)
                    totalHp += modChange * levelsBeforeCurrent; // Retro HP
            }

            int conMod = CreatureDisplayService.CalculateAbilityBonus(effectiveCon);
            totalHp += Math.Max(1, hitDie + conMod);
            levelsBeforeCurrent++;
        }

        return totalHp;
    }

    #endregion

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
            AbilityIncrease = input.AbilityIncrease
        };

        var existingHistory = LevelHistoryService.Decode(creature.Comment) ?? new List<LevelRecord>();
        existingHistory.Add(record);

        creature.Comment = LevelHistoryService.AppendToComment(
            creature.Comment,
            existingHistory,
            input.HistoryEncoding);
    }
}

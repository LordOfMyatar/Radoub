using System;
using System.Collections.Generic;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// Combat stats: BAB, attack bonus, attacks per round, saving throws.
/// </summary>
public partial class CreatureDisplayService
{
    #region Combat Stats

    /// <summary>
    /// Calculates Base Attack Bonus from class levels.
    /// </summary>
    public int CalculateBaseAttackBonus(UtcFile creature)
    {
        int totalBab = 0;

        foreach (var creatureClass in creature.ClassList)
        {
            var classBab = GetClassBab(creatureClass.Class, creatureClass.ClassLevel);
            totalBab += classBab;
        }

        return totalBab;
    }

    /// <summary>
    /// Gets the BAB for a specific class and level.
    /// </summary>
    public int GetClassBab(int classId, int classLevel)
    {
        if (classLevel <= 0)
            return 0;

        var attackTable = _gameDataService.Get2DAValue("classes", classId, "AttackBonusTable");
        if (string.IsNullOrEmpty(attackTable) || attackTable == "****")
        {
            return EstimateBab(classId, classLevel);
        }

        var babValue = _gameDataService.Get2DAValue(attackTable, classLevel - 1, "BAB");
        if (!string.IsNullOrEmpty(babValue) && babValue != "****" && int.TryParse(babValue, out int bab))
        {
            return bab;
        }

        return EstimateBab(classId, classLevel);
    }

    /// <summary>
    /// Estimates BAB when 2DA tables are not available.
    /// </summary>
    private static int EstimateBab(int classId, int classLevel)
    {
        var progression = classId switch
        {
            0 => 1.0,   // Barbarian - full
            1 => 0.75,  // Bard - 3/4
            2 => 0.75,  // Cleric - 3/4
            3 => 0.75,  // Druid - 3/4
            4 => 1.0,   // Fighter - full
            5 => 0.75,  // Monk - 3/4
            6 => 1.0,   // Paladin - full
            7 => 1.0,   // Ranger - full
            8 => 0.75,  // Rogue - 3/4
            9 => 0.5,   // Sorcerer - 1/2
            10 => 0.5,  // Wizard - 1/2
            11 => 0.75, // Shadowdancer - 3/4
            12 => 0.75, // Harper Scout - 3/4
            13 => 1.0,  // Arcane Archer - full
            14 => 0.75, // Assassin - 3/4
            15 => 1.0,  // Blackguard - full
            16 => 1.0,  // Champion of Torm - full
            17 => 1.0,  // Weapon Master - full
            18 => 0.5,  // Pale Master - 1/2
            19 => 0.75, // Shifter - 3/4
            20 => 1.0,  // Dwarven Defender - full
            21 => 0.75, // Dragon Disciple - 3/4
            27 => 1.0,  // Purple Dragon Knight - full
            _ => 0.75
        };

        return (int)(classLevel * progression);
    }

    /// <summary>
    /// Calculates attack bonus from equipped items.
    /// </summary>
    public int CalculateEquipmentAttackBonus(IEnumerable<Radoub.Formats.Uti.UtiFile?> equippedItems)
    {
        int totalBonus = 0;
        int highestEnhancement = 0;

        foreach (var item in equippedItems)
        {
            if (item == null) continue;

            foreach (var prop in item.Properties)
            {
                if (prop.PropertyName == 6) // Enhancement Bonus
                {
                    var bonus = prop.CostValue;
                    if (bonus > highestEnhancement)
                        highestEnhancement = bonus;
                }
                else if (prop.PropertyName == 56) // Attack Bonus
                {
                    totalBonus += prop.CostValue;
                }
            }
        }

        return totalBonus + highestEnhancement;
    }

    /// <summary>
    /// Gets complete combat stats breakdown for display.
    /// </summary>
    public CombatStats CalculateCombatStats(UtcFile creature, IEnumerable<Radoub.Formats.Uti.UtiFile?>? equippedItems = null)
    {
        var stats = new CombatStats();

        stats.BaseBab = CalculateBaseAttackBonus(creature);

        if (equippedItems != null)
        {
            stats.EquipmentBonus = CalculateEquipmentAttackBonus(equippedItems);
        }

        stats.TotalBab = stats.BaseBab + stats.EquipmentBonus;

        // Calculate APR from BAB, accounting for epic levels
        int totalHitDice = 0;
        foreach (var c in creature.ClassList)
            totalHitDice += c.ClassLevel;
        stats.AttacksPerRound = CalculateAttacksPerRound(stats.TotalBab, totalHitDice);

        // Build attack sequence string (e.g., "+16/+11/+6/+1")
        stats.AttackSequence = BuildAttackSequence(stats.TotalBab, stats.AttacksPerRound);

        return stats;
    }

    /// <summary>
    /// Calculates attacks per round from BAB and total hit dice.
    /// NWN subtracts epic BAB before calculating APR so epic levels
    /// don't grant extra attacks beyond the pre-epic cap of 4.
    /// Epic BAB = 1 + (hitDice - 21) / 2 for characters level 21+.
    /// </summary>
    public static int CalculateAttacksPerRound(int bab, int totalHitDice = 0)
    {
        if (bab <= 0) return 1;

        int effectiveBab = bab;
        if (totalHitDice >= 21)
        {
            int epicBab = 1 + (totalHitDice - 21) / 2;
            effectiveBab = Math.Max(1, bab - epicBab);
        }

        return 1 + Math.Min(3, (effectiveBab - 1) / 5);
    }

    /// <summary>
    /// Builds the attack sequence string (e.g., "+16/+11/+6/+1").
    /// </summary>
    public static string BuildAttackSequence(int bab, int apr)
    {
        if (apr <= 1) return FormatBonus(bab);

        var attacks = new List<string>();
        for (int i = 0; i < apr; i++)
        {
            attacks.Add(FormatBonus(bab - (i * 5)));
        }
        return string.Join("/", attacks);
    }

    /// <summary>
    /// Calculates base saving throws from class levels.
    /// </summary>
    public SavingThrows CalculateBaseSavingThrows(UtcFile creature)
    {
        var saves = new SavingThrows();

        foreach (var creatureClass in creature.ClassList)
        {
            var classSaves = GetClassSaves(creatureClass.Class, creatureClass.ClassLevel);
            saves.Fortitude += classSaves.Fortitude;
            saves.Reflex += classSaves.Reflex;
            saves.Will += classSaves.Will;
        }

        return saves;
    }

    /// <summary>
    /// Gets the saving throws for a specific class and level.
    /// </summary>
    public SavingThrows GetClassSaves(int classId, int classLevel)
    {
        var saves = new SavingThrows();
        if (classLevel <= 0)
            return saves;

        var saveTable = _gameDataService.Get2DAValue("classes", classId, "SavingThrowTable");
        if (string.IsNullOrEmpty(saveTable) || saveTable == "****")
        {
            return EstimateSaves(classId, classLevel);
        }

        // 2DA row index is level - 1
        var fortValue = _gameDataService.Get2DAValue(saveTable, classLevel - 1, "FortSave");
        var refValue = _gameDataService.Get2DAValue(saveTable, classLevel - 1, "RefSave");
        var willValue = _gameDataService.Get2DAValue(saveTable, classLevel - 1, "WillSave");

        if (!string.IsNullOrEmpty(fortValue) && fortValue != "****" && int.TryParse(fortValue, out int fort))
            saves.Fortitude = fort;
        else
            saves.Fortitude = EstimateSaves(classId, classLevel).Fortitude;

        if (!string.IsNullOrEmpty(refValue) && refValue != "****" && int.TryParse(refValue, out int refSave))
            saves.Reflex = refSave;
        else
            saves.Reflex = EstimateSaves(classId, classLevel).Reflex;

        if (!string.IsNullOrEmpty(willValue) && willValue != "****" && int.TryParse(willValue, out int will))
            saves.Will = will;
        else
            saves.Will = EstimateSaves(classId, classLevel).Will;

        return saves;
    }

    /// <summary>
    /// Estimates saving throws when 2DA tables are not available.
    /// Good save = 2 + level/2, Poor save = level/3
    /// </summary>
    private static SavingThrows EstimateSaves(int classId, int classLevel)
    {
        // Determine save progressions per class (good = true, poor = false)
        var (fortGood, refGood, willGood) = classId switch
        {
            0 => (true, false, false),   // Barbarian: Fort good
            1 => (false, true, true),    // Bard: Ref/Will good
            2 => (true, false, true),    // Cleric: Fort/Will good
            3 => (true, false, true),    // Druid: Fort/Will good
            4 => (true, false, false),   // Fighter: Fort good
            5 => (true, true, true),     // Monk: All good
            6 => (true, false, false),   // Paladin: Fort good
            7 => (true, true, false),    // Ranger: Fort/Ref good
            8 => (false, true, false),   // Rogue: Ref good
            9 => (false, false, true),   // Sorcerer: Will good
            10 => (false, false, true),  // Wizard: Will good
            11 => (false, true, false),  // Shadowdancer: Ref good
            12 => (false, true, true),   // Harper Scout: Ref/Will good
            13 => (true, true, false),   // Arcane Archer: Fort/Ref good
            14 => (false, true, false),  // Assassin: Ref good
            15 => (true, false, false),  // Blackguard: Fort good
            16 => (true, false, true),   // Champion of Torm: Fort/Will good
            17 => (false, true, false),  // Weapon Master: Ref good
            18 => (false, false, true),  // Pale Master: Will good
            19 => (true, true, true),    // Shifter: All good (per NWN2, varies)
            20 => (true, false, true),   // Dwarven Defender: Fort/Will good
            21 => (true, false, true),   // Dragon Disciple: Fort/Will good
            27 => (true, false, true),   // Purple Dragon Knight: Fort/Will good
            _ => (false, false, false)
        };

        int GoodSave(int level) => 2 + level / 2;
        int PoorSave(int level) => level / 3;

        return new SavingThrows
        {
            Fortitude = fortGood ? GoodSave(classLevel) : PoorSave(classLevel),
            Reflex = refGood ? GoodSave(classLevel) : PoorSave(classLevel),
            Will = willGood ? GoodSave(classLevel) : PoorSave(classLevel)
        };
    }

    #endregion
}

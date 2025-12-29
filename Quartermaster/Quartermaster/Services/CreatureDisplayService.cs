using System.Collections.Generic;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// Provides display name resolution for creature data using 2DA and TLK lookups.
/// Centralizes all creature stat display calculations and name lookups.
/// </summary>
public class CreatureDisplayService
{
    private readonly IGameDataService _gameDataService;

    public CreatureDisplayService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;
    }

    /// <summary>
    /// Gets the display name for a race ID.
    /// </summary>
    public string GetRaceName(byte raceId)
    {
        // Try 2DA/TLK lookup first
        var strRef = _gameDataService.Get2DAValue("racialtypes", raceId, "Name");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        // Fallback to hardcoded names
        return raceId switch
        {
            0 => "Dwarf",
            1 => "Elf",
            2 => "Gnome",
            3 => "Halfling",
            4 => "Half-Elf",
            5 => "Half-Orc",
            6 => "Human",
            _ => $"Race {raceId}"
        };
    }

    /// <summary>
    /// Gets the display name for a gender ID.
    /// </summary>
    public string GetGenderName(byte genderId)
    {
        // Try 2DA/TLK lookup first
        var strRef = _gameDataService.Get2DAValue("gender", genderId, "NAME");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        // Fallback to hardcoded names
        return genderId switch
        {
            0 => "Male",
            1 => "Female",
            2 => "Both",
            3 => "Other",
            4 => "None",
            _ => $"Gender {genderId}"
        };
    }

    /// <summary>
    /// Gets the display name for a class ID.
    /// </summary>
    public string GetClassName(int classId)
    {
        // Try 2DA/TLK lookup first
        var strRef = _gameDataService.Get2DAValue("classes", classId, "Name");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        // Fallback to hardcoded names
        return classId switch
        {
            0 => "Barbarian",
            1 => "Bard",
            2 => "Cleric",
            3 => "Druid",
            4 => "Fighter",
            5 => "Monk",
            6 => "Paladin",
            7 => "Ranger",
            8 => "Rogue",
            9 => "Sorcerer",
            10 => "Wizard",
            11 => "Shadowdancer",
            12 => "Harper Scout",
            13 => "Arcane Archer",
            14 => "Assassin",
            15 => "Blackguard",
            16 => "Champion of Torm",
            17 => "Weapon Master",
            18 => "Pale Master",
            19 => "Shifter",
            20 => "Dwarven Defender",
            21 => "Dragon Disciple",
            27 => "Purple Dragon Knight",
            _ => $"Class {classId}"
        };
    }

    /// <summary>
    /// Gets the display name for a feat ID.
    /// </summary>
    public string GetFeatName(int featId)
    {
        // Try 2DA/TLK lookup first
        var strRef = _gameDataService.Get2DAValue("feat", featId, "FEAT");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        // Fallback to hardcoded common feats
        return featId switch
        {
            0 => "Alertness",
            1 => "Ambidexterity",
            2 => "Armor Proficiency (Heavy)",
            3 => "Armor Proficiency (Light)",
            4 => "Armor Proficiency (Medium)",
            5 => "Blind-Fight",
            6 => "Called Shot",
            7 => "Cleave",
            8 => "Combat Casting",
            9 => "Deflect Arrows",
            10 => "Disarm",
            11 => "Dodge",
            _ => $"Feat {featId}"
        };
    }

    /// <summary>
    /// Gets the display name for a spell ID.
    /// </summary>
    public string GetSpellName(int spellId)
    {
        // Try 2DA/TLK lookup first
        var strRef = _gameDataService.Get2DAValue("spells", spellId, "Name");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        return $"Spell {spellId}";
    }

    /// <summary>
    /// Gets the display name for a skill ID.
    /// </summary>
    public string GetSkillName(int skillId)
    {
        // Try 2DA/TLK lookup first
        var strRef = _gameDataService.Get2DAValue("skills", skillId, "Name");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        // Fallback to hardcoded common skills
        return skillId switch
        {
            0 => "Animal Empathy",
            1 => "Concentration",
            2 => "Disable Trap",
            3 => "Discipline",
            4 => "Heal",
            5 => "Hide",
            6 => "Listen",
            7 => "Lore",
            8 => "Move Silently",
            9 => "Open Lock",
            10 => "Parry",
            11 => "Perform",
            12 => "Persuade",
            13 => "Pick Pocket",
            14 => "Search",
            15 => "Set Trap",
            16 => "Spellcraft",
            17 => "Spot",
            18 => "Taunt",
            19 => "Use Magic Device",
            20 => "Appraise",
            21 => "Tumble",
            22 => "Craft Trap",
            23 => "Bluff",
            24 => "Intimidate",
            25 => "Craft Armor",
            26 => "Craft Weapon",
            27 => "Ride",
            _ => $"Skill {skillId}"
        };
    }

    /// <summary>
    /// Gets the key ability for a skill (STR, DEX, INT, WIS, CHA, CON).
    /// </summary>
    public string GetSkillKeyAbility(int skillId)
    {
        // Try 2DA lookup first
        var ability = _gameDataService.Get2DAValue("skills", skillId, "KeyAbility");
        if (!string.IsNullOrEmpty(ability) && ability != "****")
            return ability;

        // Fallback to hardcoded values
        return skillId switch
        {
            0 => "CHA",  // Animal Empathy
            1 => "CON",  // Concentration
            2 => "INT",  // Disable Trap
            3 => "STR",  // Discipline
            4 => "WIS",  // Heal
            5 => "DEX",  // Hide
            6 => "WIS",  // Listen
            7 => "INT",  // Lore
            8 => "DEX",  // Move Silently
            9 => "DEX",  // Open Lock
            10 => "DEX", // Parry
            11 => "CHA", // Perform
            12 => "CHA", // Persuade
            13 => "DEX", // Pick Pocket
            14 => "INT", // Search
            15 => "DEX", // Set Trap
            16 => "INT", // Spellcraft
            17 => "WIS", // Spot
            18 => "CHA", // Taunt
            19 => "CHA", // Use Magic Device
            20 => "INT", // Appraise
            21 => "DEX", // Tumble
            22 => "INT", // Craft Trap
            23 => "CHA", // Bluff
            24 => "CHA", // Intimidate
            25 => "INT", // Craft Armor
            26 => "INT", // Craft Weapon
            27 => "DEX", // Ride
            _ => "INT"
        };
    }

    /// <summary>
    /// Gets the skills table name for a class (e.g., "cls_skill_fight" for Fighter).
    /// </summary>
    public string? GetClassSkillsTable(int classId)
    {
        var skillsTable = _gameDataService.Get2DAValue("classes", classId, "SkillsTable");
        if (!string.IsNullOrEmpty(skillsTable) && skillsTable != "****")
            return skillsTable;
        return null;
    }

    /// <summary>
    /// Checks if a skill is a class skill for the given class.
    /// </summary>
    public bool IsClassSkill(int classId, int skillId)
    {
        var skillsTable = GetClassSkillsTable(classId);
        if (skillsTable == null)
            return false;

        // The cls_skill_*.2da files have rows with SkillIndex and ClassSkill columns
        // We need to iterate through the rows to find the matching skill
        // Since we don't have row count, we iterate up to a reasonable limit
        for (int row = 0; row < 50; row++)
        {
            var skillIndexStr = _gameDataService.Get2DAValue(skillsTable, row, "SkillIndex");
            if (string.IsNullOrEmpty(skillIndexStr) || skillIndexStr == "****")
                break;

            if (int.TryParse(skillIndexStr, out int tableSkillId) && tableSkillId == skillId)
            {
                var classSkillStr = _gameDataService.Get2DAValue(skillsTable, row, "ClassSkill");
                return classSkillStr == "1";
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the set of class skill IDs for a given class.
    /// </summary>
    public HashSet<int> GetClassSkillIds(int classId)
    {
        var result = new HashSet<int>();
        var skillsTable = GetClassSkillsTable(classId);
        if (skillsTable == null)
            return result;

        // Iterate through the cls_skill_*.2da rows
        for (int row = 0; row < 50; row++)
        {
            var skillIndexStr = _gameDataService.Get2DAValue(skillsTable, row, "SkillIndex");
            if (string.IsNullOrEmpty(skillIndexStr) || skillIndexStr == "****")
                break;

            var classSkillStr = _gameDataService.Get2DAValue(skillsTable, row, "ClassSkill");
            if (classSkillStr == "1" && int.TryParse(skillIndexStr, out int skillId))
            {
                result.Add(skillId);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the combined set of class skill IDs for all classes the creature has.
    /// </summary>
    public HashSet<int> GetCombinedClassSkillIds(UtcFile creature)
    {
        var result = new HashSet<int>();
        foreach (var creatureClass in creature.ClassList)
        {
            var classSkills = GetClassSkillIds(creatureClass.Class);
            foreach (var skillId in classSkills)
            {
                result.Add(skillId);
            }
        }
        return result;
    }

    /// <summary>
    /// Gets the racial ability modifier for a specific ability.
    /// </summary>
    public int GetRacialModifier(byte raceId, string ability)
    {
        var columnName = ability.ToUpperInvariant() switch
        {
            "STR" => "StrAdjust",
            "DEX" => "DexAdjust",
            "CON" => "ConAdjust",
            "INT" => "IntAdjust",
            "WIS" => "WisAdjust",
            "CHA" => "ChaAdjust",
            _ => null
        };

        if (columnName == null)
            return 0;

        var value = _gameDataService.Get2DAValue("racialtypes", raceId, columnName);
        if (!string.IsNullOrEmpty(value) && value != "****" && int.TryParse(value, out int modifier))
            return modifier;

        return 0;
    }

    /// <summary>
    /// Gets all racial ability modifiers for a race.
    /// </summary>
    public RacialModifiers GetRacialModifiers(byte raceId)
    {
        return new RacialModifiers
        {
            Str = GetRacialModifier(raceId, "STR"),
            Dex = GetRacialModifier(raceId, "DEX"),
            Con = GetRacialModifier(raceId, "CON"),
            Int = GetRacialModifier(raceId, "INT"),
            Wis = GetRacialModifier(raceId, "WIS"),
            Cha = GetRacialModifier(raceId, "CHA")
        };
    }

    /// <summary>
    /// Calculates the ability bonus from an ability score.
    /// Standard D&D formula: (score - 10) / 2
    /// </summary>
    public static int CalculateAbilityBonus(int score)
    {
        return (score - 10) / 2;
    }

    /// <summary>
    /// Formats a bonus value with a + or - prefix.
    /// </summary>
    public static string FormatBonus(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

    /// <summary>
    /// Gets the full display name of a creature (first + last name).
    /// </summary>
    public static string GetCreatureFullName(UtcFile creature)
    {
        var firstName = creature.FirstName?.GetString(0) ?? "";
        var lastName = creature.LastName?.GetString(0) ?? "";
        var fullName = $"{firstName} {lastName}".Trim();

        if (string.IsNullOrEmpty(fullName))
            fullName = creature.Tag ?? "Unknown";

        return fullName;
    }

    /// <summary>
    /// Gets a summary line for a creature (race/gender/class).
    /// </summary>
    public string GetCreatureSummary(UtcFile creature)
    {
        var race = GetRaceName(creature.Race);
        var gender = GetGenderName(creature.Gender);

        var summary = $"{race} {gender}";

        if (creature.ClassList.Count > 0)
        {
            var primaryClass = creature.ClassList[0];
            var className = GetClassName(primaryClass.Class);
            summary += $" | {className} {primaryClass.ClassLevel}";
        }

        return summary;
    }

    /// <summary>
    /// Calculates Base Attack Bonus from class levels.
    /// Sums BAB from each class's attack table (cls_atk_*.2da).
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

        // Get the attack bonus table name from classes.2da
        var attackTable = _gameDataService.Get2DAValue("classes", classId, "AttackBonusTable");
        if (string.IsNullOrEmpty(attackTable) || attackTable == "****")
        {
            // Fallback: estimate based on class type (1, 3/4, or 1/2 progression)
            return EstimateBab(classId, classLevel);
        }

        // Look up BAB from the class-specific attack table
        // Row index is level - 1 (row 0 = level 1)
        var babValue = _gameDataService.Get2DAValue(attackTable, classLevel - 1, "BAB");
        if (!string.IsNullOrEmpty(babValue) && babValue != "****" && int.TryParse(babValue, out int bab))
        {
            return bab;
        }

        // Fallback if 2DA not available
        return EstimateBab(classId, classLevel);
    }

    /// <summary>
    /// Estimates BAB when 2DA tables are not available.
    /// Uses standard D&D 3E progression rates.
    /// </summary>
    private static int EstimateBab(int classId, int classLevel)
    {
        // Full BAB (Fighter, Barbarian, Paladin, Ranger): 1 per level
        // 3/4 BAB (Cleric, Druid, Monk, Rogue): 3/4 per level
        // 1/2 BAB (Wizard, Sorcerer): 1/2 per level
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
            _ => 0.75   // Default to 3/4
        };

        return (int)(classLevel * progression);
    }

    /// <summary>
    /// Calculates attack bonus from equipped items.
    /// Looks for Enhancement Bonus and Attack Bonus item properties.
    /// </summary>
    /// <param name="equippedItems">List of equipped items with their loaded UTI data</param>
    public int CalculateEquipmentAttackBonus(IEnumerable<Radoub.Formats.Uti.UtiFile?> equippedItems)
    {
        int totalBonus = 0;
        int highestEnhancement = 0; // Enhancement bonuses don't stack - take highest

        foreach (var item in equippedItems)
        {
            if (item == null) continue;

            foreach (var prop in item.Properties)
            {
                // PropertyName indices from itempropdef.2da:
                // 6 = Enhancement Bonus (doesn't stack - highest wins)
                // 56 = Attack Bonus (stacks)
                if (prop.PropertyName == 6) // Enhancement Bonus
                {
                    // CostValue is the bonus amount (index into iprp_bonuscost.2da, where value = row index)
                    var bonus = prop.CostValue;
                    if (bonus > highestEnhancement)
                        highestEnhancement = bonus;
                }
                else if (prop.PropertyName == 56) // Attack Bonus
                {
                    // Attack bonus stacks
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

        // Base Attack Bonus from class levels
        stats.BaseBab = CalculateBaseAttackBonus(creature);

        // Equipment bonus (if items provided)
        if (equippedItems != null)
        {
            stats.EquipmentBonus = CalculateEquipmentAttackBonus(equippedItems);
        }

        // Total BAB
        stats.TotalBab = stats.BaseBab + stats.EquipmentBonus;

        return stats;
    }
}

/// <summary>
/// Holds combat stats breakdown.
/// </summary>
public class CombatStats
{
    public int BaseBab { get; set; }
    public int EquipmentBonus { get; set; }
    public int TotalBab { get; set; }
}

/// <summary>
/// Holds racial ability modifiers.
/// </summary>
public class RacialModifiers
{
    public int Str { get; set; }
    public int Dex { get; set; }
    public int Con { get; set; }
    public int Int { get; set; }
    public int Wis { get; set; }
    public int Cha { get; set; }
}

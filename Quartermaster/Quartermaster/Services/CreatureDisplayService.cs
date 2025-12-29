using System.Collections.Generic;
using System.Linq;
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
    /// Checks if a skill can be used by all classes (AllClassesCanUse column in skills.2da).
    /// </summary>
    public bool IsSkillUniversal(int skillId)
    {
        var allClassesCanUse = _gameDataService.Get2DAValue("skills", skillId, "AllClassesCanUse");
        return allClassesCanUse == "1";
    }

    /// <summary>
    /// Checks if a skill appears in any of the character's class skill tables.
    /// If AllClassesCanUse is 0, and the skill doesn't appear in any cls_skill_*.2da for
    /// the character's classes, it's completely unavailable to them.
    /// </summary>
    public bool IsSkillAvailable(UtcFile creature, int skillId)
    {
        // Check if all classes can use this skill
        if (IsSkillUniversal(skillId))
            return true;

        // Check each class's skill table to see if the skill appears at all
        foreach (var creatureClass in creature.ClassList)
        {
            if (IsSkillInClassTable(creatureClass.Class, skillId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a skill appears in a class's skill table at all (regardless of ClassSkill value).
    /// </summary>
    private bool IsSkillInClassTable(int classId, int skillId)
    {
        var skillsTable = GetClassSkillsTable(classId);
        if (skillsTable == null)
            return false;

        for (int row = 0; row < 50; row++)
        {
            var skillIndexStr = _gameDataService.Get2DAValue(skillsTable, row, "SkillIndex");
            if (string.IsNullOrEmpty(skillIndexStr) || skillIndexStr == "****")
                break;

            if (int.TryParse(skillIndexStr, out int tableSkillId) && tableSkillId == skillId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the set of unavailable skill IDs for a creature (skills they cannot take at all).
    /// </summary>
    public HashSet<int> GetUnavailableSkillIds(UtcFile creature, int totalSkillCount)
    {
        var result = new HashSet<int>();
        for (int skillId = 0; skillId < totalSkillCount; skillId++)
        {
            if (!IsSkillAvailable(creature, skillId))
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

    #region Feat Methods

    /// <summary>
    /// Gets the toolset category for a feat (TOOLSCATEGORIES column).
    /// Returns: 1=Combat, 2=Active Combat, 3=Defensive, 4=Magical, 5=Class/Racial, 6=Other
    /// </summary>
    public FeatCategory GetFeatCategory(int featId)
    {
        var category = _gameDataService.Get2DAValue("feat", featId, "TOOLSCATEGORIES");
        if (!string.IsNullOrEmpty(category) && category != "****" && int.TryParse(category, out int catId))
        {
            return catId switch
            {
                1 => FeatCategory.Combat,
                2 => FeatCategory.ActiveCombat,
                3 => FeatCategory.Defensive,
                4 => FeatCategory.Magical,
                5 => FeatCategory.ClassRacial,
                6 => FeatCategory.Other,
                _ => FeatCategory.Other
            };
        }
        return FeatCategory.Other;
    }

    /// <summary>
    /// Gets the description StrRef for a feat.
    /// </summary>
    public string GetFeatDescription(int featId)
    {
        var strRef = _gameDataService.Get2DAValue("feat", featId, "DESCRIPTION");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var desc = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(desc))
                return desc;
        }
        return "";
    }

    /// <summary>
    /// Checks if a feat can be used by all classes (ALLCLASSESCANUSE column).
    /// </summary>
    public bool IsFeatUniversal(int featId)
    {
        var allClassesCanUse = _gameDataService.Get2DAValue("feat", featId, "ALLCLASSESCANUSE");
        return allClassesCanUse == "1";
    }

    /// <summary>
    /// Gets all valid feat IDs from feat.2da.
    /// Iterates until finding an empty/invalid row.
    /// </summary>
    public List<int> GetAllFeatIds()
    {
        var featIds = new List<int>();

        // feat.2da can have 1000+ rows, iterate until we find empty
        for (int i = 0; i < 2000; i++)
        {
            var label = _gameDataService.Get2DAValue("feat", i, "LABEL");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                // Check if we've found any feats - if yes, we've hit the end
                // If no, keep looking (some 2DAs have gaps)
                if (featIds.Count > 100)
                    break;
                continue;
            }

            var featName = _gameDataService.Get2DAValue("feat", i, "FEAT");
            // Skip feats with no name (internal/unused)
            if (string.IsNullOrEmpty(featName) || featName == "****")
                continue;

            featIds.Add(i);
        }

        return featIds;
    }

    /// <summary>
    /// Gets detailed feat information for display.
    /// </summary>
    public FeatInfo GetFeatInfo(int featId)
    {
        return new FeatInfo
        {
            FeatId = featId,
            Name = GetFeatName(featId),
            Description = GetFeatDescription(featId),
            Category = GetFeatCategory(featId),
            IsUniversal = IsFeatUniversal(featId)
        };
    }

    /// <summary>
    /// Gets the set of feat IDs granted by a class at all levels.
    /// Reads from cls_feat_*.2da files.
    /// </summary>
    public HashSet<int> GetClassGrantedFeatIds(int classId)
    {
        var result = new HashSet<int>();

        // Get the feat table name from classes.2da
        var featTable = _gameDataService.Get2DAValue("classes", classId, "FeatsTable");
        if (string.IsNullOrEmpty(featTable) || featTable == "****")
            return result;

        // Iterate through cls_feat_*.2da rows
        for (int row = 0; row < 200; row++)
        {
            var featIndexStr = _gameDataService.Get2DAValue(featTable, row, "FeatIndex");
            if (string.IsNullOrEmpty(featIndexStr) || featIndexStr == "****")
                break;

            if (int.TryParse(featIndexStr, out int featId))
            {
                // Check if it's granted (List column = 3 means automatic)
                var listType = _gameDataService.Get2DAValue(featTable, row, "List");
                if (listType == "3") // Automatic/granted feat
                {
                    result.Add(featId);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets combined granted feats from all of a creature's classes.
    /// </summary>
    public HashSet<int> GetCombinedGrantedFeatIds(UtcFile creature)
    {
        var result = new HashSet<int>();
        foreach (var creatureClass in creature.ClassList)
        {
            var classFeats = GetClassGrantedFeatIds(creatureClass.Class);
            foreach (var featId in classFeats)
            {
                result.Add(featId);
            }
        }
        return result;
    }

    /// <summary>
    /// Checks if a feat is available to a creature (can be selected).
    /// A feat is available if it's universal OR appears in any of the creature's class feat tables.
    /// </summary>
    public bool IsFeatAvailable(UtcFile creature, int featId)
    {
        // Universal feats are available to all
        if (IsFeatUniversal(featId))
            return true;

        // Check each class's feat table
        foreach (var creatureClass in creature.ClassList)
        {
            if (IsFeatInClassTable(creatureClass.Class, featId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a feat appears in a class's feat table (regardless of List type).
    /// </summary>
    private bool IsFeatInClassTable(int classId, int featId)
    {
        var featTable = _gameDataService.Get2DAValue("classes", classId, "FeatsTable");
        if (string.IsNullOrEmpty(featTable) || featTable == "****")
            return false;

        for (int row = 0; row < 300; row++)
        {
            var featIndexStr = _gameDataService.Get2DAValue(featTable, row, "FeatIndex");
            if (string.IsNullOrEmpty(featIndexStr) || featIndexStr == "****")
                break;

            if (int.TryParse(featIndexStr, out int tableFeatId) && tableFeatId == featId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the set of unavailable feat IDs for a creature.
    /// </summary>
    public HashSet<int> GetUnavailableFeatIds(UtcFile creature, IEnumerable<int> allFeatIds)
    {
        var result = new HashSet<int>();
        foreach (var featId in allFeatIds)
        {
            if (!IsFeatAvailable(creature, featId))
            {
                result.Add(featId);
            }
        }
        return result;
    }

    /// <summary>
    /// Gets the prerequisites for a feat.
    /// </summary>
    public FeatPrerequisites GetFeatPrerequisites(int featId)
    {
        var prereqs = new FeatPrerequisites { FeatId = featId };

        // Required feats (AND - must have all)
        var prereq1 = _gameDataService.Get2DAValue("feat", featId, "PREREQFEAT1");
        if (!string.IsNullOrEmpty(prereq1) && prereq1 != "****" && int.TryParse(prereq1, out int feat1))
            prereqs.RequiredFeats.Add(feat1);

        var prereq2 = _gameDataService.Get2DAValue("feat", featId, "PREREQFEAT2");
        if (!string.IsNullOrEmpty(prereq2) && prereq2 != "****" && int.TryParse(prereq2, out int feat2))
            prereqs.RequiredFeats.Add(feat2);

        // Or-required feats (OR - must have at least one)
        for (int i = 0; i <= 4; i++)
        {
            var orReq = _gameDataService.Get2DAValue("feat", featId, $"OrReqFeat{i}");
            if (!string.IsNullOrEmpty(orReq) && orReq != "****" && int.TryParse(orReq, out int orFeatId))
                prereqs.OrRequiredFeats.Add(orFeatId);
        }

        // Minimum ability scores
        var minStr = _gameDataService.Get2DAValue("feat", featId, "MINSTR");
        if (!string.IsNullOrEmpty(minStr) && minStr != "****" && int.TryParse(minStr, out int str))
            prereqs.MinStr = str;

        var minDex = _gameDataService.Get2DAValue("feat", featId, "MINDEX");
        if (!string.IsNullOrEmpty(minDex) && minDex != "****" && int.TryParse(minDex, out int dex))
            prereqs.MinDex = dex;

        var minInt = _gameDataService.Get2DAValue("feat", featId, "MININT");
        if (!string.IsNullOrEmpty(minInt) && minInt != "****" && int.TryParse(minInt, out int intel))
            prereqs.MinInt = intel;

        var minWis = _gameDataService.Get2DAValue("feat", featId, "MINWIS");
        if (!string.IsNullOrEmpty(minWis) && minWis != "****" && int.TryParse(minWis, out int wis))
            prereqs.MinWis = wis;

        var minCon = _gameDataService.Get2DAValue("feat", featId, "MINCON");
        if (!string.IsNullOrEmpty(minCon) && minCon != "****" && int.TryParse(minCon, out int con))
            prereqs.MinCon = con;

        var minCha = _gameDataService.Get2DAValue("feat", featId, "MINCHA");
        if (!string.IsNullOrEmpty(minCha) && minCha != "****" && int.TryParse(minCha, out int cha))
            prereqs.MinCha = cha;

        // Minimum BAB
        var minBab = _gameDataService.Get2DAValue("feat", featId, "MINATTACKBONUS");
        if (!string.IsNullOrEmpty(minBab) && minBab != "****" && int.TryParse(minBab, out int bab))
            prereqs.MinBab = bab;

        // Minimum spell level
        var minSpell = _gameDataService.Get2DAValue("feat", featId, "MINSPELLLVL");
        if (!string.IsNullOrEmpty(minSpell) && minSpell != "****" && int.TryParse(minSpell, out int spell))
            prereqs.MinSpellLevel = spell;

        // Required skills
        var reqSkill = _gameDataService.Get2DAValue("feat", featId, "REQSKILL");
        var reqSkillRanks = _gameDataService.Get2DAValue("feat", featId, "ReqSkillMinRanks");
        if (!string.IsNullOrEmpty(reqSkill) && reqSkill != "****" && int.TryParse(reqSkill, out int skillId) &&
            !string.IsNullOrEmpty(reqSkillRanks) && int.TryParse(reqSkillRanks, out int ranks))
        {
            prereqs.RequiredSkills.Add((skillId, ranks));
        }

        var reqSkill2 = _gameDataService.Get2DAValue("feat", featId, "REQSKILL2");
        var reqSkillRanks2 = _gameDataService.Get2DAValue("feat", featId, "ReqSkillMinRanks2");
        if (!string.IsNullOrEmpty(reqSkill2) && reqSkill2 != "****" && int.TryParse(reqSkill2, out int skillId2) &&
            !string.IsNullOrEmpty(reqSkillRanks2) && int.TryParse(reqSkillRanks2, out int ranks2))
        {
            prereqs.RequiredSkills.Add((skillId2, ranks2));
        }

        // Level requirements
        var minLevel = _gameDataService.Get2DAValue("feat", featId, "MinLevel");
        var minLevelClass = _gameDataService.Get2DAValue("feat", featId, "MinLevelClass");
        if (!string.IsNullOrEmpty(minLevel) && minLevel != "****" && int.TryParse(minLevel, out int lvl))
        {
            prereqs.MinLevel = lvl;
            if (!string.IsNullOrEmpty(minLevelClass) && minLevelClass != "****" && int.TryParse(minLevelClass, out int classId))
                prereqs.MinLevelClass = classId;
        }

        var maxLevel = _gameDataService.Get2DAValue("feat", featId, "MaxLevel");
        if (!string.IsNullOrEmpty(maxLevel) && maxLevel != "****" && int.TryParse(maxLevel, out int max))
            prereqs.MaxLevel = max;

        // Epic requirement
        var preReqEpic = _gameDataService.Get2DAValue("feat", featId, "PreReqEpic");
        prereqs.RequiresEpic = preReqEpic == "1";

        return prereqs;
    }

    /// <summary>
    /// Checks if a creature meets the prerequisites for a feat.
    /// Returns a result with details about what is/isn't met.
    /// </summary>
    public FeatPrereqResult CheckFeatPrerequisites(UtcFile creature, int featId, HashSet<ushort> creatureFeats)
    {
        var prereqs = GetFeatPrerequisites(featId);
        var result = new FeatPrereqResult { FeatId = featId };

        // Check required feats (AND)
        foreach (var reqFeat in prereqs.RequiredFeats)
        {
            var met = creatureFeats.Contains((ushort)reqFeat);
            result.RequiredFeatsMet.Add((reqFeat, GetFeatName(reqFeat), met));
            if (!met) result.AllMet = false;
        }

        // Check or-required feats (OR - at least one)
        if (prereqs.OrRequiredFeats.Count > 0)
        {
            bool anyMet = false;
            foreach (var orFeat in prereqs.OrRequiredFeats)
            {
                var met = creatureFeats.Contains((ushort)orFeat);
                result.OrRequiredFeatsMet.Add((orFeat, GetFeatName(orFeat), met));
                if (met) anyMet = true;
            }
            if (!anyMet) result.AllMet = false;
        }

        // Check ability scores
        if (prereqs.MinStr > 0)
        {
            var met = creature.Str >= prereqs.MinStr;
            result.AbilityRequirements.Add(($"STR {prereqs.MinStr}+", met));
            if (!met) result.AllMet = false;
        }
        if (prereqs.MinDex > 0)
        {
            var met = creature.Dex >= prereqs.MinDex;
            result.AbilityRequirements.Add(($"DEX {prereqs.MinDex}+", met));
            if (!met) result.AllMet = false;
        }
        if (prereqs.MinInt > 0)
        {
            var met = creature.Int >= prereqs.MinInt;
            result.AbilityRequirements.Add(($"INT {prereqs.MinInt}+", met));
            if (!met) result.AllMet = false;
        }
        if (prereqs.MinWis > 0)
        {
            var met = creature.Wis >= prereqs.MinWis;
            result.AbilityRequirements.Add(($"WIS {prereqs.MinWis}+", met));
            if (!met) result.AllMet = false;
        }
        if (prereqs.MinCon > 0)
        {
            var met = creature.Con >= prereqs.MinCon;
            result.AbilityRequirements.Add(($"CON {prereqs.MinCon}+", met));
            if (!met) result.AllMet = false;
        }
        if (prereqs.MinCha > 0)
        {
            var met = creature.Cha >= prereqs.MinCha;
            result.AbilityRequirements.Add(($"CHA {prereqs.MinCha}+", met));
            if (!met) result.AllMet = false;
        }

        // Check BAB
        if (prereqs.MinBab > 0)
        {
            var bab = CalculateBaseAttackBonus(creature);
            var met = bab >= prereqs.MinBab;
            result.OtherRequirements.Add(($"BAB {prereqs.MinBab}+", met));
            if (!met) result.AllMet = false;
        }

        // Check spell level (simplified - just note requirement)
        if (prereqs.MinSpellLevel > 0)
        {
            // Can't easily check this without spell slot analysis
            result.OtherRequirements.Add(($"Cast level {prereqs.MinSpellLevel} spells", null));
        }

        // Check skills
        foreach (var (skillId, minRanks) in prereqs.RequiredSkills)
        {
            var skillName = GetSkillName(skillId);
            var ranks = skillId < creature.SkillList.Count ? creature.SkillList[skillId] : 0;
            var met = ranks >= minRanks;
            result.SkillRequirements.Add(($"{skillName} {minRanks}+", met));
            if (!met) result.AllMet = false;
        }

        // Check level
        if (prereqs.MinLevel > 0)
        {
            if (prereqs.MinLevelClass.HasValue)
            {
                var className = GetClassName(prereqs.MinLevelClass.Value);
                var classLevel = creature.ClassList
                    .Where(c => c.Class == prereqs.MinLevelClass.Value)
                    .Select(c => (int)c.ClassLevel)
                    .FirstOrDefault();
                var met = classLevel >= prereqs.MinLevel;
                result.OtherRequirements.Add(($"{className} level {prereqs.MinLevel}+", met));
                if (!met) result.AllMet = false;
            }
            else
            {
                var totalLevel = creature.ClassList.Sum(c => c.ClassLevel);
                var met = totalLevel >= prereqs.MinLevel;
                result.OtherRequirements.Add(($"Character level {prereqs.MinLevel}+", met));
                if (!met) result.AllMet = false;
            }
        }

        if (prereqs.MaxLevel > 0)
        {
            var totalLevel = creature.ClassList.Sum(c => c.ClassLevel);
            var met = totalLevel <= prereqs.MaxLevel;
            result.OtherRequirements.Add(($"Max level {prereqs.MaxLevel}", met));
            if (!met) result.AllMet = false;
        }

        // Epic requirement
        if (prereqs.RequiresEpic)
        {
            var totalLevel = creature.ClassList.Sum(c => c.ClassLevel);
            var met = totalLevel >= 21;
            result.OtherRequirements.Add(("Epic (level 21+)", met));
            if (!met) result.AllMet = false;
        }

        result.HasPrerequisites = prereqs.RequiredFeats.Count > 0 ||
                                   prereqs.OrRequiredFeats.Count > 0 ||
                                   prereqs.MinStr > 0 || prereqs.MinDex > 0 ||
                                   prereqs.MinInt > 0 || prereqs.MinWis > 0 ||
                                   prereqs.MinCon > 0 || prereqs.MinCha > 0 ||
                                   prereqs.MinBab > 0 || prereqs.MinSpellLevel > 0 ||
                                   prereqs.RequiredSkills.Count > 0 ||
                                   prereqs.MinLevel > 0 || prereqs.MaxLevel > 0 ||
                                   prereqs.RequiresEpic;

        return result;
    }

    #endregion
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

/// <summary>
/// Feat category from TOOLSCATEGORIES column in feat.2da.
/// </summary>
public enum FeatCategory
{
    Combat = 1,
    ActiveCombat = 2,
    Defensive = 3,
    Magical = 4,
    ClassRacial = 5,
    Other = 6
}

/// <summary>
/// Detailed feat information for display.
/// </summary>
public class FeatInfo
{
    public int FeatId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public FeatCategory Category { get; set; }
    public bool IsUniversal { get; set; }
}

/// <summary>
/// Raw prerequisites data from feat.2da.
/// </summary>
public class FeatPrerequisites
{
    public int FeatId { get; set; }
    public List<int> RequiredFeats { get; set; } = new();
    public List<int> OrRequiredFeats { get; set; } = new();
    public int MinStr { get; set; }
    public int MinDex { get; set; }
    public int MinInt { get; set; }
    public int MinWis { get; set; }
    public int MinCon { get; set; }
    public int MinCha { get; set; }
    public int MinBab { get; set; }
    public int MinSpellLevel { get; set; }
    public List<(int SkillId, int MinRanks)> RequiredSkills { get; set; } = new();
    public int MinLevel { get; set; }
    public int? MinLevelClass { get; set; }
    public int MaxLevel { get; set; }
    public bool RequiresEpic { get; set; }
}

/// <summary>
/// Result of checking feat prerequisites against a creature.
/// </summary>
public class FeatPrereqResult
{
    public int FeatId { get; set; }
    public bool AllMet { get; set; } = true;
    public bool HasPrerequisites { get; set; }

    /// <summary>Required feats (AND): (FeatId, FeatName, Met)</summary>
    public List<(int FeatId, string Name, bool Met)> RequiredFeatsMet { get; set; } = new();

    /// <summary>Or-required feats (OR - need at least one): (FeatId, FeatName, Met)</summary>
    public List<(int FeatId, string Name, bool Met)> OrRequiredFeatsMet { get; set; } = new();

    /// <summary>Ability requirements: (Description, Met)</summary>
    public List<(string Description, bool Met)> AbilityRequirements { get; set; } = new();

    /// <summary>Skill requirements: (Description, Met)</summary>
    public List<(string Description, bool Met)> SkillRequirements { get; set; } = new();

    /// <summary>Other requirements: (Description, Met or null if unknown)</summary>
    public List<(string Description, bool? Met)> OtherRequirements { get; set; } = new();

    /// <summary>
    /// Builds a tooltip string showing all prerequisites and their status.
    /// </summary>
    public string GetTooltip()
    {
        if (!HasPrerequisites)
            return "No prerequisites";

        var lines = new List<string>();
        lines.Add("Prerequisites:");

        foreach (var (_, name, met) in RequiredFeatsMet)
            lines.Add($"  {(met ? "✓" : "✗")} {name}");

        if (OrRequiredFeatsMet.Count > 0)
        {
            var anyMet = OrRequiredFeatsMet.Any(o => o.Met);
            lines.Add($"  {(anyMet ? "✓" : "✗")} One of:");
            foreach (var (_, name, met) in OrRequiredFeatsMet)
                lines.Add($"    {(met ? "✓" : "○")} {name}");
        }

        foreach (var (desc, met) in AbilityRequirements)
            lines.Add($"  {(met ? "✓" : "✗")} {desc}");

        foreach (var (desc, met) in SkillRequirements)
            lines.Add($"  {(met ? "✓" : "✗")} {desc}");

        foreach (var (desc, met) in OtherRequirements)
            lines.Add($"  {(met.HasValue ? (met.Value ? "✓" : "✗") : "?")} {desc}");

        return string.Join("\n", lines);
    }
}

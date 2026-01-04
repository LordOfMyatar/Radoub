using System.Collections.Generic;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// Provides skill name lookups and class skill calculations.
/// Uses 2DA and TLK for game data resolution.
/// </summary>
public class SkillService
{
    private readonly IGameDataService _gameDataService;

    public SkillService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;
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
}

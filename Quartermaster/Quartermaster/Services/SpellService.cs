using System.Collections.Generic;
using Radoub.Formats.Services;

namespace Quartermaster.Services;

/// <summary>
/// Provides spell name lookups, spell info, and caster class calculations.
/// Uses 2DA and TLK for game data resolution.
/// </summary>
public class SpellService
{
    private readonly IGameDataService _gameDataService;

    public SpellService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;
    }

    /// <summary>
    /// Gets the display name for a spell ID.
    /// </summary>
    public string GetSpellName(int spellId)
    {
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
    /// Gets all valid spell IDs from spells.2da.
    /// </summary>
    public List<int> GetAllSpellIds()
    {
        var spellIds = new List<int>();

        for (int i = 0; i < 1000; i++)
        {
            var label = _gameDataService.Get2DAValue("spells", i, "Label");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                if (spellIds.Count > 100)
                    break;
                continue;
            }

            var spellName = _gameDataService.Get2DAValue("spells", i, "Name");
            if (string.IsNullOrEmpty(spellName) || spellName == "****")
                continue;

            spellIds.Add(i);
        }

        return spellIds;
    }

    /// <summary>
    /// Gets detailed spell information from spells.2da.
    /// </summary>
    public SpellInfo? GetSpellInfo(int spellId)
    {
        var label = _gameDataService.Get2DAValue("spells", spellId, "Label");
        if (string.IsNullOrEmpty(label) || label == "****")
            return null;

        var info = new SpellInfo
        {
            SpellId = spellId,
            Name = GetSpellName(spellId)
        };

        // Innate level
        var innateStr = _gameDataService.Get2DAValue("spells", spellId, "Innate");
        if (!string.IsNullOrEmpty(innateStr) && innateStr != "****" && int.TryParse(innateStr, out int innate))
            info.InnateLevel = innate;

        // School
        var schoolStr = _gameDataService.Get2DAValue("spells", spellId, "School");
        if (!string.IsNullOrEmpty(schoolStr) && schoolStr != "****")
        {
            info.School = schoolStr.ToUpperInvariant() switch
            {
                "A" => SpellSchool.Abjuration,
                "C" => SpellSchool.Conjuration,
                "D" => SpellSchool.Divination,
                "E" => SpellSchool.Enchantment,
                "V" => SpellSchool.Evocation,
                "I" => SpellSchool.Illusion,
                "N" => SpellSchool.Necromancy,
                "T" => SpellSchool.Transmutation,
                _ => SpellSchool.Unknown
            };
        }

        // Class spell levels
        var bardLevel = _gameDataService.Get2DAValue("spells", spellId, "Bard");
        if (!string.IsNullOrEmpty(bardLevel) && bardLevel != "****" && int.TryParse(bardLevel, out int bard))
            info.ClassLevels[1] = bard;

        var clericLevel = _gameDataService.Get2DAValue("spells", spellId, "Cleric");
        if (!string.IsNullOrEmpty(clericLevel) && clericLevel != "****" && int.TryParse(clericLevel, out int cleric))
            info.ClassLevels[2] = cleric;

        var druidLevel = _gameDataService.Get2DAValue("spells", spellId, "Druid");
        if (!string.IsNullOrEmpty(druidLevel) && druidLevel != "****" && int.TryParse(druidLevel, out int druid))
            info.ClassLevels[3] = druid;

        var paladinLevel = _gameDataService.Get2DAValue("spells", spellId, "Paladin");
        if (!string.IsNullOrEmpty(paladinLevel) && paladinLevel != "****" && int.TryParse(paladinLevel, out int paladin))
            info.ClassLevels[6] = paladin;

        var rangerLevel = _gameDataService.Get2DAValue("spells", spellId, "Ranger");
        if (!string.IsNullOrEmpty(rangerLevel) && rangerLevel != "****" && int.TryParse(rangerLevel, out int ranger))
            info.ClassLevels[7] = ranger;

        var wizSorcLevel = _gameDataService.Get2DAValue("spells", spellId, "Wiz_Sorc");
        if (!string.IsNullOrEmpty(wizSorcLevel) && wizSorcLevel != "****" && int.TryParse(wizSorcLevel, out int wizsorc))
        {
            info.ClassLevels[9] = wizsorc;  // Sorcerer
            info.ClassLevels[10] = wizsorc; // Wizard
        }

        return info;
    }

    /// <summary>
    /// Gets the spell school name.
    /// </summary>
    public static string GetSpellSchoolName(SpellSchool school)
    {
        return school switch
        {
            SpellSchool.Abjuration => "Abjuration",
            SpellSchool.Conjuration => "Conjuration",
            SpellSchool.Divination => "Divination",
            SpellSchool.Enchantment => "Enchantment",
            SpellSchool.Evocation => "Evocation",
            SpellSchool.Illusion => "Illusion",
            SpellSchool.Necromancy => "Necromancy",
            SpellSchool.Transmutation => "Transmutation",
            _ => "General"
        };
    }

    /// <summary>
    /// Gets the maximum spell level a class can cast at a given class level.
    /// </summary>
    /// <param name="classId">The class ID</param>
    /// <param name="classLevel">The level in that class</param>
    /// <returns>Maximum spell level (0-9), or -1 if not a caster class</returns>
    public int GetMaxSpellLevel(int classId, int classLevel)
    {
        var spellGainTable = _gameDataService.Get2DAValue("classes", classId, "SpellGainTable");
        if (string.IsNullOrEmpty(spellGainTable) || spellGainTable == "****")
            return -1;

        int rowIndex = classLevel - 1;
        if (rowIndex < 0) return -1;

        int maxSpellLevel = -1;

        for (int spellLevel = 0; spellLevel <= 9; spellLevel++)
        {
            var columnName = $"NumSpellLevels{spellLevel}";
            var slotsStr = _gameDataService.Get2DAValue(spellGainTable, rowIndex, columnName);

            if (!string.IsNullOrEmpty(slotsStr) && slotsStr != "****" && slotsStr != "-")
            {
                if (int.TryParse(slotsStr, out int slots) && slots > 0)
                {
                    maxSpellLevel = spellLevel;
                }
            }
        }

        return maxSpellLevel;
    }

    /// <summary>
    /// Checks if a class is a spellcasting class.
    /// </summary>
    public bool IsCasterClass(int classId)
    {
        var spellGainTable = _gameDataService.Get2DAValue("classes", classId, "SpellGainTable");
        return !string.IsNullOrEmpty(spellGainTable) && spellGainTable != "****";
    }

    /// <summary>
    /// Checks if a class is a spontaneous caster (Sorcerer, Bard).
    /// </summary>
    public bool IsSpontaneousCaster(int classId)
    {
        var memorizesSpells = _gameDataService.Get2DAValue("classes", classId, "MemorizesSpells");
        if (!string.IsNullOrEmpty(memorizesSpells) && memorizesSpells != "****")
        {
            if (int.TryParse(memorizesSpells, out int value))
            {
                return value != 1;
            }
        }
        return true;
    }

    /// <summary>
    /// Gets the number of spell slots available at each spell level for a class at a given level.
    /// </summary>
    /// <param name="classId">The class ID</param>
    /// <param name="classLevel">The level in that class</param>
    /// <returns>Array of 10 integers (indices 0-9) with slot counts per spell level, or null if not a caster</returns>
    public int[]? GetSpellSlots(int classId, int classLevel)
    {
        var spellGainTable = _gameDataService.Get2DAValue("classes", classId, "SpellGainTable");
        if (string.IsNullOrEmpty(spellGainTable) || spellGainTable == "****")
            return null;

        int rowIndex = classLevel - 1;
        if (rowIndex < 0) return null;

        var slots = new int[10];

        for (int spellLevel = 0; spellLevel <= 9; spellLevel++)
        {
            var columnName = $"NumSpellLevels{spellLevel}";
            var slotsStr = _gameDataService.Get2DAValue(spellGainTable, rowIndex, columnName);

            if (!string.IsNullOrEmpty(slotsStr) && slotsStr != "****" && slotsStr != "-")
            {
                if (int.TryParse(slotsStr, out int count))
                {
                    slots[spellLevel] = count;
                }
            }
        }

        return slots;
    }
}

/// <summary>
/// Spell schools from spells.2da
/// </summary>
public enum SpellSchool
{
    Abjuration = 0,
    Conjuration = 1,
    Divination = 2,
    Enchantment = 3,
    Evocation = 4,
    Illusion = 5,
    Necromancy = 6,
    Transmutation = 7,
    Unknown = -1
}

/// <summary>
/// Spell information from spells.2da
/// </summary>
public class SpellInfo
{
    public int SpellId { get; set; }
    public string Name { get; set; } = "";
    public int InnateLevel { get; set; }
    public SpellSchool School { get; set; }

    /// <summary>
    /// Spell levels by class ID.
    /// </summary>
    public Dictionary<int, int> ClassLevels { get; set; } = new();

    /// <summary>
    /// Gets the spell level for a specific class, or -1 if not available.
    /// </summary>
    public int GetLevelForClass(int classId)
    {
        return ClassLevels.TryGetValue(classId, out int level) ? level : -1;
    }
}

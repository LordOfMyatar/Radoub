using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Logging;
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
    /// NWN spells.2da has gaps, so we must scan the full range.
    /// Base game has ~550 spells, but custom content (CEP, PRC) can add more.
    /// </summary>
    public List<int> GetAllSpellIds()
    {
        var spellIds = new List<int>();
        int consecutiveEmpty = 0;
        const int maxConsecutiveEmpty = 100; // Stop after 100 consecutive empty rows

        // Scan up to 2000 to support custom content
        for (int i = 0; i < 2000; i++)
        {
            var label = _gameDataService.Get2DAValue("spells", i, "Label");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                consecutiveEmpty++;
                // Only break if we've found spells AND hit many consecutive empty rows
                if (spellIds.Count > 0 && consecutiveEmpty >= maxConsecutiveEmpty)
                    break;
                continue;
            }

            consecutiveEmpty = 0; // Reset counter when we find a valid row

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

        // cls_spgn_*.2da columns are "SpellLevel0" through "SpellLevel9"
        for (int spellLevel = 0; spellLevel <= 9; spellLevel++)
        {
            var columnName = $"SpellLevel{spellLevel}";
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
        UnifiedLogger.Log(LogLevel.INFO, $"GetSpellSlots: classId={classId}, classLevel={classLevel}, SpellGainTable={spellGainTable}", "SpellService", "🔮");
        if (string.IsNullOrEmpty(spellGainTable) || spellGainTable == "****")
            return null;

        // Log columns for debugging
        var twoDA = _gameDataService.Get2DA(spellGainTable);
        if (twoDA != null)
        {
            UnifiedLogger.Log(LogLevel.INFO, $"GetSpellSlots: {spellGainTable} columns = [{string.Join(", ", twoDA.Columns)}]", "SpellService", "🔮");
        }

        int rowIndex = classLevel - 1;
        if (rowIndex < 0) return null;

        var slots = new int[10];

        for (int spellLevel = 0; spellLevel <= 9; spellLevel++)
        {
            // Column name is "SpellLevel0", "SpellLevel1", etc. in cls_spgn_* tables
            var columnName = $"SpellLevel{spellLevel}";
            var slotsStr = _gameDataService.Get2DAValue(spellGainTable, rowIndex, columnName);

            if (!string.IsNullOrEmpty(slotsStr) && slotsStr != "****" && slotsStr != "-")
            {
                if (int.TryParse(slotsStr, out int count))
                {
                    slots[spellLevel] = count;
                }
            }
        }

        UnifiedLogger.Log(LogLevel.INFO, $"GetSpellSlots: Result slots = [{string.Join(",", slots)}]", "SpellService", "🔮");
        return slots;
    }

    /// <summary>
    /// Gets the number of spells a spontaneous caster can know at each spell level.
    /// Uses the SpellKnownTable (cls_spkn_*) from classes.2da.
    /// </summary>
    /// <param name="classId">The class ID</param>
    /// <param name="classLevel">The level in that class</param>
    /// <returns>Array of 10 integers (indices 0-9) with max known spells per level, or null if not applicable</returns>
    public int[]? GetSpellsKnownLimit(int classId, int classLevel)
    {
        var spellKnownTable = _gameDataService.Get2DAValue("classes", classId, "SpellKnownTable");
        if (string.IsNullOrEmpty(spellKnownTable) || spellKnownTable == "****")
            return null;

        int rowIndex = classLevel - 1;
        if (rowIndex < 0) return null;

        var limits = new int[10];

        for (int spellLevel = 0; spellLevel <= 9; spellLevel++)
        {
            // Column name is "SpellLevel0", "SpellLevel1", etc. in cls_spkn_* tables
            var columnName = $"SpellLevel{spellLevel}";
            var limitStr = _gameDataService.Get2DAValue(spellKnownTable, rowIndex, columnName);

            if (!string.IsNullOrEmpty(limitStr) && limitStr != "****" && limitStr != "-")
            {
                if (int.TryParse(limitStr, out int count))
                {
                    limits[spellLevel] = count;
                }
            }
        }

        return limits;
    }

    /// <summary>
    /// Gets all spell IDs available to a class at a given spell level.
    /// Filters spells.2da by the class-specific column matching the spell level.
    /// </summary>
    public List<int> GetSpellsForClassAtLevel(int classId, int spellLevel)
    {
        var result = new List<int>();
        var allSpellIds = GetAllSpellIds();

        foreach (var spellId in allSpellIds)
        {
            var info = GetSpellInfo(spellId);
            if (info == null) continue;

            int levelForClass = info.GetLevelForClass(classId);
            if (levelForClass == spellLevel)
                result.Add(spellId);
        }

        return result;
    }

    /// <summary>
    /// Auto-assigns spells based on package preferences, falling back to alphabetical selection.
    /// Shared by both New Character Wizard and Level Up Wizard.
    /// </summary>
    /// <param name="classId">The caster class</param>
    /// <param name="packageId">Package ID for preferences (255 = no package)</param>
    /// <param name="maxSpellLevel">Highest spell level to fill</param>
    /// <param name="maxPerLevel">Function returning max spells allowed for a given spell level</param>
    /// <param name="existingSpells">Set of spell IDs the creature already knows (to exclude)</param>
    /// <returns>Dictionary of spellLevel -> list of assigned spell IDs</returns>
    public Dictionary<int, List<int>> AutoAssignSpells(
        int classId,
        byte packageId,
        int maxSpellLevel,
        Func<int, int> maxPerLevel,
        HashSet<int>? existingSpells)
    {
        var result = new Dictionary<int, List<int>>();

        // Read package spell preferences
        var preferredSpellIds = new List<int>();
        if (packageId != 255)
        {
            var spellPref2da = _gameDataService.Get2DAValue("packages", packageId, "SpellPref2DA");
            if (!string.IsNullOrEmpty(spellPref2da) && spellPref2da != "****")
            {
                for (int row = 0; row < 100; row++)
                {
                    var spellIdStr = _gameDataService.Get2DAValue(spellPref2da, row, "SpellIndex");
                    if (string.IsNullOrEmpty(spellIdStr) || spellIdStr == "****")
                        break;
                    if (int.TryParse(spellIdStr, out int spellId))
                        preferredSpellIds.Add(spellId);
                }
            }
        }

        // Build available spells by level
        var availableByLevel = new Dictionary<int, List<(int SpellId, string SpellName)>>();
        var allSpellIds = GetAllSpellIds();

        foreach (var spellId in allSpellIds)
        {
            if (existingSpells != null && existingSpells.Contains(spellId))
                continue;

            var info = GetSpellInfo(spellId);
            if (info == null) continue;

            int levelForClass = info.GetLevelForClass(classId);
            if (levelForClass < 0 || levelForClass > maxSpellLevel) continue;

            if (!availableByLevel.ContainsKey(levelForClass))
                availableByLevel[levelForClass] = new List<(int, string)>();
            availableByLevel[levelForClass].Add((spellId, info.Name));
        }

        // Fill each level
        for (int level = 0; level <= maxSpellLevel; level++)
        {
            int maxForLevel = maxPerLevel(level);
            if (maxForLevel <= 0) continue;

            result[level] = new List<int>();
            var available = availableByLevel.GetValueOrDefault(level, new List<(int SpellId, string SpellName)>());

            // Prefer package spells first
            foreach (var prefId in preferredSpellIds)
            {
                if (result[level].Count >= maxForLevel) break;
                if (available.Any(s => s.SpellId == prefId) && !result[level].Contains(prefId))
                    result[level].Add(prefId);
            }

            // Fill remaining alphabetically
            foreach (var spell in available.OrderBy(s => s.SpellName))
            {
                if (result[level].Count >= maxForLevel) break;
                if (!result[level].Contains(spell.SpellId))
                    result[level].Add(spell.SpellId);
            }
        }

        return result;
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

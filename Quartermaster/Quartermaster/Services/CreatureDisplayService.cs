using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// Provides display name resolution for creature data using 2DA and TLK lookups.
/// Core creature lookups (race, gender, class, combat stats) plus delegation to focused services.
/// Partial classes: Combat (BAB, attack bonus, saves).
/// </summary>
public partial class CreatureDisplayService
{
    private readonly IGameDataService _gameDataService;

    // Focused services for specific domains
    public SkillService Skills { get; }
    public FeatService Feats { get; }
    public AppearanceService Appearances { get; }
    public SpellService Spells { get; }
    public ClassService Classes { get; }

    /// <summary>
    /// Direct access to game data service for advanced queries.
    /// </summary>
    public IGameDataService GameDataService => _gameDataService;

    // Cache services
    public FeatCacheService FeatCache { get; }

    public CreatureDisplayService(IGameDataService gameDataService)
    {
        ArgumentNullException.ThrowIfNull(gameDataService);
        _gameDataService = gameDataService;

        // Initialize cache services
        FeatCache = new FeatCacheService();

        // Initialize focused services
        Skills = new SkillService(gameDataService);
        Feats = new FeatService(gameDataService, Skills, FeatCache);
        Appearances = new AppearanceService(gameDataService);
        Spells = new SpellService(gameDataService);
        Classes = new ClassService(gameDataService, Skills, Feats);
    }

    /// <summary>
    /// Initialize all caches. Call early in application startup.
    /// </summary>
    public async Task InitializeCachesAsync()
    {
        await Feats.InitializeCacheAsync();
        // TODO: Add Spells.InitializeCacheAsync() when SpellCacheService is implemented
    }

    #region Race/Gender/Class Lookups

    /// <summary>
    /// Gets the display name for a race ID.
    /// </summary>
    public string GetRaceName(byte raceId)
    {
        var strRef = _gameDataService.Get2DAValue("racialtypes", raceId, "Name");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

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
    /// Gets all races from racialtypes.2da.
    /// </summary>
    public List<(byte Id, string Name)> GetAllRaces()
    {
        var races = new List<(byte Id, string Name)>();

        for (int i = 0; i < 256; i++)
        {
            var label = _gameDataService.Get2DAValue("racialtypes", i, "Label");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                if (races.Count > 10 && i > 50)
                    break;
                continue;
            }

            var name = GetRaceName((byte)i);
            races.Add(((byte)i, name));
        }

        races.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
        return races;
    }

    /// <summary>
    /// Gets only player-selectable races (PlayerRace=1 in racialtypes.2da).
    /// </summary>
    public List<(byte Id, string Name)> GetPlayerRaces()
    {
        var races = new List<(byte Id, string Name)>();

        for (int i = 0; i < 256; i++)
        {
            var label = _gameDataService.Get2DAValue("racialtypes", i, "Label");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                if (races.Count > 10 && i > 50)
                    break;
                continue;
            }

            var playerRace = _gameDataService.Get2DAValue("racialtypes", i, "PlayerRace");
            if (playerRace != "1")
                continue;

            var name = GetRaceName((byte)i);
            races.Add(((byte)i, name));
        }

        races.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
        return races;
    }

    /// <summary>
    /// Gets the favored class for a race. Returns class ID, or -1 for "Any" (e.g., Human).
    /// </summary>
    public int GetFavoredClass(byte raceId)
    {
        var favored = _gameDataService.Get2DAValue("racialtypes", raceId, "Favored");
        if (!string.IsNullOrEmpty(favored) && favored != "****" && int.TryParse(favored, out int classId))
            return classId;
        return -1; // "Any" or not specified
    }

    /// <summary>
    /// Gets the size category name for a race from racialtypes.2da.
    /// </summary>
    public string GetRaceSizeCategory(byte raceId)
    {
        var appearance = _gameDataService.Get2DAValue("racialtypes", raceId, "Appearance");
        if (!string.IsNullOrEmpty(appearance) && appearance != "****" && int.TryParse(appearance, out int appId))
        {
            var sizeStr = _gameDataService.Get2DAValue("appearance", appId, "SIZECATEGORY");
            if (!string.IsNullOrEmpty(sizeStr) && sizeStr != "****" && int.TryParse(sizeStr, out int size))
            {
                return size switch
                {
                    1 => "Tiny",
                    2 => "Small",
                    3 => "Medium",
                    4 => "Large",
                    5 => "Huge",
                    _ => $"Size {size}"
                };
            }
        }
        return "Medium"; // Default fallback
    }

    /// <summary>
    /// Gets the number of extra feats a race grants at first level (from racialtypes.2da ExtraFeatsAtFirstLevel).
    /// Human = 1 in standard NWN data, but custom content may differ.
    /// </summary>
    public int GetRacialExtraFeatsAtFirstLevel(int raceId)
    {
        var extraFeats = _gameDataService.Get2DAValue("racialtypes", raceId, "ExtraFeatsAtFirstLevel");
        if (!string.IsNullOrEmpty(extraFeats) && extraFeats != "****" && int.TryParse(extraFeats, out int bonus))
            return bonus;
        return 0;
    }

    /// <summary>
    /// Gets the number of extra skill points a race grants per level (from racialtypes.2da ExtraSkillPointsPerLvl).
    /// Human = 1 in standard NWN data, but custom content may differ.
    /// </summary>
    public int GetRacialExtraSkillPointsPerLevel(int raceId)
    {
        var extraPoints = _gameDataService.Get2DAValue("racialtypes", raceId, "ExtraSkillPointsPerLvl");
        if (!string.IsNullOrEmpty(extraPoints) && extraPoints != "****" && int.TryParse(extraPoints, out int bonus))
            return bonus;
        return 0;
    }

    /// <summary>
    /// Gets the default appearance type for a race from racialtypes.2da.
    /// </summary>
    public int GetRacialDefaultAppearance(int raceId)
    {
        var appearance = _gameDataService.Get2DAValue("racialtypes", raceId, "Appearance");
        if (!string.IsNullOrEmpty(appearance) && appearance != "****" && int.TryParse(appearance, out int appId))
            return appId;
        return 0;
    }

    /// <summary>
    /// Checks if a class is a divine caster (memorizes spells but doesn't use SpellKnownTable).
    /// Divine casters (Cleric, Druid, etc.) get access to all spells on their class list.
    /// </summary>
    public bool IsDivineCaster(int classId)
    {
        var spellCaster = _gameDataService.Get2DAValue("classes", classId, "SpellCaster");
        if (spellCaster != "1")
            return false;

        var memorizesSpells = _gameDataService.Get2DAValue("classes", classId, "MemorizesSpells");
        if (string.IsNullOrEmpty(memorizesSpells) || memorizesSpells == "****" || memorizesSpells != "1")
            return false;

        // Divine casters memorize spells but have no SpellKnownTable (they get ALL spells)
        var spellKnownTable = _gameDataService.Get2DAValue("classes", classId, "SpellKnownTable");
        return string.IsNullOrEmpty(spellKnownTable) || spellKnownTable == "****";
    }

    /// <summary>
    /// Gets the total skill count from skills.2da (dynamic, supports custom content).
    /// </summary>
    public int GetSkillCount()
    {
        var skills2da = _gameDataService.Get2DA("skills");
        return skills2da?.RowCount ?? 28;
    }

    /// <summary>
    /// Checks if a feat can be gained multiple times (GAINMULTIPLE column in feat.2da).
    /// </summary>
    public bool CanFeatBeGainedMultipleTimes(int featId)
    {
        var gainMultiple = _gameDataService.Get2DAValue("feat", featId, "GAINMULTIPLE");
        return gainMultiple == "1";
    }

    /// <summary>
    /// Gets the display name for a gender ID.
    /// </summary>
    public string GetGenderName(byte genderId)
    {
        var strRef = _gameDataService.Get2DAValue("gender", genderId, "NAME");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

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
        var strRef = _gameDataService.Get2DAValue("classes", classId, "Name");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

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
    /// Gets the hit die for a class (from HitDie column in classes.2da).
    /// </summary>
    public string GetClassHitDie(int classId)
    {
        var hitDie = _gameDataService.Get2DAValue("classes", classId, "HitDie");
        if (!string.IsNullOrEmpty(hitDie) && hitDie != "****" && int.TryParse(hitDie, out int die))
        {
            return $"d{die}";
        }

        // Fallback to generic value
        return $"d8";
    }

    /// <summary>
    /// Gets the numeric hit die value for a class (from HitDie column in classes.2da).
    /// </summary>
    public int GetClassHitDieValue(int classId)
    {
        var hitDie = _gameDataService.Get2DAValue("classes", classId, "HitDie");
        if (!string.IsNullOrEmpty(hitDie) && hitDie != "****" && int.TryParse(hitDie, out int die))
        {
            return die;
        }
        return 8; // Default
    }

    /// <summary>
    /// Calculates expected HP range for a creature based on class hit dice.
    /// Returns (minHP, avgHP, maxHP) from dice rolls alone (not including CON).
    /// </summary>
    public (int Min, int Avg, int Max) CalculateExpectedHpRange(UtcFile creature)
    {
        int minHp = 0;
        int maxHp = 0;

        foreach (var classEntry in creature.ClassList)
        {
            int hitDie = GetClassHitDieValue(classEntry.Class);
            int classLevel = classEntry.ClassLevel;

            // First level gets max hit die, subsequent levels roll
            if (classLevel >= 1)
            {
                // First level of first class gets max
                if (minHp == 0 && maxHp == 0)
                {
                    minHp = hitDie;
                    maxHp = hitDie;
                    classLevel--;
                }

                // Remaining levels: min=1 per level, max=hitDie per level
                minHp += classLevel * 1; // Minimum roll is 1
                maxHp += classLevel * hitDie;
            }
        }

        // Average is midpoint
        int avgHp = (minHp + maxHp) / 2;

        return (minHp, avgHp, maxHp);
    }

    /// <summary>
    /// Gets the base skill points per level for a class (from SkillPointBase column in classes.2da).
    /// </summary>
    public int GetClassSkillPointBase(int classId)
    {
        var skillPointBase = _gameDataService.Get2DAValue("classes", classId, "SkillPointBase");
        if (!string.IsNullOrEmpty(skillPointBase) && skillPointBase != "****" && int.TryParse(skillPointBase, out int points))
        {
            return points;
        }

        // Fallback to hardcoded values
        return classId switch
        {
            0 => 4,  // Barbarian
            1 => 6,  // Bard
            2 => 2,  // Cleric
            3 => 4,  // Druid
            4 => 2,  // Fighter
            5 => 4,  // Monk
            6 => 2,  // Paladin
            7 => 6,  // Ranger
            8 => 8,  // Rogue
            9 => 2,  // Sorcerer
            10 => 2, // Wizard
            11 => 6, // Shadowdancer
            12 => 4, // Harper Scout
            13 => 4, // Arcane Archer
            14 => 4, // Assassin
            15 => 2, // Blackguard
            16 => 2, // Champion of Torm
            17 => 2, // Weapon Master
            18 => 2, // Pale Master
            19 => 4, // Shifter
            20 => 2, // Dwarven Defender
            21 => 2, // Dragon Disciple
            _ => 2   // Default
        };
    }

    /// <summary>
    /// Gets all classes from classes.2da.
    /// </summary>
    public List<ClassInfo> GetAllClasses()
    {
        var classes = new List<ClassInfo>();

        for (int i = 0; i < 256; i++)
        {
            var label = _gameDataService.Get2DAValue("classes", i, "Label");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                // Stop after a reasonable gap if we've found enough classes
                if (classes.Count > 20 && i > 50)
                    break;
                continue;
            }

            var name = GetClassName(i);
            var playerClass = _gameDataService.Get2DAValue("classes", i, "PlayerClass");
            var isPlayerClass = playerClass == "1";
            var maxLevel = GetClassMaxLevel(i);

            classes.Add(new ClassInfo
            {
                Id = i,
                Name = name,
                IsPlayerClass = isPlayerClass,
                MaxLevel = maxLevel
            });
        }

        // Sort: player classes first, then by name
        classes.Sort((a, b) =>
        {
            if (a.IsPlayerClass != b.IsPlayerClass)
                return b.IsPlayerClass.CompareTo(a.IsPlayerClass);
            return string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
        });

        return classes;
    }

    /// <summary>
    /// Gets the maximum level for a class (from MaxLevel column in classes.2da).
    /// Returns 0 if no maximum (base classes), or the max level for prestige classes.
    /// </summary>
    public int GetClassMaxLevel(int classId)
    {
        var maxLevel = _gameDataService.Get2DAValue("classes", classId, "MaxLevel");
        if (!string.IsNullOrEmpty(maxLevel) && maxLevel != "****" && int.TryParse(maxLevel, out int max))
        {
            return max;
        }
        return 0; // No maximum
    }

    #endregion

    #region Racial Modifiers

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

    #endregion

    #region Ability Calculations

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

    #endregion

    #region Creature Display

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

    #endregion

    #region Delegation Methods (for backward compatibility)

    // These methods delegate to focused services for backward compatibility
    // New code should access services directly: displayService.Skills.GetSkillName(id)

    public string GetSkillName(int skillId) => Skills.GetSkillName(skillId);
    public string GetSkillKeyAbility(int skillId) => Skills.GetSkillKeyAbility(skillId);
    public bool IsClassSkill(int classId, int skillId) => Skills.IsClassSkill(classId, skillId);
    public HashSet<int> GetClassSkillIds(int classId) => Skills.GetClassSkillIds(classId);
    public HashSet<int> GetCombinedClassSkillIds(UtcFile creature) => Skills.GetCombinedClassSkillIds(creature);
    public bool IsSkillUniversal(int skillId) => Skills.IsSkillUniversal(skillId);
    public bool IsSkillAvailable(UtcFile creature, int skillId) => Skills.IsSkillAvailable(creature, skillId);
    public HashSet<int> GetUnavailableSkillIds(UtcFile creature, int totalSkillCount) => Skills.GetUnavailableSkillIds(creature, totalSkillCount);

    public string GetFeatName(int featId) => Feats.GetFeatName(featId);
    public FeatCategory GetFeatCategory(int featId) => Feats.GetFeatCategory(featId);
    public string GetFeatDescription(int featId) => Feats.GetFeatDescription(featId);
    public bool IsFeatUniversal(int featId) => Feats.IsFeatUniversal(featId);
    public List<int> GetAllFeatIds() => Feats.GetAllFeatIds();
    public FeatInfo GetFeatInfo(int featId) => Feats.GetFeatInfo(featId);
    public HashSet<int> GetClassGrantedFeatIds(int classId) => Feats.GetClassGrantedFeatIds(classId);
    public HashSet<int> GetCombinedGrantedFeatIds(UtcFile creature) => Feats.GetCombinedGrantedFeatIds(creature);
    public HashSet<int> GetRaceGrantedFeatIds(byte raceId) => Feats.GetRaceGrantedFeatIds(raceId);
    public int GetFeatGrantingClass(UtcFile creature, int featId) => Feats.GetFeatGrantingClass(creature, featId);
    public bool IsFeatGrantedByRace(UtcFile creature, int featId) => Feats.IsFeatGrantedByRace(creature, featId);
    public bool IsFeatAvailable(UtcFile creature, int featId) => Feats.IsFeatAvailable(creature, featId);
    public HashSet<int> GetUnavailableFeatIds(UtcFile creature, IEnumerable<int> allFeatIds) => Feats.GetUnavailableFeatIds(creature, allFeatIds);
    public FeatPrerequisites GetFeatPrerequisites(int featId) => Feats.GetFeatPrerequisites(featId);

    public FeatPrereqResult CheckFeatPrerequisites(UtcFile creature, int featId, HashSet<ushort> creatureFeats) =>
        Feats.CheckFeatPrerequisites(creature, featId, creatureFeats, CalculateBaseAttackBonus, GetClassName);

    public string GetAppearanceName(ushort appearanceId) => Appearances.GetAppearanceName(appearanceId);
    public bool IsPartBasedAppearance(ushort appearanceId) => Appearances.IsPartBasedAppearance(appearanceId);
    public int GetSizeAcModifier(ushort appearanceId) => Appearances.GetSizeAcModifier(appearanceId);
    public List<AppearanceInfo> GetAllAppearances() => Appearances.GetAllAppearances();
    public string GetPhenotypeName(int phenotype) => Appearances.GetPhenotypeName(phenotype);
    public List<PhenotypeInfo> GetAllPhenotypes() => Appearances.GetAllPhenotypes();
    public string GetPortraitName(ushort portraitId) => Appearances.GetPortraitName(portraitId);
    public string? GetPortraitResRef(ushort portraitId) => Appearances.GetPortraitResRef(portraitId);
    public List<(ushort Id, string Name)> GetAllPortraits() => Appearances.GetAllPortraits();
    public ushort? FindPortraitIdByResRef(string? resRef) => Appearances.FindPortraitIdByResRef(resRef);
    public string GetWingName(byte wingId) => Appearances.GetWingName(wingId);
    public string GetTailName(byte tailId) => Appearances.GetTailName(tailId);
    public List<(byte Id, string Name)> GetAllWings() => Appearances.GetAllWings();
    public List<(byte Id, string Name)> GetAllTails() => Appearances.GetAllTails();
    public string GetSoundSetName(ushort soundSetId) => Appearances.GetSoundSetName(soundSetId);
    public List<(ushort Id, string Name)> GetAllSoundSets() => Appearances.GetAllSoundSets();
    public List<(ushort Id, string Name)> GetAllFactions(string? moduleDirectory = null) => Appearances.GetAllFactions(moduleDirectory);
    public string GetPackageName(byte packageId) => Appearances.GetPackageName(packageId);
    public List<(byte Id, string Name)> GetAllPackages() => Appearances.GetAllPackages();
    public List<(byte Id, string Name)> GetPackagesForClass(int classId) => Appearances.GetPackagesForClass(classId);

    public string GetSpellName(int spellId) => Spells.GetSpellName(spellId);
    public List<int> GetAllSpellIds() => Spells.GetAllSpellIds();
    public SpellInfo? GetSpellInfo(int spellId) => Spells.GetSpellInfo(spellId);
    public string GetSpellSchoolName(SpellSchool school) => SpellService.GetSpellSchoolName(school);
    public int GetMaxSpellLevel(int classId, int classLevel) => Spells.GetMaxSpellLevel(classId, classLevel);
    public bool IsCasterClass(int classId) => Spells.IsCasterClass(classId);
    public bool IsSpontaneousCaster(int classId) => Spells.IsSpontaneousCaster(classId);
    public int[]? GetSpellSlots(int classId, int classLevel) => Spells.GetSpellSlots(classId, classLevel);
    public int[]? GetSpellsKnownLimit(int classId, int classLevel) => Spells.GetSpellsKnownLimit(classId, classLevel);

    #endregion

    #region Palette Categories

    /// <summary>
    /// Gets all palette categories for creature blueprints.
    /// </summary>
    public IEnumerable<PaletteCategory> GetCreaturePaletteCategories()
    {
        return _gameDataService.GetPaletteCategories(Radoub.Formats.Common.ResourceTypes.Utc);
    }

    /// <summary>
    /// Gets the name of a palette category by ID.
    /// </summary>
    public string? GetPaletteCategoryName(byte categoryId)
    {
        return _gameDataService.GetPaletteCategoryName(Radoub.Formats.Common.ResourceTypes.Utc, categoryId);
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

    /// <summary>
    /// Attacks per round from BAB (1-4).
    /// </summary>
    public int AttacksPerRound { get; set; }

    /// <summary>
    /// Attack sequence string (e.g., "+16/+11/+6/+1").
    /// </summary>
    public string AttackSequence { get; set; } = "";
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
/// Holds class information from classes.2da.
/// </summary>
public class ClassInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsPlayerClass { get; set; }
    /// <summary>
    /// Maximum level allowed in this class (0 = no maximum, applies to base classes).
    /// Prestige classes typically have MaxLevel of 10.
    /// </summary>
    public int MaxLevel { get; set; }
}

/// <summary>
/// Holds saving throw values.
/// </summary>
public class SavingThrows
{
    public int Fortitude { get; set; }
    public int Reflex { get; set; }
    public int Will { get; set; }
}

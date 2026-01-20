using System.Collections.Generic;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// Provides display name resolution for creature data using 2DA and TLK lookups.
/// Core creature lookups (race, gender, class, combat stats) plus delegation to focused services.
/// </summary>
public class CreatureDisplayService
{
    private readonly IGameDataService _gameDataService;

    // Focused services for specific domains
    public SkillService Skills { get; }
    public FeatService Feats { get; }
    public AppearanceService Appearances { get; }
    public SpellService Spells { get; }
    public ClassService Classes { get; }

    // Cache services
    public FeatCacheService FeatCache { get; }

    public CreatureDisplayService(IGameDataService gameDataService)
    {
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

        return stats;
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

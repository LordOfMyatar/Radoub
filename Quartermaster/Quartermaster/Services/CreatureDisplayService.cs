using System.Collections.Generic;
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

    public CreatureDisplayService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;

        // Initialize focused services
        Skills = new SkillService(gameDataService);
        Feats = new FeatService(gameDataService, Skills);
        Appearances = new AppearanceService(gameDataService);
        Spells = new SpellService(gameDataService);
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

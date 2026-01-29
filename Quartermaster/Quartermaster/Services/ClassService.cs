using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// Provides class data lookups, prestige prerequisite checking, and level-up validation.
/// Uses 2DA and TLK for game data resolution - never hardcodes game content.
/// </summary>
public class ClassService
{
    private readonly IGameDataService _gameDataService;
    private readonly SkillService _skillService;
    private readonly FeatService _featService;

    // Cache for class metadata (loaded once)
    private Dictionary<int, ClassMetadata>? _classCache;

    public ClassService(IGameDataService gameDataService, SkillService skillService, FeatService featService)
    {
        ArgumentNullException.ThrowIfNull(gameDataService);
        ArgumentNullException.ThrowIfNull(skillService);
        ArgumentNullException.ThrowIfNull(featService);

        _gameDataService = gameDataService;
        _skillService = skillService;
        _featService = featService;
    }

    #region Class Metadata

    /// <summary>
    /// Gets complete metadata for a class, including prestige status and description.
    /// </summary>
    public ClassMetadata GetClassMetadata(int classId)
    {
        EnsureCacheLoaded();
        if (_classCache!.TryGetValue(classId, out var metadata))
            return metadata;

        // Build metadata on-demand if not in cache
        return BuildClassMetadata(classId);
    }

    /// <summary>
    /// Gets all classes from classes.2da with full metadata.
    /// </summary>
    public List<ClassMetadata> GetAllClassMetadata()
    {
        EnsureCacheLoaded();
        return _classCache!.Values.OrderBy(c => !c.IsPlayerClass).ThenBy(c => c.Name).ToList();
    }

    /// <summary>
    /// Checks if a class is a prestige class (has prerequisite table).
    /// </summary>
    public bool IsPrestigeClass(int classId)
    {
        var preReqTable = _gameDataService.Get2DAValue("classes", classId, "PreReqTable");
        return !string.IsNullOrEmpty(preReqTable) && preReqTable != "****";
    }

    /// <summary>
    /// Gets the class description from TLK.
    /// </summary>
    public string GetClassDescription(int classId)
    {
        var descStrRef = _gameDataService.Get2DAValue("classes", classId, "Description");
        if (!string.IsNullOrEmpty(descStrRef) && descStrRef != "****")
        {
            var desc = _gameDataService.GetString(descStrRef);
            if (!string.IsNullOrEmpty(desc))
                return desc;
        }
        return "";
    }

    /// <summary>
    /// Gets the class name from TLK.
    /// </summary>
    public string GetClassName(int classId)
    {
        var nameStrRef = _gameDataService.Get2DAValue("classes", classId, "Name");
        if (!string.IsNullOrEmpty(nameStrRef) && nameStrRef != "****")
        {
            var name = _gameDataService.GetString(nameStrRef);
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        // Fallback for common classes
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
            _ => $"Class {classId}"
        };
    }

    private void EnsureCacheLoaded()
    {
        if (_classCache != null)
            return;

        _classCache = new Dictionary<int, ClassMetadata>();

        // Scan classes.2da for all valid classes
        for (int i = 0; i < 256; i++)
        {
            var label = _gameDataService.Get2DAValue("classes", i, "Label");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                if (_classCache.Count > 20 && i > 50)
                    break;
                continue;
            }

            _classCache[i] = BuildClassMetadata(i);
        }
    }

    private ClassMetadata BuildClassMetadata(int classId)
    {
        var metadata = new ClassMetadata { ClassId = classId };

        // Basic info
        metadata.Name = GetClassName(classId);
        metadata.Description = GetClassDescription(classId);

        // Player class check
        var playerClass = _gameDataService.Get2DAValue("classes", classId, "PlayerClass");
        metadata.IsPlayerClass = playerClass == "1";

        // Hit die
        var hitDie = _gameDataService.Get2DAValue("classes", classId, "HitDie");
        if (!string.IsNullOrEmpty(hitDie) && hitDie != "****" && int.TryParse(hitDie, out int hd))
            metadata.HitDie = hd;
        else
            metadata.HitDie = 8;

        // Skill points
        var skillPoints = _gameDataService.Get2DAValue("classes", classId, "SkillPointBase");
        if (!string.IsNullOrEmpty(skillPoints) && skillPoints != "****" && int.TryParse(skillPoints, out int sp))
            metadata.SkillPointsPerLevel = sp;
        else
            metadata.SkillPointsPerLevel = 2;

        // Max level (prestige classes typically have 10)
        var maxLevel = _gameDataService.Get2DAValue("classes", classId, "MaxLevel");
        if (!string.IsNullOrEmpty(maxLevel) && maxLevel != "****" && int.TryParse(maxLevel, out int ml))
            metadata.MaxLevel = ml;
        else
            metadata.MaxLevel = 0; // No limit

        // Prestige class identification
        var preReqTable = _gameDataService.Get2DAValue("classes", classId, "PreReqTable");
        metadata.IsPrestige = !string.IsNullOrEmpty(preReqTable) && preReqTable != "****";
        metadata.PreReqTableName = metadata.IsPrestige ? preReqTable : null;

        // Primary ability (for casters)
        var primaryAbility = _gameDataService.Get2DAValue("classes", classId, "PrimaryAbil");
        metadata.PrimaryAbility = primaryAbility ?? "";

        // Spellcaster info
        var spellGainTable = _gameDataService.Get2DAValue("classes", classId, "SpellGainTable");
        metadata.IsCaster = !string.IsNullOrEmpty(spellGainTable) && spellGainTable != "****";

        var spellKnownTable = _gameDataService.Get2DAValue("classes", classId, "SpellKnownTable");
        metadata.IsSpontaneousCaster = !string.IsNullOrEmpty(spellKnownTable) && spellKnownTable != "****";

        // Alignment restrictions
        LoadAlignmentRestrictions(classId, metadata);

        return metadata;
    }

    private void LoadAlignmentRestrictions(int classId, ClassMetadata metadata)
    {
        var alignRestrict = _gameDataService.Get2DAValue("classes", classId, "AlignRestrict");
        var alignRestrictType = _gameDataService.Get2DAValue("classes", classId, "AlignRstrctType");
        var invertRestrict = _gameDataService.Get2DAValue("classes", classId, "InvertRestrict");

        if (string.IsNullOrEmpty(alignRestrict) || alignRestrict == "****")
            return;

        // Parse bitmask
        // 0x01 = neutral, 0x02 = lawful, 0x04 = chaotic, 0x08 = good, 0x10 = evil
        if (!int.TryParse(alignRestrict, System.Globalization.NumberStyles.HexNumber, null, out int restrictMask))
        {
            if (!int.TryParse(alignRestrict, out restrictMask))
                return;
        }

        int.TryParse(alignRestrictType, out int restrictType);
        bool invert = invertRestrict == "1";

        metadata.AlignmentRestriction = new AlignmentRestriction
        {
            RestrictionMask = restrictMask,
            RestrictionType = restrictType,
            Inverted = invert
        };
    }

    #endregion

    #region Prestige Prerequisites

    /// <summary>
    /// Gets all prerequisites for a prestige class.
    /// Returns empty list for base classes.
    /// </summary>
    public List<ClassPrerequisite> GetPrestigePrerequisites(int classId)
    {
        var metadata = GetClassMetadata(classId);
        if (!metadata.IsPrestige || string.IsNullOrEmpty(metadata.PreReqTableName))
            return new List<ClassPrerequisite>();

        return ParsePrereqTable(metadata.PreReqTableName);
    }

    /// <summary>
    /// Checks if a creature meets all prerequisites for a class.
    /// </summary>
    public ClassPrereqResult CheckPrerequisites(int classId, UtcFile creature)
    {
        var result = new ClassPrereqResult { ClassId = classId };
        var metadata = GetClassMetadata(classId);

        // Base classes have no prerequisites
        if (!metadata.IsPrestige)
        {
            result.AllMet = true;
            result.HasPrerequisites = false;
            return result;
        }

        result.HasPrerequisites = true;
        var prereqs = GetPrestigePrerequisites(classId);

        // Group FEATOR requirements
        var featOrGroups = new Dictionary<string, List<(int FeatId, string FeatName, bool Met)>>();

        foreach (var prereq in prereqs)
        {
            var checkResult = CheckSinglePrerequisite(prereq, creature);

            switch (prereq.Type)
            {
                case PrereqType.Feat:
                    result.RequiredFeats.Add((prereq.Param1, checkResult.Description, checkResult.Met));
                    if (!checkResult.Met) result.AllMet = false;
                    break;

                case PrereqType.FeatOr:
                    // Group OR feats by their label prefix (for display)
                    string groupKey = prereq.Label ?? "or_group";
                    if (!featOrGroups.ContainsKey(groupKey))
                        featOrGroups[groupKey] = new List<(int, string, bool)>();
                    featOrGroups[groupKey].Add((prereq.Param1, checkResult.Description, checkResult.Met));
                    break;

                case PrereqType.Skill:
                    result.SkillRequirements.Add((checkResult.Description, checkResult.Met));
                    if (!checkResult.Met) result.AllMet = false;
                    break;

                case PrereqType.Bab:
                    result.OtherRequirements.Add((checkResult.Description, checkResult.Met));
                    if (!checkResult.Met) result.AllMet = false;
                    break;

                case PrereqType.Race:
                    result.OtherRequirements.Add((checkResult.Description, checkResult.Met));
                    if (!checkResult.Met) result.AllMet = false;
                    break;

                case PrereqType.ArcaneSpell:
                    result.OtherRequirements.Add((checkResult.Description, checkResult.Met));
                    if (!checkResult.Met) result.AllMet = false;
                    break;

                case PrereqType.DivineSpell:
                    result.OtherRequirements.Add((checkResult.Description, checkResult.Met));
                    if (!checkResult.Met) result.AllMet = false;
                    break;

                case PrereqType.ClassOr:
                    result.OtherRequirements.Add((checkResult.Description, checkResult.Met));
                    if (!checkResult.Met) result.AllMet = false;
                    break;

                case PrereqType.Var:
                    // Module variables - can't validate without module context
                    result.OtherRequirements.Add((checkResult.Description, null)); // Unknown
                    break;

                default:
                    result.OtherRequirements.Add((checkResult.Description, checkResult.Met));
                    if (!checkResult.Met) result.AllMet = false;
                    break;
            }
        }

        // Process FEATOR groups - need at least one from each group
        foreach (var group in featOrGroups)
        {
            bool anyMet = group.Value.Any(f => f.Met);
            result.OrRequiredFeats.AddRange(group.Value);
            if (!anyMet)
                result.AllMet = false;
        }

        // Check alignment restrictions
        if (metadata.AlignmentRestriction != null)
        {
            var alignResult = CheckAlignmentRestriction(creature, metadata.AlignmentRestriction);
            if (!string.IsNullOrEmpty(alignResult.Description))
            {
                result.OtherRequirements.Add((alignResult.Description, alignResult.Met));
                if (!alignResult.Met)
                    result.AllMet = false;
            }
        }

        return result;
    }

    private List<ClassPrerequisite> ParsePrereqTable(string tableName)
    {
        var prereqs = new List<ClassPrerequisite>();

        for (int row = 0; row < 50; row++)
        {
            var reqType = _gameDataService.Get2DAValue(tableName, row, "ReqType");
            if (string.IsNullOrEmpty(reqType) || reqType == "****")
                break;

            var prereq = new ClassPrerequisite
            {
                Label = _gameDataService.Get2DAValue(tableName, row, "LABEL") ?? ""
            };

            var param1Str = _gameDataService.Get2DAValue(tableName, row, "ReqParam1") ?? "";
            var param2Str = _gameDataService.Get2DAValue(tableName, row, "ReqParam2") ?? "";

            if (int.TryParse(param1Str, out int p1))
                prereq.Param1 = p1;
            if (int.TryParse(param2Str, out int p2))
                prereq.Param2 = p2;

            prereq.Type = reqType.ToUpperInvariant() switch
            {
                "FEAT" => PrereqType.Feat,
                "FEATOR" => PrereqType.FeatOr,
                "SKILL" => PrereqType.Skill,
                "BAB" => PrereqType.Bab,
                "RACE" => PrereqType.Race,
                "VAR" => PrereqType.Var,
                "ARCSPELL" => PrereqType.ArcaneSpell,
                "DIVSPELL" => PrereqType.DivineSpell,
                "CLASSOR" => PrereqType.ClassOr,
                "CLASSNOT" => PrereqType.ClassNot,
                "SAVE" => PrereqType.Save,
                _ => PrereqType.Unknown
            };

            // For VAR type, Param1 is actually a variable name string
            if (prereq.Type == PrereqType.Var)
            {
                prereq.VarName = param1Str;
            }

            prereqs.Add(prereq);
        }

        return prereqs;
    }

    private (string Description, bool Met) CheckSinglePrerequisite(ClassPrerequisite prereq, UtcFile creature)
    {
        switch (prereq.Type)
        {
            case PrereqType.Feat:
            {
                var featName = _featService.GetFeatName(prereq.Param1);
                bool hasFeat = creature.FeatList.Contains((ushort)prereq.Param1);
                return ($"Feat: {featName}", hasFeat);
            }

            case PrereqType.FeatOr:
            {
                var featName = _featService.GetFeatName(prereq.Param1);
                bool hasFeat = creature.FeatList.Contains((ushort)prereq.Param1);
                return (featName, hasFeat);
            }

            case PrereqType.Skill:
            {
                var skillName = _skillService.GetSkillName(prereq.Param1);
                int currentRanks = prereq.Param1 < creature.SkillList.Count ? creature.SkillList[prereq.Param1] : 0;
                bool meetsRanks = currentRanks >= prereq.Param2;
                return ($"{skillName} {prereq.Param2}+ ranks (have {currentRanks})", meetsRanks);
            }

            case PrereqType.Bab:
            {
                int currentBab = CalculateCreatureBab(creature);
                bool meetsBab = currentBab >= prereq.Param1;
                return ($"BAB +{prereq.Param1}+ (have +{currentBab})", meetsBab);
            }

            case PrereqType.Race:
            {
                var raceName = GetRaceName(prereq.Param1);
                bool isRace = creature.Race == prereq.Param1;
                return ($"Race: {raceName}", isRace);
            }

            case PrereqType.ArcaneSpell:
            {
                bool canCastArcane = CanCastArcaneSpells(creature, prereq.Param1);
                string levelText = prereq.Param1 > 0 ? $" level {prereq.Param1}+" : "";
                return ($"Can cast arcane spells{levelText}", canCastArcane);
            }

            case PrereqType.DivineSpell:
            {
                bool canCastDivine = CanCastDivineSpells(creature, prereq.Param1);
                string levelText = prereq.Param1 > 0 ? $" level {prereq.Param1}+" : "";
                return ($"Can cast divine spells{levelText}", canCastDivine);
            }

            case PrereqType.ClassOr:
            {
                var className = GetClassName(prereq.Param1);
                bool hasClass = creature.ClassList.Any(c => c.Class == prereq.Param1);
                return ($"Class: {className}", hasClass);
            }

            case PrereqType.ClassNot:
            {
                var className = GetClassName(prereq.Param1);
                bool lacksClass = !creature.ClassList.Any(c => c.Class == prereq.Param1);
                return ($"Not class: {className}", lacksClass);
            }

            case PrereqType.Var:
            {
                // Module variables can't be validated without module context
                return ($"Module variable: {prereq.VarName}", false);
            }

            default:
                return ($"Unknown requirement: {prereq.Label}", false);
        }
    }

    private (string Description, bool Met) CheckAlignmentRestriction(UtcFile creature, AlignmentRestriction restriction)
    {
        // Decode alignment from creature
        int lawChaos = creature.LawfulChaotic; // 0-100: 0=Lawful, 50=Neutral, 100=Chaotic
        int goodEvil = creature.GoodEvil;       // 0-100: 0=Good, 50=Neutral, 100=Evil

        // Thresholds: <30 = one extreme, 30-70 = neutral, >70 = other extreme
        bool isLawful = lawChaos < 30;
        bool isChaotic = lawChaos > 70;
        bool isNeutralLC = !isLawful && !isChaotic;

        bool isGood = goodEvil < 30;
        bool isEvil = goodEvil > 70;
        bool isNeutralGE = !isGood && !isEvil;

        // Build current alignment bitmask
        int currentMask = 0;
        if (isNeutralLC && isNeutralGE) currentMask |= 0x01; // True neutral
        if (isLawful) currentMask |= 0x02;
        if (isChaotic) currentMask |= 0x04;
        if (isGood) currentMask |= 0x08;
        if (isEvil) currentMask |= 0x10;

        // Check against restriction
        bool matches = (currentMask & restriction.RestrictionMask) != 0;
        bool allowed = restriction.Inverted ? !matches : matches;

        // Build description
        var alignDesc = new List<string>();
        if ((restriction.RestrictionMask & 0x02) != 0) alignDesc.Add("Lawful");
        if ((restriction.RestrictionMask & 0x04) != 0) alignDesc.Add("Chaotic");
        if ((restriction.RestrictionMask & 0x08) != 0) alignDesc.Add("Good");
        if ((restriction.RestrictionMask & 0x10) != 0) alignDesc.Add("Evil");
        if ((restriction.RestrictionMask & 0x01) != 0) alignDesc.Add("Neutral");

        string verb = restriction.Inverted ? "Cannot be" : "Must be";
        string description = $"{verb}: {string.Join(" or ", alignDesc)}";

        return (description, allowed);
    }

    private string GetRaceName(int raceId)
    {
        var strRef = _gameDataService.Get2DAValue("racialtypes", raceId, "Name");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var name = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(name))
                return name;
        }
        return $"Race {raceId}";
    }

    private int CalculateCreatureBab(UtcFile creature)
    {
        int totalBab = 0;
        foreach (var creatureClass in creature.ClassList)
        {
            totalBab += GetClassBab(creatureClass.Class, creatureClass.ClassLevel);
        }
        return totalBab;
    }

    private int GetClassBab(int classId, int classLevel)
    {
        if (classLevel <= 0) return 0;

        var attackTable = _gameDataService.Get2DAValue("classes", classId, "AttackBonusTable");
        if (string.IsNullOrEmpty(attackTable) || attackTable == "****")
            return EstimateBab(classId, classLevel);

        var babValue = _gameDataService.Get2DAValue(attackTable, classLevel - 1, "BAB");
        if (!string.IsNullOrEmpty(babValue) && babValue != "****" && int.TryParse(babValue, out int bab))
            return bab;

        return EstimateBab(classId, classLevel);
    }

    private static int EstimateBab(int classId, int classLevel)
    {
        // BAB progression: Full (1.0), 3/4 (0.75), 1/2 (0.5)
        double progression = classId switch
        {
            0 => 1.0,   // Barbarian
            1 => 0.75,  // Bard
            2 => 0.75,  // Cleric
            3 => 0.75,  // Druid
            4 => 1.0,   // Fighter
            5 => 0.75,  // Monk
            6 => 1.0,   // Paladin
            7 => 1.0,   // Ranger
            8 => 0.75,  // Rogue
            9 => 0.5,   // Sorcerer
            10 => 0.5,  // Wizard
            _ => 0.75
        };
        return (int)(classLevel * progression);
    }

    private bool CanCastArcaneSpells(UtcFile creature, int minLevel)
    {
        // Arcane casters: Bard (1), Sorcerer (9), Wizard (10)
        var arcaneCasterIds = new HashSet<int> { 1, 9, 10 };

        foreach (var creatureClass in creature.ClassList)
        {
            if (arcaneCasterIds.Contains(creatureClass.Class))
            {
                if (minLevel <= 0)
                    return true;

                // Check if class level grants spells of the required level
                int maxSpellLevel = GetMaxCasterSpellLevel(creatureClass.Class, creatureClass.ClassLevel);
                if (maxSpellLevel >= minLevel)
                    return true;
            }
        }
        return false;
    }

    private bool CanCastDivineSpells(UtcFile creature, int minLevel)
    {
        // Divine casters: Cleric (2), Druid (3), Paladin (6), Ranger (7)
        var divineCasterIds = new HashSet<int> { 2, 3, 6, 7 };

        foreach (var creatureClass in creature.ClassList)
        {
            if (divineCasterIds.Contains(creatureClass.Class))
            {
                if (minLevel <= 0)
                    return true;

                int maxSpellLevel = GetMaxCasterSpellLevel(creatureClass.Class, creatureClass.ClassLevel);
                if (maxSpellLevel >= minLevel)
                    return true;
            }
        }
        return false;
    }

    private int GetMaxCasterSpellLevel(int classId, int classLevel)
    {
        var spellGainTable = _gameDataService.Get2DAValue("classes", classId, "SpellGainTable");
        if (string.IsNullOrEmpty(spellGainTable) || spellGainTable == "****")
            return 0;

        // Check each spell level (9 down to 1) to find highest available
        for (int spellLevel = 9; spellLevel >= 1; spellLevel--)
        {
            var slots = _gameDataService.Get2DAValue(spellGainTable, classLevel - 1, $"SpellLevel{spellLevel}");
            if (!string.IsNullOrEmpty(slots) && slots != "****" && int.TryParse(slots, out int slotCount) && slotCount > 0)
                return spellLevel;
        }

        return 0;
    }

    #endregion

    #region Level-Up Validation

    /// <summary>
    /// Gets available classes for a creature to level up into.
    /// Filters by prerequisites and marks unqualified prestige classes.
    /// </summary>
    public List<AvailableClass> GetAvailableClasses(UtcFile creature, bool includeUnqualified = true)
    {
        var allClasses = GetAllClassMetadata();
        var result = new List<AvailableClass>();
        int totalLevel = creature.ClassList.Sum(c => c.ClassLevel);

        foreach (var metadata in allClasses)
        {
            // Skip non-player classes
            if (!metadata.IsPlayerClass)
                continue;

            var available = new AvailableClass
            {
                ClassId = metadata.ClassId,
                Name = metadata.Name,
                Description = metadata.Description,
                IsPrestige = metadata.IsPrestige,
                HitDie = metadata.HitDie,
                SkillPoints = metadata.SkillPointsPerLevel,
                MaxLevel = metadata.MaxLevel
            };

            // Check if creature already has this class
            var existingClass = creature.ClassList.FirstOrDefault(c => c.Class == metadata.ClassId);
            if (existingClass != null)
            {
                available.CurrentLevel = existingClass.ClassLevel;
                // Check max level for prestige classes
                if (metadata.MaxLevel > 0 && existingClass.ClassLevel >= metadata.MaxLevel)
                {
                    available.Qualification = ClassQualification.MaxLevelReached;
                }
                else
                {
                    available.Qualification = ClassQualification.Qualified;
                }
            }
            else if (metadata.IsPrestige)
            {
                // Check prestige prerequisites
                var prereqResult = CheckPrerequisites(metadata.ClassId, creature);
                available.PrerequisiteResult = prereqResult;

                if (prereqResult.AllMet)
                {
                    available.Qualification = ClassQualification.Qualified;
                }
                else
                {
                    available.Qualification = ClassQualification.PrerequisitesNotMet;
                    if (!includeUnqualified)
                        continue;
                }
            }
            else
            {
                // Base class - always qualified (except at level 1, max 3 base classes in classic NWN)
                available.Qualification = ClassQualification.Qualified;
            }

            // Check multiclass limit (8 classes max in EE)
            if (existingClass == null && creature.ClassList.Count >= 8)
            {
                available.Qualification = ClassQualification.MaxClassesReached;
            }

            result.Add(available);
        }

        // Sort: Qualified first, then by name
        return result
            .OrderBy(c => c.Qualification != ClassQualification.Qualified)
            .ThenBy(c => c.IsPrestige)
            .ThenBy(c => c.Name)
            .ToList();
    }

    #endregion
}

#region Data Classes

/// <summary>
/// Complete metadata for a class from classes.2da.
/// </summary>
public class ClassMetadata
{
    public int ClassId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsPlayerClass { get; set; }
    public bool IsPrestige { get; set; }
    public string? PreReqTableName { get; set; }
    public int HitDie { get; set; }
    public int SkillPointsPerLevel { get; set; }
    public int MaxLevel { get; set; } // 0 = no max
    public string PrimaryAbility { get; set; } = "";
    public bool IsCaster { get; set; }
    public bool IsSpontaneousCaster { get; set; }
    public AlignmentRestriction? AlignmentRestriction { get; set; }
}

/// <summary>
/// Alignment restriction data from classes.2da.
/// </summary>
public class AlignmentRestriction
{
    public int RestrictionMask { get; set; }
    public int RestrictionType { get; set; }
    public bool Inverted { get; set; }
}

/// <summary>
/// A single prerequisite from cls_pres_*.2da.
/// </summary>
public class ClassPrerequisite
{
    public string Label { get; set; } = "";
    public PrereqType Type { get; set; }
    public int Param1 { get; set; }
    public int Param2 { get; set; }
    public string? VarName { get; set; } // For VAR type
}

/// <summary>
/// Type of prestige class prerequisite.
/// </summary>
public enum PrereqType
{
    Unknown,
    Feat,       // Must have specific feat
    FeatOr,     // Must have one of these feats (OR group)
    Skill,      // Must have skill ranks
    Bab,        // Minimum base attack bonus
    Race,       // Must be specific race
    Var,        // Module script variable (can't validate)
    ArcaneSpell,// Can cast arcane spells of level X
    DivineSpell,// Can cast divine spells of level X
    ClassOr,    // Must have levels in class (OR)
    ClassNot,   // Must NOT have levels in class
    Save        // Minimum saving throw
}

/// <summary>
/// Result of checking prestige class prerequisites.
/// </summary>
public class ClassPrereqResult
{
    public int ClassId { get; set; }
    public bool AllMet { get; set; } = true;
    public bool HasPrerequisites { get; set; }

    /// <summary>Required feats (AND): (FeatId, Description, Met)</summary>
    public List<(int FeatId, string Description, bool Met)> RequiredFeats { get; set; } = new();

    /// <summary>Or-required feats: (FeatId, FeatName, Met)</summary>
    public List<(int FeatId, string Name, bool Met)> OrRequiredFeats { get; set; } = new();

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

        var lines = new List<string> { "Prerequisites:" };

        foreach (var (_, desc, met) in RequiredFeats)
            lines.Add($"  {(met ? "[Y]" : "[N]")} {desc}");

        if (OrRequiredFeats.Count > 0)
        {
            bool anyMet = OrRequiredFeats.Any(f => f.Met);
            lines.Add($"  {(anyMet ? "[Y]" : "[N]")} One of:");
            foreach (var (_, name, met) in OrRequiredFeats)
                lines.Add($"      {(met ? "[Y]" : "[ ]")} {name}");
        }

        foreach (var (desc, met) in SkillRequirements)
            lines.Add($"  {(met ? "[Y]" : "[N]")} {desc}");

        foreach (var (desc, met) in OtherRequirements)
            lines.Add($"  {(met.HasValue ? (met.Value ? "[Y]" : "[N]") : "[?]")} {desc}");

        return string.Join("\n", lines);
    }
}

/// <summary>
/// Qualification status for taking a class.
/// </summary>
public enum ClassQualification
{
    Qualified,
    PrerequisitesNotMet,
    MaxLevelReached,
    MaxClassesReached,
    AlignmentRestricted
}

/// <summary>
/// A class available for level-up with qualification info.
/// </summary>
public class AvailableClass
{
    public int ClassId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsPrestige { get; set; }
    public int HitDie { get; set; }
    public int SkillPoints { get; set; }
    public int MaxLevel { get; set; }
    public int CurrentLevel { get; set; }
    public ClassQualification Qualification { get; set; }
    public ClassPrereqResult? PrerequisiteResult { get; set; }

    public bool CanSelect => Qualification == ClassQualification.Qualified;

    public string QualificationText => Qualification switch
    {
        ClassQualification.Qualified => "",
        ClassQualification.PrerequisitesNotMet => "Prerequisites not met",
        ClassQualification.MaxLevelReached => $"Max level ({MaxLevel}) reached",
        ClassQualification.MaxClassesReached => "Max 8 classes",
        ClassQualification.AlignmentRestricted => "Alignment restricted",
        _ => ""
    };
}

#endregion

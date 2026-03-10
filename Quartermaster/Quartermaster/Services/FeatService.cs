using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// Provides feat name lookups, categories, prerequisites, and class feat grants.
/// Uses 2DA and TLK for game data resolution with caching for performance.
/// </summary>
public partial class FeatService
{
    private readonly IGameDataService _gameDataService;
    private readonly SkillService _skillService;
    private readonly FeatCacheService _cacheService;
    private bool _cacheInitialized;

    public FeatService(IGameDataService gameDataService, SkillService skillService, FeatCacheService cacheService)
    {
        ArgumentNullException.ThrowIfNull(gameDataService);
        ArgumentNullException.ThrowIfNull(skillService);
        ArgumentNullException.ThrowIfNull(cacheService);

        _gameDataService = gameDataService;
        _skillService = skillService;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Initialize feat cache from disk or by scanning 2DA.
    /// Call this early in application startup for best performance.
    /// </summary>
    public async Task InitializeCacheAsync()
    {
        if (_cacheInitialized)
            return;

        // Try to load from disk first
        if (_cacheService.HasValidCache() && _cacheService.LoadCacheFromDisk())
        {
            _cacheInitialized = true;
            return;
        }

        // Build cache from 2DA
        await BuildCacheAsync();
        _cacheInitialized = true;
    }

    /// <summary>
    /// Build feat cache by scanning 2DA files.
    /// </summary>
    private async Task BuildCacheAsync()
    {
        var featIds = ScanAllFeatIds();
        _cacheService.SetAllFeatIds(featIds);

        // Cache basic feat info for all feats
        foreach (var featId in featIds)
        {
            var entry = new CachedFeatEntry
            {
                FeatId = featId,
                Name = LookupFeatName(featId),
                Description = LookupFeatDescription(featId),
                Category = (int)LookupFeatCategory(featId),
                IsUniversal = LookupFeatUniversal(featId)
            };
            _cacheService.CacheFeat(entry);
        }

        // Save to disk for next time
        await _cacheService.SaveCacheToDiskAsync();
    }

    /// <summary>
    /// Get cache info for settings display.
    /// </summary>
    public Radoub.UI.Services.CacheInfo? GetCacheInfo() => _cacheService.GetCacheInfo();

    /// <summary>
    /// Clear and rebuild the cache.
    /// </summary>
    public async Task RebuildCacheAsync()
    {
        _cacheService.ClearCache();
        _cacheInitialized = false;
        await InitializeCacheAsync();
    }

    /// <summary>
    /// Gets the display name for a feat ID.
    /// </summary>
    public string GetFeatName(int featId)
    {
        // Check cache first
        if (_cacheService.TryGetFeat(featId, out var cached) && cached != null)
            return cached.Name;

        // Fallback to direct lookup
        return LookupFeatName(featId);
    }

    /// <summary>
    /// Direct 2DA/TLK lookup for feat name (bypasses cache).
    /// </summary>
    private string LookupFeatName(int featId)
    {
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
    /// Gets the toolset category for a feat (TOOLSCATEGORIES column).
    /// Returns: 1=Combat, 2=Active Combat, 3=Defensive, 4=Magical, 5=Class/Racial, 6=Other
    /// </summary>
    public FeatCategory GetFeatCategory(int featId)
    {
        // Check cache first
        if (_cacheService.TryGetFeat(featId, out var cached) && cached != null)
            return (FeatCategory)cached.Category;

        // Fallback to direct lookup
        return LookupFeatCategory(featId);
    }

    /// <summary>
    /// Direct 2DA lookup for feat category (bypasses cache).
    /// </summary>
    private FeatCategory LookupFeatCategory(int featId)
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
        // Check cache first
        if (_cacheService.TryGetFeat(featId, out var cached) && cached != null)
            return cached.Description;

        // Fallback to direct lookup
        return LookupFeatDescription(featId);
    }

    /// <summary>
    /// Direct 2DA/TLK lookup for feat description (bypasses cache).
    /// </summary>
    private string LookupFeatDescription(int featId)
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
        // Check cache first
        if (_cacheService.TryGetFeat(featId, out var cached) && cached != null)
            return cached.IsUniversal;

        // Fallback to direct lookup
        return LookupFeatUniversal(featId);
    }

    /// <summary>
    /// Direct 2DA lookup for feat universal flag (bypasses cache).
    /// </summary>
    private bool LookupFeatUniversal(int featId)
    {
        var allClassesCanUse = _gameDataService.Get2DAValue("feat", featId, "ALLCLASSESCANUSE");
        return allClassesCanUse == "1";
    }

    /// <summary>
    /// Gets all valid feat IDs from feat.2da.
    /// </summary>
    public List<int> GetAllFeatIds()
    {
        // Check cache first
        var cachedIds = _cacheService.GetAllFeatIds();
        if (cachedIds != null && cachedIds.Count > 0)
            return cachedIds;

        // Fallback to direct scan
        return ScanAllFeatIds();
    }

    /// <summary>
    /// Direct 2DA scan for all feat IDs (bypasses cache).
    /// </summary>
    private List<int> ScanAllFeatIds()
    {
        var featIds = new List<int>();

        int rowCount = _gameDataService.Get2DA("feat")?.RowCount ?? 2000;
        for (int i = 0; i < rowCount; i++)
        {
            var label = _gameDataService.Get2DAValue("feat", i, "LABEL");
            if (string.IsNullOrEmpty(label) || label == "****")
                continue;

            var featName = _gameDataService.Get2DAValue("feat", i, "FEAT");
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
    /// <summary>
    /// Gets feats automatically granted at a specific class level.
    /// Reads List=3 + GrantedOnLevel from cls_feat_*.2da, plus List=-1 for level 1.
    /// </summary>
    public HashSet<int> GetClassFeatsGrantedAtLevel(int classId, int classLevel)
    {
        var result = new HashSet<int>();

        var featTable = _gameDataService.Get2DAValue("classes", classId, "FeatsTable");
        if (string.IsNullOrEmpty(featTable) || featTable == "****")
            return result;

        int rowCount = _gameDataService.Get2DA(featTable)?.RowCount ?? 200;
        for (int row = 0; row < rowCount; row++)
        {
            var featIndexStr = _gameDataService.Get2DAValue(featTable, row, "FeatIndex");
            if (string.IsNullOrEmpty(featIndexStr) || featIndexStr == "****")
                break;

            if (!int.TryParse(featIndexStr, out int featId))
                continue;

            var listType = _gameDataService.Get2DAValue(featTable, row, "List");

            // List=-1: granted at creation (class level 1 only)
            if (listType == "-1" && classLevel == 1)
            {
                result.Add(featId);
                continue;
            }

            // List=3: automatically granted at GrantedOnLevel
            if (listType == "3")
            {
                var grantedLevelStr = _gameDataService.Get2DAValue(featTable, row, "GrantedOnLevel");
                if (int.TryParse(grantedLevelStr, out int grantedLevel) && grantedLevel == classLevel)
                    result.Add(featId);
            }
        }

        return result;
    }

    public HashSet<int> GetClassGrantedFeatIds(int classId)
    {
        // Check cache first
        if (_cacheService.TryGetClassGrantedFeats(classId, out var cached) && cached != null)
            return cached;

        // Lookup and cache
        var result = LookupClassGrantedFeatIds(classId);
        _cacheService.CacheClassGrantedFeats(classId, result);
        return result;
    }

    /// <summary>
    /// Direct 2DA lookup for class granted feats (bypasses cache).
    /// </summary>
    private HashSet<int> LookupClassGrantedFeatIds(int classId)
    {
        var result = new HashSet<int>();

        var featTable = _gameDataService.Get2DAValue("classes", classId, "FeatsTable");
        if (string.IsNullOrEmpty(featTable) || featTable == "****")
            return result;

        int rowCount = _gameDataService.Get2DA(featTable)?.RowCount ?? 200;
        for (int row = 0; row < rowCount; row++)
        {
            var featIndexStr = _gameDataService.Get2DAValue(featTable, row, "FeatIndex");
            if (string.IsNullOrEmpty(featIndexStr) || featIndexStr == "****")
                break;

            if (int.TryParse(featIndexStr, out int featId))
            {
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

        // Add class-granted feats
        foreach (var creatureClass in creature.ClassList)
        {
            var classFeats = GetClassGrantedFeatIds(creatureClass.Class);
            foreach (var featId in classFeats)
            {
                result.Add(featId);
            }
        }

        // Add racial feats
        var racialFeats = GetRaceGrantedFeatIds(creature.Race);
        foreach (var featId in racialFeats)
        {
            result.Add(featId);
        }

        return result;
    }

    /// <summary>
    /// Gets the set of feat IDs granted by a race.
    /// Reads from race feat tables referenced in racialtypes.2da.
    /// Unlike class feat tables, ALL feats in a racial feat table are automatically granted.
    /// </summary>
    public HashSet<int> GetRaceGrantedFeatIds(byte raceId)
    {
        // Check cache first
        if (_cacheService.TryGetRaceGrantedFeats(raceId, out var cached) && cached != null)
            return cached;

        // Lookup and cache
        var result = LookupRaceGrantedFeatIds(raceId);
        _cacheService.CacheRaceGrantedFeats(raceId, result);
        return result;
    }

    /// <summary>
    /// Direct 2DA lookup for race granted feats (bypasses cache).
    /// </summary>
    private HashSet<int> LookupRaceGrantedFeatIds(byte raceId)
    {
        var result = new HashSet<int>();

        var featTable = _gameDataService.Get2DAValue("racialtypes", raceId, "FeatsTable");
        if (string.IsNullOrEmpty(featTable) || featTable == "****")
            return result;

        int raceRowCount = _gameDataService.Get2DA(featTable)?.RowCount ?? 100;
        for (int row = 0; row < raceRowCount; row++)
        {
            var featIndexStr = _gameDataService.Get2DAValue(featTable, row, "FeatIndex");
            if (string.IsNullOrEmpty(featIndexStr) || featIndexStr == "****")
                break;

            if (int.TryParse(featIndexStr, out int featId))
            {
                // All feats in a racial feat table are automatically granted to that race
                result.Add(featId);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the class ID that grants a specific feat to a creature.
    /// Returns the first matching class ID, or -1 if no class grants this feat.
    /// </summary>
    public int GetFeatGrantingClass(UtcFile creature, int featId)
    {
        foreach (var creatureClass in creature.ClassList)
        {
            var classFeats = GetClassGrantedFeatIds(creatureClass.Class);
            if (classFeats.Contains(featId))
            {
                return creatureClass.Class;
            }
        }
        return -1;
    }

    /// <summary>
    /// Checks if a feat is granted by a creature's race.
    /// </summary>
    public bool IsFeatGrantedByRace(UtcFile creature, int featId)
    {
        var racialFeats = GetRaceGrantedFeatIds(creature.Race);
        return racialFeats.Contains(featId);
    }

}

/// <summary>
/// Breakdown of expected choosable feats for a creature (cumulative over all levels).
/// </summary>
public class ExpectedFeatInfo
{
    /// <summary>Base feats from character level (1 + floor(level/3))</summary>
    public int BaseFeats { get; set; }

    /// <summary>Racial bonus feats (Human = 1)</summary>
    public int RacialBonusFeats { get; set; }

    /// <summary>Class bonus feats from BonusFeatsTable</summary>
    public int ClassBonusFeats { get; set; }

    /// <summary>Total expected choosable feats</summary>
    public int TotalExpected { get; set; }
}

/// <summary>
/// Breakdown of feats to choose for a single level-up event.
/// </summary>
public class LevelUpFeatInfo
{
    /// <summary>General feat (0 or 1) - granted at levels 1, 3, 6, 9...</summary>
    public int GeneralFeats { get; set; }

    /// <summary>Racial bonus feats (only at level 1)</summary>
    public int RacialBonusFeats { get; set; }

    /// <summary>Class bonus feat (0 or 1) from BonusFeatsTable</summary>
    public int ClassBonusFeats { get; set; }

    /// <summary>Total feats to choose this level</summary>
    public int TotalFeats { get; set; }
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

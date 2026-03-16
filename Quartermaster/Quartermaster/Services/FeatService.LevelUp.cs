using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// Level-up feat calculations, auto-assignment, and bonus feat pool logic.
/// </summary>
public partial class FeatService
{
    /// <summary>
    /// Calculates the number of choosable feats for a single level-up event.
    /// Used by Level Up Wizard to determine how many feats the user picks this level.
    /// </summary>
    /// <param name="creature">The creature BEFORE leveling (current state)</param>
    /// <param name="selectedClassId">The class being leveled</param>
    /// <param name="newClassLevel">The new class level (after level-up)</param>
    public LevelUpFeatInfo GetLevelUpFeatCount(UtcFile creature, int selectedClassId, int newClassLevel)
    {
        var result = new LevelUpFeatInfo();
        int newTotalLevel = creature.ClassList.Sum(c => c.ClassLevel) + 1;

        // D&D 3.5/NWN rule: general feat at level 1, then every 3 levels (3, 6, 9, 12...)
        // This interval is an engine rule, not configurable via 2DA
        const int FeatProgressionInterval = 3;
        if (newTotalLevel == 1 || newTotalLevel % FeatProgressionInterval == 0)
            result.GeneralFeats = 1;

        // Racial bonus feats only at level 1
        if (newTotalLevel == 1)
            result.RacialBonusFeats = GetRacialBonusFeatCount(creature.Race);

        // Class bonus feat for the class being leveled
        result.ClassBonusFeats = GetClassBonusFeatAtLevel(selectedClassId, newClassLevel);

        result.TotalFeats = result.GeneralFeats + result.RacialBonusFeats + result.ClassBonusFeats;
        return result;
    }

    /// <summary>
    /// Gets whether a specific class level grants a bonus feat (0 or 1).
    /// Reads from cls_bfeat_*.2da (BonusFeatsTable column in classes.2da).
    /// </summary>
    private int GetClassBonusFeatAtLevel(int classId, int classLevel)
    {
        var bfeatTable = _gameDataService.Get2DAValue("classes", classId, "BonusFeatsTable");
        if (string.IsNullOrEmpty(bfeatTable) || bfeatTable == "****")
            return 0;

        var bonus = _gameDataService.Get2DAValue(bfeatTable, classLevel - 1, "Bonus");
        return bonus == "1" ? 1 : 0;
    }

    /// <summary>
    /// Calculates the expected number of choosable feats for a creature based on level and class.
    /// Does NOT include automatically granted feats (those are in GetCombinedGrantedFeatIds).
    /// </summary>
    public ExpectedFeatInfo GetExpectedFeatCount(UtcFile creature)
    {
        var result = new ExpectedFeatInfo();
        int totalLevel = creature.ClassList.Sum(c => c.ClassLevel);

        // D&D 3.5/NWN rule: 1 feat at level 1, then +1 at every multiple of 3
        // Levels that grant feats: 1, 3, 6, 9, 12, 15, 18, 21, 24, 27, 30, 33, 36, 39
        // This interval is an engine rule, not configurable via 2DA
        result.BaseFeats = 1 + totalLevel / 3;

        // Racial bonus feat (Human gets +1)
        result.RacialBonusFeats = GetRacialBonusFeatCount(creature.Race);

        // Class bonus feats from each class's BonusFeatsTable
        foreach (var classEntry in creature.ClassList)
        {
            int classBonusFeats = GetClassBonusFeatCount(classEntry.Class, classEntry.ClassLevel);
            result.ClassBonusFeats += classBonusFeats;
        }

        result.TotalExpected = result.BaseFeats + result.RacialBonusFeats + result.ClassBonusFeats;
        return result;
    }

    /// <summary>
    /// Gets the number of bonus feats a race grants at character creation.
    /// Human = 1 (Quick to Master racial trait), others = 0.
    /// </summary>
    private int GetRacialBonusFeatCount(byte raceId)
    {
        // Check racialtypes.2da for ExtraFeatsAtFirstLevel or similar
        var extraFeats = _gameDataService.Get2DAValue("racialtypes", raceId, "ExtraFeatsAtFirstLevel");
        if (!string.IsNullOrEmpty(extraFeats) && extraFeats != "****" && int.TryParse(extraFeats, out int bonus))
            return bonus;

        // Fallback: no extra feats if 2DA column is missing
        return 0;
    }

    /// <summary>
    /// Gets the set of feat IDs that are in the class bonus feat pool (List=1 in cls_feat_*.2da).
    /// These are the feats restricted to bonus feat slots (e.g., Fighter bonus = martial feats,
    /// Wizard bonus = metamagic/item creation).
    /// </summary>
    public HashSet<int> GetClassBonusFeatPool(int classId)
    {
        var result = new HashSet<int>();
        var featTable = _gameDataService.Get2DAValue("classes", classId, "FeatsTable");
        if (string.IsNullOrEmpty(featTable) || featTable == "****")
            return result;

        int rowCount = _gameDataService.Get2DA(featTable)?.RowCount ?? 300;
        for (int row = 0; row < rowCount; row++)
        {
            var featIndexStr = _gameDataService.Get2DAValue(featTable, row, "FeatIndex");
            if (string.IsNullOrEmpty(featIndexStr) || featIndexStr == "****")
                break;

            if (int.TryParse(featIndexStr, out int featId))
            {
                var listType = _gameDataService.Get2DAValue(featTable, row, "List");
                if (listType == "1") // Bonus-only feats
                    result.Add(featId);
            }
        }

        return result;
    }

    /// <summary>
    /// Auto-assigns feats based on package preferences, falling back to alphabetical selection.
    /// Shared by both New Character Wizard and Level Up Wizard.
    /// </summary>
    /// <param name="creature">The creature (for prereq checking)</param>
    /// <param name="classId">The class being leveled/created</param>
    /// <param name="packageId">Package ID for preferences (255 = no package)</param>
    /// <param name="currentFeats">Current feat set (including any tentatively selected)</param>
    /// <param name="maxCount">Maximum number of feats to auto-assign</param>
    /// <param name="bonusFeatPool">If non-null, restrict selections to this pool (for class bonus feats)</param>
    /// <param name="prereqChecker">Function to check if a feat meets prerequisites</param>
    /// <returns>List of auto-assigned feat IDs</returns>
    public List<int> AutoAssignFeats(
        UtcFile creature,
        int classId,
        byte packageId,
        HashSet<int> currentFeats,
        int maxCount,
        HashSet<int>? bonusFeatPool,
        Func<int, bool> prereqChecker)
    {
        var assigned = new List<int>();

        // Read package feat preferences
        var preferredFeatIds = new List<int>();
        if (packageId != 255)
        {
            var featPref2da = _gameDataService.Get2DAValue("packages", packageId, "FeatPref2DA");
            if (!string.IsNullOrEmpty(featPref2da) && featPref2da != "****")
            {
                for (int row = 0; row < 100; row++)
                {
                    var featIdStr = _gameDataService.Get2DAValue(featPref2da, row, "FeatIndex");
                    if (string.IsNullOrEmpty(featIdStr) || featIdStr == "****")
                        break;
                    if (int.TryParse(featIdStr, out int featId))
                        preferredFeatIds.Add(featId);
                }
            }
        }

        // Build a temp creature for availability check
        bool IsAvailableAndValid(int featId)
        {
            if (currentFeats.Contains(featId)) return false;
            if (assigned.Contains(featId)) return false;
            if (bonusFeatPool != null && !bonusFeatPool.Contains(featId)) return false;
            if (!IsFeatAvailable(creature, featId)) return false;
            return prereqChecker(featId);
        }

        // First pass: pick from preferred feats
        foreach (var featId in preferredFeatIds)
        {
            if (assigned.Count >= maxCount) break;
            if (IsAvailableAndValid(featId))
                assigned.Add(featId);
        }

        // Second pass: fill remaining from all available feats, preferring class feats (#1737)
        if (assigned.Count < maxCount)
        {
            var allFeatIds = GetAllFeatIds();
            var remaining = allFeatIds
                .Where(IsAvailableAndValid)
                .Select(id => (Id: id, Name: GetFeatName(id), IsClassFeat: IsFeatInClassTable(classId, id)))
                .Where(f => !string.IsNullOrEmpty(f.Name))
                .OrderByDescending(f => f.IsClassFeat) // Class feats first
                .ThenBy(f => f.Name);

            foreach (var (id, _, _) in remaining)
            {
                if (assigned.Count >= maxCount) break;
                assigned.Add(id);
            }
        }

        return assigned;
    }

    /// <summary>
    /// Gets the number of bonus feats granted by a class up to a given level.
    /// Reads from cls_bfeat_*.2da (BonusFeatsTable column in classes.2da).
    /// </summary>
    private int GetClassBonusFeatCount(int classId, int classLevel)
    {
        var bfeatTable = _gameDataService.Get2DAValue("classes", classId, "BonusFeatsTable");
        if (string.IsNullOrEmpty(bfeatTable) || bfeatTable == "****")
            return 0;

        int bonusCount = 0;
        for (int level = 1; level <= classLevel; level++)
        {
            // BonusFeatsTable rows are 0-indexed, level 1 = row 0
            var bonus = _gameDataService.Get2DAValue(bfeatTable, level - 1, "Bonus");
            if (bonus == "1")
                bonusCount++;
        }

        return bonusCount;
    }
}

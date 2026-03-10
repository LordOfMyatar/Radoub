using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// Feat availability checks and prerequisite validation.
/// </summary>
public partial class FeatService
{
    /// <summary>
    /// Checks if a feat is available to a creature (can be selected).
    /// </summary>
    public bool IsFeatAvailable(UtcFile creature, int featId)
    {
        if (IsFeatUniversal(featId))
            return true;

        foreach (var creatureClass in creature.ClassList)
        {
            if (IsFeatInClassTable(creatureClass.Class, featId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a feat appears in a class's feat table.
    /// </summary>
    private bool IsFeatInClassTable(int classId, int featId)
    {
        var featTable = _gameDataService.Get2DAValue("classes", classId, "FeatsTable");
        if (string.IsNullOrEmpty(featTable) || featTable == "****")
            return false;

        int tableRowCount = _gameDataService.Get2DA(featTable)?.RowCount ?? 300;
        for (int row = 0; row < tableRowCount; row++)
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
    /// </summary>
    /// <param name="creature">The creature to check</param>
    /// <param name="featId">The feat ID</param>
    /// <param name="creatureFeats">The creature's current feats</param>
    /// <param name="calculateBab">Function to calculate BAB (injected to avoid circular dependency)</param>
    /// <param name="getClassName">Function to get class name (injected to avoid circular dependency)</param>
    public FeatPrereqResult CheckFeatPrerequisites(
        UtcFile creature,
        int featId,
        HashSet<ushort> creatureFeats,
        System.Func<UtcFile, int> calculateBab,
        System.Func<int, string> getClassName)
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
            var bab = calculateBab(creature);
            var met = bab >= prereqs.MinBab;
            result.OtherRequirements.Add(($"BAB {prereqs.MinBab}+", met));
            if (!met) result.AllMet = false;
        }

        // Check spell level - validate against creature's caster classes
        if (prereqs.MinSpellLevel > 0)
        {
            bool canCast = false;
            foreach (var cc in creature.ClassList)
            {
                var spellGainTable = _gameDataService.Get2DAValue("classes", cc.Class, "SpellGainTable");
                if (!string.IsNullOrEmpty(spellGainTable) && spellGainTable != "****")
                {
                    // Check if this class grants spells of the required level at current level
                    int rowIndex = cc.ClassLevel - 1;
                    if (rowIndex >= 0)
                    {
                        var col = $"SpellLevel{prereqs.MinSpellLevel}";
                        var slotsStr = _gameDataService.Get2DAValue(spellGainTable, rowIndex, col);
                        if (!string.IsNullOrEmpty(slotsStr) && slotsStr != "****" && slotsStr != "-"
                            && int.TryParse(slotsStr, out int slots) && slots > 0)
                        {
                            canCast = true;
                            break;
                        }
                    }
                }
            }
            result.OtherRequirements.Add(($"Cast level {prereqs.MinSpellLevel} spells", canCast));
            if (!canCast) result.AllMet = false;
        }

        // Check skills
        foreach (var (skillId, minRanks) in prereqs.RequiredSkills)
        {
            var skillName = _skillService.GetSkillName(skillId);
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
                var className = getClassName(prereqs.MinLevelClass.Value);
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
}

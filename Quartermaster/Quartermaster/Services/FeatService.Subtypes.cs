using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// MASTERFEAT subtype grouping for feat list consolidation (#1734).
///
/// In feat.2da, variants like "Weapon Focus (Club)", "Weapon Focus (Dagger)" each have
/// their own row but share a MASTERFEAT value pointing to a parent "Weapon Focus" row.
/// This grouping collapses the variants so the UI can show a single entry per master feat
/// and present subtypes via a sub-picker dialog.
/// </summary>
public partial class FeatService
{
    /// <summary>
    /// A group of feats that share the same MASTERFEAT parent, or a singleton
    /// representing a feat with no master.
    /// </summary>
    public sealed class FeatGroup
    {
        /// <summary>
        /// For singletons: the feat's own ID. For master-feat groups: the MASTERFEAT parent's ID.
        /// </summary>
        public int FeatId { get; init; }

        /// <summary>True when this group represents multiple child subtypes.</summary>
        public bool IsMasterFeat { get; init; }

        /// <summary>Child feat IDs that belong to this master. Empty for singletons.</summary>
        public IReadOnlyList<int> SubtypeIds { get; init; } = System.Array.Empty<int>();
    }

    /// <summary>
    /// Reads the MASTERFEAT column for a feat. Returns null if unset, "****", or unparseable.
    /// The returned value is an index into <c>masterfeats.2da</c> (NOT <c>feat.2da</c>).
    /// </summary>
    public int? GetMasterFeatId(int featId)
    {
        var raw = _gameDataService.Get2DAValue("feat", featId, "MASTERFEAT");
        if (string.IsNullOrEmpty(raw) || raw == "****")
            return null;
        return int.TryParse(raw, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// Resolves the display name of a master feat by looking up <c>masterfeats.2da</c>
    /// STRREF in the TLK. Falls back to the LABEL column when the string lookup fails.
    /// The <paramref name="masterFeatId"/> is the value stored in feat.2da's MASTERFEAT column.
    /// </summary>
    public string GetMasterFeatName(int masterFeatId)
    {
        var strRef = _gameDataService.Get2DAValue("masterfeats", masterFeatId, "STRREF");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        var label = _gameDataService.Get2DAValue("masterfeats", masterFeatId, "LABEL");
        return !string.IsNullOrEmpty(label) && label != "****" ? label : $"Master feat #{masterFeatId}";
    }

    /// <summary>
    /// Finds all feats whose MASTERFEAT column points at <paramref name="masterFeatId"/>.
    /// </summary>
    public List<int> GetSubtypeFeatIds(int masterFeatId)
    {
        var result = new List<int>();
        int rowCount = _gameDataService.Get2DA("feat")?.RowCount ?? 0;
        for (int i = 0; i < rowCount; i++)
        {
            if (GetMasterFeatId(i) == masterFeatId)
                result.Add(i);
        }
        return result;
    }

    /// <summary>
    /// Groups a list of feats by their MASTERFEAT. Feats without a master become singletons;
    /// feats sharing a master are collapsed into a single <see cref="FeatGroup"/>.
    /// Preserves first-occurrence order from the input.
    /// </summary>
    public List<FeatGroup> GroupFeatsByMaster(IEnumerable<int> featIds)
    {
        var groups = new List<FeatGroup>();
        var masterIndex = new Dictionary<int, int>(); // masterId -> index in `groups`

        foreach (var featId in featIds)
        {
            var masterId = GetMasterFeatId(featId);
            if (masterId is null)
            {
                groups.Add(new FeatGroup { FeatId = featId, IsMasterFeat = false });
                continue;
            }

            if (masterIndex.TryGetValue(masterId.Value, out var existingIdx))
            {
                var existing = groups[existingIdx];
                var merged = new List<int>(existing.SubtypeIds) { featId };
                groups[existingIdx] = new FeatGroup
                {
                    FeatId = existing.FeatId,
                    IsMasterFeat = true,
                    SubtypeIds = merged
                };
            }
            else
            {
                masterIndex[masterId.Value] = groups.Count;
                groups.Add(new FeatGroup
                {
                    FeatId = masterId.Value,
                    IsMasterFeat = true,
                    SubtypeIds = new List<int> { featId }
                });
            }
        }

        return groups;
    }

    /// <summary>
    /// True if the creature has at least one spellcasting class (SpellGainTable set in classes.2da).
    /// Used to filter Spell Focus and other caster-only MASTERFEAT subtypes (#2096).
    /// </summary>
    public bool HasCasterClass(UtcFile creature)
    {
        foreach (var cc in creature.ClassList)
        {
            var spellGainTable = _gameDataService.Get2DAValue("classes", cc.Class, "SpellGainTable");
            if (!string.IsNullOrEmpty(spellGainTable) && spellGainTable != "****")
                return true;
        }
        return false;
    }

    /// <summary>
    /// Structural applicability of a MASTERFEAT subtype to a creature, independent of
    /// validation level (#2096). Filters subtypes the character is fundamentally barred from
    /// regardless of strictness:
    ///   - REQSKILL set: the governed skill must be available to the creature — i.e. it appears
    ///     in at least one of the creature's class skill tables (as a class OR cross-class skill),
    ///     or is usable by all classes. Skill Focus can be taken for cross-class skills, so the
    ///     gate is availability, not class-skill status. Only skills no class can train at all
    ///     (e.g. Use Magic Device for a Fighter) are barred. Multiclass widens availability.
    ///   - MINSPELLLVL &gt; 0: the creature must have a spellcasting class (Spell Focus subtypes).
    /// Returns true when no structural gate applies.
    /// </summary>
    public bool IsSubtypeStructurallyApplicable(UtcFile creature, int subtypeFeatId)
    {
        // Skill-governed subtype (e.g. Skill Focus): require the skill to be usable by the
        // creature (class or cross-class). IsSkillAvailable honors all of the creature's
        // classes, so a multiclass build sees every subtype any of its classes can train.
        var reqSkillStr = _gameDataService.Get2DAValue("feat", subtypeFeatId, "REQSKILL");
        if (!string.IsNullOrEmpty(reqSkillStr) && reqSkillStr != "****" &&
            int.TryParse(reqSkillStr, out int reqSkillId))
        {
            if (!_skillService.IsSkillAvailable(creature, reqSkillId))
                return false;
        }

        // Caster-gated subtype (e.g. Spell Focus): require a spellcasting class.
        var minSpellStr = _gameDataService.Get2DAValue("feat", subtypeFeatId, "MINSPELLLVL");
        if (!string.IsNullOrEmpty(minSpellStr) && minSpellStr != "****" &&
            int.TryParse(minSpellStr, out int minSpellLvl) && minSpellLvl > 0)
        {
            if (!HasCasterClass(creature))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Decides whether a MASTERFEAT subtype should be offered to the user (#2096).
    /// Structural gates (class skill / caster class) always apply. In Strict validation
    /// the subtype's own prerequisites must also be met.
    /// </summary>
    public bool IsSubtypeApplicable(
        UtcFile creature,
        int subtypeFeatId,
        ValidationLevel validationLevel,
        HashSet<ushort> creatureFeats,
        Func<UtcFile, int> calculateBab,
        Func<int, string> getClassName,
        FeatPrereqOverrides? overrides = null)
    {
        if (!IsSubtypeStructurallyApplicable(creature, subtypeFeatId))
            return false;

        if (validationLevel == ValidationLevel.Strict)
        {
            var prereq = CheckFeatPrerequisites(
                creature, subtypeFeatId, creatureFeats, calculateBab, getClassName, overrides);
            if (!prereq.AllMet)
                return false;
        }

        return true;
    }
}

using System.Collections.Generic;
using System.Linq;

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
    /// </summary>
    public int? GetMasterFeatId(int featId)
    {
        var raw = _gameDataService.Get2DAValue("feat", featId, "MASTERFEAT");
        if (string.IsNullOrEmpty(raw) || raw == "****")
            return null;
        return int.TryParse(raw, out var parsed) ? parsed : null;
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
}

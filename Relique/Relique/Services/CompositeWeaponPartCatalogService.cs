using Radoub.Formats.Common;
using Radoub.Formats.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ItemEditor.Services;

/// <summary>
/// Lists composite-weapon model parts (ModelType 2) by scanning MDL resources whose
/// ResRef matches <c>&lt;itemClass&gt;_&lt;b|m|t&gt;_NNN</c>. Per the BioWare wiki §4.1.3,
/// the 3-digit suffix is a shape×color variant so part numbers don't start at 001 — the
/// dropdown lets the user pick from what actually exists instead of guessing (#2164).
/// </summary>
public sealed class CompositeWeaponPartCatalogService
{
    private readonly IGameDataService _gameData;

    public CompositeWeaponPartCatalogService(IGameDataService gameData)
    {
        _gameData = gameData;
    }

    /// <summary>Position suffix for ModelPart1/2/3 → b (base) / m (middle) / t (top).</summary>
    public static string? PositionForPartIndex(int partIndex) => partIndex switch
    {
        1 => "b",
        2 => "m",
        3 => "t",
        _ => null,
    };

    /// <summary>
    /// Try parse a composite-weapon ResRef matching <c>&lt;itemClass&gt;_&lt;position&gt;_NNN</c>.
    /// Case-insensitive. Returns false if the prefix doesn't match or the suffix isn't numeric.
    /// </summary>
    public static bool TryParseCompositeResRef(string resRef, string itemClass, string position, out int partNumber)
    {
        partNumber = 0;
        if (string.IsNullOrEmpty(resRef) || string.IsNullOrEmpty(itemClass) || string.IsNullOrEmpty(position))
            return false;

        var expectedPrefix = $"{itemClass}_{position}_";
        if (!resRef.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var suffix = resRef.Substring(expectedPrefix.Length);
        if (suffix.Length == 0) return false;

        return int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out partNumber);
    }

    /// <summary>
    /// Pure helper: filter and dedupe a list of MDL ResRefs to part numbers for a
    /// specific (itemClass, position) pair. Sorted ascending. Used by GetAvailableParts
    /// and exposed for unit tests so we don't need a live GameDataService.
    /// </summary>
    public static IEnumerable<int> ExtractPartNumbers(IEnumerable<string> resRefs, string? itemClass, string position)
    {
        if (string.IsNullOrEmpty(itemClass)) yield break;

        var seen = new HashSet<int>();
        var collected = new List<int>();

        foreach (var rr in resRefs)
        {
            if (TryParseCompositeResRef(rr, itemClass, position, out var n) && seen.Add(n))
                collected.Add(n);
        }

        collected.Sort();
        foreach (var n in collected) yield return n;
    }

    /// <summary>
    /// List available part numbers for a composite-weapon slot (ModelPart1/2/3 → b/m/t)
    /// by scanning all MDL resources via IGameDataService.ListResources.
    /// Returns empty if itemClass is null/empty or partIndex is out of range.
    /// </summary>
    public IEnumerable<int> GetAvailableParts(string? itemClass, int partIndex)
    {
        var position = PositionForPartIndex(partIndex);
        if (position == null) return Enumerable.Empty<int>();

        var allMdl = _gameData.ListResources(ResourceTypes.Mdl).Select(r => r.ResRef);
        return ExtractPartNumbers(allMdl, itemClass, position);
    }
}

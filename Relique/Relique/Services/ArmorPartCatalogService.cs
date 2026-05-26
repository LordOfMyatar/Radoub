using Radoub.Formats.Services;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ItemEditor.Services;

/// <summary>
/// One row of a parts_*.2da filtered to entries the engine will actually render
/// (ACBONUS != ****). DisplayIndex is the 1-based position in the filtered+sorted
/// list — Aurora's "Part NNN" label (#2164).
/// </summary>
public sealed record ArmorPartEntry(int RowIndex, int DisplayIndex, double ACBonus)
{
    /// <summary>
    /// Default label: "Part N — ID NNN". "Part N" keeps Aurora-style sequential indexing;
    /// "ID NNN" is the stored byte. AC is omitted because only the Torso slot's ACBONUS
    /// affects item AC — showing it on Neck/Hand/etc. would mislead (#2164 manual-feedback v2).
    /// Pass includeAcBonus=true on the Torso row to show "(AC ±X)" where it actually matters.
    /// </summary>
    public string ToDisplayString(bool includeAcBonus = false)
    {
        if (!includeAcBonus)
            return $"Part {DisplayIndex} — ID {RowIndex}";

        int acRounded = (int)System.Math.Round(ACBonus, System.MidpointRounding.AwayFromZero);
        string acSign = acRounded >= 0 ? "+" : "";
        return $"Part {DisplayIndex} — ID {RowIndex} (AC {acSign}{acRounded})";
    }
}

/// <summary>
/// Reads parts_*.2da tables and returns the engine-valid armor parts for a given
/// armor slot (Torso, Belt, etc.), filtered and sorted per BioWare wiki Ch4 §4.1.4:
/// "available parts are sorted in order of increasing ACBONUS as listed in the parts
/// 2da. If several parts have identical ACBONUS, then they are sorted in order of
/// increasing row number." (#2164)
/// </summary>
public sealed class ArmorPartCatalogService
{
    private readonly IGameDataService _gameData;

    private static readonly Dictionary<string, string> PartTo2DA = new(System.StringComparer.Ordinal)
    {
        ["Neck"]   = "parts_neck",
        ["Torso"]  = "parts_chest",
        ["Belt"]   = "parts_belt",
        ["Pelvis"] = "parts_pelvis",
        ["RShoul"] = "parts_shoulder", ["LShoul"] = "parts_shoulder",
        ["RBicep"] = "parts_bicep",    ["LBicep"] = "parts_bicep",
        ["RFArm"]  = "parts_forearm",  ["LFArm"]  = "parts_forearm",
        ["RHand"]  = "parts_hand",     ["LHand"]  = "parts_hand",
        ["RThigh"] = "parts_legs",     ["LThigh"] = "parts_legs",
        ["RShin"]  = "parts_shin",     ["LShin"]  = "parts_shin",
        ["RFoot"]  = "parts_foot",     ["LFoot"]  = "parts_foot",
        ["Robe"]   = "parts_robe",
    };

    public ArmorPartCatalogService(IGameDataService gameData)
    {
        _gameData = gameData;
    }

    public static string? Resolve2DAName(string partName)
        => PartTo2DA.TryGetValue(partName, out var n) ? n : null;

    public IEnumerable<ArmorPartEntry> GetAvailableParts(string partName)
    {
        var twoDAName = Resolve2DAName(partName);
        if (twoDAName == null) yield break;

        var table = _gameData.Get2DA(twoDAName);
        if (table == null) yield break;

        // Collect (rowIndex, acBonus) for rows where ACBONUS != ****.
        var rows = new List<(int row, double ac)>();
        for (int i = 0; i < table.Rows.Count; i++)
        {
            var raw = table.GetValue(i, "ACBONUS");
            // Real 2DA reader normalizes **** to null; mock test fixtures may store literal "****".
            // Both forms mean "no value" — filter both.
            if (string.IsNullOrEmpty(raw) || raw == "****") continue;

            double ac = 0;
            double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out ac);
            rows.Add((i, ac));
        }

        // Sort: ACBONUS asc, then row index asc. Per wiki Ch4 §4.1.4.
        rows.Sort((a, b) =>
        {
            int cmp = a.ac.CompareTo(b.ac);
            return cmp != 0 ? cmp : a.row.CompareTo(b.row);
        });

        int displayIndex = 1;
        foreach (var (row, ac) in rows)
        {
            yield return new ArmorPartEntry(row, displayIndex++, ac);
        }
    }
}

using System;
using System.Collections.Generic;
using Radoub.Formats.Services;

namespace Radoub.UI.Services;

/// <summary>
/// Decides which creature/mannequin body parts a robe hides, read from <c>parts_robe.2da</c>
/// (#2582). Shared by every tool that composes a body + an equipped robe on
/// <see cref="MdlPartComposer"/> — Quartermaster's creature preview and Relique's armor mannequin
/// both need it (without it, a robe item double-renders the arms the robe already supplies).
///
/// The Aurora engine does NOT hide a fixed set of parts when a robe is worn — it reads the robe
/// row's <c>HIDE*</c> columns from <c>parts_robe.2da</c> and hides each body part individually
/// (rollnw <c>render/viewer/preview_scene.cpp</c> <c>robe_hides_body_part</c>; nwnexplorer honors
/// the same per-part flags). A part is hidden iff <c>parts_robe.2da[robePart][HIDE{part}] != 0</c>.
///
/// This replaces the earlier hardcoded covered-part set + the <c>RobeArmGeometry</c> geometry
/// heuristic. The 2DA is authoritative and per-robe-per-part, so it naturally handles:
///  - cloak-only robes (e.g. <c>robe116</c> hides only the shoulders — the body must render);
///  - short-sleeve robes (e.g. <c>robe5</c>/Dana hides torso+legs but NOT the arms, #2398);
///  - full-body robes (e.g. <c>robe186</c> hides everything incl. arms).
///
/// A robe whose row sets no <c>HIDE*</c> flag suppresses nothing — that is the engine's behavior
/// (the robe mesh is authored to sit over the body), so we trust the table rather than re-adding a
/// blanket fallback.
/// </summary>
public static class RobePartSuppression
{
    /// <summary>
    /// Maps a body-part token to its <c>parts_robe.2da</c> hide column. Every limb/torso part has
    /// a column (head/neck/feet included — a robe CAN hide them if the row says so); only the robe
    /// itself has no entry. Mirrors rollnw's <c>robe_hide_column</c>.
    /// </summary>
    private static readonly Dictionary<string, string> HideColumnByPart = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chest"] = "HIDECHEST",
        ["pelvis"] = "HIDEPELVIS",
        ["belt"] = "HIDEBELT",
        ["neck"] = "HIDENECK",
        ["head"] = "HIDEHEAD",
        ["shol"] = "HIDESHOL",
        ["shor"] = "HIDESHOR",
        ["bicepl"] = "HIDEBICEPL",
        ["bicepr"] = "HIDEBICEPR",
        ["forel"] = "HIDEFOREL",
        ["forer"] = "HIDEFORER",
        ["handl"] = "HIDEHANDL",
        ["handr"] = "HIDEHANDR",
        ["legl"] = "HIDELEGL",
        ["legr"] = "HIDELEGR",
        ["shinl"] = "HIDESHINL",
        ["shinr"] = "HIDESHINR",
        ["footl"] = "HIDEFOOTL",
        ["footr"] = "HIDEFOOTR",
    };

    /// <summary>
    /// Whether <paramref name="partType"/> is hidden by robe number <paramref name="robePart"/>,
    /// per <c>parts_robe.2da</c>. Returns false when no robe is active (<paramref name="robePart"/>
    /// = 0), when the part has no hide column, or when the 2DA value is missing/0/"****".
    /// </summary>
    public static bool IsSuppressedByRobe(string partType, int robePart, IGameDataService gameData)
    {
        if (robePart <= 0 || string.IsNullOrEmpty(partType) || gameData == null)
            return false;

        if (!HideColumnByPart.TryGetValue(partType, out var column))
            return false;

        var raw = gameData.Get2DAValue("parts_robe", robePart, column);
        return IsHideFlagSet(raw);
    }

    private static bool IsHideFlagSet(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw == "****")
            return false;
        // Hidden if the flag parses to a non-zero integer (the engine treats any non-zero as hide).
        return int.TryParse(raw, out var v) && v != 0;
    }
}

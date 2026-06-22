using System;
using System.Collections.Generic;

namespace Quartermaster.Services;

/// <summary>
/// Decides which body parts a robe model replaces (#1989). An NWN robe model is a near-total
/// body: inspection of CEP Robe #186 (`pmh0_robe186`, supermodel `pmh0`) shows it supplies its
/// own geometry for torso, pelvis, biceps, thighs, forearms, shins, and hands — everything
/// except head, neck, and feet. Loading the individual body parts alongside it duplicates that
/// geometry at slightly-off transforms (the floating forearms/hands seen in the bug) and leaves
/// gaps where the robe expects to be the only mesh.
///
/// So when a robe is equipped, suppress every covered part and keep only head, neck, and feet
/// (the robe has no foot geometry). Belt is kept (a separately-cinched belt is common). Rare
/// short-sleeve / half-robe variants would over-suppress under this fixed list; if one is found
/// to regress it needs a data-driven signal (tracked separately) rather than a hardcoded set.
/// </summary>
public static class RobePartSuppression
{
    // Parts the robe always replaces when active (torso + legs): the reference engines treat a
    // robe as a near-total body, so loading these alongside it duplicates geometry.
    private static readonly HashSet<string> RobeCoveredParts = new(StringComparer.OrdinalIgnoreCase)
    {
        "chest", "pelvis", "legl", "legr", "shol", "shor", "bicepl", "bicepr",
        "forel", "forer", "handl", "handr", "shinl", "shinr",
    };

    // Arm-region parts (#2541 Phase 2): suppressed only when the robe supplies RENDERABLE arm
    // geometry. Some robes (pfh0_robe005 / Dana) have Render=false arm trimeshes and an armless
    // skin — suppressing these leaves the creature with no arms at all (#2398/#2116).
    private static readonly HashSet<string> RobeArmParts = new(StringComparer.OrdinalIgnoreCase)
    {
        "shol", "shor", "bicepl", "bicepr", "forel", "forer", "handl", "handr",
    };

    /// <summary>
    /// Whether <paramref name="partType"/> is replaced by an active robe. When the part is an
    /// arm-region part and <paramref name="robeHasRenderableArms"/> is false, it is NOT suppressed
    /// (the creature keeps its own arms). Torso/leg parts are always suppressed when a robe is
    /// active, regardless of the arm signal.
    /// </summary>
    public static bool IsSuppressedByRobe(string partType, bool robeActive, bool robeHasRenderableArms = true)
    {
        if (!robeActive || string.IsNullOrEmpty(partType) || !RobeCoveredParts.Contains(partType))
            return false;

        if (!robeHasRenderableArms && RobeArmParts.Contains(partType))
            return false;

        return true;
    }
}

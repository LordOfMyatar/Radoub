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
    private static readonly HashSet<string> RobeCoveredParts = new(StringComparer.OrdinalIgnoreCase)
    {
        "chest", "pelvis", "legl", "legr", "shol", "shor", "bicepl", "bicepr",
        "forel", "forer", "handl", "handr", "shinl", "shinr",
    };

    public static bool IsSuppressedByRobe(string partType, bool robeActive) =>
        robeActive && !string.IsNullOrEmpty(partType) && RobeCoveredParts.Contains(partType);
}

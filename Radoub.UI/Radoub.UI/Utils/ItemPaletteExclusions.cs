using System.Collections.Generic;

namespace Radoub.UI.Utils;

/// <summary>
/// Base item types that should never appear in an item palette: creature natural weapons and the
/// invalid/special marker. These are internal engine items, not authorable blueprints. Extracted
/// from Fence's per-tool list so item-consuming tools (Fence, Quartermaster, Reliquary) stay in
/// sync (#2411). Indices are rows in <c>baseitems.2da</c>.
/// </summary>
public static class ItemPaletteExclusions
{
    public static IReadOnlySet<int> ExcludedBaseItemTypes { get; } = new HashSet<int>
    {
        69,  // Creature Bite
        70,  // Creature Claw
        71,  // Creature Gore
        72,  // Creature Slashing
        73,  // Creature Piercing/Bludgeoning
        255, // Invalid/special marker
    };

    /// <summary>True when the given base item type should be hidden from the palette.</summary>
    public static bool IsExcluded(int baseItemType) => ExcludedBaseItemTypes.Contains(baseItemType);
}

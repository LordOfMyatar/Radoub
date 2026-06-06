using System.Collections.Generic;

namespace PlaceableEditor.ViewModels;

/// <summary>
/// One named placeable animation state for the Initial State combo (#2376). The byte value is
/// written straight to <see cref="Radoub.Formats.Utp.UtpFile.AnimationState"/>.
/// </summary>
public sealed record PlaceableAnimationState(byte Value, string Name)
{
    /// <summary>Combo label, e.g. "Open (1)".</summary>
    public string Display => $"{Name} ({Value})";

    /// <summary>
    /// The engine-fixed set of placeable animation states, in value order. Source: BioWare
    /// Door/Placeable GFF spec, Table 4.1.2. These are engine-internal enum values (not 2DA or
    /// module data), so the catalog is authoritative rather than hardcoded game data — a model's
    /// MDL must actually contain the matching animation for a state to render, but the stored byte
    /// is always one of these six.
    /// </summary>
    public static IReadOnlyList<PlaceableAnimationState> All { get; } = new[]
    {
        new PlaceableAnimationState(0, "Default"),
        new PlaceableAnimationState(1, "Open"),
        new PlaceableAnimationState(2, "Closed"),
        new PlaceableAnimationState(3, "Destroyed"),
        new PlaceableAnimationState(4, "Activated"),
        new PlaceableAnimationState(5, "Deactivated"),
    };
}

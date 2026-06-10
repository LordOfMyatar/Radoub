using System;
using System.Collections.Generic;
using System.Linq;
using PlaceableEditor.ViewModels;
using Radoub.Formats.Mdl;

namespace PlaceableEditor.Services;

/// <summary>
/// Resolves which placeable animation states a loaded model actually provides, and the MDL
/// animation backing each (#2431). Source: BioWare Door/Placeable GFF spec Table 4.1.2 — a state
/// is only available if the model contains an animation of the required name:
///
///   Open (1) → "open", Closed (2) → "close", Destroyed (3) → "dead",
///   Activated (4) → "on", Deactivated (5) → "off".
///
/// Default (0) is always available and renders the base mesh with no animation. The byte values
/// match <see cref="PlaceableAnimationState"/> / the stored UTP AnimationState.
/// </summary>
public static class PlaceableStateResolver
{
    // State byte → required MDL animation name (Default has none).
    private static readonly IReadOnlyDictionary<byte, string> StateAnimationNames = new Dictionary<byte, string>
    {
        [1] = "open",
        [2] = "close",
        [3] = "dead",
        [4] = "on",
        [5] = "off",
    };

    /// <summary>
    /// The MDL animation name required for a state, or null for Default (0) and any unknown value.
    /// </summary>
    public static string? AnimationNameForState(byte state)
        => StateAnimationNames.TryGetValue(state, out var name) ? name : null;

    // States never offered in the preview selector even when the model declares the animation.
    // Destroyed (3): stock MDLs ship a trivial single-frame `dead` stub with no debris geometry, so
    // the preview is indistinguishable from Default. Real destruction is engine/script-driven at
    // runtime, not baked into the blueprint MDL — showing it only misleads (user request).
    private static readonly HashSet<byte> SelectorExcludedStates = new() { 3 };

    /// <summary>
    /// The states selectable for this model: Default (always) plus every non-default, non-excluded
    /// state whose required animation is present in the model. Returned in value order.
    /// </summary>
    public static IReadOnlyList<PlaceableAnimationState> AvailableStates(MdlModel? model)
    {
        var result = new List<PlaceableAnimationState>();
        foreach (var state in PlaceableAnimationState.All)
        {
            if (state.Value == 0) // Default: base mesh, always available
            {
                result.Add(state);
                continue;
            }
            if (SelectorExcludedStates.Contains(state.Value)) continue;
            if (model != null && FindAnimation(model, state.Value) != null)
                result.Add(state);
        }
        return result;
    }

    /// <summary>
    /// The <see cref="MdlAnimation"/> backing a state in this model, or null if the state is Default
    /// or the model has no matching animation. Name match is case-insensitive (MDL animation names
    /// are not case-consistent across content).
    /// </summary>
    public static MdlAnimation? FindAnimation(MdlModel? model, byte state)
    {
        if (model == null) return null;
        var name = AnimationNameForState(state);
        if (name == null) return null;
        return model.Animations.FirstOrDefault(
            a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}

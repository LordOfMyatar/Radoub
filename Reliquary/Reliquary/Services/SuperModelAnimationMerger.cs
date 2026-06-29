using System;
using System.Collections.Generic;
using Radoub.Formats.Mdl;

namespace PlaceableEditor.Services;

/// <summary>
/// Merges supermodel-inherited animations into a placeable model (#2595). Some placeables declare
/// their open/close/on/off state animations only in a supermodel (e.g. tnp_list02 → tnp_list01),
/// so without this the placeable state selector finds no animations and stays hidden. Walks the
/// <see cref="MdlModel.SuperModel"/> chain, appending each parent animation whose name the leaf
/// doesn't already define. Mirrors Quartermaster's CreatureModelResolver merge for creatures.
/// </summary>
public static class SuperModelAnimationMerger
{
    /// <summary>
    /// Append supermodel animations to <paramref name="model"/> by name. <paramref name="loadModel"/>
    /// resolves a supermodel name to a parsed model (or null if unavailable). Safe against cycles and
    /// missing parents — both terminate the walk.
    /// </summary>
    public static void Merge(MdlModel? model, Func<string, MdlModel?> loadModel)
    {
        if (model == null) return;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MergeChain(model, model, loadModel, visited);
    }

    private static void MergeChain(MdlModel leaf, MdlModel current,
        Func<string, MdlModel?> loadModel, HashSet<string> visited)
    {
        var parentName = current.SuperModel;
        if (string.IsNullOrWhiteSpace(parentName) ||
            parentName.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (!visited.Add(parentName))
            return; // cycle guard

        var parent = loadModel(parentName);
        if (parent == null) return;

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in leaf.Animations)
            existing.Add(a.Name);

        foreach (var anim in parent.Animations)
        {
            if (existing.Add(anim.Name))
                leaf.Animations.Add(anim);
        }

        // Continue up the chain (the parent may itself inherit further).
        MergeChain(leaf, parent, loadModel, visited);
    }
}

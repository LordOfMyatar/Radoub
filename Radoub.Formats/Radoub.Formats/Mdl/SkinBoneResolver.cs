namespace Radoub.Formats.Mdl;

/// <summary>
/// Resolves a skin mesh's bone-SLOT indices to bone NODE NAMES.
///
/// A skin mesh weights each vertex to up to four bone <em>slots</em> (indices into the
/// Q/T inverse-bind arrays). The only bridge from slot to the animated bone is
/// <c>NodeToBoneMap</c>: for node <c>i</c> in the model's depth-first node order,
/// <c>NodeToBoneMap[i]</c> is that node's bone slot (or -1 if the node is not a bone).
/// Inverting that map yields slot → node name, which the renderer looks up in the animation
/// pose to drive skin deformation (#2399 / R1).
/// </summary>
public static class SkinBoneResolver
{
    /// <summary>
    /// Build a slot → bone-node-name table. <paramref name="depthFirstNodes"/> must be the
    /// model's nodes in the same depth-first order the binary reader assigned indices in
    /// <paramref name="nodeToBoneMap"/> (root at index 0). Slots with no referencing node get
    /// <see cref="string.Empty"/>.
    /// </summary>
    public static string[] ResolveBoneNames(
        IReadOnlyList<MdlNode> depthFirstNodes, short[] nodeToBoneMap, int boneCount)
    {
        var names = new string[boneCount];
        for (int i = 0; i < names.Length; i++)
            names[i] = string.Empty;

        for (int nodeIndex = 0; nodeIndex < nodeToBoneMap.Length && nodeIndex < depthFirstNodes.Count; nodeIndex++)
        {
            int slot = nodeToBoneMap[nodeIndex];
            if (slot >= 0 && slot < boneCount)
                names[slot] = depthFirstNodes[nodeIndex].Name;
        }

        return names;
    }
}

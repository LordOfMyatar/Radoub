// Builds per-bone-slot skin matrices for an MDL skin mesh from the composed bone hierarchy
// and the active animation pose (#2399 / R1). Bridges parser data to SkinDeformer's pure math.

using System.Collections.Generic;
using System.Numerics;
using Radoub.Formats.Mdl;

namespace Radoub.UI.Services;

/// <summary>
/// Resolves the per-slot skin matrices for a skin mesh. In System.Numerics row-vector order
/// (a vertex transforms as v · skin[slot]):
///   skin[slot] = meshBindWorld · inverse(boneBindWorld) · boneAnimWorld
/// (built as <c>BuildSkinMatrix(boneAnimWorld, BuildInverseBind(boneBindWorld, meshBindWorld))</c>).
/// Bones are resolved per slot — preferring the direct <see cref="MdlSkinNode.BoneNodes"/> reference
/// set at composite time, falling back to name (<see cref="MdlSkinNode.BoneNodeNames"/>) in the
/// composed hierarchy. Slots whose bone can't be resolved fall back to the mesh bind-world
/// transform, leaving those vertices at their static bind-pose position rather than collapsing.
/// </summary>
public static class SkinMatrixBuilder
{
    /// <summary>
    /// Build the slot → skin matrix array for <paramref name="skin"/>. <paramref name="root"/> is
    /// the composed model root used to resolve bone nodes by name. <paramref name="pose"/> is the
    /// active animation pose (null/empty → bind pose, skin matrices collapse to meshBindWorld).
    /// </summary>
    public static Matrix4x4[] Build(
        MdlSkinNode skin,
        MdlNode root,
        IReadOnlyDictionary<string, ModelViewController.NodePose>? pose)
    {
        int boneCount = skin.BoneNodeNames.Length;
        var matrices = new Matrix4x4[boneCount];

        // The skin mesh's own bind-pose world transform — the static position every vertex sits at
        // when un-deformed. Computed without pose so it is the true bind reference.
        var meshBindWorld = ModelViewController.GetWorldTransform(skin, null);

        // Cache bone-node lookups by name (a skin may weight many slots to the same bone).
        var boneByName = new Dictionary<string, MdlNode?>(System.StringComparer.OrdinalIgnoreCase);

        for (int slot = 0; slot < boneCount; slot++)
        {
            // Prefer a direct bone reference (set at composite time) — a composite can hold two
            // bones with the same name, so a name lookup is ambiguous and the wrong bind explodes
            // the skin under animation (#2399). Fall back to name lookup for non-composited skins.
            MdlNode? bone = slot < skin.BoneNodes.Length ? skin.BoneNodes[slot] : null;

            if (bone == null)
            {
                var boneName = skin.BoneNodeNames[slot];
                if (string.IsNullOrEmpty(boneName))
                {
                    matrices[slot] = meshBindWorld;
                    continue;
                }

                if (!boneByName.TryGetValue(boneName, out bone))
                {
                    bone = FindNode(root, boneName);
                    boneByName[boneName] = bone;
                }
            }

            if (bone == null)
            {
                matrices[slot] = meshBindWorld;
                continue;
            }

            var boneBindWorld = ModelViewController.GetWorldTransform(bone, null);
            var boneAnimWorld = ModelViewController.GetWorldTransform(bone, pose);
            var inverseBind = SkinDeformer.BuildInverseBind(boneBindWorld, meshBindWorld);
            matrices[slot] = SkinDeformer.BuildSkinMatrix(boneAnimWorld, inverseBind);
        }

        return matrices;
    }

    private static MdlNode? FindNode(MdlNode node, string name)
    {
        if (string.Equals(node.Name, name, System.StringComparison.OrdinalIgnoreCase))
            return node;
        foreach (var child in node.Children)
        {
            var found = FindNode(child, name);
            if (found != null) return found;
        }
        return null;
    }
}

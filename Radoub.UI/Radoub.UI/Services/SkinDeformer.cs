// Pure linear-blend-skinning math for MDL skin meshes (#2399 / R1).
// GL-free and dependency-light so the per-vertex blend and the skin-matrix build are unit-testable.

using System.Numerics;

namespace Radoub.UI.Services;

/// <summary>
/// Linear-blend-skinning helper. Deforms skin-mesh vertices by blending per-bone transforms,
/// so a skin body follows its animated skeleton instead of staying frozen at bind pose.
///
/// Skinning identity:
///   inverseBind[slot] = inverse(boneBindWorld) · meshBindWorld
///   skin[slot]        = boneAnimWorld · inverseBind[slot]
///   v_world           = Σ wᵢ · (skin[slotᵢ] · v_local)
/// </summary>
public static class SkinDeformer
{
    /// <summary>Up-to-4 bone-slot indices + weights for one vertex.</summary>
    public readonly record struct VertexWeights(
        int bone0, float weight0,
        int bone1, float weight1,
        int bone2, float weight2,
        int bone3, float weight3);

    /// <summary>
    /// inverseBind[slot] = inverse(boneBindWorld) · meshBindWorld.
    /// Recomputed from the bind-pose hierarchy (the stored QBoneRefInv/TBoneRefInv are not
    /// trusted). If <paramref name="boneBindWorld"/> is non-invertible, falls back to identity
    /// so the slot contributes the raw mesh-bind transform rather than NaNs.
    /// </summary>
    public static Matrix4x4 BuildInverseBind(Matrix4x4 boneBindWorld, Matrix4x4 meshBindWorld)
    {
        if (!Matrix4x4.Invert(boneBindWorld, out var boneBindInv))
            boneBindInv = Matrix4x4.Identity;
        return Matrix4x4.Multiply(boneBindInv, meshBindWorld);
    }

    /// <summary>
    /// skin[slot] = boneAnimWorld · inverseBind[slot]. At bind pose (boneAnimWorld ==
    /// boneBindWorld) this collapses to meshBindWorld, leaving the vertex at its static position.
    /// </summary>
    public static Matrix4x4 BuildSkinMatrix(Matrix4x4 boneAnimWorld, Matrix4x4 inverseBind)
        => Matrix4x4.Multiply(boneAnimWorld, inverseBind);

    /// <summary>
    /// Blend a local-space vertex by its bone weights against the per-slot skin matrices.
    /// Bone indices &lt; 0, out of range, or with non-positive weight are skipped.
    /// </summary>
    public static Vector3 BlendVertex(Vector3 vertex, VertexWeights w, Matrix4x4[] skinMatrices)
    {
        var result = Vector3.Zero;

        void Accumulate(int bone, float weight)
        {
            if (weight <= 0f || bone < 0 || bone >= skinMatrices.Length) return;
            result += weight * Vector3.Transform(vertex, skinMatrices[bone]);
        }

        Accumulate(w.bone0, w.weight0);
        Accumulate(w.bone1, w.weight1);
        Accumulate(w.bone2, w.weight2);
        Accumulate(w.bone3, w.weight3);

        return result;
    }

    /// <summary>
    /// Blend a local-space normal by its bone weights against the per-slot skin matrices, using
    /// only the rotation/scale component (translation is ignored — normals are directions). The
    /// result is NOT normalized; callers normalize after.
    /// </summary>
    public static Vector3 BlendNormal(Vector3 normal, VertexWeights w, Matrix4x4[] skinMatrices)
    {
        var result = Vector3.Zero;

        void Accumulate(int bone, float weight)
        {
            if (weight <= 0f || bone < 0 || bone >= skinMatrices.Length) return;
            result += weight * Vector3.TransformNormal(normal, skinMatrices[bone]);
        }

        Accumulate(w.bone0, w.weight0);
        Accumulate(w.bone1, w.weight1);
        Accumulate(w.bone2, w.weight2);
        Accumulate(w.bone3, w.weight3);

        return result;
    }
}

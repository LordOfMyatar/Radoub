// Decides where a mesh's render normals come from: the MDL's stored per-vertex normals, or a
// smoothgroup-aware recompute (SmoothGroupNormals). Extracted from ModelPreviewGLControl so the
// decision is unit-testable without the GL pipeline.
//
// History (#2026, #1584):
//   #2026 added a recompute path for part-based HEADS, whose stored per-vertex normals are
//   absent/unreliable (2002-era BioWare mirrored-half compiler output). The original gate keyed on
//   smoothgroup COUNT (`distinctSmoothgroups <= 1`) as a proxy — but that misfired on hands, which
//   legitimately carry MULTIPLE smoothgroups AND a full, valid set of stored normals. They were
//   wrongly recomputed and rendered with faceted/streaky shading (#1584 investigation).
//
//   The real signal is simply whether the mesh HAS a usable stored normal set. Evidence from the
//   game models: heads (pmh0_head*, pfh0_head*) have ZERO stored vertex normals, so the recompute
//   is forced anyway; hands (pmh0_handl001) and bodies carry one normal per vertex. Keying on
//   presence (not smoothgroup count) keeps the head behaviour identical while letting hands and
//   multi-smoothgroup bodies use their authored normals.

using Radoub.Formats.Mdl;

namespace Radoub.UI.Controls;

public static class MeshNormalSource
{
    /// <summary>
    /// True when the mesh's stored per-vertex normals should be used as-is; false when normals must
    /// be recomputed from smoothgroups (the mesh has no usable stored normal set).
    /// </summary>
    /// <param name="vertexCount">Mesh vertex count.</param>
    /// <param name="storedNormalCount">Length of the mesh's stored normal array.</param>
    public static bool UseStoredNormals(int vertexCount, int storedNormalCount)
    {
        // A usable stored set has exactly one normal per vertex. Heads (#2026) have zero stored
        // normals and fall through to the smoothgroup recompute; hands/bodies have a full set and
        // use it, regardless of how many smoothgroups the mesh declares (#1584).
        return vertexCount > 0 && storedNormalCount == vertexCount;
    }
}

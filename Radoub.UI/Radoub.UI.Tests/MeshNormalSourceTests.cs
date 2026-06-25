// Tests for MeshNormalSource (#1584): the normal-source gate must key on whether a usable stored
// normal set exists, NOT on smoothgroup count. Reproduces the streaky-hand bug where a
// multi-smoothgroup mesh with valid stored normals was wrongly recomputed.

using Radoub.UI.Controls;

namespace Radoub.UI.Tests;

public class MeshNormalSourceTests
{
    [Fact]
    public void HandWithMultipleSmoothgroupsButValidStoredNormals_UsesStored()
    {
        // pmh0_handl001: 186 verts, 186 stored normals, 6 smoothgroups. The old gate recomputed it
        // (smoothgroup count > 1) and streaked it. It must now use its valid stored normals.
        Assert.True(MeshNormalSource.UseStoredNormals(vertexCount: 186, storedNormalCount: 186));
    }

    [Fact]
    public void HeadWithNoStoredNormals_Recomputes()
    {
        // pmh0_head009 / pfh0_head001: stored normals absent (count 0). #2026 recompute must stand.
        Assert.False(MeshNormalSource.UseStoredNormals(vertexCount: 205, storedNormalCount: 0));
    }

    [Fact]
    public void BodyWithSingleSmoothgroupAndStoredNormals_UsesStored()
    {
        // pmh0_chest016: 132 verts, 132 stored normals, single smoothgroup — unchanged behaviour.
        Assert.True(MeshNormalSource.UseStoredNormals(vertexCount: 132, storedNormalCount: 132));
    }

    [Fact]
    public void PartialStoredNormalSet_Recomputes()
    {
        // A mismatched count is not a usable stored set — recompute rather than index out of range.
        Assert.False(MeshNormalSource.UseStoredNormals(vertexCount: 100, storedNormalCount: 40));
    }

    [Fact]
    public void EmptyMesh_DoesNotUseStored()
    {
        Assert.False(MeshNormalSource.UseStoredNormals(vertexCount: 0, storedNormalCount: 0));
    }
}

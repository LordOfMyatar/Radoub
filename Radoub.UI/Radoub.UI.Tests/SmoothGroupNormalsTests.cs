// Tests for SmoothGroupNormals (#2026).
//
// The NWN MDL format stores per-face plane normals and a SurfaceId
// smoothgroup bitmask. The Aurora Engine computes per-vertex normals
// by averaging per-face normals within each smoothgroup. This mirror
// that behaviour.

using System.Numerics;
using Radoub.UI.Controls;
using Radoub.Formats.Mdl;

namespace Radoub.UI.Tests;

public class SmoothGroupNormalsTests
{
    [Fact]
    public void SingleFaceYieldsThatFacesNormalAtEachVertex()
    {
        var verts = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0) };
        var faces = new[]
        {
            new MdlFace
            {
                VertexIndex0 = 0, VertexIndex1 = 1, VertexIndex2 = 2,
                SurfaceId = 1,
                PlaneNormal = new Vector3(0, 0, 1),
            }
        };

        var result = SmoothGroupNormals.ComputePerVertex(verts, faces);

        Assert.Equal(new Vector3(0, 0, 1), result[0]);
        Assert.Equal(new Vector3(0, 0, 1), result[1]);
        Assert.Equal(new Vector3(0, 0, 1), result[2]);
    }

    [Fact]
    public void TwoFacesSameSmoothgroupAverageAtSharedVertex()
    {
        // Two triangles sharing vertex 1. Both in smoothgroup 1.
        // Face normals (0,0,1) and (1,0,0). Shared vertex averages to (~0.707, 0, 0.707).
        var verts = new[]
        {
            new Vector3(0, 0, 0), // only in face 0
            new Vector3(1, 0, 0), // shared
            new Vector3(0, 1, 0), // only in face 0
            new Vector3(1, 1, 0), // only in face 1
            new Vector3(1, 0, 1), // only in face 1
        };
        var faces = new[]
        {
            new MdlFace
            {
                VertexIndex0 = 0, VertexIndex1 = 1, VertexIndex2 = 2,
                SurfaceId = 1,
                PlaneNormal = new Vector3(0, 0, 1),
            },
            new MdlFace
            {
                VertexIndex0 = 1, VertexIndex1 = 3, VertexIndex2 = 4,
                SurfaceId = 1,
                PlaneNormal = new Vector3(1, 0, 0),
            },
        };

        var result = SmoothGroupNormals.ComputePerVertex(verts, faces);

        Assert.Equal(new Vector3(0, 0, 1), result[0]);
        Assert.Equal(new Vector3(1, 0, 0), result[3]);

        // Shared vertex averages the two face normals and normalises.
        var shared = result[1];
        Assert.Equal(0.7071f, shared.X, 3);
        Assert.Equal(0.0f, shared.Y, 3);
        Assert.Equal(0.7071f, shared.Z, 3);
    }

    [Fact]
    public void DisjointSmoothgroupsStillCombineInPerVertexMode()
    {
        // ComputePerVertex falls back to combining all incident faces' masks,
        // so even disjoint smoothgroups contribute together at the shared
        // vertex. Hard-edge preservation requires ComputePerCorner.
        var verts = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(1, 1, 0),
            new Vector3(1, 0, 1),
        };
        var faces = new[]
        {
            new MdlFace
            {
                VertexIndex0 = 0, VertexIndex1 = 1, VertexIndex2 = 2,
                SurfaceId = 1, // group 1 only
                PlaneNormal = new Vector3(0, 0, 1),
            },
            new MdlFace
            {
                VertexIndex0 = 1, VertexIndex1 = 3, VertexIndex2 = 4,
                SurfaceId = 2, // group 2 only (no shared bit)
                PlaneNormal = new Vector3(1, 0, 0),
            },
        };

        var result = SmoothGroupNormals.ComputePerVertex(verts, faces);

        // At shared vertex 1 both faces contribute (per-vertex combines masks).
        var shared = result[1];
        Assert.True(shared.LengthSquared() > 0.99f && shared.LengthSquared() < 1.01f,
            $"Expected unit normal, got length {shared.Length():F4}");
    }

    [Fact]
    public void PerCornerPreservesHardEdgeBetweenDisjointSmoothgroups()
    {
        // Same geometry as above, but use per-corner normals so faces in
        // disjoint smoothgroups keep their own normals at shared vertices.
        var verts = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(1, 1, 0),
            new Vector3(1, 0, 1),
        };
        var faces = new[]
        {
            new MdlFace
            {
                VertexIndex0 = 0, VertexIndex1 = 1, VertexIndex2 = 2,
                SurfaceId = 1,
                PlaneNormal = new Vector3(0, 0, 1),
            },
            new MdlFace
            {
                VertexIndex0 = 1, VertexIndex1 = 3, VertexIndex2 = 4,
                SurfaceId = 2,
                PlaneNormal = new Vector3(1, 0, 0),
            },
        };

        var result = SmoothGroupNormals.ComputePerCorner(verts, faces);

        // Face 0 corner at vertex 1: only face 0 (sg 1) contributes.
        Assert.Equal(new Vector3(0, 0, 1), result[0 * 3 + 1]);
        // Face 1 corner at vertex 1: only face 1 (sg 2) contributes.
        Assert.Equal(new Vector3(1, 0, 0), result[1 * 3 + 0]);
    }

    [Fact]
    public void OverlappingSmoothgroupsBlendInPerCornerMode()
    {
        // Two faces sharing a vertex, both have smoothgroup bit 1 set.
        var verts = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(1, 1, 0),
            new Vector3(1, 0, 1),
        };
        var faces = new[]
        {
            new MdlFace
            {
                VertexIndex0 = 0, VertexIndex1 = 1, VertexIndex2 = 2,
                SurfaceId = 0b11, // groups 1 and 2
                PlaneNormal = new Vector3(0, 0, 1),
            },
            new MdlFace
            {
                VertexIndex0 = 1, VertexIndex1 = 3, VertexIndex2 = 4,
                SurfaceId = 0b01, // group 1 only (shares bit 1 with face 0)
                PlaneNormal = new Vector3(1, 0, 0),
            },
        };

        var result = SmoothGroupNormals.ComputePerCorner(verts, faces);

        // Both corners at vertex 1 blend: (0,0,1) + (1,0,0) normalised.
        var c0 = result[0 * 3 + 1];
        var c1 = result[1 * 3 + 0];
        Assert.Equal(0.7071f, c0.X, 3);
        Assert.Equal(0.7071f, c0.Z, 3);
        Assert.Equal(c0, c1);
    }

    [Fact]
    public void UsesStoredPlaneNormalWhenUnit()
    {
        // If the MDL's stored plane normal is a unit vector, we trust it
        // even when it differs from the cross-product derivation (e.g. the
        // author intentionally biased it, or cross product is degenerate).
        var verts = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0) };
        var storedPlane = Vector3.Normalize(new Vector3(0.1f, 0.2f, 1f));
        var faces = new[]
        {
            new MdlFace
            {
                VertexIndex0 = 0, VertexIndex1 = 1, VertexIndex2 = 2,
                SurfaceId = 1,
                PlaneNormal = storedPlane,
            }
        };

        var result = SmoothGroupNormals.ComputePerVertex(verts, faces);

        Assert.Equal(storedPlane.X, result[0].X, 3);
        Assert.Equal(storedPlane.Y, result[0].Y, 3);
        Assert.Equal(storedPlane.Z, result[0].Z, 3);
    }

    [Fact]
    public void FallsBackToCrossProductForNonUnitPlaneNormal()
    {
        // Zero/garbage plane normal -> recompute from vertex positions.
        var verts = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0) };
        var faces = new[]
        {
            new MdlFace
            {
                VertexIndex0 = 0, VertexIndex1 = 1, VertexIndex2 = 2,
                SurfaceId = 1,
                PlaneNormal = Vector3.Zero, // invalid
            }
        };

        var result = SmoothGroupNormals.ComputePerVertex(verts, faces);

        Assert.Equal(new Vector3(0, 0, 1), result[0]);
    }

    [Fact]
    public void IsolatedVertexYieldsUnitZ()
    {
        var verts = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(5, 5, 5) };
        var faces = new[]
        {
            new MdlFace
            {
                VertexIndex0 = 0, VertexIndex1 = 1, VertexIndex2 = 2,
                SurfaceId = 1,
                PlaneNormal = new Vector3(0, 0, 1),
            }
        };

        var result = SmoothGroupNormals.ComputePerVertex(verts, faces);

        Assert.Equal(Vector3.UnitZ, result[3]);
    }
}

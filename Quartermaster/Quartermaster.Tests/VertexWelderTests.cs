// Regression tests for VertexWelder (#2026).
//
// Background: NWN binary MDL files for part-based head models (and many
// other BioWare-compiled meshes) contain face-corner vertex duplicates
// with identical (position, normal, UV) triples. Without welding, the
// GPU renders each triangle in isolation, producing visible flat-shaded
// facets on what should be smooth surfaces — the "shading misaligned"
// symptom in #2026.

using System.Numerics;
using Quartermaster.Controls;

namespace Quartermaster.Tests;

public class VertexWelderTests
{
    [Fact]
    public void CollapsesIdenticalTriples()
    {
        // Two triangles sharing an edge, but the shared vertices are
        // duplicated with identical attributes (the #2026 pattern).
        var positions = new[]
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0),
            new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0),
        };
        var normals = new[]
        {
            Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ,
            Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ,
        };
        var uvs = new Vector2[6]; // all zero

        var result = VertexWelder.Build(positions, normals, uvs);

        // (1,0,0) and (0,1,0) each appear twice with identical attributes.
        Assert.Equal(4, result.OutputVertexCount);
        Assert.Equal(2, result.WeldedCount);

        // Same source positions map to same output index.
        Assert.Equal(result.IndexRemap[1], result.IndexRemap[3]);
        Assert.Equal(result.IndexRemap[2], result.IndexRemap[4]);
    }

    [Fact]
    public void KeepsDistinctNormalsSeparate()
    {
        // Same position, different normals = real smoothing-group split.
        // These must NOT be welded.
        var positions = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 0) };
        var normals = new[] { Vector3.UnitX, Vector3.UnitY };
        var uvs = new Vector2[2];

        var result = VertexWelder.Build(positions, normals, uvs);

        Assert.Equal(2, result.OutputVertexCount);
        Assert.Equal(0, result.WeldedCount);
        Assert.NotEqual(result.IndexRemap[0], result.IndexRemap[1]);
    }

    [Fact]
    public void KeepsDistinctUVsSeparate()
    {
        // Same position and normal, different UV = a UV seam.
        // These must NOT be welded.
        var positions = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 0) };
        var normals = new[] { Vector3.UnitZ, Vector3.UnitZ };
        var uvs = new[] { new Vector2(0, 0), new Vector2(1, 0) };

        var result = VertexWelder.Build(positions, normals, uvs);

        Assert.Equal(2, result.OutputVertexCount);
        Assert.Equal(0, result.WeldedCount);
    }

    [Fact]
    public void DropMaskSkipsFlaggedVertices()
    {
        var positions = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0) };
        var normals = new[] { Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ };
        var uvs = new Vector2[3];
        var drop = new[] { false, true, false };

        var result = VertexWelder.Build(positions, normals, uvs, drop);

        Assert.Equal(2, result.OutputVertexCount);
        Assert.Equal(-1, result.IndexRemap[1]);
        Assert.NotEqual(-1, result.IndexRemap[0]);
        Assert.NotEqual(-1, result.IndexRemap[2]);
    }

    [Fact]
    public void ToleratesFloatingPointNoise()
    {
        // Positions that differ by less than the quantisation step should weld.
        var positions = new[]
        {
            new Vector3(0.12345f, 0, 0),
            new Vector3(0.123451f, 0, 0), // ~1e-6 delta, below 1e-5 quantisation
        };
        var normals = new[] { Vector3.UnitZ, Vector3.UnitZ };
        var uvs = new Vector2[2];

        var result = VertexWelder.Build(positions, normals, uvs);

        Assert.Equal(1, result.OutputVertexCount);
        Assert.Equal(1, result.WeldedCount);
    }

    [Fact]
    public void SinglePassReturnsAllDistinctWhenNoDuplicates()
    {
        var positions = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0) };
        var normals = new[] { Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ };
        var uvs = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) };

        var result = VertexWelder.Build(positions, normals, uvs);

        Assert.Equal(3, result.OutputVertexCount);
        Assert.Equal(0, result.WeldedCount);
    }
}

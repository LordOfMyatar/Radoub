// Tests for VertexWelder (#2026).
//
// VertexWelder deduplicates per-face-corner vertex streams back into
// shared indices when adjacent corners carry identical (position, normal,
// UV) triples. This preserves intentional hard edges (different normals
// or UVs produce different keys and stay separate) while enabling the
// GPU to interpolate normals across same-smoothgroup edges.

using System.Numerics;
using Quartermaster.Controls;

namespace Quartermaster.Tests;

public class VertexWelderTests
{
    [Fact]
    public void CollapsesMatchingTriples()
    {
        // Two triangles sharing an edge. Their shared corners have
        // identical attributes (same smoothgroup neighbours), so the
        // welder should collapse 2 corners into 1 output vertex each.
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
        var uvs = new Vector2[6];

        var result = VertexWelder.Build(positions, normals, uvs);

        Assert.Equal(4, result.OutputVertexCount);
        Assert.Equal(2, result.WeldedCount);
        Assert.Equal(result.IndexRemap[1], result.IndexRemap[3]);
        Assert.Equal(result.IndexRemap[2], result.IndexRemap[4]);
    }

    [Fact]
    public void KeepsDistinctNormalsSeparate()
    {
        var positions = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 0) };
        var normals = new[] { Vector3.UnitX, Vector3.UnitY };
        var uvs = new Vector2[2];

        var result = VertexWelder.Build(positions, normals, uvs);

        Assert.Equal(2, result.OutputVertexCount);
        Assert.Equal(0, result.WeldedCount);
    }

    [Fact]
    public void KeepsDistinctUVsSeparate()
    {
        var positions = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 0) };
        var normals = new[] { Vector3.UnitZ, Vector3.UnitZ };
        var uvs = new[] { new Vector2(0, 0), new Vector2(1, 0) };

        var result = VertexWelder.Build(positions, normals, uvs);

        Assert.Equal(2, result.OutputVertexCount);
    }

    [Fact]
    public void DropMaskSkipsFlaggedCorners()
    {
        var positions = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0) };
        var normals = new[] { Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ };
        var uvs = new Vector2[3];
        var drop = new[] { false, true, false };

        var result = VertexWelder.Build(positions, normals, uvs, drop);

        Assert.Equal(2, result.OutputVertexCount);
        Assert.Equal(-1, result.IndexRemap[1]);
    }

    [Fact]
    public void ToleratesFloatingPointNoise()
    {
        var positions = new[]
        {
            new Vector3(0.12345f, 0, 0),
            new Vector3(0.123451f, 0, 0), // below 1e-5 quantisation step
        };
        var normals = new[] { Vector3.UnitZ, Vector3.UnitZ };
        var uvs = new Vector2[2];

        var result = VertexWelder.Build(positions, normals, uvs);

        Assert.Equal(1, result.OutputVertexCount);
        Assert.Equal(1, result.WeldedCount);
    }
}

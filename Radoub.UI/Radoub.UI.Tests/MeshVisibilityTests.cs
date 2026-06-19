using Radoub.UI.Controls;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for mesh visibility gating (#2498). The model preview honors the MDL Render flag
/// and skips only empty geometry — matching the Aurora engine (nwnexplorer/borealis), which
/// apply NO vertex-count or shared-bitmap heuristic. The old 30-vertex MeshSkipHeuristic
/// (#1676/#2057) was removed because it hid real geometry that reuses the body texture
/// (Antoine's hands/neck/hair, dragon spikes, snake tongue).
/// </summary>
public class MeshVisibilityTests
{
    [Fact]
    public void RenderFalse_IsHidden()
    {
        Assert.False(MeshVisibility.ShouldRender(render: false, vertexCount: 100, faceCount: 50));
    }

    [Fact]
    public void EmptyGeometry_IsHidden()
    {
        Assert.False(MeshVisibility.ShouldRender(render: true, vertexCount: 0, faceCount: 0));
        Assert.False(MeshVisibility.ShouldRender(render: true, vertexCount: 10, faceCount: 0));
        Assert.False(MeshVisibility.ShouldRender(render: true, vertexCount: 0, faceCount: 10));
    }

    [Fact]
    public void RenderTrueWithGeometry_IsVisible()
    {
        Assert.True(MeshVisibility.ShouldRender(render: true, vertexCount: 100, faceCount: 50));
    }

    [Fact]
    public void TinyTrimesh_IsVisible_NoVertexCountHeuristic()
    {
        // A tiny (<30 vert) trimesh that would have been skipped by the old heuristic — e.g.
        // Antoine's hand (28 verts) or a dragon fin (16 verts) sharing the body texture — must
        // now render. Vertex count and bitmap no longer affect visibility.
        Assert.True(MeshVisibility.ShouldRender(render: true, vertexCount: 7, faceCount: 4));
        Assert.True(MeshVisibility.ShouldRender(render: true, vertexCount: 28, faceCount: 20));
    }
}

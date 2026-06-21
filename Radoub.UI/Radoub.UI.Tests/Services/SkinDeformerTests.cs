using System;
using System.Numerics;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Tests for SkinDeformer — pure linear-blend-skinning math (no GL).
///
/// Skinning identity (verified against rollnw + our own parsed bind data, #2399):
///   inverseBind[slot] = inverse(boneBindWorld) * meshBindWorld
///   skin[slot]        = boneAnimWorld * inverseBind[slot]
///   v_world           = Σ wᵢ · (skin[slotᵢ] · v_local)
/// At bind pose (boneAnimWorld == boneBindWorld) every skin[slot] == meshBindWorld, so an
/// un-animated skin renders exactly at its current static bind-pose position — the regression
/// invariant that protects creatures already rendering correctly.
/// </summary>
public class SkinDeformerTests
{
    [Fact]
    public void BlendVertex_SingleBoneIdentitySkin_ReturnsVertexUnchanged()
    {
        var vertex = new Vector3(1, 2, 3);
        var skinMatrices = new[] { Matrix4x4.Identity };
        var weight = new SkinDeformer.VertexWeights(
            bone0: 0, weight0: 1f, bone1: -1, weight1: 0f, bone2: -1, weight2: 0f, bone3: -1, weight3: 0f);

        var result = SkinDeformer.BlendVertex(vertex, weight, skinMatrices);

        Assert.Equal(1f, result.X, 5);
        Assert.Equal(2f, result.Y, 5);
        Assert.Equal(3f, result.Z, 5);
    }

    [Fact]
    public void BuildSkinMatrix_AtBindPose_EqualsMeshBindWorld()
    {
        // A bone offset from the mesh; at bind pose the animated bone world == bind bone world.
        var boneBindWorld = Matrix4x4.CreateRotationZ(0.7f) * Matrix4x4.CreateTranslation(2, 1, 3);
        var meshBindWorld = Matrix4x4.CreateTranslation(5, 0, 0);

        var inverseBind = SkinDeformer.BuildInverseBind(boneBindWorld, meshBindWorld);
        // boneAnimWorld == boneBindWorld (no animation) → skin must collapse to meshBindWorld.
        var skin = SkinDeformer.BuildSkinMatrix(boneBindWorld, inverseBind);

        AssertMatrixApproxEqual(meshBindWorld, skin);
    }

    [Fact]
    public void BlendVertex_BindPose_LeavesVertexAtBindWorldPosition()
    {
        // Full pipeline at bind pose: a vertex blended through a bone must land where the static
        // mesh bind-pose transform would put it (the regression invariant for #2399).
        var boneBindWorld = Matrix4x4.CreateRotationX(0.4f) * Matrix4x4.CreateTranslation(1, 2, 3);
        var meshBindWorld = Matrix4x4.CreateRotationY(0.2f) * Matrix4x4.CreateTranslation(-1, 0, 4);
        var localVertex = new Vector3(0.5f, -0.3f, 0.9f);

        var inverseBind = SkinDeformer.BuildInverseBind(boneBindWorld, meshBindWorld);
        var skin = new[] { SkinDeformer.BuildSkinMatrix(boneBindWorld, inverseBind) };
        var w = new SkinDeformer.VertexWeights(0, 1f, -1, 0f, -1, 0f, -1, 0f);

        var deformed = SkinDeformer.BlendVertex(localVertex, w, skin);
        var staticBind = Vector3.Transform(localVertex, meshBindWorld);

        Assert.Equal(staticBind.X, deformed.X, 4);
        Assert.Equal(staticBind.Y, deformed.Y, 4);
        Assert.Equal(staticBind.Z, deformed.Z, 4);
    }

    [Fact]
    public void BlendVertex_AnimatedBone_MovesVertexByBoneDelta()
    {
        // bone and mesh share bind world (identity) so inverseBind == identity; then animate the
        // bone by a pure +Z translation. A fully-weighted vertex must move by exactly that delta.
        var boneBindWorld = Matrix4x4.Identity;
        var meshBindWorld = Matrix4x4.Identity;
        var boneAnimWorld = Matrix4x4.CreateTranslation(0, 0, 10);

        var inverseBind = SkinDeformer.BuildInverseBind(boneBindWorld, meshBindWorld);
        var skin = new[] { SkinDeformer.BuildSkinMatrix(boneAnimWorld, inverseBind) };
        var w = new SkinDeformer.VertexWeights(0, 1f, -1, 0f, -1, 0f, -1, 0f);

        var deformed = SkinDeformer.BlendVertex(new Vector3(1, 2, 3), w, skin);

        Assert.Equal(1f, deformed.X, 4);
        Assert.Equal(2f, deformed.Y, 4);
        Assert.Equal(13f, deformed.Z, 4); // 3 + 10
    }

    [Fact]
    public void BlendVertex_TwoBonesHalfWeight_AveragesTransforms()
    {
        // Vertex weighted 50/50 between an unmoved bone and a +10Z bone → moves +5Z.
        var skin = new[]
        {
            Matrix4x4.Identity,
            Matrix4x4.CreateTranslation(0, 0, 10),
        };
        var w = new SkinDeformer.VertexWeights(0, 0.5f, 1, 0.5f, -1, 0f, -1, 0f);

        var deformed = SkinDeformer.BlendVertex(new Vector3(0, 0, 0), w, skin);

        Assert.Equal(0f, deformed.X, 4);
        Assert.Equal(0f, deformed.Y, 4);
        Assert.Equal(5f, deformed.Z, 4);
    }

    [Fact]
    public void BlendNormal_RotationOnlySkin_RotatesNormalIgnoringTranslation()
    {
        // A skin matrix with a 90° Z rotation AND a translation; the normal must rotate but NOT
        // pick up the translation (normals are direction vectors).
        var skin = new[] { Matrix4x4.CreateRotationZ(MathF.PI / 2f) * Matrix4x4.CreateTranslation(100, 0, 0) };
        var w = new SkinDeformer.VertexWeights(0, 1f, -1, 0f, -1, 0f, -1, 0f);

        var n = SkinDeformer.BlendNormal(new Vector3(1, 0, 0), w, skin);

        // +X rotated 90° about Z → +Y; translation ignored.
        Assert.Equal(0f, n.X, 4);
        Assert.Equal(1f, n.Y, 4);
        Assert.Equal(0f, n.Z, 4);
    }

    private static void AssertMatrixApproxEqual(Matrix4x4 expected, Matrix4x4 actual)
    {
        Assert.Equal(expected.M11, actual.M11, 4);
        Assert.Equal(expected.M12, actual.M12, 4);
        Assert.Equal(expected.M13, actual.M13, 4);
        Assert.Equal(expected.M21, actual.M21, 4);
        Assert.Equal(expected.M22, actual.M22, 4);
        Assert.Equal(expected.M23, actual.M23, 4);
        Assert.Equal(expected.M31, actual.M31, 4);
        Assert.Equal(expected.M32, actual.M32, 4);
        Assert.Equal(expected.M33, actual.M33, 4);
        Assert.Equal(expected.M41, actual.M41, 4);
        Assert.Equal(expected.M42, actual.M42, 4);
        Assert.Equal(expected.M43, actual.M43, 4);
    }
}

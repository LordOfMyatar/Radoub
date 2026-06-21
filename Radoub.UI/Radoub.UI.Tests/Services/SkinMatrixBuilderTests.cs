using System.Collections.Generic;
using System.Numerics;
using Radoub.Formats.Mdl;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Tests for SkinMatrixBuilder — resolves a skin mesh's per-slot skin matrices from the
/// composed bone hierarchy + animation pose (#2399). Bridges parser data (BoneNodeNames,
/// the skin mesh node) to SkinDeformer's pure math.
/// </summary>
public class SkinMatrixBuilderTests
{
    // Build a minimal composite: root → bone(named) → skin(child of root).
    private static (MdlSkinNode skin, MdlNode root) BuildRig(
        Vector3 bonePos, string boneName, Vector3 meshPos)
    {
        var root = new MdlNode { Name = "root", Scale = 1f, Orientation = Quaternion.Identity };
        var bone = new MdlNode { Name = boneName, Position = bonePos, Scale = 1f, Orientation = Quaternion.Identity, Parent = root };
        root.Children.Add(bone);
        var skin = new MdlSkinNode
        {
            Name = "skinbody",
            Position = meshPos,
            Scale = 1f,
            Orientation = Quaternion.Identity,
            Parent = root,
            BoneNodeNames = new[] { boneName },
        };
        root.Children.Add(skin);
        return (skin, root);
    }

    [Fact]
    public void Build_NoPose_SkinMatrixEqualsMeshBindWorld()
    {
        var (skin, root) = BuildRig(new Vector3(0, 0, 1), "torso_g", new Vector3(2, 0, 0));

        var matrices = SkinMatrixBuilder.Build(skin, root, pose: null);

        // At bind pose, slot 0's skin matrix must equal the mesh's bind-world transform.
        var meshBindWorld = ModelViewController.GetWorldTransform(skin, null);
        Assert.Single(matrices);
        AssertMatrixApprox(meshBindWorld, matrices[0]);
    }

    [Fact]
    public void Build_WithBonePose_AppliesAnimatedDelta()
    {
        var (skin, root) = BuildRig(new Vector3(0, 0, 1), "torso_g", new Vector3(0, 0, 0));

        // Animate torso_g: translate +5 on Z (pose Position replaces the bone's bind position).
        var pose = new Dictionary<string, ModelViewController.NodePose>
        {
            ["torso_g"] = new ModelViewController.NodePose(
                HasPosition: true, Position: new Vector3(0, 0, 6),
                HasOrientation: false, Orientation: Quaternion.Identity,
                HasScale: false, Scale: 1f),
        };

        var matrices = SkinMatrixBuilder.Build(skin, root, pose);

        // A vertex at local origin, fully weighted to slot 0, should move from bind (z=0) to
        // bind + (animated bone delta). Bone bind z=1, animated z=6 → delta +5.
        var w = new SkinDeformer.VertexWeights(0, 1f, -1, 0f, -1, 0f, -1, 0f);
        var deformed = SkinDeformer.BlendVertex(Vector3.Zero, w, matrices);
        Assert.Equal(0f, deformed.X, 4);
        Assert.Equal(0f, deformed.Y, 4);
        Assert.Equal(5f, deformed.Z, 4);
    }

    [Fact]
    public void Build_MissingBone_SlotFallsBackToMeshBindWorld()
    {
        var (skin, root) = BuildRig(new Vector3(0, 0, 1), "torso_g", new Vector3(1, 1, 1));
        skin.BoneNodeNames = new[] { "nonexistent_bone" };

        var matrices = SkinMatrixBuilder.Build(skin, root, pose: null);

        // Unknown bone → keep the mesh at its static bind-world position (no crash, no collapse).
        var meshBindWorld = ModelViewController.GetWorldTransform(skin, null);
        AssertMatrixApprox(meshBindWorld, matrices[0]);
    }

    private static void AssertMatrixApprox(Matrix4x4 e, Matrix4x4 a)
    {
        Assert.Equal(e.M11, a.M11, 4); Assert.Equal(e.M22, a.M22, 4); Assert.Equal(e.M33, a.M33, 4);
        Assert.Equal(e.M41, a.M41, 4); Assert.Equal(e.M42, a.M42, 4); Assert.Equal(e.M43, a.M43, 4);
    }
}

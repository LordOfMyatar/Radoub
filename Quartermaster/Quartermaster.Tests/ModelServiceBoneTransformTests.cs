using System.Numerics;
using Quartermaster.Services;
using Radoub.Formats.Mdl;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for ModelService.GetBoneWorldTransform — validates that bone world positions
/// correctly account for parent rotations, scale, and translation through the hierarchy.
/// Fix for #1557: elf head/neck mismatch and halfling proportion issues.
/// </summary>
public class ModelServiceBoneTransformTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void GetBoneWorldTransform_SingleNode_ReturnsNodeTransform()
    {
        var node = new MdlNode
        {
            Name = "bone",
            Position = new Vector3(1, 2, 3),
            Orientation = Quaternion.Identity,
            Scale = 1.0f
        };

        var result = ModelService.GetBoneWorldTransform(node);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        Assert.Equal(1f, translation.X, Tolerance);
        Assert.Equal(2f, translation.Y, Tolerance);
        Assert.Equal(3f, translation.Z, Tolerance);
    }

    [Fact]
    public void GetBoneWorldTransform_IdentityParent_ReturnsChildPosition()
    {
        var parent = new MdlNode
        {
            Name = "root",
            Position = new Vector3(10, 0, 0),
            Orientation = Quaternion.Identity,
            Scale = 1.0f
        };

        var child = new MdlNode
        {
            Name = "head_g",
            Position = new Vector3(0, 0, 5),
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = parent
        };
        parent.Children.Add(child);

        var result = ModelService.GetBoneWorldTransform(child);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        // With identity rotation, world position is simply sum of positions
        Assert.Equal(10f, translation.X, Tolerance);
        Assert.Equal(0f, translation.Y, Tolerance);
        Assert.Equal(5f, translation.Z, Tolerance);
    }

    [Fact]
    public void GetBoneWorldTransform_RotatedParent_AppliesRotationToChildPosition()
    {
        // Parent rotated 90 degrees around Z axis
        var rotation90Z = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);

        var parent = new MdlNode
        {
            Name = "root",
            Position = Vector3.Zero,
            Orientation = rotation90Z,
            Scale = 1.0f
        };

        var child = new MdlNode
        {
            Name = "head_g",
            Position = new Vector3(1, 0, 0), // Local: 1 unit along X
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = parent
        };
        parent.Children.Add(child);

        var result = ModelService.GetBoneWorldTransform(child);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        // Parent rotation of 90° around Z should rotate child's local X-offset to Y direction
        // (1,0,0) rotated 90° around Z = (0,1,0)
        Assert.Equal(0f, translation.X, Tolerance);
        Assert.Equal(1f, translation.Y, Tolerance);
        Assert.Equal(0f, translation.Z, Tolerance);
    }

    [Fact]
    public void GetBoneWorldTransform_RotatedParentWithOffset_CombinesCorrectly()
    {
        // Parent at (5,0,0) rotated 90° around Z
        var rotation90Z = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);

        var parent = new MdlNode
        {
            Name = "torso",
            Position = new Vector3(5, 0, 0),
            Orientation = rotation90Z,
            Scale = 1.0f
        };

        var child = new MdlNode
        {
            Name = "head_g",
            Position = new Vector3(2, 0, 0), // 2 units along local X
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = parent
        };
        parent.Children.Add(child);

        var result = ModelService.GetBoneWorldTransform(child);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        // Child position (2,0,0) rotated by parent's 90°Z = (0,2,0), then + parent pos (5,0,0)
        Assert.Equal(5f, translation.X, Tolerance);
        Assert.Equal(2f, translation.Y, Tolerance);
        Assert.Equal(0f, translation.Z, Tolerance);
    }

    [Fact]
    public void GetBoneWorldTransform_ScaledParent_AppliesScaleToChildPosition()
    {
        var parent = new MdlNode
        {
            Name = "root",
            Position = Vector3.Zero,
            Orientation = Quaternion.Identity,
            Scale = 2.0f
        };

        var child = new MdlNode
        {
            Name = "bone",
            Position = new Vector3(1, 1, 1),
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = parent
        };
        parent.Children.Add(child);

        var result = ModelService.GetBoneWorldTransform(child);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        // Parent scale 2x should scale child's position
        Assert.Equal(2f, translation.X, Tolerance);
        Assert.Equal(2f, translation.Y, Tolerance);
        Assert.Equal(2f, translation.Z, Tolerance);
    }

    [Fact]
    public void GetBoneWorldTransform_ThreeLevelHierarchy_AccumulatesCorrectly()
    {
        // Root at origin, parent rotated 90° around Z, child offset from parent
        var rotation90Z = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);

        var root = new MdlNode
        {
            Name = "rootdummy",
            Position = new Vector3(0, 0, 1), // 1 unit up
            Orientation = Quaternion.Identity,
            Scale = 1.0f
        };

        var mid = new MdlNode
        {
            Name = "torso_g",
            Position = new Vector3(0, 0, 2), // 2 units up from root
            Orientation = rotation90Z,
            Scale = 1.0f,
            Parent = root
        };
        root.Children.Add(mid);

        var leaf = new MdlNode
        {
            Name = "head_g",
            Position = new Vector3(1, 0, 0), // 1 unit along local X (will be rotated)
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = mid
        };
        mid.Children.Add(leaf);

        var result = ModelService.GetBoneWorldTransform(leaf);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        // Leaf (1,0,0) → mid rotation 90°Z → (0,1,0) + mid pos (0,0,2) → (0,1,2) + root pos (0,0,1) → (0,1,3)
        Assert.Equal(0f, translation.X, Tolerance);
        Assert.Equal(1f, translation.Y, Tolerance);
        Assert.Equal(3f, translation.Z, Tolerance);
    }

    [Fact]
    public void GetBoneWorldTransform_IdentityHierarchy_EqualsPositionSum()
    {
        // When all rotations are identity, the result should equal simple position addition
        // This verifies the fix doesn't break races that worked before (human, dwarf)
        var root = new MdlNode
        {
            Name = "rootdummy",
            Position = new Vector3(1, 2, 3),
            Orientation = Quaternion.Identity,
            Scale = 1.0f
        };

        var mid = new MdlNode
        {
            Name = "torso_g",
            Position = new Vector3(0, 0, 5),
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = root
        };
        root.Children.Add(mid);

        var leaf = new MdlNode
        {
            Name = "head_g",
            Position = new Vector3(0, 0, 2),
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = mid
        };
        mid.Children.Add(leaf);

        var result = ModelService.GetBoneWorldTransform(leaf);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        // Simple sum: (1,2,3) + (0,0,5) + (0,0,2) = (1,2,10)
        var expectedSum = root.Position + mid.Position + leaf.Position;
        Assert.Equal(expectedSum.X, translation.X, Tolerance);
        Assert.Equal(expectedSum.Y, translation.Y, Tolerance);
        Assert.Equal(expectedSum.Z, translation.Z, Tolerance);
    }

    [Fact]
    public void GetBoneWorldTransform_PreservesOrientation()
    {
        var rotation45X = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 4f);

        var node = new MdlNode
        {
            Name = "bone",
            Position = Vector3.Zero,
            Orientation = rotation45X,
            Scale = 1.0f
        };

        var result = ModelService.GetBoneWorldTransform(node);
        Matrix4x4.Decompose(result, out _, out var rotation, out _);

        // Rotation should be preserved
        Assert.Equal(rotation45X.X, rotation.X, Tolerance);
        Assert.Equal(rotation45X.Y, rotation.Y, Tolerance);
        Assert.Equal(rotation45X.Z, rotation.Z, Tolerance);
        Assert.Equal(rotation45X.W, rotation.W, Tolerance);
    }

    [Fact]
    public void GetBoneWorldTransform_NoParent_ReturnsSelfTransform()
    {
        var node = new MdlNode
        {
            Name = "orphan",
            Position = new Vector3(3, 4, 5),
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = null
        };

        var result = ModelService.GetBoneWorldTransform(node);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        Assert.Equal(3f, translation.X, Tolerance);
        Assert.Equal(4f, translation.Y, Tolerance);
        Assert.Equal(5f, translation.Z, Tolerance);
    }
}

using System.Numerics;
using Radoub.Formats.Mdl;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Tests for MdlPartComposer.GetBoneWorldTransform — validates that bone world positions
/// correctly account for parent rotations, scale, and translation through the hierarchy.
/// Originally added in Quartermaster for #1557 (elf head/neck mismatch); migrated to
/// Radoub.UI.Tests when the bone-transform helper moved into MdlPartComposer (PR3a, #2159).
/// </summary>
public class MdlPartComposerBoneTransformTests
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

        var result = MdlPartComposer.GetBoneWorldTransform(node);
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

        var result = MdlPartComposer.GetBoneWorldTransform(child);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        Assert.Equal(10f, translation.X, Tolerance);
        Assert.Equal(0f, translation.Y, Tolerance);
        Assert.Equal(5f, translation.Z, Tolerance);
    }

    [Fact]
    public void GetBoneWorldTransform_RotatedParent_AppliesRotationToChildPosition()
    {
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
            Position = new Vector3(1, 0, 0),
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = parent
        };
        parent.Children.Add(child);

        var result = MdlPartComposer.GetBoneWorldTransform(child);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        Assert.Equal(0f, translation.X, Tolerance);
        Assert.Equal(1f, translation.Y, Tolerance);
        Assert.Equal(0f, translation.Z, Tolerance);
    }

    [Fact]
    public void GetBoneWorldTransform_RotatedParentWithOffset_CombinesCorrectly()
    {
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
            Position = new Vector3(2, 0, 0),
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = parent
        };
        parent.Children.Add(child);

        var result = MdlPartComposer.GetBoneWorldTransform(child);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

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

        var result = MdlPartComposer.GetBoneWorldTransform(child);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        Assert.Equal(2f, translation.X, Tolerance);
        Assert.Equal(2f, translation.Y, Tolerance);
        Assert.Equal(2f, translation.Z, Tolerance);
    }

    [Fact]
    public void GetBoneWorldTransform_ThreeLevelHierarchy_AccumulatesCorrectly()
    {
        var rotation90Z = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);

        var root = new MdlNode
        {
            Name = "rootdummy",
            Position = new Vector3(0, 0, 1),
            Orientation = Quaternion.Identity,
            Scale = 1.0f
        };

        var mid = new MdlNode
        {
            Name = "torso_g",
            Position = new Vector3(0, 0, 2),
            Orientation = rotation90Z,
            Scale = 1.0f,
            Parent = root
        };
        root.Children.Add(mid);

        var leaf = new MdlNode
        {
            Name = "head_g",
            Position = new Vector3(1, 0, 0),
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = mid
        };
        mid.Children.Add(leaf);

        var result = MdlPartComposer.GetBoneWorldTransform(leaf);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        Assert.Equal(0f, translation.X, Tolerance);
        Assert.Equal(1f, translation.Y, Tolerance);
        Assert.Equal(3f, translation.Z, Tolerance);
    }

    [Fact]
    public void GetBoneWorldTransform_IdentityHierarchy_EqualsPositionSum()
    {
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

        var result = MdlPartComposer.GetBoneWorldTransform(leaf);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

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

        var result = MdlPartComposer.GetBoneWorldTransform(node);
        Matrix4x4.Decompose(result, out _, out var rotation, out _);

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

        var result = MdlPartComposer.GetBoneWorldTransform(node);
        Matrix4x4.Decompose(result, out _, out _, out var translation);

        Assert.Equal(3f, translation.X, Tolerance);
        Assert.Equal(4f, translation.Y, Tolerance);
        Assert.Equal(5f, translation.Z, Tolerance);
    }
}

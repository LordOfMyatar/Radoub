using System.Numerics;
using Quartermaster.Controls;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for ModelViewController — pure math, no GL dependency.
/// </summary>
public class ModelViewControllerTests
{
    [Fact]
    public void GetWorldTransform_NullNode_ReturnsIdentity()
    {
        var result = ModelViewController.GetWorldTransform(null);
        Assert.Equal(Matrix4x4.Identity, result);
    }

    [Fact]
    public void TransformPosition_IdentityMatrix_ReturnsSamePosition()
    {
        var pos = new Vector3(1, 2, 3);
        var result = ModelViewController.TransformPosition(pos, Matrix4x4.Identity);

        Assert.Equal(pos.X, result.X, 5);
        Assert.Equal(pos.Y, result.Y, 5);
        Assert.Equal(pos.Z, result.Z, 5);
    }

    [Fact]
    public void TransformPosition_TranslationMatrix_TranslatesCorrectly()
    {
        var pos = new Vector3(1, 2, 3);
        var matrix = Matrix4x4.CreateTranslation(10, 20, 30);
        var result = ModelViewController.TransformPosition(pos, matrix);

        Assert.Equal(11, result.X, 5);
        Assert.Equal(22, result.Y, 5);
        Assert.Equal(33, result.Z, 5);
    }

    [Fact]
    public void TransformNormal_IdentityMatrix_ReturnsSameNormal()
    {
        var normal = Vector3.Normalize(new Vector3(1, 1, 0));
        var result = ModelViewController.TransformNormal(normal, Matrix4x4.Identity);

        Assert.Equal(normal.X, result.X, 4);
        Assert.Equal(normal.Y, result.Y, 4);
        Assert.Equal(normal.Z, result.Z, 4);
    }

    [Fact]
    public void TransformNormal_ResultIsNormalized()
    {
        var normal = new Vector3(0, 0, 1);
        var matrix = Matrix4x4.CreateScale(2f); // Scale shouldn't affect normal length
        var result = ModelViewController.TransformNormal(normal, matrix);

        var length = result.Length();
        Assert.Equal(1.0f, length, 4);
    }

    [Fact]
    public void CenterCamera_ResetsToDefaults()
    {
        var vc = new ModelViewController();
        vc.UpdateBounds(5.0f, true);

        vc.CenterCamera();

        Assert.Equal(Vector3.Zero, vc.CameraTarget);
        Assert.Equal(1.0f, vc.ModelRadius);
        Assert.False(vc.HasVertexBounds);
    }

    [Fact]
    public void ResetView_ResetsRotationAndZoom()
    {
        var vc = new ModelViewController();
        vc.RotationY = 1.5f;
        vc.RotationX = 0.5f;
        vc.Zoom = 3.0f;

        vc.ResetView();

        Assert.Equal(MathF.PI, vc.RotationY);
        Assert.Equal(0, vc.RotationX);
        Assert.Equal(1.0f, vc.Zoom);
    }

    [Fact]
    public void ResetView_PreservesVertexBounds_WhenSet()
    {
        var vc = new ModelViewController();
        vc.UpdateBounds(5.0f, true);
        vc.RotationY = 2.0f;

        vc.ResetView();

        // Vertex bounds should be preserved (not reset to 1.0)
        Assert.Equal(5.0f, vc.ModelRadius);
        Assert.True(vc.HasVertexBounds);
    }

    [Fact]
    public void Rotate_AccumulatesDeltas()
    {
        var vc = new ModelViewController();
        var initialY = vc.RotationY;
        var initialX = vc.RotationX;

        vc.Rotate(0.5f, 0.3f);

        Assert.Equal(initialY + 0.5f, vc.RotationY, 5);
        Assert.Equal(initialX + 0.3f, vc.RotationX, 5);
    }

    [Fact]
    public void Zoom_ClampsToRange()
    {
        var vc = new ModelViewController();

        vc.Zoom = 0.01f;
        Assert.Equal(0.1f, vc.Zoom);

        vc.Zoom = 100f;
        Assert.Equal(10f, vc.Zoom);
    }

    [Fact]
    public void UpdateBounds_SetsRadiusAndFlag()
    {
        var vc = new ModelViewController();

        vc.UpdateBounds(3.5f, true);

        Assert.Equal(3.5f, vc.ModelRadius);
        Assert.True(vc.HasVertexBounds);
        Assert.Equal(Vector3.Zero, vc.CameraTarget);
    }
}

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

    // ----- #2124: Pan + cursor-centric zoom -----

    [Fact]
    public void CenterCamera_PreservesUserPan()
    {
        // Switching heads / equipment triggers CenterCamera. The user's
        // pan (and zoom) must survive so they keep looking at the spot
        // they zoomed into (#2124).
        var vc = new ModelViewController();
        vc.UpdateBounds(2f, true);
        vc.Pan(new Vector3(3, 0, 2));
        vc.Zoom = 4f;

        vc.CenterCamera();

        Assert.Equal(3f, vc.CameraTarget.X, 4);
        Assert.Equal(2f, vc.CameraTarget.Z, 4);
        Assert.Equal(4f, vc.Zoom, 4);
    }

    [Fact]
    public void UpdateBounds_PreservesUserPan()
    {
        var vc = new ModelViewController();
        vc.Pan(new Vector3(1, 0, 1));

        vc.UpdateBounds(3.5f, true);

        Assert.Equal(1f, vc.CameraTarget.X, 4);
        Assert.Equal(1f, vc.CameraTarget.Z, 4);
        Assert.Equal(3.5f, vc.ModelRadius);
    }

    [Fact]
    public void Pan_TranslatesCameraTargetByDelta()
    {
        var vc = new ModelViewController();
        vc.UpdateBounds(1.0f, true);

        vc.Pan(new Vector3(0.5f, 0, -0.25f));

        Assert.Equal(0.5f, vc.CameraTarget.X, 5);
        Assert.Equal(0f, vc.CameraTarget.Y, 5);
        Assert.Equal(-0.25f, vc.CameraTarget.Z, 5);
    }

    [Fact]
    public void Pan_Accumulates()
    {
        var vc = new ModelViewController();
        vc.UpdateBounds(1.0f, true);

        vc.Pan(new Vector3(1, 0, 0));
        vc.Pan(new Vector3(0, 0, 2));
        vc.Pan(new Vector3(-0.5f, 0, 0));

        Assert.Equal(0.5f, vc.CameraTarget.X, 5);
        Assert.Equal(0f, vc.CameraTarget.Y, 5);
        Assert.Equal(2f, vc.CameraTarget.Z, 5);
    }

    [Fact]
    public void ResetView_ClearsPan()
    {
        var vc = new ModelViewController();
        vc.UpdateBounds(2.0f, true);
        vc.Pan(new Vector3(3, 4, 5));

        vc.ResetView();

        Assert.Equal(Vector3.Zero, vc.CameraTarget);
    }

    [Fact]
    public void ZoomAtPoint_ZoomingIn_MovesTargetTowardPivot()
    {
        // When zooming in at an off-center world point, the camera target should
        // drift toward the pivot so the pivot stays roughly under the cursor.
        var vc = new ModelViewController();
        vc.UpdateBounds(1.0f, true);
        float initialZoom = vc.Zoom;
        var pivot = new Vector3(2, 0, 0);

        vc.ZoomAtPoint(1.5f, pivot);

        Assert.True(vc.Zoom > initialZoom, "Zoom should increase");
        Assert.True(vc.CameraTarget.X > 0, "Target should drift toward pivot on X");
        Assert.True(vc.CameraTarget.X < pivot.X, "Target should not overshoot pivot");
    }

    [Fact]
    public void ZoomAtPoint_AtCameraTarget_KeepsTargetStable()
    {
        // Zooming at the current target (pivot == target) must not move the target.
        var vc = new ModelViewController();
        vc.UpdateBounds(1.0f, true);
        vc.Pan(new Vector3(1, 0, 0));
        var pivot = vc.CameraTarget;

        vc.ZoomAtPoint(1.5f, pivot);

        Assert.Equal(pivot.X, vc.CameraTarget.X, 4);
        Assert.Equal(pivot.Y, vc.CameraTarget.Y, 4);
        Assert.Equal(pivot.Z, vc.CameraTarget.Z, 4);
    }

    [Fact]
    public void ZoomAtPoint_ClampsToZoomRange()
    {
        var vc = new ModelViewController();
        vc.UpdateBounds(1.0f, true);

        vc.ZoomAtPoint(1000f, Vector3.Zero);
        Assert.Equal(10f, vc.Zoom, 4);

        vc.ZoomAtPoint(0.0001f, Vector3.Zero);
        Assert.Equal(0.1f, vc.Zoom, 4);
    }

    [Fact]
    public void ViewPreset_Front_SetsExpectedRotation()
    {
        var vc = new ModelViewController();
        vc.RotationY = 0.5f;
        vc.RotationX = 0.3f;

        vc.SetViewPreset(ViewPreset.Front);

        Assert.Equal(MathF.PI, vc.RotationY, 4);
        Assert.Equal(0f, vc.RotationX, 4);
    }

    [Fact]
    public void ViewPreset_Back_SetsZeroY()
    {
        var vc = new ModelViewController();
        vc.SetViewPreset(ViewPreset.Back);

        Assert.Equal(0f, vc.RotationY, 4);
        Assert.Equal(0f, vc.RotationX, 4);
    }

    [Fact]
    public void ViewPreset_Side_SetsQuarterTurn()
    {
        var vc = new ModelViewController();
        vc.SetViewPreset(ViewPreset.Side);

        Assert.Equal(MathF.PI / 2f, vc.RotationY, 4);
        Assert.Equal(0f, vc.RotationX, 4);
    }

    [Fact]
    public void ViewPreset_Top_TiltsDownOnX()
    {
        var vc = new ModelViewController();
        vc.SetViewPreset(ViewPreset.Top);

        Assert.Equal(MathF.PI, vc.RotationY, 4);
        Assert.Equal(MathF.PI / 2f, vc.RotationX, 4);
    }

    [Fact]
    public void ViewPreset_ClearsPanAndRestoresDefaultZoom()
    {
        var vc = new ModelViewController();
        vc.UpdateBounds(2.0f, true);
        vc.Pan(new Vector3(3, 0, 4));
        vc.Zoom = 5f;

        vc.SetViewPreset(ViewPreset.Front);

        Assert.Equal(Vector3.Zero, vc.CameraTarget);
        Assert.Equal(1.0f, vc.Zoom, 4);
    }

    [Fact]
    public void ZoomAtPoint_DoesNotMoveTarget_WhenZoomClamped()
    {
        // If the zoom factor is clamped (no effective change), the pivot pull must
        // also be suppressed — otherwise repeated scrolling at max zoom would drift.
        var vc = new ModelViewController();
        vc.UpdateBounds(1.0f, true);
        vc.Zoom = 10f; // at max
        var before = vc.CameraTarget;

        vc.ZoomAtPoint(2f, new Vector3(5, 0, 0));

        Assert.Equal(before.X, vc.CameraTarget.X, 4);
        Assert.Equal(before.Y, vc.CameraTarget.Y, 4);
        Assert.Equal(before.Z, vc.CameraTarget.Z, 4);
    }
}

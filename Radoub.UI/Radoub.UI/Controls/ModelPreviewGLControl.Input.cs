using System;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Radoub.UI.Services;

namespace Radoub.UI.Controls;

/// <summary>
/// ModelPreviewGLControl partial: pointer/keyboard input and the screen-pixel → world-space camera
/// gestures (pan/zoom/rotate) the host overlay forwards in. Split from the monolithic control (#2127);
/// no behavior change. Shared GL/camera state lives in the main partial.
/// </summary>
public partial class ModelPreviewGLControl
{
    // ----- Pointer / keyboard input (#2124) -----
    //
    // OpenGlControlBase has no Background brush, so pointer events don't
    // hit-test on its transparent pixels. The host panel overlays a
    // transparent Border on top of this control and forwards input here
    // via the public Handle* / Pan / ZoomAtCursor / RotateBy methods
    // below. The OnPointerPressed etc. overrides remain as a fallback
    // if the control is ever used without the overlay.

    /// <summary>
    /// Host panels call this to request a pan in screen-pixel units.
    /// Converts to world units using the last-rendered viewport + camera distance.
    /// </summary>
    public void PanByPixels(double dxPixels, double dyPixels)
    {
        if (_lastViewportHeight <= 0) return;
        float fovRadians = MathF.PI / 6f;
        float halfFovTan = MathF.Tan(fovRadians * 0.5f);
        float worldPerPixel = 2f * _lastCameraDistance * halfFovTan / _lastViewportHeight;

        // See OnPointerMoved for axis convention; camera right = -X, up = +Z.
        _viewController.Pan(new Vector3(-(float)dxPixels * worldPerPixel, 0, (float)dyPixels * worldPerPixel));
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Host panels call this to zoom toward a world point under the cursor.
    /// </summary>
    public void ZoomAtCursorPixels(Point screenPos, double wheelDeltaY)
    {
        float factor = (float)Math.Pow(1.1, wheelDeltaY);
        var pivot = UnprojectToTargetPlane(screenPos);
        _viewController.ZoomAtPoint(factor, pivot);
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Host panels call this to rotate by screen-pixel drag deltas.
    /// </summary>
    public void RotateByPixels(double dxPixels, double dyPixels)
    {
        _viewController.Rotate((float)(dxPixels * 0.01), (float)(dyPixels * 0.01));
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Cycle debug visualisation mode (0..4).
    /// </summary>
    public void CycleDebugMode()
    {
        DebugMode = (_debugMode + 1) % 5;
    }

    /// <summary>
    /// Snap to a preset view (front/side/top/back). #2124.
    /// </summary>
    public void SetViewPreset(ViewPreset preset)
    {
        _viewController.SetViewPreset(preset);
        RequestNextFrameRendering();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var props = e.GetCurrentPoint(this).Properties;
        bool shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && shift))
        {
            _dragMode = DragMode.Pan;
        }
        else if (props.IsLeftButtonPressed)
        {
            _dragMode = DragMode.Rotate;
        }
        else
        {
            return;
        }

        _lastPointerPos = e.GetPosition(this);
        Focus();
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragMode == DragMode.None) return;

        var pos = e.GetPosition(this);
        double dx = pos.X - _lastPointerPos.X;
        double dy = pos.Y - _lastPointerPos.Y;
        _lastPointerPos = pos;

        if (_dragMode == DragMode.Rotate)
        {
            // 0.01 rad/px matches the feel of the discrete rotate buttons
            // (0.3 rad per click). Free X-axis orbit — no clamping.
            _viewController.Rotate((float)(dx * 0.01), (float)(dy * 0.01));
            RequestNextFrameRendering();
        }
        else if (_dragMode == DragMode.Pan)
        {
            // Convert screen-pixel delta to world units at the target's depth.
            // At the camera-to-target distance, one NDC unit covers
            // cameraDistance * halfFovTan world units vertically.
            if (_lastViewportHeight <= 0) return;
            float fovRadians = MathF.PI / 6f;
            float halfFovTan = MathF.Tan(fovRadians * 0.5f);
            float worldPerPixel = 2f * _lastCameraDistance * halfFovTan / _lastViewportHeight;

            var worldDelta = ScreenDeltaToWorld(-(float)dx * worldPerPixel, (float)dy * worldPerPixel);
            _viewController.Pan(worldDelta);
            RequestNextFrameRendering();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragMode != DragMode.None)
        {
            _dragMode = DragMode.None;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        // Each wheel notch is ~1.0 in e.Delta.Y. 1.1× per notch gives
        // a natural zoom feel without being twitchy.
        float factor = (float)Math.Pow(1.1, e.Delta.Y);

        var pos = e.GetPosition(this);
        var pivot = UnprojectToTargetPlane(pos);
        _viewController.ZoomAtPoint(factor, pivot);

        RequestNextFrameRendering();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        const float rotStep = 0.1f;

        switch (e.Key)
        {
            case Key.Left:
            case Key.A:
                _viewController.Rotate(-rotStep, 0);
                break;
            case Key.Right:
            case Key.D:
                _viewController.Rotate(rotStep, 0);
                break;
            case Key.Up:
            case Key.W:
                _viewController.Rotate(0, -rotStep);
                break;
            case Key.Down:
            case Key.S:
                _viewController.Rotate(0, rotStep);
                break;
            case Key.Home:
                _viewController.ResetView();
                break;
            case Key.F8:
                DebugMode = (_debugMode + 1) % 5;
                return; // DebugMode setter already triggers re-render
            default:
                return;
        }

        RequestNextFrameRendering();
        e.Handled = true;
    }

    /// <summary>
    /// Convert a screen-space (pixel) delta into a world-space delta in the
    /// plane facing the camera, using the current rotation state. Pan drags
    /// move the model along these in-plane axes rather than world XY so the
    /// motion feels right at any view angle.
    /// </summary>
    private Vector3 ScreenDeltaToWorld(float rightAmount, float upAmount)
    {
        // The model rotation is applied to vertices, so the visible
        // "right" / "up" directions in world space are the unrotated
        // camera axes. Camera looks along +Y (LookAt from -Y eye to
        // origin with Z-up), so "right" = -X and "up" = +Z when no
        // rotation has been applied. We want pan to move the model
        // under the mouse regardless of the model rotation — so we
        // translate the camera target along world-aligned axes only.
        // This matches common DCC tool behaviour.
        return new Vector3(rightAmount, 0, upAmount);
    }

    /// <summary>
    /// Approximate the world-space point under the cursor at the depth of
    /// the camera target, so scroll-wheel zoom can pivot there.
    /// </summary>
    private Vector3 UnprojectToTargetPlane(Point screenPos)
    {
        if (_lastViewportWidth <= 0 || _lastViewportHeight <= 0)
            return _viewController.CameraTarget;

        // NDC coordinates (-1..1), Y inverted because screen Y is top-down.
        float ndcX = (float)(screenPos.X / _lastViewportWidth * 2.0 - 1.0);
        float ndcY = (float)(1.0 - screenPos.Y / _lastViewportHeight * 2.0);

        // At the target's depth, one NDC unit vertically = cameraDistance * halfFovTan world units.
        float fovRadians = MathF.PI / 6f;
        float halfFovTan = MathF.Tan(fovRadians * 0.5f);
        float aspect = (float)_lastViewportWidth / _lastViewportHeight;
        float worldY = ndcY * _lastCameraDistance * halfFovTan;
        float worldX = ndcX * _lastCameraDistance * halfFovTan * aspect;

        // The camera's right/up axes at the target depth. See
        // ScreenDeltaToWorld — right = -X, up = +Z under our LookAt.
        var target = _viewController.CameraTarget;
        return target + new Vector3(-worldX, 0, worldY);
    }
}

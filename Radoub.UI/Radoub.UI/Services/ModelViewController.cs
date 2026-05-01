// Pure math controller for 3D model view: camera, rotation, transforms.
// Extracted from ModelPreviewGLControl to improve testability and separation of concerns.

using System;
using System.Collections.Generic;
using System.Numerics;
using Radoub.Formats.Mdl;

namespace Radoub.UI.Services;

/// <summary>
/// Preset camera orientations (#2124).
/// Values map to RotationY (model-around-Z) + RotationX tilt.
/// </summary>
public enum ViewPreset
{
    Front,
    Back,
    Side,       // quarter turn — "left" side in viewer terms
    SideRight,  // opposite side
    Top,
}

/// <summary>
/// Manages camera state (rotation, zoom, target) and provides
/// world-space transform calculations for 3D model rendering.
/// Pure math — no GL dependency.
/// </summary>
public class ModelViewController
{
    private float _rotationY = MathF.PI; // Default 180° so model faces camera
    private float _rotationX;
    private float _zoom = 1.0f;
    private Vector3 _cameraTarget = Vector3.Zero;
    private float _modelRadius = 1.0f;
    private bool _hasVertexBounds;

    public float RotationY
    {
        get => _rotationY;
        set => _rotationY = value;
    }

    public float RotationX
    {
        get => _rotationX;
        set => _rotationX = value;
    }

    public float Zoom
    {
        get => _zoom;
        set => _zoom = Math.Clamp(value, 0.1f, 10f);
    }

    public Vector3 CameraTarget => _cameraTarget;
    public float ModelRadius => _modelRadius;
    public bool HasVertexBounds => _hasVertexBounds;

    /// <summary>
    /// Rotate the model incrementally.
    /// </summary>
    public void Rotate(float deltaY, float deltaX = 0)
    {
        _rotationY += deltaY;
        _rotationX += deltaX;
    }

    /// <summary>
    /// Translate the camera target in world space. Used by panning so
    /// the model appears to slide under the camera.
    /// </summary>
    public void Pan(Vector3 worldDelta)
    {
        _cameraTarget += worldDelta;
    }

    /// <summary>
    /// Multiplicatively change zoom while keeping a world-space pivot
    /// point anchored. When the user zooms with the scroll wheel at a
    /// cursor position, we want that spot to stay roughly under the
    /// cursor rather than drifting off screen (#2124).
    /// </summary>
    public void ZoomAtPoint(float factor, Vector3 worldPivot)
    {
        float before = _zoom;
        Zoom = _zoom * factor;
        float applied = _zoom / before;

        // If zoom was clamped (no effective change), suppress the pivot
        // pull so repeated scrolls at the limit don't drift the target.
        if (MathF.Abs(applied - 1f) < 1e-5f)
            return;

        // The camera views the scene at distance ∝ 1/zoom from the
        // target. Scaling zoom by `applied` shrinks that distance by
        // 1/applied. To keep the pivot stable on screen, move the
        // target a matching fraction of (pivot - target).
        float t = 1f - 1f / applied;
        _cameraTarget += (worldPivot - _cameraTarget) * t;
    }

    /// <summary>
    /// Snap the camera to a preset orientation. Clears pan and zoom so
    /// the chosen view fills the frame the same way ResetView does.
    /// </summary>
    public void SetViewPreset(ViewPreset preset)
    {
        switch (preset)
        {
            case ViewPreset.Front:
                _rotationY = MathF.PI;
                _rotationX = 0f;
                break;
            case ViewPreset.Back:
                _rotationY = 0f;
                _rotationX = 0f;
                break;
            case ViewPreset.Side:
                _rotationY = MathF.PI / 2f;
                _rotationX = 0f;
                break;
            case ViewPreset.SideRight:
                _rotationY = 3f * MathF.PI / 2f;
                _rotationX = 0f;
                break;
            case ViewPreset.Top:
                _rotationY = MathF.PI;
                _rotationX = MathF.PI / 2f;
                break;
        }
        _zoom = 1.0f;
        _cameraTarget = Vector3.Zero;
    }

    /// <summary>
    /// Reset the view to default (facing front).
    /// </summary>
    public void ResetView()
    {
        _rotationY = MathF.PI;
        _rotationX = 0;
        _zoom = 1.0f;
        // Always clear user-applied pan on reset so the model recenters.
        _cameraTarget = Vector3.Zero;
        // Use vertex-computed bounds if available, otherwise safe defaults.
        // Don't call CenterCamera() — model stored bounds are unreliable
        // (they include the full skeleton hierarchy, not just rendered mesh).
        if (!_hasVertexBounds)
        {
            _modelRadius = 1.0f;
        }
    }

    /// <summary>
    /// Reset camera framing defaults. Called when a model loads/unloads so
    /// vertex-computed bounds can be rebuilt. Zoom, rotation, and user pan
    /// are preserved — switching equipment or heads should not snap the
    /// camera back to the default view (#2124).
    /// </summary>
    public void CenterCamera()
    {
        _modelRadius = 1.0f;
        _hasVertexBounds = false;
    }

    /// <summary>
    /// Update the camera bounds from computed vertex data.
    /// Called by the GL control after mesh buffer assembly. Preserves the
    /// user's pan — only radius/bounds-flag are refreshed.
    /// </summary>
    public void UpdateBounds(float modelRadius, bool hasVertexBounds)
    {
        _modelRadius = modelRadius;
        _hasVertexBounds = hasVertexBounds;
    }

    /// <summary>
    /// Calculate the world transform matrix for a node by walking up the parent chain.
    /// Combines position, rotation (quaternion), and scale from each ancestor.
    /// Transform order for each node: Scale first, then Rotate, then Translate (SRT).
    /// </summary>
    public static Matrix4x4 GetWorldTransform(MdlNode? node)
    {
        return GetWorldTransform(node, null);
    }

    /// <summary>
    /// Variant that consults an animation pose (node name → sampled transform).
    /// Used by the appearance preview to play animation stances (#2124).
    /// When a node's name is in <paramref name="pose"/>, the sampled values
    /// replace the bind-pose Position/Orientation/Scale for that link of the
    /// hierarchy. Other nodes use their static values.
    /// </summary>
    public static Matrix4x4 GetWorldTransform(MdlNode? node, IReadOnlyDictionary<string, NodePose>? pose)
    {
        var worldTransform = Matrix4x4.Identity;
        var current = node;

        while (current != null)
        {
            Vector3 pos = current.Position;
            Quaternion orient = current.Orientation;
            float scl = current.Scale;

            if (pose != null && !string.IsNullOrEmpty(current.Name)
                && pose.TryGetValue(current.Name, out var p))
            {
                if (p.HasPosition) pos = p.Position;
                if (p.HasOrientation) orient = p.Orientation;
                if (p.HasScale) scl = p.Scale;
            }

            var scale = Matrix4x4.CreateScale(scl);
            var rotation = Matrix4x4.CreateFromQuaternion(orient);
            var translation = Matrix4x4.CreateTranslation(pos);

            var localTransform = scale * rotation * translation;
            worldTransform = worldTransform * localTransform;

            current = current.Parent;
        }

        return worldTransform;
    }

    /// <summary>
    /// Sampled pose for a single node at a specific animation time.
    /// Flags indicate which channels were actually animated (vs inheriting
    /// the bind pose).
    /// </summary>
    public readonly record struct NodePose(
        bool HasPosition, Vector3 Position,
        bool HasOrientation, Quaternion Orientation,
        bool HasScale, float Scale);

    /// <summary>
    /// Transform a position vector by a matrix.
    /// </summary>
    public static Vector3 TransformPosition(Vector3 position, Matrix4x4 matrix)
    {
        return Vector3.Transform(position, matrix);
    }

    /// <summary>
    /// Transform a normal vector by a matrix (ignores translation, handles non-uniform scale).
    /// </summary>
    public static Vector3 TransformNormal(Vector3 normal, Matrix4x4 matrix)
    {
        var transformed = Vector3.TransformNormal(normal, matrix);
        return Vector3.Normalize(transformed);
    }

    /// <summary>
    /// Apply bone-weighted skinning transform to a vertex position.
    /// The on-disk data contains INVERSE bind-pose transforms (world→bone space).
    /// To display in world space, we invert: v_world = Q_fwd * (v_local - T_inv)
    /// where Q_fwd = conjugate(Q_inv).
    /// </summary>
    public static Vector3 ApplySkinTransform(Vector3 vertex, int vertexIndex, MdlSkinNode skin)
    {
        var bw = skin.BoneWeights[vertexIndex];
        var result = Vector3.Zero;

        void Accumulate(int boneIndex, float weight)
        {
            if (weight <= 0 || boneIndex < 0) return;
            if (boneIndex >= skin.BoneQuaternions.Length || boneIndex >= skin.BoneTranslations.Length) return;

            // NWN runtime skinning formula: v_world = Q_stored * v_local + T_stored
            var q = skin.BoneQuaternions[boneIndex];
            var t = skin.BoneTranslations[boneIndex];
            var rotated = Vector3.Transform(vertex, q);
            result += weight * (rotated + t);
        }

        Accumulate(bw.Bone0, bw.Weight0);
        Accumulate(bw.Bone1, bw.Weight1);
        Accumulate(bw.Bone2, bw.Weight2);
        Accumulate(bw.Bone3, bw.Weight3);

        // Guard against NaN from invalid bone data or degenerate transforms
        if (float.IsNaN(result.X) || float.IsNaN(result.Y) || float.IsNaN(result.Z))
            return vertex; // Fall back to raw vertex

        return result;
    }

    /// <summary>
    /// Apply bone-weighted skinning rotation to a normal vector.
    /// Only applies the rotation component (no translation for normals).
    /// </summary>
    public static Vector3 ApplySkinNormalTransform(Vector3 normal, int vertexIndex, MdlSkinNode skin)
    {
        var bw = skin.BoneWeights[vertexIndex];
        var result = Vector3.Zero;

        void Accumulate(int boneIndex, float weight)
        {
            if (weight <= 0 || boneIndex < 0) return;
            if (boneIndex >= skin.BoneQuaternions.Length) return;

            var q = skin.BoneQuaternions[boneIndex];
            result += weight * Vector3.Transform(normal, q);
        }

        Accumulate(bw.Bone0, bw.Weight0);
        Accumulate(bw.Bone1, bw.Weight1);
        Accumulate(bw.Bone2, bw.Weight2);
        Accumulate(bw.Bone3, bw.Weight3);

        var len = result.Length();
        return len > 0.0001f ? result / len : Vector3.UnitZ;
    }
}

// Pure math controller for 3D model view: camera, rotation, transforms.
// Extracted from ModelPreviewGLControl to improve testability and separation of concerns.

using System;
using System.Numerics;
using Radoub.Formats.Mdl;

namespace Quartermaster.Controls;

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
    /// Reset the view to default (facing front).
    /// </summary>
    public void ResetView()
    {
        _rotationY = MathF.PI;
        _rotationX = 0;
        _zoom = 1.0f;
        // Use vertex-computed bounds if available, otherwise safe defaults.
        // Don't call CenterCamera() — model stored bounds are unreliable
        // (they include the full skeleton hierarchy, not just rendered mesh).
        if (!_hasVertexBounds)
        {
            _cameraTarget = Vector3.Zero;
            _modelRadius = 1.0f;
        }
    }

    /// <summary>
    /// Reset camera to defaults. Actual center and radius are computed from
    /// rendered vertices in UpdateMeshBuffers(). The model's stored
    /// BoundingMin/Max encompasses the full skeleton hierarchy and is
    /// much larger than the visible mesh.
    /// </summary>
    public void CenterCamera()
    {
        _cameraTarget = Vector3.Zero;
        _modelRadius = 1.0f;
        _hasVertexBounds = false;
    }

    /// <summary>
    /// Update the camera bounds from computed vertex data.
    /// Called by the GL control after mesh buffer assembly.
    /// </summary>
    public void UpdateBounds(float modelRadius, bool hasVertexBounds)
    {
        _modelRadius = modelRadius;
        _hasVertexBounds = hasVertexBounds;
        _cameraTarget = Vector3.Zero;
    }

    /// <summary>
    /// Calculate the world transform matrix for a node by walking up the parent chain.
    /// Combines position, rotation (quaternion), and scale from each ancestor.
    /// Transform order for each node: Scale first, then Rotate, then Translate (SRT).
    /// </summary>
    public static Matrix4x4 GetWorldTransform(MdlNode? node)
    {
        // System.Numerics uses row-major convention where Vector3.Transform(v, M) = v * M
        // For hierarchical transforms: v_world = v_local * NodeLocal * ParentLocal * ... * RootLocal
        // So we need: worldTransform = NodeLocal * ParentLocal * ... * RootLocal
        // We walk leaf-to-root, accumulating: world = local * world
        var worldTransform = Matrix4x4.Identity;
        var current = node;

        while (current != null)
        {
            var scale = Matrix4x4.CreateScale(current.Scale);
            var rotation = Matrix4x4.CreateFromQuaternion(current.Orientation);
            var translation = Matrix4x4.CreateTranslation(current.Position);

            // Row-major local transform: S * R * T
            var localTransform = scale * rotation * translation;

            // Accumulate: node * parent * grandparent * ... * root
            worldTransform = worldTransform * localTransform;

            current = current.Parent;
        }

        return worldTransform;
    }

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

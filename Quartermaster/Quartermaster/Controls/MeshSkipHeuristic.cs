using System;
using System.Collections.Generic;

namespace Quartermaster.Controls;

/// <summary>
/// Texture-based heuristic for skipping tiny trimeshes in skin models (#2057).
/// Bone visualization trimeshes share the same texture as the skin they overlap.
/// Real geometry parts (tails, manes, fangs) have distinct textures and are kept.
/// </summary>
public static class MeshSkipHeuristic
{
    private const int TinyTrimeshThreshold = 30;

    /// <summary>
    /// Determines whether a trimesh should be skipped during rendering.
    /// </summary>
    /// <param name="hasSkins">Whether the model contains any skin meshes.</param>
    /// <param name="isSkinMesh">Whether this specific mesh is a skin mesh.</param>
    /// <param name="vertexCount">Number of vertices in the mesh.</param>
    /// <param name="meshBitmap">The mesh's bitmap name.</param>
    /// <param name="skinBitmaps">Set of bitmap names used by skin meshes (case-insensitive).</param>
    /// <returns>True if the mesh should be skipped (bone visualization overlay).</returns>
    public static bool ShouldSkipTrimesh(
        bool hasSkins, bool isSkinMesh, int vertexCount,
        string? meshBitmap, HashSet<string> skinBitmaps)
    {
        if (!hasSkins || isSkinMesh || vertexCount >= TinyTrimeshThreshold)
            return false;

        bool hasUniqueBitmap = !string.IsNullOrEmpty(meshBitmap)
            && !meshBitmap.Equals("null", StringComparison.OrdinalIgnoreCase)
            && !skinBitmaps.Contains(meshBitmap);

        return !hasUniqueBitmap;
    }
}

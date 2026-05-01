using Radoub.UI.Controls;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for the texture-based mesh skip heuristic (#2057).
/// Tiny trimeshes (&lt;30 verts) in skin models should only be skipped
/// when they share a bitmap with a skin mesh (bone visualization overlay).
/// Trimeshes with unique bitmaps are real geometry (tails, manes, fangs).
/// </summary>
public class MeshSkipHeuristicTests
{
    [Fact]
    public void SharedBitmap_TinyTrimesh_IsSkipped()
    {
        // Bone visualization trimesh shares the skin's texture
        var skinBitmaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "c_dragon_red" };

        var result = MeshSkipHeuristic.ShouldSkipTrimesh(
            hasSkins: true, isSkinMesh: false, vertexCount: 12,
            meshBitmap: "c_dragon_red", skinBitmaps: skinBitmaps);

        Assert.True(result);
    }

    [Fact]
    public void UniqueBitmap_TinyTrimesh_IsKept()
    {
        // Tail/mane/fang trimesh has its own distinct texture — real geometry
        var skinBitmaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "c_dragon_red" };

        var result = MeshSkipHeuristic.ShouldSkipTrimesh(
            hasSkins: true, isSkinMesh: false, vertexCount: 18,
            meshBitmap: "c_dragon_tail", skinBitmaps: skinBitmaps);

        Assert.False(result);
    }

    [Fact]
    public void EmptyBitmap_TinyTrimesh_IsSkipped()
    {
        // Untextured <30 vert trimesh in skin model — likely bone viz
        var skinBitmaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "c_dragon_red" };

        var result = MeshSkipHeuristic.ShouldSkipTrimesh(
            hasSkins: true, isSkinMesh: false, vertexCount: 8,
            meshBitmap: "", skinBitmaps: skinBitmaps);

        Assert.True(result);
    }

    [Fact]
    public void NullBitmap_TinyTrimesh_IsSkipped()
    {
        var skinBitmaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "c_dragon_red" };

        var result = MeshSkipHeuristic.ShouldSkipTrimesh(
            hasSkins: true, isSkinMesh: false, vertexCount: 8,
            meshBitmap: "null", skinBitmaps: skinBitmaps);

        Assert.True(result);
    }

    [Fact]
    public void NoSkins_TinyTrimesh_IsKept()
    {
        // No skin meshes in model — all trimeshes kept regardless of size
        var skinBitmaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = MeshSkipHeuristic.ShouldSkipTrimesh(
            hasSkins: false, isSkinMesh: false, vertexCount: 12,
            meshBitmap: "some_texture", skinBitmaps: skinBitmaps);

        Assert.False(result);
    }

    [Fact]
    public void LargeTrimesh_SharedBitmap_IsKept()
    {
        // Trimesh >= 30 verts is kept even if it shares a skin bitmap
        var skinBitmaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "c_dragon_red" };

        var result = MeshSkipHeuristic.ShouldSkipTrimesh(
            hasSkins: true, isSkinMesh: false, vertexCount: 30,
            meshBitmap: "c_dragon_red", skinBitmaps: skinBitmaps);

        Assert.False(result);
    }

    [Fact]
    public void SkinMesh_IsNeverSkipped()
    {
        // Skin meshes are never filtered by this heuristic
        var skinBitmaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "c_dragon_red" };

        var result = MeshSkipHeuristic.ShouldSkipTrimesh(
            hasSkins: true, isSkinMesh: true, vertexCount: 10,
            meshBitmap: "c_dragon_red", skinBitmaps: skinBitmaps);

        Assert.False(result);
    }

    [Fact]
    public void BitmapComparison_IsCaseInsensitive()
    {
        // Bitmap matching should be case-insensitive
        var skinBitmaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "C_Dragon_Red" };

        var result = MeshSkipHeuristic.ShouldSkipTrimesh(
            hasSkins: true, isSkinMesh: false, vertexCount: 12,
            meshBitmap: "c_dragon_red", skinBitmaps: skinBitmaps);

        Assert.True(result);
    }
}

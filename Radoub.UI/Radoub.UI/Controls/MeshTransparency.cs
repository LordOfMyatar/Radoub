using System.Numerics;

namespace Radoub.UI.Controls;

/// <summary>
/// How a texture's alpha channel reads: no transparency, a hard cutout mask, or a soft gradient.
/// </summary>
public enum AlphaProfile
{
    /// <summary>Every texel is fully opaque (alpha 255). The alpha channel carries no transparency.</summary>
    Opaque,

    /// <summary>Alpha is effectively a 0/255 mask — a cutout silhouette (fur, foliage, mane).</summary>
    Binary,

    /// <summary>Alpha has a real gradient — a soft blend (ghost, glass, wraith, elemental).</summary>
    Graded,
}

/// <summary>
/// How a mesh is drawn with respect to transparency.
/// </summary>
public enum MaterialMode
{
    /// <summary>Solid; depth write on, no blend, no alpha test.</summary>
    Opaque,

    /// <summary>Alpha-test silhouette via <c>discard</c>; depth write on (effectively opaque per-texel).</summary>
    Cutout,

    /// <summary>Alpha-blended; depth write off, drawn back-to-front after opaque/cutout geometry.</summary>
    Transparent,
}

/// <summary>
/// Per-mesh transparency classification for the model preview (#2540). Mirrors the authoritative
/// rollnw renderer (<c>classify_material</c>): <see cref="MaterialMode"/> is derived from the mesh
/// <c>Alpha</c> controller, the MDL <c>TransparencyHint</c>, and the texture's <see cref="AlphaProfile"/>.
///
/// <para>Hint-less meshes (#2540): the real Aurora engine blends a mesh with no TransparencyHint
/// iff its texture has a meaningful alpha channel (xoreos <c>modelnode.cpp:505</c>, gated by the
/// <c>alphaMean != 1.0</c> check at <c>:473</c> — behavioral reference only, GPLv3, not copied).
/// A texture FORMAT carrying alpha (DXT5 / 32-bit TGA) is necessary but not sufficient: NWN:EE
/// <c>_d</c> skins pack a specular/gloss map in alpha that never goes transparent (#2615). So
/// <see cref="AnalyzeAlphaProfile"/> requires actual near-zero texels before reporting a non-Opaque
/// profile — a high gradient with no transparent texels (<c>boy_head</c>) stays Opaque and keeps
/// depth writes, while genuinely transparent skins (zodiac creatures #2435, reaching alpha 0) and
/// hard fur masks (mane #2507) classify Graded / Binary. DXT1 / 24-bit bodies decode fully opaque.</para>
/// </summary>
public static class MeshTransparency
{
    /// <summary>Mesh Alpha at/above this is treated as fully opaque (controller fade ignored).</summary>
    private const float OpaqueAlphaCutoff = 0.999f;

    /// <summary>Alpha values strictly inside [SoftMin, SoftMax] count as "soft" (gradient) texels.</summary>
    private const byte SoftMin = 11;
    private const byte SoftMax = 244;

    /// <summary>
    /// Fraction of soft texels above which the channel reads as a real gradient rather than a
    /// hard mask. Below it, rare soft pixels are dithering/AA on an otherwise binary cutout.
    /// </summary>
    private const double GradedSoftFraction = 0.05;

    /// <summary>Alpha at/below this counts as effectively transparent (a true see-through texel).</summary>
    private const byte NearZeroAlpha = 16;

    /// <summary>
    /// Minimum count of near-zero texels for the alpha channel to be transparency at all (#2615).
    /// NWN:EE <c>_d</c> skin textures pack a specular/gloss map in alpha — a high-valued gradient
    /// that never approaches 0 (e.g. <c>boy_head</c>: minAlpha=42, zero near-zero texels). Such a
    /// channel is a packed data channel, not transparency; without this gate it mis-read as Graded
    /// and the head drew with depth-write off (see-through). A single transparent texel is enough
    /// (a genuine cutout/blend always has a transparent region), so this is a count, not a fraction:
    /// a fraction would wrongly gate out a small-but-real cutout hole in a large texture.
    /// </summary>
    private const int MinTransparentTexels = 1;

    /// <summary>
    /// Classify a texture's alpha channel from its RGBA bytes. <paramref name="rgba"/> is tightly
    /// packed (4 bytes/pixel, alpha last). Returns <see cref="AlphaProfile.Opaque"/> for empty/null
    /// data. Width/height are accepted for caller convenience but only the length is used.
    /// </summary>
    public static AlphaProfile AnalyzeAlphaProfile(byte[] rgba, int width, int height)
    {
        if (rgba == null || rgba.Length < 4)
            return AlphaProfile.Opaque;

        int pixels = rgba.Length / 4;
        int softCount = 0;
        int nearZeroCount = 0;

        for (int i = 0; i < pixels; i++)
        {
            byte a = rgba[i * 4 + 3];
            if (a <= NearZeroAlpha) nearZeroCount++;
            if (a > SoftMin && a < SoftMax) softCount++;
        }

        // #2615: transparency requires texels that actually go (near) transparent. A channel with
        // no near-zero texels is a packed spec/gloss map (NWN:EE _d skins), not a transparency
        // mask — treat it as Opaque so the mesh keeps depth writes and occludes correctly. This
        // mirrors xoreos's "alphaMean ~= 1.0 => not transparent" gate (modelnode.cpp:473), using
        // the decoded pixels our production decoder yields.
        if (nearZeroCount < MinTransparentTexels)
            return AlphaProfile.Opaque;

        // Has real transparent texels: soft-band fraction decides hard mask (Binary) vs gradient.
        return (double)softCount / pixels >= GradedSoftFraction
            ? AlphaProfile.Graded
            : AlphaProfile.Binary;
    }

    /// <summary>
    /// Map a mesh's transparency inputs to a <see cref="MaterialMode"/>.
    /// </summary>
    /// <param name="alpha">Mesh Alpha controller value (1.0 = fully opaque).</param>
    /// <param name="transparencyHint">MDL TransparencyHint (0 = no transparency interpretation).</param>
    /// <param name="profile">Alpha profile of the mesh's diffuse texture.</param>
    /// <param name="isSkin">
    /// True when the mesh is a deformable body <c>SkinNode</c> (#2588). A SkinNode is never a cutout
    /// card: it is the solid creature body. When it shares its DDS atlas with a cutout fur/mane mesh
    /// (CEP dire tiger — body skin + mane both use <c>N_Tiger_LaoHu02_D</c>), the texture reads
    /// Binary, but the body's UVs map to the SOLID skin region, not the mane's cutout silhouette.
    /// Alpha-testing the body then discards fragments where its UVs land on the mane's low-alpha
    /// region, carving black patches on the body/crown. So a Binary-profile skin classifies Opaque
    /// (depth writes on, no discard) instead of Cutout; the separate mane/dangly trimeshes still
    /// classify Cutout and carve correctly. A genuinely graded skin (none in the test set) still
    /// blends. The never-cutout rule holds regardless of TransparencyHint.
    /// </param>
    public static MaterialMode ClassifyMesh(float alpha, int transparencyHint, AlphaProfile profile,
        bool isSkin = false, bool isMirrored = false)
    {
        // Controller-driven fade wins outright — a mesh told to be semi-transparent always blends.
        if (alpha < OpaqueAlphaCutoff)
            return MaterialMode.Transparent;

        // #2588: a deformable body SkinNode is never a cutout card. Its Binary profile means the
        // SHARED atlas carries a cutout mask in the mane region, but the body's own UVs map to solid
        // skin — alpha-testing it would punch holes in the body. Route Binary->Opaque (keep depth
        // writes, no discard); leave a genuinely graded skin to blend.
        if (isSkin)
            return profile == AlphaProfile.Graded
                ? MaterialMode.Transparent
                : MaterialMode.Opaque;

        // No alpha channel -> opaque, regardless of hint. A DXT1/24-bit body decodes Opaque (incl.
        // the #2507 _d-spec trap), so it never blends or carves.
        if (profile == AlphaProfile.Opaque)
            return MaterialMode.Opaque;

        // Alpha-bearing mesh. The authoritative Aurora engine (xoreos modelnode.cpp:505/779-810 —
        // behavioral reference only, GPLv3, not copied) has only TWO modes for NWN: Opaque and
        // Transparent (blend). isTransparent = hasAlpha; renderGeometryNormal just alpha-blends,
        // with no alpha-test for ordinary meshes. So the DEFAULT for any alpha mesh is Transparent.
        //
        // The one exception (#2588, from nwn_mdl_webviewer scene_build.js — MIT): handbuilt
        // double-sided meshes (fences, foliage, fur/mane cards) duplicate every face with inverted
        // normals to fake two-sidedness, so ~half their normals point the opposite way on some axis
        // (HasMirroredNormals). Such a mesh cannot be depth-sorted as a unit in a blend pass — the
        // back-face quads sort over the front-face quads and show the clear colour through. The
        // viewer's fix: keep these in the OPAQUE queue and alpha-TEST (hard cutout, depth write on,
        // order-independent) instead of blending. The discriminator is the GEOMETRY (mirrored
        // normals) plus a hard 0/1 mask (Binary) — NOT the texture histogram alone. A genuinely
        // graded alpha (zodiac/celestial #2435) is never a hard cutout: it always blends.
        if (profile == AlphaProfile.Binary && isMirrored)
            return MaterialMode.Cutout;

        return MaterialMode.Transparent;
    }

    /// <summary>
    /// True when a mesh is "handbuilt double-sided" (#2588): NWN modellers duplicate every face
    /// with inverted normals/winding to fake two-sidedness, so roughly half the vertex normals
    /// point the opposite way along at least one axis. Mirrors nwn_mdl_webviewer's
    /// <c>hasMirroredNormals</c> (scene_build.js, MIT — behavioral reference). Used to decide that a
    /// Binary-alpha mesh is a true hard cutout (fence/foliage/fur card) rather than ordinary alpha
    /// geometry that should blend. A 40–60% positive/negative split on any well-used axis counts.
    /// </summary>
    public static bool HasMirroredNormals(Vector3[]? normals)
    {
        if (normals == null || normals.Length < 4)
            return false;

        int posX = 0, negX = 0, posY = 0, negY = 0, posZ = 0, negZ = 0;
        foreach (var n in normals)
        {
            if (n.X > 0.01f) posX++; else if (n.X < -0.01f) negX++;
            if (n.Y > 0.01f) posY++; else if (n.Y < -0.01f) negY++;
            if (n.Z > 0.01f) posZ++; else if (n.Z < -0.01f) negZ++;
        }

        int total = normals.Length;
        bool Mirrored(int p, int q)
        {
            int used = p + q;
            if (used < total * 0.5) return false;          // axis barely used -> skip
            return (double)Math.Min(p, q) / used >= 0.40;  // 40-60% split -> mirrored
        }

        return Mirrored(posX, negX) || Mirrored(posY, negY) || Mirrored(posZ, negZ);
    }

    /// <summary>
    /// Order transparent meshes for correct back-to-front blending (#2540), mirroring rollnw:
    /// <c>TransparencyHint</c> ascending first (an explicit author-set draw layer), then farthest
    /// from the camera first as the tiebreak. <paramref name="items"/> carries each mesh's hint
    /// and a view-space depth (larger = farther). Returns indices into <paramref name="items"/>
    /// in the order they should be drawn. Stable for equal keys (preserves parse order).
    /// </summary>
    public static int[] SortBackToFront(IReadOnlyList<(int hint, float depth)> items)
    {
        var order = new int[items.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;

        // OrderBy is stable; compose hint (asc) then depth (desc = farthest first).
        return order
            .OrderBy(i => items[i].hint)
            .ThenByDescending(i => items[i].depth)
            .ToArray();
    }
}

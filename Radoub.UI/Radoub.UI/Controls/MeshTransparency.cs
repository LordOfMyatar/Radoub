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
/// <para>The decisive rule (the #2507 trap): <c>TransparencyHint == 0</c> never yields Cutout or
/// Transparent. NWN:EE <c>_d</c> diffuse maps overload the alpha channel as a PBR/spec value, so an
/// opaque body's alpha must not be consulted — a blanket <c>discard</c> punches holes in it.</para>
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
        bool anyTransparent = false;

        for (int i = 0; i < pixels; i++)
        {
            byte a = rgba[i * 4 + 3];
            if (a < 255) anyTransparent = true;
            if (a > SoftMin && a < SoftMax) softCount++;
        }

        if (!anyTransparent)
            return AlphaProfile.Opaque;

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
    public static MaterialMode ClassifyMesh(float alpha, int transparencyHint, AlphaProfile profile)
    {
        // Controller-driven fade wins outright — a mesh told to be semi-transparent always blends.
        if (alpha < OpaqueAlphaCutoff)
            return MaterialMode.Transparent;

        // No hint => never consult the texture alpha (it may be an overloaded _d/spec channel).
        if (transparencyHint <= 0)
            return MaterialMode.Opaque;

        // Hint set: the alpha channel is real; its profile decides cutout vs blend.
        return profile switch
        {
            AlphaProfile.Binary => MaterialMode.Cutout,
            AlphaProfile.Graded => MaterialMode.Transparent,
            _ => MaterialMode.Opaque,
        };
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

// Tests for MeshTransparency (#2540, Sprint 1).
//
// Two pure functions drive the model-preview transparency pipeline:
//   - AnalyzeAlphaProfile: classify a texture's alpha channel as Opaque / Binary / Graded.
//   - ClassifyMesh: map (mesh Alpha, TransparencyHint, alpha profile) to a MaterialMode
//     (Opaque / Cutout / Transparent), following the authoritative rollnw renderer.
//
// The key invariant (the #2507 trap): TransparencyHint == 0 must NEVER produce Cutout or
// Transparent — opaque _d PBR bodies overload the alpha channel as a spec value, so their
// alpha is never consulted.

using Radoub.UI.Controls;

namespace Radoub.UI.Tests;

public class MeshTransparencyTests
{
    // Helper: build an RGBA byte array from a list of alpha values (RGB filled with 255).
    private static byte[] Rgba(params byte[] alphas)
    {
        var data = new byte[alphas.Length * 4];
        for (int i = 0; i < alphas.Length; i++)
        {
            data[i * 4 + 0] = 255;
            data[i * 4 + 1] = 255;
            data[i * 4 + 2] = 255;
            data[i * 4 + 3] = alphas[i];
        }
        return data;
    }

    // ---- AnalyzeAlphaProfile ----

    [Fact]
    public void AllOpaque_IsOpaque()
    {
        var rgba = Rgba(255, 255, 255, 255);
        Assert.Equal(AlphaProfile.Opaque, MeshTransparency.AnalyzeAlphaProfile(rgba, 2, 2));
    }

    [Fact]
    public void HardEdges_NoSoftBand_IsBinary()
    {
        // Pure 0/255 mask — a cutout silhouette (fur card, foliage).
        var rgba = Rgba(0, 255, 0, 255, 255, 0, 255, 0);
        Assert.Equal(AlphaProfile.Binary, MeshTransparency.AnalyzeAlphaProfile(rgba, 4, 2));
    }

    [Fact]
    public void MostlyOpaqueWithRareSoftEdge_IsBinary()
    {
        // A hard cutout that reaches 0 with a thin anti-aliased edge: 1 soft pixel out of 100
        // = 1% < 5% threshold -> binary mask, not graded. (#2615: requires near-zero texels to
        // be transparency at all; here index 1 is a true-0 cutout texel.)
        var alphas = new byte[100];
        for (int i = 0; i < 100; i++) alphas[i] = (byte)(i == 0 ? 128 : (i == 1 ? 0 : 255));
        Assert.Equal(AlphaProfile.Binary, MeshTransparency.AnalyzeAlphaProfile(Rgba(alphas), 10, 10));
    }

    [Fact]
    public void ManySoftPixelsReachingZero_IsGraded()
    {
        // 50% of pixels in the soft band AND the channel reaches 0 -> a real gradient (ghost,
        // glass). (#2615: a soft gradient is only transparency if it actually goes transparent.)
        var alphas = new byte[100];
        for (int i = 0; i < 100; i++) alphas[i] = (byte)(i < 50 ? (i % 10 == 0 ? 0 : 128) : 255);
        Assert.Equal(AlphaProfile.Graded, MeshTransparency.AnalyzeAlphaProfile(Rgba(alphas), 10, 10));
    }

    [Fact]
    public void EmptyOrNullData_IsOpaque()
    {
        Assert.Equal(AlphaProfile.Opaque, MeshTransparency.AnalyzeAlphaProfile(System.Array.Empty<byte>(), 0, 0));
        Assert.Equal(AlphaProfile.Opaque, MeshTransparency.AnalyzeAlphaProfile(null!, 0, 0));
    }

    [Fact]
    public void HighGradientNeverNearZero_IsOpaque_SpecPackedInAlpha()
    {
        // #2615: NWN:EE _d skin textures pack a SPECULAR/gloss map in the alpha channel — a
        // high-valued gradient that never approaches 0 (no actual transparency). c_ykid_m's
        // 'boy_head' is exactly this: minA=42, ZERO texels at alpha 0, ~25% in the soft band.
        // The old "any texel < 255 => transparent" gate mis-read this as Graded -> Transparent
        // -> the head drew with depth-write off -> see-through face. A channel with no near-zero
        // texels is a packed data channel, not transparency: classify Opaque (mirrors xoreos's
        // alphaMean~=1 => not transparent gate, modelnode.cpp:473).
        var alphas = new byte[100];
        for (int i = 0; i < 100; i++) alphas[i] = (byte)(i < 25 ? 128 : 255); // 25% soft, min 128, none near 0
        Assert.Equal(AlphaProfile.Opaque, MeshTransparency.AnalyzeAlphaProfile(Rgba(alphas), 10, 10));
    }

    [Fact]
    public void GradientReachingZero_IsGraded_RealTransparency()
    {
        // #2615 must-not-regress: a genuinely transparent body (zodiac rat c_zod_rat, #2435) has
        // texels at/near alpha 0 (real punch-through/blend). It must still classify non-Opaque.
        var alphas = new byte[100];
        for (int i = 0; i < 100; i++) alphas[i] = (byte)(i < 50 ? (i % 25 == 0 ? 0 : 128) : 255); // soft band + true-zero texels
        Assert.Equal(AlphaProfile.Graded, MeshTransparency.AnalyzeAlphaProfile(Rgba(alphas), 10, 10));
    }

    [Fact]
    public void HardCutoutReachingZero_IsBinary_FurMask()
    {
        // #2615 must-not-regress: a 0/255 fur/mane cutout card (dire-tiger mane, #2507) reaches 0
        // and stays a hard mask -> Binary (alpha-test cutout), not Opaque.
        var rgba = Rgba(0, 255, 0, 255, 255, 0, 255, 0);
        Assert.Equal(AlphaProfile.Binary, MeshTransparency.AnalyzeAlphaProfile(rgba, 4, 2));
    }

    // ---- ClassifyMesh ----

    [Fact]
    public void MeshAlphaBelowOne_IsTransparent_RegardlessOfHint()
    {
        // Controller-driven fade takes priority over everything.
        Assert.Equal(MaterialMode.Transparent,
            MeshTransparency.ClassifyMesh(alpha: 0.5f, transparencyHint: 0, profile: AlphaProfile.Opaque));
    }

    [Fact]
    public void HintZero_BinaryProfile_IsCutout()
    {
        // #2540: a hint-less mesh with a hard 0/1 alpha mask (fur/mane cards — e.g. the dire-tiger
        // texture shared by the mane AND the body skin, #2507) is alpha-TESTED with depth write,
        // not order-dependent blended. The engine's default for alpha-bearing meshes is alpha-test
        // (xoreos keeps GL_ALPHA_TEST on; behavioral ref only, GPLv3, not copied). Routing the many
        // overlapping mane/body layers through the depth-mask-off blend pass made them sort-fight
        // into black triangular shards; Cutout is order-independent and renders clean.
        Assert.Equal(MaterialMode.Cutout,
            MeshTransparency.ClassifyMesh(1.0f, 0, AlphaProfile.Binary));
    }

    [Fact]
    public void HintZero_GradedProfile_IsTransparent()
    {
        // Genuinely graded alpha with no hint (the zodiac/celestial creatures, #2435 — a real soft
        // see-through body) blends. Distinct from the binary fur mask above.
        Assert.Equal(MaterialMode.Transparent,
            MeshTransparency.ClassifyMesh(1.0f, 0, AlphaProfile.Graded));
    }

    [Fact]
    public void HintZero_OpaqueTexture_IsOpaque()
    {
        // DXT1 / 24-bit body has no alpha channel -> Opaque profile -> stays opaque (e.g. the dire
        // tiger's own DXT1 body texture c_cat_dire). The #2507 "punch holes in _d bodies" trap does
        // not arise: an overloaded _d spec body decodes Opaque here, so it never blends or carves.
        Assert.Equal(MaterialMode.Opaque,
            MeshTransparency.ClassifyMesh(1.0f, 0, AlphaProfile.Opaque));
    }

    [Fact]
    public void HintSet_BinaryProfile_IsCutout()
    {
        Assert.Equal(MaterialMode.Cutout,
            MeshTransparency.ClassifyMesh(1.0f, 1, AlphaProfile.Binary));
    }

    // ---- SkinNode never carves (#2588) ----

    [Fact]
    public void SkinNode_BinaryProfile_HintZero_IsOpaque_NotCutout()
    {
        // #2588: a deformable body SkinNode that SHARES its DDS atlas with a cutout fur/mane mesh
        // (CEP dire tiger: dire_tiger SkinNode + Mane DanglyNode both use N_Tiger_LaoHu02_D) reads
        // the shared texture as Binary. A single per-texture profile can't be right for both: the
        // mane region is a cutout silhouette, the body region is solid skin. Alpha-testing the body
        // discards fragments where its UVs land on the mane's low-alpha region -> black patches on
        // body/crown. A SkinNode body is never a cutout card: classify Opaque, keep depth writes,
        // no discard. The separate mane/dangly trimeshes still classify Cutout and carve correctly.
        Assert.Equal(MaterialMode.Opaque,
            MeshTransparency.ClassifyMesh(1.0f, 0, AlphaProfile.Binary, isSkin: true));
    }

    [Fact]
    public void SkinNode_BinaryProfile_HintSet_IsOpaque_NotCutout()
    {
        // Even with an explicit TransparencyHint, a body SkinNode must not alpha-test itself into
        // holes — the never-cutout rule for skins holds regardless of the hint.
        Assert.Equal(MaterialMode.Opaque,
            MeshTransparency.ClassifyMesh(1.0f, 1, AlphaProfile.Binary, isSkin: true));
    }

    [Fact]
    public void SkinNode_GradedProfile_IsTransparent()
    {
        // A genuinely soft-gradient skinned body (none observed in the #2588 test set — zodiac
        // creatures are Trimesh — but kept correct) still blends. The never-cutout rule only
        // diverts the Binary case; Graded skins keep their real translucency.
        Assert.Equal(MaterialMode.Transparent,
            MeshTransparency.ClassifyMesh(1.0f, 0, AlphaProfile.Graded, isSkin: true));
    }

    [Fact]
    public void SkinNode_MeshAlphaBelowOne_IsTransparent()
    {
        // Controller-driven fade still wins for skins — a body told to be semi-transparent blends.
        Assert.Equal(MaterialMode.Transparent,
            MeshTransparency.ClassifyMesh(0.5f, 0, AlphaProfile.Binary, isSkin: true));
    }

    [Fact]
    public void TrimeshDangly_BinaryProfile_StillCutout_NotSkin()
    {
        // The mane/fur accessory (Dangly/Trimesh, NOT a SkinNode) keeps Cutout — the body-skin
        // carve-out must not disarm cutout on the fur cards that genuinely need it (#2507).
        Assert.Equal(MaterialMode.Cutout,
            MeshTransparency.ClassifyMesh(1.0f, 0, AlphaProfile.Binary, isSkin: false));
    }

    [Fact]
    public void HintSet_GradedProfile_IsTransparent()
    {
        Assert.Equal(MaterialMode.Transparent,
            MeshTransparency.ClassifyMesh(1.0f, 1, AlphaProfile.Graded));
    }

    [Fact]
    public void HintSet_OpaqueProfile_IsOpaque()
    {
        // Hint says "I may have alpha to interpret", but the texture has none -> opaque.
        Assert.Equal(MaterialMode.Opaque,
            MeshTransparency.ClassifyMesh(1.0f, 1, AlphaProfile.Opaque));
    }

    [Fact]
    public void FullyOpaqueMesh_OpaqueTexture_IsOpaque()
    {
        Assert.Equal(MaterialMode.Opaque,
            MeshTransparency.ClassifyMesh(1.0f, 0, AlphaProfile.Opaque));
    }

    // ---- SortBackToFront ----

    [Fact]
    public void Sort_SameHint_FarthestFirst()
    {
        // depth: larger = farther from camera. Back-to-front => descending depth.
        var items = new[]
        {
            (hint: 0, depth: 1.0f),  // index 0, near
            (hint: 0, depth: 5.0f),  // index 1, far
            (hint: 0, depth: 3.0f),  // index 2, mid
        };
        var order = MeshTransparency.SortBackToFront(items);
        Assert.Equal(new[] { 1, 2, 0 }, order);
    }

    [Fact]
    public void Sort_LowerHintDrawnFirst()
    {
        // rollnw: TransparencyHint orders first (ascending), distance is the tiebreak.
        // A near low-hint mesh still draws before a far high-hint mesh.
        var items = new[]
        {
            (hint: 5, depth: 9.0f),  // index 0: high hint, very far
            (hint: 1, depth: 1.0f),  // index 1: low hint, near
        };
        var order = MeshTransparency.SortBackToFront(items);
        Assert.Equal(new[] { 1, 0 }, order);
    }

    [Fact]
    public void Sort_Empty_ReturnsEmpty()
    {
        var order = MeshTransparency.SortBackToFront(System.Array.Empty<(int, float)>());
        Assert.Empty(order);
    }
}

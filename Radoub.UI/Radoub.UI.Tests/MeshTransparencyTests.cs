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
    public void MostlyOpaqueWithRareSoftPixel_IsBinary()
    {
        // 1 soft pixel out of 100 = 1% < 5% threshold -> binary, not graded.
        var alphas = new byte[100];
        for (int i = 0; i < 100; i++) alphas[i] = (byte)(i == 0 ? 128 : 255);
        Assert.Equal(AlphaProfile.Binary, MeshTransparency.AnalyzeAlphaProfile(Rgba(alphas), 10, 10));
    }

    [Fact]
    public void ManySoftPixels_IsGraded()
    {
        // 50% of pixels in the soft band -> a real gradient (ghost, glass).
        var alphas = new byte[100];
        for (int i = 0; i < 100; i++) alphas[i] = (byte)(i < 50 ? 128 : 255);
        Assert.Equal(AlphaProfile.Graded, MeshTransparency.AnalyzeAlphaProfile(Rgba(alphas), 10, 10));
    }

    [Fact]
    public void EmptyOrNullData_IsOpaque()
    {
        Assert.Equal(AlphaProfile.Opaque, MeshTransparency.AnalyzeAlphaProfile(System.Array.Empty<byte>(), 0, 0));
        Assert.Equal(AlphaProfile.Opaque, MeshTransparency.AnalyzeAlphaProfile(null!, 0, 0));
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
        // #2540 hint-less fallthrough: CEP creatures (e.g. the dire-tiger mane, #2507) encode
        // their cutout only in the texture alpha, never in the MDL. A genuinely BINARY profile
        // is a real 0/255 cutout mask, so it carves even with no hint. This is safe because the
        // #2507 trap — an opaque _d body with alpha overloaded as a spec value — classifies as
        // Opaque or Graded by AnalyzeAlphaProfile, NOT Binary, so it never reaches this branch.
        Assert.Equal(MaterialMode.Cutout,
            MeshTransparency.ClassifyMesh(1.0f, 0, AlphaProfile.Binary));
    }

    [Fact]
    public void HintZero_GradedProfile_IsOpaque()
    {
        // Graded alpha with no hint stays opaque on the creature path (matches rollnw's
        // character-class behavior). The zod rat (#2435) is graded but is gated on in-game UAT
        // before any creature blend path lands, so it must NOT auto-blend here.
        Assert.Equal(MaterialMode.Opaque,
            MeshTransparency.ClassifyMesh(1.0f, 0, AlphaProfile.Graded));
    }

    [Fact]
    public void HintSet_BinaryProfile_IsCutout()
    {
        Assert.Equal(MaterialMode.Cutout,
            MeshTransparency.ClassifyMesh(1.0f, 1, AlphaProfile.Binary));
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

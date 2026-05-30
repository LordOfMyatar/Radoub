using System.Numerics;
using Radoub.Formats.Mdl;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Regression tests for #1735 — seam-overlap and bounds math must use each part mesh's FULL
/// world transform (parent bone chain × mesh-local), not the mesh-local transform alone.
///
/// In real composition (<see cref="MdlPartComposer.TryAddBodyPart"/>), a part mesh is reparented
/// under a skeleton bone and its own <c>Position</c> is zeroed — the world position lives in the
/// bone chain. The renderer (<see cref="ModelViewController.GetWorldTransform"/>) walks that chain,
/// so parts render at the bone positions. But <see cref="MdlPartComposer.AdjustSeamOverlaps"/> and
/// <see cref="MdlPartComposer.UpdateCompositeBounds"/> previously read only <c>mesh.Position</c>,
/// so for bone-parented parts every mesh appeared to sit at the bone-local origin (Z≈0). That
/// misjudged the overlap and fired large bogus nudges, scattering parts on non-human skeletons
/// (Brownie severe, elf "head on a pin").
///
/// These tests build the PRODUCTION-shaped graph: head/neck meshes parented under bones that carry
/// the world Z, with mesh-local Position zeroed. The earlier
/// <see cref="MdlPartComposerSeamOverlapTests"/> baked world Z into mesh.Position and so never
/// exercised this path.
/// </summary>
public class MdlPartComposerBoneParentedSeamTests
{
    private const float Tolerance = 0.001f;

    /// <summary>
    /// Build a composite whose GeometryRoot is a skeleton root with head_g/neck_g bones at the
    /// given world Z. Head and neck trimeshes are parented under their bones with Position=Zero;
    /// their vertices are authored in bone-local space (relative to the bone's world Z).
    /// </summary>
    /// <param name="bodyBottomZ">
    /// When set, a tiny "foot" anchor mesh is planted at this world Z so the composite's total
    /// height (used to scale the seam threshold, #1735) reflects a full body rather than just the
    /// head+neck span. Pass the model's foot Z (e.g. 0 for a ~headBoneZ-tall body) to exercise the
    /// human-scale threshold; omit for height-agnostic checks.
    /// </param>
    private static (MdlModel model, Dictionary<string, string> partTypes) CreateBoneParentedHeadNeck(
        float headBoneZ, float headLocalMinZ, float headLocalMaxZ,
        float neckBoneZ, float neckLocalMinZ, float neckLocalMaxZ,
        float? bodyBottomZ = null)
    {
        var root = new MdlNode { Name = "skeleton_root", Orientation = Quaternion.Identity, Scale = 1.0f };

        if (bodyBottomZ is float footZ)
        {
            var footBone = new MdlNode
            {
                Name = "foot_g",
                Position = new Vector3(0, 0, footZ),
                Orientation = Quaternion.Identity,
                Scale = 1.0f,
                Parent = root,
            };
            root.Children.Add(footBone);
            var footMesh = new MdlTrimeshNode
            {
                Name = "footmesh01",
                Position = Vector3.Zero,
                Orientation = Quaternion.Identity,
                Scale = 1.0f,
                Vertices = new[] { new Vector3(0, 0, 0), new Vector3(0.05f, 0, 0.02f) },
                Faces = Array.Empty<MdlFace>(),
                Parent = footBone,
            };
            footBone.Children.Add(footMesh);
        }

        var headBone = new MdlNode
        {
            Name = "head_g",
            Position = new Vector3(0, 0, headBoneZ),
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = root,
        };
        root.Children.Add(headBone);

        var neckBone = new MdlNode
        {
            Name = "neck_g",
            Position = new Vector3(0, 0, neckBoneZ),
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = root,
        };
        root.Children.Add(neckBone);

        // Parts reparented under bones with zeroed local Position (mirrors TryAddBodyPart).
        var headMesh = new MdlTrimeshNode
        {
            Name = "headmesh01",
            Position = Vector3.Zero,
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Vertices = new[]
            {
                new Vector3(0, 0, headLocalMinZ),
                new Vector3(0.1f, 0, headLocalMaxZ),
                new Vector3(-0.1f, 0, (headLocalMinZ + headLocalMaxZ) / 2f),
            },
            Faces = Array.Empty<MdlFace>(),
            Parent = headBone,
        };
        headBone.Children.Add(headMesh);

        var neckMesh = new MdlTrimeshNode
        {
            Name = "neckmesh01",
            Position = Vector3.Zero,
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Vertices = new[]
            {
                new Vector3(0, 0, neckLocalMinZ),
                new Vector3(0.1f, 0, neckLocalMaxZ),
                new Vector3(-0.1f, 0, (neckLocalMinZ + neckLocalMaxZ) / 2f),
            },
            Faces = Array.Empty<MdlFace>(),
            Parent = neckBone,
        };
        neckBone.Children.Add(neckMesh);

        var model = new MdlModel { Name = "test_composite", GeometryRoot = root };
        var partTypes = new Dictionary<string, string>
        {
            ["headmesh01"] = "head",
            ["neckmesh01"] = "neck",
        };
        return (model, partTypes);
    }

    [Fact]
    public void AdjustSeamOverlaps_TinyModel_ProportionalOverlap_NotOverNudged()
    {
        // #1735: Brownie is human-PROPORTIONED but tiny (~0.45× scale). Its parts overlap by the
        // same FRACTION of body height as a human's, so an absolute 0.10 threshold (tuned for a
        // ~1.9-tall human) over-nudges a ~0.85-tall model and shoves the head into the chest.
        //
        // Real Brownie log: head worldZ [0.850..0.954], neck [0.777..0.869]. head/neck overlap
        // = 0.869 - 0.850 = 0.019. Whole-body extent ≈ 0.064..0.954 ≈ 0.89. As a fraction this
        // overlap (~2.1% of height) is the same order as a human thin seam — it should NOT be
        // yanked down by the full human-scale deficit.
        //
        // With a height-relative threshold (~5% of extent ≈ 0.045 here), the deficit is
        // 0.045 - 0.019 = 0.026 → half = 0.013 per part. The OLD absolute 0.10 threshold gives
        // deficit 0.081 → 0.040 per part — twice the entire neck height. Assert the head is not
        // moved more than the part actually overlaps (never pushed through the neck).
        // Foot anchor at Z=0.064 → full body height ≈ 0.954 - 0.064 = 0.89 (real Brownie extent),
        // so the seam threshold scales to ≈ 0.89 × 0.0526 ≈ 0.047 instead of the human 0.10.
        var (model, partTypes) = CreateBoneParentedHeadNeck(
            headBoneZ: 0.90f, headLocalMinZ: -0.05f, headLocalMaxZ: 0.054f,   // world [0.850..0.954]
            neckBoneZ: 0.82f, neckLocalMinZ: -0.043f, neckLocalMaxZ: 0.049f,  // world [0.777..0.869]
            bodyBottomZ: 0.064f);

        MdlPartComposer.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        float headDrop = -headMesh.Position.Z; // how far the head was pushed down

        // The head must not be pushed down by more than the existing overlap (0.019) — i.e. it
        // must never be driven past where the neck already meets it. The old absolute threshold
        // pushed it 0.040 (> 0.019), sinking the head into the chest. With the height-scaled
        // threshold + overlap cap, the move is ≤ overlap/2.
        Assert.True(headDrop <= 0.019f + Tolerance,
            $"Head over-nudged: dropped {headDrop:F3} but parts only overlapped 0.019");
    }

    [Fact]
    public void AdjustSeamOverlaps_BoneParented_AdequateOverlap_NoNudge()
    {
        // Head bone at Z=1.7, head verts span world [1.6, 1.9]. Neck bone at Z=1.5, neck verts
        // span world [1.3, 1.75]. World overlap = neckMaxZ(1.75) - headMinZ(1.6) = 0.15 ≥ 0.10.
        // With correct world-aware math: no nudge. With the old mesh-local math: both meshes read
        // as centered at Z≈0 with overlapping vertex ranges → wrong deficit → spurious nudge.
        var (model, partTypes) = CreateBoneParentedHeadNeck(
            headBoneZ: 1.7f, headLocalMinZ: -0.1f, headLocalMaxZ: 0.2f,
            neckBoneZ: 1.5f, neckLocalMinZ: -0.2f, neckLocalMaxZ: 0.25f);

        MdlPartComposer.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        // Adequate world overlap → parts must NOT be moved.
        Assert.Equal(0f, headMesh.Position.Z, Tolerance);
        Assert.Equal(0f, neckMesh.Position.Z, Tolerance);
    }

    [Fact]
    public void AdjustSeamOverlaps_BoneParented_ThinOverlap_NudgesCloser()
    {
        // Full-height human body (foot at Z=0, head top at 1.90 → height 1.90 → threshold ≈ 0.10).
        // Head world span [1.70, 1.90], neck world span [1.50, 1.74]. World overlap =
        // 1.74 - 1.70 = 0.04 < 0.10 → nudge. The nudge is CAPPED by the existing overlap (#1735)
        // so parts already touching aren't driven past each other: deficit = min(0.06, 0.04) = 0.04
        // → 0.02 per part.
        var (model, partTypes) = CreateBoneParentedHeadNeck(
            headBoneZ: 1.7f, headLocalMinZ: 0.0f, headLocalMaxZ: 0.2f,
            neckBoneZ: 1.5f, neckLocalMinZ: 0.0f, neckLocalMaxZ: 0.24f,
            bodyBottomZ: 0f);

        MdlPartComposer.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        Assert.True(headMesh.Position.Z < 0f, "Head should nudge down (negative local Z delta)");
        Assert.True(neckMesh.Position.Z > 0f, "Neck should nudge up (positive local Z delta)");

        float headDelta = -headMesh.Position.Z;
        float neckDelta = neckMesh.Position.Z;
        Assert.Equal(headDelta, neckDelta, Tolerance); // split evenly
        Assert.Equal(0.02f, headDelta, Tolerance);     // min(threshold-overlap, overlap)/2 = 0.04/2
    }

    [Fact]
    public void UpdateCompositeBounds_BoneParented_UsesWorldZ()
    {
        // Head bone Z=1.7, verts local [-0.1,0.2] → world [1.6,1.9].
        // Neck bone Z=1.5, verts local [-0.2,0.25] → world [1.3,1.75].
        // Correct world bounds Z: min 1.3, max 1.9. Old mesh-local math: min -0.2, max 0.25.
        var (model, _) = CreateBoneParentedHeadNeck(
            headBoneZ: 1.7f, headLocalMinZ: -0.1f, headLocalMaxZ: 0.2f,
            neckBoneZ: 1.5f, neckLocalMinZ: -0.2f, neckLocalMaxZ: 0.25f);

        MdlPartComposer.UpdateCompositeBounds(model);

        Assert.Equal(1.3f, model.BoundingMin.Z, Tolerance);
        Assert.Equal(1.9f, model.BoundingMax.Z, Tolerance);
    }

    [Fact]
    public void AdjustSeamOverlaps_BoneParented_Gap_ClosesFullDeficit()
    {
        // A real GAP (no overlap): head world [1.75, 1.90], neck world [1.50, 1.74]. Gap =
        // 1.74 - 1.75 = -0.01. Full-height body (foot Z=0 → height 1.90 → threshold ≈ 0.10).
        // overlap ≤ 0 ⇒ no cap (nothing to push through); close the whole deficit
        // 0.10 - (-0.01) = 0.11 → 0.055 per part.
        var (model, partTypes) = CreateBoneParentedHeadNeck(
            headBoneZ: 1.75f, headLocalMinZ: 0.0f, headLocalMaxZ: 0.15f,
            neckBoneZ: 1.50f, neckLocalMinZ: 0.0f, neckLocalMaxZ: 0.24f,
            bodyBottomZ: 0f);

        MdlPartComposer.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        float headDelta = -headMesh.Position.Z;
        float neckDelta = neckMesh.Position.Z;
        Assert.Equal(headDelta, neckDelta, Tolerance);
        Assert.Equal(0.11f, headDelta + neckDelta, Tolerance);
    }

    [Fact]
    public void AdjustSeamOverlaps_UnmappedMeshes_Ignored()
    {
        var root = new MdlNode { Name = "composite_root" };
        var mesh = new MdlTrimeshNode
        {
            Name = "randomMesh",
            Position = Vector3.Zero,
            Vertices = new[] { new Vector3(0, 0, 0) },
            Faces = Array.Empty<MdlFace>(),
            Parent = root,
        };
        root.Children.Add(mesh);

        var model = new MdlModel { Name = "test", GeometryRoot = root };

        MdlPartComposer.AdjustSeamOverlaps(model, new Dictionary<string, string>());

        Assert.Equal(Vector3.Zero, mesh.Position);
    }
}

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
    private static (MdlModel model, Dictionary<string, string> partTypes) CreateBoneParentedHeadNeck(
        float headBoneZ, float headLocalMinZ, float headLocalMaxZ,
        float neckBoneZ, float neckLocalMinZ, float neckLocalMaxZ)
    {
        var root = new MdlNode { Name = "skeleton_root", Orientation = Quaternion.Identity, Scale = 1.0f };

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
        // Head world span [1.70, 1.90], neck world span [1.50, 1.74]. World overlap =
        // 1.74 - 1.70 = 0.04 < 0.10 → should nudge head down, neck up, by 0.03 each.
        var (model, partTypes) = CreateBoneParentedHeadNeck(
            headBoneZ: 1.7f, headLocalMinZ: 0.0f, headLocalMaxZ: 0.2f,
            neckBoneZ: 1.5f, neckLocalMinZ: 0.0f, neckLocalMaxZ: 0.24f);

        MdlPartComposer.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        Assert.True(headMesh.Position.Z < 0f, "Head should nudge down (negative local Z delta)");
        Assert.True(neckMesh.Position.Z > 0f, "Neck should nudge up (positive local Z delta)");

        float headDelta = -headMesh.Position.Z;
        float neckDelta = neckMesh.Position.Z;
        Assert.Equal(headDelta, neckDelta, Tolerance); // split evenly
        Assert.Equal(0.03f, headDelta, Tolerance);     // (0.10 - 0.04) / 2
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
}

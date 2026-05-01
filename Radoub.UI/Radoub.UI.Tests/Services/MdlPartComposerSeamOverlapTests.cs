using System.Numerics;
using Radoub.Formats.Mdl;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Tests for MdlPartComposer.AdjustSeamOverlaps — body parts with thin vertex overlap
/// at joints get nudged closer together (#1557). Threshold is 0.10 world units (matching
/// human-level overlap). Originally added in Quartermaster; migrated when the seam-overlap
/// helper moved into MdlPartComposer (PR3a, #2159).
/// </summary>
public class MdlPartComposerSeamOverlapTests
{
    private const float Tolerance = 0.001f;
    private const float Threshold = 0.10f; // Must match MdlPartComposer.MinSeamOverlap

    /// <summary>
    /// Create a synthetic composite model with head and neck meshes at given Z positions.
    /// </summary>
    private static (MdlModel model, Dictionary<string, string> partTypes) CreateHeadNeckModel(
        float headMinZ, float headMaxZ, Vector3 headPosition,
        float neckMinZ, float neckMaxZ, Vector3 neckPosition)
    {
        var root = new MdlNode { Name = "composite_root" };

        var headMesh = new MdlTrimeshNode
        {
            Name = "headmesh01",
            Position = headPosition,
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Vertices = new[]
            {
                new Vector3(0, 0, headMinZ - headPosition.Z),
                new Vector3(0.1f, 0, headMaxZ - headPosition.Z),
                new Vector3(-0.1f, 0, (headMinZ + headMaxZ) / 2f - headPosition.Z),
            },
            Faces = Array.Empty<MdlFace>(),
            Parent = root
        };
        root.Children.Add(headMesh);

        var neckMesh = new MdlTrimeshNode
        {
            Name = "neckmesh01",
            Position = neckPosition,
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Vertices = new[]
            {
                new Vector3(0, 0, neckMinZ - neckPosition.Z),
                new Vector3(0.1f, 0, neckMaxZ - neckPosition.Z),
                new Vector3(-0.1f, 0, (neckMinZ + neckMaxZ) / 2f - neckPosition.Z),
            },
            Faces = Array.Empty<MdlFace>(),
            Parent = root
        };
        root.Children.Add(neckMesh);

        var model = new MdlModel
        {
            Name = "test_composite",
            GeometryRoot = root
        };

        var partTypes = new Dictionary<string, string>
        {
            ["headmesh01"] = "head",
            ["neckmesh01"] = "neck",
        };

        return (model, partTypes);
    }

    [Fact]
    public void AdjustSeamOverlaps_ThinOverlap_NudgesPartsCloser()
    {
        var (model, partTypes) = CreateHeadNeckModel(
            headMinZ: 1.0f, headMaxZ: 1.5f, headPosition: new Vector3(0, 0, 1.25f),
            neckMinZ: 0.5f, neckMaxZ: 1.05f, neckPosition: new Vector3(0, 0, 0.75f));

        float overlapBefore = 1.05f - 1.0f;
        Assert.True(overlapBefore < Threshold, "Precondition: overlap should be below threshold");

        MdlPartComposer.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        Assert.True(headMesh.Position.Z < 1.25f, "Head should have moved down");
        Assert.True(neckMesh.Position.Z > 0.75f, "Neck should have moved up");
    }

    [Fact]
    public void AdjustSeamOverlaps_AdequateOverlap_NoChange()
    {
        var headPos = new Vector3(0, 0, 1.25f);
        var neckPos = new Vector3(0, 0, 0.75f);
        var (model, partTypes) = CreateHeadNeckModel(
            headMinZ: 1.0f, headMaxZ: 1.5f, headPosition: headPos,
            neckMinZ: 0.5f, neckMaxZ: 1.12f, neckPosition: neckPos);

        MdlPartComposer.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        Assert.Equal(headPos.Z, headMesh.Position.Z, Tolerance);
        Assert.Equal(neckPos.Z, neckMesh.Position.Z, Tolerance);
    }

    [Fact]
    public void AdjustSeamOverlaps_NegativeOverlap_NudgesPartsCloser()
    {
        var (model, partTypes) = CreateHeadNeckModel(
            headMinZ: 1.0f, headMaxZ: 1.5f, headPosition: new Vector3(0, 0, 1.25f),
            neckMinZ: 0.5f, neckMaxZ: 0.99f, neckPosition: new Vector3(0, 0, 0.75f));

        float overlapBefore = 0.99f - 1.0f;
        Assert.True(overlapBefore < 0f, "Precondition: should be a gap");

        MdlPartComposer.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        Assert.True(headMesh.Position.Z < 1.25f, "Head should have moved down");
        Assert.True(neckMesh.Position.Z > 0.75f, "Neck should have moved up");

        float totalNudge = (1.25f - headMesh.Position.Z) + (neckMesh.Position.Z - 0.75f);
        Assert.Equal(0.11f, totalNudge, Tolerance);
    }

    [Fact]
    public void AdjustSeamOverlaps_SplitsDeficitEvenly()
    {
        var (model, partTypes) = CreateHeadNeckModel(
            headMinZ: 1.0f, headMaxZ: 1.5f, headPosition: new Vector3(0, 0, 1.25f),
            neckMinZ: 0.5f, neckMaxZ: 1.05f, neckPosition: new Vector3(0, 0, 0.75f));

        MdlPartComposer.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        float headDelta = 1.25f - headMesh.Position.Z;
        float neckDelta = neckMesh.Position.Z - 0.75f;

        Assert.Equal(headDelta, neckDelta, Tolerance);
        Assert.Equal(0.025f, headDelta, Tolerance);
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
            Parent = root
        };
        root.Children.Add(mesh);

        var model = new MdlModel { Name = "test", GeometryRoot = root };
        var partTypes = new Dictionary<string, string>();

        MdlPartComposer.AdjustSeamOverlaps(model, partTypes);

        Assert.Equal(Vector3.Zero, mesh.Position);
    }

    [Fact]
    public void AdjustSeamOverlaps_ElfLikeOverlap_GetsNudged()
    {
        // Real measured elf overlap: 0.048
        var (model, partTypes) = CreateHeadNeckModel(
            headMinZ: 1.655f, headMaxZ: 1.85f, headPosition: new Vector3(0, 0, 1.703f),
            neckMinZ: 1.50f, neckMaxZ: 1.703f, neckPosition: new Vector3(0, 0, 1.622f));

        float overlapBefore = 1.703f - 1.655f;
        Assert.Equal(0.048f, overlapBefore, Tolerance);
        Assert.True(overlapBefore < Threshold);

        MdlPartComposer.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        Assert.True(headMesh.Position.Z < 1.703f, "Elf head should have moved down");
        Assert.True(neckMesh.Position.Z > 1.622f, "Elf neck should have moved up");

        float headDelta = 1.703f - headMesh.Position.Z;
        Assert.Equal(0.026f, headDelta, Tolerance);
    }
}

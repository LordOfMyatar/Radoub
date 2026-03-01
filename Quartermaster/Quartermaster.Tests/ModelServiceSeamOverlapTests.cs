using System.Numerics;
using Quartermaster.Services;
using Radoub.Formats.Mdl;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for ModelService.AdjustSeamOverlaps — validates that body parts with thin
/// vertex overlap at joints get nudged closer together (#1557).
/// </summary>
public class ModelServiceSeamOverlapTests
{
    private const float Tolerance = 0.001f;

    /// <summary>
    /// Create a synthetic composite model with head and neck meshes at given Z positions.
    /// Head mesh vertices span [headMinZ, headMaxZ], neck vertices span [neckMinZ, neckMaxZ].
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
        // Head bottom at Z=1.0, neck top at Z=1.01 → overlap = 0.01 (below 0.03 threshold)
        var (model, partTypes) = CreateHeadNeckModel(
            headMinZ: 1.0f, headMaxZ: 1.5f, headPosition: new Vector3(0, 0, 1.25f),
            neckMinZ: 0.5f, neckMaxZ: 1.01f, neckPosition: new Vector3(0, 0, 0.75f));

        float overlapBefore = 1.01f - 1.0f; // 0.01
        Assert.True(overlapBefore < 0.03f, "Precondition: overlap should be thin");

        ModelService.AdjustSeamOverlaps(model, partTypes);

        // After adjustment, head should have moved down and neck up
        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        // Head moved down (Z decreased), neck moved up (Z increased)
        Assert.True(headMesh.Position.Z < 1.25f, "Head should have moved down");
        Assert.True(neckMesh.Position.Z > 0.75f, "Neck should have moved up");
    }

    [Fact]
    public void AdjustSeamOverlaps_AdequateOverlap_NoChange()
    {
        // Head bottom at Z=1.0, neck top at Z=1.07 → overlap = 0.07 (above 0.03 threshold)
        var headPos = new Vector3(0, 0, 1.25f);
        var neckPos = new Vector3(0, 0, 0.75f);
        var (model, partTypes) = CreateHeadNeckModel(
            headMinZ: 1.0f, headMaxZ: 1.5f, headPosition: headPos,
            neckMinZ: 0.5f, neckMaxZ: 1.07f, neckPosition: neckPos);

        ModelService.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        // Positions should be unchanged
        Assert.Equal(headPos.Z, headMesh.Position.Z, Tolerance);
        Assert.Equal(neckPos.Z, neckMesh.Position.Z, Tolerance);
    }

    [Fact]
    public void AdjustSeamOverlaps_NegativeOverlap_NudgesPartsCloser()
    {
        // Head bottom at Z=1.0, neck top at Z=0.99 → overlap = -0.01 (gap!)
        var (model, partTypes) = CreateHeadNeckModel(
            headMinZ: 1.0f, headMaxZ: 1.5f, headPosition: new Vector3(0, 0, 1.25f),
            neckMinZ: 0.5f, neckMaxZ: 0.99f, neckPosition: new Vector3(0, 0, 0.75f));

        float overlapBefore = 0.99f - 1.0f; // -0.01 (gap)
        Assert.True(overlapBefore < 0f, "Precondition: should be a gap");

        ModelService.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        // Both should have been nudged
        Assert.True(headMesh.Position.Z < 1.25f, "Head should have moved down");
        Assert.True(neckMesh.Position.Z > 0.75f, "Neck should have moved up");

        // Total nudge should close the 0.01 gap plus reach the 0.03 threshold
        float totalNudge = (1.25f - headMesh.Position.Z) + (neckMesh.Position.Z - 0.75f);
        Assert.Equal(0.04f, totalNudge, Tolerance); // deficit = 0.03 - (-0.01) = 0.04
    }

    [Fact]
    public void AdjustSeamOverlaps_SplitsDeficitEvenly()
    {
        // Overlap = 0.01, threshold = 0.03, deficit = 0.02, half = 0.01
        var (model, partTypes) = CreateHeadNeckModel(
            headMinZ: 1.0f, headMaxZ: 1.5f, headPosition: new Vector3(0, 0, 1.25f),
            neckMinZ: 0.5f, neckMaxZ: 1.01f, neckPosition: new Vector3(0, 0, 0.75f));

        ModelService.AdjustSeamOverlaps(model, partTypes);

        var headMesh = model.GetMeshNodes().First(m => m.Name == "headmesh01");
        var neckMesh = model.GetMeshNodes().First(m => m.Name == "neckmesh01");

        float headDelta = 1.25f - headMesh.Position.Z;
        float neckDelta = neckMesh.Position.Z - 0.75f;

        // Both should move by the same amount
        Assert.Equal(headDelta, neckDelta, Tolerance);
        Assert.Equal(0.01f, headDelta, Tolerance); // half of 0.02 deficit
    }

    [Fact]
    public void AdjustSeamOverlaps_UnmappedMeshes_Ignored()
    {
        // Mesh exists but isn't in the part type dictionary — should be skipped
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
        var partTypes = new Dictionary<string, string>(); // empty — nothing mapped

        // Should not throw, should not modify anything
        ModelService.AdjustSeamOverlaps(model, partTypes);

        Assert.Equal(Vector3.Zero, mesh.Position);
    }
}

using System;
using System.Linq;
using System.Numerics;
using Radoub.Formats.Common;
using Radoub.Formats.Mdl;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Tests for #2541 Phase 3: part composition must PRESERVE the authored local Position of a
/// body-part mesh instead of zeroing it. Real head MDLs author a small nonzero offset on the
/// trimesh relative to the part root (verified by dump: pmh0_head001g = (0,-0.010,+0.015);
/// pfh0_head001g danglymesh = (0,-0.0236,-0.0341)); the old <c>Position = Vector3.Zero</c>
/// discarded it, lifting the head into a visible gap above the neck (#2161).
/// </summary>
public class MdlPartComposerAuthoredPositionTests
{
    private const float Tolerance = 0.0001f;

    private static MdlModel MakeSkeleton(params (string boneName, Vector3 position)[] bones)
    {
        var root = new MdlNode { Name = "skeleton_root", Scale = 1.0f };
        foreach (var (boneName, position) in bones)
        {
            var bone = new MdlNode { Name = boneName, Position = position, Scale = 1.0f, Parent = root };
            root.Children.Add(bone);
        }
        return new MdlModel { Name = "skeleton", GeometryRoot = root, IsBinary = true };
    }

    /// <summary>Part MDL shaped like a real head part: dummy root at origin, mesh authored at a
    /// nonzero local offset. <paramref name="makeNode"/> picks trimesh vs danglymesh.</summary>
    private static MdlModel MakeHeadPart(string name, string meshName, Vector3 meshOffset,
        Func<MdlTrimeshNode> makeNode)
    {
        var root = new MdlNode { Name = $"{name}", Scale = 1.0f };
        var mesh = makeNode();
        mesh.Name = meshName;
        mesh.Position = meshOffset;       // the authored local offset that must survive
        mesh.Scale = 1.0f;
        mesh.Vertices = new[] { Vector3.Zero };  // single vertex at mesh-local origin
        mesh.Faces = Array.Empty<MdlFace>();
        mesh.Bitmap = "stale";
        mesh.Parent = root;
        root.Children.Add(mesh);
        return new MdlModel { Name = name, GeometryRoot = root, IsBinary = true };
    }

    private static MockGameDataService Game(params string[] resRefs)
    {
        var game = new MockGameDataService(includeSampleData: false);
        foreach (var r in resRefs)
            game.SetResource(r, ResourceTypes.Mdl, new byte[] { 0x42 });
        return game;
    }

    [Theory]
    [InlineData(false)] // plain trimesh (male head)
    [InlineData(true)]  // danglymesh (female head) — must be handled identically
    public void Compose_PreservesAuthoredMeshOffset_UnderBone(bool dangly)
    {
        var bonePos = new Vector3(0, 0, 1.7f);            // head_g world height
        var authored = new Vector3(0, -0.0236f, -0.0341f); // the female-head offset from the dump
        var skeleton = MakeSkeleton(("head_g", bonePos));
        var headPart = MakeHeadPart("pfh0_head001", "head_mesh", authored,
            () => dangly ? new MdlDanglyNode() : new MdlTrimeshNode());

        var composer = new MdlPartComposer(Game("pfh0", "pfh0_head001"),
            (resRef, _) => resRef switch
            {
                "pfh0" => skeleton,
                "pfh0_head001" => headPart,
                _ => null,
            });

        // adjustSeams: false so we test the raw attachment position, not the seam nudge.
        var result = composer.Compose("pfh0", new[] { ("head", "pfh0_head001") }, adjustSeams: false);

        Assert.NotNull(result);
        var mesh = result!.GetMeshNodes().Single();
        Assert.Equal("head_g", mesh.Parent!.Name);

        // The mesh's world position must be bone + authored offset, NOT just the bone (zeroed).
        var world = MdlPartComposer.GetMeshWorldTransform(mesh);
        var worldPos = Vector3.Transform(Vector3.Zero, world);

        var expected = bonePos + authored;
        Assert.Equal(expected.X, worldPos.X, Tolerance);
        Assert.Equal(expected.Y, worldPos.Y, Tolerance);
        Assert.Equal(expected.Z, worldPos.Z, Tolerance);
    }

    [Fact]
    public void Compose_ChestAuthoredAtOrigin_SeatsExactlyOnBone()
    {
        // Regression guard: chest is authored at origin (dump), so preserving its position must
        // leave it exactly on the bone — the position fix must not move parts that were already 0.
        var bonePos = new Vector3(0, 0, 1.0f);
        var skeleton = MakeSkeleton(("torso_g", bonePos));
        var chestPart = MakeHeadPart("pmh0_chest001", "chest_mesh", Vector3.Zero,
            () => new MdlTrimeshNode());

        var composer = new MdlPartComposer(Game("pmh0", "pmh0_chest001"),
            (resRef, _) => resRef switch
            {
                "pmh0" => skeleton,
                "pmh0_chest001" => chestPart,
                _ => null,
            });

        var result = composer.Compose("pmh0", new[] { ("chest", "pmh0_chest001") }, adjustSeams: false);

        var mesh = result!.GetMeshNodes().Single();
        var worldPos = Vector3.Transform(Vector3.Zero, MdlPartComposer.GetMeshWorldTransform(mesh));

        Assert.Equal(bonePos.X, worldPos.X, Tolerance);
        Assert.Equal(bonePos.Y, worldPos.Y, Tolerance);
        Assert.Equal(bonePos.Z, worldPos.Z, Tolerance);
    }
}

using System.Numerics;
using Radoub.Formats.Common;
using Radoub.Formats.Mdl;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Tests the public Compose / ComposeFlat surface of MdlPartComposer.
/// Internal helpers (seam-overlap, bone-transform) are covered by their own
/// dedicated test files migrated from QM (see MdlPartComposerSeamOverlapTests
/// and MdlPartComposerBoneTransformTests).
/// </summary>
public class MdlPartComposerTests
{
    private static MockGameDataService BuildGameDataWith(params (string resRef, MdlModel model)[] models)
    {
        var game = new MockGameDataService(includeSampleData: false);
        foreach (var (resRef, model) in models)
        {
            // Mock parser doesn't actually parse — we hand the composer a pre-built model
            // via the factory injection point so the resource bytes here are placeholder
            game.SetResource(resRef, ResourceTypes.Mdl, new byte[] { 0x42 });
        }
        return game;
    }

    private static MdlModel MakeSkeleton(params (string boneName, Vector3 position)[] bones)
    {
        var root = new MdlNode
        {
            Name = "skeleton_root",
            Position = Vector3.Zero,
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
        };
        foreach (var (boneName, position) in bones)
        {
            var bone = new MdlNode
            {
                Name = boneName,
                Position = position,
                Orientation = Quaternion.Identity,
                Scale = 1.0f,
                Parent = root,
            };
            root.Children.Add(bone);
        }

        return new MdlModel
        {
            Name = "skeleton",
            GeometryRoot = root,
            IsBinary = true,
        };
    }

    private static MdlModel MakePartModel(string name, string meshName, Vector3 vertex)
        => MakePartModel(name, meshName, vertex, rootOffset: Vector3.Zero);

    /// <summary>
    /// Build a part MDL shaped like a real composite-weapon part: a dummy root node whose
    /// <paramref name="rootOffset"/> encodes the assembly position, with the mesh as a child at
    /// local origin. Aurora glues b/m/t parts using these dummy offsets, so a correct composer
    /// must preserve them.
    /// </summary>
    private static MdlModel MakePartModel(string name, string meshName, Vector3 vertex, Vector3 rootOffset)
    {
        var root = new MdlNode
        {
            Name = $"{name}_root",
            Position = rootOffset,
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
        };
        var mesh = new MdlTrimeshNode
        {
            Name = meshName,
            Position = Vector3.Zero,
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Vertices = new[] { vertex },
            Faces = Array.Empty<MdlFace>(),
            Bitmap = "stale_bitmap_field",  // verify composer overrides this
            Parent = root,
        };
        root.Children.Add(mesh);
        return new MdlModel { Name = name, GeometryRoot = root, IsBinary = true };
    }

    [Fact]
    public void Compose_NoParts_ReturnsNull()
    {
        var composer = new MdlPartComposer(BuildGameDataWith(), (_, _) => null);
        var result = composer.Compose("pmh0", Array.Empty<(string, string)>());

        Assert.Null(result);
    }

    [Fact]
    public void Compose_AllPartsMissingFromGameData_ReturnsNull()
    {
        var game = new MockGameDataService(includeSampleData: false);
        // No resources registered
        var composer = new MdlPartComposer(game, (_, _) => null);

        var result = composer.Compose("pmh0", new[] { ("chest", "pmh0_chest005") });

        Assert.Null(result);
    }

    [Fact]
    public void Compose_AttachesPartMeshUnderNamedBone()
    {
        var skeleton = MakeSkeleton(("torso_g", new Vector3(0, 0, 1)));
        var chestPart = MakePartModel("pmh0_chest005", "chest_mesh", new Vector3(0.5f, 0, 0));

        MdlModel? Loader(string resRef, bool _)
        {
            return resRef switch
            {
                "pmh0" => skeleton,
                "pmh0_chest005" => chestPart,
                _ => null,
            };
        }

        var game = BuildGameDataWith(("pmh0", skeleton), ("pmh0_chest005", chestPart));
        var composer = new MdlPartComposer(game, Loader);

        var result = composer.Compose("pmh0", new[] { ("chest", "pmh0_chest005") });

        Assert.NotNull(result);
        var meshes = result!.GetMeshNodes().ToList();
        Assert.Single(meshes);
        Assert.Equal("chest_mesh", meshes[0].Name);

        // Mesh should be parented under the bone, not the composite root directly
        Assert.NotNull(meshes[0].Parent);
        Assert.Equal("torso_g", meshes[0].Parent!.Name);
    }

    [Fact]
    public void Compose_OverridesStaleBitmapWithDerivedTextureName()
    {
        var skeleton = MakeSkeleton(("torso_g", new Vector3(0, 0, 1)));
        var chestPart = MakePartModel("pmh0_chest005", "chest_mesh", new Vector3(0.5f, 0, 0));

        var game = BuildGameDataWith(("pmh0", skeleton), ("pmh0_chest005", chestPart));
        var composer = new MdlPartComposer(game, (resRef, _) => resRef switch
        {
            "pmh0" => skeleton,
            "pmh0_chest005" => chestPart,
            _ => null,
        });

        var result = composer.Compose("pmh0", new[] { ("chest", "pmh0_chest005") });

        var mesh = result!.GetMeshNodes().First();

        // Stale bitmap must be replaced with the part ResRef (texture naming convention)
        Assert.NotEqual("stale_bitmap_field", mesh.Bitmap);
        Assert.Equal("pmh0_chest005", mesh.Bitmap);
    }

    [Fact]
    public void Compose_PartWithUnknownBoneName_StillIncludesMesh()
    {
        // boneNameForPart returns a bone not in the skeleton — should fall back to composite root
        var skeleton = MakeSkeleton(("torso_g", Vector3.Zero));
        var weirdPart = MakePartModel("pmh0_xyz001", "xyz_mesh", new Vector3(1, 0, 0));

        var game = BuildGameDataWith(("pmh0", skeleton), ("pmh0_xyz001", weirdPart));
        var composer = new MdlPartComposer(
            game,
            (resRef, _) => resRef switch
            {
                "pmh0" => skeleton,
                "pmh0_xyz001" => weirdPart,
                _ => null,
            },
            partType => "missing_bone_g");  // every part maps to a missing bone

        var result = composer.Compose("pmh0", new[] { ("xyz", "pmh0_xyz001") });

        Assert.NotNull(result);
        Assert.Single(result!.GetMeshNodes());
    }

    [Fact]
    public void Compose_UpdatesBoundingBoxFromAllMeshes()
    {
        var skeleton = MakeSkeleton(
            ("torso_g", new Vector3(0, 0, 1)),
            ("pelvis_g", new Vector3(0, 0, 0)));

        var chestPart = MakePartModel("pmh0_chest001", "chest_mesh", new Vector3(0, 0, 0.5f));
        var pelvisPart = MakePartModel("pmh0_pelvis001", "pelvis_mesh", new Vector3(0, 0, -0.5f));

        var game = BuildGameDataWith(("pmh0", skeleton), ("pmh0_chest001", chestPart), ("pmh0_pelvis001", pelvisPart));
        var composer = new MdlPartComposer(game, (resRef, _) => resRef switch
        {
            "pmh0" => skeleton,
            "pmh0_chest001" => chestPart,
            "pmh0_pelvis001" => pelvisPart,
            _ => null,
        });

        var result = composer.Compose("pmh0", new[]
        {
            ("chest", "pmh0_chest001"),
            ("pelvis", "pmh0_pelvis001"),
        });

        Assert.NotNull(result);
        Assert.True(result!.BoundingMax.Z > result.BoundingMin.Z, "Bounds must span chest+pelvis");
        Assert.True(result.Radius > 0f);
    }

    [Fact]
    public void ComposeFlat_AggregatesPartsUnderSyntheticRoot()
    {
        // Composite-weapon path: no skeleton, three MDLs joined under a flat root
        var bottom = MakePartModel("wdbsw_b_011", "wdbsw_b_mesh", new Vector3(0, 0, -1));
        var middle = MakePartModel("wdbsw_m_011", "wdbsw_m_mesh", new Vector3(0, 0, 0));
        var top = MakePartModel("wdbsw_t_011", "wdbsw_t_mesh", new Vector3(0, 0, 1));

        var game = BuildGameDataWith(("wdbsw_b_011", bottom), ("wdbsw_m_011", middle), ("wdbsw_t_011", top));
        var composer = new MdlPartComposer(game, (resRef, _) => resRef switch
        {
            "wdbsw_b_011" => bottom,
            "wdbsw_m_011" => middle,
            "wdbsw_t_011" => top,
            _ => null,
        });

        var result = composer.ComposeFlat(new[] { "wdbsw_b_011", "wdbsw_m_011", "wdbsw_t_011" });

        Assert.NotNull(result);
        Assert.Equal(3, result!.GetMeshNodes().Count());
        Assert.NotNull(result.GeometryRoot);
    }

    [Fact]
    public void ComposeFlat_PreservesDummyParentOffsets()
    {
        // Real composite-weapon parts are authored as: dummy root (assembly offset) → mesh at
        // local origin. Aurora glues b/m/t along the weapon axis using those dummy offsets. A
        // flatten that reparents only the trimesh discards the offset and collapses every part
        // to the composite origin (the floating-blade bug, #2233).
        var bottom = MakePartModel("wdbsw_b_011", "wdbsw_b_mesh", Vector3.Zero, rootOffset: new Vector3(0, 0, -2));
        var middle = MakePartModel("wdbsw_m_011", "wdbsw_m_mesh", Vector3.Zero, rootOffset: Vector3.Zero);
        var top = MakePartModel("wdbsw_t_011", "wdbsw_t_mesh", Vector3.Zero, rootOffset: new Vector3(0, 0, 2));

        var game = BuildGameDataWith(("wdbsw_b_011", bottom), ("wdbsw_m_011", middle), ("wdbsw_t_011", top));
        var composer = new MdlPartComposer(game, (resRef, _) => resRef switch
        {
            "wdbsw_b_011" => bottom,
            "wdbsw_m_011" => middle,
            "wdbsw_t_011" => top,
            _ => null,
        });

        var result = composer.ComposeFlat(new[] { "wdbsw_b_011", "wdbsw_m_011", "wdbsw_t_011" });

        Assert.NotNull(result);
        var meshes = result!.GetMeshNodes().ToList();
        Assert.Equal(3, meshes.Count);

        // Each mesh's effective world position must still reflect its part's dummy offset.
        // The single vertex is at local origin, so its world Z == accumulated parent-chain Z.
        float WorldZOf(string meshName)
        {
            var mesh = meshes.Single(m => m.Name == meshName);
            var world = MdlPartComposer.GetMeshWorldTransform(mesh);
            return Vector3.Transform(mesh.Vertices[0], world).Z;
        }

        Assert.Equal(-2f, WorldZOf("wdbsw_b_mesh"), precision: 3);
        Assert.Equal(0f, WorldZOf("wdbsw_m_mesh"), precision: 3);
        Assert.Equal(2f, WorldZOf("wdbsw_t_mesh"), precision: 3);
    }

    [Fact]
    public void ComposeFlat_NoParts_ReturnsNull()
    {
        var composer = new MdlPartComposer(new MockGameDataService(includeSampleData: false), (_, _) => null);
        Assert.Null(composer.ComposeFlat(Array.Empty<string>()));
    }

    [Fact]
    public void ComposeFlat_AllPartsMissing_ReturnsNull()
    {
        var game = new MockGameDataService(includeSampleData: false);
        var composer = new MdlPartComposer(game, (_, _) => null);

        Assert.Null(composer.ComposeFlat(new[] { "missing_001", "missing_002" }));
    }

    [Fact]
    public void Compose_PartialPartsMissing_KeepsRemainingMeshes()
    {
        var skeleton = MakeSkeleton(
            ("torso_g", new Vector3(0, 0, 1)),
            ("pelvis_g", Vector3.Zero));

        var chestPart = MakePartModel("pmh0_chest001", "chest_mesh", new Vector3(0, 0, 0.5f));
        // pelvis intentionally missing

        var game = BuildGameDataWith(("pmh0", skeleton), ("pmh0_chest001", chestPart));
        var composer = new MdlPartComposer(game, (resRef, _) => resRef switch
        {
            "pmh0" => skeleton,
            "pmh0_chest001" => chestPart,
            _ => null,
        });

        var result = composer.Compose("pmh0", new[]
        {
            ("chest", "pmh0_chest001"),
            ("pelvis", "pmh0_pelvis999"),  // missing
        });

        Assert.NotNull(result);
        Assert.Single(result!.GetMeshNodes());
        Assert.Equal("chest_mesh", result.GetMeshNodes().First().Name);
    }
}

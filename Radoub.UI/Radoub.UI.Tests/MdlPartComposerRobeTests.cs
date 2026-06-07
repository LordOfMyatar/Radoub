using System.Numerics;
using Radoub.Formats.Mdl;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests the robe subtree-graft path in MdlPartComposer (#1989). A robe model is a near-complete
/// posed body with its own nested bone hierarchy; the composer must graft that subtree preserving
/// each mesh's local transform (so limbs keep their authored positions) rather than splicing
/// individual meshes onto the skeleton's bones (which collapsed the arms onto the torso).
/// </summary>
public class MdlPartComposerRobeTests
{
    private static MdlTrimeshNode Mesh(string name, Vector3 pos, int verts, bool skin = false)
    {
        var n = skin ? new MdlSkinNode() : new MdlTrimeshNode();
        n.Name = name;
        n.Position = pos;
        n.Orientation = Quaternion.Identity;
        n.Scale = 1.0f;
        n.Vertices = new Vector3[verts];
        n.Faces = new MdlFace[1];
        return n;
    }

    private static MdlNode Bone(string name, Vector3 pos)
        => new() { Name = name, Position = pos, Orientation = Quaternion.Identity, Scale = 1.0f };

    /// <summary>Skeleton: rootdummy → torso_g → Rbicep_g → rforearm_g.</summary>
    private static MdlModel BuildSkeleton()
    {
        var root = Bone("rootdummy", new Vector3(0, 0, 1.2f));
        var torso = Bone("torso_g", new Vector3(0, 0, 0));
        var bicep = Bone("Rbicep_g", new Vector3(0.2f, 0, 0.35f));
        var fore = Bone("rforearm_g", new Vector3(0.04f, 0, -0.30f));
        bicep.Children.Add(fore); fore.Parent = bicep;
        torso.Children.Add(bicep); bicep.Parent = torso;
        root.Children.Add(torso); torso.Parent = root;
        return new MdlModel { Name = "pmh0", GeometryRoot = root };
    }

    /// <summary>Robe: rootdummy → torso_g → rbicep_g → rforearm_g, plus a 'coat' skin child.</summary>
    private static MdlModel BuildRobe()
    {
        var root = Bone("rootdummy", new Vector3(0, 0, 1.2f));
        var torso = Mesh("torso_g", new Vector3(0, 0, 0), 27);
        var bicep = Mesh("rbicep_g", new Vector3(0.2f, 0, 0.35f), 33);
        var fore = Mesh("rforearm_g", new Vector3(0.04f, 0, -0.30f), 27);
        bicep.Children.Add(fore); fore.Parent = bicep;
        torso.Children.Add(bicep); bicep.Parent = torso;
        var coat = Mesh("coat", new Vector3(0, 0, 1.21f), 324, skin: true);
        root.Children.Add(torso); torso.Parent = root;
        root.Children.Add(coat); coat.Parent = root;
        return new MdlModel { Name = "pmh0_robe186", GeometryRoot = root };
    }

    private static MdlPartComposer MakeComposer(MdlModel skeleton, MdlModel robe)
    {
        var mock = new MockGameDataService(includeSampleData: false);
        return new MdlPartComposer(mock, (resRef, _) => resRef switch
        {
            "pmh0" => skeleton,
            "pmh0_robe186" => robe,
            _ => null,
        });
    }

    [Fact]
    public void Robe_GraftsForearmAtItsOwnChainPosition_NotCollapsedToTorso()
    {
        var composer = MakeComposer(BuildSkeleton(), BuildRobe());

        var composite = composer.Compose("pmh0", new[] { ("robe", "pmh0_robe186") }, adjustSeams: false);

        Assert.NotNull(composite);
        var fore = MdlPartComposer.FindBoneByName(composite!.GeometryRoot!, "rforearm_g");
        Assert.NotNull(fore);

        // World Z of the robe forearm via its own chain: root(1.2) + torso(0) + bicep(0.35) + fore(-0.30)
        var world = MdlPartComposer.GetMeshWorldTransform(fore!);
        Assert.True(Matrix4x4.Decompose(world, out _, out _, out var t));
        Assert.Equal(1.25f, t.Z, 3); // 1.2 + 0 + 0.35 - 0.30
        // If it had been collapsed onto the torso bone (the old bug) Z would be ~1.2, not 1.25.
    }

    [Fact]
    public void Robe_PreservesCoatAsSkinMesh()
    {
        var composer = MakeComposer(BuildSkeleton(), BuildRobe());

        var composite = composer.Compose("pmh0", new[] { ("robe", "pmh0_robe186") }, adjustSeams: false);

        var coat = MdlPartComposer.FindBoneByName(composite!.GeometryRoot!, "coat");
        Assert.NotNull(coat);
        Assert.IsType<MdlSkinNode>(coat); // clone preserves skin type (#1989)
    }
}

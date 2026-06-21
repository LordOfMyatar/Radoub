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

    /// <summary>
    /// After grafting, a robe skin's BoneNodes must point at the ROBE's grafted bone clones, not
    /// the skeleton's same-named bones — otherwise skin deformation uses the wrong bind and the
    /// robe explodes under animation (#2399). The robe and skeleton both have a "torso_g".
    /// </summary>
    [Fact]
    public void Robe_BindsSkinBonesToGraftedClones_NotSkeletonBones()
    {
        var skeleton = BuildSkeleton();
        var robe = BuildRobe();
        // Give the coat skin a slot→name table referencing torso_g (a name the skeleton ALSO owns).
        var coatSrc = (MdlSkinNode)FindChild(robe.GeometryRoot!, "coat")!;
        coatSrc.BoneNodeNames = new[] { "torso_g" };
        coatSrc.NodeToBoneMap = new short[] { -1, 0 }; // not used by composer, present for realism
        coatSrc.BoneQuaternions = new System.Numerics.Quaternion[1];
        coatSrc.BoneTranslations = new Vector3[1];

        var composer = MakeComposer(skeleton, robe);
        var composite = composer.Compose("pmh0", new[] { ("robe", "pmh0_robe186") }, adjustSeams: false);

        var coat = (MdlSkinNode)MdlPartComposer.FindBoneByName(composite!.GeometryRoot!, "coat")!;
        Assert.Single(coat.BoneNodes);
        var boundTorso = coat.BoneNodes[0];
        Assert.NotNull(boundTorso);

        // The bound torso_g must be the robe's grafted clone (a mesh — robe authored torso_g as a
        // mesh), NOT the skeleton's torso_g bone node. Decisive discriminator: type differs.
        Assert.IsAssignableFrom<MdlTrimeshNode>(boundTorso);
        Assert.False(boundTorso!.GetType() == typeof(MdlNode),
            "skin bound to the skeleton's plain bone node instead of the robe's grafted torso_g");
    }

    private static MdlNode? FindChild(MdlNode node, string name)
    {
        if (node.Name == name) return node;
        foreach (var c in node.Children) { var r = FindChild(c, name); if (r != null) return r; }
        return null;
    }
}

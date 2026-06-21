using System.Numerics;
using Radoub.Formats.Mdl;
using Xunit;

namespace Radoub.Formats.Tests.Mdl;

/// <summary>
/// Tests for SkinBoneResolver — maps a skin mesh's per-vertex bone SLOT indices to the
/// animated bone NODE NAMES, via NodeToBoneMap inverted against the model's depth-first node
/// order. Empirically validated against pfh0_robe003 (slot 5 → torso_g, slot 9 → lbicep_g, ...).
/// </summary>
public class SkinBoneResolverTests
{
    private static MdlNode Node(string name) => new() { Name = name };

    [Fact]
    public void ResolveBoneNames_MapsSlotToNodeNameViaNodeToBoneMap()
    {
        // Depth-first node order (index → name). NodeToBoneMap[i] = bone slot for node i (or -1).
        // Mirrors the validated pfh0_robe003 case in miniature:
        //   node[0]=root(-1), node[1]=rootdummy(-1), node[2]=torso_g(slot1), node[3]=pelvis_g(slot0)
        var nodes = new[] { Node("root"), Node("rootdummy"), Node("torso_g"), Node("pelvis_g") };
        var nodeToBoneMap = new short[] { -1, -1, 1, 0 };
        int boneCount = 2;

        var names = SkinBoneResolver.ResolveBoneNames(nodes, nodeToBoneMap, boneCount);

        Assert.Equal(2, names.Length);
        Assert.Equal("pelvis_g", names[0]); // slot 0
        Assert.Equal("torso_g", names[1]);  // slot 1
    }

    [Fact]
    public void ResolveBoneNames_SlotWithNoNode_IsEmptyString()
    {
        var nodes = new[] { Node("root"), Node("torso_g") };
        var nodeToBoneMap = new short[] { -1, 0 };
        int boneCount = 3; // slots 1 and 2 have no node referencing them

        var names = SkinBoneResolver.ResolveBoneNames(nodes, nodeToBoneMap, boneCount);

        Assert.Equal("torso_g", names[0]);
        Assert.Equal(string.Empty, names[1]);
        Assert.Equal(string.Empty, names[2]);
    }

    [Fact]
    public void ResolveBoneNames_EmptyMap_ReturnsAllEmpty()
    {
        var names = SkinBoneResolver.ResolveBoneNames(new[] { Node("root") }, System.Array.Empty<short>(), 2);

        Assert.Equal(2, names.Length);
        Assert.All(names, n => Assert.Equal(string.Empty, n));
    }
}

using System.Linq;
using Radoub.Formats.Mdl;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// ASCII MDL skin-weight parsing (#2552). The ASCII `weights` block references bones by NAME
/// (e.g. "Wolf_ribcage 0.5 Wolf_pelvis 0.5"), not by index. The reader previously ran ParseInt on
/// the name → 0, so every vertex weighted to bogus slot 0 and BoneNodeNames stayed empty. With no
/// slot→bone-name bridge the renderer disabled skinning and ASCII creatures (blinkdog) rendered
/// frozen. These tests pin name → slot-index resolution.
/// </summary>
public class MdlAsciiSkinWeightTests
{
    private const string SkinModel = @"
newmodel critter
setsupermodel critter NULL
beginmodelgeom critter
node dummy critter
  parent NULL
  node dummy Wolf_pelvis
    parent critter
  endnode
  node dummy Wolf_ribcage
    parent Wolf_pelvis
  endnode
  node skin body
    parent critter
    verts 3
      0 0 0
      1 0 0
      0 1 0
    weights 3
      Wolf_ribcage 1.0
      Wolf_ribcage 0.5 Wolf_pelvis 0.5
      Wolf_pelvis 1.0
  endnode
endnode
endmodelgeom critter
donemodel critter
";

    private static MdlSkinNode LoadSkin()
    {
        var model = new MdlAsciiReader().Parse(SkinModel);
        return model.GetMeshNodes().OfType<MdlSkinNode>().Single();
    }

    [Fact]
    public void Parse_WeightsByName_PopulatesBoneNodeNames()
    {
        var skin = LoadSkin();

        // Two distinct bones referenced across the block.
        Assert.Contains("Wolf_ribcage", skin.BoneNodeNames);
        Assert.Contains("Wolf_pelvis", skin.BoneNodeNames);
    }

    [Fact]
    public void Parse_WeightsByName_StoresSlotIndicesNotZero()
    {
        var skin = LoadSkin();
        Assert.Equal(3, skin.BoneWeights.Length);

        // Resolve each vertex's primary bone back to a name via the slot map.
        string Name(int slot) => slot >= 0 && slot < skin.BoneNodeNames.Length ? skin.BoneNodeNames[slot] : "";

        // Vertex 0: 100% Wolf_ribcage
        Assert.Equal("Wolf_ribcage", Name(skin.BoneWeights[0].Bone0));
        Assert.Equal(1.0f, skin.BoneWeights[0].Weight0, 3);

        // Vertex 1: ribcage 0.5 + pelvis 0.5 — two distinct slots, not both 0.
        var v1 = skin.BoneWeights[1];
        Assert.NotEqual(v1.Bone0, v1.Bone1);
        Assert.Equal("Wolf_ribcage", Name(v1.Bone0));
        Assert.Equal("Wolf_pelvis", Name(v1.Bone1));

        // Vertex 2: 100% Wolf_pelvis
        Assert.Equal("Wolf_pelvis", Name(skin.BoneWeights[2].Bone0));
    }

    [Fact]
    public void Parse_BoneNodeNames_AreUniqueAndStable()
    {
        var skin = LoadSkin();
        // No duplicate slots for the same bone name.
        Assert.Equal(skin.BoneNodeNames.Distinct().Count(), skin.BoneNodeNames.Length);
    }

    // A skin whose tverts force a split-vertex unroll: the bone-weight array must be remapped to the
    // unrolled vertex count, or the renderer's "BoneWeights.Length == Vertices.Length" skinning guard
    // fails and the creature renders frozen (blinkdog: 199 weights vs 279 unrolled verts). (#2552)
    private const string SkinNeedingUnroll = @"
newmodel critter
beginmodelgeom critter
node dummy critter
  parent NULL
  node dummy Wolf_pelvis
    parent critter
  endnode
  node skin body
    parent critter
    verts 3
      0 0 0
      1 0 0
      0 1 0
    tverts 4
      0 0
      1 0
      0 1
      0.5 0.5
    faces 2
      0 1 2 0 0 1 2 0
      0 2 1 0 0 3 1 0
    weights 3
      Wolf_pelvis 1.0
      Wolf_pelvis 1.0
      Wolf_pelvis 1.0
  endnode
endnode
endmodelgeom critter
donemodel critter
";

    [Fact]
    public void Parse_SkinUnroll_RemapsBoneWeightsToVertexCount()
    {
        var model = new MdlAsciiReader().Parse(SkinNeedingUnroll);
        var skin = model.GetMeshNodes().OfType<MdlSkinNode>().Single();

        // Unroll split vertex 0 (used with tvert 0 and tvert 3) into two unified vertices.
        Assert.True(skin.Vertices.Length > 3, "expected unroll to add vertices");
        Assert.Equal(skin.Vertices.Length, skin.BoneWeights.Length);
    }
}

using System.Linq;
using Radoub.Formats.Mdl;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// ASCII MDL animation-node parsing (#2552). Canine creatures (blinkdog) inherit animations from
/// an ASCII supermodel (canine_a); the ASCII reader previously skipped animation node bodies
/// (SkipToEndNode), so every animation parsed with GeometryRoot=null and the preview could never
/// build a pose. These tests pin the keyframe-controller parsing that fixes that.
/// </summary>
public class MdlAsciiAnimationParsingTests
{
    private const string ModelWithAnimatedNode = @"
newmodel critter
setsupermodel critter NULL
classification character
beginmodelgeom critter
node dummy critter
  parent NULL
  position 0 0 0
  node dummy pelvis
    parent critter
    position 0 0 1
  endnode
endnode
endmodelgeom critter
newanim cpause1 critter
  length 2.0
  transtime 0.25
  animroot critter
  node dummy pelvis
    parent critter
    positionkey
      0.0 0 0 1
      1.0 0 0 2
    endlist
    orientationkey
      0.0 0 0 1 0.5
    endlist
  endnode
doneanim cpause1 critter
donemodel critter
";

    [Fact]
    public void Parse_AnimationWithKeyframes_PopulatesGeometryRoot()
    {
        var model = new MdlAsciiReader().Parse(ModelWithAnimatedNode);

        var anim = Assert.Single(model.Animations);
        Assert.Equal("cpause1", anim.Name);
        Assert.NotNull(anim.GeometryRoot);
    }

    [Fact]
    public void Parse_AnimationNode_CarriesPositionAndOrientationKeyframes()
    {
        var model = new MdlAsciiReader().Parse(ModelWithAnimatedNode);
        var anim = model.Animations.Single();

        var pelvis = FindNode(anim.GeometryRoot!, "pelvis");
        Assert.NotNull(pelvis);

        // Two position keys at t=0 and t=1.
        Assert.Equal(2, pelvis!.PositionTimes.Length);
        Assert.Equal(0.0f, pelvis.PositionTimes[0]);
        Assert.Equal(1.0f, pelvis.PositionTimes[1]);
        Assert.Equal(2, pelvis.PositionValues.Length);
        Assert.Equal(2f, pelvis.PositionValues[1].Z);

        // One orientation key.
        Assert.Single(pelvis.OrientationTimes);
        Assert.Single(pelvis.OrientationValues);
    }

    [Fact]
    public void Parse_AnimatedNode_IsDetectedAsAnimated()
    {
        // The preview treats a node with >1 position/orientation/scale key as "animated"
        // (BuildPoseRecursive). The pelvis has 2 position keys, so it must qualify.
        var model = new MdlAsciiReader().Parse(ModelWithAnimatedNode);
        var pelvis = FindNode(model.Animations.Single().GeometryRoot!, "pelvis");

        Assert.True(pelvis!.PositionTimes.Length > 1);
    }

    private static MdlNode? FindNode(MdlNode node, string name)
    {
        if (string.Equals(node.Name, name, System.StringComparison.OrdinalIgnoreCase))
            return node;
        foreach (var c in node.Children)
        {
            var found = FindNode(c, name);
            if (found != null) return found;
        }
        return null;
    }
}

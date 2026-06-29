using Radoub.Formats.Mdl;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Idle-pose-at-rest heuristic (#2619). A model with a DanglyNode mane/cloth whose parent chain is
/// animated should auto-play its idle so the dangly drapes instead of sitting flat at bind pose.
/// </summary>
public class ModelIdlePoseHeuristicTests
{
    private static MdlNode Bone(string name, params MdlNode[] children)
    {
        var n = new MdlNode { Name = name };
        foreach (var c in children) { c.Parent = n; n.Children.Add(c); }
        return n;
    }

    private static MdlDanglyNode Dangly(string name)
        => new() { Name = name, NodeType = MdlNodeType.Dangly };

    private static MdlAnimation AnimKeyframing(string nodeName)
    {
        var animNode = new MdlNode
        {
            Name = nodeName,
            OrientationTimes = new[] { 0f, 0.5f },
            OrientationValues = new[] { System.Numerics.Quaternion.Identity, System.Numerics.Quaternion.Identity },
        };
        return new MdlAnimation { Name = "cpause1", Length = 1f, GeometryRoot = animNode };
    }

    [Fact]
    public void NullModel_False()
        => Assert.False(ModelIdlePoseHeuristic.HasAnimatedDanglyMesh(null));

    [Fact]
    public void NoDanglyMesh_False()
    {
        var model = new MdlModel { GeometryRoot = Bone("root", new MdlTrimeshNode { Name = "body" }) };
        model.Animations.Add(AnimKeyframing("root"));
        Assert.False(ModelIdlePoseHeuristic.HasAnimatedDanglyMesh(model));
    }

    [Fact]
    public void DanglyMesh_ButNoAnimationTouchesItsChain_False()
    {
        // Mane hangs off Cat_head, but the only animation keyframes an unrelated node.
        var mane = Dangly("Mane");
        var model = new MdlModel { GeometryRoot = Bone("root", Bone("Cat_head", mane)) };
        model.Animations.Add(AnimKeyframing("SomeOtherBone"));
        Assert.False(ModelIdlePoseHeuristic.HasAnimatedDanglyMesh(model));
    }

    [Fact]
    public void DanglyMesh_WithAnimatedParentChain_True()
    {
        var mane = Dangly("Mane");
        var model = new MdlModel { GeometryRoot = Bone("root", Bone("Cat_head", mane)) };
        model.Animations.Add(AnimKeyframing("Cat_head")); // idle poses the mane's parent
        Assert.True(ModelIdlePoseHeuristic.HasAnimatedDanglyMesh(model));
    }

    [Fact]
    public void DanglyMesh_NoAnimations_False()
    {
        var mane = Dangly("Mane");
        var model = new MdlModel { GeometryRoot = Bone("root", Bone("Cat_head", mane)) };
        Assert.False(ModelIdlePoseHeuristic.HasAnimatedDanglyMesh(model));
    }
}

using System.Linq;
using PlaceableEditor.Services;
using Radoub.Formats.Mdl;
using Xunit;

namespace PlaceableEditor.Tests.Services;

/// <summary>
/// Placeable supermodel animation merge (#2595). Some placeables (e.g. tnp_list02 → tnp_list01,
/// zlc_ccp_b93 → plc_a07) declare their open/close/on/off state animations only in a SUPERMODEL.
/// PlaceableModelLoader previously skipped supermodel merging ("placeables are static"), so the
/// state selector found no animations and was hidden — the reported missing-controls symptom.
/// SuperModelAnimationMerger walks the chain and appends inherited animations by name.
/// </summary>
public class SuperModelAnimationMergeTests
{
    private static MdlModel ModelWith(string superModel, params string[] animationNames)
    {
        var model = new MdlModel { SuperModel = superModel };
        foreach (var name in animationNames)
            model.Animations.Add(new MdlAnimation { Name = name, Length = 1f });
        return model;
    }

    [Fact]
    public void Merge_PullsStateAnimationsFromSupermodel()
    {
        var leaf = ModelWith("parent"); // no own animations
        var parent = ModelWith("NULL", "open", "close");

        SuperModelAnimationMerger.Merge(leaf, name =>
            string.Equals(name, "parent", System.StringComparison.OrdinalIgnoreCase) ? parent : null);

        var names = leaf.Animations.Select(a => a.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "close", "open" }, names);
    }

    [Fact]
    public void Merge_DoesNotOverrideOwnAnimations()
    {
        var leaf = ModelWith("parent", "open");
        var parent = ModelWith("NULL", "open", "close");

        SuperModelAnimationMerger.Merge(leaf, name => name == "parent" ? parent : null);

        // "open" stays the leaf's own; "close" is inherited. No duplicate "open".
        Assert.Equal(2, leaf.Animations.Count);
        Assert.Single(leaf.Animations, a => a.Name == "open");
        Assert.Single(leaf.Animations, a => a.Name == "close");
    }

    [Fact]
    public void Merge_WalksMultiLevelChain()
    {
        var leaf = ModelWith("mid");
        var mid = ModelWith("root", "on");
        var root = ModelWith("NULL", "off");

        SuperModelAnimationMerger.Merge(leaf, name => name switch
        {
            "mid" => mid,
            "root" => root,
            _ => null,
        });

        var names = leaf.Animations.Select(a => a.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "off", "on" }, names);
    }

    [Fact]
    public void Merge_NoSupermodel_IsNoOp()
    {
        var leaf = ModelWith("NULL", "open");
        SuperModelAnimationMerger.Merge(leaf, _ => null);
        Assert.Single(leaf.Animations);
    }

    [Fact]
    public void Merge_CycleDoesNotInfiniteLoop()
    {
        var a = ModelWith("b", "open");
        var b = ModelWith("a", "close");

        SuperModelAnimationMerger.Merge(a, name => name switch { "a" => a, "b" => b, _ => null });

        // open (own) + close (from b); the a→b→a cycle terminates.
        Assert.Equal(2, a.Animations.Count);
    }

    [Fact]
    public void Merge_MissingSupermodel_IsNoOp()
    {
        var leaf = ModelWith("ghost", "on");
        SuperModelAnimationMerger.Merge(leaf, _ => null); // parent never resolves
        Assert.Single(leaf.Animations);
    }
}

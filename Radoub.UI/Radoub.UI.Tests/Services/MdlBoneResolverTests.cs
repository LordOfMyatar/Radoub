using Radoub.Formats.Mdl;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Tests for MdlBoneResolver (#2541 Phase 1b): the bone-name resolution fallback that lets
/// part composition attach to a custom skeleton whose bones don't follow the <c>_g</c>
/// convention, instead of silently grafting the part at the composite root (the creature's feet).
/// </summary>
public class MdlBoneResolverTests
{
    private static MdlNode Skeleton(params string[] boneNames)
    {
        var root = new MdlNode { Name = "rootdummy" };
        MdlNode parent = root;
        foreach (var name in boneNames)
        {
            var bone = new MdlNode { Name = name };
            bone.Parent = parent;
            parent.Children.Add(bone);
            parent = bone; // chain them so we exercise recursive search
        }
        return root;
    }

    [Fact]
    public void Resolve_ExactConventionalBone_PreferredFirst()
    {
        var root = Skeleton("torso_g", "neck_g", "head_g");
        var result = MdlBoneResolver.Resolve(root, "head");

        Assert.NotNull(result.Bone);
        Assert.Equal("head_g", result.Bone!.Name);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public void Resolve_CustomSkeleton_BestMatchByStem()
    {
        // Custom skeleton names the head bone "Head" (no _g). The conventional "head_g" misses;
        // best-match should still find it by the "head" stem rather than returning null.
        var root = Skeleton("Torso", "Neck", "Head");
        var result = MdlBoneResolver.Resolve(root, "head");

        Assert.NotNull(result.Bone);
        Assert.Equal("Head", result.Bone!.Name);
        Assert.True(result.UsedFallback);
    }

    [Fact]
    public void Resolve_CustomSkeleton_BestMatchForSidedPart()
    {
        // "bicepl" -> conventional "lbicep_g". A custom skeleton calls it "L_Bicep".
        var root = Skeleton("Torso", "L_Bicep", "R_Bicep");
        var result = MdlBoneResolver.Resolve(root, "bicepl");

        Assert.NotNull(result.Bone);
        Assert.Equal("L_Bicep", result.Bone!.Name);
        Assert.True(result.UsedFallback);
    }

    [Fact]
    public void Resolve_NoMatch_ReturnsNullBone_NotRoot()
    {
        // A skeleton with no head-like bone must NOT silently return the root (feet graft);
        // it returns a null bone so the caller can log and fall back deliberately.
        var root = Skeleton("Torso", "Pelvis");
        var result = MdlBoneResolver.Resolve(root, "head");

        Assert.Null(result.Bone);
    }

    [Fact]
    public void Resolve_DoesNotMatchUnrelatedBone()
    {
        // "head" must not best-match "headband_g" only if a real head bone is absent — but it
        // also must not match a clearly-unrelated bone like "pelvis_g".
        var root = Skeleton("Torso", "Pelvis_g");
        var result = MdlBoneResolver.Resolve(root, "head");

        Assert.Null(result.Bone);
    }

    [Fact]
    public void Resolve_DoesNotSubstringMatchCompoundBone()
    {
        // A skeleton with a "headband_g" accessory bone but NO real head bone must NOT graft the
        // head onto the headband (the substring trap). Exact-stem match is required first.
        var root = Skeleton("Torso", "headband_g");
        var result = MdlBoneResolver.Resolve(root, "head");

        Assert.Null(result.Bone);
    }

    [Fact]
    public void Resolve_ExactStemBeatsEarlierSubstringMatch()
    {
        // "headband" appears BEFORE the real "Head" in traversal order. The matcher must prefer the
        // exact-stem bone ("Head" -> normalized "head") over the earlier substring hit ("headband").
        var root = Skeleton("headband", "Head");
        var result = MdlBoneResolver.Resolve(root, "head");

        Assert.NotNull(result.Bone);
        Assert.Equal("Head", result.Bone!.Name);
        Assert.True(result.UsedFallback);
    }
}

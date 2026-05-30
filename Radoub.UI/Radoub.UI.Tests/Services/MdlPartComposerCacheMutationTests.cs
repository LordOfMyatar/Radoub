using System.Numerics;
using Radoub.Formats.Common;
using Radoub.Formats.Mdl;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Regression tests for #1735 — repeated composition must not corrupt the cached source models.
///
/// <see cref="ModelService"/> caches parsed <see cref="MdlModel"/> instances by ResRef and returns
/// the SAME instance on every call. The composer previously reparented part trimeshes directly onto
/// the cached skeleton's bones (<c>bone.Children.Add(node)</c>) and reused the cached skeleton's
/// <c>GeometryRoot</c> as the composite root. So each render appended parts onto the cached skeleton
/// and nudged cached part meshes — the next render of the same skeleton inherited the prior parts and
/// re-applied the seam nudge, drifting worse every pass. The user observed Brownie/elf "getting worse
/// and worse" when toggling races (parts not released from memory).
///
/// These tests drive the composer twice through a SHARED model cache (mirroring ModelService) and
/// assert the second composite matches the first — stable mesh count and stable positions.
/// </summary>
public class MdlPartComposerCacheMutationTests
{
    private static MockGameDataService Game() => new(includeSampleData: false);

    /// <summary>Build a fresh skeleton model with a head_g bone at world Z=1.5.</summary>
    private static MdlModel MakeSkeleton()
    {
        var root = new MdlNode { Name = "skeleton_root", Orientation = Quaternion.Identity, Scale = 1.0f };
        var headBone = new MdlNode
        {
            Name = "head_g",
            Position = new Vector3(0, 0, 1.5f),
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Parent = root,
        };
        root.Children.Add(headBone);
        return new MdlModel { Name = "skel", GeometryRoot = root };
    }

    /// <summary>Build a fresh single-trimesh head part at local origin.</summary>
    private static MdlModel MakeHeadPart()
    {
        var root = new MdlNode { Name = "head_root", Orientation = Quaternion.Identity, Scale = 1.0f };
        var mesh = new MdlTrimeshNode
        {
            Name = "headmesh",
            Position = Vector3.Zero,
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Vertices = new[] { new Vector3(0, 0, 0), new Vector3(0.1f, 0, 0.2f), new Vector3(-0.1f, 0, 0.1f) },
            Faces = Array.Empty<MdlFace>(),
            Parent = root,
        };
        root.Children.Add(mesh);
        return new MdlModel { Name = "head", GeometryRoot = root };
    }

    [Fact]
    public void Compose_RepeatedWithSharedCache_DoesNotAccumulateMeshes()
    {
        // Shared cache mirroring ModelService: same instances returned every call.
        var skeleton = MakeSkeleton();
        var head = MakeHeadPart();
        var cache = new Dictionary<string, MdlModel> { ["skel"] = skeleton, ["head"] = head };

        var composer = new MdlPartComposer(
            Game(),
            (resRef, _) => cache.TryGetValue(resRef, out var m) ? m : null);

        var parts = new[] { ("head", "head") };

        var first = composer.Compose("skel", parts);
        var second = composer.Compose("skel", parts);

        Assert.NotNull(first);
        Assert.NotNull(second);

        int firstMeshes = first!.GetMeshNodes().Count();
        int secondMeshes = second!.GetMeshNodes().Count();

        Assert.Equal(1, firstMeshes);
        Assert.Equal(firstMeshes, secondMeshes); // no accumulation across renders
    }

    [Fact]
    public void Compose_DoesNotMutateCachedSkeleton()
    {
        var skeleton = MakeSkeleton();
        var head = MakeHeadPart();
        var cache = new Dictionary<string, MdlModel> { ["skel"] = skeleton, ["head"] = head };

        var composer = new MdlPartComposer(
            Game(),
            (resRef, _) => cache.TryGetValue(resRef, out var m) ? m : null);

        composer.Compose("skel", new[] { ("head", "head") });

        // Cached skeleton's head_g bone must NOT have gained any children from composition.
        var headBone = skeleton.GeometryRoot!.Children.First(c => c.Name == "head_g");
        Assert.Empty(headBone.Children);
    }

    [Fact]
    public void Compose_DoesNotMutateCachedPartPosition()
    {
        var skeleton = MakeSkeleton();
        var head = MakeHeadPart();
        var cache = new Dictionary<string, MdlModel> { ["skel"] = skeleton, ["head"] = head };

        var composer = new MdlPartComposer(
            Game(),
            (resRef, _) => cache.TryGetValue(resRef, out var m) ? m : null);

        composer.Compose("skel", new[] { ("head", "head") });

        // The cached part's mesh Position must remain at origin (seam nudge must hit a clone).
        var cachedMesh = (MdlTrimeshNode)head.GeometryRoot!.Children.First(c => c.Name == "headmesh");
        Assert.Equal(Vector3.Zero, cachedMesh.Position);
    }
}

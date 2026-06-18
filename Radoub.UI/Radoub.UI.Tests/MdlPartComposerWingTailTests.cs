using System.Linq;
using System.Numerics;
using Radoub.Formats.Mdl;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests the wing/tail supermodel-graft path in MdlPartComposer (#1485). Wings/tail are standalone
/// MDLs (wingmodel/tailmodel.2da MODEL column) with their own bone hierarchy + meshes AND their own
/// animation set named to match the body (cwalkf, cidle, ...). The composer grafts the subtree at
/// the composite root, scales it by WING_TAIL_SCALE, and merges each wing animation's keyframed
/// nodes INTO the same-named composite animation so one playhead drives body + wings in sync.
/// </summary>
public class MdlPartComposerWingTailTests
{
    private static MdlTrimeshNode Mesh(string name, Vector3 pos, int verts)
    {
        var n = new MdlTrimeshNode
        {
            Name = name,
            Position = pos,
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Vertices = new Vector3[verts],
            Faces = new MdlFace[1],
        };
        return n;
    }

    private static MdlNode Bone(string name, Vector3 pos)
        => new() { Name = name, Position = pos, Orientation = Quaternion.Identity, Scale = 1.0f };

    /// <summary>An animation node with position keyframes (so it counts as "keyed").</summary>
    private static MdlNode AnimNode(string name)
        => new()
        {
            Name = name,
            PositionTimes = new[] { 0f, 0.5f },
            PositionValues = new[] { Vector3.Zero, new Vector3(0, 0, 0.1f) },
        };

    /// <summary>
    /// Skeleton: rootdummy(z=1.2) → torso_g → wings (the back attach bone, z=0.25), plus a
    /// 'cwalkf' animation keying torso_g. Mirrors real pmh0, which carries a 'wings' bone on the
    /// upper back and a 'tail' bone on the pelvis that the wing/tail MDLs attach to (#1485).
    /// </summary>
    private static MdlModel BuildSkeleton()
    {
        var root = Bone("rootdummy", new Vector3(0, 0, 1.2f));
        var torso = Bone("torso_g", Vector3.Zero);
        var wingsBone = Bone("wings", new Vector3(0, -0.12f, 0.25f)); // upper back
        torso.Children.Add(wingsBone); wingsBone.Parent = torso;
        root.Children.Add(torso); torso.Parent = root;

        var animRoot = AnimNode("rootdummy");
        animRoot.Children.Add(AnimNode("torso_g"));
        var skeleton = new MdlModel { Name = "pmh0", GeometryRoot = root };
        skeleton.Animations.Add(new MdlAnimation { Name = "cwalkf", Length = 0.5f, GeometryRoot = animRoot });
        return skeleton;
    }

    /// <summary>
    /// Wing MDL: c_wingsdm → wings01 → Bone1 (mesh 'LWing' under Bone1), all near local origin —
    /// authored to be parented under the body's 'wings' attach bone. Carries a 'cwalkf' animation
    /// keying Bone1, plus a 'wflap' animation the body lacks.
    /// </summary>
    private static MdlModel BuildWing()
    {
        var root = Bone("c_wingsdm", new Vector3(0, 0, 0));
        var wings = Bone("wings01", Vector3.Zero);
        var bone1 = Bone("Bone1", new Vector3(0.1f, 0, 0.2f));
        var lwing = Mesh("LWing", Vector3.Zero, 50);
        bone1.Children.Add(lwing); lwing.Parent = bone1;
        wings.Children.Add(bone1); bone1.Parent = wings;
        root.Children.Add(wings); wings.Parent = root;

        var wing = new MdlModel { Name = "c_wingsdm", GeometryRoot = root };

        var walkRoot = AnimNode("c_wingsdm");
        walkRoot.Children.Add(AnimNode("Bone1"));
        wing.Animations.Add(new MdlAnimation { Name = "cwalkf", Length = 0.5f, GeometryRoot = walkRoot });

        var flapRoot = AnimNode("c_wingsdm");
        flapRoot.Children.Add(AnimNode("Bone1"));
        wing.Animations.Add(new MdlAnimation { Name = "wflap", Length = 1.0f, GeometryRoot = flapRoot });

        return wing;
    }

    /// <summary>A minimal single-mesh body part so composition yields a non-null model.</summary>
    private static MdlModel BuildChest()
    {
        var root = Bone("pmh0_chest001", Vector3.Zero);
        var chest = Mesh("chestmesh", Vector3.Zero, 40);
        root.Children.Add(chest); chest.Parent = root;
        return new MdlModel { Name = "pmh0_chest001", GeometryRoot = root };
    }

    private static MdlPartComposer MakeComposer(MdlModel skeleton, MdlModel wing)
    {
        var mock = new MockGameDataService(includeSampleData: false);
        return new MdlPartComposer(mock, (resRef, _) => resRef switch
        {
            "pmh0" => skeleton,
            "c_wingsdm" => wing,
            "pmh0_chest001" => BuildChest(),
            _ => null,
        });
    }

    private static MdlNode? FindAnimNode(MdlNode root, string name)
    {
        if (root.Name == name) return root;
        foreach (var c in root.Children)
        {
            var f = FindAnimNode(c, name);
            if (f != null) return f;
        }
        return null;
    }

    [Fact]
    public void GraftSupermodel_AddsWingMeshesToComposite()
    {
        var composer = MakeComposer(BuildSkeleton(), BuildWing());

        var composite = composer.Compose("pmh0", System.Array.Empty<(string, string)>(),
            adjustSeams: false, supermodels: new[] { ("wings", "c_wingsdm", 1.0f) });

        Assert.NotNull(composite);
        var lwing = MdlPartComposer.FindBoneByName(composite!.GeometryRoot!, "LWing");
        Assert.NotNull(lwing);
    }

    [Fact]
    public void GraftSupermodel_AppliesScaleToGraftedRoot()
    {
        var composer = MakeComposer(BuildSkeleton(), BuildWing());

        var composite = composer.Compose("pmh0", System.Array.Empty<(string, string)>(),
            adjustSeams: false, supermodels: new[] { ("wings", "c_wingsdm", 2.5f) });

        // The grafted wing subtree root ('wings01') carries the WING_TAIL_SCALE factor.
        var wings = MdlPartComposer.FindBoneByName(composite!.GeometryRoot!, "wings01");
        Assert.NotNull(wings);
        Assert.Equal(2.5f, wings!.Scale, 3);
    }

    [Fact]
    public void GraftSupermodel_AttachesUnderNamedBone_NotRoot()
    {
        // #1485 regression: wings must hang off the body's 'wings' attach bone (upper back),
        // not the composite root (feet). The wing's LWing mesh world Z must reflect the bone's
        // back-height chain (root 1.2 + torso 0 + wings 0.25 + Bone1 0.2 = 1.65), not ~0.
        var composer = MakeComposer(BuildSkeleton(), BuildWing());

        var composite = composer.Compose("pmh0", System.Array.Empty<(string, string)>(),
            adjustSeams: false, supermodels: new[] { ("wings", "c_wingsdm", 1.0f) });

        var lwing = MdlPartComposer.FindBoneByName(composite!.GeometryRoot!, "LWing");
        Assert.NotNull(lwing);
        var world = MdlPartComposer.GetMeshWorldTransform(lwing!);
        Assert.True(Matrix4x4.Decompose(world, out _, out _, out var t));
        Assert.True(t.Z > 1.0f, $"wing mesh world Z={t.Z:F3} — expected back height (>1.0), got feet height");
        Assert.Equal(1.65f, t.Z, 2); // 1.2 + 0 + 0.25 + 0.2
    }

    [Fact]
    public void GraftSupermodel_NoAttachBone_FallsBackToRoot()
    {
        // A skeleton without a 'tail' bone still gets the tail grafted (at root) rather than dropped.
        var composer = MakeComposer(BuildSkeleton(), BuildWing());

        var composite = composer.Compose("pmh0", System.Array.Empty<(string, string)>(),
            adjustSeams: false, supermodels: new[] { ("tail", "c_wingsdm", 1.0f) }); // no 'tail' bone in skeleton

        // Still attached (fallback to root), not dropped.
        Assert.NotNull(MdlPartComposer.FindBoneByName(composite!.GeometryRoot!, "LWing"));
    }

    [Fact]
    public void GraftSupermodel_MissingResRef_IsNoOp()
    {
        var composer = MakeComposer(BuildSkeleton(), BuildWing());

        var composite = composer.Compose("pmh0", new[] { ("chest", "pmh0_chest001") },
            adjustSeams: false, supermodels: new[] { ("wings", "does_not_exist", 1.0f) });

        // Composite still builds (from the chest part); no wing mesh, no throw.
        Assert.NotNull(composite);
        Assert.Null(MdlPartComposer.FindBoneByName(composite!.GeometryRoot!, "LWing"));
    }

    [Fact]
    public void GraftSupermodel_MergesWingAnimIntoSameNamedBodyAnim()
    {
        var composer = MakeComposer(BuildSkeleton(), BuildWing());

        var composite = composer.Compose("pmh0", System.Array.Empty<(string, string)>(),
            adjustSeams: false, supermodels: new[] { ("wings", "c_wingsdm", 1.0f) });

        // Exactly one 'cwalkf' animation (not a duplicate entry).
        var walks = composite!.Animations.Where(a => a.Name == "cwalkf").ToList();
        Assert.Single(walks);

        // The wing's Bone1 keyframes are grafted into the body's cwalkf animation tree.
        var bone1Anim = FindAnimNode(walks[0].GeometryRoot!, "Bone1");
        Assert.NotNull(bone1Anim);
        Assert.True(bone1Anim!.PositionTimes.Length > 1); // carries keyframes
    }

    [Fact]
    public void GraftSupermodel_WingOnlyAnimAppendedAsNewEntry()
    {
        var composer = MakeComposer(BuildSkeleton(), BuildWing());

        var composite = composer.Compose("pmh0", System.Array.Empty<(string, string)>(),
            adjustSeams: false, supermodels: new[] { ("wings", "c_wingsdm", 1.0f) });

        // 'wflap' exists only on the wing — appended so it is selectable.
        Assert.Contains(composite!.Animations, a => a.Name == "wflap");
    }
}

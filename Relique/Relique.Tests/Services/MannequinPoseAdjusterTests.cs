using System.Numerics;
using ItemEditor.Services;
using Radoub.Formats.Mdl;
using Radoub.UI.Services;
using Xunit;

namespace ItemEditor.Tests.Services;

public class MannequinPoseAdjusterTests
{
    private static MdlModel BuildSkeleton()
    {
        // root → torso_g → {lbicep_g→lforearm_g, rbicep_g→rforearm_g},
        //         pelvis_g → {lthigh_g→lshin_g, rthigh_g→rshin_g}
        var lforearm = new MdlNode { Name = "lforearm_g", Orientation = Quaternion.Identity };
        var lbicep = new MdlNode { Name = "lbicep_g", Orientation = Quaternion.Identity };
        lbicep.Children.Add(lforearm); lforearm.Parent = lbicep;
        var rforearm = new MdlNode { Name = "rforearm_g", Orientation = Quaternion.Identity };
        var rbicep = new MdlNode { Name = "rbicep_g", Orientation = Quaternion.Identity };
        rbicep.Children.Add(rforearm); rforearm.Parent = rbicep;

        var torso = new MdlNode { Name = "torso_g", Orientation = Quaternion.Identity };
        torso.Children.Add(lbicep); lbicep.Parent = torso;
        torso.Children.Add(rbicep); rbicep.Parent = torso;

        var lshin = new MdlNode { Name = "lshin_g", Orientation = Quaternion.Identity };
        var lthigh = new MdlNode { Name = "lthigh_g", Orientation = Quaternion.Identity };
        lthigh.Children.Add(lshin); lshin.Parent = lthigh;
        var rshin = new MdlNode { Name = "rshin_g", Orientation = Quaternion.Identity };
        var rthigh = new MdlNode { Name = "rthigh_g", Orientation = Quaternion.Identity };
        rthigh.Children.Add(rshin); rshin.Parent = rthigh;
        var pelvis = new MdlNode { Name = "pelvis_g", Orientation = Quaternion.Identity };
        pelvis.Children.Add(lthigh); lthigh.Parent = pelvis;
        pelvis.Children.Add(rthigh); rthigh.Parent = pelvis;

        var root = new MdlNode { Name = "root", Orientation = Quaternion.Identity };
        root.Children.Add(torso); torso.Parent = root;
        root.Children.Add(pelvis); pelvis.Parent = root;

        return new MdlModel { Name = "pmh0", GeometryRoot = root };
    }

    private static MdlNode Find(MdlModel m, string name) =>
        MdlPartComposer.FindBoneByName(m.GeometryRoot!, name)!;

    [Fact]
    public void ApplyRelaxedPose_RotatesBicepBones()
    {
        var model = BuildSkeleton();
        MannequinPoseAdjuster.ApplyRelaxedPose(model);

        Assert.NotEqual(Quaternion.Identity, Find(model, "lbicep_g").Orientation);
        Assert.NotEqual(Quaternion.Identity, Find(model, "rbicep_g").Orientation);
    }

    [Fact]
    public void ApplyRelaxedPose_RotatesThighBones()
    {
        var model = BuildSkeleton();
        MannequinPoseAdjuster.ApplyRelaxedPose(model);

        Assert.NotEqual(Quaternion.Identity, Find(model, "lthigh_g").Orientation);
        Assert.NotEqual(Quaternion.Identity, Find(model, "rthigh_g").Orientation);
    }

    [Fact]
    public void ApplyRelaxedPose_LeftAndRightAreMirrored()
    {
        var model = BuildSkeleton();
        MannequinPoseAdjuster.ApplyRelaxedPose(model);

        var l = Find(model, "lbicep_g").Orientation;
        var r = Find(model, "rbicep_g").Orientation;

        // Mirrored: the right rotation is the conjugate (opposite angle about the same axis)
        // of the left, so combining left with right yields (near) identity.
        var combined = Quaternion.Normalize(l * r);
        Assert.True(QuaternionsApproximatelyEqual(combined, Quaternion.Identity),
            $"expected mirrored rotations to cancel, got {combined}");
    }

    [Fact]
    public void ApplyRelaxedPose_DoesNotRotateUnrelatedBones()
    {
        var model = BuildSkeleton();
        MannequinPoseAdjuster.ApplyRelaxedPose(model);

        // torso, pelvis, root are not adjusted directly.
        Assert.Equal(Quaternion.Identity, Find(model, "torso_g").Orientation);
        Assert.Equal(Quaternion.Identity, Find(model, "pelvis_g").Orientation);
        Assert.Equal(Quaternion.Identity, Find(model, "root").Orientation);
    }

    [Fact]
    public void ApplyRelaxedPose_FlexesForearmAndShinBones()
    {
        var model = BuildSkeleton();
        MannequinPoseAdjuster.ApplyRelaxedPose(model);

        Assert.NotEqual(Quaternion.Identity, Find(model, "lforearm_g").Orientation);
        Assert.NotEqual(Quaternion.Identity, Find(model, "rforearm_g").Orientation);
        Assert.NotEqual(Quaternion.Identity, Find(model, "lshin_g").Orientation);
        Assert.NotEqual(Quaternion.Identity, Find(model, "rshin_g").Orientation);
    }

    [Fact]
    public void ApplyRelaxedPose_AbductsByConfiguredAngle()
    {
        var model = BuildSkeleton();
        MannequinPoseAdjuster.ApplyRelaxedPose(model);

        var expected = Quaternion.CreateFromAxisAngle(
            Vector3.UnitY, MannequinPoseAdjuster.ArmAbductionDegrees * (float)System.Math.PI / 180f);
        var actual = Find(model, "lbicep_g").Orientation;

        Assert.True(QuaternionsApproximatelyEqual(actual, Quaternion.Normalize(expected)),
            $"expected {expected}, got {actual}");
    }

    [Fact]
    public void ApplyRelaxedPose_NullModel_DoesNotThrow()
    {
        MannequinPoseAdjuster.ApplyRelaxedPose(null);
        MannequinPoseAdjuster.ApplyRelaxedPose(new MdlModel { GeometryRoot = null });
    }

    [Fact]
    public void ApplyRelaxedPose_MissingBones_SkippedSilently()
    {
        // Skeleton with only a root — none of the target bones exist.
        var model = new MdlModel { GeometryRoot = new MdlNode { Name = "root" } };
        MannequinPoseAdjuster.ApplyRelaxedPose(model);
        Assert.Equal(Quaternion.Identity, model.GeometryRoot!.Orientation);
    }

    private static bool QuaternionsApproximatelyEqual(Quaternion a, Quaternion b, float tol = 1e-4f)
    {
        // q and -q represent the same rotation.
        bool same = System.MathF.Abs(a.X - b.X) < tol && System.MathF.Abs(a.Y - b.Y) < tol
                 && System.MathF.Abs(a.Z - b.Z) < tol && System.MathF.Abs(a.W - b.W) < tol;
        bool neg = System.MathF.Abs(a.X + b.X) < tol && System.MathF.Abs(a.Y + b.Y) < tol
                 && System.MathF.Abs(a.Z + b.Z) < tol && System.MathF.Abs(a.W + b.W) < tol;
        return same || neg;
    }
}

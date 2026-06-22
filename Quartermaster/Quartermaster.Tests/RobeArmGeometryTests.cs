using System;
using System.Numerics;
using Quartermaster.Services;
using Radoub.Formats.Mdl;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for the #2541 Phase 2 narrow carve-out: a robe should only suppress the creature's own
/// arm parts when the robe actually supplies RENDERABLE arm geometry. Dana's robe
/// (<c>pfh0_robe005</c>) has all arm bone trimeshes Render=false and an armless torso+legs skin,
/// so suppressing the creature arms leaves her armless (#2398/#2116). Blanket suppression of
/// torso/legs is unchanged — this only narrows the arm decision.
/// </summary>
public class RobeArmGeometryTests
{
    private static MdlTrimeshNode Mesh(string name, bool render, int verts = 10)
        => new()
        {
            Name = name,
            Render = render,
            Vertices = verts > 0 ? new Vector3[verts] : Array.Empty<Vector3>(),
            Faces = Array.Empty<MdlFace>(),
        };

    private static MdlModel Robe(MdlNode root) => new() { Name = "robe", GeometryRoot = root, IsBinary = true };

    [Fact]
    public void HasRenderableArmGeometry_True_WhenArmBoneTrimeshRenders()
    {
        // pfh0_robe186 shape: bicep/forearm/hand trimeshes are Render=true.
        var root = new MdlNode { Name = "robe_root" };
        var torso = new MdlNode { Name = "torso_g", Parent = root };
        var bicep = Mesh("lbicep_g", render: true);
        bicep.Parent = torso;
        torso.Children.Add(bicep);
        root.Children.Add(torso);

        Assert.True(RobeArmGeometry.HasRenderableArmGeometry(Robe(root)));
    }

    [Fact]
    public void HasRenderableArmGeometry_False_WhenAllArmBonesRenderFalse()
    {
        // pfh0_robe005 shape: torso+legs skin renders, but every arm bone trimesh is Render=false
        // and no skin slot is weighted to an arm bone.
        var root = new MdlNode { Name = "robe_root" };
        var robeSkin = new MdlSkinNode
        {
            Name = "Robe",
            Render = true,
            Vertices = new Vector3[364],
            Faces = Array.Empty<MdlFace>(),
            // resolved skin bones: legs + torso only — NO arm bones
            BoneNodeNames = new[] { "lshin_g", "rshin_g", "rthigh_g", "pelvis_g", "lthigh_g", "torso_g" },
        };
        robeSkin.Parent = root;
        root.Children.Add(robeSkin);

        var torso = new MdlNode { Name = "torso_g", Parent = root };
        var bicepL = Mesh("lbicep_g", render: false);
        bicepL.Parent = torso;
        torso.Children.Add(bicepL);
        var bicepR = Mesh("rbicep_g", render: false);
        bicepR.Parent = torso;
        torso.Children.Add(bicepR);
        root.Children.Add(torso);

        Assert.False(RobeArmGeometry.HasRenderableArmGeometry(Robe(root)));
    }

    [Fact]
    public void HasRenderableArmGeometry_True_WhenSkinWeightsAnArmBone()
    {
        // A robe whose visible skin IS weighted to arm bones (long-sleeve skin) supplies arms.
        var root = new MdlNode { Name = "robe_root" };
        var armSkin = new MdlSkinNode
        {
            Name = "arms",
            Render = true,
            Vertices = new Vector3[100],
            Faces = Array.Empty<MdlFace>(),
            BoneNodeNames = new[] { "torso_g", "lbicep_g", "lforearm_g" },
        };
        armSkin.Parent = root;
        root.Children.Add(armSkin);

        Assert.True(RobeArmGeometry.HasRenderableArmGeometry(Robe(root)));
    }

    [Fact]
    public void HasRenderableArmGeometry_False_ForNullOrEmptyRobe()
    {
        Assert.False(RobeArmGeometry.HasRenderableArmGeometry(null));
        Assert.False(RobeArmGeometry.HasRenderableArmGeometry(new MdlModel { GeometryRoot = null }));
    }

    [Fact]
    public void HasRenderableArmGeometry_False_WhenOnlyArmlessSkinSurface()
    {
        // pfh0_robe005 signature: the sole renderable surface is a skin weighted to torso/legs
        // but NOT arms → creature must keep its own arms.
        var root = new MdlNode { Name = "robe_root" };
        var torsoSkin = new MdlSkinNode
        {
            Name = "Robe",
            Render = true,
            Vertices = new Vector3[200],
            Faces = Array.Empty<MdlFace>(),
            BoneNodeNames = new[] { "torso_g", "pelvis_g", "lthigh_g", "lshin_g" },
        };
        torsoSkin.Parent = root;
        root.Children.Add(torsoSkin);

        Assert.False(RobeArmGeometry.HasRenderableArmGeometry(Robe(root)));
    }

    [Fact]
    public void HasRenderableArmGeometry_True_WhenRigidFullBodyDrape()
    {
        // pmh0_robe001 signature: a single Render=true rigid 'Robe' TRIMESH that drapes the whole
        // body (X-span 2x the torso) — it visually covers the arms even with no arm-bone geometry.
        // Must report true so blanket suppression stays and we don't get duplicate arms (#2398).
        var root = new MdlNode { Name = "robe_root" };
        var robeMesh = Mesh("Robe", render: true, verts: 465);
        robeMesh.Parent = root;
        root.Children.Add(robeMesh);

        // arm bone trimeshes present but Render=false (animation drivers only)
        var bicep = Mesh("lbicep_g", render: false);
        bicep.Parent = root;
        root.Children.Add(bicep);

        Assert.True(RobeArmGeometry.HasRenderableArmGeometry(Robe(root)));
    }
}

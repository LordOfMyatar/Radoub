using System;
using Radoub.Formats.Mdl;

namespace Quartermaster.Services;

/// <summary>
/// Inspects a robe MDL for RENDERABLE arm geometry (#2541 Phase 2). Some robe variants
/// (e.g. <c>pfh0_robe005</c> — Dana) author their bicep/forearm/hand bone trimeshes as
/// <c>Render=false</c> and ship only a torso+legs skin with no arm-weighted vertices. When
/// <see cref="RobePartSuppression"/> strips the creature's own arm parts on the assumption the
/// robe replaces them, those creatures lose their arms entirely (#2398/#2116).
///
/// This predicate lets suppression keep the creature's arm parts when, and only when, the robe
/// supplies no renderable arm geometry. It is a narrow, evidence-driven carve-out — NOT a general
/// spatial coverage probe (the reference engines replace the whole body rather than compositing,
/// so blanket suppression of torso/legs stays the default). For robes that DO render arms
/// (<c>pfh0_robe186</c>: Render=true arm trimeshes; long-sleeve skins weighted to arm bones) it
/// returns true and suppression behaves exactly as before — no duplicate arms.
/// </summary>
public static class RobeArmGeometry
{
    // Substrings identifying an arm-region bone/mesh name (case-insensitive). Aurora arm bones are
    // lbicep_g/rbicep_g, lforearm_g/rforearm_g, lhand_g/rhand_g, lshoulder_g/rshoulder_g.
    private static readonly string[] ArmKeywords = { "bicep", "forearm", "hand", "shoul" };

    /// <summary>
    /// True if <paramref name="robe"/> provides arm coverage. Conservative by design — it returns
    /// true (suppress creature arms) for everything EXCEPT the specific armless signature, so the
    /// carve-out stays narrow and blanket suppression remains the default:
    ///
    /// <list type="bullet">
    /// <item>A Render=true arm bone trimesh (<c>*bicep_g</c>/<c>*forearm_g</c>/<c>*hand_g</c>) →
    /// renders arms directly (e.g. <c>pfh0_robe186</c>).</item>
    /// <item>A skin mesh weighted to an arm bone → its surface deforms with and covers the arms.</item>
    /// <item>A renderable rigid <c>Robe</c> trimesh → a full-body drape that visually covers the
    /// arms even without arm-bone geometry (e.g. <c>pmh0_robe001</c>, X-span 2× the torso).
    /// Treated as covering to avoid reintroducing duplicate arms.</item>
    /// </list>
    ///
    /// Only the <c>pfh0_robe005</c> signature — the sole renderable surface is a skin weighted to
    /// torso/legs but NOT arms — reports false, so the creature keeps its own arms (#2398/#2116).
    /// </summary>
    public static bool HasRenderableArmGeometry(MdlModel? robe)
    {
        var root = robe?.GeometryRoot;
        if (root == null)
            return false;

        bool hasRenderableRigidMesh = false;
        bool hasRenderableSkin = false;
        bool hasArmCoverage = false;

        void Walk(MdlNode node)
        {
            if (node is MdlSkinNode skin)
            {
                if (skin.Render && skin.Vertices.Length > 0)
                {
                    hasRenderableSkin = true;
                    foreach (var boneName in skin.BoneNodeNames)
                    {
                        if (IsArmName(boneName))
                            hasArmCoverage = true;
                    }
                }
            }
            else if (node is MdlTrimeshNode mesh)
            {
                if (mesh.Render && mesh.Vertices.Length > 0)
                {
                    // A renderable arm bone trimesh covers the arm directly.
                    if (IsArmName(mesh.Name))
                        hasArmCoverage = true;
                    else
                        // A renderable non-arm-named rigid trimesh is a full-body drape (robe001);
                        // assume it covers the arms (conservative — keeps blanket suppression).
                        hasRenderableRigidMesh = true;
                }
            }

            foreach (var child in node.Children)
                Walk(child);
        }
        Walk(root);

        // Arms are covered if any explicit arm geometry exists, OR a full-body rigid drape is the
        // visible surface. Only the skin-only-without-arm-bones case (robe005) returns false.
        return hasArmCoverage || hasRenderableRigidMesh;
    }

    private static bool IsArmName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        foreach (var kw in ArmKeywords)
        {
            if (name.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

using System;
using Radoub.Formats.Mdl;

namespace Radoub.UI.Services;

/// <summary>
/// Maps NWN body-part type names (e.g., "head", "chest", "shol") to the corresponding
/// skeleton bone names (with the "_g" suffix used by Aurora Engine MDL skeletons).
///
/// Used by <see cref="MdlPartComposer"/> to attach part meshes onto the correct bone
/// in a creature or armor mannequin skeleton, so that animation pose lookup walks the
/// parent chain through the supermodel correctly (#2124).
/// </summary>
public static class MdlPartBoneMap
{
    /// <summary>
    /// Default mapping from part type to skeleton bone name.
    /// Unknown part types fall back to <c>{partType}_g</c>.
    /// </summary>
    public static string GetBoneNameForPart(string partType) => partType switch
    {
        "head" => "head_g",
        "neck" => "neck_g",
        "chest" => "torso_g",
        "robe" => "torso_g",
        "pelvis" => "pelvis_g",
        "belt" => "belt_g",
        "shol" => "lshoulder_g",
        "shor" => "rshoulder_g",
        "bicepl" => "lbicep_g",
        "bicepr" => "rbicep_g",
        "forel" => "lforearm_g",
        "forer" => "rforearm_g",
        "handl" => "lhand_g",
        "handr" => "rhand_g",
        "legl" => "lthigh_g",
        "legr" => "rthigh_g",
        "shinl" => "lshin_g",
        "shinr" => "rshin_g",
        "footl" => "lfoot_g",
        "footr" => "rfoot_g",
        _ => partType + "_g"
    };
}

/// <summary>
/// String-formatting helpers for NWN body-part MDL ResRefs.
/// </summary>
public static class MdlPartNaming
{
    /// <summary>
    /// Build a body-part MDL ResRef from a creature/mannequin prefix, part type, and part number.
    /// Format: <c>{prefix}_{partType}{partNumber:D3}</c>, e.g., <c>pmh0_chest005</c>.
    /// </summary>
    public static string BuildBodyPartName(string prefix, string partType, byte partNumber)
        => $"{prefix}_{partType}{partNumber:D3}";
}

/// <summary>Result of resolving a part type to a skeleton bone: the bone (or null) and whether
/// a non-conventional best-match fallback fired (so the caller can log custom-skeleton cases).</summary>
public readonly record struct BoneResolution(MdlNode? Bone, bool UsedFallback);

/// <summary>
/// Resolve a body-part type to a skeleton bone (#2541 Phase 1b). Tries the conventional
/// <c>_g</c> bone name first; if a custom skeleton names its bones differently, falls back to a
/// conservative best-match on the bone-name stem. Returns a null bone (NOT the root) when nothing
/// matches, so the caller logs and falls back deliberately rather than silently grafting the part
/// at the composite root (the creature's feet).
/// </summary>
public static class MdlBoneResolver
{
    /// <summary>
    /// Resolve <paramref name="partType"/> to a bone under <paramref name="root"/> using the
    /// default <see cref="MdlPartBoneMap"/> convention.
    /// </summary>
    public static BoneResolution Resolve(MdlNode root, string partType)
        => ResolveByBoneName(root, MdlPartBoneMap.GetBoneNameForPart(partType));

    /// <summary>
    /// Resolve a bone under <paramref name="root"/> from an already-mapped conventional bone name
    /// (e.g. the output of an injected part→bone delegate). Exact match first, then best-match by
    /// the bone-name stem for custom skeletons.
    /// </summary>
    public static BoneResolution ResolveByBoneName(MdlNode root, string conventional)
    {
        var exact = MdlPartComposer.FindBoneByName(root, conventional);
        if (exact != null)
            return new BoneResolution(exact, UsedFallback: false);

        // Best-match: strip the "_g" suffix to get the part stem (e.g. "lbicep", "head"), normalize
        // away separators/case, and match a bone whose normalized name EQUALS that stem. Equality
        // (not substring containment) so we never mis-attach to a compound bone whose name merely
        // contains the stem — "head" must not grab "headband_g", "tail" must not grab "ponytail".
        // Legitimate custom-skeleton renames ("Head"→head, "L_Bicep"→lbicep) normalize to the exact
        // stem, so equality still resolves them; only accidental superstrings are excluded.
        var stem = Normalize(StripBoneSuffix(conventional));
        if (stem.Length == 0)
            return new BoneResolution(null, UsedFallback: false);

        var match = FindByNormalizedStem(root, stem);
        return new BoneResolution(match, UsedFallback: match != null);
    }

    private static MdlNode? FindByNormalizedStem(MdlNode node, string stem)
    {
        if (string.Equals(Normalize(node.Name), stem, StringComparison.Ordinal))
            return node;

        foreach (var child in node.Children)
        {
            var found = FindByNormalizedStem(child, stem);
            if (found != null)
                return found;
        }
        return null;
    }

    private static string StripBoneSuffix(string boneName)
        => boneName.EndsWith("_g", StringComparison.OrdinalIgnoreCase)
            ? boneName.Substring(0, boneName.Length - 2)
            : boneName;

    /// <summary>Lowercase and drop underscores/spaces so "L_Bicep" and "lbicep" compare equal.</summary>
    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch == '_' || ch == ' ' || ch == '-')
                continue;
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }
}

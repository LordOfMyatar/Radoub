using System;
using System.Collections.Generic;
using System.Numerics;
using Radoub.Formats.Mdl;
using Radoub.UI.Services;

namespace ItemEditor.Services;

/// <summary>
/// Relaxes the armor-preview mannequin's static bind pose so limb armor parts are visible
/// from the default front camera (#2232). The stock skeleton stands "at attention" — arms
/// straight down (hands/forearms occluded by the hips) and legs together. This adjuster
/// abducts the upper arms slightly outward and separates the thighs to roughly hip width,
/// without going to a T-pose.
///
/// Applied only to the Relique armor mannequin — it mutates the composite model's cloned
/// bone hierarchy after composition, so the shared <see cref="MdlPartComposer"/> and other
/// consumers (e.g. Quartermaster creature preview) are unaffected.
///
/// Each adjustment premultiplies a delta rotation in the bone's local frame
/// (<c>orientation = orientation * delta</c>), so meshes attached to the bone and every
/// child bone (forearm, hand; shin, foot) follow the rotation automatically. Angles are
/// intentionally conservative and live here as named constants for easy visual tuning.
/// </summary>
public static class MannequinPoseAdjuster
{
    /// <summary>Upper-arm abduction angle (degrees) — swings arms out from the torso.</summary>
    public const float ArmAbductionDegrees = 18f;

    /// <summary>Thigh separation angle (degrees) — opens the stance to hip width.</summary>
    public const float LegSeparationDegrees = 9f;

    /// <summary>Elbow flexion (degrees) — slight forearm bend so arms aren't locked straight.</summary>
    public const float ElbowFlexionDegrees = 5f;

    /// <summary>Knee flexion (degrees) — slight shin bend so legs aren't locked straight.</summary>
    public const float KneeFlexionDegrees = 4f;

    /// <summary>
    /// Local rotation axis for arm abduction. NWN bicep bones run down the limb; rotating
    /// about the forward (Y) axis swings the arm in the frontal (X-Z) plane — out to the side.
    /// </summary>
    private static readonly Vector3 ArmAbductionAxis = Vector3.UnitY;

    /// <summary>Local rotation axis for thigh separation (same frontal-plane swing).</summary>
    private static readonly Vector3 LegSeparationAxis = Vector3.UnitY;

    /// <summary>
    /// Local rotation axis for elbow/knee flexion. Rotating about the side (X) axis bends the
    /// limb in the sagittal plane — forearm forward, shin back — for a relaxed, non-locked pose.
    /// </summary>
    private static readonly Vector3 FlexionAxis = Vector3.UnitX;

    private static readonly (string Bone, Vector3 Axis, float Degrees)[] Adjustments =
    {
        // Left/right mirrored: opposite signs so both limbs swing outward symmetrically.
        ("lbicep_g", ArmAbductionAxis,  ArmAbductionDegrees),
        ("rbicep_g", ArmAbductionAxis, -ArmAbductionDegrees),
        ("lthigh_g", LegSeparationAxis,  LegSeparationDegrees),
        ("rthigh_g", LegSeparationAxis, -LegSeparationDegrees),

        // Slight elbow/knee bend so limbs aren't locked straight (same sign both sides).
        ("lforearm_g", FlexionAxis, ElbowFlexionDegrees),
        ("rforearm_g", FlexionAxis, ElbowFlexionDegrees),
        ("lshin_g", FlexionAxis, -KneeFlexionDegrees),
        ("rshin_g", FlexionAxis, -KneeFlexionDegrees),
    };

    /// <summary>
    /// Whether any resolved part resref is the robe part (#2596). A robe composes as a rigid,
    /// non-bone-bound coat skin grafted under the composite root, so it cannot follow the
    /// per-bone rotations of the relaxed pose. Relaxing the stance would swing the bone-attached
    /// kept parts (hands, shins, feet) away from the frozen robe — the "arms gone / shins
    /// misaligned" defect. The caller skips <see cref="ApplyRelaxedPose"/> when this is true.
    ///
    /// Detection keys on the part-type token of each resref (<c>{prefix}_robe{NNN}</c>), matching
    /// <c>ItemModelResolver</c>'s "robe" suffix — NOT a substring match, so "pmh0_wardrobe001"
    /// does not false-match.
    /// </summary>
    public static bool ContainsRobePart(IEnumerable<string>? resRefs)
    {
        if (resRefs == null)
            return false;

        foreach (var resRef in resRefs)
        {
            if (string.IsNullOrEmpty(resRef))
                continue;

            // Strip the body prefix ("pmh0_") then the trailing part number; compare the stem.
            var underscore = resRef.IndexOf('_');
            var stem = underscore >= 0 && underscore + 1 < resRef.Length
                ? resRef.Substring(underscore + 1)
                : resRef;
            stem = stem.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            if (stem.Equals("robe", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Apply the relaxed stance in place. No-op if the model has no skeleton root. Bones that
    /// aren't present in this skeleton are skipped silently.
    /// </summary>
    public static void ApplyRelaxedPose(MdlModel? model)
    {
        var root = model?.GeometryRoot;
        if (root == null) return;

        foreach (var (boneName, axis, degrees) in Adjustments)
        {
            var bone = MdlPartComposer.FindBoneByName(root, boneName);
            if (bone == null) continue;

            var delta = Quaternion.CreateFromAxisAngle(
                Vector3.Normalize(axis),
                degrees * (float)System.Math.PI / 180f);

            // Premultiply in the bone's local frame so children inherit the rotation.
            bone.Orientation = Quaternion.Normalize(bone.Orientation * delta);
        }
    }
}

using System.Collections.Generic;
using Radoub.Formats.Mdl;

namespace Radoub.UI.Services;

/// <summary>
/// Decides whether a model should default to a playing idle animation in the preview so its
/// pose-dependent geometry looks right at rest (#2619). DanglyNode meshes (manes, cloth, hair)
/// have no physics in the preview, so at bind pose they sit flat; the engine/toolset shows them
/// draped because an idle animation poses their parent bones. We mirror that by auto-playing the
/// idle when a dangly mesh's ancestor chain is actually keyframed by an animation. Pure (model-only)
/// so it is unit-testable without the GL control. Companion to the emitter heuristic (#2434).
/// </summary>
public static class ModelIdlePoseHeuristic
{
    /// <summary>
    /// True if the model has a DanglyNode mesh whose geometry-tree ancestor chain is keyframed by
    /// some animation — i.e. an idle pose would visibly drape it. False for static dangly meshes
    /// (no animation touches their chain), so we don't needlessly auto-play unrelated models.
    /// </summary>
    public static bool HasAnimatedDanglyMesh(MdlModel? model)
    {
        if (model == null || model.Animations.Count == 0 || model.GeometryRoot == null)
            return false;

        // Ancestor names of every dangly mesh.
        var danglyChainNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        bool anyDangly = false;
        CollectDanglyChains(model.GeometryRoot, danglyChainNames, ref anyDangly);
        if (!anyDangly) return false;

        foreach (var anim in model.Animations)
        {
            if (anim.GeometryRoot == null) continue;
            if (AnimTreeKeyframesAny(anim.GeometryRoot, danglyChainNames))
                return true;
        }
        return false;
    }

    private static void CollectDanglyChains(MdlNode node, HashSet<string> names, ref bool anyDangly)
    {
        if (node is MdlDanglyNode)
        {
            anyDangly = true;
            for (var cur = (MdlNode?)node; cur != null; cur = cur.Parent)
                if (!string.IsNullOrEmpty(cur.Name)) names.Add(cur.Name);
        }
        foreach (var child in node.Children)
            CollectDanglyChains(child, names, ref anyDangly);
    }

    private static bool AnimTreeKeyframesAny(MdlNode animNode, HashSet<string> names)
    {
        bool keyed = animNode.PositionTimes.Length > 1 || animNode.OrientationTimes.Length > 1
            || animNode.ScaleTimes.Length > 1;
        if (keyed && !string.IsNullOrEmpty(animNode.Name) && names.Contains(animNode.Name))
            return true;
        foreach (var child in animNode.Children)
            if (AnimTreeKeyframesAny(child, names))
                return true;
        return false;
    }
}

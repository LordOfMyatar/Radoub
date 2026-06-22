// At-rest emitter gating (#2544 / #2439). NWN placeable destruction effects (wood debris, fire)
// are emitters keyed to a "die" animation: their birthrate is 0 in the "default" (at-rest) pose
// and only fires during destruction. The model preview shows the placeable at rest, so those
// emitters must be silent. The model leads: the signal is the emitter's birthrate in the "default"
// animation, not the static MDL-header peak. A model with no "default" animation gating an emitter
// (e.g. a creature like c_fairy) renders it from the header as before. Pure/static — unit-testable
// without a GL context.

using Radoub.Formats.Mdl;

namespace Radoub.UI.Particles;

/// <summary>
/// Decides whether an emitter should render in the preview's at-rest (default) state, based on
/// whether the model's "default" animation actually drives it. (#2544)
/// </summary>
public static class EmitterAnimationGate
{
    /// <summary>
    /// True if the named emitter should render at rest. False when the model has a "default"
    /// animation whose copy of the emitter keys its birthrate to ~0 — the destruction-effect case
    /// (the emitter only fires during a die/damage animation). When no "default" animation drives
    /// the emitter, falls back to the static-header birthrate (render unless that is ~0).
    /// </summary>
    public static bool ShouldRenderAtRest(MdlModel model, string emitterName)
    {
        // The at-rest pose is the "default" animation. If it carries a copy of this emitter, its
        // keyed birthrate is the authoritative at-rest emission — 0 means "off until destroyed".
        var defaultAnim = FindAnimation(model, "default");
        if (defaultAnim?.GeometryRoot != null)
        {
            var atRest = FindEmitter(defaultAnim.GeometryRoot, emitterName);
            if (atRest != null)
                return atRest.BirthRate > 0f;
        }

        // No default-animation gating for this emitter: use the static header. Render unless the
        // emitter is authored with no output at all.
        var headerEmitter = FindEmitter(model.GeometryRoot, emitterName);
        return headerEmitter == null || headerEmitter.BirthRate > 0f;
    }

    private static MdlAnimation? FindAnimation(MdlModel model, string name)
    {
        foreach (var anim in model.Animations)
            if (string.Equals(anim.Name, name, System.StringComparison.OrdinalIgnoreCase))
                return anim;
        return null;
    }

    private static MdlEmitterNode? FindEmitter(MdlNode? node, string name)
    {
        if (node == null) return null;
        if (node is MdlEmitterNode emitter &&
            string.Equals(node.Name, name, System.StringComparison.OrdinalIgnoreCase))
            return emitter;
        foreach (var child in node.Children)
        {
            var found = FindEmitter(child, name);
            if (found != null) return found;
        }
        return null;
    }
}

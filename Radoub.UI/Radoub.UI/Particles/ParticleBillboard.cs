// Particle quad orientation per emitter render mode (#2434). Render-mode semantics from the
// NWN1 EE MDL emitter docs (nwn.wiki "MDL Ascii Emitter Nodes"):
//   Normal / Linked          - quad constantly faces the camera
//   Billboard to Local Z     - quad faces "the way it came out", locked to the emitter's local
//                              frame, ignoring the camera
//   Billboard to World Z     - quad lies parallel to the world ground plane
// Only the camera and local-Z cases are implemented here; other modes fall back to camera-facing
// (the MVP approximation logged in ModelPreviewGLControl). (#2434)

using System.Numerics;

namespace Radoub.UI.Particles;

/// <summary>
/// Computes the world-space right/up axes a particle quad expands along, given the render mode,
/// the camera basis, and the emitter node's world rotation. Pure/static so the orientation logic
/// is unit-testable without a GL context.
/// </summary>
public static class ParticleBillboard
{
    /// <summary>
    /// Returns the (right, up) axes for a particle quad. For camera-facing modes these are the
    /// supplied camera axes; for <see cref="ParticleRenderMode.BillboardLocalZ"/> they are the
    /// emitter node's local X/Y rotated into world space, so the quad stays fixed in the emitter's
    /// frame instead of turning to face the camera. (#2434)
    /// </summary>
    public static (Vector3 right, Vector3 up) QuadBasis(
        ParticleRenderMode mode, Vector3 camRight, Vector3 camUp, Quaternion emitterWorldRotation)
    {
        switch (mode)
        {
            case ParticleRenderMode.BillboardLocalZ:
                // Quad locked to the emitter's local frame: local +X / +Y rotated into world space.
                // The quad normal is local +Z, so the sprite faces the way the emitter points.
                var right = Vector3.Transform(Vector3.UnitX, emitterWorldRotation);
                var up = Vector3.Transform(Vector3.UnitY, emitterWorldRotation);
                return (right, up);

            // Camera-facing (Normal/Linked) and every not-yet-implemented mode default to the
            // camera basis (MVP approximation; the caller logs the unsupported ones).
            case ParticleRenderMode.Billboard:
            default:
                return (camRight, camUp);
        }
    }
}

// Particle quad orientation per emitter render mode. Render-mode semantics from the NWN1 EE MDL
// emitter docs (nwn.wiki "MDL Ascii Emitter Nodes") and the MIT reference viewers (rollnw,
// nwn_mdl_webviewer):
//   Normal / Linked          - quad constantly faces the camera
//   Billboard to Local Z     - quad faces "the way it came out", locked to the emitter's local
//                              frame, ignoring the camera (#2434)
//   Billboard to World Z     - quad lies flat, parallel to the world ground plane (normal = world +Z)
//   Aligned to World Z       - quad's up axis is locked to world +Z (a standing card)
//   Aligned to particle dir  - quad's up axis follows the particle's velocity direction
//   Motion blur (Stretched)  - like velocity-aligned, but the up axis grows with speed (a streak)
// Beam/Mesh/LinkedChain have no faithful billboard form yet and fall back to camera-facing
// (the caller logs them). (#2544 / #2450)

using System.Numerics;

namespace Radoub.UI.Particles;

/// <summary>
/// Computes the world-space right/up axes a particle quad expands along, given the render mode,
/// the camera basis, the emitter node's world rotation, and (for velocity-dependent modes) the
/// particle's world velocity. Pure/static so the orientation logic is unit-testable without a GL
/// context.
/// </summary>
public static class ParticleBillboard
{
    private const float Epsilon = 1e-6f;

    /// <summary>
    /// Orientation-only overload for modes that don't depend on per-particle velocity. Velocity-
    /// dependent modes (VelocityAligned/Stretched) fall back to the camera basis here. (#2434)
    /// </summary>
    public static (Vector3 right, Vector3 up) QuadBasis(
        ParticleRenderMode mode, Vector3 camRight, Vector3 camUp, Quaternion emitterWorldRotation)
        => QuadBasis(mode, camRight, camUp, emitterWorldRotation, Vector3.Zero);

    /// <summary>
    /// Returns the (right, up) axes for a particle quad. Camera-facing modes use the supplied
    /// camera axes; local-frame and world-plane modes use the node/world axes; velocity modes use
    /// <paramref name="particleVelocityWorld"/>. A degenerate input (e.g. zero velocity for a
    /// velocity mode) falls back to the camera basis rather than producing a zero-length axis. (#2544)
    /// </summary>
    public static (Vector3 right, Vector3 up) QuadBasis(
        ParticleRenderMode mode, Vector3 camRight, Vector3 camUp, Quaternion emitterWorldRotation,
        Vector3 particleVelocityWorld)
    {
        switch (mode)
        {
            case ParticleRenderMode.BillboardLocalZ:
                // Quad locked to the emitter's local frame: local +X / +Y rotated into world space.
                // The quad normal is local +Z, so the sprite faces the way the emitter points.
                return (Vector3.Transform(Vector3.UnitX, emitterWorldRotation),
                        Vector3.Transform(Vector3.UnitY, emitterWorldRotation));

            case ParticleRenderMode.BillboardWorldZ:
            {
                // Quad lies flat in the world ground plane (normal = world +Z). Use world X/Y,
                // re-derived from the camera right so the flat card keeps a stable in-plane frame.
                var right = FlattenToGroundPlane(camRight, Vector3.UnitX);
                var up = Vector3.Cross(Vector3.UnitZ, right); // in-plane, perpendicular to right
                return (Normalize(right, Vector3.UnitX), Normalize(up, Vector3.UnitY));
            }

            case ParticleRenderMode.AlignedWorldZ:
            {
                // Standing card: up locked to world +Z, right perpendicular via the camera.
                var up = Vector3.UnitZ;
                var right = Vector3.Cross(up, camUp);
                if (right.LengthSquared() < Epsilon)
                    right = Vector3.Cross(up, camRight); // camera looking down +Z: pick another ref
                return (Normalize(right, Vector3.UnitX), up);
            }

            case ParticleRenderMode.VelocityAligned:
            {
                if (particleVelocityWorld.LengthSquared() < Epsilon)
                    return (camRight, camUp); // no direction to align to
                var up = Vector3.Normalize(particleVelocityWorld);
                var right = Vector3.Cross(up, camUp);
                if (right.LengthSquared() < Epsilon)
                    right = Vector3.Cross(up, camRight);
                return (Normalize(right, camRight), up);
            }

            case ParticleRenderMode.Stretched:
            {
                if (particleVelocityWorld.LengthSquared() < Epsilon)
                    return (camRight, camUp);
                // Up axis points along velocity and grows with speed (motion-blur streak); right is
                // a unit cross so the streak has a thin constant width.
                var dir = Vector3.Normalize(particleVelocityWorld);
                var up = particleVelocityWorld; // length == speed → longer streak when faster
                var right = Vector3.Cross(dir, camUp);
                if (right.LengthSquared() < Epsilon)
                    right = Vector3.Cross(dir, camRight);
                return (Normalize(right, camRight), up);
            }

            // Camera-facing (Normal) and the not-yet-faithful modes (Beam/Mesh/LinkedChain) default
            // to the camera basis (the caller logs the approximated ones).
            case ParticleRenderMode.Billboard:
            default:
                return (camRight, camUp);
        }
    }

    /// <summary>Project a vector onto the world XY plane (drop Z); return <paramref name="fallback"/> if degenerate.</summary>
    private static Vector3 FlattenToGroundPlane(Vector3 v, Vector3 fallback)
    {
        var flat = new Vector3(v.X, v.Y, 0f);
        return flat.LengthSquared() < Epsilon ? fallback : Vector3.Normalize(flat);
    }

    private static Vector3 Normalize(Vector3 v, Vector3 fallback)
        => v.LengthSquared() < Epsilon ? fallback : Vector3.Normalize(v);

    /// <summary>
    /// True when <paramref name="mode"/>'s quad orientation depends on the individual particle's
    /// velocity (so the basis must be resolved per particle, not once per emitter). (#2544)
    /// </summary>
    public static bool IsVelocityDependent(ParticleRenderMode mode) =>
        mode is ParticleRenderMode.VelocityAligned or ParticleRenderMode.Stretched;
}

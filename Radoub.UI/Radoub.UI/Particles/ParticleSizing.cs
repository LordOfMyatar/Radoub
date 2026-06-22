// Authored particle size resolution (#2544 item 3). The render path previously clamped every
// particle to 0.6×ModelRadius (a c_fairy-tuned magic number) and only used SizeX, forcing square
// billboards. The model leads: render the authored SizeX/SizeY directly. Pure/static so the
// resolution is unit-testable without a GL context.

using System;

namespace Radoub.UI.Particles;

/// <summary>
/// Resolves the per-particle billboard half-extents from authored sizes. No blanket clamp — the
/// authored size (already in raw MDL units, the same space as mesh vertices) is rendered directly.
/// Garbage (negative / NaN) is clamped to 0 so a quad can't explode.
/// </summary>
public static class ParticleSizing
{
    /// <summary>Resolve the (x, y) particle size. Negative/NaN inputs clamp to 0.</summary>
    public static (float sizeX, float sizeY) Resolve(float sizeX, float sizeY)
        => (Sanitize(sizeX), Sanitize(sizeY));

    private static float Sanitize(float v) => float.IsNaN(v) || v < 0f ? 0f : v;
}

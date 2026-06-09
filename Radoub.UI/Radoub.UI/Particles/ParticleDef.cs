// Particle model + compiler ported from rollnw (https://github.com/jd28/rollnw), MIT License,
// Copyright (c) jmd. Aurora particle behavior cross-referenced with nwn_mdl_webviewer
// (https://github.com/dunahan/nwn_mdl_webviewer), MIT License. See repo README for attribution. (#2395)

using System.Numerics;

namespace Radoub.UI.Particles;

/// <summary>How an emitter releases particles over time.</summary>
public enum ParticleEmissionMode
{
    Continuous,
    SingleShot,
    EventBurst
}

/// <summary>How individual particles are oriented/rendered.</summary>
public enum ParticleRenderMode
{
    Billboard,
    BillboardWorldZ,
    BillboardLocalZ,
    AlignedWorldZ,
    VelocityAligned,
    Stretched,
    LinkedChain,
    Beam,
    Mesh
}

/// <summary>Blend equation used when compositing particles.</summary>
public enum ParticleBlendMode
{
    Alpha,
    Cutout,
    Additive
}

/// <summary>Shape of the region particles spawn within.</summary>
public enum ParticleSpawnRegionType
{
    Point,
    Rect
}

/// <summary>Inclusive numeric range [Min, Max].</summary>
public struct RangeF
{
    public float Min;
    public float Max;
}

/// <summary>A single keyframe on a scalar curve.</summary>
public struct CurveKey
{
    public float Time;
    public float Value;
}

/// <summary>
/// Piecewise-linear scalar curve over time. Keys are assumed sorted by Time.
/// </summary>
public class CurveF
{
    public List<CurveKey> Keys { get; } = new();

    /// <summary>
    /// Evaluate the curve at <paramref name="t"/>. Returns 0 if empty, the lone
    /// value if one key, clamps to first/last value outside the key range, and
    /// linearly interpolates between bracketing keys otherwise.
    /// </summary>
    public float Eval(float t)
    {
        if (Keys.Count == 0)
            return 0f;
        if (Keys.Count == 1)
            return Keys[0].Value;

        if (t <= Keys[0].Time)
            return Keys[0].Value;
        if (t >= Keys[^1].Time)
            return Keys[^1].Value;

        for (int i = 0; i < Keys.Count - 1; i++)
        {
            var a = Keys[i];
            var b = Keys[i + 1];
            if (t >= a.Time && t <= b.Time)
            {
                float span = b.Time - a.Time;
                if (span <= 0f)
                    return a.Value;
                float f = (t - a.Time) / span;
                return a.Value + (b.Value - a.Value) * f;
            }
        }

        return Keys[^1].Value;
    }
}

/// <summary>A single keyframe on a color/vector gradient.</summary>
public struct GradientKey
{
    public float Time;
    public Vector4 Value;
}

/// <summary>
/// Piecewise-linear per-channel gradient over time. Keys are assumed sorted by Time.
/// </summary>
public class Gradient
{
    public List<GradientKey> Keys { get; } = new();

    /// <summary>
    /// Evaluate the gradient at <paramref name="t"/>. Returns <see cref="Vector4.Zero"/>
    /// if empty, the lone value if one key, clamps outside the key range, and
    /// linearly interpolates per channel otherwise.
    /// </summary>
    public Vector4 Eval(float t)
    {
        if (Keys.Count == 0)
            return Vector4.Zero;
        if (Keys.Count == 1)
            return Keys[0].Value;

        if (t <= Keys[0].Time)
            return Keys[0].Value;
        if (t >= Keys[^1].Time)
            return Keys[^1].Value;

        for (int i = 0; i < Keys.Count - 1; i++)
        {
            var a = Keys[i];
            var b = Keys[i + 1];
            if (t >= a.Time && t <= b.Time)
            {
                float span = b.Time - a.Time;
                if (span <= 0f)
                    return a.Value;
                float f = (t - a.Time) / span;
                return Vector4.Lerp(a.Value, b.Value, f);
            }
        }

        return Keys[^1].Value;
    }
}

/// <summary>Texture-atlas animation parameters for a particle material.</summary>
public class ParticleSpriteSheet
{
    public ushort Columns { get; set; } = 1;
    public ushort Rows { get; set; } = 1;
    public ushort FrameBegin { get; set; }
    public ushort FrameEnd { get; set; }
    public float FramesPerSecond { get; set; }
    public bool RandomStart { get; set; }
}

/// <summary>Material/blend description for rendering particles.</summary>
public class ParticleMaterial
{
    public string Texture { get; set; } = "";
    public ParticleBlendMode Blend { get; set; }
    public bool DoubleSided { get; set; }
    public ParticleSpriteSheet Sheet { get; set; } = new();
}

/// <summary>Curves describing how particle attributes change over their lifetime.</summary>
public class OverLife
{
    public CurveF Alpha { get; set; } = new();
    public CurveF SizeX { get; set; } = new();
    public CurveF SizeY { get; set; } = new();
    public Gradient Color { get; set; } = new();
}

/// <summary>
/// Runtime particle emitter definition compiled from a parsed MDL emitter node.
/// </summary>
public class CompiledEmitter
{
    public ParticleEmissionMode EmissionMode { get; set; }
    public ParticleRenderMode RenderMode { get; set; }
    public ParticleBlendMode Blend { get; set; }
    public ParticleSpawnRegionType RegionType { get; set; }
    public Vector2 RegionSize { get; set; }
    public RangeF Lifetime { get; set; }
    public RangeF Speed { get; set; }
    public RangeF SizeX { get; set; }
    public RangeF SizeY { get; set; }
    public float Spread { get; set; }
    public float Mass { get; set; }
    public float Grav { get; set; }
    public float Drag { get; set; }
    public float BirthRate { get; set; }
    public float VelocityInheritance { get; set; }
    public bool Tinted { get; set; }
    public ParticleMaterial Material { get; set; } = new();
    public OverLife OverLife { get; set; } = new();
    public int XGrid { get; set; } = 1;
    public int YGrid { get; set; } = 1;
    public float Fps { get; set; }
    public int FrameStart { get; set; }
    public int FrameEnd { get; set; }
}

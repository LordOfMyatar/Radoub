// Particle model + compiler ported from rollnw (https://github.com/jd28/rollnw), MIT License,
// Copyright (c) jmd. Aurora particle behavior cross-referenced with nwn_mdl_webviewer
// (https://github.com/dunahan/nwn_mdl_webviewer), MIT License. See repo README for attribution. (#2395)

using System.Numerics;
using Radoub.Formats.Mdl;

namespace Radoub.UI.Particles;

/// <summary>
/// Compiles a parsed <see cref="MdlEmitterNode"/> into a runtime
/// <see cref="CompiledEmitter"/>. Port of rollnw mdl_particle_import.cpp.
/// </summary>
public static class EmitterCompiler
{
    // EmitterFlags bits (see MdlEmitterNode header field 0x144).
    private const uint FlagIsTinted = 0x0008;
    private const uint FlagInheritVel = 0x0080;

    /// <summary>
    /// Quiet gap (seconds) inserted after a fire-and-forget burst fades, before it replays.
    /// Model-led on the burst duration (LifeExp); the gap itself is a fixed pause so the preview
    /// shows a periodic burst rather than a non-stop fountain. Tune here if the cadence reads
    /// wrong against real models (#2544 / #2439).
    /// </summary>
    private const float FireAndForgetQuietGapSeconds = 30f;

    /// <summary>
    /// Assumes <paramref name="node"/> is already parsed. Sanitizes only LifeExp
    /// (negative lifetime is clamped to 0) and the over-life percent times
    /// (NaN is treated as 0); all other fields are passed through unchanged.
    /// </summary>
    public static CompiledEmitter Compile(MdlEmitterNode node)
    {
        var result = new CompiledEmitter
        {
            EmissionMode = MapEmission(node.Update),
            RenderMode = MapRender(node.RenderMethod),
            Blend = MapBlend(node.Blend),
            Spread = node.Spread,
            Mass = node.Mass,
            Grav = node.Grav,
            Drag = node.Drag,
            BirthRate = node.BirthRate,
            Tinted = (node.EmitterFlags & FlagIsTinted) != 0,
            VelocityInheritance = (node.EmitterFlags & FlagInheritVel) != 0 ? 1f : 0f,
            RegionType = ParticleSpawnRegionType.Point,
            RegionSize = Vector2.Zero,
            XGrid = node.XGrid,
            YGrid = node.YGrid,
            Fps = node.Fps,
            FrameStart = node.FrameStart,
            FrameEnd = node.FrameEnd
        };

        float life = node.LifeExp > 0f ? node.LifeExp : 0f;
        result.Lifetime = new RangeF { Min = life, Max = life };

        // Replay cadence for fire-and-forget modes: burst runs ~life, then a quiet gap, then
        // replay. Continuous (Fountain) has no cycle (period 0). (#2544)
        result.ReplayPeriod = result.EmissionMode == ParticleEmissionMode.Continuous
            ? 0f
            : life + FireAndForgetQuietGapSeconds;
        result.Speed = new RangeF { Min = node.Velocity, Max = node.Velocity + Math.Max(0f, node.RandVel) };

        float sizeStartY = node.SizeStartY == 0f ? node.SizeStart : node.SizeStartY;
        result.SizeX = new RangeF { Min = node.SizeStart, Max = node.SizeStart };
        result.SizeY = new RangeF { Min = sizeStartY, Max = sizeStartY };

        result.Material.Texture = node.Texture;
        result.Material.Blend = result.Blend;
        result.Material.Sheet.Columns = (ushort)Math.Max(1, node.XGrid);
        result.Material.Sheet.Rows = (ushort)Math.Max(1, node.YGrid);
        result.Material.Sheet.FrameBegin = (ushort)node.FrameStart;
        result.Material.Sheet.FrameEnd = (ushort)node.FrameEnd;
        result.Material.Sheet.FramesPerSecond = node.Fps;

        // Over-life percent times: clamped and monotonically non-decreasing.
        float pStart = Clamp01(node.PercentStart);
        float pMid = Math.Max(pStart, Clamp01(node.PercentMid));
        float pEnd = Math.Max(pMid, Clamp01(node.PercentEnd));

        // When a mid controller was not authored, default the mid value to the
        // start/end midpoint instead of the field default (white / 1.0). Mirrors
        // rollnw mdl_particle_import.cpp vec3_or(..., mix(start, end, 0.5)) (#2395).
        float alphaMid = node.HasAlphaMid ? node.AlphaMid : (node.AlphaStart + node.AlphaEnd) * 0.5f;
        float sizeMid = node.HasSizeMid ? node.SizeMid : (node.SizeStart + node.SizeEnd) * 0.5f;
        var colorMid = node.HasColorMid ? node.ColorMid : (node.ColorStart + node.ColorEnd) * 0.5f;

        result.OverLife.Alpha = ThreeKeyCurve(pStart, pMid, pEnd,
            node.AlphaStart, alphaMid, node.AlphaEnd);

        // SizeY falls back to the X value when the corresponding _Y field is 0.
        float sizeMidY = node.SizeMidY == 0f ? sizeMid : node.SizeMidY;
        float sizeEndY = node.SizeEndY == 0f ? node.SizeEnd : node.SizeEndY;

        result.OverLife.SizeX = ThreeKeyCurve(pStart, pMid, pEnd,
            node.SizeStart, sizeMid, node.SizeEnd);
        result.OverLife.SizeY = ThreeKeyCurve(pStart, pMid, pEnd,
            sizeStartY, sizeMidY, sizeEndY);

        result.OverLife.Color = ThreeKeyGradient(pStart, pMid, pEnd,
            node.ColorStart, colorMid, node.ColorEnd);

        return result;
    }

    private static ParticleEmissionMode MapEmission(string? update) => Normalize(update) switch
    {
        "fountain" => ParticleEmissionMode.Continuous,
        "single" => ParticleEmissionMode.SingleShot,
        "explosion" => ParticleEmissionMode.EventBurst,
        // Lightning is an event/beam mode with no faithful continuous render — fold it into the
        // fire-and-forget burst path so it cycles instead of streaming as a fountain (#2544).
        "lightning" => ParticleEmissionMode.EventBurst,
        _ => ParticleEmissionMode.Continuous
    };

    private static ParticleRenderMode MapRender(string? render) => Normalize(render) switch
    {
        "normal" => ParticleRenderMode.Billboard,
        "billboard_to_world_z" => ParticleRenderMode.BillboardWorldZ,
        "billboard_to_local_z" => ParticleRenderMode.BillboardLocalZ,
        "aligned_to_world_z" => ParticleRenderMode.AlignedWorldZ,
        "aligned_to_particle_dir" => ParticleRenderMode.VelocityAligned,
        "motion_blur" => ParticleRenderMode.Stretched,
        "linked" => ParticleRenderMode.LinkedChain,
        _ => ParticleRenderMode.Billboard
    };

    private static ParticleBlendMode MapBlend(string? blend) => Normalize(blend) switch
    {
        "lighten" => ParticleBlendMode.Additive,
        "punchthrough" => ParticleBlendMode.Cutout,
        "punch_through" => ParticleBlendMode.Cutout,
        _ => ParticleBlendMode.Alpha
    };

    // Lowercase, trim, and fold spaces/hyphens to underscores for stable matching.
    private static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;
        return s.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
    }

    private static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Clamp(v, 0f, 1f);

    private static CurveF ThreeKeyCurve(float t0, float t1, float t2, float v0, float v1, float v2)
    {
        var c = new CurveF();
        c.Keys.Add(new CurveKey { Time = t0, Value = v0 });
        c.Keys.Add(new CurveKey { Time = t1, Value = v1 });
        c.Keys.Add(new CurveKey { Time = t2, Value = v2 });
        return c;
    }

    private static Gradient ThreeKeyGradient(float t0, float t1, float t2, Vector3 c0, Vector3 c1, Vector3 c2)
    {
        var g = new Gradient();
        g.Keys.Add(new GradientKey { Time = t0, Value = new Vector4(c0, 1f) });
        g.Keys.Add(new GradientKey { Time = t1, Value = new Vector4(c1, 1f) });
        g.Keys.Add(new GradientKey { Time = t2, Value = new Vector4(c2, 1f) });
        return g;
    }
}

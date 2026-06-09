using System.Numerics;
using Radoub.Formats.Mdl;
using Radoub.UI.Particles;

namespace Radoub.UI.Tests.Particles;

public class EmitterCompilerTests
{
    private static MdlEmitterNode SampleNode() => new()
    {
        Update = "Fountain",
        RenderMethod = "Normal",
        Blend = "Lighten",
        BirthRate = 100,
        LifeExp = 0.5f,
        Velocity = 2f,
        Spread = 0.3f,
        SizeStart = 0.4f,
        SizeMid = 0.6f,
        HasSizeMid = true,
        SizeEnd = 0.1f,
        AlphaStart = 1f,
        AlphaMid = 1f,
        HasAlphaMid = true,
        AlphaEnd = 0f,
        PercentStart = 0f,
        PercentMid = 0.5f,
        PercentEnd = 1f,
        ColorStart = Vector3.One,
        ColorEnd = new Vector3(1, 0, 0)
    };

    [Fact]
    public void Compile_MapsEmissionRenderBlendModes()
    {
        var result = EmitterCompiler.Compile(SampleNode());
        Assert.Equal(ParticleEmissionMode.Continuous, result.EmissionMode);
        Assert.Equal(ParticleRenderMode.Billboard, result.RenderMode);
        Assert.Equal(ParticleBlendMode.Additive, result.Blend);
    }

    [Fact]
    public void Compile_SizeXOverLifeHasThreeKeys()
    {
        var result = EmitterCompiler.Compile(SampleNode());
        Assert.Equal(3, result.OverLife.SizeX.Keys.Count);

        Assert.Equal(0f, result.OverLife.SizeX.Keys[0].Time, 5);
        Assert.Equal(0.4f, result.OverLife.SizeX.Keys[0].Value, 5);

        Assert.Equal(0.5f, result.OverLife.SizeX.Keys[1].Time, 5);
        Assert.Equal(0.6f, result.OverLife.SizeX.Keys[1].Value, 5);

        Assert.Equal(1f, result.OverLife.SizeX.Keys[2].Time, 5);
        Assert.Equal(0.1f, result.OverLife.SizeX.Keys[2].Value, 5);
    }

    [Fact]
    public void Compile_AlphaOverLifeEndsAtZero()
    {
        var result = EmitterCompiler.Compile(SampleNode());
        Assert.Equal(0f, result.OverLife.Alpha.Keys[^1].Value, 5);
    }

    [Fact]
    public void Compile_CopiesScalarFields()
    {
        var result = EmitterCompiler.Compile(SampleNode());
        Assert.Equal(2f, result.Speed.Min, 5);
        Assert.Equal(0.5f, result.Lifetime.Min, 5);
        Assert.Equal(100f, result.BirthRate, 5);
        Assert.Equal(0.3f, result.Spread, 5);
    }

    [Fact]
    public void Compile_EqualPercentTimes_ProducesEvaluableCurve()
    {
        var node = SampleNode();
        node.PercentStart = 0f;
        node.PercentMid = 0f;
        var compiled = EmitterCompiler.Compile(node);
        var v = compiled.OverLife.SizeX.Eval(0f);
        Assert.False(float.IsNaN(v));
    }

    [Fact]
    public void Compile_NegativeLifeExp_ClampsToZero()
    {
        var node = SampleNode();
        node.LifeExp = -5f;
        var compiled = EmitterCompiler.Compile(node);
        Assert.Equal(0f, compiled.Lifetime.Min);
    }

    [Fact]
    public void Compile_BlendPunchThrough_MapsCutout()
    {
        var node = SampleNode();
        node.Blend = "Punch-Through";
        Assert.Equal(ParticleBlendMode.Cutout, EmitterCompiler.Compile(node).Blend);

        node.Blend = "punchthrough";
        Assert.Equal(ParticleBlendMode.Cutout, EmitterCompiler.Compile(node).Blend);
    }

    [Fact]
    public void Compile_InheritVelFlag_SetsVelocityInheritance()
    {
        var node = SampleNode();
        node.EmitterFlags = 0x0080;
        Assert.Equal(1f, EmitterCompiler.Compile(node).VelocityInheritance);

        node.EmitterFlags = 0;
        Assert.Equal(0f, EmitterCompiler.Compile(node).VelocityInheritance);
    }

    [Fact]
    public void Compile_SizeYZero_FallsBackToSizeX()
    {
        var node = SampleNode();
        node.SizeStart = 0.4f;
        node.SizeStartY = 0f;
        Assert.Equal(0.4f, EmitterCompiler.Compile(node).SizeY.Min, 5);
    }

    [Fact]
    public void Compile_NoColorMid_UsesStartEndMidpoint()
    {
        // zodrat-style: ColorStart == ColorEnd == yellow, no authored ColorMid.
        // Unauthored mid must default to the start/end midpoint (yellow), not white.
        var node = SampleNode();
        node.ColorStart = new Vector3(1f, 0.96f, 0f);
        node.ColorEnd = new Vector3(1f, 0.96f, 0f);
        node.HasColorMid = false; // ColorMid left at default (white)
        node.PercentStart = 0f;
        node.PercentMid = 0.5f;
        node.PercentEnd = 1f;

        var compiled = EmitterCompiler.Compile(node);
        var mid = compiled.OverLife.Color.Eval(0.5f);

        Assert.Equal(1f, mid.X, 3);
        Assert.Equal(0.96f, mid.Y, 3);
        Assert.Equal(0f, mid.Z, 3);
    }

    [Fact]
    public void Compile_WithColorMid_UsesAuthoredMid()
    {
        var node = SampleNode();
        node.ColorStart = new Vector3(1f, 0f, 0f);
        node.ColorEnd = new Vector3(0f, 0f, 1f);
        node.ColorMid = new Vector3(0f, 1f, 0f);
        node.HasColorMid = true;
        node.PercentStart = 0f;
        node.PercentMid = 0.5f;
        node.PercentEnd = 1f;

        var compiled = EmitterCompiler.Compile(node);
        var mid = compiled.OverLife.Color.Eval(0.5f);

        Assert.Equal(0f, mid.X, 3);
        Assert.Equal(1f, mid.Y, 3);
        Assert.Equal(0f, mid.Z, 3);
    }

    [Fact]
    public void Compile_SpeedMax_AddsRandVel()
    {
        var node = SampleNode();
        node.Velocity = 2f;
        node.RandVel = 3f;
        var compiled = EmitterCompiler.Compile(node);
        Assert.Equal(5f, compiled.Speed.Max, 5);
        Assert.Equal(2f, compiled.Speed.Min, 5);

        node.RandVel = -1f;
        Assert.Equal(2f, EmitterCompiler.Compile(node).Speed.Max, 5);
    }
}

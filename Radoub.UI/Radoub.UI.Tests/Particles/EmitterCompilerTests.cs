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
        SizeEnd = 0.1f,
        AlphaStart = 1f,
        AlphaMid = 1f,
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
}

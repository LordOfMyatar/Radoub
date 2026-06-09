using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Radoub.Formats.Mdl;
using Xunit;

namespace Radoub.Formats.Tests.Mdl;

/// <summary>
/// Emitter controller + struct-field parsing tests (#2395).
/// Uses <see cref="EmitterMdlFixture"/> as the authoritative binary ground truth.
/// </summary>
public class MdlEmitterTests
{
    [Fact]
    public void EmitterNode_ParsesCoreControllers()
    {
        var bytes = EmitterMdlFixture.BuildSingleEmitter(
            birthrate: 100f,
            lifeExp: 0.5f,
            velocity: 2f,
            spread: 0.3f,
            sizeStart: 0.4f,
            sizeEnd: 0.1f,
            colorStart: new Vector3(1f, 0.5f, 0f));

        var model = new MdlBinaryReader().Parse(bytes);

        var emitter = model.EnumerateAllNodes().OfType<MdlEmitterNode>().Single();
        Assert.Equal(100f, emitter.BirthRate);
        Assert.Equal(0.5f, emitter.LifeExp);
        Assert.Equal(2f, emitter.Velocity);
        Assert.Equal(0.3f, emitter.Spread);
        Assert.Equal(0.4f, emitter.SizeStart);
        Assert.Equal(0.1f, emitter.SizeEnd);
        Assert.Equal(new Vector3(1f, 0.5f, 0f), emitter.ColorStart);
    }

    [Fact]
    public void EmitterNode_ParsesStringFields_WithCorrectTextureWidth()
    {
        var bytes = EmitterMdlFixture.BuildSingleEmitter(
            update: "Fountain",
            render: "Normal",
            blend: "Lighten",
            texture: "fxpa_glow",
            xgrid: 2,
            ygrid: 2,
            loop: true,
            renderOrder: 3);

        var model = new MdlBinaryReader().Parse(bytes);

        var emitter = model.EnumerateAllNodes().OfType<MdlEmitterNode>().Single();
        Assert.Equal("Fountain", emitter.Update);
        Assert.Equal("Normal", emitter.RenderMethod);
        Assert.Equal("Lighten", emitter.Blend);
        Assert.Equal("fxpa_glow", emitter.Texture);
        Assert.Equal(2, emitter.XGrid);
        Assert.Equal(2, emitter.YGrid);
        Assert.True(emitter.Loop);            // misaligned/garbage if texture read as 32 bytes
        Assert.Equal(3, emitter.RenderOrder); // misaligned under the old 32-byte bug
    }

    [Fact]
    public void EmitterNode_ColorMidController_SetsHasColorMidTrue()
    {
        var bytes = EmitterMdlFixture.BuildSingleEmitter(
            colorStart: new Vector3(1f, 0.96f, 0f),
            colorMid: new Vector3(0.5f, 0.5f, 0.5f),
            colorEnd: new Vector3(1f, 0.96f, 0f));

        var model = new MdlBinaryReader().Parse(bytes);
        var emitter = model.EnumerateAllNodes().OfType<MdlEmitterNode>().Single();

        Assert.True(emitter.HasColorMid);
        Assert.Equal(new Vector3(0.5f, 0.5f, 0.5f), emitter.ColorMid);
    }

    [Fact]
    public void EmitterNode_NoColorMidController_LeavesHasColorMidFalse()
    {
        var bytes = EmitterMdlFixture.BuildSingleEmitter(
            colorStart: new Vector3(1f, 0.96f, 0f),
            colorEnd: new Vector3(1f, 0.96f, 0f));

        var model = new MdlBinaryReader().Parse(bytes);
        var emitter = model.EnumerateAllNodes().OfType<MdlEmitterNode>().Single();

        Assert.False(emitter.HasColorMid);
    }

    /// <summary>
    /// Real-file parity: the ASCII reader must parse emitter string + numeric
    /// controller props from a genuine NWN model (c_allip_d.mdl, OmenEmitter01/02).
    /// </summary>
    [Fact]
    public void AsciiReader_ParsesAllipEmitterControllers()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Mdl", "c_allip_d.mdl");
        Assert.True(File.Exists(path), $"Fixture not found: {path}");

        var content = File.ReadAllText(path);
        var model = new MdlAsciiReader().Parse(content);

        var emitters = model.EnumerateAllNodes().OfType<MdlEmitterNode>().ToList();
        Assert.NotEmpty(emitters);

        // OmenEmitter01/02 use update "Fountain", blend "Lighten", birthrate 100.
        var omen = emitters.FirstOrDefault(e =>
            string.Equals(e.Update, "Fountain", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Blend, "Lighten", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(omen);
        Assert.True(omen!.BirthRate > 0f, "BirthRate should parse from 'birthrate 100'");
    }
}

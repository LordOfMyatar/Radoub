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
}

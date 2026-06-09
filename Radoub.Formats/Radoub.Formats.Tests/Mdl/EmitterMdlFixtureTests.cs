using System.Linq;
using Radoub.Formats.Mdl;
using Xunit;

namespace Radoub.Formats.Tests.Mdl;

/// <summary>
/// Sanity tests for <see cref="EmitterMdlFixture"/>, the in-memory binary MDL builder
/// used by emitter-parser unit tests (#2395).
/// </summary>
public class EmitterMdlFixtureTests
{
    [Fact]
    public void Fixture_ParsesToSingleEmitter()
    {
        var bytes = EmitterMdlFixture.BuildSingleEmitter();

        var model = new MdlBinaryReader().Parse(bytes);

        var emitters = model.EnumerateAllNodes().OfType<MdlEmitterNode>().ToList();
        Assert.Single(emitters);
    }

    [Fact]
    public void Fixture_RoundTripsStringFields()
    {
        // update/render/blend already parse correctly under the current parser.
        // (texture is intentionally NOT asserted here — the 32-vs-64 byte width
        //  bug is fixed in a later task.)
        var bytes = EmitterMdlFixture.BuildSingleEmitter(
            update: "Fountain",
            render: "Linked",
            blend: "Lighten",
            xgrid: 4,
            ygrid: 2);

        var model = new MdlBinaryReader().Parse(bytes);

        var emitter = model.EnumerateAllNodes().OfType<MdlEmitterNode>().Single();
        Assert.Equal("Fountain", emitter.Update);
        Assert.Equal("Linked", emitter.RenderMethod);
        Assert.Equal("Lighten", emitter.Blend);
        Assert.Equal(4, emitter.XGrid);
        Assert.Equal(2, emitter.YGrid);
    }
}

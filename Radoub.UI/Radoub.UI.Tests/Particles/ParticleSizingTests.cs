using Radoub.UI.Particles;

namespace Radoub.UI.Tests.Particles;

/// <summary>
/// Authored particle size resolution (#2544 item 3). The old render path clamped every particle
/// to 0.6×ModelRadius (a c_fairy-tuned magic number) and only read SizeX, forcing square
/// billboards. These tests pin the model-led behavior: render the authored SizeX/SizeY directly,
/// no blanket clamp.
/// </summary>
public class ParticleSizingTests
{
    [Fact]
    public void Resolve_ReturnsAuthoredXAndY_Independently()
    {
        // A non-square particle (SizeX != SizeY) must keep both extents — no forced square.
        var (sx, sy) = ParticleSizing.Resolve(sizeX: 2f, sizeY: 0.5f);
        Assert.Equal(2f, sx);
        Assert.Equal(0.5f, sy);
    }

    [Fact]
    public void Resolve_DoesNotClampLargeAuthoredSize()
    {
        // The blanket 0.6×radius cap is gone: a legitimately large authored puff (e.g. the
        // plc_a02 fire emitter authors size 4) renders at its authored size, not shrunk.
        var (sx, sy) = ParticleSizing.Resolve(sizeX: 4f, sizeY: 4f);
        Assert.Equal(4f, sx);
        Assert.Equal(4f, sy);
    }

    [Fact]
    public void Resolve_NegativeOrNaN_ClampsToZero()
    {
        // Defensive: a garbage size must not produce a negative/NaN extent that explodes the quad.
        var (sx, sy) = ParticleSizing.Resolve(sizeX: -1f, sizeY: float.NaN);
        Assert.Equal(0f, sx);
        Assert.Equal(0f, sy);
    }
}

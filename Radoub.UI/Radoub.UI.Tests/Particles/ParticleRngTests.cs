using Radoub.UI.Particles;

namespace Radoub.UI.Tests.Particles;

public class ParticleRngTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalSequence()
    {
        var a = new ParticleRng(12345u);
        var b = new ParticleRng(12345u);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(a.NextUnit(), b.NextUnit());
        }
    }

    [Fact]
    public void NextUnit_InZeroToOneRange()
    {
        var rng = new ParticleRng(1u);
        for (int i = 0; i < 1000; i++)
        {
            float v = rng.NextUnit();
            Assert.InRange(v, 0f, 1f);
            Assert.True(v < 1f, $"NextUnit returned {v}, expected < 1");
        }
    }

    [Fact]
    public void NextRange_WithinBounds()
    {
        var rng = new ParticleRng(7u);
        for (int i = 0; i < 1000; i++)
        {
            float v = rng.NextRange(2f, 5f);
            Assert.InRange(v, 2f, 5f);
            Assert.True(v < 5f, $"NextRange returned {v}, expected < 5");
        }
    }

    [Fact]
    public void NextRange_EqualBounds_ReturnsMin()
    {
        var rng = new ParticleRng(7u);
        Assert.Equal(5f, rng.NextRange(5f, 5f));
    }

    [Fact]
    public void NextRange_MaxLessThanMin_ReturnsMin()
    {
        var rng = new ParticleRng(7u);
        Assert.Equal(5f, rng.NextRange(5f, 2f));
    }
}

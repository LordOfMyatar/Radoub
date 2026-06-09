using Radoub.Formats.Mdl;
using Radoub.UI.Particles;

namespace Radoub.UI.Tests.Particles;

public class ParticleSystemTests
{
    // Builds a CompiledEmitter by compiling an MdlEmitterNode, so these tests
    // also exercise the compiler -> sim seam.
    private static CompiledEmitter Compile(MdlEmitterNode node) => EmitterCompiler.Compile(node);

    private static MdlEmitterNode BaseNode() => new()
    {
        Update = "Fountain",
        BirthRate = 100f,
        LifeExp = 10f,
        Velocity = 0f,
        Spread = 0f,
        Mass = 1f,
        Grav = 1f,
        SizeStart = 1f,
        SizeEnd = 1f,
        PercentStart = 0f,
        PercentMid = 0.5f,
        PercentEnd = 1f
    };

    [Fact]
    public void Update_SpawnsBirthRateTimesDt()
    {
        var node = BaseNode();
        node.BirthRate = 100f;
        node.LifeExp = 10f; // long life, no deaths during the test
        var sys = new ParticleSystem(Compile(node), seed: 1u);

        for (int i = 0; i < 30; i++)
            sys.Update(1f / 30f);

        // 100/s * 1s = ~100 spawned, none died yet.
        Assert.InRange(sys.LiveCount, 95, 105);
    }

    [Fact]
    public void Update_KillsParticlesPastLifeExp()
    {
        var node = BaseNode();
        node.BirthRate = 100f;
        node.LifeExp = 0.1f;
        var sys = new ParticleSystem(Compile(node), seed: 2u);

        for (int i = 0; i < 60; i++)
            sys.Update(1f / 30f);

        // Steady-state ~ birthRate * lifeExp = 100 * 0.1 = 10.
        Assert.InRange(sys.LiveCount, 7, 13);
    }

    [Fact]
    public void Update_AppliesGravity_NegativeZ()
    {
        var node = BaseNode();
        node.BirthRate = 1f;
        node.LifeExp = 10f;
        node.Velocity = 0f;
        node.Spread = 0f;
        node.Mass = 1f;
        node.Grav = 1f;
        var sys = new ParticleSystem(Compile(node), seed: 3u);

        // Small update spawns one particle (birthRate 1 * dt accumulates over time;
        // use a dt that crosses 1 spawn threshold).
        sys.Update(1f);
        Assert.True(sys.LiveCount >= 1, "expected at least one particle spawned");
        float z0 = sys.FirstParticle.Position.Z;

        for (int i = 0; i < 100; i++)
            sys.Update(0.01f);

        // Discrete integrator: only assert the sign, gravity pulls -Z.
        Assert.True(sys.FirstParticle.Position.Z < z0,
            $"expected Z to fall below {z0}, got {sys.FirstParticle.Position.Z}");
    }

    [Fact]
    public void Update_LerpsSizeOverLife()
    {
        var node = BaseNode();
        node.BirthRate = 1f;
        node.LifeExp = 1f;
        node.Velocity = 0f;
        node.Spread = 0f;
        node.SizeStart = 1f;
        node.SizeMid = 0.5f; // linear fade midpoint so SizeX at t=0.5 is ~0.5
        node.SizeEnd = 0f;   // OverLife.SizeX fades 1 -> 0 over life
        node.PercentStart = 0f;
        node.PercentMid = 0.5f;
        node.PercentEnd = 1f;
        var sys = new ParticleSystem(Compile(node), seed: 4u);

        sys.Update(1f); // spawn one (age becomes ~ the dt past spawn)
        Assert.True(sys.LiveCount >= 1, "expected at least one particle spawned");

        // Advance until first particle age ~= 0.5.
        // After spawn the particle exists; step in small increments.
        for (int i = 0; i < 50 && sys.FirstParticle.Age < 0.5f; i++)
            sys.Update(0.01f);

        Assert.InRange(sys.FirstParticle.SizeX, 0.4f, 0.6f);
    }
}

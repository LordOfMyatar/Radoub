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

        // birthRate 1 * dt accumulates; step in clamped-size frames until one spawns.
        for (int i = 0; i < 20 && sys.LiveCount == 0; i++)
            sys.Update(0.1f);
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

        // birthRate 1 * dt accumulates; step in clamped-size frames until one spawns.
        for (int i = 0; i < 20 && sys.LiveCount == 0; i++)
            sys.Update(0.1f);
        Assert.True(sys.LiveCount >= 1, "expected at least one particle spawned");

        // Advance until first particle age ~= 0.5.
        // After spawn the particle exists; step in small increments.
        for (int i = 0; i < 50 && sys.FirstParticle.Age < 0.5f; i++)
            sys.Update(0.01f);

        Assert.InRange(sys.FirstParticle.SizeX, 0.4f, 0.6f);
    }

    [Fact]
    public void Update_AlwaysAppliesColorStart_RegardlessOfTintFlag()
    {
        // Aurora applies emitter color controllers unconditionally — IsTinted (0x0008) is NOT a
        // color gate (it means "tint by scene ambient", per rollnw). zodrat's yellow colorStart
        // must show through even with the flag clear. Regression for the orbs rendering wrong (#2395).
        var color = new System.Numerics.Vector3(1f, 0.96f, 0f); // zodrat yellow
        foreach (uint flags in new uint[] { 0u, 0x0008u })
        {
            var node = BaseNode();
            node.BirthRate = 1f;
            node.LifeExp = 10f;
            node.EmitterFlags = flags;
            node.ColorStart = color;
            node.ColorMid = color;
            node.ColorEnd = color;
            var sys = new ParticleSystem(Compile(node), seed: 11u);

            for (int i = 0; i < 20 && sys.LiveCount == 0; i++)
                sys.Update(0.1f);
            Assert.True(sys.LiveCount >= 1);

            var c = sys.FirstParticle.Color;
            Assert.True(System.Math.Abs(c.X - 1f) < 0.01f && System.Math.Abs(c.Y - 0.96f) < 0.01f && System.Math.Abs(c.Z) < 0.01f,
                $"flags=0x{flags:X}: expected yellow (1,0.96,0), got ({c.X},{c.Y},{c.Z})");
        }
    }

    [Fact]
    public void Update_RotatesEmissionByEmitterRotation()
    {
        // Emitter emits along local +Z with no spread. A 180° rotation about X maps local +Z
        // to world -Z, so the spawned particle's velocity should point down (-Z). (#2395)
        var node = BaseNode();
        node.BirthRate = 1f;
        node.LifeExp = 10f;
        node.Velocity = 1f; // unit speed so velocity magnitude ~= 1
        node.Spread = 0f;
        var sys = new ParticleSystem(Compile(node), seed: 9u);

        var rot = System.Numerics.Quaternion.CreateFromAxisAngle(
            System.Numerics.Vector3.UnitX, System.MathF.PI);

        for (int i = 0; i < 20 && sys.LiveCount == 0; i++)
            sys.Update(0.1f, System.Numerics.Vector3.Zero, rot);
        Assert.True(sys.LiveCount >= 1, "expected at least one particle spawned");

        var v = sys.FirstParticle.Velocity;
        Assert.True(v.Z < 0f, $"expected velocity Z negative after 180° X rotation, got {v.Z}");
        Assert.InRange(v.Z, -1.01f, -0.99f);
        Assert.True(System.Math.Abs(v.X) < 0.01f, $"expected X ~= 0, got {v.X}");
        Assert.True(System.Math.Abs(v.Y) < 0.01f, $"expected Y ~= 0, got {v.Y}");
    }

    [Fact]
    public void SampleConeTheta_BiasesTowardAxis()
    {
        // Uniform sampling puts theta = spread * u, so u=0.5 -> spread/2 (mean angle).
        // Axis-biased sampling (bias > 1) must pull the same midpoint draw closer to the
        // emission axis (theta = 0), so the fairy dust falls in a tighter column. (#2434)
        float spread = 1.05f; // fairyDust spread (~60deg half-angle)
        float midUniform = spread * 0.5f;

        float biased = ParticleSystem.SampleConeTheta(spread, 0.5f, ParticleSystem.EmissionAxisBias);

        Assert.True(ParticleSystem.EmissionAxisBias > 1f,
            "axis bias exponent must be > 1 to concentrate particles near the axis");
        Assert.True(biased < midUniform,
            $"expected biased theta {biased} < uniform midpoint {midUniform}");
    }

    [Fact]
    public void SampleConeTheta_PreservesEndpoints()
    {
        // The bias reshapes the interior of the cone but must not exceed the spread
        // half-angle (u=1) or emit behind the axis (u=0). (#2434)
        float spread = 1.05f;
        Assert.Equal(0f, ParticleSystem.SampleConeTheta(spread, 0f, ParticleSystem.EmissionAxisBias), 5);
        Assert.Equal(spread, ParticleSystem.SampleConeTheta(spread, 1f, ParticleSystem.EmissionAxisBias), 5);
    }

    [Fact]
    public void EmissionDirection_ConcentratesNearAxis()
    {
        // Over many spawns with a wide cone, the mean polar angle from the emission axis
        // (local +Z) must be smaller than the uniform expectation (spread/2), confirming
        // the fan-out is tightened toward a column. (#2434)
        var node = BaseNode();
        node.BirthRate = 500f;
        node.LifeExp = 100f; // long life so nothing dies during sampling
        node.Velocity = 1f;
        node.Spread = 1.05f;
        node.Mass = 0f; // no gravity: velocity stays at the emission direction we want to measure
        var sys = new ParticleSystem(Compile(node), seed: 42u);

        for (int i = 0; i < 30; i++)
            sys.Update(1f / 30f);

        Assert.True(sys.LiveCount > 200, $"expected a large sample, got {sys.LiveCount}");

        double sumTheta = 0;
        foreach (var p in sys.Particles)
        {
            var v = System.Numerics.Vector3.Normalize(p.Velocity);
            // Polar angle from +Z axis. Clamp for float drift before Acos.
            float cosTheta = System.Math.Clamp(v.Z, -1f, 1f);
            sumTheta += System.MathF.Acos(cosTheta);
        }
        double meanTheta = sumTheta / sys.LiveCount;

        Assert.True(meanTheta < node.Spread * 0.5f,
            $"expected mean cone angle {meanTheta:F3} < uniform midpoint {node.Spread * 0.5f:F3}");
    }

    [Fact]
    public void Update_ZeroDt_IsNoOp()
    {
        var node = BaseNode();
        node.BirthRate = 100f;
        node.LifeExp = 10f;
        var sys = new ParticleSystem(Compile(node), seed: 5u);

        for (int i = 0; i < 30; i++)
            sys.Update(1f / 30f);
        int before = sys.LiveCount;
        Assert.True(before > 0, "expected particles spawned before the zero-dt call");

        sys.Update(0f);

        Assert.Equal(before, sys.LiveCount);
    }

    [Fact]
    public void Update_NegativeDt_IsNoOp()
    {
        var node = BaseNode();
        node.BirthRate = 100f;
        node.LifeExp = 10f;
        var sys = new ParticleSystem(Compile(node), seed: 6u);

        for (int i = 0; i < 30; i++)
            sys.Update(1f / 30f);
        int before = sys.LiveCount;
        Assert.True(before > 0, "expected particles spawned before the negative-dt call");

        sys.Update(-1f);

        Assert.Equal(before, sys.LiveCount);
    }

    [Fact]
    public void Update_HugeDt_DoesNotSpawnUnbounded()
    {
        var node = BaseNode();
        node.BirthRate = 100f;
        node.LifeExp = 10f;
        var sys = new ParticleSystem(Compile(node), seed: 7u);

        // Huge dt is clamped (MaxFrameDt) and spawns are capped (MaxSpawnPerFrame).
        sys.Update(1000f, System.Numerics.Vector3.Zero);

        Assert.True(sys.LiveCount <= 4096,
            $"expected spawn cap to bound LiveCount, got {sys.LiveCount}");
    }

    [Fact]
    public void Update_ZeroLifetime_DoesNotThrow()
    {
        var node = BaseNode();
        node.BirthRate = 100f;
        node.LifeExp = 0f; // compiler clamps to 0; particles die immediately
        var sys = new ParticleSystem(Compile(node), seed: 8u);

        var ex = Record.Exception(() =>
        {
            for (int i = 0; i < 3; i++)
                sys.Update(1f / 30f);
        });

        Assert.Null(ex);
    }
}

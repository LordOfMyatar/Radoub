// Particle simulation ported from rollnw (https://github.com/jd28/rollnw), MIT License, (c) jmd.
// Aurora constants (gravity g≈9.81·mass, drag v*=(1-drag)^dt) cross-referenced with
// nwn_mdl_webviewer (https://github.com/dunahan/nwn_mdl_webviewer), MIT. See repo README. (#2395)

using System.Numerics;

namespace Radoub.UI.Particles;

/// <summary>
/// One live particle. CPU-side simulation state; the render layer reads these.
/// </summary>
public struct Particle
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Age;
    public float Lifetime;
    public float SizeX;
    public float SizeY;
    public float Rotation;
    public float RotationRate;
    public Vector4 Color;
    public int Frame;
}

/// <summary>
/// Pure-CPU particle simulation for a single emitter. Works in a local frame:
/// particles spawn at the emitter world position supplied to <see cref="Update(float, Vector3)"/>
/// and integrate in world space from there. No rendering / OpenGL here.
/// Port of rollnw particle_system.cpp.
/// </summary>
public sealed class ParticleSystem
{
    private const float GravityConstant = 9.81f;

    private readonly CompiledEmitter _emitter;
    private readonly ParticleRng _rng;
    private readonly List<Particle> _particles = new();
    private float _spawnAcc;

    public ParticleSystem(CompiledEmitter emitter, uint seed)
    {
        _emitter = emitter;
        _rng = new ParticleRng(seed);
    }

    public int LiveCount => _particles.Count;

    public IReadOnlyList<Particle> Particles => _particles;

    /// <summary>First live particle (test/inspection convenience).</summary>
    public Particle FirstParticle => _particles[0];

    /// <summary>Advance the simulation, spawning at the origin.</summary>
    public void Update(float dt) => Update(dt, Vector3.Zero);

    /// <summary>
    /// Advance the simulation by <paramref name="dt"/> seconds. Existing particles
    /// age and integrate first; new particles spawn at <paramref name="emitterWorldPos"/>
    /// with Age 0 (not integrated until the next update).
    /// </summary>
    public void Update(float dt, Vector3 emitterWorldPos)
    {
        IntegrateAndCull(dt);
        SpawnForFrame(dt, emitterWorldPos);
    }

    private void IntegrateAndCull(float dt)
    {
        // Locked rollnw constants (#2395 spike): gravity = (0,0,-1)*(9.81*mass)*dt;
        // drag multiplies velocity by (1-drag)^dt each step. NWN world up is +Z, so
        // gravity pulls -Z; negative mass makes a particle rise.
        float dragFactor = MathF.Pow(MathF.Max(1f - _emitter.Drag, 0f), dt);
        var gravity = new Vector3(0f, 0f, -1f) * (GravityConstant * _emitter.Mass);

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age += dt;
            if (p.Age >= p.Lifetime)
            {
                _particles.RemoveAt(i);
                continue;
            }

            // Integrate motion.
            p.Velocity += gravity * dt;
            p.Velocity *= dragFactor;
            p.Position += p.Velocity * dt;
            p.Rotation += p.RotationRate * dt;

            // Over-life attributes.
            float t = p.Lifetime > 0f ? Math.Clamp(p.Age / p.Lifetime, 0f, 1f) : 0f;
            p.SizeX = _emitter.OverLife.SizeX.Eval(t);
            p.SizeY = _emitter.OverLife.SizeY.Eval(t);
            var color = _emitter.OverLife.Color.Eval(t);
            color.W = _emitter.OverLife.Alpha.Eval(t);
            p.Color = color;

            _particles[i] = p;
        }
    }

    private void SpawnForFrame(float dt, Vector3 emitterWorldPos)
    {
        _spawnAcc += _emitter.BirthRate * dt;
        while (_spawnAcc >= 1f)
        {
            Spawn(emitterWorldPos);
            _spawnAcc -= 1f;
        }
    }

    private void Spawn(Vector3 emitterWorldPos)
    {
        float lifetime = _rng.NextRange(_emitter.Lifetime.Min, _emitter.Lifetime.Max);
        float speed = _rng.NextRange(_emitter.Speed.Min, _emitter.Speed.Max);
        Vector3 dir = EmissionDirection();

        var p = new Particle
        {
            Position = emitterWorldPos,
            Velocity = dir * speed,
            Age = 0f,
            Lifetime = lifetime,
            SizeX = _emitter.SizeX.Min,
            SizeY = _emitter.SizeY.Min,
            Rotation = 0f,
            RotationRate = 0f,
            Color = new Vector4(1f, 1f, 1f, 1f),
            Frame = _emitter.FrameStart
        };
        _particles.Add(p);
    }

    /// <summary>
    /// Initial emission direction in the emitter's local frame. Emits along local +Z;
    /// with spread &gt; 0 it samples a cone around +Z.
    /// </summary>
    private Vector3 EmissionDirection()
    {
        // TODO(#2395): confirm emission axis +Z vs +X in render UAT.
        if (_emitter.Spread <= 0f)
            return new Vector3(0f, 0f, 1f);

        float azimuth = _rng.NextRange(0f, MathF.PI * 2f);
        float theta = _rng.NextRange(0f, _emitter.Spread);
        float sinTheta = MathF.Sin(theta);
        var dir = new Vector3(
            MathF.Cos(azimuth) * sinTheta,
            MathF.Sin(azimuth) * sinTheta,
            MathF.Cos(theta));
        return Vector3.Normalize(dir);
    }
}

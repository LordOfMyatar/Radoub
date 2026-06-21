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

    /// <summary>Clamp ceiling for a single frame's dt (100ms) so a frame hitch can't fast-forward the sim.</summary>
    private const float MaxFrameDt = 0.1f;

    /// <summary>Hard cap on particles spawned in one Update call so a pathological BirthRate can't spike allocations.</summary>
    private const int MaxSpawnPerFrame = 4096;

    private readonly CompiledEmitter _emitter;
    private readonly ParticleRng _rng;
    private readonly List<Particle> _particles = new();
    private float _spawnAcc;

    /// <summary>Emitter node's world rotation for the in-progress Update; Spawn reads it to orient emission. (#2395)</summary>
    private Quaternion _currentEmitterRotation = Quaternion.Identity;

    public ParticleSystem(CompiledEmitter emitter, uint seed)
    {
        _emitter = emitter;
        _rng = new ParticleRng(seed);
    }

    public int LiveCount => _particles.Count;

    public IReadOnlyList<Particle> Particles => _particles;

    /// <summary>First live particle (test/inspection convenience).</summary>
    /// <remarks>Intended for tests/inspection only; check <see cref="LiveCount"/> first.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when no particles are live.</exception>
    public Particle FirstParticle => _particles[0];

    /// <summary>Advance the simulation, spawning at the origin with no rotation.</summary>
    public void Update(float dt) => Update(dt, Vector3.Zero, Quaternion.Identity);

    /// <summary>Advance the simulation, spawning at <paramref name="emitterWorldPos"/> with no rotation.</summary>
    public void Update(float dt, Vector3 emitterWorldPos) => Update(dt, emitterWorldPos, Quaternion.Identity);

    /// <summary>
    /// Advance the simulation by <paramref name="dt"/> seconds. Existing particles
    /// age and integrate first; new particles spawn at <paramref name="emitterWorldPos"/>
    /// with Age 0 (not integrated until the next update). The emitter node's world rotation
    /// <paramref name="emitterWorldRotation"/> orients the local emission direction into world
    /// space, so a downward-pointing emitter sprays particles downward. (#2395)
    /// </summary>
    public void Update(float dt, Vector3 emitterWorldPos, Quaternion emitterWorldRotation)
    {
        if (!float.IsFinite(dt) || dt <= 0f)
            return;                          // NaN/negative/zero frame: nothing to advance
        dt = MathF.Min(dt, MaxFrameDt);      // clamp frame hitch

        _currentEmitterRotation = emitterWorldRotation;

        IntegrateAndCull(dt);
        SpawnForFrame(dt, emitterWorldPos);
    }

    /// <summary>
    /// Advance the sim to a steady-state population before the first frame, so an emitter looks
    /// already-running (Aurora pre-warms emitters; without this they fill in slowly from empty).
    /// Steps through roughly one particle lifetime at a fixed dt. (#2395)
    /// </summary>
    public void PreWarm(Vector3 emitterWorldPos) => PreWarm(emitterWorldPos, Quaternion.Identity);

    /// <summary>
    /// Pre-warm overload that orients emission by the emitter node's world rotation
    /// <paramref name="emitterWorldRotation"/>, so the warmed-up population matches the live sim. (#2395)
    /// </summary>
    public void PreWarm(Vector3 emitterWorldPos, Quaternion emitterWorldRotation)
    {
        // Lifetime.Max bounds how long a particle lives; one lifetime of spawning reaches
        // steady-state. Cap the work so a pathological lifetime can't spin forever.
        float life = MathF.Max(_emitter.Lifetime.Max, 0f);
        if (life <= 0f) return;
        const float step = 1f / 30f;
        int steps = (int)MathF.Ceiling(life / step);
        steps = Math.Min(steps, 600); // cap at ~20s of warm-up
        for (int i = 0; i < steps; i++)
            Update(step, emitterWorldPos, emitterWorldRotation);
    }

    private void IntegrateAndCull(float dt)
    {
        // Locked rollnw constants (#2395 spike): gravity = (0,0,-1)*(9.81*mass)*dt;
        // drag multiplies velocity by (1-drag)^dt each step. NWN world up is +Z, so
        // gravity pulls -Z; negative mass makes a particle rise.
        float dragFactor = MathF.Pow(MathF.Max(1f - _emitter.Drag, 0f), dt);
        // Aurora gravity = 9.81 * mass (locked rollnw constant); CompiledEmitter.Grav is the separate point-attractor field, intentionally unused in MVP. See #2395.
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
            // Aurora always applies the emitter's color controllers (colorStart/Mid/End) — the
            // IsTinted flag is NOT a color gate (it means "tint by scene ambient", per rollnw
            // mdl_particle_import). Particle color = texture × this gradient. (#2395)
            var color = _emitter.OverLife.Color.Eval(t);
            color.W = _emitter.OverLife.Alpha.Eval(t);
            p.Color = color;

            _particles[i] = p;
        }
    }

    private void SpawnForFrame(float dt, Vector3 emitterWorldPos)
    {
        _spawnAcc += _emitter.BirthRate * dt;
        int spawned = 0;
        while (_spawnAcc >= 1f)
        {
            if (spawned >= MaxSpawnPerFrame)
            {
                _spawnAcc = 0f;   // drain carry so the cap doesn't run away into the next frame
                break;
            }
            Spawn(emitterWorldPos);
            _spawnAcc -= 1f;
            spawned++;
        }
    }

    private void Spawn(Vector3 emitterWorldPos)
    {
        float lifetime = _rng.NextRange(_emitter.Lifetime.Min, _emitter.Lifetime.Max);
        float speed = _rng.NextRange(_emitter.Speed.Min, _emitter.Speed.Max);
        // EmissionDirection() is in the emitter's local frame; rotate it into world space by the
        // emitter node's world rotation so a downward emitter sprays downward, etc. (#2395)
        Vector3 localDir = EmissionDirection();
        Vector3 dir = Vector3.Transform(localDir, _currentEmitterRotation);

        // Initialize over-life attributes at t=0 so a freshly-spawned particle is visually
        // correct on its first rendered frame (size/color/alpha), not one frame late.
        var color0 = _emitter.OverLife.Color.Eval(0f);
        color0.W = _emitter.OverLife.Alpha.Eval(0f);

        var p = new Particle
        {
            Position = emitterWorldPos,
            Velocity = dir * speed,
            Age = 0f,
            Lifetime = lifetime,
            SizeX = _emitter.OverLife.SizeX.Eval(0f),
            SizeY = _emitter.OverLife.SizeY.Eval(0f),
            Rotation = 0f,
            RotationRate = 0f,
            Color = color0,
            Frame = _emitter.FrameStart
        };
        _particles.Add(p);
    }

    /// <summary>
    /// Initial emission direction in the emitter's LOCAL frame. Emits along local +Z;
    /// with spread &gt; 0 it samples a cone around +Z. The caller (<see cref="Spawn"/>) rotates
    /// this local direction into world space by the emitter node's world rotation, which carries
    /// the node's orientation. (#2395)
    /// </summary>
    private Vector3 EmissionDirection()
    {
        // Emission is local +Z, rotated into world space by the emitter node's world rotation. (#2395)
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

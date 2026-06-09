// Particle simulation ported from rollnw (https://github.com/jd28/rollnw), MIT License, (c) jmd.
// Aurora constants (gravity g≈9.81·mass, drag v*=(1-drag)^dt) cross-referenced with
// nwn_mdl_webviewer (https://github.com/dunahan/nwn_mdl_webviewer), MIT. See repo README. (#2395)

namespace Radoub.UI.Particles;

/// <summary>
/// Deterministic linear-congruential RNG matching rollnw's <c>advance_rng</c>.
/// Same seed always reproduces the same sequence.
/// </summary>
public sealed class ParticleRng
{
    private uint _state;

    public ParticleRng(uint seed)
    {
        _state = seed;
    }

    /// <summary>Next unit float in [0, 1).</summary>
    public float NextUnit()
    {
        // rollnw advance_rng: LCG step, then take top 16 bits as a fraction.
        _state = _state * 1664525u + 1013904223u;
        return (_state >> 8) * (1f / 16777216f);
    }

    /// <summary>
    /// Next float in [min, max). Returns <paramref name="min"/> when
    /// <paramref name="max"/> &lt;= <paramref name="min"/>.
    /// </summary>
    public float NextRange(float min, float max)
    {
        if (max <= min)
            return min;
        return min + (max - min) * NextUnit();
    }
}

// Pure-math evaluator for MDL animation keyframe tracks (#2124).
// Samples Position / Orientation / Scale for a node at time t.
// Returns static values when no tracks are present.

using System;
using System.Numerics;

namespace Radoub.Formats.Mdl;

/// <summary>
/// Stateless helpers that interpolate an <see cref="MdlNode"/>'s keyframe
/// tracks at a given time. Used by the model preview to play animation
/// stances (#2124).
/// </summary>
public static class MdlAnimationEvaluator
{
    public static Vector3 EvaluatePosition(MdlNode node, float time)
    {
        var times = node.PositionTimes;
        var values = node.PositionValues;
        if (times.Length == 0 || values.Length == 0)
            return node.Position;
        if (times.Length == 1 || values.Length == 1)
            return values[0];

        var (a, b, t) = FindSegment(times, time);
        return Vector3.Lerp(values[a], values[b], t);
    }

    public static Quaternion EvaluateOrientation(MdlNode node, float time)
    {
        var times = node.OrientationTimes;
        var values = node.OrientationValues;
        if (times.Length == 0 || values.Length == 0)
            return node.Orientation;
        if (times.Length == 1 || values.Length == 1)
            return values[0];

        var (a, b, t) = FindSegment(times, time);
        return Quaternion.Slerp(values[a], values[b], t);
    }

    public static float EvaluateScale(MdlNode node, float time)
    {
        var times = node.ScaleTimes;
        var values = node.ScaleValues;
        if (times.Length == 0 || values.Length == 0)
            return node.Scale;
        if (times.Length == 1 || values.Length == 1)
            return values[0];

        var (a, b, t) = FindSegment(times, time);
        return values[a] + (values[b] - values[a]) * t;
    }

    /// <summary>
    /// Locate the keyframe segment enclosing <paramref name="time"/>.
    /// Clamps to endpoints so out-of-range times return the nearest key.
    /// Returns (indexA, indexB, t) where t is the 0..1 position within [A, B].
    /// </summary>
    private static (int a, int b, float t) FindSegment(float[] times, float time)
    {
        int last = times.Length - 1;

        if (time <= times[0]) return (0, 0, 0f);
        if (time >= times[last]) return (last, last, 0f);

        // Linear scan — keyframe counts are small (usually <50 per track).
        for (int i = 0; i < last; i++)
        {
            if (time >= times[i] && time <= times[i + 1])
            {
                float span = times[i + 1] - times[i];
                float t = span > 0 ? (time - times[i]) / span : 0f;
                return (i, i + 1, t);
            }
        }

        // Shouldn't reach here given the clamps above.
        return (last, last, 0f);
    }
}

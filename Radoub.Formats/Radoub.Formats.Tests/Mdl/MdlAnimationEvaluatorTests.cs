using System.Numerics;
using Radoub.Formats.Mdl;
using Xunit;

namespace Radoub.Formats.Tests.Mdl;

public class MdlAnimationEvaluatorTests
{
    [Fact]
    public void EvaluatePosition_NoTracks_ReturnsStaticPosition()
    {
        var node = new MdlNode { Position = new Vector3(1, 2, 3) };

        var result = MdlAnimationEvaluator.EvaluatePosition(node, 0.5f);

        Assert.Equal(new Vector3(1, 2, 3), result);
    }

    [Fact]
    public void EvaluatePosition_SingleKey_ReturnsThatValue()
    {
        var node = new MdlNode
        {
            PositionTimes = new[] { 0f },
            PositionValues = new[] { new Vector3(5, 5, 5) },
            Position = new Vector3(999, 999, 999),
        };

        var result = MdlAnimationEvaluator.EvaluatePosition(node, 10f);

        Assert.Equal(new Vector3(5, 5, 5), result);
    }

    [Fact]
    public void EvaluatePosition_BetweenKeys_LinearlyInterpolates()
    {
        var node = new MdlNode
        {
            PositionTimes = new[] { 0f, 1f },
            PositionValues = new[] { Vector3.Zero, new Vector3(10, 20, 30) },
        };

        var result = MdlAnimationEvaluator.EvaluatePosition(node, 0.5f);

        Assert.Equal(5f, result.X, 4);
        Assert.Equal(10f, result.Y, 4);
        Assert.Equal(15f, result.Z, 4);
    }

    [Fact]
    public void EvaluatePosition_BeforeFirstKey_ClampsToFirst()
    {
        var node = new MdlNode
        {
            PositionTimes = new[] { 1f, 2f },
            PositionValues = new[] { new Vector3(5, 0, 0), new Vector3(10, 0, 0) },
        };

        var result = MdlAnimationEvaluator.EvaluatePosition(node, -1f);

        Assert.Equal(new Vector3(5, 0, 0), result);
    }

    [Fact]
    public void EvaluatePosition_AfterLastKey_ClampsToLast()
    {
        var node = new MdlNode
        {
            PositionTimes = new[] { 0f, 1f },
            PositionValues = new[] { new Vector3(5, 0, 0), new Vector3(10, 0, 0) },
        };

        var result = MdlAnimationEvaluator.EvaluatePosition(node, 99f);

        Assert.Equal(new Vector3(10, 0, 0), result);
    }

    [Fact]
    public void EvaluateOrientation_SlerpsBetweenQuats()
    {
        // Identity at t=0, 90° around Y at t=1. At t=0.5, rotating UnitZ should
        // land halfway between UnitZ and UnitX (at 45° around Y from Z).
        var q0 = Quaternion.Identity;
        var q1 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);
        var node = new MdlNode
        {
            OrientationTimes = new[] { 0f, 1f },
            OrientationValues = new[] { q0, q1 },
        };

        var result = MdlAnimationEvaluator.EvaluateOrientation(node, 0.5f);
        var rotated = Vector3.Transform(Vector3.UnitZ, result);
        var expected = Vector3.Transform(Vector3.UnitZ,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4f));

        Assert.Equal(expected.X, rotated.X, 4);
        Assert.Equal(expected.Y, rotated.Y, 4);
        Assert.Equal(expected.Z, rotated.Z, 4);
    }

    [Fact]
    public void EvaluateScale_InterpolatesFloats()
    {
        var node = new MdlNode
        {
            ScaleTimes = new[] { 0f, 2f },
            ScaleValues = new[] { 1f, 5f },
        };

        var result = MdlAnimationEvaluator.EvaluateScale(node, 1f);

        Assert.Equal(3f, result, 4);
    }

    [Fact]
    public void EvaluateScale_NoTrack_ReturnsStaticScale()
    {
        var node = new MdlNode { Scale = 2.5f };

        var result = MdlAnimationEvaluator.EvaluateScale(node, 42f);

        Assert.Equal(2.5f, result);
    }
}

using System.Numerics;
using Radoub.UI.Particles;

namespace Radoub.UI.Tests.Particles;

public class CurveTests
{
    [Fact]
    public void CurveF_Empty_ReturnsZero()
    {
        var c = new CurveF();
        Assert.Equal(0f, c.Eval(0.5f));
    }

    [Fact]
    public void CurveF_SingleKey_ReturnsThatValue()
    {
        var c = new CurveF();
        c.Keys.Add(new CurveKey { Time = 0.3f, Value = 7f });
        Assert.Equal(7f, c.Eval(0f));
        Assert.Equal(7f, c.Eval(0.3f));
        Assert.Equal(7f, c.Eval(1f));
    }

    [Fact]
    public void CurveF_Endpoints_ReturnExactValues()
    {
        var c = new CurveF();
        c.Keys.Add(new CurveKey { Time = 0f, Value = 2f });
        c.Keys.Add(new CurveKey { Time = 1f, Value = 4f });
        Assert.Equal(2f, c.Eval(0f));
        Assert.Equal(4f, c.Eval(1f));
    }

    [Fact]
    public void CurveF_Midpoint_LinearInterpolation()
    {
        var c = new CurveF();
        c.Keys.Add(new CurveKey { Time = 0f, Value = 2f });
        c.Keys.Add(new CurveKey { Time = 1f, Value = 4f });
        Assert.Equal(3f, c.Eval(0.5f), 5);
    }

    [Fact]
    public void CurveF_ClampBelowFirstAndAboveLast()
    {
        var c = new CurveF();
        c.Keys.Add(new CurveKey { Time = 0.2f, Value = 5f });
        c.Keys.Add(new CurveKey { Time = 0.8f, Value = 9f });
        Assert.Equal(5f, c.Eval(0f));
        Assert.Equal(9f, c.Eval(1f));
    }

    [Fact]
    public void CurveF_ThreeKeys_InterpolatesCorrectSegment()
    {
        var c = new CurveF();
        c.Keys.Add(new CurveKey { Time = 0f, Value = 0f });
        c.Keys.Add(new CurveKey { Time = 0.5f, Value = 10f });
        c.Keys.Add(new CurveKey { Time = 1f, Value = 0f });
        Assert.Equal(5f, c.Eval(0.25f), 5);
        Assert.Equal(5f, c.Eval(0.75f), 5);
        Assert.Equal(10f, c.Eval(0.5f), 5);
    }

    [Fact]
    public void Gradient_Empty_ReturnsZero()
    {
        var g = new Gradient();
        Assert.Equal(Vector4.Zero, g.Eval(0.5f));
    }

    [Fact]
    public void Gradient_SingleKey_ReturnsThatValue()
    {
        var g = new Gradient();
        var v = new Vector4(0.1f, 0.2f, 0.3f, 1f);
        g.Keys.Add(new GradientKey { Time = 0.4f, Value = v });
        Assert.Equal(v, g.Eval(0f));
        Assert.Equal(v, g.Eval(1f));
    }

    [Fact]
    public void Gradient_Endpoints_ReturnExactValues()
    {
        var g = new Gradient();
        var a = new Vector4(0f, 0f, 0f, 1f);
        var b = new Vector4(1f, 1f, 1f, 1f);
        g.Keys.Add(new GradientKey { Time = 0f, Value = a });
        g.Keys.Add(new GradientKey { Time = 1f, Value = b });
        Assert.Equal(a, g.Eval(0f));
        Assert.Equal(b, g.Eval(1f));
    }

    [Fact]
    public void Gradient_Midpoint_PerChannelLerp()
    {
        var g = new Gradient();
        g.Keys.Add(new GradientKey { Time = 0f, Value = new Vector4(0f, 0f, 0f, 0f) });
        g.Keys.Add(new GradientKey { Time = 1f, Value = new Vector4(1f, 2f, 3f, 4f) });
        var mid = g.Eval(0.5f);
        Assert.Equal(0.5f, mid.X, 5);
        Assert.Equal(1f, mid.Y, 5);
        Assert.Equal(1.5f, mid.Z, 5);
        Assert.Equal(2f, mid.W, 5);
    }

    [Fact]
    public void Gradient_ClampOutsideRange()
    {
        var g = new Gradient();
        var a = new Vector4(0.2f, 0.2f, 0.2f, 1f);
        var b = new Vector4(0.8f, 0.8f, 0.8f, 1f);
        g.Keys.Add(new GradientKey { Time = 0.2f, Value = a });
        g.Keys.Add(new GradientKey { Time = 0.8f, Value = b });
        Assert.Equal(a, g.Eval(0f));
        Assert.Equal(b, g.Eval(1f));
    }
}

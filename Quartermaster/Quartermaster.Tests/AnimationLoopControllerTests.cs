using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for the "Loop all animations" diagnostic sequencer (#2140). The controller is a pure
/// state machine — it owns the index/cycle bookkeeping so the UI partial only has to start a
/// timer, set the active animation, and show the overlay name. No Avalonia dependency here.
/// </summary>
public class AnimationLoopControllerTests
{
    [Fact]
    public void Start_WithAnimations_BeginsAtFirstAnimationCycleOne()
    {
        var c = new AnimationLoopController(animationCount: 3, maxCycles: 3);

        Assert.True(c.Start());
        Assert.True(c.IsRunning);
        Assert.Equal(0, c.CurrentIndex);
        Assert.Equal(1, c.CurrentCycle);
    }

    [Fact]
    public void Start_WithNoAnimations_DoesNotRun()
    {
        var c = new AnimationLoopController(animationCount: 0, maxCycles: 3);

        Assert.False(c.Start());
        Assert.False(c.IsRunning);
    }

    [Fact]
    public void Advance_WalksThroughAnimationsInOrder()
    {
        var c = new AnimationLoopController(animationCount: 3, maxCycles: 3);
        c.Start();

        Assert.Equal(0, c.CurrentIndex);
        Assert.True(c.Advance());
        Assert.Equal(1, c.CurrentIndex);
        Assert.Equal(1, c.CurrentCycle);
        Assert.True(c.Advance());
        Assert.Equal(2, c.CurrentIndex);
        Assert.Equal(1, c.CurrentCycle);
    }

    [Fact]
    public void Advance_PastLastAnimation_WrapsToNextCycle()
    {
        var c = new AnimationLoopController(animationCount: 2, maxCycles: 3);
        c.Start();

        Assert.True(c.Advance()); // -> idx 1, cycle 1
        Assert.True(c.Advance()); // wrap -> idx 0, cycle 2
        Assert.Equal(0, c.CurrentIndex);
        Assert.Equal(2, c.CurrentCycle);
    }

    [Fact]
    public void Advance_AfterMaxCycles_StopsAndReturnsFalse()
    {
        // 1 animation, 3 cycles: plays it once per cycle, stops after the 3rd.
        var c = new AnimationLoopController(animationCount: 1, maxCycles: 3);
        c.Start();

        Assert.Equal(1, c.CurrentCycle);
        Assert.True(c.Advance());  // cycle 2
        Assert.Equal(2, c.CurrentCycle);
        Assert.True(c.Advance());  // cycle 3
        Assert.Equal(3, c.CurrentCycle);
        Assert.False(c.Advance()); // exhausted -> stop
        Assert.False(c.IsRunning);
    }

    [Fact]
    public void Stop_HaltsAndAdvanceBecomesNoOp()
    {
        var c = new AnimationLoopController(animationCount: 3, maxCycles: 3);
        c.Start();
        c.Stop();

        Assert.False(c.IsRunning);
        Assert.False(c.Advance());
    }

    [Fact]
    public void Start_ClampsMaxCyclesToAtLeastOne()
    {
        var c = new AnimationLoopController(animationCount: 2, maxCycles: 0);
        c.Start();

        // Even with a bad maxCycles, one full pass must run.
        Assert.True(c.Advance());  // idx 1
        Assert.False(c.Advance()); // would wrap to cycle 2, but max is clamped to 1 -> stop
    }
}

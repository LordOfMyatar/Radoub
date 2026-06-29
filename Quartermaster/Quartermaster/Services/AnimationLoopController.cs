using System;

namespace Quartermaster.Services;

/// <summary>
/// Pure state machine driving the "Loop all animations" diagnostic (#2140). It owns only the
/// index/cycle bookkeeping — selecting the animation, starting the preview timer, and showing
/// the on-screen overlay name are the UI partial's job. Kept Avalonia-free so it is unit-testable
/// without FlaUI.
///
/// Lifecycle: <see cref="Start"/> to begin at animation 0 / cycle 1, then <see cref="Advance"/>
/// once each animation finishes its natural length. Advance walks the list in order, wraps to the
/// next cycle past the last animation, and returns false (auto-stopping) once <c>maxCycles</c>
/// full passes are done.
/// </summary>
public sealed class AnimationLoopController
{
    private readonly int _animationCount;
    private readonly int _maxCycles;

    public AnimationLoopController(int animationCount, int maxCycles)
    {
        _animationCount = Math.Max(0, animationCount);
        // A non-positive maxCycles still runs one full pass — a diagnostic that played nothing
        // would be a silent no-op, the exact failure mode this tool exists to avoid.
        _maxCycles = Math.Max(1, maxCycles);
    }

    /// <summary>Zero-based index of the animation currently playing.</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>One-based cycle counter (1.._maxCycles).</summary>
    public int CurrentCycle { get; private set; }

    public bool IsRunning { get; private set; }

    public int AnimationCount => _animationCount;
    public int MaxCycles => _maxCycles;

    /// <summary>
    /// Begin the loop. Returns false (and stays stopped) when there are no animations to cycle.
    /// </summary>
    public bool Start()
    {
        if (_animationCount <= 0)
        {
            IsRunning = false;
            return false;
        }

        CurrentIndex = 0;
        CurrentCycle = 1;
        IsRunning = true;
        return true;
    }

    /// <summary>
    /// Move to the next animation. Wraps to the next cycle past the last animation. Returns false
    /// — and stops the loop — once the configured number of cycles is exhausted or if not running.
    /// </summary>
    public bool Advance()
    {
        if (!IsRunning) return false;

        int next = CurrentIndex + 1;
        if (next < _animationCount)
        {
            CurrentIndex = next;
            return true;
        }

        // Past the last animation: start the next cycle, unless we have finished the last one.
        if (CurrentCycle >= _maxCycles)
        {
            Stop();
            return false;
        }

        CurrentCycle++;
        CurrentIndex = 0;
        return true;
    }

    public void Stop()
    {
        IsRunning = false;
    }
}

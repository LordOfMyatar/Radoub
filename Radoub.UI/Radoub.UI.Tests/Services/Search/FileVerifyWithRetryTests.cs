using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

/// <summary>
/// Unit tests for <see cref="FileVerifyWithRetry"/> — proves the retry
/// semantics around the post-rename file-existence verify (#2181).
/// Uses injected probe + sleep delegates so no real I/O or wall-clock sleeps.
/// </summary>
public class FileVerifyWithRetryTests
{
    [Fact]
    public async Task WaitForAsync_ImmediateMatch_SkipsSleep()
    {
        var sleepCount = 0;
        Func<int, Task> sleep = _ => { sleepCount++; return Task.CompletedTask; };

        var result = await FileVerifyWithRetry.WaitForAsync(
            probe: () => true,
            desired: true,
            maxAttempts: 4,
            delayMs: 50,
            sleep: sleep);

        Assert.True(result);
        Assert.Equal(0, sleepCount);
    }

    [Fact]
    public async Task WaitForAsync_NeverMatches_ReturnsFalseAfterMaxAttempts()
    {
        var probeCount = 0;
        var sleepCount = 0;
        Func<int, Task> sleep = _ => { sleepCount++; return Task.CompletedTask; };

        var result = await FileVerifyWithRetry.WaitForAsync(
            probe: () => { probeCount++; return false; },
            desired: true,
            maxAttempts: 4,
            delayMs: 50,
            sleep: sleep);

        Assert.False(result);
        Assert.Equal(4, probeCount);
        Assert.Equal(3, sleepCount);  // sleeps BETWEEN attempts, not after last
    }

    [Fact]
    public async Task WaitForAsync_MatchesOnSecondAttempt_SleepsOnce()
    {
        var probeResults = new Queue<bool>(new[] { false, true });
        var sleepCount = 0;
        Func<int, Task> sleep = _ => { sleepCount++; return Task.CompletedTask; };

        var result = await FileVerifyWithRetry.WaitForAsync(
            probe: () => probeResults.Dequeue(),
            desired: true,
            maxAttempts: 4,
            delayMs: 50,
            sleep: sleep);

        Assert.True(result);
        Assert.Equal(1, sleepCount);
    }

    [Fact]
    public async Task WaitForAsync_DesiredFalse_PollsForFalse()
    {
        // Verifies the desired parameter works in both directions (used for
        // both WaitForGoneAsync and WaitForExistsAsync).
        var probeResults = new Queue<bool>(new[] { true, true, false });
        var sleepCount = 0;
        Func<int, Task> sleep = _ => { sleepCount++; return Task.CompletedTask; };

        var result = await FileVerifyWithRetry.WaitForAsync(
            probe: () => probeResults.Dequeue(),
            desired: false,
            maxAttempts: 4,
            delayMs: 50,
            sleep: sleep);

        Assert.True(result);
        Assert.Equal(2, sleepCount);
    }

    [Fact]
    public async Task WaitForAsync_MaxAttemptsZero_StillProbesOnce()
    {
        var probeCount = 0;
        var result = await FileVerifyWithRetry.WaitForAsync(
            probe: () => { probeCount++; return true; },
            desired: true,
            maxAttempts: 0,
            delayMs: 50,
            sleep: _ => Task.CompletedTask);

        // Implementation clamps maxAttempts to >= 1.
        Assert.True(result);
        Assert.Equal(1, probeCount);
    }

    [Fact]
    public async Task WaitForGoneAsync_FileAlreadyGone_ReturnsTrueWithoutSleep()
    {
        var sleepCount = 0;
        var result = await FileVerifyWithRetry.WaitForGoneAsync(
            "/definitely/does/not/exist/anywhere.tmp",
            maxAttempts: 4,
            delayMs: 50,
            sleep: _ => { sleepCount++; return Task.CompletedTask; });

        Assert.True(result);
        Assert.Equal(0, sleepCount);
    }
}

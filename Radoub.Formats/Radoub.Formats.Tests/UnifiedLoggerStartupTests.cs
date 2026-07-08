using Radoub.Formats.Logging;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for cold-start instrumentation on UnifiedLogger (#2128, measure-only).
/// </summary>
public class UnifiedLoggerStartupTests
{
    [Fact]
    public void StartupElapsedMs_IsNonNegative()
    {
        // The stopwatch starts at static init and only ever moves forward.
        Assert.True(UnifiedLogger.StartupElapsedMs >= 0);
    }

    [Fact]
    public void StartupElapsedMs_IsMonotonicNonDecreasing()
    {
        var first = UnifiedLogger.StartupElapsedMs;
        var second = UnifiedLogger.StartupElapsedMs;
        Assert.True(second >= first);
    }

    [Fact]
    public void LogStartupMilestone_DoesNotThrow()
    {
        // Measure-only helper: must never fault startup even if the logger is unconfigured.
        var ex = Record.Exception(() => UnifiedLogger.LogStartupMilestone("test-milestone"));
        Assert.Null(ex);
    }
}

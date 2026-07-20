using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Coverage for the deferred startup-cleanup entry point (#2647).
/// </summary>
public class StartupCleanupCoordinatorTests : IDisposable
{
    public StartupCleanupCoordinatorTests() => StartupCleanupCoordinator.ResetForTests();

    public void Dispose() => StartupCleanupCoordinator.ResetForTests();

    [Fact]
    public void RunDeferredCleanup_ReturnsImmediately_DoesNotBlockCaller()
    {
        // The whole point is to keep this off the first-paint path: the call must return
        // without waiting on any directory walk.
        var sw = System.Diagnostics.Stopwatch.StartNew();

        StartupCleanupCoordinator.RunDeferredCleanup(10, 30);

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"RunDeferredCleanup blocked for {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void RunDeferredCleanup_DoesNotThrowIntoCaller()
    {
        // Called from UI event handlers — a cleanup failure must never fault the handler.
        var ex = Record.Exception(() => StartupCleanupCoordinator.RunDeferredCleanup(10, 30));

        Assert.Null(ex);
    }

    [Fact]
    public void RunDeferredCleanup_IsIdempotent_SecondCallIsNoOp()
    {
        // Tools may wire this to both Loaded and Opened; the sweep must still run once.
        StartupCleanupCoordinator.RunDeferredCleanup(10, 30);

        var ex = Record.Exception(() => StartupCleanupCoordinator.RunDeferredCleanup(10, 30));

        Assert.Null(ex);
    }

    [Fact]
    public void ResetForTests_AllowsAFreshRun()
    {
        StartupCleanupCoordinator.RunDeferredCleanup(10, 30);
        StartupCleanupCoordinator.ResetForTests();

        var ex = Record.Exception(() => StartupCleanupCoordinator.RunDeferredCleanup(10, 30));

        Assert.Null(ex);
    }
}

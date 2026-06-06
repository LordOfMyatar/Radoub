using System;
using System.Threading;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Shared;

public class FlaUIGlobalMutexTests
{
    // Use a unique mutex name per test run so we don't collide with a real
    // FlaUI run on the same machine (or with other test cases below).
    private static string UniqueName() =>
        $"Local\\Radoub.FlaUI.UnitTest.{Guid.NewGuid():N}";

    /// <summary>
    /// Runs <paramref name="body"/> on a dedicated foreground thread and returns
    /// its result. Mutex is reentrant per-thread, so a *different* thread is
    /// required to observe contention. We use an explicit Thread rather than
    /// Task.Run because the FlaUI assembly runs serially and a soak run can
    /// starve the threadpool — a queued Task.Run may not be scheduled within the
    /// short acquisition timeout, which made these tests flake on the first
    /// (coldest) soak pass (#2360 reliability theme). A dedicated thread starts
    /// deterministically regardless of pool pressure.
    /// </summary>
    private static T OnForeignThread<T>(Func<T> body)
    {
        T result = default!;
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { captured = ex; }
        })
        { IsBackground = false };
        thread.Start();
        thread.Join();
        if (captured != null)
            throw captured;
        return result;
    }

    [Fact]
    public void Acquire_ReturnsHandleWhenUncontested()
    {
        using var mutex = FlaUIGlobalMutex.Acquire(UniqueName(), TimeSpan.FromSeconds(1));

        Assert.NotNull(mutex);
    }

    [Fact]
    public void Acquire_ThrowsTimeoutExceptionWithMutexNameInMessage_WhenAlreadyHeld()
    {
        var name = UniqueName();
        using var first = FlaUIGlobalMutex.Acquire(name, TimeSpan.FromSeconds(1));

        // Foreign thread so we don't recursively acquire on the same thread
        // (Mutex is reentrant per-thread).
        var ex = Assert.Throws<TimeoutException>(() =>
            OnForeignThread(() => FlaUIGlobalMutex.Acquire(name, TimeSpan.FromMilliseconds(200))));

        Assert.Contains(name, ex.Message);
    }

    [Fact]
    public void Release_AllowsSubsequentAcquireFromAnotherThread()
    {
        var name = UniqueName();
        var first = FlaUIGlobalMutex.Acquire(name, TimeSpan.FromSeconds(1));
        first.Dispose();

        var second = OnForeignThread(() =>
            FlaUIGlobalMutex.Acquire(name, TimeSpan.FromSeconds(1)));
        second.Dispose();
    }

    [Fact]
    public void Acquire_ZeroTimeout_ThrowsImmediatelyWhenContended()
    {
        var name = UniqueName();
        using var first = FlaUIGlobalMutex.Acquire(name, TimeSpan.FromSeconds(1));

        Assert.Throws<TimeoutException>(() =>
            OnForeignThread(() => FlaUIGlobalMutex.Acquire(name, TimeSpan.Zero)));
    }
}

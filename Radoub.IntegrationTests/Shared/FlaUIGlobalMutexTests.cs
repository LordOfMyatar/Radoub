using System;
using System.Threading;
using System.Threading.Tasks;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Shared;

public class FlaUIGlobalMutexTests
{
    // Use a unique mutex name per test run so we don't collide with a real
    // FlaUI run on the same machine (or with other test cases below).
    private static string UniqueName() =>
        $"Local\\Radoub.FlaUI.UnitTest.{Guid.NewGuid():N}";

    [Fact]
    public void Acquire_ReturnsHandleWhenUncontested()
    {
        using var mutex = FlaUIGlobalMutex.Acquire(UniqueName(), TimeSpan.FromSeconds(1));

        Assert.NotNull(mutex);
    }

    [Fact]
    public async Task Acquire_ThrowsTimeoutExceptionWithMutexNameInMessage_WhenAlreadyHeld()
    {
        var name = UniqueName();
        using var first = FlaUIGlobalMutex.Acquire(name, TimeSpan.FromSeconds(1));

        // Foreign thread so we don't recursively acquire on the same thread
        // (Mutex is reentrant per-thread).
        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            Task.Run(() => FlaUIGlobalMutex.Acquire(name, TimeSpan.FromMilliseconds(200))));

        Assert.Contains(name, ex.Message);
    }

    [Fact]
    public async Task Release_AllowsSubsequentAcquireFromAnotherThread()
    {
        var name = UniqueName();
        var first = FlaUIGlobalMutex.Acquire(name, TimeSpan.FromSeconds(1));
        first.Dispose();

        var second = await Task.Run(() =>
            FlaUIGlobalMutex.Acquire(name, TimeSpan.FromSeconds(1)));
        second.Dispose();
    }

    [Fact]
    public async Task Acquire_ZeroTimeout_ThrowsImmediatelyWhenContended()
    {
        var name = UniqueName();
        using var first = FlaUIGlobalMutex.Acquire(name, TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            Task.Run(() => FlaUIGlobalMutex.Acquire(name, TimeSpan.Zero)));
    }
}

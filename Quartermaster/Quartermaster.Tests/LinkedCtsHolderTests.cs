using System.Threading;
using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for the palette-cache CTS lifecycle helper (#2299). The bug: closing the
/// window mid-cache-build disposed the linked CTS without cancelling it first, so the
/// in-flight scan ran to completion and froze the UI. The helper enforces
/// Cancel -> Dispose -> null on teardown and on restart.
/// </summary>
public class LinkedCtsHolderTests
{
    [Fact]
    public void CancelAndDispose_SignalsCancellationBeforeDisposing()
    {
        var holder = new LinkedCtsHolder();
        var token = holder.Restart(null);

        Assert.False(token.IsCancellationRequested);

        holder.CancelAndDispose();

        // The token captured before teardown must observe cancellation — proves we
        // Cancel() rather than just Dispose() (the #2299 regression).
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void CancelAndDispose_IsIdempotent()
    {
        var holder = new LinkedCtsHolder();
        holder.Restart(null);

        holder.CancelAndDispose();
        // Second call must be a no-op, not a double-dispose throw.
        holder.CancelAndDispose();
    }

    [Fact]
    public void CancelAndDispose_WithNoActiveToken_DoesNotThrow()
    {
        var holder = new LinkedCtsHolder();
        // Never started — teardown must be safe.
        holder.CancelAndDispose();
    }

    [Fact]
    public void Restart_CancelsAndDisposesPreviousToken()
    {
        var holder = new LinkedCtsHolder();
        var first = holder.Restart(null);

        var second = holder.Restart(null);

        Assert.True(first.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Restart_WithParentToken_CancelsWhenParentCancels()
    {
        var holder = new LinkedCtsHolder();
        using var parent = new CancellationTokenSource();

        var token = holder.Restart(parent.Token);
        Assert.False(token.IsCancellationRequested);

        parent.Cancel();

        Assert.True(token.IsCancellationRequested);
        holder.CancelAndDispose();
    }

    [Fact]
    public void Restart_WithAlreadyCancelledParent_ProducesCancelledToken()
    {
        var holder = new LinkedCtsHolder();
        using var parent = new CancellationTokenSource();
        parent.Cancel();

        var token = holder.Restart(parent.Token);

        Assert.True(token.IsCancellationRequested);
        holder.CancelAndDispose();
    }
}

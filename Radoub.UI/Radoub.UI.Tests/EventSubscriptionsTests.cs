using Xunit;
using Radoub.UI.Services;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for EventSubscriptions — the helper that lets panels track lambda
/// subscriptions so they can be unwired on Unloaded (#2034).
/// </summary>
public class EventSubscriptionsTests
{
    [Fact]
    public void Track_ExecutesAttachImmediately()
    {
        var subs = new EventSubscriptions();
        var attached = false;

        subs.Track(attach: () => attached = true, detach: () => { });

        Assert.True(attached);
    }

    [Fact]
    public void Count_ReturnsTrackedSubscriptions()
    {
        var subs = new EventSubscriptions();
        subs.Track(attach: () => { }, detach: () => { });
        subs.Track(attach: () => { }, detach: () => { });
        subs.Track(attach: () => { }, detach: () => { });

        Assert.Equal(3, subs.Count);
    }

    [Fact]
    public void DetachAll_RunsEveryDetachAction()
    {
        var subs = new EventSubscriptions();
        var detachCount = 0;

        subs.Track(attach: () => { }, detach: () => detachCount++);
        subs.Track(attach: () => { }, detach: () => detachCount++);
        subs.Track(attach: () => { }, detach: () => detachCount++);

        subs.DetachAll();

        Assert.Equal(3, detachCount);
    }

    [Fact]
    public void DetachAll_ClearsTrackedSubscriptions()
    {
        var subs = new EventSubscriptions();
        subs.Track(attach: () => { }, detach: () => { });
        subs.Track(attach: () => { }, detach: () => { });

        subs.DetachAll();

        Assert.Equal(0, subs.Count);
    }

    [Fact]
    public void DetachAll_IsIdempotent()
    {
        var subs = new EventSubscriptions();
        var detachCount = 0;
        subs.Track(attach: () => { }, detach: () => detachCount++);

        subs.DetachAll();
        subs.DetachAll();

        Assert.Equal(1, detachCount);
    }

    [Fact]
    public void Track_AfterDetachAll_StillWorks()
    {
        var subs = new EventSubscriptions();
        subs.Track(attach: () => { }, detach: () => { });
        subs.DetachAll();

        var attached = false;
        subs.Track(attach: () => attached = true, detach: () => { });

        Assert.True(attached);
        Assert.Equal(1, subs.Count);
    }
}

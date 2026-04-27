using System;
using System.Collections.Generic;

namespace Radoub.UI.Services;

/// <summary>
/// Tracks (attach, detach) pairs so a control can release every subscription
/// in one call on <c>Unloaded</c>. Solves the lambda-closure leak from #2034 —
/// inline <c>cb.IsCheckedChanged += (s,e) => ...</c> handlers can't be
/// unsubscribed because the delegate reference is unreachable.
///
/// Usage:
/// <code>
/// var subs = new EventSubscriptions();
/// EventHandler&lt;RoutedEventArgs&gt; handler = (s, e) => DoThing();
/// subs.Track(
///     attach: () => button.Click += handler,
///     detach: () => button.Click -= handler);
/// // ...later, on Unloaded:
/// subs.DetachAll();
/// </code>
/// </summary>
public sealed class EventSubscriptions
{
    private readonly List<Action> _detachActions = new();

    public int Count => _detachActions.Count;

    /// <summary>
    /// Run <paramref name="attach"/> immediately and remember <paramref name="detach"/>
    /// so it can be invoked by <see cref="DetachAll"/>.
    /// </summary>
    public void Track(Action attach, Action detach)
    {
        ArgumentNullException.ThrowIfNull(attach);
        ArgumentNullException.ThrowIfNull(detach);

        attach();
        _detachActions.Add(detach);
    }

    /// <summary>
    /// Run every tracked detach action and clear the list. Idempotent —
    /// safe to call multiple times.
    /// </summary>
    public void DetachAll()
    {
        foreach (var detach in _detachActions)
            detach();
        _detachActions.Clear();
    }
}

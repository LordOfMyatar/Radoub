using System;
using System.Threading;

namespace Quartermaster.Services;

/// <summary>
/// Owns the lifecycle of a single background-work <see cref="CancellationTokenSource"/>,
/// optionally linked to a parent token. Enforces the Cancel -> Dispose -> null discipline
/// (the #2262 FileBrowserPanelBase pattern) so tearing down or restarting the work never
/// orphans an in-flight scan.
///
/// #2299: the palette/HAK cache build was disposed on window close without being cancelled
/// first, so the close handler waited for the synchronous tail of the scan and the UI froze.
/// Routing that CTS through this helper guarantees cancellation is signalled before disposal.
/// </summary>
public sealed class LinkedCtsHolder
{
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Cancel and dispose any existing source, then create a fresh one (linked to
    /// <paramref name="parentToken"/> when supplied) and return its token.
    /// </summary>
    public CancellationToken Restart(CancellationToken? parentToken)
    {
        CancelAndDispose();

        _cts = parentToken.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(parentToken.Value)
            : new CancellationTokenSource();

        return _cts.Token;
    }

    /// <summary>
    /// Cancel the in-flight work and dispose its source. Idempotent and null-safe.
    /// </summary>
    public void CancelAndDispose()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}

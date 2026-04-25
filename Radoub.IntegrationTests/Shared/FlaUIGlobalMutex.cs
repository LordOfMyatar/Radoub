using System;
using System.Threading;

namespace Radoub.IntegrationTests.Shared;

/// <summary>
/// Cross-process serialization for FlaUI tests. Desktop input (focus,
/// foreground window, the UIA client) is process-global, so two simultaneous
/// FlaUI test runs on the same machine — even from different test
/// assemblies, IDE Test Explorer + terminal, or two developer sessions —
/// will sabotage each other.
///
/// <para>
/// The default mutex name (<see cref="DefaultName"/>) is `Global\` so it
/// covers user sessions on a shared runner. Tests should use
/// <see cref="Acquire(string, TimeSpan)"/> with a per-test name to avoid
/// colliding with a real FlaUI run on the same machine. (#1526)
/// </para>
/// </summary>
public sealed class FlaUIGlobalMutex : IDisposable
{
    public const string DefaultName = "Global\\Radoub.FlaUI.SerialExecution";

    /// <summary>Default acquisition timeout (#1526 decision: 30 seconds).</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly Mutex _mutex;
    private bool _released;

    private FlaUIGlobalMutex(Mutex mutex)
    {
        _mutex = mutex;
    }

    /// <summary>
    /// Acquire the named mutex or throw <see cref="TimeoutException"/> with a
    /// clear message if another FlaUI run is holding it.
    /// </summary>
    public static FlaUIGlobalMutex Acquire(string name, TimeSpan timeout)
    {
        var mutex = new Mutex(initiallyOwned: false, name: name);
        bool acquired;
        try
        {
            acquired = mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            // Previous holder process crashed without releasing. We now own
            // the mutex; treat as success and proceed.
            acquired = true;
        }

        if (!acquired)
        {
            mutex.Dispose();
            throw new TimeoutException(
                $"Could not acquire FlaUI mutex '{name}' within {timeout}. " +
                "Another FlaUI test run is holding the lock — wait for it to " +
                "finish or kill the holder process.");
        }

        return new FlaUIGlobalMutex(mutex);
    }

    /// <summary>Acquire <see cref="DefaultName"/> with <see cref="DefaultTimeout"/>.</summary>
    public static FlaUIGlobalMutex AcquireDefault() => Acquire(DefaultName, DefaultTimeout);

    public void Dispose()
    {
        if (_released) return;
        _released = true;

        try { _mutex.ReleaseMutex(); } catch { /* released or never owned */ }
        _mutex.Dispose();
    }
}

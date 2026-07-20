using Radoub.Formats.Logging;

namespace Radoub.UI.Services;

/// <summary>
/// Runs startup housekeeping (log-session retention, backup expiry) off the first-paint
/// path (#2647).
///
/// Both sweeps walk directories and recursively delete — 100 ms-1 s of blocking I/O
/// depending on how much history has accumulated. Neither result is needed to paint a
/// window, so tools invoke this once from their window's Opened handler instead of from
/// App.Initialize / Program.Main.
/// </summary>
public static class StartupCleanupCoordinator
{
    private static int _started;

    /// <summary>
    /// Kick off cleanup on a background thread. Safe to call from a UI event handler:
    /// returns immediately and never throws into the caller.
    ///
    /// Idempotent per process — repeat calls are ignored, so a tool that wires this to
    /// both Loaded and Opened still sweeps once.
    /// </summary>
    public static void RunDeferredCleanup(int logRetentionSessions, int backupRetentionDays)
    {
        // Interlocked, not a plain bool: Loaded/Opened can both fire before the first
        // sweep finishes.
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return;

        _ = Task.Run(() =>
        {
            try
            {
                UnifiedLogger.CleanupOldSessions(logRetentionSessions);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Deferred log-session cleanup failed: {ex.Message}");
            }

            try
            {
                BackupCleanupService.CleanupExpiredBackups(backupRetentionDays);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Deferred backup cleanup failed: {ex.Message}");
            }
        });
    }

    /// <summary>Test hook: allow a fresh run within the same process.</summary>
    internal static void ResetForTests() => Interlocked.Exchange(ref _started, 0);
}

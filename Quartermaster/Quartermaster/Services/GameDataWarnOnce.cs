using System.Collections.Concurrent;
using Radoub.Formats.Logging;

namespace Quartermaster.Services;

/// <summary>
/// Emits a WARN log the first time a given fallback key fires per process,
/// silently no-ops on subsequent hits. Used so the cited hardcoded fallback
/// tables surface in logs when 2DA/TLK lookups fail without spamming the log
/// for repeated lookups of the same id. (#2251)
/// </summary>
public static class GameDataWarnOnce
{
    private static readonly ConcurrentDictionary<string, byte> _seen = new();

    /// <summary>
    /// Log <paramref name="message"/> the first time <paramref name="key"/> is
    /// observed in this process. Subsequent calls with the same key are silent.
    /// </summary>
    public static void Warn(string key, string message)
    {
        if (_seen.TryAdd(key, 0))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, message);
        }
    }

    /// <summary>
    /// Test seam — clears the set of already-seen keys.
    /// </summary>
    internal static void ResetForTests() => _seen.Clear();
}

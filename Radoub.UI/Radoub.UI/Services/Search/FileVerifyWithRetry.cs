using System;
using System.IO;
using System.Threading.Tasks;

namespace Radoub.UI.Services.Search;

/// <summary>
/// Retry helpers for file-existence verification after rename operations (#2181).
///
/// On Windows, <see cref="File.Move(string, string)"/> can return successfully
/// while the directory cache, antivirus scanner, or NTFS USN journal briefly
/// keeps the old path visible to subsequent <see cref="File.Exists(string)"/>
/// calls — especially when the rename targets a path that is a substring of
/// the source filename (e.g. `lompqj_qu1.nss` → `lompqj_qu.nss`). The orphan
/// "remains" only for a few milliseconds; a short retry window resolves the
/// race without masking real failures.
///
/// The helpers are pure (delegate-injected probe + delay) so the retry
/// semantics can be unit-tested without sleeping the test process.
/// </summary>
public static class FileVerifyWithRetry
{
    /// <summary>Default: 3 retries, 50 ms apart (~150 ms worst case).</summary>
    public const int DefaultMaxAttempts = 4;  // initial + 3 retries
    public const int DefaultDelayMs = 50;

    /// <summary>
    /// Polls until <paramref name="probe"/> returns the <paramref name="desired"/>
    /// value or <paramref name="maxAttempts"/> is exhausted. Returns true if the
    /// probe ever reported the desired value, false if it never did.
    /// </summary>
    public static async Task<bool> WaitForAsync(
        Func<bool> probe,
        bool desired,
        int maxAttempts = DefaultMaxAttempts,
        int delayMs = DefaultDelayMs,
        Func<int, Task>? sleep = null)
    {
        if (probe == null) throw new ArgumentNullException(nameof(probe));
        if (maxAttempts < 1) maxAttempts = 1;

        sleep ??= ms => Task.Delay(ms);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (probe() == desired) return true;
            if (attempt < maxAttempts - 1) await sleep(delayMs);
        }
        return false;
    }

    /// <summary>
    /// Wait until <paramref name="path"/> no longer exists on disk. Returns true
    /// if the path disappears within the retry window, false otherwise.
    /// </summary>
    public static Task<bool> WaitForGoneAsync(
        string path,
        int maxAttempts = DefaultMaxAttempts,
        int delayMs = DefaultDelayMs,
        Func<int, Task>? sleep = null)
        => WaitForAsync(() => !File.Exists(path), desired: true, maxAttempts, delayMs, sleep);

    /// <summary>
    /// Wait until <paramref name="path"/> exists on disk. Returns true if the
    /// path appears within the retry window, false otherwise.
    /// </summary>
    public static Task<bool> WaitForExistsAsync(
        string path,
        int maxAttempts = DefaultMaxAttempts,
        int delayMs = DefaultDelayMs,
        Func<int, Task>? sleep = null)
        => WaitForAsync(() => File.Exists(path), desired: true, maxAttempts, delayMs, sleep);
}

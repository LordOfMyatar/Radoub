using Radoub.Formats.Logging;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Retention/exclusion behavior for log-session cleanup (#2647).
///
/// Cleanup moved off the synchronous first-paint path, which makes it concurrent with
/// active logging rather than sequential-before-it. These tests pin the invariant that
/// protects the live session directory from a concurrent sweep.
/// </summary>
public class UnifiedLoggerCleanupTests : IDisposable
{
    private readonly string _tempRoot;

    public UnifiedLoggerCleanupTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RadoubLogCleanupTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    private string CreateSession(DateTime timestamp)
    {
        var dir = Path.Combine(_tempRoot, $"Session_{timestamp:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Application.log"), "log data");
        return dir;
    }

    [Fact]
    public void CleanupOldSessions_KeepsMostRecentSessions()
    {
        var now = DateTime.Now;
        var newest = CreateSession(now);
        var middle = CreateSession(now.AddHours(-1));
        var oldest = CreateSession(now.AddHours(-2));

        UnifiedLogger.CleanupOldSessions(2, _tempRoot);

        Assert.True(Directory.Exists(newest));
        Assert.True(Directory.Exists(middle));
        Assert.False(Directory.Exists(oldest));
    }

    [Fact]
    public void CleanupOldSessions_NeverDeletesProtectedSession_EvenWhenBeyondRetention()
    {
        // The live session must survive a sweep that would otherwise evict it by age.
        // Backgrounding cleanup (#2647) makes this reachable: a long-running app's session
        // ages past the retention window while newer sessions from other launches pile up.
        var now = DateTime.Now;
        var liveSession = CreateSession(now.AddHours(-5));   // oldest by timestamp
        CreateSession(now);
        CreateSession(now.AddHours(-1));
        CreateSession(now.AddHours(-2));

        UnifiedLogger.CleanupOldSessions(2, _tempRoot, protectedSessionDirectory: liveSession);

        Assert.True(Directory.Exists(liveSession));
    }

    [Fact]
    public void CleanupOldSessions_ProtectedSessionDoesNotConsumeRetentionSlot()
    {
        // Protecting the live session must not silently evict an extra recent session.
        var now = DateTime.Now;
        var liveSession = CreateSession(now.AddHours(-5));
        var newest = CreateSession(now);
        var second = CreateSession(now.AddHours(-1));

        UnifiedLogger.CleanupOldSessions(2, _tempRoot, protectedSessionDirectory: liveSession);

        Assert.True(Directory.Exists(liveSession));
        Assert.True(Directory.Exists(newest));
        Assert.True(Directory.Exists(second));
    }

    [Fact]
    public void CleanupOldSessions_HandlesNonExistentDirectory()
    {
        var missing = Path.Combine(_tempRoot, "does-not-exist");

        var ex = Record.Exception(() => UnifiedLogger.CleanupOldSessions(2, missing));

        Assert.Null(ex);
    }

    [Fact]
    public void CleanupOldSessions_IgnoresUnparseableDirectoryNames()
    {
        var now = DateTime.Now;
        var stray = Path.Combine(_tempRoot, "Session_not-a-timestamp");
        Directory.CreateDirectory(stray);
        var valid = CreateSession(now);

        UnifiedLogger.CleanupOldSessions(1, _tempRoot);

        Assert.True(Directory.Exists(valid));
        Assert.True(Directory.Exists(stray));   // untouched, not crashed on
    }
}

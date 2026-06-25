using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

public class GameDataWarnOnceTests
{
    public GameDataWarnOnceTests()
    {
        GameDataWarnOnce.ResetForTests();
    }

    [Fact]
    public void Warn_FirstCallRecordsKey_SecondCallStaysRecorded()
    {
        Assert.False(GameDataWarnOnce.HasSeen("skill_42"));

        GameDataWarnOnce.Warn("skill_42", "First");
        Assert.True(GameDataWarnOnce.HasSeen("skill_42"));

        // Second call with the same key is a silent no-op; the key remains recorded.
        GameDataWarnOnce.Warn("skill_42", "Second");
        Assert.True(GameDataWarnOnce.HasSeen("skill_42"));
    }

    [Fact]
    public void Warn_DistinctKeysAreTrackedIndependently()
    {
        GameDataWarnOnce.Warn("a", "msg-a");
        GameDataWarnOnce.Warn("b", "msg-b");

        Assert.True(GameDataWarnOnce.HasSeen("a"));
        Assert.True(GameDataWarnOnce.HasSeen("b"));
        Assert.False(GameDataWarnOnce.HasSeen("c"));
    }

    [Fact]
    public void ResetForTests_ClearsSeenSet()
    {
        GameDataWarnOnce.Warn("reset_key", "first");
        Assert.True(GameDataWarnOnce.HasSeen("reset_key"));

        GameDataWarnOnce.ResetForTests();
        Assert.False(GameDataWarnOnce.HasSeen("reset_key"));
    }
}

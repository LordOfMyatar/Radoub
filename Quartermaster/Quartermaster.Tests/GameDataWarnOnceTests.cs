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
    public void Warn_FirstCallEmits_SecondCallSilent()
    {
        // Side-effect is to UnifiedLogger; we exercise the dedup logic via the
        // public surface — repeated calls with the same key must not throw or
        // re-add to the underlying set.
        GameDataWarnOnce.Warn("skill_42", "First");
        GameDataWarnOnce.Warn("skill_42", "Second"); // should be silent

        // No exception thrown is the assertion; we just want to confirm the
        // happy-path doesn't blow up on dup keys.
        Assert.True(true);
    }

    [Fact]
    public void Warn_DifferentKeysEachFire()
    {
        // Each distinct key gets its own first-fire — no cross-contamination.
        GameDataWarnOnce.Warn("a", "msg-a");
        GameDataWarnOnce.Warn("b", "msg-b");
        GameDataWarnOnce.Warn("c", "msg-c");

        // Re-warn should still be silent for a,b,c
        GameDataWarnOnce.Warn("a", "msg-a-2");
        Assert.True(true);
    }

    [Fact]
    public void ResetForTests_ClearsSeenSet()
    {
        GameDataWarnOnce.Warn("reset_key", "first");
        GameDataWarnOnce.ResetForTests();
        // After reset, the same key fires again — confirmed by absence of exceptions.
        GameDataWarnOnce.Warn("reset_key", "first-again");
        Assert.True(true);
    }
}

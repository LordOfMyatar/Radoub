using System.Reflection;
using System.Threading.Tasks;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Guards #2262 finding 3: TokenInsertionHelper.OpenTokenWindow was
/// `public static async void`. async void swallows exceptions thrown
/// after the first await into the SynchronizationContext, and host tools
/// cannot await completion. Fix: rename to OpenTokenWindowAsync and
/// return Task. Callers that need the old fire-and-forget signature
/// discard the Task at the call site.
/// </summary>
public class TokenInsertionHelperAsyncSignatureTests
{
    [Fact]
    public void OpenTokenWindowAsync_ReturnsTask()
    {
        var method = typeof(TokenInsertionHelper).GetMethod(
            "OpenTokenWindowAsync",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
    }

    [Fact]
    public void OldAsyncVoidOpenTokenWindow_IsRemoved()
    {
        // Surface area cleanup — no async void OpenTokenWindow should remain.
        var method = typeof(TokenInsertionHelper).GetMethod(
            "OpenTokenWindow",
            BindingFlags.Public | BindingFlags.Static);

        // Either absent, or migrated to Task return type.
        if (method != null)
        {
            Assert.Equal(typeof(Task), method.ReturnType);
        }
    }
}

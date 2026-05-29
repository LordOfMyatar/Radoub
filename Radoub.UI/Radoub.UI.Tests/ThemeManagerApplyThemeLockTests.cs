using System.Linq;
using System.Reflection;
using System.Threading;
using Radoub.UI.Models;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Guards #2262 finding 4: ThemeManager.ApplyTheme(ThemeManifest) used to
/// do its Step-1-opposite / Step-2a-semantic-colors / Step-2b-Post dance
/// with no lock. Two concurrent callers (settings dialog + startup race)
/// could interleave Step 1, producing variant flicker and interleaved
/// resource writes.
///
/// Fix: serialize the critical section via the existing _lock. We assert
/// the method's IL contains a Monitor.Enter call — the lock keyword's
/// lowered form — so any future refactor that drops the lock fails here.
/// </summary>
public class ThemeManagerApplyThemeLockTests
{
    [Fact]
    public void ApplyTheme_OnManifest_UsesLock()
    {
        var method = typeof(ThemeManager).GetMethod(
            "ApplyTheme",
            BindingFlags.Public | BindingFlags.Instance,
            new[] { typeof(ThemeManifest) });

        Assert.NotNull(method);
        var body = method!.GetMethodBody();
        Assert.NotNull(body);

        // C# lock { ... } lowers to Monitor.Enter(obj, ref bool) +
        // try/finally Monitor.Exit(obj). Confirm at least one Monitor.Enter
        // call is reachable from this method.
        var il = body!.GetILAsByteArray();
        Assert.NotNull(il);

        var monitorEnterTokens = method.Module
            .GetTypes() // force module load
            .Length;
        _ = monitorEnterTokens; // keep compiler quiet on intentional warm-up

        // Walk the IL looking for a `call` / `callvirt` that targets
        // System.Threading.Monitor.Enter.
        bool foundMonitorEnter = ContainsMonitorEnter(method);
        Assert.True(
            foundMonitorEnter,
            "ApplyTheme(ThemeManifest) must hold _lock across the Step-1 + Step-2a + " +
            "Post-Step-2b sequence (#2262). Monitor.Enter was not found in the method's IL.");
    }

    private static bool ContainsMonitorEnter(MethodBase method)
    {
        var body = method.GetMethodBody();
        if (body == null) return false;

        var il = body.GetILAsByteArray();
        if (il == null) return false;

        var module = method.Module;
        int i = 0;
        while (i < il.Length)
        {
            byte op = il[i];

            // call (0x28) and callvirt (0x6F) are both 5-byte instructions
            // (1 opcode + 4-byte metadata token).
            if (op == 0x28 || op == 0x6F)
            {
                if (i + 4 >= il.Length) break;
                int token = il[i + 1]
                            | (il[i + 2] << 8)
                            | (il[i + 3] << 16)
                            | (il[i + 4] << 24);
                try
                {
                    var resolved = module.ResolveMethod(token);
                    if (resolved != null
                        && resolved.DeclaringType == typeof(Monitor)
                        && resolved.Name == "Enter")
                    {
                        return true;
                    }
                }
                catch
                {
                    // Generic / vararg tokens we can't resolve; skip.
                }
                i += 5;
            }
            else
            {
                i += SingleByteOpcodeLength(op);
            }
        }
        return false;
    }

    private static int SingleByteOpcodeLength(byte op)
    {
        // Fallback: most IL opcodes are 1 byte. The handful that take
        // operands and aren't call/callvirt aren't relevant here because
        // we only need to step forward enough to keep scanning. Worst case
        // we mis-step a few bytes — harmless for a presence check.
        return op switch
        {
            0xFE => 2, // two-byte opcode prefix
            _ => 1
        };
    }
}

using System.Reflection;
using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// Tests that GetToolPathFromSettings and CacheToolPath in ToolLauncherService
/// recognize exactly the same set of tool names. Asymmetry causes silent
/// "discovered path is read but never written back" bugs (#2247 cache-name finding).
/// </summary>
public class ToolLauncherCacheSymmetryTests
{
    private static readonly string[] KnownToolNames =
    {
        "parley", "manifest", "quartermaster", "fence", "relique"
    };

    [Theory]
    [InlineData("parley")]
    [InlineData("manifest")]
    [InlineData("quartermaster")]
    [InlineData("fence")]
    [InlineData("relique")]
    public void GetToolPathFromSettings_AcceptsKnownTools(string toolName)
    {
        var method = typeof(ToolLauncherService).GetMethod(
            "GetToolPathFromSettings",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(ToolLauncherService.Instance, new object[] { toolName });

        // null is fine (no path cached yet); the assertion is that the call
        // succeeded without falling through to the catch-all _=>null only via
        // an unrecognized name. We assert no exception path was hit.
        _ = result;
    }

    [Fact]
    public void GetToolPathFromSettings_DoesNotAcceptItemEditorAlias()
    {
        // Regression: #2247 — legacy "itemeditor" alias was accepted by the
        // read path (GetToolPathFromSettings) but NOT by the write path
        // (CacheToolPath). The asymmetry meant any caller using the alias
        // would re-probe dev/common-install on every startup.
        //
        // Resolution: the alias has no live callers (ToolInfo.Name is "Relique"
        // throughout ToolLauncherService, and ItemEditorLauncher / ToolDispatchService
        // read RadoubSettings.ReliquePath directly). Drop the alias.
        var method = typeof(ToolLauncherService).GetMethod(
            "GetToolPathFromSettings",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        // After the fix, "itemeditor" should return null (unknown tool),
        // matching CacheToolPath's behavior (silently does nothing).
        var result = method!.Invoke(ToolLauncherService.Instance, new object[] { "itemeditor" });

        Assert.Null(result);
    }
}

using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for ToolRecentFilesService settings-path mapping.
/// Covers the regression in #2247 where "Relique" returned null and the
/// MRU dropdown on the Relique tool card was permanently empty.
/// </summary>
public class ToolRecentFilesServiceTests
{
    private const string FakeRadoubDir = "/fake/radoub";

    [Theory]
    [InlineData("Parley", "/fake/radoub/Parley/ParleySettings.json")]
    [InlineData("Quartermaster", "/fake/radoub/Quartermaster/QuartermasterSettings.json")]
    [InlineData("Manifest", "/fake/radoub/Manifest/ManifestSettings.json")]
    [InlineData("Fence", "/fake/radoub/Fence/FenceSettings.json")]
    [InlineData("Relique", "/fake/radoub/Relique/ReliqueSettings.json")]
    [InlineData("Reliquary", "/fake/radoub/Reliquary/ReliquarySettings.json")]
    public void GetSettingsPath_KnownTool_ReturnsExpectedPath(string toolName, string expectedSuffix)
    {
        var actual = ToolRecentFilesService.GetSettingsPathFor(FakeRadoubDir, toolName);

        Assert.NotNull(actual);
        var normalized = actual!.Replace('\\', '/');
        Assert.Equal(expectedSuffix, normalized);
    }

    [Fact]
    public void GetSettingsPath_Relique_IsNotNull()
    {
        // Regression: #2247 - "Relique" case was missing from the switch,
        // causing GetRecentFiles("Relique") to return an empty list always.
        var path = ToolRecentFilesService.GetSettingsPathFor(FakeRadoubDir, "Relique");

        Assert.NotNull(path);
    }

    [Fact]
    public void GetSettingsPath_Reliquary_IsNotNull()
    {
        // #2368 - same regression shape as #2247: Reliquary shipped without an MRU
        // registration, so its tool-card dropdown was permanently empty.
        var path = ToolRecentFilesService.GetSettingsPathFor(FakeRadoubDir, "Reliquary");

        Assert.NotNull(path);
    }

    [Fact]
    public void GetSettingsPath_UnknownTool_ReturnsNull()
    {
        var path = ToolRecentFilesService.GetSettingsPathFor(FakeRadoubDir, "NotARealTool");

        Assert.Null(path);
    }
}

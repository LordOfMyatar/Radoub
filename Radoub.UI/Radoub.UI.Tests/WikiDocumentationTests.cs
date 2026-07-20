using Radoub.UI.Utils;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// #2061 / #1567 follow-up: every tool's Help menu points at its own wiki page.
/// </summary>
public class WikiDocumentationTests
{
    [Theory]
    [InlineData("Manifest", "https://github.com/LordOfMyatar/Radoub/wiki/Manifest")]
    [InlineData("Quartermaster", "https://github.com/LordOfMyatar/Radoub/wiki/Quartermaster")]
    [InlineData("Fence", "https://github.com/LordOfMyatar/Radoub/wiki/Fence")]
    [InlineData("Relique", "https://github.com/LordOfMyatar/Radoub/wiki/Relique")]
    [InlineData("Reliquary", "https://github.com/LordOfMyatar/Radoub/wiki/Reliquary")]
    [InlineData("Trebuchet", "https://github.com/LordOfMyatar/Radoub/wiki/Trebuchet")]
    [InlineData("Marlinspike", "https://github.com/LordOfMyatar/Radoub/wiki/Marlinspike")]
    public void GetToolWikiUrl_KnownTool_ReturnsItsLandingPage(string tool, string expected)
    {
        Assert.Equal(expected, WikiHelper.GetToolWikiUrl(tool));
    }

    [Fact]
    public void GetToolWikiUrl_Parley_ReturnsGettingStarted()
    {
        // Parley has no "Parley" landing page; its entry point is Parley-Getting-Started
        // (this is how Home.md links it).
        Assert.Equal(
            "https://github.com/LordOfMyatar/Radoub/wiki/Parley-Getting-Started",
            WikiHelper.GetToolWikiUrl("Parley"));
    }

    [Fact]
    public void GetToolWikiUrl_UnknownTool_FallsBackToWikiRoot()
    {
        Assert.Equal(
            "https://github.com/LordOfMyatar/Radoub/wiki",
            WikiHelper.GetToolWikiUrl("NotARealTool"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GetToolWikiUrl_MissingToolName_FallsBackToWikiRoot(string? tool)
    {
        Assert.Equal("https://github.com/LordOfMyatar/Radoub/wiki", WikiHelper.GetToolWikiUrl(tool));
    }

    [Fact]
    public void GetToolWikiUrl_IsCaseInsensitive()
    {
        Assert.Equal(
            "https://github.com/LordOfMyatar/Radoub/wiki/Fence",
            WikiHelper.GetToolWikiUrl("fence"));
    }
}

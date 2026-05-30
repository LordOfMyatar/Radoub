using RadoubLauncher.Services;
using Xunit;

namespace Trebuchet.Tests;

/// <summary>
/// Unit tests for UpdateService.IsNewerVersion — pure version comparison.
/// Covers #2248: a non-numeric version segment (e.g. "0.1.0+abc123") must not throw
/// and silently suppress updates; TryParse treats unparseable segments as 0.
/// </summary>
public class UpdateServiceVersionTests
{
    [Theory]
    [InlineData("1.0.0", "0.9.0", true)]
    [InlineData("0.2.0", "0.1.0", true)]
    [InlineData("0.1.1", "0.1.0", true)]
    [InlineData("0.1.0", "0.1.0", false)]
    [InlineData("0.1.0", "0.2.0", false)]
    [InlineData("0.9.0", "1.0.0", false)]
    public void IsNewerVersion_NumericComparison(string latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsNewerVersion(latest, current));
    }

    [Fact]
    public void IsNewerVersion_ReleaseNewerThanPrerelease_SameBase()
    {
        Assert.True(UpdateService.IsNewerVersion("0.1.0", "0.1.0-alpha"));
    }

    [Fact]
    public void IsNewerVersion_PrereleaseNotNewerThanRelease_SameBase()
    {
        Assert.False(UpdateService.IsNewerVersion("0.1.0-alpha", "0.1.0"));
    }

    [Fact]
    public void IsNewerVersion_GitHashSuffix_DoesNotSuppressRealUpdate()
    {
        // "0.2.0+abc123" segment "0+abc123"... here the higher minor must still win
        // instead of throwing FormatException and returning false.
        Assert.True(UpdateService.IsNewerVersion("0.2.0+abc123", "0.1.0"));
    }

    [Fact]
    public void IsNewerVersion_NonNumericSegment_TreatedAsZero_NotThrow()
    {
        // Equal numeric prefix, junk suffix → not newer, but must NOT throw.
        Assert.False(UpdateService.IsNewerVersion("0.1.0+abc123", "0.1.0"));
    }
}

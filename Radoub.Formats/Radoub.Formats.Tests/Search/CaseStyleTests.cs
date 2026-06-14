using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search;

/// <summary>
/// Tests for CaseStyle — detecting a matched string's case style and reapplying
/// it to a replacement, for case-preserving content replace (#2180).
/// </summary>
public class CaseStyleTests
{
    [Theory]
    [InlineData("louis", CaseKind.AllLower)]
    [InlineData("LOUIS", CaseKind.AllUpper)]
    [InlineData("Louis", CaseKind.TitleCase)]
    [InlineData("LoUiS", CaseKind.Mixed)]
    [InlineData("McLeod", CaseKind.Mixed)]
    [InlineData("", CaseKind.Mixed)]
    [InlineData("123", CaseKind.Mixed)]   // no letters → Mixed (verbatim)
    public void Detect_ClassifiesCorrectly(string input, CaseKind expected)
        => Assert.Equal(expected, CaseStyle.Detect(input));

    [Theory]
    [InlineData(CaseKind.AllLower, "lewie", "lewie")]
    [InlineData(CaseKind.AllUpper, "lewie", "LEWIE")]
    [InlineData(CaseKind.TitleCase, "lewie", "Lewie")]
    [InlineData(CaseKind.Mixed, "lewie", "lewie")]   // fallback = verbatim
    public void Apply_ReappliesStyle(CaseKind kind, string replacement, string expected)
        => Assert.Equal(expected, CaseStyle.Apply(kind, replacement));

    [Fact]
    public void DetectThenApply_LouisExample()
    {
        Assert.Equal("lewie", CaseStyle.Apply(CaseStyle.Detect("louis"), "lewie"));
        Assert.Equal("Lewie", CaseStyle.Apply(CaseStyle.Detect("Louis"), "lewie"));
        Assert.Equal("LEWIE", CaseStyle.Apply(CaseStyle.Detect("LOUIS"), "lewie"));
    }

    [Fact]
    public void Apply_EmptyReplacement_ReturnsEmpty()
        => Assert.Equal("", CaseStyle.Apply(CaseKind.AllUpper, ""));
}

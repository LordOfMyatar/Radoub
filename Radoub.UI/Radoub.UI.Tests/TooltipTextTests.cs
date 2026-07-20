using Radoub.UI.Utils;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// #1567: tooltips are for short hints. Long game-data text (TLK feat/spell
/// descriptions) must be summarized rather than dumped into a tooltip.
/// </summary>
public class TooltipTextTests
{
    [Fact]
    public void Summarize_ShortText_ReturnedUnchanged()
    {
        Assert.Equal("Feat is chosen for creature",
            TooltipText.Summarize("Feat is chosen for creature"));
    }

    [Fact]
    public void Summarize_LongText_IsTruncatedWithEllipsis()
    {
        var longText = new string('a', 500);

        var result = TooltipText.Summarize(longText);

        Assert.True(result.Length < longText.Length);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void Summarize_LongText_StaysWithinLimit()
    {
        var longText = new string('a', 500);

        Assert.True(TooltipText.Summarize(longText).Length <= TooltipText.MaxLength);
    }

    [Fact]
    public void Summarize_BreaksOnWordBoundary_NotMidWord()
    {
        // 40 words of 9 chars each — comfortably over the limit.
        var longText = string.Join(" ", System.Linq.Enumerable.Repeat("wordsmith", 40));

        var result = TooltipText.Summarize(longText).TrimEnd('…').TrimEnd();

        // Every retained token must be a whole word, never a fragment.
        Assert.All(result.Split(' '), w => Assert.Equal("wordsmith", w));
    }

    [Fact]
    public void Summarize_CollapsesInternalNewlines()
    {
        // TLK entries carry hard line breaks that render badly in a tooltip.
        var result = TooltipText.Summarize("First line.\nSecond line.\r\nThird line.");

        Assert.DoesNotContain("\n", result);
        Assert.DoesNotContain("\r", result);
        Assert.Contains("First line. Second line. Third line.", result);
    }

    [Fact]
    public void Summarize_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal("", TooltipText.Summarize(null));
        Assert.Equal("", TooltipText.Summarize(""));
        Assert.Equal("", TooltipText.Summarize("   "));
    }

    [Fact]
    public void Summarize_TrimsSurroundingWhitespace()
    {
        Assert.Equal("Power Attack", TooltipText.Summarize("   Power Attack   "));
    }
}

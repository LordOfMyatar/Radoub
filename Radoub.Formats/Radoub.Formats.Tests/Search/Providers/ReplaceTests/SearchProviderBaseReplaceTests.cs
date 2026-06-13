using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers.ReplaceTests;

/// <summary>
/// Tests for the shared replace helper methods in SearchProviderBase.
/// Uses a test subclass to expose protected methods.
/// </summary>
public class SearchProviderBaseReplaceTests
{
    /// <summary>
    /// Test subclass to expose protected replace helpers.
    /// </summary>
    private class TestableProvider : SearchProviderBase
    {
        public static string CallReplaceInString(string value, ReplaceOperation op)
            => ReplaceInString(value, op);

        public static (string newValue, string? warning) CallReplaceResRef(string value, ReplaceOperation op)
            => ReplaceResRef(value, op);

        public static string CallReplaceInLocStringVariant(string value, ReplaceOperation op)
            => ReplaceInString(value, op);
    }

    // --- Plain text replacement ---

    [Fact]
    public void ReplaceInString_SimpleReplace()
    {
        var op = MakeOp("Louis", 0, 5, "Marcel");

        var result = TestableProvider.CallReplaceInString("Louis Romain", op);

        Assert.Equal("Marcel Romain", result);
    }

    [Fact]
    public void ReplaceInString_MiddleOfString()
    {
        var op = MakeOp("old", 10, 3, "new");

        var result = TestableProvider.CallReplaceInString("prefix -- old -- suffix", op);

        Assert.Equal("prefix -- new -- suffix", result);
    }

    [Fact]
    public void ReplaceInString_RegexCaptureGroup()
    {
        var op = MakeOp("Louis", 0, 5, "Sir $0", isRegex: true);

        var result = TestableProvider.CallReplaceInString("Louis Romain", op);

        Assert.Equal("Sir Louis Romain", result);
    }

    // --- ResRef replacement (16-char limit) ---

    [Fact]
    public void ReplaceResRef_ValidLength()
    {
        var op = MakeOp("old_script", 0, 10, "new_script");

        var (result, warning) = TestableProvider.CallReplaceResRef("old_script", op);

        Assert.Equal("new_script", result);
        Assert.Null(warning);
    }

    [Fact]
    public void ReplaceResRef_TruncatesAt16Chars_WithWarning()
    {
        var op = MakeOp("short", 0, 5, "this_is_way_too_long_for_resref");

        var (result, warning) = TestableProvider.CallReplaceResRef("short", op);

        Assert.Equal(16, result.Length);
        Assert.Equal("this_is_way_too_", result);
        Assert.NotNull(warning);
        Assert.Contains("truncated", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReplaceResRef_Exactly16Chars_NoWarning()
    {
        var op = MakeOp("old", 0, 3, "exactly_16_chars");

        var (result, warning) = TestableProvider.CallReplaceResRef("old", op);

        Assert.Equal("exactly_16_chars", result);
        Assert.Null(warning);
    }

    // --- Case preservation (#2180) ---

    [Theory]
    [InlineData("louis", "lewie")]
    [InlineData("Louis", "Lewie")]
    [InlineData("LOUIS", "LEWIE")]
    public void ReplaceInString_PreserveCaseOn_RestylesReplacement(string matched, string expected)
    {
        var op = MakeOp(matched, 0, matched.Length, "lewie", preserveCase: true);

        var result = TestableProvider.CallReplaceInString(matched, op);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceInString_PreserveCaseOff_InsertsVerbatim()
    {
        var op = MakeOp("Louis", 0, 5, "lewie", preserveCase: false);

        var result = TestableProvider.CallReplaceInString("Louis", op);

        Assert.Equal("lewie", result);
    }

    [Fact]
    public void ReplaceInString_ResRefField_IgnoresPreserveCase()
    {
        // ResRef-typed field must stay lowercase even with PreserveCase on.
        var op = MakeOp("LOUIS", 0, 5, "lewie", preserveCase: true, fieldType: SearchFieldType.ResRef);

        var result = TestableProvider.CallReplaceInString("LOUIS", op);

        Assert.Equal("lewie", result);
    }

    [Fact]
    public void ReplaceInString_PreserveCaseOn_PreservesRemainder()
    {
        // Match "Louis" inside "Louisromain" → "Lewieromain" (remainder verbatim).
        var op = MakeOp("Louis", 0, 5, "lewie", preserveCase: true);

        var result = TestableProvider.CallReplaceInString("Louisromain", op);

        Assert.Equal("Lewieromain", result);
    }

    [Fact]
    public void ReplaceInString_MatchesComputedValueTransform()
    {
        // Cross-path parity: ReplaceInString output == the CaseStyle transform
        // PendingChange.ComputedNewFieldValue applies (#2180). Both route through
        // CaseStyle so the preview never lies about the write.
        const string matched = "Louis";
        const string repl = "lewie";
        var op = MakeOp(matched, 0, matched.Length, repl, preserveCase: true);

        var write = TestableProvider.CallReplaceInString(matched, op);
        var previewSpan = CaseStyle.Apply(CaseStyle.Detect(matched), repl);

        Assert.Equal("Lewie", write);
        Assert.Equal(previewSpan, write);
    }

    // --- Helpers ---

    private static ReplaceOperation MakeOp(
        string matchedText, int offset, int length, string replacement,
        bool isRegex = false, bool preserveCase = false,
        SearchFieldType fieldType = SearchFieldType.Text)
    {
        return new ReplaceOperation
        {
            Match = new SearchMatch
            {
                Field = new FieldDefinition
                {
                    Name = "Test", GffPath = "Test",
                    FieldType = fieldType,
                    Category = SearchFieldCategory.Content
                },
                MatchedText = matchedText,
                FullFieldValue = "placeholder",
                MatchOffset = offset,
                MatchLength = length
            },
            ReplacementText = replacement,
            IsRegex = isRegex,
            PreserveCase = preserveCase
        };
    }
}

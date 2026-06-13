using Radoub.Formats.Search;
using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

/// <summary>
/// #2224: ReplacePreviewWindow showed the bare replacement term instead of the
/// substring-substituted field value. PendingChange.ComputedNewFieldValue produces
/// the same result the write path produces (literal substitution at the match
/// offset) so the preview matches reality. No lowercasing — the write path
/// (SearchProviderBase.ReplaceInString) does not lowercase.
/// </summary>
public class PendingChangeComputedValueTests
{
    private static FieldDefinition Field(SearchFieldType type = SearchFieldType.Text) =>
        new()
        {
            Name = "FirstName",
            GffPath = "FirstName",
            FieldType = type,
            Category = SearchFieldCategory.Content
        };

    private static PendingChange Change(string fullValue, int offset, int length, string replacement,
        SearchFieldType type = SearchFieldType.Text, bool preserveCase = false) =>
        new()
        {
            Match = new SearchMatch
            {
                Field = Field(type),
                MatchedText = fullValue.Substring(offset, length),
                FullFieldValue = fullValue,
                MatchOffset = offset,
                MatchLength = length
            },
            ReplacementText = replacement,
            FilePath = "x.utc",
            PreserveCase = preserveCase
        };

    [Fact]
    public void Substitutes_match_within_field_not_whole_field()
    {
        // "Louis Romain", match "Louis" (0..5) -> "lewie"
        var change = Change("Louis Romain", 0, 5, "lewie");
        Assert.Equal("lewie Romain", change.ComputedNewFieldValue);
    }

    [Fact]
    public void Substitutes_match_in_middle_of_field()
    {
        // "There is also Louis. The locals..." match "Louis" at 14
        var full = "There is also Louis. The locals tell me...";
        var change = Change(full, 14, 5, "lewie");
        Assert.Equal("There is also lewie. The locals tell me...", change.ComputedNewFieldValue);
    }

    [Fact]
    public void Substitutes_match_with_trailing_text_no_space()
    {
        // "LouisROMAIN" match "Louis" -> "lewieROMAIN"
        var change = Change("LouisROMAIN", 0, 5, "lewie");
        Assert.Equal("lewieROMAIN", change.ComputedNewFieldValue);
    }

    [Fact]
    public void Whole_field_match_replaces_entire_value()
    {
        var change = Change("Louis", 0, 5, "lewie");
        Assert.Equal("lewie", change.ComputedNewFieldValue);
    }

    [Fact]
    public void Preserves_case_does_not_lowercase()
    {
        // Write path does not lowercase; preview must match reality.
        var change = Change("Louis Romain", 0, 5, "Lewie");
        Assert.Equal("Lewie Romain", change.ComputedNewFieldValue);
    }

    [Fact]
    public void Empty_replacement_deletes_the_match()
    {
        var change = Change("Louis Romain", 0, 6, "");
        Assert.Equal("Romain", change.ComputedNewFieldValue);
    }

    // --- #2180: case-preservation in the preview ---

    [Theory]
    [InlineData("louis", "lewie")]
    [InlineData("Louis", "Lewie")]
    [InlineData("LOUIS", "LEWIE")]
    public void PreserveCaseOn_RestylesMatchedSpan(string matched, string expected)
    {
        // Whole-field match, replacement "lewie", preserve case on.
        var change = Change(matched, 0, matched.Length, "lewie", preserveCase: true);
        Assert.Equal(expected, change.ComputedNewFieldValue);
    }

    [Fact]
    public void PreserveCaseOn_PreservesRemainder()
    {
        // "Louisromain", match "Louis" -> "Lewieromain" (remainder verbatim).
        var change = Change("Louisromain", 0, 5, "lewie", preserveCase: true);
        Assert.Equal("Lewieromain", change.ComputedNewFieldValue);
    }

    [Fact]
    public void PreserveCaseOn_ResRefField_StaysLowercase()
    {
        // ResRef-typed field ignores the flag.
        var change = Change("LOUIS", 0, 5, "lewie", type: SearchFieldType.ResRef, preserveCase: true);
        Assert.Equal("lewie", change.ComputedNewFieldValue);
    }

    [Fact]
    public void PreserveCaseOff_InsertsVerbatim()
    {
        var change = Change("Louis", 0, 5, "lewie", preserveCase: false);
        Assert.Equal("lewie", change.ComputedNewFieldValue);
    }
}

using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Models;

public class SearchCriteriaTests
{
    private static FieldDefinition MakeField(
        string name = "TestField",
        SearchFieldType type = SearchFieldType.Text,
        SearchFieldCategory category = SearchFieldCategory.Content)
        => new() { Name = name, GffPath = name, FieldType = type, Category = category };

    // --- ToRegex tests ---

    [Fact]
    public void ToRegex_PlainText_EscapesSpecialChars()
    {
        var criteria = new SearchCriteria { Pattern = "hello.world" };
        var regex = criteria.ToRegex();
        Assert.Matches(regex, "hello.world");
        Assert.DoesNotMatch(regex, "helloXworld");
    }

    [Fact]
    public void ToRegex_Regex_UsesPatternDirectly()
    {
        var criteria = new SearchCriteria { Pattern = "hello.world", IsRegex = true };
        var regex = criteria.ToRegex();
        Assert.Matches(regex, "helloXworld");
    }

    [Fact]
    public void ToRegex_CaseInsensitive_MatchesBothCases()
    {
        var criteria = new SearchCriteria { Pattern = "hello", CaseSensitive = false };
        var regex = criteria.ToRegex();
        Assert.Matches(regex, "HELLO");
        Assert.Matches(regex, "hello");
    }

    [Fact]
    public void ToRegex_CaseSensitive_MatchesExactCase()
    {
        var criteria = new SearchCriteria { Pattern = "hello", CaseSensitive = true };
        var regex = criteria.ToRegex();
        Assert.Matches(regex, "hello");
        Assert.DoesNotMatch(regex, "HELLO");
    }

    [Fact]
    public void ToRegex_WholeWord_MatchesWordBoundaries()
    {
        var criteria = new SearchCriteria { Pattern = "cat", WholeWord = true };
        var regex = criteria.ToRegex();
        Assert.Matches(regex, "the cat sat");
        Assert.DoesNotMatch(regex, "concatenate");
    }

    [Fact]
    public void ToRegex_WholeWordAndCaseInsensitive_Combined()
    {
        var criteria = new SearchCriteria { Pattern = "Cat", WholeWord = true, CaseSensitive = false };
        var regex = criteria.ToRegex();
        Assert.Matches(regex, "the CAT sat");
        Assert.DoesNotMatch(regex, "concatenate");
    }

    // --- Validate tests ---

    [Fact]
    public void Validate_EmptyPattern_ReturnsError()
    {
        var criteria = new SearchCriteria { Pattern = "" };
        Assert.NotNull(criteria.Validate());
    }

    [Fact]
    public void Validate_ValidPlainText_ReturnsNull()
    {
        var criteria = new SearchCriteria { Pattern = "hello" };
        Assert.Null(criteria.Validate());
    }

    [Fact]
    public void Validate_ValidRegex_ReturnsNull()
    {
        var criteria = new SearchCriteria { Pattern = @"hello\s+world", IsRegex = true };
        Assert.Null(criteria.Validate());
    }

    [Fact]
    public void Validate_InvalidRegex_ReturnsError()
    {
        var criteria = new SearchCriteria { Pattern = "[invalid", IsRegex = true };
        Assert.NotNull(criteria.Validate());
    }

    // --- MatchesField tests ---

    [Fact]
    public void MatchesField_NoFilters_MatchesAll()
    {
        var criteria = new SearchCriteria { Pattern = "test" };
        Assert.True(criteria.MatchesField(MakeField()));
    }

    [Fact]
    public void MatchesField_FieldNameFilter_MatchesOnly()
    {
        var criteria = new SearchCriteria
        {
            Pattern = "test",
            FieldFilter = new[] { "Speaker" }
        };
        Assert.True(criteria.MatchesField(MakeField("Speaker")));
        Assert.False(criteria.MatchesField(MakeField("Comment")));
    }

    [Fact]
    public void MatchesField_FieldTypeFilter_MatchesOnly()
    {
        var criteria = new SearchCriteria
        {
            Pattern = "test",
            FieldTypeFilter = new[] { SearchFieldType.Script }
        };
        Assert.True(criteria.MatchesField(MakeField(type: SearchFieldType.Script)));
        Assert.False(criteria.MatchesField(MakeField(type: SearchFieldType.Text)));
    }

    [Fact]
    public void MatchesField_CategoryFilter_MatchesOnly()
    {
        var criteria = new SearchCriteria
        {
            Pattern = "test",
            CategoryFilter = new[] { SearchFieldCategory.Script }
        };
        Assert.True(criteria.MatchesField(MakeField(category: SearchFieldCategory.Script)));
        Assert.False(criteria.MatchesField(MakeField(category: SearchFieldCategory.Content)));
    }

    [Fact]
    public void MatchesField_MultipleFilters_AllMustPass()
    {
        var criteria = new SearchCriteria
        {
            Pattern = "test",
            FieldTypeFilter = new[] { SearchFieldType.Script },
            CategoryFilter = new[] { SearchFieldCategory.Script }
        };
        Assert.True(criteria.MatchesField(MakeField(type: SearchFieldType.Script, category: SearchFieldCategory.Script)));
        Assert.False(criteria.MatchesField(MakeField(type: SearchFieldType.Script, category: SearchFieldCategory.Content)));
    }
}

using Radoub.Formats.Search;
using Radoub.UI.Models;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Coverage for the SearchBar criteria factory (#2360). The GFF find/replace
/// criteria build was previously untested; a wrong flag or a broken category-tag
/// parse silently changes what the search matches.
/// </summary>
public class SearchBarCriteriaFactoryTests
{
    [Fact]
    public void Build_CopiesPattern()
    {
        var c = SearchBarCriteriaFactory.Build("dragon", isRegex: false, caseSensitive: false, categoryTag: null);
        Assert.Equal("dragon", c.Pattern);
    }

    [Fact]
    public void Build_PassesRegexFlag()
    {
        Assert.True(SearchBarCriteriaFactory.Build("p", isRegex: true, caseSensitive: false, categoryTag: null).IsRegex);
        Assert.False(SearchBarCriteriaFactory.Build("p", isRegex: false, caseSensitive: false, categoryTag: null).IsRegex);
    }

    [Fact]
    public void Build_PassesCaseSensitiveFlag()
    {
        Assert.True(SearchBarCriteriaFactory.Build("p", isRegex: false, caseSensitive: true, categoryTag: null).CaseSensitive);
        Assert.False(SearchBarCriteriaFactory.Build("p", isRegex: false, caseSensitive: false, categoryTag: null).CaseSensitive);
    }

    [Theory]
    [InlineData("Content", SearchFieldCategory.Content)]
    [InlineData("Identity", SearchFieldCategory.Identity)]
    [InlineData("Script", SearchFieldCategory.Script)]
    [InlineData("Metadata", SearchFieldCategory.Metadata)]
    [InlineData("Variable", SearchFieldCategory.Variable)]
    public void Build_ParsesValidCategoryTag_ToSingleCategoryFilter(string tag, SearchFieldCategory expected)
    {
        var c = SearchBarCriteriaFactory.Build("p", isRegex: false, caseSensitive: false, categoryTag: tag);

        Assert.NotNull(c.CategoryFilter);
        Assert.Single(c.CategoryFilter!);
        Assert.Equal(expected, c.CategoryFilter![0]);
    }

    [Fact]
    public void Build_NullCategoryTag_NoCategoryFilter()
    {
        var c = SearchBarCriteriaFactory.Build("p", isRegex: false, caseSensitive: false, categoryTag: null);
        Assert.Null(c.CategoryFilter);
    }

    [Fact]
    public void Build_EmptyCategoryTag_NoCategoryFilter()
    {
        var c = SearchBarCriteriaFactory.Build("p", isRegex: false, caseSensitive: false, categoryTag: "");
        Assert.Null(c.CategoryFilter);
    }

    [Fact]
    public void Build_UnparseableCategoryTag_NoCategoryFilter()
    {
        // An "All" sentinel or any non-enum tag must mean "no category filter",
        // not a crash and not a stray filter.
        var c = SearchBarCriteriaFactory.Build("p", isRegex: false, caseSensitive: false, categoryTag: "All");
        Assert.Null(c.CategoryFilter);
    }
}

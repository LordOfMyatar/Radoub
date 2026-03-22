using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

public class SearchProviderBaseTests
{
    /// <summary>
    /// Concrete test subclass to expose protected methods.
    /// </summary>
    private class TestProvider : SearchProviderBase
    {
        public List<SearchMatch> TestSearchString(string value, FieldDefinition field, System.Text.RegularExpressions.Regex pattern, object? location)
            => SearchString(value, field, pattern, location);

        public List<SearchMatch> TestSearchLocString(CExoLocString? locString, FieldDefinition field, System.Text.RegularExpressions.Regex pattern, object? location, Func<uint, string?>? tlkResolver = null)
            => SearchLocString(locString, field, pattern, location, tlkResolver);

        public List<SearchMatch> TestSearchParams(IEnumerable<(string Key, string Value)> parameters, FieldDefinition field, System.Text.RegularExpressions.Regex pattern, object? location)
            => SearchParams(parameters, field, pattern, location);
    }

    private static FieldDefinition MakeField(string name = "TestField", SearchFieldType type = SearchFieldType.Text)
        => new() { Name = name, GffPath = name, FieldType = type, Category = SearchFieldCategory.Content };

    private static System.Text.RegularExpressions.Regex MakeRegex(string pattern, bool caseSensitive = false)
    {
        var criteria = new SearchCriteria { Pattern = pattern, CaseSensitive = caseSensitive };
        return criteria.ToRegex();
    }

    private readonly TestProvider _provider = new();

    // --- SearchString ---

    [Fact]
    public void SearchString_EmptyValue_ReturnsEmpty()
    {
        var matches = _provider.TestSearchString("", MakeField(), MakeRegex("hello"), null);
        Assert.Empty(matches);
    }

    [Fact]
    public void SearchString_NoMatch_ReturnsEmpty()
    {
        var matches = _provider.TestSearchString("goodbye world", MakeField(), MakeRegex("hello"), null);
        Assert.Empty(matches);
    }

    [Fact]
    public void SearchString_SingleMatch_ReturnsOne()
    {
        var matches = _provider.TestSearchString("hello world", MakeField(), MakeRegex("hello"), null);
        Assert.Single(matches);
        Assert.Equal("hello", matches[0].MatchedText);
        Assert.Equal(0, matches[0].MatchOffset);
        Assert.Equal(5, matches[0].MatchLength);
        Assert.Equal("hello world", matches[0].FullFieldValue);
    }

    [Fact]
    public void SearchString_MultipleMatches_ReturnsAll()
    {
        var matches = _provider.TestSearchString("cat sat on cat mat", MakeField(), MakeRegex("cat"), null);
        Assert.Equal(2, matches.Count);
        Assert.Equal(0, matches[0].MatchOffset);
        Assert.Equal(11, matches[1].MatchOffset);
    }

    [Fact]
    public void SearchString_PreservesLocation()
    {
        var location = "Entry #5";
        var matches = _provider.TestSearchString("hello", MakeField(), MakeRegex("hello"), location);
        Assert.Equal("Entry #5", matches[0].Location);
    }

    // --- SearchLocString ---

    [Fact]
    public void SearchLocString_Null_ReturnsEmpty()
    {
        var matches = _provider.TestSearchLocString(null, MakeField(), MakeRegex("hello"), null);
        Assert.Empty(matches);
    }

    [Fact]
    public void SearchLocString_MatchesInlineText()
    {
        var locString = new CExoLocString();
        locString.LocalizedStrings[0] = "I am Louis Romain";
        var matches = _provider.TestSearchLocString(locString, MakeField(), MakeRegex("Louis"), null);
        Assert.Single(matches);
        Assert.Equal("Louis", matches[0].MatchedText);
        Assert.Equal((uint)0, matches[0].LanguageId);
    }

    [Fact]
    public void SearchLocString_SearchesAllLanguages()
    {
        var locString = new CExoLocString();
        locString.LocalizedStrings[0] = "Hello Louis";
        locString.LocalizedStrings[2] = "Bonjour Louis";
        var matches = _provider.TestSearchLocString(locString, MakeField(), MakeRegex("Louis"), null);
        Assert.Equal(2, matches.Count);
        Assert.Equal((uint)0, matches[0].LanguageId);
        Assert.Equal((uint)2, matches[1].LanguageId);
    }

    [Fact]
    public void SearchLocString_TlkResolver_MatchesResolvedText()
    {
        var locString = new CExoLocString { StrRef = 1234 };
        // No inline strings, only TLK reference
        Func<uint, string?> resolver = strRef => strRef == 1234 ? "Bubba the Mighty" : null;
        var matches = _provider.TestSearchLocString(locString, MakeField(), MakeRegex("Bubba"), null, resolver);
        Assert.Single(matches);
        Assert.Equal("Bubba", matches[0].MatchedText);
        Assert.Null(matches[0].LanguageId); // TLK-resolved, not a specific language
    }

    [Fact]
    public void SearchLocString_NoTlkResolver_SkipsStrRef()
    {
        var locString = new CExoLocString { StrRef = 1234 };
        var matches = _provider.TestSearchLocString(locString, MakeField(), MakeRegex("Bubba"), null);
        Assert.Empty(matches);
    }

    // --- SearchParams ---

    [Fact]
    public void SearchParams_MatchesKey()
    {
        var parameters = new[] { ("sQuestTag", "q_main") };
        var matches = _provider.TestSearchParams(parameters, MakeField(), MakeRegex("sQuestTag"), null);
        Assert.Single(matches);
        Assert.Equal("sQuestTag", matches[0].MatchedText);
    }

    [Fact]
    public void SearchParams_MatchesValue()
    {
        var parameters = new[] { ("sQuestTag", "q_main_plot") };
        var matches = _provider.TestSearchParams(parameters, MakeField(), MakeRegex("q_main_plot"), null);
        Assert.Single(matches);
        Assert.Equal("q_main_plot", matches[0].MatchedText);
    }

    [Fact]
    public void SearchParams_MatchesBothKeyAndValue()
    {
        var parameters = new[] { ("test", "test_value") };
        var matches = _provider.TestSearchParams(parameters, MakeField(), MakeRegex("test"), null);
        Assert.Equal(2, matches.Count); // "test" in key and "test" in value
    }

    [Fact]
    public void SearchParams_Empty_ReturnsEmpty()
    {
        var matches = _provider.TestSearchParams(Array.Empty<(string, string)>(), MakeField(), MakeRegex("test"), null);
        Assert.Empty(matches);
    }
}

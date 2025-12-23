using Xunit;

namespace Radoub.Dictionary.Tests;

public class SpellCheckerTests
{
    [Fact]
    public void IsCorrect_ReturnsTrueForDictionaryWords()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Neverwinter");
        var checker = new SpellChecker(manager);

        Assert.True(checker.IsCorrect("Neverwinter"));
    }

    [Fact]
    public void IsCorrect_IsCaseInsensitive()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Neverwinter");
        var checker = new SpellChecker(manager);

        Assert.True(checker.IsCorrect("neverwinter"));
        Assert.True(checker.IsCorrect("NEVERWINTER"));
    }

    [Fact]
    public void IsCorrect_ReturnsFalseForUnknownWords()
    {
        var manager = new DictionaryManager();
        var checker = new SpellChecker(manager);

        Assert.False(checker.IsCorrect("unknownword"));
    }

    [Fact]
    public void IsCorrect_ReturnsTrueForNumbers()
    {
        var manager = new DictionaryManager();
        var checker = new SpellChecker(manager);

        Assert.True(checker.IsCorrect("123"));
        Assert.True(checker.IsCorrect("3.14"));
        Assert.True(checker.IsCorrect("-42"));
    }

    [Fact]
    public void IsCorrect_ReturnsTrueForEmptyStrings()
    {
        var manager = new DictionaryManager();
        var checker = new SpellChecker(manager);

        Assert.True(checker.IsCorrect(""));
        Assert.True(checker.IsCorrect("   "));
    }

    [Fact]
    public void IsCorrect_HandlesLeadingTrailingPunctuation()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Neverwinter");
        var checker = new SpellChecker(manager);

        Assert.True(checker.IsCorrect("Neverwinter."));
        Assert.True(checker.IsCorrect("Neverwinter!"));
        Assert.True(checker.IsCorrect("\"Neverwinter\""));
    }

    [Fact]
    public void IsCorrect_ReturnsTrueForIgnoredWords()
    {
        var manager = new DictionaryManager();
        manager.AddIgnoredWord("xyz");
        var checker = new SpellChecker(manager);

        Assert.True(checker.IsCorrect("xyz"));
    }

    [Fact]
    public void IgnoreForSession_AddsToSessionIgnoreList()
    {
        var manager = new DictionaryManager();
        var checker = new SpellChecker(manager);

        Assert.False(checker.IsCorrect("tempword"));

        checker.IgnoreForSession("tempword");

        Assert.True(checker.IsCorrect("tempword"));
        Assert.Equal(1, checker.SessionIgnoredCount);
    }

    [Fact]
    public void ClearSessionIgnored_RemovesSessionIgnoredWords()
    {
        var manager = new DictionaryManager();
        var checker = new SpellChecker(manager);
        checker.IgnoreForSession("tempword");

        checker.ClearSessionIgnored();

        Assert.False(checker.IsCorrect("tempword"));
        Assert.Equal(0, checker.SessionIgnoredCount);
    }

    [Fact]
    public void CheckText_FindsMisspelledWords()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Welcome");
        manager.AddWord("to");
        manager.AddWord("adventurer");
        var checker = new SpellChecker(manager);

        var errors = checker.CheckText("Welcome to Neverwinter, adventurer!").ToList();

        Assert.Single(errors);
        Assert.Equal("Neverwinter", errors[0].Word);
    }

    [Fact]
    public void CheckText_ReturnsCorrectPositions()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Hello");
        var checker = new SpellChecker(manager);

        var errors = checker.CheckText("Hello Neverwinter").ToList();

        Assert.Single(errors);
        Assert.Equal("Neverwinter", errors[0].Word);
        Assert.Equal(6, errors[0].StartIndex);
        Assert.Equal(11, errors[0].Length);
    }

    [Fact]
    public void CheckText_ReturnsEmptyForValidText()
    {
        var manager = new DictionaryManager();
        manager.AddWord("All");
        manager.AddWord("words");
        manager.AddWord("are");
        manager.AddWord("valid");
        var checker = new SpellChecker(manager);

        var errors = checker.CheckText("All words are valid").ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public void CheckText_HandlesEmptyText()
    {
        var manager = new DictionaryManager();
        var checker = new SpellChecker(manager);

        var errors = checker.CheckText("").ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public void GetSuggestions_ReturnsCloseMatches()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Neverwinter");
        manager.AddWord("Never");
        manager.AddWord("Winter");
        var checker = new SpellChecker(manager);

        var suggestions = checker.GetSuggestions("Neverwiner", 5).ToList();

        Assert.Contains("Neverwinter", suggestions);
    }

    [Fact]
    public void GetSuggestions_ReturnsEmptyForEmptyInput()
    {
        var manager = new DictionaryManager();
        var checker = new SpellChecker(manager);

        var suggestions = checker.GetSuggestions("").ToList();

        Assert.Empty(suggestions);
    }

    [Fact]
    public void GetSuggestions_LimitsResults()
    {
        var manager = new DictionaryManager();
        // Add many similar words
        for (int i = 0; i < 20; i++)
        {
            manager.AddWord($"Word{i}");
        }
        var checker = new SpellChecker(manager);

        var suggestions = checker.GetSuggestions("Word", 3).ToList();

        Assert.True(suggestions.Count <= 3);
    }

    [Fact]
    public void GetSuggestions_OrdersByEditDistance()
    {
        var manager = new DictionaryManager();
        manager.AddWord("cat");
        manager.AddWord("car");
        manager.AddWord("cart");
        manager.AddWord("castle");
        var checker = new SpellChecker(manager);

        var suggestions = checker.GetSuggestions("crt", 4).ToList();

        // "car" and "cart" should be near the top (edit distance 1)
        Assert.True(suggestions.IndexOf("car") <= 1 || suggestions.IndexOf("cart") <= 1);
    }
}

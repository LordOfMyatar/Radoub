using Xunit;
using Radoub.Dictionary;

namespace Manifest.Tests;

/// <summary>
/// Tests for spell-check integration in Manifest.
/// </summary>
public class SpellCheckTests
{
    [Fact]
    public void DictionaryManager_AddWord_IncreasesWordCount()
    {
        // Arrange
        var manager = new DictionaryManager();
        var initialCount = manager.WordCount;

        // Act
        manager.AddWord("testword");

        // Assert
        Assert.Equal(initialCount + 1, manager.WordCount);
    }

    [Fact]
    public void DictionaryManager_AddWord_ContainsWord()
    {
        // Arrange
        var manager = new DictionaryManager();

        // Act
        manager.AddWord("customterm");

        // Assert
        Assert.True(manager.ContainsWord("customterm"));
        Assert.True(manager.ContainsWord("CUSTOMTERM")); // Case insensitive
    }

    [Fact]
    public void DictionaryManager_AddIgnoredWord_IsIgnored()
    {
        // Arrange
        var manager = new DictionaryManager();

        // Act
        manager.AddIgnoredWord("nwnspecific");

        // Assert
        Assert.True(manager.IsIgnored("nwnspecific"));
        Assert.True(manager.IsKnown("nwnspecific"));
    }

    [Fact]
    public void DictionaryManager_Clear_RemovesAllWords()
    {
        // Arrange
        var manager = new DictionaryManager();
        manager.AddWord("word1");
        manager.AddWord("word2");
        manager.AddIgnoredWord("ignored1");

        // Act
        manager.Clear();

        // Assert
        Assert.Equal(0, manager.WordCount);
        Assert.Equal(0, manager.IgnoredWordCount);
    }

    [Fact]
    public void SpellChecker_CheckText_FindsMisspelledWords()
    {
        // Arrange
        var manager = new DictionaryManager();
        var checker = new SpellChecker(manager);

        // Add some known words
        manager.AddWord("hello");
        manager.AddWord("world");

        // Act
        var errors = checker.CheckText("hello wrold").ToList();

        // Assert
        Assert.Single(errors);
        Assert.Equal("wrold", errors[0].Word);
    }

    [Fact]
    public void SpellChecker_IsCorrect_ReturnsTrueForKnownWord()
    {
        // Arrange
        var manager = new DictionaryManager();
        manager.AddWord("manifest");
        var checker = new SpellChecker(manager);

        // Act & Assert
        Assert.True(checker.IsCorrect("manifest"));
        Assert.True(checker.IsCorrect("MANIFEST")); // Case insensitive
    }

    [Fact]
    public void SpellChecker_IgnoreForSession_StopsReportingWord()
    {
        // Arrange
        var manager = new DictionaryManager();
        var checker = new SpellChecker(manager);

        // Act - Initially unknown
        var errorsBefore = checker.CheckText("xyzabc").ToList();
        checker.IgnoreForSession("xyzabc");
        var errorsAfter = checker.CheckText("xyzabc").ToList();

        // Assert
        Assert.Single(errorsBefore);
        Assert.Empty(errorsAfter);
    }

    [Fact]
    public void SpellChecker_ClearSessionIgnored_ResetsIgnoredWords()
    {
        // Arrange
        var manager = new DictionaryManager();
        var checker = new SpellChecker(manager);
        checker.IgnoreForSession("tempword");

        // Act
        checker.ClearSessionIgnored();
        var errors = checker.CheckText("tempword").ToList();

        // Assert
        Assert.Single(errors);
    }
}

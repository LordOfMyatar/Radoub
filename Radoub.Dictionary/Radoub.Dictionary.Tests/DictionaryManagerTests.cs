using Radoub.Dictionary.Models;
using Xunit;

namespace Radoub.Dictionary.Tests;

public class DictionaryManagerTests
{
    [Fact]
    public void AddWord_AddsToWordSet()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Neverwinter");

        Assert.True(manager.ContainsWord("Neverwinter"));
        Assert.Equal(1, manager.WordCount);
    }

    [Fact]
    public void ContainsWord_IsCaseInsensitive()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Neverwinter");

        Assert.True(manager.ContainsWord("neverwinter"));
        Assert.True(manager.ContainsWord("NEVERWINTER"));
        Assert.True(manager.ContainsWord("NeVerWiNtEr"));
    }

    [Fact]
    public void AddWord_TrimsWhitespace()
    {
        var manager = new DictionaryManager();
        manager.AddWord("  Luskan  ");

        Assert.True(manager.ContainsWord("Luskan"));
    }

    [Fact]
    public void AddWord_IgnoresEmptyStrings()
    {
        var manager = new DictionaryManager();
        manager.AddWord("");
        manager.AddWord("   ");

        Assert.Equal(0, manager.WordCount);
    }

    [Fact]
    public void AddIgnoredWord_AddsToIgnoreList()
    {
        var manager = new DictionaryManager();
        manager.AddIgnoredWord("asdf");

        Assert.True(manager.IsIgnored("asdf"));
        Assert.Equal(1, manager.IgnoredWordCount);
    }

    [Fact]
    public void IsKnown_ReturnsTrueForDictionaryWords()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Waterdeep");

        Assert.True(manager.IsKnown("Waterdeep"));
    }

    [Fact]
    public void IsKnown_ReturnsTrueForIgnoredWords()
    {
        var manager = new DictionaryManager();
        manager.AddIgnoredWord("xyz");

        Assert.True(manager.IsKnown("xyz"));
    }

    [Fact]
    public void IsKnown_ReturnsFalseForUnknownWords()
    {
        var manager = new DictionaryManager();

        Assert.False(manager.IsKnown("unknownword"));
    }

    [Fact]
    public void MergeDictionary_AddsAllWords()
    {
        var manager = new DictionaryManager();
        var dictionary = new CustomDictionary
        {
            Source = "Test",
            Words = ["Aribeth", "Fenthick", "Desther"]
        };

        manager.MergeDictionary(dictionary);

        Assert.Equal(3, manager.WordCount);
        Assert.True(manager.ContainsWord("Aribeth"));
        Assert.True(manager.ContainsWord("Fenthick"));
        Assert.True(manager.ContainsWord("Desther"));
    }

    [Fact]
    public void MergeDictionary_AddsIgnoredWords()
    {
        var manager = new DictionaryManager();
        var dictionary = new CustomDictionary
        {
            Source = "Test",
            IgnoredWords = ["lol", "brb"]
        };

        manager.MergeDictionary(dictionary);

        Assert.True(manager.IsIgnored("lol"));
        Assert.True(manager.IsIgnored("brb"));
    }

    [Fact]
    public void MergeDictionary_AddsDictionaryEntries()
    {
        var manager = new DictionaryManager();
        var dictionary = new CustomDictionary
        {
            Source = "Test",
            Entries =
            [
                new DictionaryEntry { Word = "Beholder", Category = "creature" },
                new DictionaryEntry { Word = "Fireball", Category = "spell" }
            ]
        };

        manager.MergeDictionary(dictionary);

        Assert.Equal(2, manager.WordCount);
        Assert.True(manager.ContainsWord("Beholder"));
        Assert.True(manager.ContainsWord("Fireball"));
    }

    [Fact]
    public void CreateDictionary_ExportsAllWords()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Word1");
        manager.AddWord("Word2");
        manager.AddIgnoredWord("Ignored1");

        var dictionary = manager.CreateDictionary("Test Export");

        Assert.Equal("Test Export", dictionary.Source);
        Assert.Equal(2, dictionary.Words.Count);
        Assert.Contains("Word1", dictionary.Words);
        Assert.Contains("Word2", dictionary.Words);
        Assert.Single(dictionary.IgnoredWords);
        Assert.Contains("Ignored1", dictionary.IgnoredWords);
    }

    [Fact]
    public void Clear_RemovesAllWords()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Word1");
        manager.AddIgnoredWord("Ignored1");

        manager.Clear();

        Assert.Equal(0, manager.WordCount);
        Assert.Equal(0, manager.IgnoredWordCount);
        Assert.Equal(0, manager.DictionaryCount);
    }

    [Fact]
    public void LoadDictionaryFromJson_ParsesCorrectly()
    {
        var manager = new DictionaryManager();
        var json = """
        {
            "version": "1.0",
            "source": "Test Dictionary",
            "words": ["Aribeth", "Neverwinter", "Luskan"]
        }
        """;

        manager.LoadDictionaryFromJson(json);

        Assert.Equal(3, manager.WordCount);
        Assert.True(manager.ContainsWord("Aribeth"));
        Assert.True(manager.ContainsWord("Neverwinter"));
        Assert.True(manager.ContainsWord("Luskan"));
    }

    [Fact]
    public void ExportDictionaryToJson_GeneratesValidJson()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Waterdeep");
        manager.AddWord("Baldur");

        var json = manager.ExportDictionaryToJson("Test Export");

        Assert.Contains("\"source\": \"Test Export\"", json);
        Assert.Contains("Waterdeep", json);
        Assert.Contains("Baldur", json);
    }

    [Fact]
    public void GetAllWords_ReturnsAlphabeticalOrder()
    {
        var manager = new DictionaryManager();
        manager.AddWord("Zebra");
        manager.AddWord("Apple");
        manager.AddWord("Mango");

        var words = manager.GetAllWords().ToList();

        Assert.Equal(["Apple", "Mango", "Zebra"], words);
    }

    [Fact]
    public void MultipleDictionaries_MergeWithoutDuplicates()
    {
        var manager = new DictionaryManager();

        var dict1 = new CustomDictionary { Source = "Dict1", Words = ["Aribeth", "Fenthick"] };
        var dict2 = new CustomDictionary { Source = "Dict2", Words = ["Aribeth", "Desther"] };

        manager.MergeDictionary(dict1);
        manager.MergeDictionary(dict2);

        Assert.Equal(3, manager.WordCount); // Aribeth, Fenthick, Desther
        Assert.Equal(2, manager.DictionaryCount);
    }
}

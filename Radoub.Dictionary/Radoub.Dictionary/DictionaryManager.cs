using System.Text.Json;
using Radoub.Dictionary.Models;

namespace Radoub.Dictionary;

/// <summary>
/// Manages loading, saving, and merging dictionaries.
/// </summary>
public class DictionaryManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HashSet<string> _words = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _ignoredWords = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CustomDictionary> _loadedDictionaries = [];

    /// <summary>
    /// Gets the count of unique words in all loaded dictionaries.
    /// </summary>
    public int WordCount => _words.Count;

    /// <summary>
    /// Gets the count of ignored words.
    /// </summary>
    public int IgnoredWordCount => _ignoredWords.Count;

    /// <summary>
    /// Gets the number of dictionaries currently loaded.
    /// </summary>
    public int DictionaryCount => _loadedDictionaries.Count;

    /// <summary>
    /// Checks if a word exists in any loaded dictionary.
    /// </summary>
    public bool ContainsWord(string word)
    {
        return _words.Contains(word);
    }

    /// <summary>
    /// Checks if a word is in the ignored list.
    /// </summary>
    public bool IsIgnored(string word)
    {
        return _ignoredWords.Contains(word);
    }

    /// <summary>
    /// Checks if a word is known (exists in dictionary or is ignored).
    /// </summary>
    public bool IsKnown(string word)
    {
        return ContainsWord(word) || IsIgnored(word);
    }

    /// <summary>
    /// Loads a dictionary from a JSON file and merges it with existing words.
    /// </summary>
    public async Task LoadDictionaryAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var dictionary = JsonSerializer.Deserialize<CustomDictionary>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse dictionary file: {filePath}");

        MergeDictionary(dictionary);
    }

    /// <summary>
    /// Loads a dictionary from JSON content and merges it with existing words.
    /// </summary>
    public void LoadDictionaryFromJson(string json)
    {
        var dictionary = JsonSerializer.Deserialize<CustomDictionary>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse dictionary JSON");

        MergeDictionary(dictionary);
    }

    /// <summary>
    /// Merges a dictionary into the current word set.
    /// </summary>
    public void MergeDictionary(CustomDictionary dictionary)
    {
        foreach (var word in dictionary.AllWords)
        {
            if (!string.IsNullOrWhiteSpace(word))
            {
                _words.Add(word.Trim());
            }
        }

        foreach (var word in dictionary.IgnoredWords)
        {
            if (!string.IsNullOrWhiteSpace(word))
            {
                _ignoredWords.Add(word.Trim());
            }
        }

        _loadedDictionaries.Add(dictionary);
    }

    /// <summary>
    /// Adds a single word to the dictionary.
    /// </summary>
    public void AddWord(string word)
    {
        if (!string.IsNullOrWhiteSpace(word))
        {
            _words.Add(word.Trim());
        }
    }

    /// <summary>
    /// Adds a word to the ignored list.
    /// </summary>
    public void AddIgnoredWord(string word)
    {
        if (!string.IsNullOrWhiteSpace(word))
        {
            _ignoredWords.Add(word.Trim());
        }
    }

    /// <summary>
    /// Removes a word from the ignored list.
    /// </summary>
    public void RemoveIgnoredWord(string word)
    {
        _ignoredWords.Remove(word);
    }

    /// <summary>
    /// Clears all loaded dictionaries and words.
    /// </summary>
    public void Clear()
    {
        _words.Clear();
        _ignoredWords.Clear();
        _loadedDictionaries.Clear();
    }

    /// <summary>
    /// Creates a new dictionary from the current words.
    /// </summary>
    public CustomDictionary CreateDictionary(string source, string? description = null)
    {
        return new CustomDictionary
        {
            Source = source,
            Description = description,
            Words = _words.OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ToList(),
            IgnoredWords = _ignoredWords.OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    /// <summary>
    /// Exports the current dictionary to a JSON file.
    /// </summary>
    public async Task ExportDictionaryAsync(string filePath, string source, string? description = null)
    {
        var dictionary = CreateDictionary(source, description);
        var json = JsonSerializer.Serialize(dictionary, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Exports the current dictionary to a JSON string.
    /// </summary>
    public string ExportDictionaryToJson(string source, string? description = null)
    {
        var dictionary = CreateDictionary(source, description);
        return JsonSerializer.Serialize(dictionary, JsonOptions);
    }

    /// <summary>
    /// Gets all words in alphabetical order.
    /// </summary>
    public IEnumerable<string> GetAllWords()
    {
        return _words.OrderBy(w => w, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets all ignored words in alphabetical order.
    /// </summary>
    public IEnumerable<string> GetIgnoredWords()
    {
        return _ignoredWords.OrderBy(w => w, StringComparer.OrdinalIgnoreCase);
    }
}

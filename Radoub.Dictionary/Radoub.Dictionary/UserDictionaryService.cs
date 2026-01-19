using System.Text.Json;
using Radoub.Dictionary.Models;

namespace Radoub.Dictionary;

/// <summary>
/// Manages the shared user dictionary across all Radoub tools.
/// Supports both JSON format (custom.dic) and simple text format (custom_dictionary.txt).
/// </summary>
/// <remarks>
/// User dictionary locations:
/// - ~/Radoub/Dictionaries/custom.dic (JSON format, full features)
/// - ~/Radoub/Dictionaries/custom_dictionary.txt (simple text format, one word per line)
///
/// The text file format is easier for users to edit manually.
/// Both files are loaded if present; words are merged.
/// New words are saved to the JSON file by default.
/// </remarks>
public class UserDictionaryService
{
    private static UserDictionaryService? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Singleton instance of the user dictionary service.
    /// </summary>
    public static UserDictionaryService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new UserDictionaryService();
                }
            }
            return _instance;
        }
    }

    private readonly HashSet<string> _userWords = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _dictionaryPath;
    private readonly string _textFilePath;

    /// <summary>
    /// Event raised when a word is added to the dictionary.
    /// </summary>
    public event EventHandler<string>? WordAdded;

    /// <summary>
    /// Gets the number of user words.
    /// </summary>
    public int WordCount => _userWords.Count;

    /// <summary>
    /// Gets all user words in alphabetical order.
    /// </summary>
    public IEnumerable<string> Words => _userWords.OrderBy(w => w, StringComparer.OrdinalIgnoreCase);

    private UserDictionaryService()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dictionariesDir = Path.Combine(userProfile, "Radoub", "Dictionaries");
        Directory.CreateDirectory(dictionariesDir);

        _dictionaryPath = Path.Combine(dictionariesDir, "custom.dic");
        _textFilePath = Path.Combine(dictionariesDir, "custom_dictionary.txt");

        Load();
    }

    /// <summary>
    /// For testing: create instance with custom paths.
    /// </summary>
    internal UserDictionaryService(string dictionaryPath, string textFilePath)
    {
        _dictionaryPath = dictionaryPath;
        _textFilePath = textFilePath;
        Load();
    }

    /// <summary>
    /// Check if a word is in the user dictionary.
    /// </summary>
    public bool Contains(string word)
    {
        return !string.IsNullOrWhiteSpace(word) && _userWords.Contains(word.Trim());
    }

    /// <summary>
    /// Add a word to the user dictionary.
    /// </summary>
    /// <param name="word">Word to add</param>
    /// <param name="autoSave">If true, save to disk immediately (default: true)</param>
    public void AddWord(string word, bool autoSave = true)
    {
        if (string.IsNullOrWhiteSpace(word))
            return;

        var trimmedWord = word.Trim();
        if (_userWords.Add(trimmedWord))
        {
            WordAdded?.Invoke(this, trimmedWord);

            if (autoSave)
            {
                Save();
            }
        }
    }

    /// <summary>
    /// Add multiple words to the user dictionary.
    /// </summary>
    public void AddWords(IEnumerable<string> words, bool autoSave = true)
    {
        bool anyAdded = false;
        foreach (var word in words)
        {
            if (!string.IsNullOrWhiteSpace(word))
            {
                if (_userWords.Add(word.Trim()))
                {
                    anyAdded = true;
                    WordAdded?.Invoke(this, word.Trim());
                }
            }
        }

        if (anyAdded && autoSave)
        {
            Save();
        }
    }

    /// <summary>
    /// Remove a word from the user dictionary.
    /// </summary>
    public bool RemoveWord(string word, bool autoSave = true)
    {
        if (string.IsNullOrWhiteSpace(word))
            return false;

        var removed = _userWords.Remove(word.Trim());
        if (removed && autoSave)
        {
            Save();
        }
        return removed;
    }

    /// <summary>
    /// Clear all user words.
    /// </summary>
    public void Clear(bool autoSave = true)
    {
        _userWords.Clear();
        if (autoSave)
        {
            Save();
        }
    }

    /// <summary>
    /// Load user dictionary from disk.
    /// Loads both JSON and text formats if present.
    /// </summary>
    public void Load()
    {
        _userWords.Clear();

        // Load JSON format (custom.dic)
        if (File.Exists(_dictionaryPath))
        {
            LoadJsonFormat();
        }

        // Load text format (custom_dictionary.txt)
        if (File.Exists(_textFilePath))
        {
            LoadTextFormat();
        }
    }

    private void LoadJsonFormat()
    {
        try
        {
            var json = File.ReadAllText(_dictionaryPath);
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var dict = JsonSerializer.Deserialize<CustomDictionary>(json, options);

            if (dict != null)
            {
                foreach (var word in dict.AllWords)
                {
                    if (!string.IsNullOrWhiteSpace(word))
                    {
                        _userWords.Add(word.Trim());
                    }
                }
            }
        }
        catch
        {
            // Ignore errors - file may be corrupted
        }
    }

    private void LoadTextFormat()
    {
        try
        {
            var lines = File.ReadAllLines(_textFilePath);
            foreach (var line in lines)
            {
                // Skip empty lines and comments
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#") && !trimmed.StartsWith("//"))
                {
                    _userWords.Add(trimmed);
                }
            }
        }
        catch
        {
            // Ignore errors - file may be corrupted
        }
    }

    /// <summary>
    /// Save user dictionary to disk (JSON format).
    /// </summary>
    public void Save()
    {
        try
        {
            var dict = new CustomDictionary
            {
                Source = "Radoub User Dictionary",
                Description = "Custom words added by the user (shared across all Radoub tools)",
                Words = _userWords.OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(dict, options);
            File.WriteAllText(_dictionaryPath, json);
        }
        catch
        {
            // Ignore errors - user may not have write access
        }
    }

    /// <summary>
    /// Export user words to a simple text file (one word per line).
    /// </summary>
    public void ExportToTextFile(string? path = null)
    {
        try
        {
            var targetPath = path ?? _textFilePath;
            var lines = new List<string>
            {
                "# Radoub Custom Dictionary",
                "# Add words below, one per line. Lines starting with # are comments.",
                ""
            };
            lines.AddRange(_userWords.OrderBy(w => w, StringComparer.OrdinalIgnoreCase));
            File.WriteAllLines(targetPath, lines);
        }
        catch
        {
            // Ignore errors
        }
    }

    /// <summary>
    /// Create a sample text dictionary file if it doesn't exist.
    /// Pre-populated with common NWN/D&D terms.
    /// </summary>
    public void CreateSampleTextDictionary()
    {
        if (File.Exists(_textFilePath))
            return;

        var sampleWords = new[]
        {
            "# Radoub Custom Dictionary",
            "# Add words below, one per line. Lines starting with # are comments.",
            "# This file is shared across all Radoub tools.",
            "",
            "# D&D Races",
            "Aasimar",
            "Drow",
            "Gnoll",
            "Halfling",
            "Kobold",
            "Tiefling",
            "",
            "# D&D Classes",
            "Barbarian",
            "Bard",
            "Cleric",
            "Druid",
            "Monk",
            "Paladin",
            "Ranger",
            "Rogue",
            "Sorcerer",
            "Warlock",
            "Wizard",
            "",
            "# Neverwinter Nights Places",
            "Neverwinter",
            "Waterdeep",
            "Luskan",
            "Baldur",
            "Athkatla",
            "",
            "# Common D&D Terms",
            "cantrip",
            "longsword",
            "greatsword",
            "spellcasting",
            "multiclass"
        };

        try
        {
            File.WriteAllLines(_textFilePath, sampleWords);
        }
        catch
        {
            // Ignore errors
        }
    }

    /// <summary>
    /// Get the path to the text dictionary file.
    /// </summary>
    public string TextFilePath => _textFilePath;

    /// <summary>
    /// Get the path to the JSON dictionary file.
    /// </summary>
    public string JsonFilePath => _dictionaryPath;
}

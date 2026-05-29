using System.Text.Json;
using Radoub.Dictionary.Models;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;

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
    private readonly object _wordsLock = new();
    private readonly string _dictionaryPath;
    private readonly string _textFilePath;

    /// <summary>
    /// Event raised when a word is added to the dictionary.
    /// </summary>
    public event EventHandler<string>? WordAdded;

    /// <summary>
    /// Gets the number of user words.
    /// </summary>
    public int WordCount
    {
        get { lock (_wordsLock) { return _userWords.Count; } }
    }

    /// <summary>
    /// Gets all user words in alphabetical order.
    /// Returns a snapshot; safe to enumerate without holding the lock.
    /// </summary>
    public IEnumerable<string> Words
    {
        get
        {
            lock (_wordsLock)
            {
                return _userWords.OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
    }

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
        if (string.IsNullOrWhiteSpace(word))
            return false;

        lock (_wordsLock)
        {
            return _userWords.Contains(word.Trim());
        }
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
        bool added;
        lock (_wordsLock)
        {
            added = _userWords.Add(trimmedWord);
        }

        if (added)
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
        var addedWords = new List<string>();

        lock (_wordsLock)
        {
            foreach (var word in words)
            {
                if (!string.IsNullOrWhiteSpace(word))
                {
                    var trimmed = word.Trim();
                    if (_userWords.Add(trimmed))
                    {
                        anyAdded = true;
                        addedWords.Add(trimmed);
                    }
                }
            }
        }

        foreach (var added in addedWords)
        {
            WordAdded?.Invoke(this, added);
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

        bool removed;
        lock (_wordsLock)
        {
            removed = _userWords.Remove(word.Trim());
        }

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
        lock (_wordsLock)
        {
            _userWords.Clear();
        }

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
        lock (_wordsLock)
        {
            _userWords.Clear();
        }

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
                lock (_wordsLock)
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
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.WARN, $"Failed to load JSON dictionary: {ex.Message}", "UserDictionaryService", "Dictionary");
        }
    }

    private void LoadTextFormat()
    {
        try
        {
            var lines = File.ReadAllLines(_textFilePath);
            lock (_wordsLock)
            {
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
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.WARN, $"Failed to load text dictionary: {ex.Message}", "UserDictionaryService", "Dictionary");
        }
    }

    /// <summary>
    /// Cross-process lock acquisition retry budget for <see cref="Save"/>. Each attempt
    /// opens the lock file with <see cref="FileShare.None"/>; a sharing violation means
    /// another tool process is mid-save, so we back off and retry.
    /// </summary>
    private const int SaveLockRetries = 10;
    private const int SaveLockDelayMs = 25;

    private static readonly object _saveSerializer = new();

    /// <summary>
    /// Save the user dictionary to disk (JSON format), merging any words written by
    /// other tool instances/processes since this instance last read the file (#2263).
    ///
    /// Read-merge-write + atomic swap: the on-disk dictionary is re-read and unioned with
    /// this instance's words before writing, so concurrent writers no longer clobber each
    /// other (last-writer-wins becomes merge-then-write). The write goes to a sibling temp
    /// file and is swapped in via <see cref="AtomicFile.Replace"/>, so a reader never sees
    /// a half-written file. A coarse in-process lock plus a cross-process file lock serialize
    /// concurrent savers.
    /// </summary>
    public void Save()
    {
        // Serialize savers within this process first (the file lock handles other processes).
        lock (_saveSerializer)
        {
            FileStream? lockStream = null;
            try
            {
                lockStream = AcquireSaveLock();

                // Merge external words written since we loaded, folding them back into our
                // own set so this instance's view stays consistent with disk.
                var merged = MergeWithDisk();

                var dict = new CustomDictionary
                {
                    Source = "Radoub User Dictionary",
                    Description = "Custom words added by the user (shared across all Radoub tools)",
                    Words = merged
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(dict, options);

                var tempPath = _dictionaryPath + ".tmp";
                var backupPath = _dictionaryPath + ".bak";
                try
                {
                    File.WriteAllText(tempPath, json);
                    // Back up the prior file so a corrupt/overwritten dictionary is recoverable.
                    AtomicFile.Replace(tempPath, _dictionaryPath, backupPath);
                }
                finally
                {
                    // AtomicFile.Replace consumes tempPath on success; on failure (serialize/write
                    // threw, or the swap failed) it may remain — never leave a partial temp behind.
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.Log(LogLevel.WARN, $"Failed to save user dictionary: {ex.Message}", "UserDictionaryService", "Dictionary");
            }
            finally
            {
                lockStream?.Dispose();
            }
        }
    }

    /// <summary>
    /// Re-read the on-disk dictionary and union it with this instance's in-memory words,
    /// updating <see cref="_userWords"/> so the instance reflects external additions.
    /// Returns the merged, sorted snapshot to persist.
    /// </summary>
    private List<string> MergeWithDisk()
    {
        var diskWords = ReadDiskWords();

        lock (_wordsLock)
        {
            foreach (var word in diskWords)
            {
                if (!string.IsNullOrWhiteSpace(word))
                    _userWords.Add(word.Trim());
            }
            return _userWords.OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>
    /// Read the current words from the on-disk JSON dictionary. Returns an empty list if
    /// the file is absent or unreadable (a transient/partial read must not drop in-memory
    /// words — the union still contains them).
    /// </summary>
    private List<string> ReadDiskWords()
    {
        if (!File.Exists(_dictionaryPath))
            return new List<string>();

        try
        {
            var json = File.ReadAllText(_dictionaryPath);
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var dict = JsonSerializer.Deserialize<CustomDictionary>(json, options);
            return dict?.AllWords.ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.WARN, $"Could not read existing dictionary for merge: {ex.Message}", "UserDictionaryService", "Dictionary");
            return new List<string>();
        }
    }

    /// <summary>
    /// Acquire a cross-process advisory lock by opening a sidecar lock file with
    /// <see cref="FileShare.None"/>. Retries on sharing violation; returns null if the
    /// lock cannot be acquired within the retry budget (the save then proceeds best-effort).
    /// </summary>
    private FileStream? AcquireSaveLock()
    {
        var lockPath = _dictionaryPath + ".lock";
        for (int attempt = 0; attempt < SaveLockRetries; attempt++)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                Thread.Sleep(SaveLockDelayMs);
            }
            catch (UnauthorizedAccessException)
            {
                // Cannot create/open lock file at all; proceed without the cross-process lock.
                return null;
            }
        }

        UnifiedLogger.Log(LogLevel.WARN, "Timed out acquiring user-dictionary save lock; saving without cross-process lock", "UserDictionaryService", "Dictionary");
        return null;
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

            lock (_wordsLock)
            {
                lines.AddRange(_userWords.OrderBy(w => w, StringComparer.OrdinalIgnoreCase));
            }

            File.WriteAllLines(targetPath, lines);
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.WARN, $"Failed to export text dictionary: {ex.Message}", "UserDictionaryService", "Dictionary");
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
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"Failed to create sample dictionary: {ex.Message}", "UserDictionaryService", "Dictionary");
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

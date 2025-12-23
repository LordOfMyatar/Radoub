using System.Reflection;
using System.Text.RegularExpressions;
using WeCantSpell.Hunspell;

namespace Radoub.Dictionary;

/// <summary>
/// Provides spell-checking functionality using Hunspell and custom dictionaries.
/// Uses WeCantSpell.Hunspell for base language checking and DictionaryManager for
/// D&amp;D/NWN-specific terminology overlay.
/// </summary>
public partial class SpellChecker : IDisposable
{
    private readonly DictionaryManager _dictionaryManager;
    private readonly HashSet<string> _sessionIgnored = new(StringComparer.OrdinalIgnoreCase);
    private WordList? _hunspell;
    private bool _disposed;

    /// <summary>
    /// Creates a new spell checker using the given dictionary manager.
    /// Call LoadHunspellDictionaryAsync to enable base language checking.
    /// </summary>
    public SpellChecker(DictionaryManager dictionaryManager)
    {
        _dictionaryManager = dictionaryManager;
    }

    /// <summary>
    /// Whether Hunspell is loaded and available for base language checking.
    /// </summary>
    public bool IsHunspellLoaded => _hunspell != null;

    /// <summary>
    /// Loads a Hunspell dictionary from file paths.
    /// </summary>
    /// <param name="dicPath">Path to the .dic file.</param>
    /// <param name="affPath">Path to the .aff file.</param>
    public async Task LoadHunspellDictionaryAsync(string dicPath, string affPath)
    {
        _hunspell = await WordList.CreateFromFilesAsync(dicPath, affPath);
    }

    /// <summary>
    /// Loads a Hunspell dictionary from streams.
    /// </summary>
    public async Task LoadHunspellDictionaryAsync(Stream dicStream, Stream affStream)
    {
        _hunspell = await WordList.CreateFromStreamsAsync(dicStream, affStream);
    }

    /// <summary>
    /// Loads a bundled Hunspell dictionary from embedded resources.
    /// </summary>
    /// <param name="languageCode">Language code (e.g., "en_US", "es_ES").</param>
    public async Task LoadBundledDictionaryAsync(string languageCode)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var dicResourceName = $"Radoub.Dictionary.Dictionaries.{languageCode}.{languageCode}.dic";
        var affResourceName = $"Radoub.Dictionary.Dictionaries.{languageCode}.{languageCode}.aff";

        using var dicStream = assembly.GetManifestResourceStream(dicResourceName)
            ?? throw new FileNotFoundException($"Bundled dictionary not found: {languageCode}.dic");
        using var affStream = assembly.GetManifestResourceStream(affResourceName)
            ?? throw new FileNotFoundException($"Bundled dictionary not found: {languageCode}.aff");

        _hunspell = await WordList.CreateFromStreamsAsync(dicStream, affStream);
    }

    /// <summary>
    /// Checks if a word is spelled correctly.
    /// </summary>
    /// <returns>True if the word is known (in Hunspell, custom dictionary, ignored, or session-ignored).</returns>
    public bool IsCorrect(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return true;

        var cleanWord = CleanWord(word);
        if (string.IsNullOrWhiteSpace(cleanWord))
            return true;

        // Numbers are always correct
        if (IsNumber(cleanWord))
            return true;

        // Check session ignore list first (fastest)
        if (_sessionIgnored.Contains(cleanWord))
            return true;

        // Check custom dictionary (D&D/NWN terms)
        if (_dictionaryManager.IsKnown(cleanWord))
            return true;

        // Check Hunspell if loaded (base language)
        if (_hunspell != null && _hunspell.Check(cleanWord))
            return true;

        return false;
    }

    /// <summary>
    /// Gets all misspelled words in the given text.
    /// </summary>
    public IEnumerable<SpellingError> CheckText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var matches = WordPattern().Matches(text);
        foreach (Match match in matches)
        {
            var word = match.Value;
            if (!IsCorrect(word))
            {
                yield return new SpellingError
                {
                    Word = word,
                    StartIndex = match.Index,
                    Length = match.Length
                };
            }
        }
    }

    /// <summary>
    /// Gets spelling suggestions for a misspelled word.
    /// </summary>
    /// <param name="word">The misspelled word.</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return.</param>
    public IEnumerable<string> GetSuggestions(string word, int maxSuggestions = 5)
    {
        if (string.IsNullOrWhiteSpace(word))
            yield break;

        var cleanWord = CleanWord(word);
        if (string.IsNullOrWhiteSpace(cleanWord))
            yield break;

        var suggestions = new List<string>();

        // Get Hunspell suggestions first (better quality for real words)
        if (_hunspell != null)
        {
            suggestions.AddRange(_hunspell.Suggest(cleanWord).Take(maxSuggestions));
        }

        // Add custom dictionary suggestions if we need more
        if (suggestions.Count < maxSuggestions)
        {
            var remaining = maxSuggestions - suggestions.Count;
            var customSuggestions = _dictionaryManager.GetAllWords()
                .Select(w => new { Word = w, Distance = LevenshteinDistance(cleanWord.ToLowerInvariant(), w.ToLowerInvariant()) })
                .Where(x => x.Distance <= 3 && !suggestions.Contains(x.Word, StringComparer.OrdinalIgnoreCase))
                .OrderBy(x => x.Distance)
                .ThenBy(x => Math.Abs(x.Word.Length - cleanWord.Length))
                .Take(remaining)
                .Select(x => x.Word);

            suggestions.AddRange(customSuggestions);
        }

        foreach (var suggestion in suggestions.Take(maxSuggestions))
        {
            yield return suggestion;
        }
    }

    /// <summary>
    /// Ignores a word for the current session only.
    /// </summary>
    public void IgnoreForSession(string word)
    {
        if (!string.IsNullOrWhiteSpace(word))
        {
            _sessionIgnored.Add(word.Trim());
        }
    }

    /// <summary>
    /// Clears all session-ignored words.
    /// </summary>
    public void ClearSessionIgnored()
    {
        _sessionIgnored.Clear();
    }

    /// <summary>
    /// Gets the count of session-ignored words.
    /// </summary>
    public int SessionIgnoredCount => _sessionIgnored.Count;

    /// <summary>
    /// Cleans a word by removing leading/trailing punctuation.
    /// </summary>
    private static string CleanWord(string word)
    {
        return word.Trim().Trim('\'', '"', '.', ',', '!', '?', ';', ':', '(', ')', '[', ']', '{', '}');
    }

    /// <summary>
    /// Checks if a string is a number.
    /// </summary>
    private static bool IsNumber(string word)
    {
        return word.All(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-' || c == '+');
    }

    /// <summary>
    /// Calculates the Levenshtein distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s))
            return string.IsNullOrEmpty(t) ? 0 : t.Length;
        if (string.IsNullOrEmpty(t))
            return s.Length;

        var m = s.Length;
        var n = t.Length;
        var d = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++)
            d[i, 0] = i;
        for (var j = 0; j <= n; j++)
            d[0, j] = j;

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[m, n];
    }

    [GeneratedRegex(@"\b[\w']+\b", RegexOptions.Compiled)]
    private static partial Regex WordPattern();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // WordList doesn't implement IDisposable but clear reference
                _hunspell = null;
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents a spelling error in text.
/// </summary>
public class SpellingError
{
    /// <summary>
    /// The misspelled word.
    /// </summary>
    public required string Word { get; set; }

    /// <summary>
    /// Start index of the word in the original text.
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// Length of the word.
    /// </summary>
    public int Length { get; set; }
}

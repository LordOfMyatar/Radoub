using System.Text.RegularExpressions;

namespace Radoub.Dictionary;

/// <summary>
/// Provides spell-checking functionality using loaded dictionaries.
/// </summary>
public partial class SpellChecker
{
    private readonly DictionaryManager _dictionaryManager;
    private readonly HashSet<string> _sessionIgnored = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new spell checker using the given dictionary manager.
    /// </summary>
    public SpellChecker(DictionaryManager dictionaryManager)
    {
        _dictionaryManager = dictionaryManager;
    }

    /// <summary>
    /// Checks if a word is spelled correctly.
    /// </summary>
    /// <returns>True if the word is known (in dictionary, ignored, or session-ignored).</returns>
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

        // Check dictionary and ignore lists
        return _dictionaryManager.IsKnown(cleanWord) || _sessionIgnored.Contains(cleanWord);
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

        // Find words with small edit distance
        var candidates = _dictionaryManager.GetAllWords()
            .Select(w => new { Word = w, Distance = LevenshteinDistance(cleanWord.ToLowerInvariant(), w.ToLowerInvariant()) })
            .Where(x => x.Distance <= 3) // Max 3 edits
            .OrderBy(x => x.Distance)
            .ThenBy(x => Math.Abs(x.Word.Length - cleanWord.Length))
            .Take(maxSuggestions)
            .Select(x => x.Word);

        foreach (var candidate in candidates)
        {
            yield return candidate;
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

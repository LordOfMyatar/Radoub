using System.Text.RegularExpressions;
using Radoub.Formats.Gff;
using Radoub.Formats.TwoDA;

namespace Radoub.Dictionary;

/// <summary>
/// Extracts terms from Aurora Engine game files (.2da, dialog files, etc.).
/// </summary>
public static partial class TermExtractor
{
    /// <summary>
    /// Extracts terms from a 2DA file.
    /// </summary>
    /// <param name="twoDA">The parsed 2DA file.</param>
    /// <param name="columnNames">Column names to extract from (e.g., "LABEL", "NAME", "STRREF"). If null, extracts from common name columns.</param>
    /// <returns>List of extracted terms.</returns>
    public static IEnumerable<string> ExtractFromTwoDA(TwoDAFile twoDA, IEnumerable<string>? columnNames = null)
    {
        var columnsToCheck = columnNames?.ToList() ?? GetDefaultNameColumns();
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var columnName in columnsToCheck)
        {
            var colIndex = twoDA.GetColumnIndex(columnName);
            if (colIndex < 0)
                continue;

            foreach (var row in twoDA.Rows)
            {
                var value = row.GetValue(colIndex);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // Clean up the value (remove underscores, handle multi-word)
                    var cleanedTerms = CleanTerm(value);
                    foreach (var term in cleanedTerms)
                    {
                        if (!string.IsNullOrWhiteSpace(term) && term.Length > 1)
                        {
                            terms.Add(term);
                        }
                    }
                }
            }
        }

        return terms.OrderBy(t => t, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts terms from a 2DA file loaded from a file path.
    /// </summary>
    public static IEnumerable<string> ExtractFromTwoDAFile(string filePath, IEnumerable<string>? columnNames = null)
    {
        var twoDA = TwoDAReader.Read(filePath);
        return ExtractFromTwoDA(twoDA, columnNames);
    }

    /// <summary>
    /// Extracts text strings from a GFF file (dialog, journal, etc.).
    /// </summary>
    /// <param name="gff">The parsed GFF file.</param>
    /// <returns>List of extracted terms.</returns>
    public static IEnumerable<string> ExtractFromGff(GffFile gff)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExtractFromStruct(gff.RootStruct, terms);
        return terms.OrderBy(t => t, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts text strings from a GFF file loaded from a file path.
    /// </summary>
    public static IEnumerable<string> ExtractFromGffFile(string filePath)
    {
        var gff = GffReader.Read(filePath);
        return ExtractFromGff(gff);
    }

    /// <summary>
    /// Extracts words from text content (dialog text, descriptions, etc.).
    /// Filters to words that look like proper nouns or game-specific terms.
    /// </summary>
    public static IEnumerable<string> ExtractProperNouns(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var matches = WordPattern().Matches(text);
        var seenWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches)
        {
            var word = match.Value;

            // Skip if already seen
            if (!seenWords.Add(word))
                continue;

            // Skip very short words
            if (word.Length < 2)
                continue;

            // Include words that start with capital letter (proper nouns)
            // or contain unusual patterns (game terms)
            if (char.IsUpper(word[0]) || IsGameTerm(word))
            {
                yield return word;
            }
        }
    }

    /// <summary>
    /// Recursively extracts text from a GFF struct.
    /// </summary>
    private static void ExtractFromStruct(GffStruct gffStruct, HashSet<string> terms)
    {
        foreach (var field in gffStruct.Fields)
        {
            switch (field.Type)
            {
                case GffField.CExoString:
                    if (field.Value is string str && !string.IsNullOrWhiteSpace(str))
                    {
                        ExtractWordsFromText(str, terms);
                    }
                    break;

                case GffField.CExoLocString:
                    if (field.Value is CExoLocString locStr)
                    {
                        foreach (var localizedString in locStr.LocalizedStrings.Values)
                        {
                            if (!string.IsNullOrWhiteSpace(localizedString))
                            {
                                ExtractWordsFromText(localizedString, terms);
                            }
                        }
                    }
                    break;

                case GffField.Struct:
                    if (field.Value is GffStruct childStruct)
                    {
                        ExtractFromStruct(childStruct, terms);
                    }
                    break;

                case GffField.List:
                    if (field.Value is GffList list)
                    {
                        foreach (var element in list.Elements)
                        {
                            ExtractFromStruct(element, terms);
                        }
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Extracts words from text and adds them to the term set.
    /// </summary>
    private static void ExtractWordsFromText(string text, HashSet<string> terms)
    {
        foreach (var word in ExtractProperNouns(text))
        {
            terms.Add(word);
        }
    }

    /// <summary>
    /// Cleans a term from 2DA (handles underscores, mixed case, etc.).
    /// </summary>
    private static IEnumerable<string> CleanTerm(string term)
    {
        // Replace underscores with spaces
        var cleaned = term.Replace('_', ' ').Trim();

        // If it's a multi-word term, split and return individual words
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            // Skip pure numbers
            if (word.All(char.IsDigit))
                continue;

            // Skip very short tokens
            if (word.Length < 2)
                continue;

            yield return word;
        }

        // Also return the full term if multi-word
        if (words.Length > 1)
        {
            yield return cleaned;
        }
    }

    /// <summary>
    /// Default columns to check in 2DA files for names/labels.
    /// </summary>
    private static List<string> GetDefaultNameColumns()
    {
        return
        [
            "LABEL",
            "NAME",
            "Label",
            "Name",
            "STRREF",  // String reference (would need TLK lookup)
            "FEAT",
            "SPELLLABEL",
            "ITEMCLASS"
        ];
    }

    /// <summary>
    /// Checks if a word looks like a game-specific term.
    /// </summary>
    private static bool IsGameTerm(string word)
    {
        // Words with mixed case in the middle (e.g., "MacGuffin")
        if (word.Length > 2)
        {
            for (int i = 1; i < word.Length - 1; i++)
            {
                if (char.IsUpper(word[i]))
                    return true;
            }
        }

        // Words with apostrophes (common in fantasy names)
        if (word.Contains('\''))
            return true;

        // Words ending in common fantasy suffixes
        var lowerWord = word.ToLowerInvariant();
        string[] fantasySuffixes = ["heim", "fell", "dale", "hold", "keep", "haven", "shire"];
        foreach (var suffix in fantasySuffixes)
        {
            if (lowerWord.EndsWith(suffix) && word.Length > suffix.Length + 2)
                return true;
        }

        return false;
    }

    [GeneratedRegex(@"\b[\w']+\b", RegexOptions.Compiled)]
    private static partial Regex WordPattern();
}

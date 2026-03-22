using System.Text.RegularExpressions;
using Radoub.Formats.Gff;

namespace Radoub.Formats.Search;

/// <summary>
/// Shared matching logic for all search providers.
/// Subclasses implement file-type-specific traversal.
/// </summary>
public abstract class SearchProviderBase
{
    /// <summary>
    /// Search a plain string field for matches.
    /// </summary>
    protected static List<SearchMatch> SearchString(
        string value, FieldDefinition field, Regex pattern, object? location)
    {
        var matches = new List<SearchMatch>();
        if (string.IsNullOrEmpty(value)) return matches;

        foreach (Match m in pattern.Matches(value))
        {
            matches.Add(new SearchMatch
            {
                Field = field,
                MatchedText = m.Value,
                FullFieldValue = value,
                MatchOffset = m.Index,
                MatchLength = m.Length,
                Location = location
            });
        }
        return matches;
    }

    /// <summary>
    /// Search a CExoLocString field across all language variants.
    /// Optionally resolves TLK StrRef values via a resolver function.
    /// </summary>
    protected static List<SearchMatch> SearchLocString(
        CExoLocString? locString, FieldDefinition field, Regex pattern,
        object? location, Func<uint, string?>? tlkResolver = null)
    {
        var matches = new List<SearchMatch>();
        if (locString == null) return matches;

        // Search inline localized strings
        foreach (var (langId, text) in locString.LocalizedStrings)
        {
            if (string.IsNullOrEmpty(text)) continue;

            foreach (Match m in pattern.Matches(text))
            {
                matches.Add(new SearchMatch
                {
                    Field = field,
                    MatchedText = m.Value,
                    FullFieldValue = text,
                    MatchOffset = m.Index,
                    MatchLength = m.Length,
                    Location = location,
                    LanguageId = langId
                });
            }
        }

        // Search TLK-resolved text (if resolver provided and StrRef is set)
        if (tlkResolver != null && locString.StrRef != 0xFFFFFFFF)
        {
            var resolved = tlkResolver(locString.StrRef);
            if (!string.IsNullOrEmpty(resolved))
            {
                foreach (Match m in pattern.Matches(resolved))
                {
                    matches.Add(new SearchMatch
                    {
                        Field = field,
                        MatchedText = m.Value,
                        FullFieldValue = resolved,
                        MatchOffset = m.Index,
                        MatchLength = m.Length,
                        Location = location,
                        LanguageId = null // TLK-resolved, not a specific language
                    });
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// Search VarTable variable names and string values.
    /// </summary>
    protected static List<SearchMatch> SearchVarTable(
        GffStruct root, FieldDefinition field, Regex pattern, object? location)
    {
        var matches = new List<SearchMatch>();
        var variables = VarTableHelper.ReadVarTable(root);

        foreach (var variable in variables)
        {
            // Search variable name
            foreach (Match m in pattern.Matches(variable.Name ?? string.Empty))
            {
                matches.Add(new SearchMatch
                {
                    Field = field,
                    MatchedText = m.Value,
                    FullFieldValue = $"{variable.Name} = {variable.Value}",
                    MatchOffset = m.Index,
                    MatchLength = m.Length,
                    Location = location
                });
            }

            // Search string variable values
            if (variable.Type == VariableType.String && variable.Value is string strValue)
            {
                foreach (Match m in pattern.Matches(strValue))
                {
                    matches.Add(new SearchMatch
                    {
                        Field = field,
                        MatchedText = m.Value,
                        FullFieldValue = $"{variable.Name} = {strValue}",
                        MatchOffset = m.Index,
                        MatchLength = m.Length,
                        Location = location
                    });
                }
            }
        }
        return matches;
    }

    /// <summary>
    /// Search script parameter key/value pairs.
    /// </summary>
    protected static List<SearchMatch> SearchParams(
        IEnumerable<(string Key, string Value)> parameters,
        FieldDefinition field, Regex pattern, object? location)
    {
        var matches = new List<SearchMatch>();
        foreach (var (key, value) in parameters)
        {
            var display = $"{key} = {value}";

            foreach (Match m in pattern.Matches(key))
            {
                matches.Add(new SearchMatch
                {
                    Field = field,
                    MatchedText = m.Value,
                    FullFieldValue = display,
                    MatchOffset = m.Index,
                    MatchLength = m.Length,
                    Location = location
                });
            }

            foreach (Match m in pattern.Matches(value))
            {
                matches.Add(new SearchMatch
                {
                    Field = field,
                    MatchedText = m.Value,
                    FullFieldValue = display,
                    MatchOffset = m.Index,
                    MatchLength = m.Length,
                    Location = location
                });
            }
        }
        return matches;
    }
}

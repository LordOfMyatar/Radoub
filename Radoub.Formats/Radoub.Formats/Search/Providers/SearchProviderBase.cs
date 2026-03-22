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

    // --- Replace helpers ---

    /// <summary>
    /// Apply a text replacement at the match offset within a string value.
    /// For regex replacements, uses capture group substitution on the matched text.
    /// </summary>
    protected static string ReplaceInString(string value, ReplaceOperation op)
    {
        var offset = op.Match.MatchOffset;
        var length = op.Match.MatchLength;

        if (op.IsRegex)
        {
            // Use regex substitution on the matched portion to support capture groups
            var regex = new Regex(Regex.Escape(op.Match.MatchedText));
            var replaced = regex.Replace(op.Match.MatchedText, op.ReplacementText, 1);
            return string.Concat(value.AsSpan(0, offset), replaced, value.AsSpan(offset + length));
        }

        return string.Concat(value.AsSpan(0, offset), op.ReplacementText, value.AsSpan(offset + length));
    }

    /// <summary>
    /// Apply a text replacement to a ResRef value with 16-char length validation.
    /// Returns the new value and an optional warning if truncated.
    /// Note: This changes the reference field only — it does NOT rename the
    /// actual resource file on disk. The caller must rename files separately
    /// if the referenced resource (e.g., a .utc blueprint) needs to match.
    /// </summary>
    protected static (string newValue, string? warning) ReplaceResRef(string value, ReplaceOperation op)
    {
        var newValue = ReplaceInString(value, op);
        string? warning = null;

        if (newValue.Length > 16)
        {
            warning = $"ResRef truncated from {newValue.Length} to 16 characters: '{newValue}' → '{newValue[..16]}'";
            newValue = newValue[..16];
        }

        return (newValue, warning);
    }

    /// <summary>
    /// Apply a replace operation to a CExoString or CResRef GFF field.
    /// Mutates the field's Value in place.
    /// </summary>
    protected static ReplaceResult ReplaceStringField(GffStruct gffStruct, string fieldLabel, ReplaceOperation op)
    {
        var field = gffStruct.GetField(fieldLabel);
        if (field == null || field.Value is not string currentValue)
        {
            return new ReplaceResult
            {
                Success = false, Field = op.Match.Field,
                OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                Skipped = true, SkipReason = $"Field '{fieldLabel}' not found in GFF struct"
            };
        }

        string? warning = null;
        string newValue;

        if (field.Type == GffField.CResRef || op.Match.Field.FieldType == SearchFieldType.ResRef)
        {
            (newValue, warning) = ReplaceResRef(currentValue, op);
        }
        else
        {
            newValue = ReplaceInString(currentValue, op);
        }

        var oldValue = currentValue;
        field.Value = newValue;

        return new ReplaceResult
        {
            Success = true, Field = op.Match.Field,
            OldValue = oldValue, NewValue = newValue,
            Warning = warning
        };
    }

    /// <summary>
    /// Apply a replace operation to a CExoLocString GFF field's specific language variant.
    /// Uses op.Match.LanguageId to target the correct language.
    /// </summary>
    protected static ReplaceResult ReplaceLocStringField(GffStruct gffStruct, string fieldLabel, ReplaceOperation op)
    {
        var field = gffStruct.GetField(fieldLabel);
        if (field?.Value is not CExoLocString locString)
        {
            return new ReplaceResult
            {
                Success = false, Field = op.Match.Field,
                OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                Skipped = true, SkipReason = $"LocString field '{fieldLabel}' not found in GFF struct"
            };
        }

        var langId = op.Match.LanguageId ?? 0;
        if (!locString.LocalizedStrings.TryGetValue(langId, out var currentValue))
        {
            return new ReplaceResult
            {
                Success = false, Field = op.Match.Field,
                OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                Skipped = true, SkipReason = $"Language variant {langId} not found in '{fieldLabel}'"
            };
        }

        var newValue = ReplaceInString(currentValue, op);
        locString.LocalizedStrings[langId] = newValue;

        return new ReplaceResult
        {
            Success = true, Field = op.Match.Field,
            OldValue = currentValue, NewValue = newValue
        };
    }

    /// <summary>
    /// Sort replace operations in reverse offset order within each field,
    /// so that later replacements don't shift earlier match offsets.
    /// </summary>
    protected static IReadOnlyList<ReplaceOperation> SortReverseOffset(IReadOnlyList<ReplaceOperation> operations)
    {
        return operations
            .OrderByDescending(op => op.Match.MatchOffset)
            .ToList();
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

using System.Text.RegularExpressions;
using Radoub.Formats.Gff;

namespace Radoub.Formats.Search;

/// <summary>
/// Fallback provider that walks any GFF file tree and searches all
/// CExoString, CResRef, and CExoLocString fields.
/// Used for file types without a dedicated provider.
/// </summary>
public class GenericGffSearchProvider : SearchProviderBase, IFileSearchProvider
{
    public ushort FileType => 0; // Generic — used as fallback

    public IReadOnlyList<string> Extensions => Array.Empty<string>();

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();
        WalkStruct(gffFile.RootStruct, regex, "", matches);
        return matches;
    }

    public IReadOnlyList<ReplaceResult> Replace(GffFile gffFile, IReadOnlyList<ReplaceOperation> operations)
    {
        if (operations.Count == 0) return Array.Empty<ReplaceResult>();

        var sorted = SortReverseOffset(operations);
        var results = new List<ReplaceResult>();

        foreach (var op in sorted)
        {
            if (op.Match.Location is not string fieldPath)
            {
                results.Add(new ReplaceResult
                {
                    Success = false, Field = op.Match.Field,
                    OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                    Skipped = true, SkipReason = "Missing field path location"
                });
                continue;
            }

            var targetStruct = NavigateToParent(gffFile.RootStruct, fieldPath);
            if (targetStruct == null)
            {
                results.Add(new ReplaceResult
                {
                    Success = false, Field = op.Match.Field,
                    OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                    Skipped = true, SkipReason = $"Could not navigate to: {fieldPath}"
                });
                continue;
            }

            var result = op.Match.Field.FieldType switch
            {
                SearchFieldType.LocString => ReplaceLocStringField(targetStruct, op.Match.Field.GffPath, op),
                _ => ReplaceStringField(targetStruct, op.Match.Field.GffPath, op)
            };
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Navigate a dot-separated path (e.g., "EntryList[3].Text") to find the parent struct.
    /// Returns the struct containing the final field.
    /// </summary>
    private static GffStruct? NavigateToParent(GffStruct root, string path)
    {
        // Path format: "Field", "Parent.Field", "Parent[0].Field", "A.B[1].C[2].Field"
        var segments = path.Split('.');
        var current = root;

        // Navigate all segments except the last (which is the field itself)
        for (int i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            var bracketIndex = segment.IndexOf('[');

            if (bracketIndex >= 0)
            {
                // List navigation: "FieldName[index]"
                var fieldName = segment[..bracketIndex];
                var indexStr = segment[(bracketIndex + 1)..^1];
                if (!int.TryParse(indexStr, out var index)) return null;

                var listField = current.Fields?.FirstOrDefault(f => f.Label == fieldName);
                if (listField?.Value is not GffList list) return null;
                if (index < 0 || index >= list.Elements.Count) return null;

                current = list.Elements[index];
            }
            else
            {
                // Struct navigation
                var structField = current.Fields?.FirstOrDefault(f => f.Label == segment);
                if (structField?.Value is not GffStruct childStruct) return null;
                current = childStruct;
            }
        }

        return current;
    }

    private void WalkStruct(GffStruct gffStruct, Regex pattern, string pathPrefix, List<SearchMatch> matches)
    {
        if (gffStruct.Fields == null) return;

        foreach (var field in gffStruct.Fields)
        {
            var fieldPath = string.IsNullOrEmpty(pathPrefix) ? field.Label : $"{pathPrefix}.{field.Label}";

            switch (field.Type)
            {
                case GffField.CExoString:
                case GffField.CResRef:
                    if (field.Value is string strValue)
                    {
                        var fieldDef = MakeFieldDef(field);
                        matches.AddRange(SearchString(strValue, fieldDef, pattern, fieldPath));
                    }
                    break;

                case GffField.CExoLocString:
                    if (field.Value is CExoLocString locString)
                    {
                        var fieldDef = MakeFieldDef(field);
                        matches.AddRange(SearchLocString(locString, fieldDef, pattern, fieldPath));
                    }
                    break;

                case GffField.Struct:
                    if (field.Value is GffStruct childStruct)
                        WalkStruct(childStruct, pattern, fieldPath, matches);
                    break;

                case GffField.List:
                    if (field.Value is GffList list)
                    {
                        for (int i = 0; i < list.Elements.Count; i++)
                            WalkStruct(list.Elements[i], pattern, $"{fieldPath}[{i}]", matches);
                    }
                    break;
            }
        }
    }

    private static FieldDefinition MakeFieldDef(GffField field)
    {
        var fieldType = field.Type switch
        {
            GffField.CExoLocString => SearchFieldType.LocString,
            GffField.CResRef => SearchFieldType.ResRef,
            _ => SearchFieldType.Text
        };

        return new FieldDefinition
        {
            Name = field.Label,
            GffPath = field.Label,
            FieldType = fieldType,
            Category = SearchFieldCategory.Content
        };
    }
}

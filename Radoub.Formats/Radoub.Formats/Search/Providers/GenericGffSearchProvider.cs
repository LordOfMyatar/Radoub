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
        // Phase 3 — not implemented yet
        return operations.Select(op => new ReplaceResult
        {
            Success = false,
            Field = op.Match.Field,
            OldValue = op.Match.FullFieldValue,
            NewValue = op.ReplacementText,
            Skipped = true,
            SkipReason = "Replace not implemented for generic GFF provider"
        }).ToList();
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

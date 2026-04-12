using System.Text.RegularExpressions;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Uti;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for UTI (item blueprint) files.
/// Searches names, descriptions, tag, template, and comment.
/// </summary>
public class UtiSearchProvider : SearchProviderBase, IFileSearchProvider
{
    private static readonly FieldDefinition NameField = new() { Name = "Name", GffPath = "LocalizedName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Item name" };
    private static readonly FieldDefinition DescriptionField = new() { Name = "Description", GffPath = "Description", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Unidentified description" };
    private static readonly FieldDefinition DescIdentifiedField = new() { Name = "Identified Description", GffPath = "DescIdentified", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Identified item description" };
    private static readonly FieldDefinition TagField = new() { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Item tag" };
    private static readonly FieldDefinition TemplateResRefField = new() { Name = "Template ResRef", GffPath = "TemplateResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Blueprint resource reference", IsReplaceable = false };
    private static readonly FieldDefinition CommentField = new() { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" };

    // Variable field
    private static readonly FieldDefinition VarTableField = new() { Name = "Local Variables", GffPath = "VarTable", FieldType = SearchFieldType.Variable, Category = SearchFieldCategory.Variable, Description = "Local variable names and string values" };

    public ushort FileType => ResourceTypes.Uti;

    public IReadOnlyList<string> Extensions => new[] { ".uti" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        var bytes = GffWriter.Write(gffFile);
        var uti = UtiReader.Read(bytes);

        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();

        if (criteria.MatchesField(NameField))
            matches.AddRange(SearchLocString(uti.LocalizedName, NameField, regex, "LocalizedName", criteria.EffectiveTlkResolver));
        if (criteria.MatchesField(DescriptionField))
            matches.AddRange(SearchLocString(uti.Description, DescriptionField, regex, "Description", criteria.EffectiveTlkResolver));
        if (criteria.MatchesField(DescIdentifiedField))
            matches.AddRange(SearchLocString(uti.DescIdentified, DescIdentifiedField, regex, "DescIdentified", criteria.EffectiveTlkResolver));
        if (criteria.MatchesField(TagField))
            matches.AddRange(SearchString(uti.Tag, TagField, regex, "Tag"));
        if (criteria.MatchesField(TemplateResRefField))
            matches.AddRange(SearchString(uti.TemplateResRef, TemplateResRefField, regex, "TemplateResRef"));
        if (criteria.MatchesField(CommentField))
            matches.AddRange(SearchString(uti.Comment, CommentField, regex, "Comment"));

        // Local variables
        if (criteria.MatchesField(VarTableField))
            matches.AddRange(SearchVarTable(gffFile.RootStruct, VarTableField, regex, "VarTable"));

        return matches;
    }

    public IReadOnlyList<ReplaceResult> Replace(GffFile gffFile, IReadOnlyList<ReplaceOperation> operations)
    {
        if (operations.Count == 0) return Array.Empty<ReplaceResult>();

        var sorted = SortReverseOffset(operations);
        var results = new List<ReplaceResult>();

        foreach (var op in sorted)
        {
            var gffPath = op.Match.Field.GffPath;
            var result = op.Match.Field.FieldType switch
            {
                SearchFieldType.LocString => ReplaceLocStringField(gffFile.RootStruct, gffPath, op),
                SearchFieldType.Variable => ReplaceVarTableField(gffFile.RootStruct, op),
                _ => ReplaceStringField(gffFile.RootStruct, gffPath, op)
            };
            results.Add(result);
        }

        return results;
    }
}

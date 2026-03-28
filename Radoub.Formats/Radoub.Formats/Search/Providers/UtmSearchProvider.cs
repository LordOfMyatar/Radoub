using System.Text.RegularExpressions;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Utm;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for UTM (store/merchant) files.
/// Searches name, tag, resref, comment, scripts, and local variables.
/// </summary>
public class UtmSearchProvider : SearchProviderBase, IFileSearchProvider
{
    private static readonly FieldDefinition NameField = new() { Name = "Name", GffPath = "LocName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Store name" };
    private static readonly FieldDefinition TagField = new() { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Store tag" };
    private static readonly FieldDefinition ResRefField = new() { Name = "ResRef", GffPath = "ResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Store resource reference", IsReplaceable = false };
    private static readonly FieldDefinition CommentField = new() { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" };
    private static readonly FieldDefinition OnOpenStoreField = new() { Name = "OnOpenStore", GffPath = "OnOpenStore", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script when store opened" };
    private static readonly FieldDefinition OnStoreClosedField = new() { Name = "OnStoreClosed", GffPath = "OnStoreClosed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script when store closed" };
    private static readonly FieldDefinition VarTableField = new() { Name = "Local Variables", GffPath = "VarTable", FieldType = SearchFieldType.Variable, Category = SearchFieldCategory.Variable, Description = "Local variable names and string values" };

    public ushort FileType => ResourceTypes.Utm;

    public IReadOnlyList<string> Extensions => new[] { ".utm" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        var bytes = GffWriter.Write(gffFile);
        var utm = UtmReader.Read(bytes);

        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();

        if (criteria.MatchesField(NameField))
            matches.AddRange(SearchLocString(utm.LocName, NameField, regex, "LocName"));
        if (criteria.MatchesField(TagField))
            matches.AddRange(SearchString(utm.Tag, TagField, regex, "Tag"));
        if (criteria.MatchesField(ResRefField))
            matches.AddRange(SearchString(utm.ResRef, ResRefField, regex, "ResRef"));
        if (criteria.MatchesField(CommentField))
            matches.AddRange(SearchString(utm.Comment, CommentField, regex, "Comment"));
        if (criteria.MatchesField(OnOpenStoreField))
            matches.AddRange(SearchString(utm.OnOpenStore, OnOpenStoreField, regex, "OnOpenStore"));
        if (criteria.MatchesField(OnStoreClosedField))
            matches.AddRange(SearchString(utm.OnStoreClosed, OnStoreClosedField, regex, "OnStoreClosed"));
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

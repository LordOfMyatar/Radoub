using Radoub.Formats.Are;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for ARE (area) files.
/// Searches name, tag, resref, comments, and event scripts.
/// </summary>
public class AreSearchProvider : SearchProviderBase, IFileSearchProvider
{
    private static readonly FieldDefinition NameField = new() { Name = "Name", GffPath = "Name", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Area name" };
    private static readonly FieldDefinition TagField = new() { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Area scripting tag" };
    private static readonly FieldDefinition ResRefField = new() { Name = "ResRef", GffPath = "ResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Area resource reference", IsReplaceable = false };
    private static readonly FieldDefinition CommentsField = new() { Name = "Comments", GffPath = "Comments", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comments" };
    private static readonly FieldDefinition OnEnterField = new() { Name = "OnEnter", GffPath = "OnEnter", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnEnter event script" };
    private static readonly FieldDefinition OnExitField = new() { Name = "OnExit", GffPath = "OnExit", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnExit event script" };
    private static readonly FieldDefinition OnHeartbeatField = new() { Name = "OnHeartbeat", GffPath = "OnHeartbeat", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnHeartbeat event script" };
    private static readonly FieldDefinition OnUserDefinedField = new() { Name = "OnUserDefined", GffPath = "OnUserDefined", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUserDefined event script" };

    public ushort FileType => ResourceTypes.Are;

    public IReadOnlyList<string> Extensions => new[] { ".are" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        var bytes = GffWriter.Write(gffFile);
        var are = AreReader.Read(bytes);

        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();

        if (criteria.MatchesField(NameField))
            matches.AddRange(SearchLocString(are.Name, NameField, regex, "Name", criteria.EffectiveTlkResolver));
        if (criteria.MatchesField(TagField))
            matches.AddRange(SearchString(are.Tag, TagField, regex, "Tag"));
        if (criteria.MatchesField(ResRefField))
            matches.AddRange(SearchString(are.ResRef, ResRefField, regex, "ResRef"));
        if (criteria.MatchesField(CommentsField))
            matches.AddRange(SearchString(are.Comments, CommentsField, regex, "Comments"));
        if (criteria.MatchesField(OnEnterField))
            matches.AddRange(SearchString(are.OnEnter, OnEnterField, regex, "OnEnter"));
        if (criteria.MatchesField(OnExitField))
            matches.AddRange(SearchString(are.OnExit, OnExitField, regex, "OnExit"));
        if (criteria.MatchesField(OnHeartbeatField))
            matches.AddRange(SearchString(are.OnHeartbeat, OnHeartbeatField, regex, "OnHeartbeat"));
        if (criteria.MatchesField(OnUserDefinedField))
            matches.AddRange(SearchString(are.OnUserDefined, OnUserDefinedField, regex, "OnUserDefined"));

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
                _ => ReplaceStringField(gffFile.RootStruct, gffPath, op)
            };
            results.Add(result);
        }

        return results;
    }
}

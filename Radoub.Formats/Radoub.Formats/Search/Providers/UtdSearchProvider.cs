using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Utd;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for UTD (door) files.
/// Searches name, description, tag, resref, comment, conversation, scripts, and local variables.
/// </summary>
public class UtdSearchProvider : SearchProviderBase, IFileSearchProvider
{
    private static readonly FieldDefinition NameField = new() { Name = "Name", GffPath = "LocName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Door name" };
    private static readonly FieldDefinition DescriptionField = new() { Name = "Description", GffPath = "Description", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Door description" };
    private static readonly FieldDefinition TagField = new() { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Door tag" };
    private static readonly FieldDefinition ResRefField = new() { Name = "ResRef", GffPath = "TemplateResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Door resource reference", IsReplaceable = false };
    private static readonly FieldDefinition CommentField = new() { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" };
    private static readonly FieldDefinition ConversationField = new() { Name = "Conversation", GffPath = "Conversation", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Metadata, Description = "Conversation file reference", IsReplaceable = false };
    private static readonly FieldDefinition LinkedToField = new() { Name = "LinkedTo", GffPath = "LinkedTo", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Metadata, Description = "Area transition target tag" };
    private static readonly FieldDefinition OnClickField = new() { Name = "OnClick", GffPath = "OnClick", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnAreaTransitionClick event script" };
    private static readonly FieldDefinition OnClosedField = new() { Name = "OnClosed", GffPath = "OnClosed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnClosed event script" };
    private static readonly FieldDefinition OnDamagedField = new() { Name = "OnDamaged", GffPath = "OnDamaged", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDamaged event script" };
    private static readonly FieldDefinition OnDeathField = new() { Name = "OnDeath", GffPath = "OnDeath", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDeath event script" };
    private static readonly FieldDefinition OnDisarmField = new() { Name = "OnDisarm", GffPath = "OnDisarm", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDisarm event script" };
    private static readonly FieldDefinition OnFailToOpenField = new() { Name = "OnFailToOpen", GffPath = "OnFailToOpen", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnFailToOpen event script" };
    private static readonly FieldDefinition OnHeartbeatField = new() { Name = "OnHeartbeat", GffPath = "OnHeartbeat", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnHeartbeat event script" };
    private static readonly FieldDefinition OnLockField = new() { Name = "OnLock", GffPath = "OnLock", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnLock event script" };
    private static readonly FieldDefinition OnMeleeAttackedField = new() { Name = "OnMeleeAttacked", GffPath = "OnMeleeAttacked", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnPhysicalAttacked event script" };
    private static readonly FieldDefinition OnOpenField = new() { Name = "OnOpen", GffPath = "OnOpen", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnOpen event script" };
    private static readonly FieldDefinition OnSpellCastAtField = new() { Name = "OnSpellCastAt", GffPath = "OnSpellCastAt", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnSpellCastAt event script" };
    private static readonly FieldDefinition OnTrapTriggeredField = new() { Name = "OnTrapTriggered", GffPath = "OnTrapTriggered", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnTrapTriggered event script" };
    private static readonly FieldDefinition OnUnlockField = new() { Name = "OnUnlock", GffPath = "OnUnlock", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUnlock event script" };
    private static readonly FieldDefinition OnUserDefinedField = new() { Name = "OnUserDefined", GffPath = "OnUserDefined", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUserDefined event script" };
    private static readonly FieldDefinition VarTableField = new() { Name = "Local Variables", GffPath = "VarTable", FieldType = SearchFieldType.Variable, Category = SearchFieldCategory.Variable, Description = "Local variable names and string values" };

    public ushort FileType => ResourceTypes.Utd;

    public IReadOnlyList<string> Extensions => new[] { ".utd" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        var bytes = GffWriter.Write(gffFile);
        var utd = UtdReader.Read(bytes);

        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();

        if (criteria.MatchesField(NameField))
            matches.AddRange(SearchLocString(utd.LocName, NameField, regex, "LocName"));
        if (criteria.MatchesField(DescriptionField))
            matches.AddRange(SearchLocString(utd.Description, DescriptionField, regex, "Description"));
        if (criteria.MatchesField(TagField))
            matches.AddRange(SearchString(utd.Tag, TagField, regex, "Tag"));
        if (criteria.MatchesField(ResRefField))
            matches.AddRange(SearchString(utd.TemplateResRef, ResRefField, regex, "TemplateResRef"));
        if (criteria.MatchesField(CommentField))
            matches.AddRange(SearchString(utd.Comment, CommentField, regex, "Comment"));
        if (criteria.MatchesField(ConversationField))
            matches.AddRange(SearchString(utd.Conversation, ConversationField, regex, "Conversation"));
        if (criteria.MatchesField(LinkedToField))
            matches.AddRange(SearchString(utd.LinkedTo, LinkedToField, regex, "LinkedTo"));
        if (criteria.MatchesField(OnClickField))
            matches.AddRange(SearchString(utd.OnClick, OnClickField, regex, "OnClick"));
        if (criteria.MatchesField(OnClosedField))
            matches.AddRange(SearchString(utd.OnClosed, OnClosedField, regex, "OnClosed"));
        if (criteria.MatchesField(OnDamagedField))
            matches.AddRange(SearchString(utd.OnDamaged, OnDamagedField, regex, "OnDamaged"));
        if (criteria.MatchesField(OnDeathField))
            matches.AddRange(SearchString(utd.OnDeath, OnDeathField, regex, "OnDeath"));
        if (criteria.MatchesField(OnDisarmField))
            matches.AddRange(SearchString(utd.OnDisarm, OnDisarmField, regex, "OnDisarm"));
        if (criteria.MatchesField(OnFailToOpenField))
            matches.AddRange(SearchString(utd.OnFailToOpen, OnFailToOpenField, regex, "OnFailToOpen"));
        if (criteria.MatchesField(OnHeartbeatField))
            matches.AddRange(SearchString(utd.OnHeartbeat, OnHeartbeatField, regex, "OnHeartbeat"));
        if (criteria.MatchesField(OnLockField))
            matches.AddRange(SearchString(utd.OnLock, OnLockField, regex, "OnLock"));
        if (criteria.MatchesField(OnMeleeAttackedField))
            matches.AddRange(SearchString(utd.OnMeleeAttacked, OnMeleeAttackedField, regex, "OnMeleeAttacked"));
        if (criteria.MatchesField(OnOpenField))
            matches.AddRange(SearchString(utd.OnOpen, OnOpenField, regex, "OnOpen"));
        if (criteria.MatchesField(OnSpellCastAtField))
            matches.AddRange(SearchString(utd.OnSpellCastAt, OnSpellCastAtField, regex, "OnSpellCastAt"));
        if (criteria.MatchesField(OnTrapTriggeredField))
            matches.AddRange(SearchString(utd.OnTrapTriggered, OnTrapTriggeredField, regex, "OnTrapTriggered"));
        if (criteria.MatchesField(OnUnlockField))
            matches.AddRange(SearchString(utd.OnUnlock, OnUnlockField, regex, "OnUnlock"));
        if (criteria.MatchesField(OnUserDefinedField))
            matches.AddRange(SearchString(utd.OnUserDefined, OnUserDefinedField, regex, "OnUserDefined"));
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
                _ => ReplaceStringField(gffFile.RootStruct, gffPath, op)
            };
            results.Add(result);
        }

        return results;
    }
}

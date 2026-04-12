using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Utp;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for UTP (placeable) files.
/// Searches name, description, tag, resref, comment, conversation, scripts, and local variables.
/// </summary>
public class UtpSearchProvider : SearchProviderBase, IFileSearchProvider
{
    private static readonly FieldDefinition NameField = new() { Name = "Name", GffPath = "LocName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Placeable name" };
    private static readonly FieldDefinition DescriptionField = new() { Name = "Description", GffPath = "Description", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Placeable description" };
    private static readonly FieldDefinition TagField = new() { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Placeable tag" };
    private static readonly FieldDefinition ResRefField = new() { Name = "ResRef", GffPath = "TemplateResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Placeable resource reference", IsReplaceable = false };
    private static readonly FieldDefinition CommentField = new() { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" };
    private static readonly FieldDefinition ConversationField = new() { Name = "Conversation", GffPath = "Conversation", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Metadata, Description = "Conversation file reference", IsReplaceable = false };
    private static readonly FieldDefinition OnClosedField = new() { Name = "OnClosed", GffPath = "OnClosed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnClosed event script" };
    private static readonly FieldDefinition OnDamagedField = new() { Name = "OnDamaged", GffPath = "OnDamaged", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDamaged event script" };
    private static readonly FieldDefinition OnDeathField = new() { Name = "OnDeath", GffPath = "OnDeath", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDeath event script" };
    private static readonly FieldDefinition OnDisarmField = new() { Name = "OnDisarm", GffPath = "OnDisarm", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDisarm event script" };
    private static readonly FieldDefinition OnHeartbeatField = new() { Name = "OnHeartbeat", GffPath = "OnHeartbeat", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnHeartbeat event script" };
    private static readonly FieldDefinition OnInvDisturbedField = new() { Name = "OnInvDisturbed", GffPath = "OnInvDisturbed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnInventoryDisturbed event script" };
    private static readonly FieldDefinition OnLockField = new() { Name = "OnLock", GffPath = "OnLock", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnLock event script" };
    private static readonly FieldDefinition OnMeleeAttackedField = new() { Name = "OnMeleeAttacked", GffPath = "OnMeleeAttacked", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnPhysicalAttacked event script" };
    private static readonly FieldDefinition OnOpenField = new() { Name = "OnOpen", GffPath = "OnOpen", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnOpen event script" };
    private static readonly FieldDefinition OnSpellCastAtField = new() { Name = "OnSpellCastAt", GffPath = "OnSpellCastAt", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnSpellCastAt event script" };
    private static readonly FieldDefinition OnTrapTriggeredField = new() { Name = "OnTrapTriggered", GffPath = "OnTrapTriggered", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnTrapTriggered event script" };
    private static readonly FieldDefinition OnUnlockField = new() { Name = "OnUnlock", GffPath = "OnUnlock", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUnlock event script" };
    private static readonly FieldDefinition OnUserDefinedField = new() { Name = "OnUserDefined", GffPath = "OnUserDefined", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUserDefined event script" };
    private static readonly FieldDefinition OnUsedField = new() { Name = "OnUsed", GffPath = "OnUsed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUsed event script" };
    private static readonly FieldDefinition VarTableField = new() { Name = "Local Variables", GffPath = "VarTable", FieldType = SearchFieldType.Variable, Category = SearchFieldCategory.Variable, Description = "Local variable names and string values" };
    private static readonly FieldDefinition InventoryResField = new() { Name = "InventoryRes", GffPath = "InventoryRes", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Inventory item ResRef", IsReplaceable = false };

    public ushort FileType => ResourceTypes.Utp;

    public IReadOnlyList<string> Extensions => new[] { ".utp" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        var bytes = GffWriter.Write(gffFile);
        var utp = UtpReader.Read(bytes);

        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();

        if (criteria.MatchesField(NameField))
            matches.AddRange(SearchLocString(utp.LocName, NameField, regex, "LocName", criteria.EffectiveTlkResolver));
        if (criteria.MatchesField(DescriptionField))
            matches.AddRange(SearchLocString(utp.Description, DescriptionField, regex, "Description", criteria.EffectiveTlkResolver));
        if (criteria.MatchesField(TagField))
            matches.AddRange(SearchString(utp.Tag, TagField, regex, "Tag"));
        if (criteria.MatchesField(ResRefField))
            matches.AddRange(SearchString(utp.TemplateResRef, ResRefField, regex, "TemplateResRef"));
        if (criteria.MatchesField(CommentField))
            matches.AddRange(SearchString(utp.Comment, CommentField, regex, "Comment"));
        if (criteria.MatchesField(ConversationField))
            matches.AddRange(SearchString(utp.Conversation, ConversationField, regex, "Conversation"));
        if (criteria.MatchesField(OnClosedField))
            matches.AddRange(SearchString(utp.OnClosed, OnClosedField, regex, "OnClosed"));
        if (criteria.MatchesField(OnDamagedField))
            matches.AddRange(SearchString(utp.OnDamaged, OnDamagedField, regex, "OnDamaged"));
        if (criteria.MatchesField(OnDeathField))
            matches.AddRange(SearchString(utp.OnDeath, OnDeathField, regex, "OnDeath"));
        if (criteria.MatchesField(OnDisarmField))
            matches.AddRange(SearchString(utp.OnDisarm, OnDisarmField, regex, "OnDisarm"));
        if (criteria.MatchesField(OnHeartbeatField))
            matches.AddRange(SearchString(utp.OnHeartbeat, OnHeartbeatField, regex, "OnHeartbeat"));
        if (criteria.MatchesField(OnInvDisturbedField))
            matches.AddRange(SearchString(utp.OnInvDisturbed, OnInvDisturbedField, regex, "OnInvDisturbed"));
        if (criteria.MatchesField(OnLockField))
            matches.AddRange(SearchString(utp.OnLock, OnLockField, regex, "OnLock"));
        if (criteria.MatchesField(OnMeleeAttackedField))
            matches.AddRange(SearchString(utp.OnMeleeAttacked, OnMeleeAttackedField, regex, "OnMeleeAttacked"));
        if (criteria.MatchesField(OnOpenField))
            matches.AddRange(SearchString(utp.OnOpen, OnOpenField, regex, "OnOpen"));
        if (criteria.MatchesField(OnSpellCastAtField))
            matches.AddRange(SearchString(utp.OnSpellCastAt, OnSpellCastAtField, regex, "OnSpellCastAt"));
        if (criteria.MatchesField(OnTrapTriggeredField))
            matches.AddRange(SearchString(utp.OnTrapTriggered, OnTrapTriggeredField, regex, "OnTrapTriggered"));
        if (criteria.MatchesField(OnUnlockField))
            matches.AddRange(SearchString(utp.OnUnlock, OnUnlockField, regex, "OnUnlock"));
        if (criteria.MatchesField(OnUserDefinedField))
            matches.AddRange(SearchString(utp.OnUserDefined, OnUserDefinedField, regex, "OnUserDefined"));
        if (criteria.MatchesField(OnUsedField))
            matches.AddRange(SearchString(utp.OnUsed, OnUsedField, regex, "OnUsed"));
        if (criteria.MatchesField(VarTableField))
            matches.AddRange(SearchVarTable(gffFile.RootStruct, VarTableField, regex, "VarTable"));

        // Inventory items (#1951)
        if (criteria.MatchesField(InventoryResField))
        {
            for (int i = 0; i < utp.ItemList.Count; i++)
            {
                var location = $"Inventory > Item {i} > InventoryRes";
                matches.AddRange(SearchString(utp.ItemList[i].InventoryRes, InventoryResField, regex, location));
            }
        }

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

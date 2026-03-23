using System.Text.RegularExpressions;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Utc;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for UTC (creature) and BIC (character) files.
/// Searches names, description, tag, scripts, and local variables.
/// </summary>
public class UtcSearchProvider : SearchProviderBase, IFileSearchProvider
{
    // Content fields (LocString)
    private static readonly FieldDefinition FirstNameField = new() { Name = "First Name", GffPath = "FirstName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Creature first name" };
    private static readonly FieldDefinition LastNameField = new() { Name = "Last Name", GffPath = "LastName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Creature last name" };
    private static readonly FieldDefinition DescriptionField = new() { Name = "Description", GffPath = "Description", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Creature description" };

    // Identity fields
    private static readonly FieldDefinition TagField = new() { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Creature tag" };
    private static readonly FieldDefinition TemplateResRefField = new() { Name = "Template ResRef", GffPath = "TemplateResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Blueprint resource reference", IsReplaceable = false };
    private static readonly FieldDefinition SubraceField = new() { Name = "Subrace", GffPath = "Subrace", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Identity, Description = "Creature subrace" };
    private static readonly FieldDefinition DeityField = new() { Name = "Deity", GffPath = "Deity", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Identity, Description = "Creature deity" };

    // Metadata fields
    private static readonly FieldDefinition CommentField = new() { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" };
    private static readonly FieldDefinition ConversationField = new() { Name = "Conversation", GffPath = "Conversation", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Metadata, Description = "Default conversation file", IsReplaceable = false };

    // Script fields
    private static readonly FieldDefinition ScriptAttackedField = new() { Name = "ScriptAttacked", GffPath = "ScriptAttacked", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnPhysicalAttacked event script" };
    private static readonly FieldDefinition ScriptDamagedField = new() { Name = "ScriptDamaged", GffPath = "ScriptDamaged", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDamaged event script" };
    private static readonly FieldDefinition ScriptDeathField = new() { Name = "ScriptDeath", GffPath = "ScriptDeath", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDeath event script" };
    private static readonly FieldDefinition ScriptDialogueField = new() { Name = "ScriptDialogue", GffPath = "ScriptDialogue", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnConversation event script" };
    private static readonly FieldDefinition ScriptDisturbedField = new() { Name = "ScriptDisturbed", GffPath = "ScriptDisturbed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDisturbed event script" };
    private static readonly FieldDefinition ScriptEndRoundField = new() { Name = "ScriptEndRound", GffPath = "ScriptEndRound", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnEndCombatRound event script" };
    private static readonly FieldDefinition ScriptHeartbeatField = new() { Name = "ScriptHeartbeat", GffPath = "ScriptHeartbeat", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnHeartbeat event script" };
    private static readonly FieldDefinition ScriptOnBlockedField = new() { Name = "ScriptOnBlocked", GffPath = "ScriptOnBlocked", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnBlocked event script" };
    private static readonly FieldDefinition ScriptOnNoticeField = new() { Name = "ScriptOnNotice", GffPath = "ScriptOnNotice", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnPerception event script" };
    private static readonly FieldDefinition ScriptRestedField = new() { Name = "ScriptRested", GffPath = "ScriptRested", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnRested event script" };
    private static readonly FieldDefinition ScriptSpawnField = new() { Name = "ScriptSpawn", GffPath = "ScriptSpawn", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnSpawn event script" };
    private static readonly FieldDefinition ScriptSpellAtField = new() { Name = "ScriptSpellAt", GffPath = "ScriptSpellAt", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnSpellCastAt event script" };
    private static readonly FieldDefinition ScriptUserDefineField = new() { Name = "ScriptUserDefine", GffPath = "ScriptUserDefine", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUserDefined event script" };

    // Variable field
    private static readonly FieldDefinition VarTableField = new() { Name = "Local Variables", GffPath = "VarTable", FieldType = SearchFieldType.Variable, Category = SearchFieldCategory.Variable, Description = "Local variable names and string values" };

    public ushort FileType => ResourceTypes.Utc;

    public IReadOnlyList<string> Extensions => new[] { ".utc", ".bic" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        var bytes = GffWriter.Write(gffFile);
        var utc = UtcReader.Read(bytes);

        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();

        // Content (LocString)
        if (criteria.MatchesField(FirstNameField))
            matches.AddRange(SearchLocString(utc.FirstName, FirstNameField, regex, "FirstName"));
        if (criteria.MatchesField(LastNameField))
            matches.AddRange(SearchLocString(utc.LastName, LastNameField, regex, "LastName"));
        if (criteria.MatchesField(DescriptionField))
            matches.AddRange(SearchLocString(utc.Description, DescriptionField, regex, "Description"));

        // Identity
        if (criteria.MatchesField(TagField))
            matches.AddRange(SearchString(utc.Tag, TagField, regex, "Tag"));
        if (criteria.MatchesField(TemplateResRefField))
            matches.AddRange(SearchString(utc.TemplateResRef, TemplateResRefField, regex, "TemplateResRef"));
        if (criteria.MatchesField(SubraceField))
            matches.AddRange(SearchString(utc.Subrace, SubraceField, regex, "Subrace"));
        if (criteria.MatchesField(DeityField))
            matches.AddRange(SearchString(utc.Deity, DeityField, regex, "Deity"));

        // Metadata
        if (criteria.MatchesField(CommentField))
            matches.AddRange(SearchString(utc.Comment, CommentField, regex, "Comment"));
        if (criteria.MatchesField(ConversationField))
            matches.AddRange(SearchString(utc.Conversation, ConversationField, regex, "Conversation"));

        // Scripts
        if (criteria.MatchesField(ScriptAttackedField))
            matches.AddRange(SearchString(utc.ScriptAttacked, ScriptAttackedField, regex, "ScriptAttacked"));
        if (criteria.MatchesField(ScriptDamagedField))
            matches.AddRange(SearchString(utc.ScriptDamaged, ScriptDamagedField, regex, "ScriptDamaged"));
        if (criteria.MatchesField(ScriptDeathField))
            matches.AddRange(SearchString(utc.ScriptDeath, ScriptDeathField, regex, "ScriptDeath"));
        if (criteria.MatchesField(ScriptDialogueField))
            matches.AddRange(SearchString(utc.ScriptDialogue, ScriptDialogueField, regex, "ScriptDialogue"));
        if (criteria.MatchesField(ScriptDisturbedField))
            matches.AddRange(SearchString(utc.ScriptDisturbed, ScriptDisturbedField, regex, "ScriptDisturbed"));
        if (criteria.MatchesField(ScriptEndRoundField))
            matches.AddRange(SearchString(utc.ScriptEndRound, ScriptEndRoundField, regex, "ScriptEndRound"));
        if (criteria.MatchesField(ScriptHeartbeatField))
            matches.AddRange(SearchString(utc.ScriptHeartbeat, ScriptHeartbeatField, regex, "ScriptHeartbeat"));
        if (criteria.MatchesField(ScriptOnBlockedField))
            matches.AddRange(SearchString(utc.ScriptOnBlocked, ScriptOnBlockedField, regex, "ScriptOnBlocked"));
        if (criteria.MatchesField(ScriptOnNoticeField))
            matches.AddRange(SearchString(utc.ScriptOnNotice, ScriptOnNoticeField, regex, "ScriptOnNotice"));
        if (criteria.MatchesField(ScriptRestedField))
            matches.AddRange(SearchString(utc.ScriptRested, ScriptRestedField, regex, "ScriptRested"));
        if (criteria.MatchesField(ScriptSpawnField))
            matches.AddRange(SearchString(utc.ScriptSpawn, ScriptSpawnField, regex, "ScriptSpawn"));
        if (criteria.MatchesField(ScriptSpellAtField))
            matches.AddRange(SearchString(utc.ScriptSpellAt, ScriptSpellAtField, regex, "ScriptSpellAt"));
        if (criteria.MatchesField(ScriptUserDefineField))
            matches.AddRange(SearchString(utc.ScriptUserDefine, ScriptUserDefineField, regex, "ScriptUserDefine"));

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
                _ => ReplaceStringField(gffFile.RootStruct, gffPath, op)
            };
            results.Add(result);
        }

        return results;
    }
}

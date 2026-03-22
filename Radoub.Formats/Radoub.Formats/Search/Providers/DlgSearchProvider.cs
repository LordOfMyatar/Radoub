using System.Text.RegularExpressions;
using Radoub.Formats.Common;
using Radoub.Formats.Dlg;
using Radoub.Formats.Gff;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for DLG (dialog) files.
/// Walks entries, replies, links, and starting list with tree-aware location.
/// </summary>
public class DlgSearchProvider : SearchProviderBase, IFileSearchProvider
{
    // Field definitions (match FieldRegistrations)
    private static readonly FieldDefinition TextField = new() { Name = "Text", GffPath = "Text", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Dialog text (entries and replies)" };
    private static readonly FieldDefinition SpeakerField = new() { Name = "Speaker", GffPath = "Speaker", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Identity, Description = "NPC speaker tag" };
    private static readonly FieldDefinition ActionScriptField = new() { Name = "Action Script", GffPath = "Script", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script executed when node is reached" };
    private static readonly FieldDefinition ActionParamsField = new() { Name = "Action Params", GffPath = "ActionParams", FieldType = SearchFieldType.ScriptParam, Category = SearchFieldCategory.Script, Description = "Parameters passed to action script" };
    private static readonly FieldDefinition ConditionScriptField = new() { Name = "Condition Script", GffPath = "Active", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Condition script on links" };
    private static readonly FieldDefinition ConditionParamsField = new() { Name = "Condition Params", GffPath = "ConditionParams", FieldType = SearchFieldType.ScriptParam, Category = SearchFieldCategory.Script, Description = "Parameters passed to condition script" };
    private static readonly FieldDefinition EndConversationField = new() { Name = "End Conversation", GffPath = "EndConversation", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script on normal conversation end" };
    private static readonly FieldDefinition EndConverAbortField = new() { Name = "End Conversation Abort", GffPath = "EndConverAbort", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script on aborted conversation" };
    private static readonly FieldDefinition SoundField = new() { Name = "Sound", GffPath = "Sound", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Metadata, Description = "Sound file reference" };
    private static readonly FieldDefinition QuestField = new() { Name = "Quest", GffPath = "Quest", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Metadata, Description = "Quest tag for journal updates" };
    private static readonly FieldDefinition CommentField = new() { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment (not shown in-game)" };

    public ushort FileType => ResourceTypes.Dlg;

    public IReadOnlyList<string> Extensions => new[] { ".dlg" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        // Round-trip through binary to get a typed DlgFile
        var bytes = GffWriter.Write(gffFile);
        var dlg = DlgReader.Read(bytes);

        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();

        // Root-level scripts
        if (criteria.MatchesField(EndConversationField))
            matches.AddRange(SearchString(dlg.EndConversation, EndConversationField, regex, MakeRootLocation("EndConversation")));
        if (criteria.MatchesField(EndConverAbortField))
            matches.AddRange(SearchString(dlg.EndConverAbort, EndConverAbortField, regex, MakeRootLocation("EndConverAbort")));

        // Entries
        for (int i = 0; i < dlg.Entries.Count; i++)
            SearchEntry(dlg.Entries[i], i, criteria, regex, matches);

        // Replies
        for (int i = 0; i < dlg.Replies.Count; i++)
            SearchReply(dlg.Replies[i], i, criteria, regex, matches);

        // Starting list
        for (int i = 0; i < dlg.StartingList.Count; i++)
            SearchLink(dlg.StartingList[i], DlgNodeType.StartingLink, null, i, criteria, regex, matches);

        return matches;
    }

    public IReadOnlyList<ReplaceResult> Replace(GffFile gffFile, IReadOnlyList<ReplaceOperation> operations)
    {
        if (operations.Count == 0) return Array.Empty<ReplaceResult>();

        var sorted = SortReverseOffset(operations);
        var results = new List<ReplaceResult>();

        foreach (var op in sorted)
        {
            if (op.Match.Location is not DlgMatchLocation loc)
            {
                results.Add(new ReplaceResult
                {
                    Success = false, Field = op.Match.Field,
                    OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                    Skipped = true, SkipReason = "Missing DLG location info"
                });
                continue;
            }

            var targetStruct = FindTargetStruct(gffFile.RootStruct, loc);
            if (targetStruct == null)
            {
                results.Add(new ReplaceResult
                {
                    Success = false, Field = op.Match.Field,
                    OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                    Skipped = true, SkipReason = $"Could not locate target struct: {loc.DisplayPath}"
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
    /// Navigate the GFF tree to find the struct that contains the field to replace.
    /// </summary>
    private static GffStruct? FindTargetStruct(GffStruct root, DlgMatchLocation loc)
    {
        // Root-level fields (EndConversation, EndConverAbort) — NodeIndex is null
        if (loc.NodeIndex == null && !loc.IsOnLink)
            return root;

        // Get the appropriate list (EntryList or ReplyList or StartingList)
        if (loc.IsOnLink)
            return FindLinkStruct(root, loc);

        var listLabel = loc.NodeType == DlgNodeType.Entry ? "EntryList" : "ReplyList";
        var listField = root.GetField(listLabel);
        if (listField?.Value is not GffList list) return null;

        var nodeIndex = loc.NodeIndex!.Value;
        if (nodeIndex < 0 || nodeIndex >= list.Elements.Count) return null;

        return list.Elements[nodeIndex];
    }

    /// <summary>
    /// Navigate to a link struct within an entry's RepliesList, a reply's EntriesList,
    /// or the root StartingList.
    /// </summary>
    private static GffStruct? FindLinkStruct(GffStruct root, DlgMatchLocation loc)
    {
        if (loc.LinkIndex == null) return null;

        GffStruct parentStruct;

        if (loc.NodeType == DlgNodeType.StartingLink)
        {
            // Links on StartingList are at root level
            parentStruct = root;
        }
        else
        {
            // Links on Entry or Reply — find the parent node first
            var listLabel = loc.NodeType == DlgNodeType.Entry ? "EntryList" : "ReplyList";
            var listField = root.GetField(listLabel);
            if (listField?.Value is not GffList list) return null;

            if (loc.NodeIndex == null || loc.NodeIndex.Value < 0 || loc.NodeIndex.Value >= list.Elements.Count)
                return null;

            parentStruct = list.Elements[loc.NodeIndex.Value];
        }

        // Now find the link list within the parent
        var linkListLabel = loc.NodeType == DlgNodeType.Entry ? "RepliesList" :
                            loc.NodeType == DlgNodeType.Reply ? "EntriesList" : "StartingList";
        var linkListField = parentStruct.GetField(linkListLabel);
        if (linkListField?.Value is not GffList linkList) return null;

        if (loc.LinkIndex.Value < 0 || loc.LinkIndex.Value >= linkList.Elements.Count) return null;

        return linkList.Elements[loc.LinkIndex.Value];
    }

    private void SearchEntry(DlgEntry entry, int index, SearchCriteria criteria, Regex regex, List<SearchMatch> matches)
    {
        var loc = new DlgMatchLocation { NodeType = DlgNodeType.Entry, NodeIndex = index, DisplayPath = $"Entry #{index}" };

        if (criteria.MatchesField(TextField))
            matches.AddRange(SearchLocString(entry.Text, TextField, regex, loc));
        if (criteria.MatchesField(SpeakerField))
            matches.AddRange(SearchString(entry.Speaker, SpeakerField, regex, loc));
        if (criteria.MatchesField(ActionScriptField))
            matches.AddRange(SearchString(entry.Script, ActionScriptField, regex, loc));
        if (criteria.MatchesField(ActionParamsField) && entry.ActionParams != null)
            matches.AddRange(SearchParams(entry.ActionParams.Select(p => (p.Key, p.Value)), ActionParamsField, regex, loc));
        if (criteria.MatchesField(SoundField))
            matches.AddRange(SearchString(entry.Sound, SoundField, regex, loc));
        if (criteria.MatchesField(QuestField))
            matches.AddRange(SearchString(entry.Quest, QuestField, regex, loc));
        if (criteria.MatchesField(CommentField))
            matches.AddRange(SearchString(entry.Comment, CommentField, regex, loc));

        // Search links on this entry
        if (entry.RepliesList != null)
        {
            for (int i = 0; i < entry.RepliesList.Count; i++)
                SearchLink(entry.RepliesList[i], DlgNodeType.Entry, index, i, criteria, regex, matches);
        }
    }

    private void SearchReply(DlgReply reply, int index, SearchCriteria criteria, Regex regex, List<SearchMatch> matches)
    {
        var loc = new DlgMatchLocation { NodeType = DlgNodeType.Reply, NodeIndex = index, DisplayPath = $"Reply #{index}" };

        if (criteria.MatchesField(TextField))
            matches.AddRange(SearchLocString(reply.Text, TextField, regex, loc));
        if (criteria.MatchesField(ActionScriptField))
            matches.AddRange(SearchString(reply.Script, ActionScriptField, regex, loc));
        if (criteria.MatchesField(ActionParamsField) && reply.ActionParams != null)
            matches.AddRange(SearchParams(reply.ActionParams.Select(p => (p.Key, p.Value)), ActionParamsField, regex, loc));
        if (criteria.MatchesField(SoundField))
            matches.AddRange(SearchString(reply.Sound, SoundField, regex, loc));
        if (criteria.MatchesField(QuestField))
            matches.AddRange(SearchString(reply.Quest, QuestField, regex, loc));
        if (criteria.MatchesField(CommentField))
            matches.AddRange(SearchString(reply.Comment, CommentField, regex, loc));

        // Search links on this reply
        if (reply.EntriesList != null)
        {
            for (int i = 0; i < reply.EntriesList.Count; i++)
                SearchLink(reply.EntriesList[i], DlgNodeType.Reply, index, i, criteria, regex, matches);
        }
    }

    private void SearchLink(DlgLink link, DlgNodeType parentType, int? parentIndex, int linkIndex,
        SearchCriteria criteria, Regex regex, List<SearchMatch> matches)
    {
        var displayPath = parentType == DlgNodeType.StartingLink
            ? $"StartingList \u2192 Link #{linkIndex}"
            : $"{parentType} #{parentIndex} \u2192 Link #{linkIndex}";

        var loc = new DlgMatchLocation
        {
            NodeType = parentType == DlgNodeType.StartingLink ? DlgNodeType.StartingLink : parentType,
            NodeIndex = parentIndex,
            IsOnLink = true,
            LinkIndex = linkIndex,
            DisplayPath = displayPath
        };

        if (criteria.MatchesField(ConditionScriptField))
            matches.AddRange(SearchString(link.Active, ConditionScriptField, regex, loc));
        if (criteria.MatchesField(ConditionParamsField) && link.ConditionParams != null)
            matches.AddRange(SearchParams(link.ConditionParams.Select(p => (p.Key, p.Value)), ConditionParamsField, regex, loc));
    }

    private static DlgMatchLocation MakeRootLocation(string fieldName)
    {
        return new DlgMatchLocation
        {
            NodeType = DlgNodeType.Entry,
            NodeIndex = null,
            DisplayPath = $"Root \u2192 {fieldName}"
        };
    }
}

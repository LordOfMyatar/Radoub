using Radoub.Formats.Gff;

namespace Radoub.Formats.Dlg;

/// <summary>
/// Reads DLG (Dialog) files from binary format.
/// DLG files are GFF-based with file type "DLG ".
/// Reference: BioWare Aurora Dialog Format specification, neverwinter.nim
/// </summary>
public static class DlgReader
{
    /// <summary>
    /// Read a DLG file from a file path.
    /// </summary>
    public static DlgFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read a DLG file from a stream.
    /// </summary>
    public static DlgFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read a DLG file from a byte buffer.
    /// </summary>
    public static DlgFile Read(byte[] buffer)
    {
        // Parse as GFF first
        var gff = GffReader.Read(buffer);

        // Validate file type
        if (gff.FileType.TrimEnd() != "DLG")
        {
            throw new InvalidDataException(
                $"Invalid DLG file type: '{gff.FileType}' (expected 'DLG ')");
        }

        return ParseDlgFile(gff);
    }

    private static DlgFile ParseDlgFile(GffFile gff)
    {
        var root = gff.RootStruct;

        var dlg = new DlgFile
        {
            FileType = gff.FileType,
            FileVersion = gff.FileVersion,

            // Root level fields
            DelayEntry = root.GetFieldValue<uint>("DelayEntry", 0),
            DelayReply = root.GetFieldValue<uint>("DelayReply", 0),
            NumWords = root.GetFieldValue<uint>("NumWords", 0),
            EndConversation = root.GetFieldValue<string>("EndConversation", string.Empty),
            EndConverAbort = root.GetFieldValue<string>("EndConverAbort", string.Empty),
            PreventZoomIn = root.GetFieldValue<byte>("PreventZoomIn", 0) != 0
        };

        // Parse EntryList
        var entriesField = root.GetField("EntryList");
        if (entriesField?.Value is GffList entriesList)
        {
            foreach (var entryStruct in entriesList.Elements)
            {
                dlg.Entries.Add(ParseEntry(entryStruct));
            }
        }

        // Parse ReplyList
        var repliesField = root.GetField("ReplyList");
        if (repliesField?.Value is GffList repliesList)
        {
            foreach (var replyStruct in repliesList.Elements)
            {
                dlg.Replies.Add(ParseReply(replyStruct));
            }
        }

        // Parse StartingList
        var startingField = root.GetField("StartingList");
        if (startingField?.Value is GffList startingList)
        {
            foreach (var linkStruct in startingList.Elements)
            {
                dlg.StartingList.Add(ParseLink(linkStruct));
            }
        }

        return dlg;
    }

    private static DlgEntry ParseEntry(GffStruct entryStruct)
    {
        var entry = new DlgEntry
        {
            Speaker = entryStruct.GetFieldValue<string>("Speaker", string.Empty),
            Animation = entryStruct.GetFieldValue<uint>("Animation", 0),
            AnimLoop = entryStruct.GetFieldValue<byte>("AnimLoop", 0) != 0,
            Script = entryStruct.GetFieldValue<string>("Script", string.Empty),
            Delay = entryStruct.GetFieldValue<uint>("Delay", 0xFFFFFFFF),
            Comment = entryStruct.GetFieldValue<string>("Comment", string.Empty),
            Sound = entryStruct.GetFieldValue<string>("Sound", string.Empty),
            Quest = entryStruct.GetFieldValue<string>("Quest", string.Empty),
            QuestEntry = entryStruct.GetFieldValue<uint>("QuestEntry", 0xFFFFFFFF)
        };

        // Parse localized text
        entry.Text = ParseLocString(entryStruct, "Text") ?? new CExoLocString();

        // Parse ActionParams
        var actionParamsField = entryStruct.GetField("ActionParams");
        if (actionParamsField?.Value is GffList actionParamsList)
        {
            foreach (var paramStruct in actionParamsList.Elements)
            {
                entry.ActionParams.Add(ParseParam(paramStruct));
            }
        }

        // Parse RepliesList (links to replies)
        var repliesListField = entryStruct.GetField("RepliesList");
        if (repliesListField?.Value is GffList repliesList)
        {
            foreach (var linkStruct in repliesList.Elements)
            {
                entry.RepliesList.Add(ParseLink(linkStruct));
            }
        }

        return entry;
    }

    private static DlgReply ParseReply(GffStruct replyStruct)
    {
        var reply = new DlgReply
        {
            Animation = replyStruct.GetFieldValue<uint>("Animation", 0),
            AnimLoop = replyStruct.GetFieldValue<byte>("AnimLoop", 0) != 0,
            Script = replyStruct.GetFieldValue<string>("Script", string.Empty),
            Delay = replyStruct.GetFieldValue<uint>("Delay", 0xFFFFFFFF),
            Comment = replyStruct.GetFieldValue<string>("Comment", string.Empty),
            Sound = replyStruct.GetFieldValue<string>("Sound", string.Empty),
            Quest = replyStruct.GetFieldValue<string>("Quest", string.Empty),
            QuestEntry = replyStruct.GetFieldValue<uint>("QuestEntry", 0xFFFFFFFF)
        };

        // Parse localized text
        reply.Text = ParseLocString(replyStruct, "Text") ?? new CExoLocString();

        // Parse ActionParams
        var actionParamsField = replyStruct.GetField("ActionParams");
        if (actionParamsField?.Value is GffList actionParamsList)
        {
            foreach (var paramStruct in actionParamsList.Elements)
            {
                reply.ActionParams.Add(ParseParam(paramStruct));
            }
        }

        // Parse EntriesList (links to entries)
        var entriesListField = replyStruct.GetField("EntriesList");
        if (entriesListField?.Value is GffList entriesList)
        {
            foreach (var linkStruct in entriesList.Elements)
            {
                reply.EntriesList.Add(ParseLink(linkStruct));
            }
        }

        return reply;
    }

    private static DlgLink ParseLink(GffStruct linkStruct)
    {
        var link = new DlgLink
        {
            Index = linkStruct.GetFieldValue<uint>("Index", 0),
            Active = linkStruct.GetFieldValue<string>("Active", string.Empty),
            IsChild = linkStruct.GetFieldValue<byte>("IsChild", 0) != 0,
            LinkComment = linkStruct.GetFieldValue<string>("LinkComment", string.Empty)
        };

        // Parse ConditionParams
        var conditionParamsField = linkStruct.GetField("ConditionParams");
        if (conditionParamsField?.Value is GffList conditionParamsList)
        {
            foreach (var paramStruct in conditionParamsList.Elements)
            {
                link.ConditionParams.Add(ParseParam(paramStruct));
            }
        }

        return link;
    }

    private static DlgParam ParseParam(GffStruct paramStruct)
    {
        return new DlgParam
        {
            Key = paramStruct.GetFieldValue<string>("Key", string.Empty),
            Value = paramStruct.GetFieldValue<string>("Value", string.Empty)
        };
    }

    private static CExoLocString? ParseLocString(GffStruct parent, string fieldName)
    {
        var field = parent.GetField(fieldName);
        if (field == null || !field.IsCExoLocString || field.Value is not CExoLocString locString)
            return null;

        return locString;
    }
}

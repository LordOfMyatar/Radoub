using Radoub.Formats.Gff;
using static Radoub.Formats.Gff.GffFieldBuilder;

namespace Radoub.Formats.Dlg;

/// <summary>
/// Writes DLG (Dialog) files to binary format.
/// DLG files are GFF-based with file type "DLG ".
/// Reference: BioWare Aurora Dialog Format specification, neverwinter.nim
/// </summary>
public static class DlgWriter
{
    /// <summary>
    /// Write a DLG file to a file path.
    /// </summary>
    public static void Write(DlgFile dlg, string filePath)
    {
        var buffer = Write(dlg);
        File.WriteAllBytes(filePath, buffer);
    }

    /// <summary>
    /// Write a DLG file to a stream.
    /// </summary>
    public static void Write(DlgFile dlg, Stream stream)
    {
        var buffer = Write(dlg);
        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Write a DLG file to a byte buffer.
    /// </summary>
    public static byte[] Write(DlgFile dlg)
    {
        var gff = BuildGffFile(dlg);
        return GffWriter.Write(gff);
    }

    private static GffFile BuildGffFile(DlgFile dlg)
    {
        var gff = new GffFile
        {
            FileType = dlg.FileType,
            FileVersion = dlg.FileVersion,
            RootStruct = BuildRootStruct(dlg)
        };

        return gff;
    }

    private static GffStruct BuildRootStruct(DlgFile dlg)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        // Root level fields - all are required per BioWare spec
        AddDwordField(root, "DelayEntry", dlg.DelayEntry);
        AddDwordField(root, "DelayReply", dlg.DelayReply);
        AddCResRefField(root, "EndConverAbort", dlg.EndConverAbort ?? "");
        AddCResRefField(root, "EndConversation", dlg.EndConversation ?? "");
        AddDwordField(root, "NumWords", dlg.NumWords);
        AddByteField(root, "PreventZoomIn", (byte)(dlg.PreventZoomIn ? 1 : 0));

        // EntryList
        var entryList = new GffList();
        foreach (var entry in dlg.Entries)
            entryList.Elements.Add(BuildEntryStruct(entry));
        AddListField(root, "EntryList", entryList);

        // ReplyList
        var replyList = new GffList();
        foreach (var reply in dlg.Replies)
            replyList.Elements.Add(BuildReplyStruct(reply));
        AddListField(root, "ReplyList", replyList);

        // StartingList
        var startingList = new GffList();
        foreach (var link in dlg.StartingList)
            startingList.Elements.Add(BuildLinkStruct(link));
        AddListField(root, "StartingList", startingList);

        return root;
    }

    private static GffStruct BuildEntryStruct(DlgEntry entry)
    {
        var entryStruct = new GffStruct { Type = 0 };

        // Entry fields - all are required per BioWare DLG spec (Table 2.2.1)
        // Aurora Engine expects all fields to be present, even when empty
        AddCExoStringField(entryStruct, "Speaker", entry.Speaker ?? "");
        AddDwordField(entryStruct, "Animation", entry.Animation);
        AddByteField(entryStruct, "AnimLoop", (byte)(entry.AnimLoop ? 1 : 0));
        AddLocStringField(entryStruct, "Text", entry.Text);
        AddCResRefField(entryStruct, "Script", entry.Script ?? "");

        // ActionParams - always write (empty list if no params)
        var actionParamsList = new GffList();
        foreach (var param in entry.ActionParams)
            actionParamsList.Elements.Add(BuildParamStruct(param));
        AddListField(entryStruct, "ActionParams", actionParamsList);

        AddDwordField(entryStruct, "Delay", entry.Delay);
        AddCExoStringField(entryStruct, "Comment", entry.Comment ?? "");
        AddCResRefField(entryStruct, "Sound", entry.Sound ?? "");

        // Quest - always write (QuestEntry only when Quest is non-empty per spec)
        AddCExoStringField(entryStruct, "Quest", entry.Quest ?? "");
        if (!string.IsNullOrEmpty(entry.Quest))
        {
            AddDwordField(entryStruct, "QuestEntry", entry.QuestEntry);
        }

        // RepliesList
        var repliesList = new GffList();
        foreach (var link in entry.RepliesList)
            repliesList.Elements.Add(BuildLinkStruct(link));
        AddListField(entryStruct, "RepliesList", repliesList);

        return entryStruct;
    }

    private static GffStruct BuildReplyStruct(DlgReply reply)
    {
        var replyStruct = new GffStruct { Type = 0 };

        // Reply fields - all are required per BioWare DLG spec (Table 2.2.1)
        // Aurora Engine expects all fields to be present, even when empty
        AddDwordField(replyStruct, "Animation", reply.Animation);
        AddByteField(replyStruct, "AnimLoop", (byte)(reply.AnimLoop ? 1 : 0));
        AddLocStringField(replyStruct, "Text", reply.Text);
        AddCResRefField(replyStruct, "Script", reply.Script ?? "");

        // ActionParams - always write (empty list if no params)
        var actionParamsList = new GffList();
        foreach (var param in reply.ActionParams)
            actionParamsList.Elements.Add(BuildParamStruct(param));
        AddListField(replyStruct, "ActionParams", actionParamsList);

        AddDwordField(replyStruct, "Delay", reply.Delay);
        AddCExoStringField(replyStruct, "Comment", reply.Comment ?? "");
        AddCResRefField(replyStruct, "Sound", reply.Sound ?? "");

        // Quest - always write (QuestEntry only when Quest is non-empty per spec)
        AddCExoStringField(replyStruct, "Quest", reply.Quest ?? "");
        if (!string.IsNullOrEmpty(reply.Quest))
        {
            AddDwordField(replyStruct, "QuestEntry", reply.QuestEntry);
        }

        // EntriesList
        var entriesList = new GffList();
        foreach (var link in reply.EntriesList)
            entriesList.Elements.Add(BuildLinkStruct(link));
        AddListField(replyStruct, "EntriesList", entriesList);

        return replyStruct;
    }

    private static GffStruct BuildLinkStruct(DlgLink link)
    {
        var linkStruct = new GffStruct { Type = 0 };

        // Sync struct fields - Active and Index are required per DLG spec (Table 2.3.x)
        AddCResRefField(linkStruct, "Active", link.Active ?? "");

        // ConditionParams - always write (empty list if no params)
        var condParamsList = new GffList();
        foreach (var param in link.ConditionParams)
            condParamsList.Elements.Add(BuildParamStruct(param));
        AddListField(linkStruct, "ConditionParams", condParamsList);

        AddDwordField(linkStruct, "Index", link.Index);
        AddByteField(linkStruct, "IsChild", (byte)(link.IsChild ? 1 : 0));

        // LinkComment - only present when IsChild=1 per spec
        if (link.IsChild)
            AddCExoStringField(linkStruct, "LinkComment", link.LinkComment ?? "");

        return linkStruct;
    }

    private static GffStruct BuildParamStruct(DlgParam param)
    {
        var paramStruct = new GffStruct { Type = 0 };

        AddCExoStringField(paramStruct, "Key", param.Key);
        AddCExoStringField(paramStruct, "Value", param.Value);

        return paramStruct;
    }
}

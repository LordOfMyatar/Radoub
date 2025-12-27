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

        // Root level fields
        AddDwordField(root, "DelayEntry", dlg.DelayEntry);
        AddDwordField(root, "DelayReply", dlg.DelayReply);
        AddDwordField(root, "NumWords", dlg.NumWords);

        if (!string.IsNullOrEmpty(dlg.EndConversation))
            AddCResRefField(root, "EndConversation", dlg.EndConversation);

        if (!string.IsNullOrEmpty(dlg.EndConverAbort))
            AddCResRefField(root, "EndConverAbort", dlg.EndConverAbort);

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

        // Entry fields
        if (!string.IsNullOrEmpty(entry.Speaker))
            AddCExoStringField(entryStruct, "Speaker", entry.Speaker);

        AddDwordField(entryStruct, "Animation", entry.Animation);
        AddByteField(entryStruct, "AnimLoop", (byte)(entry.AnimLoop ? 1 : 0));
        AddLocStringField(entryStruct, "Text", entry.Text);

        if (!string.IsNullOrEmpty(entry.Script))
            AddCResRefField(entryStruct, "Script", entry.Script);

        // ActionParams
        if (entry.ActionParams.Count > 0)
        {
            var paramList = new GffList();
            foreach (var param in entry.ActionParams)
                paramList.Elements.Add(BuildParamStruct(param));
            AddListField(entryStruct, "ActionParams", paramList);
        }

        if (entry.Delay != 0xFFFFFFFF)
            AddDwordField(entryStruct, "Delay", entry.Delay);

        if (!string.IsNullOrEmpty(entry.Comment))
            AddCExoStringField(entryStruct, "Comment", entry.Comment);

        if (!string.IsNullOrEmpty(entry.Sound))
            AddCResRefField(entryStruct, "Sound", entry.Sound);

        if (!string.IsNullOrEmpty(entry.Quest))
        {
            AddCExoStringField(entryStruct, "Quest", entry.Quest);
            if (entry.QuestEntry != 0xFFFFFFFF)
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

        // Reply fields
        AddDwordField(replyStruct, "Animation", reply.Animation);
        AddByteField(replyStruct, "AnimLoop", (byte)(reply.AnimLoop ? 1 : 0));
        AddLocStringField(replyStruct, "Text", reply.Text);

        if (!string.IsNullOrEmpty(reply.Script))
            AddCResRefField(replyStruct, "Script", reply.Script);

        // ActionParams
        if (reply.ActionParams.Count > 0)
        {
            var paramList = new GffList();
            foreach (var param in reply.ActionParams)
                paramList.Elements.Add(BuildParamStruct(param));
            AddListField(replyStruct, "ActionParams", paramList);
        }

        if (reply.Delay != 0xFFFFFFFF)
            AddDwordField(replyStruct, "Delay", reply.Delay);

        if (!string.IsNullOrEmpty(reply.Comment))
            AddCExoStringField(replyStruct, "Comment", reply.Comment);

        if (!string.IsNullOrEmpty(reply.Sound))
            AddCResRefField(replyStruct, "Sound", reply.Sound);

        if (!string.IsNullOrEmpty(reply.Quest))
        {
            AddCExoStringField(replyStruct, "Quest", reply.Quest);
            if (reply.QuestEntry != 0xFFFFFFFF)
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

        AddDwordField(linkStruct, "Index", link.Index);

        if (!string.IsNullOrEmpty(link.Active))
            AddCResRefField(linkStruct, "Active", link.Active);

        // ConditionParams
        if (link.ConditionParams.Count > 0)
        {
            var paramList = new GffList();
            foreach (var param in link.ConditionParams)
                paramList.Elements.Add(BuildParamStruct(param));
            AddListField(linkStruct, "ConditionParams", paramList);
        }

        AddByteField(linkStruct, "IsChild", (byte)(link.IsChild ? 1 : 0));

        if (link.IsChild && !string.IsNullOrEmpty(link.LinkComment))
            AddCExoStringField(linkStruct, "LinkComment", link.LinkComment);

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

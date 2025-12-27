using Radoub.Formats.Dlg;
using Radoub.Formats.Gff;
using Xunit;

namespace Radoub.Formats.Tests;

public class DlgReaderTests
{
    [Fact]
    public void Read_ValidMinimalDlg_ParsesCorrectly()
    {
        var buffer = CreateMinimalDlgFile();

        var dlg = DlgReader.Read(buffer);

        Assert.Equal("DLG ", dlg.FileType);
        Assert.Equal("V3.2", dlg.FileVersion);
    }

    [Fact]
    public void Read_DlgWithRootFields_ParsesAllFields()
    {
        var buffer = CreateDlgWithRootFields();

        var dlg = DlgReader.Read(buffer);

        Assert.Equal(100u, dlg.DelayEntry);
        Assert.Equal(200u, dlg.DelayReply);
        Assert.Equal(50u, dlg.NumWords);
        Assert.Equal("end_script", dlg.EndConversation);
        Assert.Equal("abort_script", dlg.EndConverAbort);
        Assert.True(dlg.PreventZoomIn);
    }

    [Fact]
    public void Read_DlgWithEntry_ParsesEntryCorrectly()
    {
        var buffer = CreateDlgWithSingleEntry();

        var dlg = DlgReader.Read(buffer);

        Assert.Single(dlg.Entries);
        var entry = dlg.Entries[0];
        Assert.Equal("NPC_TAG", entry.Speaker);
        Assert.Equal(28u, entry.Animation); // Taunt
        Assert.True(entry.AnimLoop);
        Assert.Equal("Hello adventurer!", entry.Text.GetDefault());
        Assert.Equal("on_speak", entry.Script);
        Assert.Equal("Test comment", entry.Comment);
        Assert.Equal("vo_hello", entry.Sound);
    }

    [Fact]
    public void Read_DlgWithReply_ParsesReplyCorrectly()
    {
        var buffer = CreateDlgWithSingleReply();

        var dlg = DlgReader.Read(buffer);

        Assert.Single(dlg.Replies);
        var reply = dlg.Replies[0];
        Assert.Equal(29u, reply.Animation); // Greeting
        Assert.False(reply.AnimLoop);
        Assert.Equal("Greetings!", reply.Text.GetDefault());
        Assert.Equal("on_reply", reply.Script);
        Assert.Equal("Reply comment", reply.Comment);
    }

    [Fact]
    public void Read_DlgWithQuestEntry_ParsesQuestFields()
    {
        var buffer = CreateDlgWithQuestEntry();

        var dlg = DlgReader.Read(buffer);

        Assert.Single(dlg.Entries);
        var entry = dlg.Entries[0];
        Assert.Equal("main_quest", entry.Quest);
        Assert.Equal(5u, entry.QuestEntry);
    }

    [Fact]
    public void Read_DlgWithStartingList_ParsesStartingLinks()
    {
        var buffer = CreateDlgWithStartingList();

        var dlg = DlgReader.Read(buffer);

        Assert.Single(dlg.StartingList);
        var start = dlg.StartingList[0];
        Assert.Equal(0u, start.Index);
        Assert.Equal("check_condition", start.Active);
        Assert.False(start.IsChild);
    }

    [Fact]
    public void Read_DlgWithLinks_ParsesRepliesList()
    {
        var buffer = CreateDlgWithEntryToReplyLink();

        var dlg = DlgReader.Read(buffer);

        Assert.Single(dlg.Entries);
        Assert.Single(dlg.Entries[0].RepliesList);
        var link = dlg.Entries[0].RepliesList[0];
        Assert.Equal(0u, link.Index);
        Assert.False(link.IsChild);
    }

    [Fact]
    public void Read_DlgWithLinks_ParsesEntriesList()
    {
        var buffer = CreateDlgWithReplyToEntryLink();

        var dlg = DlgReader.Read(buffer);

        Assert.Single(dlg.Replies);
        Assert.Single(dlg.Replies[0].EntriesList);
        var link = dlg.Replies[0].EntriesList[0];
        Assert.Equal(0u, link.Index);
        Assert.True(link.IsChild); // It's a link back to existing entry
        Assert.Equal("Linked back", link.LinkComment);
    }

    [Fact]
    public void Read_DlgWithActionParams_ParsesParams()
    {
        var buffer = CreateDlgWithActionParams();

        var dlg = DlgReader.Read(buffer);

        Assert.Single(dlg.Entries);
        Assert.Single(dlg.Entries[0].ActionParams);
        var param = dlg.Entries[0].ActionParams[0];
        Assert.Equal("param_key", param.Key);
        Assert.Equal("param_value", param.Value);
    }

    [Fact]
    public void Read_DlgWithConditionParams_ParsesParams()
    {
        var buffer = CreateDlgWithConditionParams();

        var dlg = DlgReader.Read(buffer);

        Assert.Single(dlg.StartingList);
        Assert.Single(dlg.StartingList[0].ConditionParams);
        var param = dlg.StartingList[0].ConditionParams[0];
        Assert.Equal("condition_key", param.Key);
        Assert.Equal("condition_value", param.Value);
    }

    [Fact]
    public void Read_InvalidFileType_ThrowsException()
    {
        var gff = CreateGffFileWithType("UTC ");
        var buffer = GffWriter.Write(gff);

        var ex = Assert.Throws<InvalidDataException>(() => DlgReader.Read(buffer));
        Assert.Contains("Invalid DLG file type", ex.Message);
    }

    [Fact]
    public void RoundTrip_MinimalDlg_PreservesData()
    {
        var original = CreateMinimalDlgFile();

        var dlg = DlgReader.Read(original);
        var written = DlgWriter.Write(dlg);
        var dlg2 = DlgReader.Read(written);

        Assert.Equal(dlg.FileType, dlg2.FileType);
        Assert.Equal(dlg.FileVersion, dlg2.FileVersion);
    }

    [Fact]
    public void RoundTrip_DlgWithRootFields_PreservesData()
    {
        var original = CreateDlgWithRootFields();

        var dlg = DlgReader.Read(original);
        var written = DlgWriter.Write(dlg);
        var dlg2 = DlgReader.Read(written);

        Assert.Equal(dlg.DelayEntry, dlg2.DelayEntry);
        Assert.Equal(dlg.DelayReply, dlg2.DelayReply);
        Assert.Equal(dlg.NumWords, dlg2.NumWords);
        Assert.Equal(dlg.EndConversation, dlg2.EndConversation);
        Assert.Equal(dlg.EndConverAbort, dlg2.EndConverAbort);
        Assert.Equal(dlg.PreventZoomIn, dlg2.PreventZoomIn);
    }

    [Fact]
    public void RoundTrip_DlgWithEntry_PreservesData()
    {
        var original = CreateDlgWithSingleEntry();

        var dlg = DlgReader.Read(original);
        var written = DlgWriter.Write(dlg);
        var dlg2 = DlgReader.Read(written);

        Assert.Equal(dlg.Entries.Count, dlg2.Entries.Count);
        Assert.Equal(dlg.Entries[0].Speaker, dlg2.Entries[0].Speaker);
        Assert.Equal(dlg.Entries[0].Animation, dlg2.Entries[0].Animation);
        Assert.Equal(dlg.Entries[0].AnimLoop, dlg2.Entries[0].AnimLoop);
        Assert.Equal(dlg.Entries[0].Text.GetDefault(), dlg2.Entries[0].Text.GetDefault());
        Assert.Equal(dlg.Entries[0].Script, dlg2.Entries[0].Script);
    }

    [Fact]
    public void RoundTrip_DlgWithReply_PreservesData()
    {
        var original = CreateDlgWithSingleReply();

        var dlg = DlgReader.Read(original);
        var written = DlgWriter.Write(dlg);
        var dlg2 = DlgReader.Read(written);

        Assert.Equal(dlg.Replies.Count, dlg2.Replies.Count);
        Assert.Equal(dlg.Replies[0].Animation, dlg2.Replies[0].Animation);
        Assert.Equal(dlg.Replies[0].Text.GetDefault(), dlg2.Replies[0].Text.GetDefault());
        Assert.Equal(dlg.Replies[0].Script, dlg2.Replies[0].Script);
    }

    [Fact]
    public void RoundTrip_DlgWithLinks_PreservesLinks()
    {
        var original = CreateDlgWithEntryToReplyLink();

        var dlg = DlgReader.Read(original);
        var written = DlgWriter.Write(dlg);
        var dlg2 = DlgReader.Read(written);

        Assert.Equal(dlg.Entries[0].RepliesList.Count, dlg2.Entries[0].RepliesList.Count);
        Assert.Equal(dlg.Entries[0].RepliesList[0].Index, dlg2.Entries[0].RepliesList[0].Index);
    }

    [Fact]
    public void RoundTrip_ComplexDialog_PreservesAllData()
    {
        var original = CreateComplexDialog();

        var dlg = DlgReader.Read(original);
        var written = DlgWriter.Write(dlg);
        var dlg2 = DlgReader.Read(written);

        Assert.Equal(dlg.Entries.Count, dlg2.Entries.Count);
        Assert.Equal(dlg.Replies.Count, dlg2.Replies.Count);
        Assert.Equal(dlg.StartingList.Count, dlg2.StartingList.Count);

        // Verify first entry
        Assert.Equal(dlg.Entries[0].Speaker, dlg2.Entries[0].Speaker);
        Assert.Equal(dlg.Entries[0].RepliesList.Count, dlg2.Entries[0].RepliesList.Count);

        // Verify first reply
        Assert.Equal(dlg.Replies[0].EntriesList.Count, dlg2.Replies[0].EntriesList.Count);

        // Verify starting point
        Assert.Equal(dlg.StartingList[0].Index, dlg2.StartingList[0].Index);
        Assert.Equal(dlg.StartingList[0].Active, dlg2.StartingList[0].Active);
    }

    #region Test Helpers

    private static byte[] CreateMinimalDlgFile()
    {
        var gff = CreateGffFileWithType("DLG ");
        return GffWriter.Write(gff);
    }

    private static byte[] CreateDlgWithRootFields()
    {
        var gff = CreateGffFileWithType("DLG ");
        var root = gff.RootStruct;

        AddDwordField(root, "DelayEntry", 100);
        AddDwordField(root, "DelayReply", 200);
        AddDwordField(root, "NumWords", 50);
        AddCResRefField(root, "EndConversation", "end_script");
        AddCResRefField(root, "EndConverAbort", "abort_script");
        AddByteField(root, "PreventZoomIn", 1);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateDlgWithSingleEntry()
    {
        var gff = CreateGffFileWithType("DLG ");
        var root = gff.RootStruct;

        var entryList = new GffList();
        var entryStruct = new GffStruct { Type = 0 };
        AddCExoStringField(entryStruct, "Speaker", "NPC_TAG");
        AddDwordField(entryStruct, "Animation", 28);
        AddByteField(entryStruct, "AnimLoop", 1);
        AddCResRefField(entryStruct, "Script", "on_speak");
        AddCExoStringField(entryStruct, "Comment", "Test comment");
        AddCResRefField(entryStruct, "Sound", "vo_hello");

        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        locString.LocalizedStrings[0] = "Hello adventurer!";
        AddLocStringField(entryStruct, "Text", locString);

        entryList.Elements.Add(entryStruct);
        entryList.Count = 1;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "EntryList",
            Value = entryList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateDlgWithSingleReply()
    {
        var gff = CreateGffFileWithType("DLG ");
        var root = gff.RootStruct;

        var replyList = new GffList();
        var replyStruct = new GffStruct { Type = 0 };
        AddDwordField(replyStruct, "Animation", 29);
        AddByteField(replyStruct, "AnimLoop", 0);
        AddCResRefField(replyStruct, "Script", "on_reply");
        AddCExoStringField(replyStruct, "Comment", "Reply comment");

        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        locString.LocalizedStrings[0] = "Greetings!";
        AddLocStringField(replyStruct, "Text", locString);

        replyList.Elements.Add(replyStruct);
        replyList.Count = 1;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "ReplyList",
            Value = replyList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateDlgWithQuestEntry()
    {
        var gff = CreateGffFileWithType("DLG ");
        var root = gff.RootStruct;

        var entryList = new GffList();
        var entryStruct = new GffStruct { Type = 0 };
        AddCExoStringField(entryStruct, "Quest", "main_quest");
        AddDwordField(entryStruct, "QuestEntry", 5);

        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        locString.LocalizedStrings[0] = "Quest updated!";
        AddLocStringField(entryStruct, "Text", locString);

        entryList.Elements.Add(entryStruct);
        entryList.Count = 1;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "EntryList",
            Value = entryList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateDlgWithStartingList()
    {
        var gff = CreateGffFileWithType("DLG ");
        var root = gff.RootStruct;

        // Add an entry for the start to point to
        var entryList = new GffList();
        var entryStruct = new GffStruct { Type = 0 };
        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        locString.LocalizedStrings[0] = "First entry";
        AddLocStringField(entryStruct, "Text", locString);
        entryList.Elements.Add(entryStruct);
        entryList.Count = 1;
        root.Fields.Add(new GffField { Type = GffField.List, Label = "EntryList", Value = entryList });

        // Add starting list
        var startingList = new GffList();
        var startStruct = new GffStruct { Type = 0 };
        AddDwordField(startStruct, "Index", 0);
        AddCResRefField(startStruct, "Active", "check_condition");
        AddByteField(startStruct, "IsChild", 0);
        startingList.Elements.Add(startStruct);
        startingList.Count = 1;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "StartingList",
            Value = startingList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateDlgWithEntryToReplyLink()
    {
        var gff = CreateGffFileWithType("DLG ");
        var root = gff.RootStruct;

        // Create a reply
        var replyList = new GffList();
        var replyStruct = new GffStruct { Type = 0 };
        var replyText = new CExoLocString { StrRef = 0xFFFFFFFF };
        replyText.LocalizedStrings[0] = "Player response";
        AddLocStringField(replyStruct, "Text", replyText);
        replyList.Elements.Add(replyStruct);
        replyList.Count = 1;
        root.Fields.Add(new GffField { Type = GffField.List, Label = "ReplyList", Value = replyList });

        // Create an entry with RepliesList pointing to the reply
        var entryList = new GffList();
        var entryStruct = new GffStruct { Type = 0 };
        var entryText = new CExoLocString { StrRef = 0xFFFFFFFF };
        entryText.LocalizedStrings[0] = "NPC greeting";
        AddLocStringField(entryStruct, "Text", entryText);

        // Add RepliesList
        var repliesList = new GffList();
        var linkStruct = new GffStruct { Type = 0 };
        AddDwordField(linkStruct, "Index", 0);
        AddByteField(linkStruct, "IsChild", 0);
        repliesList.Elements.Add(linkStruct);
        repliesList.Count = 1;
        entryStruct.Fields.Add(new GffField { Type = GffField.List, Label = "RepliesList", Value = repliesList });

        entryList.Elements.Add(entryStruct);
        entryList.Count = 1;
        root.Fields.Add(new GffField { Type = GffField.List, Label = "EntryList", Value = entryList });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateDlgWithReplyToEntryLink()
    {
        var gff = CreateGffFileWithType("DLG ");
        var root = gff.RootStruct;

        // Create an entry
        var entryList = new GffList();
        var entryStruct = new GffStruct { Type = 0 };
        var entryText = new CExoLocString { StrRef = 0xFFFFFFFF };
        entryText.LocalizedStrings[0] = "NPC speech";
        AddLocStringField(entryStruct, "Text", entryText);
        entryList.Elements.Add(entryStruct);
        entryList.Count = 1;
        root.Fields.Add(new GffField { Type = GffField.List, Label = "EntryList", Value = entryList });

        // Create a reply with EntriesList linking back (IsChild=1)
        var replyList = new GffList();
        var replyStruct = new GffStruct { Type = 0 };
        var replyText = new CExoLocString { StrRef = 0xFFFFFFFF };
        replyText.LocalizedStrings[0] = "Player reply";
        AddLocStringField(replyStruct, "Text", replyText);

        // Add EntriesList with IsChild=1 (link back)
        var entriesList = new GffList();
        var linkStruct = new GffStruct { Type = 0 };
        AddDwordField(linkStruct, "Index", 0);
        AddByteField(linkStruct, "IsChild", 1);
        AddCExoStringField(linkStruct, "LinkComment", "Linked back");
        entriesList.Elements.Add(linkStruct);
        entriesList.Count = 1;
        replyStruct.Fields.Add(new GffField { Type = GffField.List, Label = "EntriesList", Value = entriesList });

        replyList.Elements.Add(replyStruct);
        replyList.Count = 1;
        root.Fields.Add(new GffField { Type = GffField.List, Label = "ReplyList", Value = replyList });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateDlgWithActionParams()
    {
        var gff = CreateGffFileWithType("DLG ");
        var root = gff.RootStruct;

        var entryList = new GffList();
        var entryStruct = new GffStruct { Type = 0 };

        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        locString.LocalizedStrings[0] = "Entry with params";
        AddLocStringField(entryStruct, "Text", locString);

        // Add ActionParams
        var paramList = new GffList();
        var paramStruct = new GffStruct { Type = 0 };
        AddCExoStringField(paramStruct, "Key", "param_key");
        AddCExoStringField(paramStruct, "Value", "param_value");
        paramList.Elements.Add(paramStruct);
        paramList.Count = 1;
        entryStruct.Fields.Add(new GffField { Type = GffField.List, Label = "ActionParams", Value = paramList });

        entryList.Elements.Add(entryStruct);
        entryList.Count = 1;
        root.Fields.Add(new GffField { Type = GffField.List, Label = "EntryList", Value = entryList });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateDlgWithConditionParams()
    {
        var gff = CreateGffFileWithType("DLG ");
        var root = gff.RootStruct;

        // Add entry
        var entryList = new GffList();
        var entryStruct = new GffStruct { Type = 0 };
        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        locString.LocalizedStrings[0] = "Entry";
        AddLocStringField(entryStruct, "Text", locString);
        entryList.Elements.Add(entryStruct);
        entryList.Count = 1;
        root.Fields.Add(new GffField { Type = GffField.List, Label = "EntryList", Value = entryList });

        // Add StartingList with ConditionParams
        var startingList = new GffList();
        var startStruct = new GffStruct { Type = 0 };
        AddDwordField(startStruct, "Index", 0);
        AddByteField(startStruct, "IsChild", 0);

        var paramList = new GffList();
        var paramStruct = new GffStruct { Type = 0 };
        AddCExoStringField(paramStruct, "Key", "condition_key");
        AddCExoStringField(paramStruct, "Value", "condition_value");
        paramList.Elements.Add(paramStruct);
        paramList.Count = 1;
        startStruct.Fields.Add(new GffField { Type = GffField.List, Label = "ConditionParams", Value = paramList });

        startingList.Elements.Add(startStruct);
        startingList.Count = 1;
        root.Fields.Add(new GffField { Type = GffField.List, Label = "StartingList", Value = startingList });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateComplexDialog()
    {
        var gff = CreateGffFileWithType("DLG ");
        var root = gff.RootStruct;

        // Root fields
        AddDwordField(root, "DelayEntry", 50);
        AddDwordField(root, "DelayReply", 50);

        // Create 2 entries
        var entryList = new GffList();

        // Entry 0: NPC greeting
        var entry0 = new GffStruct { Type = 0 };
        AddCExoStringField(entry0, "Speaker", "MERCHANT");
        var text0 = new CExoLocString { StrRef = 0xFFFFFFFF };
        text0.LocalizedStrings[0] = "Welcome to my shop!";
        AddLocStringField(entry0, "Text", text0);

        var replies0 = new GffList();
        var link0 = new GffStruct { Type = 0 };
        AddDwordField(link0, "Index", 0);
        AddByteField(link0, "IsChild", 0);
        replies0.Elements.Add(link0);
        var link1 = new GffStruct { Type = 0 };
        AddDwordField(link1, "Index", 1);
        AddByteField(link1, "IsChild", 0);
        replies0.Elements.Add(link1);
        replies0.Count = 2;
        entry0.Fields.Add(new GffField { Type = GffField.List, Label = "RepliesList", Value = replies0 });

        // Entry 1: NPC farewell
        var entry1 = new GffStruct { Type = 0 };
        AddCExoStringField(entry1, "Speaker", "MERCHANT");
        var text1 = new CExoLocString { StrRef = 0xFFFFFFFF };
        text1.LocalizedStrings[0] = "Come back anytime!";
        AddLocStringField(entry1, "Text", text1);

        entryList.Elements.Add(entry0);
        entryList.Elements.Add(entry1);
        entryList.Count = 2;
        root.Fields.Add(new GffField { Type = GffField.List, Label = "EntryList", Value = entryList });

        // Create 2 replies
        var replyList = new GffList();

        // Reply 0: Buy
        var reply0 = new GffStruct { Type = 0 };
        var replyText0 = new CExoLocString { StrRef = 0xFFFFFFFF };
        replyText0.LocalizedStrings[0] = "Let me see your wares.";
        AddLocStringField(reply0, "Text", replyText0);
        // Link to farewell entry
        var entries0 = new GffList();
        var entryLink0 = new GffStruct { Type = 0 };
        AddDwordField(entryLink0, "Index", 1);
        AddByteField(entryLink0, "IsChild", 0);
        entries0.Elements.Add(entryLink0);
        entries0.Count = 1;
        reply0.Fields.Add(new GffField { Type = GffField.List, Label = "EntriesList", Value = entries0 });

        // Reply 1: Goodbye
        var reply1 = new GffStruct { Type = 0 };
        var replyText1 = new CExoLocString { StrRef = 0xFFFFFFFF };
        replyText1.LocalizedStrings[0] = "Goodbye.";
        AddLocStringField(reply1, "Text", replyText1);
        // Link back to farewell (IsChild=1)
        var entries1 = new GffList();
        var entryLink1 = new GffStruct { Type = 0 };
        AddDwordField(entryLink1, "Index", 1);
        AddByteField(entryLink1, "IsChild", 1);
        entries1.Elements.Add(entryLink1);
        entries1.Count = 1;
        reply1.Fields.Add(new GffField { Type = GffField.List, Label = "EntriesList", Value = entries1 });

        replyList.Elements.Add(reply0);
        replyList.Elements.Add(reply1);
        replyList.Count = 2;
        root.Fields.Add(new GffField { Type = GffField.List, Label = "ReplyList", Value = replyList });

        // Starting list
        var startingList = new GffList();
        var startStruct = new GffStruct { Type = 0 };
        AddDwordField(startStruct, "Index", 0);
        AddCResRefField(startStruct, "Active", "check_shop_open");
        AddByteField(startStruct, "IsChild", 0);
        startingList.Elements.Add(startStruct);
        startingList.Count = 1;
        root.Fields.Add(new GffField { Type = GffField.List, Label = "StartingList", Value = startingList });

        return GffWriter.Write(gff);
    }

    private static GffFile CreateGffFileWithType(string fileType)
    {
        return new GffFile
        {
            FileType = fileType,
            FileVersion = "V3.2",
            RootStruct = new GffStruct { Type = 0xFFFFFFFF }
        };
    }

    private static void AddByteField(GffStruct parent, string label, byte value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.BYTE,
            Label = label,
            Value = value
        });
    }

    private static void AddDwordField(GffStruct parent, string label, uint value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.DWORD,
            Label = label,
            Value = value
        });
    }

    private static void AddCExoStringField(GffStruct parent, string label, string value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CExoString,
            Label = label,
            Value = value
        });
    }

    private static void AddCResRefField(GffStruct parent, string label, string value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CResRef,
            Label = label,
            Value = value
        });
    }

    private static void AddLocStringField(GffStruct parent, string label, CExoLocString value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CExoLocString,
            Label = label,
            Value = value
        });
    }

    #endregion
}

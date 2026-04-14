using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Dlg;
using Xunit;
using Xunit.Abstractions;

namespace Parley.Tests;

/// <summary>
/// Stress tests for DLG parser and editor with large, complex, and deeply nested dialogs.
/// Covers #1309 Round-Trip Integrity test areas.
/// </summary>
public class DlgStressTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDirectory;
    private readonly DialogFileService _fileService;

    public DlgStressTests(ITestOutputHelper output)
    {
        _output = output;
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParleyStress_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _fileService = new DialogFileService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try { Directory.Delete(_testDirectory, true); }
            catch { /* Ignore cleanup errors */ }
        }
    }

    #region Large Dialog Tests (500+ nodes)

    [Fact]
    public async Task RoundTrip_LargeDialog_500Nodes_NoDataLoss()
    {
        // Build a dialog with 250 entries and 250 replies in a flat branching structure
        var dialog = new Dialog();
        dialog.DelayEntry = 42;
        dialog.DelayReply = 84;
        dialog.NumWords = 9999;

        var rootEntry = dialog.CreateNode(DialogNodeType.Entry)!;
        rootEntry.Text.Add(0, "Root NPC greeting");
        rootEntry.Speaker = "NPC_TAG";
        dialog.AddNodeInternal(rootEntry, DialogNodeType.Entry);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = rootEntry;
        startPtr.Index = 0;
        startPtr.IsStart = true;
        dialog.Starts.Add(startPtr);

        // Build 249 more entry/reply pairs branching from root
        for (int i = 0; i < 249; i++)
        {
            var reply = dialog.CreateNode(DialogNodeType.Reply)!;
            reply.Text.Add(0, $"PC response {i}: What about option {i}?");
            dialog.AddNodeInternal(reply, DialogNodeType.Reply);

            var replyPtr = dialog.CreatePtr()!;
            replyPtr.Type = DialogNodeType.Reply;
            replyPtr.Node = reply;
            replyPtr.Index = (uint)i;
            rootEntry.Pointers.Add(replyPtr);

            var entry = dialog.CreateNode(DialogNodeType.Entry)!;
            entry.Text.Add(0, $"NPC answer {i}: Here's what I know about {i}.");
            entry.Speaker = i % 2 == 0 ? "NPC_TAG" : "NPC_HELPER";
            dialog.AddNodeInternal(entry, DialogNodeType.Entry);

            var entryPtr = dialog.CreatePtr()!;
            entryPtr.Type = DialogNodeType.Entry;
            entryPtr.Node = entry;
            entryPtr.Index = (uint)(i + 1);
            reply.Pointers.Add(entryPtr);
        }

        var filePath = Path.Combine(_testDirectory, "large500.dlg");

        _output.WriteLine($"Dialog built: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");
        Assert.Equal(250, dialog.Entries.Count);
        Assert.Equal(249, dialog.Replies.Count);

        // Round-trip
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved, "Save should succeed");

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);

        _output.WriteLine($"Reloaded: {reloaded!.Entries.Count} entries, {reloaded.Replies.Count} replies");
        Assert.Equal(dialog.Entries.Count, reloaded.Entries.Count);
        Assert.Equal(dialog.Replies.Count, reloaded.Replies.Count);
        Assert.Equal(42u, reloaded.DelayEntry);
        Assert.Equal(84u, reloaded.DelayReply);
        Assert.Equal(9999u, reloaded.NumWords);

        // Spot-check text preservation
        Assert.Equal("Root NPC greeting", reloaded.Entries[0].Text.GetDefault());
        Assert.Equal("PC response 100: What about option 100?", reloaded.Replies[100].Text.GetDefault());
        Assert.Equal("NPC answer 200: Here's what I know about 200.", reloaded.Entries[201].Text.GetDefault());
    }

    [Fact]
    public async Task RoundTrip_DeepNesting_20Levels_PreservesStructure()
    {
        // Build a 20-level deep conversation chain: Entry -> Reply -> Entry -> Reply -> ...
        var dialog = new Dialog();
        DialogNode? currentEntry = null;

        for (int depth = 0; depth < 20; depth++)
        {
            var entry = dialog.CreateNode(DialogNodeType.Entry)!;
            entry.Text.Add(0, $"NPC at depth {depth}");
            entry.Speaker = "DEEP_NPC";
            dialog.AddNodeInternal(entry, DialogNodeType.Entry);

            if (depth == 0)
            {
                var startPtr = dialog.CreatePtr()!;
                startPtr.Type = DialogNodeType.Entry;
                startPtr.Node = entry;
                startPtr.Index = 0;
                startPtr.IsStart = true;
                dialog.Starts.Add(startPtr);
            }

            if (currentEntry != null)
            {
                // Previous entry should have a reply linking down
                var reply = dialog.CreateNode(DialogNodeType.Reply)!;
                reply.Text.Add(0, $"PC at depth {depth - 1} to {depth}");
                dialog.AddNodeInternal(reply, DialogNodeType.Reply);

                var replyPtr = dialog.CreatePtr()!;
                replyPtr.Type = DialogNodeType.Reply;
                replyPtr.Node = reply;
                currentEntry.Pointers.Add(replyPtr);

                var entryPtr = dialog.CreatePtr()!;
                entryPtr.Type = DialogNodeType.Entry;
                entryPtr.Node = entry;
                reply.Pointers.Add(entryPtr);
            }

            currentEntry = entry;
        }

        var filePath = Path.Combine(_testDirectory, "deep20.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);

        Assert.Equal(20, reloaded!.Entries.Count);
        Assert.Equal(19, reloaded.Replies.Count);

        // Verify chain integrity: walk from start down 20 levels
        var current = reloaded.Starts[0].Node!;
        for (int depth = 0; depth < 20; depth++)
        {
            Assert.Equal($"NPC at depth {depth}", current.Text.GetDefault());
            if (depth < 19)
            {
                Assert.Single(current.Pointers);
                var reply = current.Pointers[0].Node!;
                Assert.Single(reply.Pointers);
                current = reply.Pointers[0].Node!;
            }
        }
    }

    [Fact]
    public async Task RoundTrip_AllFieldsPopulated_PreservesEverything()
    {
        // Create a dialog with every field filled on entries and replies
        var dialog = new Dialog
        {
            DelayEntry = 500,
            DelayReply = 750,
            NumWords = 12345,
            PreventZoom = true,
            ScriptEnd = "end_convo_scr",
            ScriptAbort = "abort_convo_scr"
        };

        var entry = dialog.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "English text");
        entry.Text.Add(2, "French text");
        entry.Text.Add(4, "German text");
        entry.Speaker = "FULL_NPC";
        entry.ScriptAction = "entry_act_scr";
        entry.Sound = "vo_entry_sound";
        entry.Comment = "This is a developer comment on entry";
        entry.Quest = "q_main_quest";
        entry.QuestEntry = 5;
        entry.Animation = DialogAnimation.Taunt;
        entry.AnimationLoop = true;
        entry.Delay = 3000;
        entry.ActionParams["Param1"] = "Value1";
        entry.ActionParams["Param2"] = "42";
        dialog.AddNodeInternal(entry, DialogNodeType.Entry);

        var reply = dialog.CreateNode(DialogNodeType.Reply)!;
        reply.Text.Add(0, "PC reply text");
        reply.Text.Add(2, "French PC reply");
        reply.ScriptAction = "reply_act_scr";
        reply.Sound = "vo_reply_sound";
        reply.Comment = "Developer comment on reply";
        reply.Quest = "q_side_quest";
        reply.QuestEntry = 3;
        reply.Animation = DialogAnimation.Bow;
        reply.AnimationLoop = false;
        reply.Delay = 1500;
        reply.ActionParams["ReplyParam"] = "ReplyValue";
        dialog.AddNodeInternal(reply, DialogNodeType.Reply);

        // Link entry -> reply with condition script and params
        var replyPtr = dialog.CreatePtr()!;
        replyPtr.Type = DialogNodeType.Reply;
        replyPtr.Node = reply;
        replyPtr.ScriptAppears = "check_condition";
        replyPtr.ConditionParams["CondKey"] = "CondValue";
        replyPtr.ConditionParams["CondNum"] = "99";
        entry.Pointers.Add(replyPtr);

        // Start pointer with condition
        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = entry;
        startPtr.IsStart = true;
        startPtr.ScriptAppears = "start_condition";
        startPtr.ConditionParams["StartParam"] = "StartValue";
        dialog.Starts.Add(startPtr);

        var filePath = Path.Combine(_testDirectory, "allfields.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);

        // Verify global properties
        Assert.Equal(500u, reloaded!.DelayEntry);
        Assert.Equal(750u, reloaded.DelayReply);
        Assert.Equal(12345u, reloaded.NumWords);
        Assert.True(reloaded.PreventZoom);
        Assert.Equal("end_convo_scr", reloaded.ScriptEnd);
        Assert.Equal("abort_convo_scr", reloaded.ScriptAbort);

        // Verify entry
        var rEntry = reloaded.Entries[0];
        Assert.Equal("English text", rEntry.Text.GetDefault());
        Assert.Equal("FULL_NPC", rEntry.Speaker);
        Assert.Equal("entry_act_scr", rEntry.ScriptAction);
        Assert.Equal("vo_entry_sound", rEntry.Sound);
        Assert.Equal("This is a developer comment on entry", rEntry.Comment);
        Assert.Equal("q_main_quest", rEntry.Quest);
        Assert.Equal(5u, rEntry.QuestEntry);
        Assert.Equal(DialogAnimation.Taunt, rEntry.Animation);
        Assert.True(rEntry.AnimationLoop);
        Assert.Equal(3000u, rEntry.Delay);
        Assert.Equal("Value1", rEntry.ActionParams["Param1"]);
        Assert.Equal("42", rEntry.ActionParams["Param2"]);

        // Verify reply
        var rReply = reloaded.Replies[0];
        Assert.Equal("PC reply text", rReply.Text.GetDefault());
        Assert.Equal("reply_act_scr", rReply.ScriptAction);
        Assert.Equal("vo_reply_sound", rReply.Sound);
        Assert.Equal("Developer comment on reply", rReply.Comment);
        Assert.Equal("q_side_quest", rReply.Quest);
        Assert.Equal(3u, rReply.QuestEntry);
        Assert.Equal(DialogAnimation.Bow, rReply.Animation);
        Assert.False(rReply.AnimationLoop);
        Assert.Equal(1500u, rReply.Delay);
        Assert.Equal("ReplyValue", rReply.ActionParams["ReplyParam"]);

        // Verify link with condition
        var rReplyPtr = rEntry.Pointers[0];
        Assert.Equal("check_condition", rReplyPtr.ScriptAppears);
        Assert.Equal("CondValue", rReplyPtr.ConditionParams["CondKey"]);
        Assert.Equal("99", rReplyPtr.ConditionParams["CondNum"]);

        // Verify start condition
        var rStartPtr = reloaded.Starts[0];
        Assert.Equal("start_condition", rStartPtr.ScriptAppears);
        Assert.Equal("StartValue", rStartPtr.ConditionParams["StartParam"]);
    }

    [Fact]
    public async Task RoundTrip_SharedReplies_IsLinkPreserved()
    {
        // Two entries share the same reply via IsLink
        var dialog = new Dialog();

        var entry0 = dialog.CreateNode(DialogNodeType.Entry)!;
        entry0.Text.Add(0, "Entry 0: Greeting");
        dialog.AddNodeInternal(entry0, DialogNodeType.Entry);

        var entry1 = dialog.CreateNode(DialogNodeType.Entry)!;
        entry1.Text.Add(0, "Entry 1: Alternate greeting");
        dialog.AddNodeInternal(entry1, DialogNodeType.Entry);

        var sharedReply = dialog.CreateNode(DialogNodeType.Reply)!;
        sharedReply.Text.Add(0, "PC: Shared response");
        dialog.AddNodeInternal(sharedReply, DialogNodeType.Reply);

        // Entry 0 owns the reply (IsLink=false)
        var ptr0 = dialog.CreatePtr()!;
        ptr0.Type = DialogNodeType.Reply;
        ptr0.Node = sharedReply;
        ptr0.IsLink = false;
        entry0.Pointers.Add(ptr0);

        // Entry 1 links to the same reply (IsLink=true)
        var ptr1 = dialog.CreatePtr()!;
        ptr1.Type = DialogNodeType.Reply;
        ptr1.Node = sharedReply;
        ptr1.IsLink = true;
        entry1.Pointers.Add(ptr1);

        // Two starts
        var start0 = dialog.CreatePtr()!;
        start0.Type = DialogNodeType.Entry;
        start0.Node = entry0;
        start0.IsStart = true;
        dialog.Starts.Add(start0);

        var start1 = dialog.CreatePtr()!;
        start1.Type = DialogNodeType.Entry;
        start1.Node = entry1;
        start1.IsStart = true;
        dialog.Starts.Add(start1);

        var filePath = Path.Combine(_testDirectory, "shared.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);

        Assert.Equal(2, reloaded!.Entries.Count);
        Assert.Single(reloaded.Replies);
        Assert.Equal(2, reloaded.Starts.Count);

        // One pointer should be a link, one should not
        var entry0Ptrs = reloaded.Entries[0].Pointers;
        var entry1Ptrs = reloaded.Entries[1].Pointers;
        Assert.Single(entry0Ptrs);
        Assert.Single(entry1Ptrs);

        // The non-link entry should have IsLink=false, the link should have IsLink=true
        var hasLink = entry0Ptrs[0].IsLink || entry1Ptrs[0].IsLink;
        var hasNonLink = !entry0Ptrs[0].IsLink || !entry1Ptrs[0].IsLink;
        Assert.True(hasLink, "One pointer should be a link");
        Assert.True(hasNonLink, "One pointer should be a non-link (owner)");
    }

    [Fact]
    public async Task RoundTrip_MultipleConditionalStarts_PreservesOrder()
    {
        // Dialog with 5 conditional start entries — order matters for NWN engine
        var dialog = new Dialog();

        for (int i = 0; i < 5; i++)
        {
            var entry = dialog.CreateNode(DialogNodeType.Entry)!;
            entry.Text.Add(0, $"Start {i}: Conditional greeting {i}");
            entry.Speaker = "NPC_TAG";
            dialog.AddNodeInternal(entry, DialogNodeType.Entry);

            var startPtr = dialog.CreatePtr()!;
            startPtr.Type = DialogNodeType.Entry;
            startPtr.Node = entry;
            startPtr.IsStart = true;
            startPtr.ScriptAppears = $"check_start_{i}";
            startPtr.ConditionParams[$"param_{i}"] = $"value_{i}";
            dialog.Starts.Add(startPtr);
        }

        var filePath = Path.Combine(_testDirectory, "multistarts.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);

        Assert.Equal(5, reloaded!.Starts.Count);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal($"Start {i}: Conditional greeting {i}", reloaded.Starts[i].Node!.Text.GetDefault());
            Assert.Equal($"check_start_{i}", reloaded.Starts[i].ScriptAppears);
            Assert.Equal($"value_{i}", reloaded.Starts[i].ConditionParams[$"param_{i}"]);
        }
    }

    #endregion

    #region Binary-Level Stress Tests

    [Fact]
    public void FormatLevel_LargeDialog_RoundTrips()
    {
        // Build a DlgFile directly and verify binary round-trip at format level
        var dlg = new DlgFile
        {
            FileType = "DLG ",
            FileVersion = "V3.2",
            DelayEntry = 100,
            DelayReply = 200,
            NumWords = 5000,
            EndConversation = "end_script",
            EndConverAbort = "abort_script",
            PreventZoomIn = true
        };

        // Add 100 entries and 100 replies
        for (int i = 0; i < 100; i++)
        {
            var entry = new DlgEntry
            {
                Speaker = $"NPC_{i}",
                Script = $"entry_script_{i}",
                Sound = $"vo_entry_{i}",
                Comment = $"Entry comment {i}",
                Animation = (uint)(i % 30),
                AnimLoop = i % 2 == 0,
                Quest = i % 10 == 0 ? $"quest_{i}" : "",
                QuestEntry = i % 10 == 0 ? (uint)i : 0,
                Delay = (uint)(i * 100)
            };
            entry.Text.StrRef = 0xFFFFFFFF;
            entry.Text.LocalizedStrings[0] = $"Entry text {i}: The quick brown fox jumps over the lazy dog.";

            if (i < 100)
            {
                entry.RepliesList.Add(new DlgLink { Index = (uint)i, IsChild = false });
            }

            dlg.Entries.Add(entry);

            var reply = new DlgReply
            {
                Script = $"reply_script_{i}",
                Sound = $"vo_reply_{i}",
                Comment = $"Reply comment {i}",
                Animation = (uint)((i + 5) % 30),
                AnimLoop = i % 3 == 0,
                Delay = (uint)(i * 50)
            };
            reply.Text.StrRef = 0xFFFFFFFF;
            reply.Text.LocalizedStrings[0] = $"Reply text {i}: Player response number {i}.";

            if (i < 99)
            {
                reply.EntriesList.Add(new DlgLink { Index = (uint)(i + 1), IsChild = false });
            }

            dlg.Replies.Add(reply);
        }

        dlg.StartingList.Add(new DlgLink { Index = 0, IsChild = false });

        // Round-trip at format level
        var bytes = DlgWriter.Write(dlg);
        var reloaded = DlgReader.Read(bytes);

        _output.WriteLine($"Binary size: {bytes.Length} bytes");
        Assert.Equal(100, reloaded.Entries.Count);
        Assert.Equal(100, reloaded.Replies.Count);
        Assert.Equal(100u, reloaded.DelayEntry);
        Assert.Equal(200u, reloaded.DelayReply);
        Assert.Equal(5000u, reloaded.NumWords);
        Assert.True(reloaded.PreventZoomIn);

        // Spot-check fields
        for (int i = 0; i < 100; i += 25)
        {
            Assert.Equal($"NPC_{i}", reloaded.Entries[i].Speaker);
            Assert.Equal($"entry_script_{i}", reloaded.Entries[i].Script);
            Assert.Equal($"Entry text {i}: The quick brown fox jumps over the lazy dog.",
                reloaded.Entries[i].Text.LocalizedStrings[0]);
            Assert.Equal($"Reply text {i}: Player response number {i}.",
                reloaded.Replies[i].Text.LocalizedStrings[0]);
        }
    }

    [Fact]
    public void FormatLevel_MaxScriptParameters_RoundTrips()
    {
        var dlg = new DlgFile
        {
            FileType = "DLG ",
            FileVersion = "V3.2"
        };

        var entry = new DlgEntry();
        entry.Text.StrRef = 0xFFFFFFFF;
        entry.Text.LocalizedStrings[0] = "Entry with many params";

        // Add 10 action params (more than typical usage)
        for (int i = 0; i < 10; i++)
        {
            entry.ActionParams.Add(new DlgParam
            {
                Key = $"action_key_{i}",
                Value = $"action_value_{i}"
            });
        }

        dlg.Entries.Add(entry);

        // Start link with condition params
        var startLink = new DlgLink { Index = 0, IsChild = false };
        for (int i = 0; i < 10; i++)
        {
            startLink.ConditionParams.Add(new DlgParam
            {
                Key = $"cond_key_{i}",
                Value = $"cond_value_{i}"
            });
        }
        dlg.StartingList.Add(startLink);

        var bytes = DlgWriter.Write(dlg);
        var reloaded = DlgReader.Read(bytes);

        Assert.Equal(10, reloaded.Entries[0].ActionParams.Count);
        Assert.Equal(10, reloaded.StartingList[0].ConditionParams.Count);

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal($"action_key_{i}", reloaded.Entries[0].ActionParams[i].Key);
            Assert.Equal($"action_value_{i}", reloaded.Entries[0].ActionParams[i].Value);
            Assert.Equal($"cond_key_{i}", reloaded.StartingList[0].ConditionParams[i].Key);
            Assert.Equal($"cond_value_{i}", reloaded.StartingList[0].ConditionParams[i].Value);
        }
    }

    #endregion
}

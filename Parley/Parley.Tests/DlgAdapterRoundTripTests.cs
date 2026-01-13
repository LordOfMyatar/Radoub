using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Dlg;
using Xunit;

namespace Parley.Tests;

/// <summary>
/// Tool-level round-trip tests for Parley dialog editing.
/// Verifies that the DlgAdapter correctly converts between DlgFile (parser)
/// and Dialog (UI model) without data loss.
/// </summary>
public class DlgAdapterRoundTripTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly DialogFileService _fileService;

    public DlgAdapterRoundTripTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParleyAdapterTests_{Guid.NewGuid()}");
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

    #region Basic Round-Trip Tests

    [Fact]
    public async Task RoundTrip_EmptyDialog_PreservesDefaults()
    {
        // Arrange
        var original = new Dialog();
        var filePath = Path.Combine(_testDirectory, "empty.dlg");

        // Act: Save and reload
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Empty(reloaded!.Entries);
        Assert.Empty(reloaded.Replies);
        Assert.Empty(reloaded.Starts);
    }

    [Fact]
    public async Task RoundTrip_DialogWithSingleEntry_PreservesAllFields()
    {
        // Arrange
        var original = new Dialog();
        var entry = original.CreateNode(DialogNodeType.Entry)!;
        entry.Speaker = "NPC_MERCHANT";
        entry.Text.Add(0, "Welcome to my shop!");
        entry.Animation = DialogAnimation.Greeting;
        entry.AnimationLoop = true;
        entry.ScriptAction = "on_speak";
        entry.Comment = "Opening line";
        entry.Sound = "vo_welcome";
        entry.Quest = "main_quest";
        entry.QuestEntry = 5;
        entry.Delay = 1000;
        entry.ActionParams["key1"] = "value1";

        original.AddNodeInternal(entry, DialogNodeType.Entry);
        var startPtr = original.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = entry;
        original.Starts.Add(startPtr);

        var filePath = Path.Combine(_testDirectory, "single_entry.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Entries);
        var e = reloaded.Entries[0];
        Assert.Equal("NPC_MERCHANT", e.Speaker);
        Assert.Equal("Welcome to my shop!", e.Text.GetDefault());
        Assert.Equal(DialogAnimation.Greeting, e.Animation);
        Assert.True(e.AnimationLoop);
        Assert.Equal("on_speak", e.ScriptAction);
        Assert.Equal("Opening line", e.Comment);
        Assert.Equal("vo_welcome", e.Sound);
        Assert.Equal("main_quest", e.Quest);
        Assert.Equal(5u, e.QuestEntry);
        Assert.Equal(1000u, e.Delay);
        Assert.Equal("value1", e.ActionParams["key1"]);
    }

    [Fact]
    public async Task RoundTrip_DialogWithReply_PreservesAllFields()
    {
        // Arrange
        var original = new Dialog();
        var entry = original.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "Hello");
        original.AddNodeInternal(entry, DialogNodeType.Entry);

        var reply = original.CreateNode(DialogNodeType.Reply)!;
        reply.Text.Add(0, "Goodbye");
        reply.Animation = DialogAnimation.Bow;
        reply.AnimationLoop = false;
        reply.ScriptAction = "on_reply";
        reply.Comment = "Player farewell";
        reply.Sound = "vo_bye";
        reply.Quest = "side_quest";
        reply.QuestEntry = 10;
        reply.ActionParams["param1"] = "data1";

        original.AddNodeInternal(reply, DialogNodeType.Reply);

        var filePath = Path.Combine(_testDirectory, "with_reply.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Replies);
        var r = reloaded.Replies[0];
        Assert.Equal("Goodbye", r.Text.GetDefault());
        Assert.Equal(DialogAnimation.Bow, r.Animation);
        Assert.False(r.AnimationLoop);
        Assert.Equal("on_reply", r.ScriptAction);
        Assert.Equal("Player farewell", r.Comment);
        Assert.Equal("vo_bye", r.Sound);
        Assert.Equal("side_quest", r.Quest);
        Assert.Equal(10u, r.QuestEntry);
        Assert.Equal("data1", r.ActionParams["param1"]);
    }

    #endregion

    #region Global Properties Tests

    [Fact]
    public async Task RoundTrip_DialogGlobalProperties_PreservesAll()
    {
        // Arrange
        var original = new Dialog
        {
            DelayEntry = 500,
            DelayReply = 300,
            NumWords = 42,
            ScriptEnd = "end_convo",
            ScriptAbort = "abort_convo",
            PreventZoom = true
        };

        var filePath = Path.Combine(_testDirectory, "global_props.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Equal(500u, reloaded!.DelayEntry);
        Assert.Equal(300u, reloaded.DelayReply);
        Assert.Equal(42u, reloaded.NumWords);
        Assert.Equal("end_convo", reloaded.ScriptEnd);
        Assert.Equal("abort_convo", reloaded.ScriptAbort);
        Assert.True(reloaded.PreventZoom);
    }

    #endregion

    #region Link Structure Tests

    [Fact]
    public async Task RoundTrip_EntryToReplyLink_PreservesLinks()
    {
        // Arrange
        var original = new Dialog();

        // Create entry and reply
        var entry = original.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "NPC asks a question");
        original.AddNodeInternal(entry, DialogNodeType.Entry);

        var reply = original.CreateNode(DialogNodeType.Reply)!;
        reply.Text.Add(0, "Player answers");
        original.AddNodeInternal(reply, DialogNodeType.Reply);

        // Link entry to reply
        var ptr = original.CreatePtr()!;
        ptr.Type = DialogNodeType.Reply;
        ptr.Index = 0;
        ptr.Node = reply;
        entry.Pointers.Add(ptr);

        // Add start pointer
        var start = original.CreatePtr()!;
        start.Type = DialogNodeType.Entry;
        start.Index = 0;
        start.Node = entry;
        original.Starts.Add(start);

        var filePath = Path.Combine(_testDirectory, "entry_reply_link.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Entries);
        Assert.Single(reloaded.Replies);
        Assert.Single(reloaded.Starts);
        // Entry should have a child linking to the reply
        Assert.Single(reloaded.Entries[0].Pointers);
        Assert.Equal(DialogNodeType.Reply, reloaded.Entries[0].Pointers[0].Type);
    }

    [Fact]
    public async Task RoundTrip_ConditionalStart_PreservesConditionScript()
    {
        // Arrange
        var original = new Dialog();
        var entry = original.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "Conditional greeting");
        original.AddNodeInternal(entry, DialogNodeType.Entry);

        var start = original.CreatePtr()!;
        start.Type = DialogNodeType.Entry;
        start.Index = 0;
        start.Node = entry;
        start.ScriptAppears = "check_condition";
        start.ConditionParams["test_key"] = "test_value";
        original.Starts.Add(start);

        var filePath = Path.Combine(_testDirectory, "conditional_start.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Starts);
        Assert.Equal("check_condition", reloaded.Starts[0].ScriptAppears);
        Assert.True(reloaded.Starts[0].ConditionParams.ContainsKey("test_key"));
        Assert.Equal("test_value", reloaded.Starts[0].ConditionParams["test_key"]);
    }

    [Fact]
    public async Task RoundTrip_IsLinkFlag_PreservesLinkStatus()
    {
        // Arrange: Create a dialog with a link (IsChild=true scenario)
        var original = new Dialog();

        // Create shared entry that gets linked to
        var sharedEntry = original.CreateNode(DialogNodeType.Entry)!;
        sharedEntry.Text.Add(0, "Shared response");
        original.AddNodeInternal(sharedEntry, DialogNodeType.Entry);

        // Create reply that links back to shared entry
        var reply = original.CreateNode(DialogNodeType.Reply)!;
        reply.Text.Add(0, "Player choice");
        original.AddNodeInternal(reply, DialogNodeType.Reply);

        // Create link pointer (IsLink=true)
        var linkPtr = original.CreatePtr()!;
        linkPtr.Type = DialogNodeType.Entry;
        linkPtr.Index = 0;
        linkPtr.Node = sharedEntry;
        linkPtr.IsLink = true;
        linkPtr.LinkComment = "Links back to shared response";
        reply.Pointers.Add(linkPtr);

        var filePath = Path.Combine(_testDirectory, "with_link.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Replies);
        Assert.Single(reloaded.Replies[0].Pointers);
        var reloadedLink = reloaded.Replies[0].Pointers[0];
        Assert.True(reloadedLink.IsLink);
        Assert.Equal("Links back to shared response", reloadedLink.LinkComment);
    }

    #endregion

    #region Multiple Nodes Tests

    [Fact]
    public async Task RoundTrip_MultipleEntries_PreservesOrder()
    {
        // Arrange
        var original = new Dialog();
        for (int i = 0; i < 5; i++)
        {
            var entry = original.CreateNode(DialogNodeType.Entry)!;
            entry.Text.Add(0, $"Entry number {i}");
            entry.Speaker = $"NPC_{i}";
            original.AddNodeInternal(entry, DialogNodeType.Entry);
        }

        var filePath = Path.Combine(_testDirectory, "multi_entry.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Equal(5, reloaded!.Entries.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal($"Entry number {i}", reloaded.Entries[i].Text.GetDefault());
            Assert.Equal($"NPC_{i}", reloaded.Entries[i].Speaker);
        }
    }

    [Fact]
    public async Task RoundTrip_MultipleReplies_PreservesOrder()
    {
        // Arrange
        var original = new Dialog();
        for (int i = 0; i < 3; i++)
        {
            var reply = original.CreateNode(DialogNodeType.Reply)!;
            reply.Text.Add(0, $"Reply option {i}");
            original.AddNodeInternal(reply, DialogNodeType.Reply);
        }

        var filePath = Path.Combine(_testDirectory, "multi_reply.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Equal(3, reloaded!.Replies.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal($"Reply option {i}", reloaded.Replies[i].Text.GetDefault());
        }
    }

    #endregion

    #region Complex Dialogs Tests

    [Fact]
    public async Task RoundTrip_BranchingDialog_PreservesStructure()
    {
        // Arrange: NPC greeting with 3 reply choices, each leading to different outcomes
        var original = new Dialog();

        // NPC greeting
        var greeting = original.CreateNode(DialogNodeType.Entry)!;
        greeting.Text.Add(0, "What can I help you with?");
        greeting.Speaker = "MERCHANT";
        original.AddNodeInternal(greeting, DialogNodeType.Entry);

        // Three reply options
        var replies = new DialogNode[3];
        for (int i = 0; i < 3; i++)
        {
            replies[i] = original.CreateNode(DialogNodeType.Reply)!;
            replies[i].Text.Add(0, $"Option {i + 1}");
            original.AddNodeInternal(replies[i], DialogNodeType.Reply);

            var replyPtr = original.CreatePtr()!;
            replyPtr.Type = DialogNodeType.Reply;
            replyPtr.Index = (uint)i;
            replyPtr.Node = replies[i];
            greeting.Pointers.Add(replyPtr);
        }

        // Start pointer
        var start = original.CreatePtr()!;
        start.Type = DialogNodeType.Entry;
        start.Index = 0;
        start.Node = greeting;
        original.Starts.Add(start);

        var filePath = Path.Combine(_testDirectory, "branching.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Entries);
        Assert.Equal(3, reloaded.Replies.Count);
        Assert.Equal(3, reloaded.Entries[0].Pointers.Count);
        Assert.Equal("MERCHANT", reloaded.Entries[0].Speaker);
    }

    [Fact]
    public async Task RoundTrip_DialogWithTlkStrRef_PreservesStrRef()
    {
        // Arrange: Dialog entry with TLK reference instead of inline text
        var original = new Dialog();
        var entry = original.CreateNode(DialogNodeType.Entry)!;
        entry.Text.StrRef = 12345; // Reference into TLK file
        original.AddNodeInternal(entry, DialogNodeType.Entry);

        var filePath = Path.Combine(_testDirectory, "tlk_ref.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Entries);
        Assert.Equal(12345u, reloaded.Entries[0].Text.StrRef);
    }

    #endregion

    #region Edit Operations Tests

    [Fact]
    public async Task RoundTrip_EditOnlyText_PreservesOtherFields()
    {
        // Arrange: Create dialog, save, load, edit text only, save again
        var original = new Dialog
        {
            DelayEntry = 100,
            ScriptEnd = "original_end"
        };
        var entry = original.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "Original text");
        entry.Speaker = "ORIGINAL_SPEAKER";
        entry.ScriptAction = "original_script";
        original.AddNodeInternal(entry, DialogNodeType.Entry);

        var filePath = Path.Combine(_testDirectory, "edit_test.dlg");
        await _fileService.SaveToFileAsync(original, filePath);

        // Act: Load, edit only the text, save
        var loaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(loaded);
        loaded!.Entries[0].Text.Add(0, "Modified text");
        await _fileService.SaveToFileAsync(loaded, filePath);

        // Reload and verify
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert: Text changed, everything else preserved
        Assert.NotNull(reloaded);
        Assert.Equal("Modified text", reloaded!.Entries[0].Text.GetDefault());
        Assert.Equal("ORIGINAL_SPEAKER", reloaded.Entries[0].Speaker);
        Assert.Equal("original_script", reloaded.Entries[0].ScriptAction);
        Assert.Equal(100u, reloaded.DelayEntry);
        Assert.Equal("original_end", reloaded.ScriptEnd);
    }

    [Fact]
    public async Task RoundTrip_AddNode_PreservesExistingNodes()
    {
        // Arrange: Create dialog with one entry
        var original = new Dialog();
        var entry1 = original.CreateNode(DialogNodeType.Entry)!;
        entry1.Text.Add(0, "First entry");
        entry1.Speaker = "NPC_1";
        original.AddNodeInternal(entry1, DialogNodeType.Entry);

        var filePath = Path.Combine(_testDirectory, "add_node_test.dlg");
        await _fileService.SaveToFileAsync(original, filePath);

        // Act: Load, add second entry, save
        var loaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(loaded);
        var entry2 = loaded!.CreateNode(DialogNodeType.Entry)!;
        entry2.Text.Add(0, "Second entry");
        entry2.Speaker = "NPC_2";
        loaded.AddNodeInternal(entry2, DialogNodeType.Entry);
        await _fileService.SaveToFileAsync(loaded, filePath);

        // Reload
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded!.Entries.Count);
        Assert.Equal("First entry", reloaded.Entries[0].Text.GetDefault());
        Assert.Equal("NPC_1", reloaded.Entries[0].Speaker);
        Assert.Equal("Second entry", reloaded.Entries[1].Text.GetDefault());
        Assert.Equal("NPC_2", reloaded.Entries[1].Speaker);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task RoundTrip_SpecialCharacters_PreservesText()
    {
        // Arrange
        var specialText = "Line1\nLine2\tTabbed \"Quoted\" <angle> & ampersand";
        var original = new Dialog();
        var entry = original.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, specialText);
        original.AddNodeInternal(entry, DialogNodeType.Entry);

        var filePath = Path.Combine(_testDirectory, "special_chars.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Equal(specialText, reloaded!.Entries[0].Text.GetDefault());
    }

    [Fact]
    public async Task RoundTrip_UnicodeText_PreservesText()
    {
        // Arrange
        var unicodeText = "Élémentaire! Café résumé naïve";
        var original = new Dialog();
        var entry = original.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, unicodeText);
        original.AddNodeInternal(entry, DialogNodeType.Entry);

        var filePath = Path.Combine(_testDirectory, "unicode.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Equal(unicodeText, reloaded!.Entries[0].Text.GetDefault());
    }

    [Fact]
    public async Task RoundTrip_LongText_PreservesFullText()
    {
        // Arrange
        var longText = new string('A', 5000);
        var original = new Dialog();
        var entry = original.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, longText);
        original.AddNodeInternal(entry, DialogNodeType.Entry);

        var filePath = Path.Combine(_testDirectory, "long_text.dlg");

        // Act
        await _fileService.SaveToFileAsync(original, filePath);
        var reloaded = await _fileService.LoadFromFileAsync(filePath);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Equal(5000, reloaded!.Entries[0].Text.GetDefault().Length);
        Assert.Equal(longText, reloaded.Entries[0].Text.GetDefault());
    }

    #endregion
}

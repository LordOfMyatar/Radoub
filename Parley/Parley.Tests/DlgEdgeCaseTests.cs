using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Dlg;
using Xunit;
using Xunit.Abstractions;

namespace Parley.Tests;

/// <summary>
/// Edge case tests for DLG files. Covers #1309 Edge Cases test area:
/// empty dialogs, single-entry, orphaned nodes, long strings, unicode,
/// and unusual but valid structures.
/// </summary>
public class DlgEdgeCaseTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDirectory;
    private readonly DialogFileService _fileService;

    public DlgEdgeCaseTests(ITestOutputHelper output)
    {
        _output = output;
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParleyEdge_{Guid.NewGuid()}");
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

    #region Empty and Minimal Dialogs

    [Fact]
    public async Task EmptyDialog_NoEntries_NoReplies_RoundTrips()
    {
        var dialog = new Dialog();
        var filePath = Path.Combine(_testDirectory, "empty.dlg");

        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);
        Assert.Empty(reloaded!.Entries);
        Assert.Empty(reloaded.Replies);
        Assert.Empty(reloaded.Starts);
    }

    [Fact]
    public async Task SingleEntry_NoReplies_RoundTrips()
    {
        var dialog = new Dialog();
        var entry = dialog.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "A lone NPC greeting with no PC responses.");
        entry.Speaker = "LONELY_NPC";
        dialog.AddNodeInternal(entry, DialogNodeType.Entry);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = entry;
        startPtr.IsStart = true;
        dialog.Starts.Add(startPtr);

        var filePath = Path.Combine(_testDirectory, "single.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Entries);
        Assert.Empty(reloaded.Replies);
        Assert.Single(reloaded.Starts);
        Assert.Equal("A lone NPC greeting with no PC responses.", reloaded.Entries[0].Text.GetDefault());
    }

    [Fact]
    public void FormatLevel_EmptyDialog_WritesAndReads()
    {
        var dlg = new DlgFile
        {
            FileType = "DLG ",
            FileVersion = "V3.2"
        };

        var bytes = DlgWriter.Write(dlg);
        Assert.True(bytes.Length > 0, "Empty dialog should still produce valid GFF");

        var reloaded = DlgReader.Read(bytes);
        Assert.Empty(reloaded.Entries);
        Assert.Empty(reloaded.Replies);
        Assert.Empty(reloaded.StartingList);
    }

    #endregion

    #region Text Edge Cases

    [Fact]
    public async Task LongText_5000Characters_PreservedExactly()
    {
        var dialog = new Dialog();
        var longText = new string('A', 5000);

        var entry = dialog.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, longText);
        dialog.AddNodeInternal(entry, DialogNodeType.Entry);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = entry;
        startPtr.IsStart = true;
        dialog.Starts.Add(startPtr);

        var filePath = Path.Combine(_testDirectory, "longtext.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);
        Assert.Equal(5000, reloaded!.Entries[0].Text.GetDefault().Length);
        Assert.Equal(longText, reloaded.Entries[0].Text.GetDefault());
    }

    [Fact]
    public async Task UnicodeText_AccentedCharacters_Preserved()
    {
        var dialog = new Dialog();
        var entry = dialog.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "Héllo, àdventürer! Ñice tö meet yoü. ¿Cómo estás?");
        dialog.AddNodeInternal(entry, DialogNodeType.Entry);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = entry;
        startPtr.IsStart = true;
        dialog.Starts.Add(startPtr);

        var filePath = Path.Combine(_testDirectory, "unicode.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);
        Assert.Equal("Héllo, àdventürer! Ñice tö meet yoü. ¿Cómo estás?",
            reloaded!.Entries[0].Text.GetDefault());
    }

    [Fact]
    public async Task SpecialCharacters_NewlinesTabsQuotes_Preserved()
    {
        var dialog = new Dialog();
        var entry = dialog.CreateNode(DialogNodeType.Entry)!;
        var text = "Line one\nLine two\nLine three\t\t(tabbed)\n\"Quoted\" and <tagged> & ampersand";
        entry.Text.Add(0, text);
        dialog.AddNodeInternal(entry, DialogNodeType.Entry);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = entry;
        startPtr.IsStart = true;
        dialog.Starts.Add(startPtr);

        var filePath = Path.Combine(_testDirectory, "special.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);
        Assert.Equal(text, reloaded!.Entries[0].Text.GetDefault());
    }

    [Fact]
    public async Task EmptyTextStrings_PreservedWithoutCorruption()
    {
        var dialog = new Dialog();
        var entry = dialog.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "");
        dialog.AddNodeInternal(entry, DialogNodeType.Entry);

        var reply = dialog.CreateNode(DialogNodeType.Reply)!;
        reply.Text.Add(0, "");
        dialog.AddNodeInternal(reply, DialogNodeType.Reply);

        var replyPtr = dialog.CreatePtr()!;
        replyPtr.Type = DialogNodeType.Reply;
        replyPtr.Node = reply;
        entry.Pointers.Add(replyPtr);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = entry;
        startPtr.IsStart = true;
        dialog.Starts.Add(startPtr);

        var filePath = Path.Combine(_testDirectory, "emptytext.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Entries);
        Assert.Single(reloaded.Replies);
    }

    [Fact]
    public async Task MultipleLanguages_AllPreserved()
    {
        var dialog = new Dialog();
        var entry = dialog.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "English text");     // English
        entry.Text.Add(1, "Texte français");   // French
        entry.Text.Add(2, "Texto español");    // Spanish (using lang ID 2)
        entry.Text.Add(4, "Deutscher Text");   // German
        dialog.AddNodeInternal(entry, DialogNodeType.Entry);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = entry;
        startPtr.IsStart = true;
        dialog.Starts.Add(startPtr);

        var filePath = Path.Combine(_testDirectory, "multilang.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);

        var strings = reloaded!.Entries[0].Text.GetAllStrings();
        Assert.Equal("English text", reloaded.Entries[0].Text.GetDefault());

        _output.WriteLine($"Languages preserved: {strings.Count}");
        Assert.True(strings.Count >= 4, $"Expected at least 4 language strings, got {strings.Count}");
    }

    #endregion

    #region Structural Edge Cases

    [Fact]
    public async Task OrphanedNodes_NotReachableFromStart_StillSerialized()
    {
        // Create nodes that exist in the dialog but aren't reachable from any start
        var dialog = new Dialog();

        // Reachable entry
        var reachable = dialog.CreateNode(DialogNodeType.Entry)!;
        reachable.Text.Add(0, "Reachable from start");
        dialog.AddNodeInternal(reachable, DialogNodeType.Entry);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = reachable;
        startPtr.IsStart = true;
        dialog.Starts.Add(startPtr);

        // Orphan entry — in Entries list but not linked from start
        var orphan = dialog.CreateNode(DialogNodeType.Entry)!;
        orphan.Text.Add(0, "Orphan entry - not reachable");
        orphan.Speaker = "GHOST";
        dialog.AddNodeInternal(orphan, DialogNodeType.Entry);

        // Orphan reply
        var orphanReply = dialog.CreateNode(DialogNodeType.Reply)!;
        orphanReply.Text.Add(0, "Orphan reply");
        dialog.AddNodeInternal(orphanReply, DialogNodeType.Reply);

        Assert.Equal(2, dialog.Entries.Count);
        Assert.Single(dialog.Replies);

        var filePath = Path.Combine(_testDirectory, "orphaned.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);

        // All nodes should be serialized, even orphans
        Assert.Equal(2, reloaded!.Entries.Count);
        Assert.Single(reloaded.Replies);
        Assert.Single(reloaded.Starts);
    }

    [Fact]
    public async Task EntryWithNoReplies_LeafNode_RoundTrips()
    {
        // Entry that is a dead-end (conversation just stops)
        var dialog = new Dialog();

        var entry = dialog.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "Farewell. The conversation ends here.");
        dialog.AddNodeInternal(entry, DialogNodeType.Entry);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = entry;
        startPtr.IsStart = true;
        dialog.Starts.Add(startPtr);

        var filePath = Path.Combine(_testDirectory, "deadend.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Entries);
        Assert.Empty(reloaded.Entries[0].Pointers);
    }

    [Fact]
    public async Task ReplyWithNoEntries_PCEndsBranch_RoundTrips()
    {
        // PC reply that doesn't link to any further NPC entry
        var dialog = new Dialog();

        var entry = dialog.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "What say you?");
        dialog.AddNodeInternal(entry, DialogNodeType.Entry);

        var reply = dialog.CreateNode(DialogNodeType.Reply)!;
        reply.Text.Add(0, "Goodbye forever.");
        dialog.AddNodeInternal(reply, DialogNodeType.Reply);

        var replyPtr = dialog.CreatePtr()!;
        replyPtr.Type = DialogNodeType.Reply;
        replyPtr.Node = reply;
        entry.Pointers.Add(replyPtr);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = entry;
        startPtr.IsStart = true;
        dialog.Starts.Add(startPtr);

        var filePath = Path.Combine(_testDirectory, "pcends.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);
        Assert.Empty(reloaded!.Replies[0].Pointers);
    }

    [Fact]
    public void FormatLevel_TlkStrRef_Preserved()
    {
        // Entry with StrRef pointing to TLK (no inline text)
        var dlg = new DlgFile
        {
            FileType = "DLG ",
            FileVersion = "V3.2"
        };

        var entry = new DlgEntry();
        entry.Text.StrRef = 12345; // TLK reference
        // No localized strings — text comes from TLK
        dlg.Entries.Add(entry);
        dlg.StartingList.Add(new DlgLink { Index = 0, IsChild = false });

        var bytes = DlgWriter.Write(dlg);
        var reloaded = DlgReader.Read(bytes);

        Assert.Equal(12345u, reloaded.Entries[0].Text.StrRef);
    }

    [Fact]
    public async Task EmptyStrings_AllFieldsEmpty_NoCorruption()
    {
        // Dialog where all string fields are explicitly empty
        var dialog = new Dialog
        {
            ScriptEnd = "",
            ScriptAbort = ""
        };

        var entry = dialog.CreateNode(DialogNodeType.Entry)!;
        entry.Speaker = "";
        entry.ScriptAction = "";
        entry.Sound = "";
        entry.Comment = "";
        entry.Quest = "";
        entry.Text.Add(0, "");
        dialog.AddNodeInternal(entry, DialogNodeType.Entry);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Node = entry;
        startPtr.IsStart = true;
        startPtr.ScriptAppears = "";
        dialog.Starts.Add(startPtr);

        var filePath = Path.Combine(_testDirectory, "allempty.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, filePath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Entries);
    }

    #endregion
}

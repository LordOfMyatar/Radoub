using System.Text.Json;
using Radoub.Dictionary.Models;
using Xunit;

namespace Radoub.Dictionary.Tests;

/// <summary>
/// Tests for cross-instance / cross-process custom-word persistence (#2263).
///
/// The bug: two writers each hold their own in-memory word set and save the whole
/// file with last-writer-wins semantics, so a word added by writer A is silently
/// dropped when writer B saves. The fix routes all writes through a single
/// read-merge-write + atomic save path so concurrent writers' words both survive.
///
/// Each <see cref="UserDictionaryService"/> instance built with the internal
/// custom-path constructor models a separate process: separate HashSet, same files.
/// </summary>
public class UserDictionaryServiceConcurrencyTests : IDisposable
{
    private readonly string _dir;
    private readonly string _jsonPath;
    private readonly string _textPath;

    public UserDictionaryServiceConcurrencyTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"RadoubDictConc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _jsonPath = Path.Combine(_dir, "custom.dic");
        _textPath = Path.Combine(_dir, "custom_dictionary.txt");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { }
    }

    private UserDictionaryService NewInstance() => new(_jsonPath, _textPath);

    private List<string> ReadWordsFromDisk()
    {
        var json = File.ReadAllText(_jsonPath);
        var dict = JsonSerializer.Deserialize<CustomDictionary>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return dict!.AllWords.ToList();
    }

    [Fact]
    public void AddWord_RoundTripsThroughDisk()
    {
        var a = NewInstance();
        a.AddWord("Foobar");

        var reloaded = NewInstance();
        Assert.Contains("Foobar", reloaded.Words);
    }

    [Fact]
    public void TwoWriters_BothWordsSurvive_NoLastWriterWins()
    {
        // Model two tool processes that both started before either saved.
        var writerA = NewInstance();
        var writerB = NewInstance();

        writerA.AddWord("Foobar");   // A saves {Foobar}
        writerB.AddWord("Bazquux");  // B must merge with disk, save {Foobar, Bazquux}

        var onDisk = ReadWordsFromDisk();
        Assert.Contains("Foobar", onDisk);   // <-- dropped today (B clobbers A)
        Assert.Contains("Bazquux", onDisk);
    }

    [Fact]
    public void Save_MergesExternalWordsAddedSinceLoad()
    {
        var writer = NewInstance();   // loaded empty

        // Another process writes a word to the file after `writer` loaded.
        var external = new CustomDictionary { Words = new List<string> { "External" } };
        File.WriteAllText(_jsonPath, JsonSerializer.Serialize(external,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        writer.AddWord("Local");      // save must not clobber "External"

        var onDisk = ReadWordsFromDisk();
        Assert.Contains("External", onDisk);
        Assert.Contains("Local", onDisk);
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        var writer = NewInstance();
        writer.AddWord("Cleanup");

        var leftovers = Directory.GetFiles(_dir, "*.tmp");
        Assert.Empty(leftovers);
    }

    [Fact]
    public void Save_ProducesValidJson_NotTruncated()
    {
        var writer = NewInstance();
        writer.AddWord("ValidJsonCheck");

        // Must parse cleanly — a half-written file would throw here.
        var dict = JsonSerializer.Deserialize<CustomDictionary>(
            File.ReadAllText(_jsonPath),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(dict);
        Assert.Contains("ValidJsonCheck", dict!.AllWords);
    }
}

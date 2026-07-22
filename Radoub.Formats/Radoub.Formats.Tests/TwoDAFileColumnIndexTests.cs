using Radoub.Formats.TwoDA;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// #2580: GetColumnIndex is on the hot path (one call per GetValue-by-name). These pin its
/// correctness and confirm the lookup cache stays consistent when Columns is mutated.
/// </summary>
public class TwoDAFileColumnIndexTests
{
    private static TwoDAFile MakeFile(params string[] columns)
    {
        var file = new TwoDAFile();
        file.Columns.AddRange(columns);
        return file;
    }

    [Fact]
    public void GetColumnIndex_FindsColumn_CaseInsensitive()
    {
        var file = MakeFile("FeatIndex", "List", "GrantedOnLevel");

        Assert.Equal(0, file.GetColumnIndex("FeatIndex"));
        Assert.Equal(1, file.GetColumnIndex("list"));
        Assert.Equal(2, file.GetColumnIndex("GRANTEDONLEVEL"));
    }

    [Fact]
    public void GetColumnIndex_MissingColumn_ReturnsMinusOne()
    {
        var file = MakeFile("FeatIndex", "List");

        Assert.Equal(-1, file.GetColumnIndex("Nonexistent"));
    }

    [Fact]
    public void GetColumnIndex_RepeatedLookups_ReturnSameIndex()
    {
        var file = MakeFile("A", "B", "C");

        // Second and later calls must agree with the first (cache must not corrupt results).
        Assert.Equal(1, file.GetColumnIndex("B"));
        Assert.Equal(1, file.GetColumnIndex("B"));
        Assert.Equal(2, file.GetColumnIndex("c"));
    }

    [Fact]
    public void HasColumn_ReflectsColumnPresence()
    {
        var file = MakeFile("A", "B");

        Assert.True(file.HasColumn("a"));
        Assert.False(file.HasColumn("Z"));
    }

    [Fact]
    public void GetColumnIndex_DuplicateColumnName_ReturnsFirstOccurrence()
    {
        var file = MakeFile("A", "B", "A");

        // Matches the original linear scan, which returned the first match.
        Assert.Equal(0, file.GetColumnIndex("A"));
    }

    [Fact]
    public void GetColumnIndex_AfterColumnsChange_ReflectsNewLayout()
    {
        var file = MakeFile("A", "B");
        Assert.Equal(1, file.GetColumnIndex("B")); // prime any cache

        // A parser that reuses the instance and rebuilds Columns must not get stale indices.
        file.Columns.Clear();
        file.Columns.AddRange(new[] { "X", "B", "A" });

        Assert.Equal(2, file.GetColumnIndex("A"));
        Assert.Equal(1, file.GetColumnIndex("B"));
        Assert.Equal(-1, file.GetColumnIndex("C"));
    }
}

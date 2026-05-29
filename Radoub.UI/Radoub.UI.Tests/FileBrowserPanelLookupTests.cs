using Radoub.UI.Controls;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for FileBrowserPanelBase.FindEntryByFilePath — pure-logic lookup
/// used by host tools (Relique etc.) to locate a browser row after save
/// so RefreshEntryMetadataAsync can re-read Tag/DisplayLabel without a
/// full reindex (#2199).
/// </summary>
public class FileBrowserPanelLookupTests
{
    [Fact]
    public void FindEntryByFilePath_ExactMatch_ReturnsEntry()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "sword", FilePath = @"C:\mod\sword.uti" },
            new() { Name = "shield", FilePath = @"C:\mod\shield.uti" }
        };

        var match = FileBrowserPanelBase.FindEntryByFilePath(entries, @"C:\mod\sword.uti");

        Assert.NotNull(match);
        Assert.Equal("sword", match!.Name);
    }

    [Fact]
    public void FindEntryByFilePath_CaseInsensitive()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "sword", FilePath = @"C:\mod\sword.uti" }
        };

        var match = FileBrowserPanelBase.FindEntryByFilePath(entries, @"c:\MOD\Sword.UTI");

        Assert.NotNull(match);
        Assert.Equal("sword", match!.Name);
    }

    [Fact]
    public void FindEntryByFilePath_NoMatch_ReturnsNull()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "sword", FilePath = @"C:\mod\sword.uti" }
        };

        Assert.Null(FileBrowserPanelBase.FindEntryByFilePath(entries, @"C:\mod\missing.uti"));
    }

    [Fact]
    public void FindEntryByFilePath_EmptyList_ReturnsNull()
    {
        var entries = new List<FileBrowserEntry>();

        Assert.Null(FileBrowserPanelBase.FindEntryByFilePath(entries, @"C:\mod\sword.uti"));
    }

    [Fact]
    public void FindEntryByFilePath_NullOrEmptyPath_ReturnsNull()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "sword", FilePath = @"C:\mod\sword.uti" }
        };

        Assert.Null(FileBrowserPanelBase.FindEntryByFilePath(entries, null!));
        Assert.Null(FileBrowserPanelBase.FindEntryByFilePath(entries, string.Empty));
    }

    [Fact]
    public void FindEntryByFilePath_SkipsHakBifEntriesWithoutFilePath()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "sword", FilePath = null, IsFromHak = true, HakPath = "items.hak" },
            new() { Name = "sword", FilePath = @"C:\mod\sword.uti" }
        };

        var match = FileBrowserPanelBase.FindEntryByFilePath(entries, @"C:\mod\sword.uti");

        Assert.NotNull(match);
        Assert.False(match!.IsFromHak);
    }

    // #2285 — RemoveEntryByFilePath is called from the rename flow to drop the
    // stale pre-rename row before the host triggers a panel refresh.

    [Fact]
    public void RemoveEntryByFilePath_ExactMatch_RemovesAndReturnsTrue()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "old_name", FilePath = @"C:\mod\old_name.utc" },
            new() { Name = "other", FilePath = @"C:\mod\other.utc" }
        };

        var removed = FileBrowserPanelBase.RemoveEntryByFilePath(entries, @"C:\mod\old_name.utc");

        Assert.True(removed);
        Assert.Single(entries);
        Assert.Equal("other", entries[0].Name);
    }

    [Fact]
    public void RemoveEntryByFilePath_CaseInsensitive()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "x", FilePath = @"C:\mod\x.utc" }
        };

        var removed = FileBrowserPanelBase.RemoveEntryByFilePath(entries, @"c:\MOD\X.UTC");

        Assert.True(removed);
        Assert.Empty(entries);
    }

    [Fact]
    public void RemoveEntryByFilePath_NoMatch_ReturnsFalse_ListUnchanged()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "x", FilePath = @"C:\mod\x.utc" }
        };

        var removed = FileBrowserPanelBase.RemoveEntryByFilePath(entries, @"C:\mod\missing.utc");

        Assert.False(removed);
        Assert.Single(entries);
    }

    [Fact]
    public void RemoveEntryByFilePath_NullOrEmpty_ReturnsFalse()
    {
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "x", FilePath = @"C:\mod\x.utc" }
        };

        Assert.False(FileBrowserPanelBase.RemoveEntryByFilePath(entries, null!));
        Assert.False(FileBrowserPanelBase.RemoveEntryByFilePath(entries, string.Empty));
        Assert.Single(entries);
    }

    [Fact]
    public void RemoveEntryByFilePath_SkipsHakBifEntriesWithoutFilePath()
    {
        // HAK/BIF rows share names with module entries but have null FilePath.
        // The remove must not collapse a HAK row when given a module path.
        var entries = new List<FileBrowserEntry>
        {
            new() { Name = "same_name", FilePath = null, IsFromHak = true, HakPath = "x.hak" },
            new() { Name = "same_name", FilePath = @"C:\mod\same_name.utc" }
        };

        var removed = FileBrowserPanelBase.RemoveEntryByFilePath(entries, @"C:\mod\same_name.utc");

        Assert.True(removed);
        Assert.Single(entries);
        Assert.True(entries[0].IsFromHak);
    }
}

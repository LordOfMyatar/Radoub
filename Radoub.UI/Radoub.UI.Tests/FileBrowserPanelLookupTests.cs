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
}

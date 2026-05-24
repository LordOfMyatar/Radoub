using System.IO;
using Radoub.Formats.Uti;
using Radoub.UI.Controls;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for ItemBrowserPanel.RefreshEntryFromDiskAsync — the save-flow hook
/// that re-reads Tag/DisplayLabel from a UTI on disk after the host tool
/// (Relique) overwrites it (#2199 Sprint 2).
/// </summary>
public class ItemBrowserPanelRefreshTests
{
    private static byte[] BuildUti(string resRef, string tag, string name)
    {
        var uti = new UtiFile
        {
            TemplateResRef = resRef,
            Tag = tag
        };
        uti.LocalizedName.SetString(0, name);
        return UtiWriter.Write(uti);
    }

    [Fact]
    public async Task RefreshEntryFromDiskAsync_RereadsUpdatedTagAndName()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, BuildUti("sword01", "OLD_TAG", "Old Name"));
            var entry = new ItemBrowserEntry
            {
                Name = "sword01",
                FilePath = tempFile,
                Tag = "OLD_TAG",
                DisplayLabel = "Old Name",
                MetadataLoaded = true
            };

            File.WriteAllBytes(tempFile, BuildUti("sword01", "NEW_TAG", "Enchanted Sword"));

            await ItemBrowserPanel.RefreshEntryFromDiskAsync(entry);

            Assert.Equal("NEW_TAG", entry.Tag);
            Assert.Equal("Enchanted Sword", entry.DisplayLabel);
            Assert.True(entry.MetadataLoaded);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RefreshEntryFromDiskAsync_NullFilePath_NoOp()
    {
        var entry = new ItemBrowserEntry
        {
            Name = "hak_item",
            FilePath = null,
            Tag = "FROM_CACHE",
            DisplayLabel = "From Cache",
            MetadataLoaded = true
        };

        await ItemBrowserPanel.RefreshEntryFromDiskAsync(entry);

        Assert.Equal("FROM_CACHE", entry.Tag);
        Assert.Equal("From Cache", entry.DisplayLabel);
    }

    [Fact]
    public async Task RefreshEntryFromDiskAsync_MissingFile_NoOp()
    {
        var entry = new ItemBrowserEntry
        {
            Name = "ghost",
            FilePath = @"C:\nonexistent\ghost.uti",
            Tag = "PRIOR_TAG",
            DisplayLabel = "Prior Name",
            MetadataLoaded = true
        };

        await ItemBrowserPanel.RefreshEntryFromDiskAsync(entry);

        Assert.Equal("PRIOR_TAG", entry.Tag);
        Assert.Equal("Prior Name", entry.DisplayLabel);
    }

    [Fact]
    public async Task RefreshEntryFromDiskAsync_CorruptFile_EntryUnchanged()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
            var entry = new ItemBrowserEntry
            {
                Name = "corrupt",
                FilePath = tempFile,
                Tag = "PRIOR_TAG",
                DisplayLabel = "Prior Name",
                MetadataLoaded = true
            };

            await ItemBrowserPanel.RefreshEntryFromDiskAsync(entry);

            Assert.Equal("PRIOR_TAG", entry.Tag);
            Assert.Equal("Prior Name", entry.DisplayLabel);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

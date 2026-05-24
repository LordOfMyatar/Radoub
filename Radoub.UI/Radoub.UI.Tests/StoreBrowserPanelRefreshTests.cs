using System.IO;
using Radoub.Formats.Utm;
using Radoub.UI.Controls;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for StoreBrowserPanel.RefreshEntryFromDiskAsync — the save-flow hook
/// that re-reads Tag/DisplayLabel from a UTM on disk after the host tool
/// (Fence) overwrites it (#2186 Sprint 3 / #2200).
/// </summary>
public class StoreBrowserPanelRefreshTests
{
    private static byte[] BuildUtm(string resRef, string tag, string name)
    {
        var utm = new UtmFile
        {
            ResRef = resRef,
            Tag = tag
        };
        utm.LocName.SetString(0, name);
        return UtmWriter.Write(utm);
    }

    [Fact]
    public async Task RefreshEntryFromDiskAsync_RereadsUpdatedTagAndName()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, BuildUtm("store01", "OLD_TAG", "Old Store"));
            var entry = new StoreBrowserEntry
            {
                Name = "store01",
                FilePath = tempFile,
                Tag = "OLD_TAG",
                DisplayLabel = "Old Store",
                MetadataLoaded = true
            };

            File.WriteAllBytes(tempFile, BuildUtm("store01", "NEW_TAG", "Royal Merchant"));

            await StoreBrowserPanel.RefreshEntryFromDiskAsync(entry);

            Assert.Equal("NEW_TAG", entry.Tag);
            Assert.Equal("Royal Merchant", entry.DisplayLabel);
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
        var entry = new StoreBrowserEntry
        {
            Name = "hak_store",
            FilePath = null,
            Tag = "FROM_CACHE",
            DisplayLabel = "From Cache",
            MetadataLoaded = true
        };

        await StoreBrowserPanel.RefreshEntryFromDiskAsync(entry);

        Assert.Equal("FROM_CACHE", entry.Tag);
        Assert.Equal("From Cache", entry.DisplayLabel);
    }

    [Fact]
    public async Task RefreshEntryFromDiskAsync_MissingFile_NoOp()
    {
        var entry = new StoreBrowserEntry
        {
            Name = "ghost",
            FilePath = @"C:\nonexistent\ghost.utm",
            Tag = "PRIOR_TAG",
            DisplayLabel = "Prior Name",
            MetadataLoaded = true
        };

        await StoreBrowserPanel.RefreshEntryFromDiskAsync(entry);

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
            var entry = new StoreBrowserEntry
            {
                Name = "corrupt",
                FilePath = tempFile,
                Tag = "PRIOR_TAG",
                DisplayLabel = "Prior Name",
                MetadataLoaded = true
            };

            await StoreBrowserPanel.RefreshEntryFromDiskAsync(entry);

            Assert.Equal("PRIOR_TAG", entry.Tag);
            Assert.Equal("Prior Name", entry.DisplayLabel);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

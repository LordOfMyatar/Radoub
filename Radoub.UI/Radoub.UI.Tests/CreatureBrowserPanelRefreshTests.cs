using System.IO;
using Radoub.Formats.Bic;
using Radoub.Formats.Utc;
using Radoub.UI.Controls;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for CreatureBrowserPanel.RefreshEntryFromDiskAsync — the save-flow
/// hook that re-reads Tag/DisplayLabel from a UTC on disk after the host tool
/// (Quartermaster) overwrites it (#2186 Sprint 4 / #2201).
/// </summary>
public class CreatureBrowserPanelRefreshTests
{
    private static byte[] BuildUtc(string resRef, string tag, string firstName, string lastName)
    {
        var utc = new UtcFile
        {
            TemplateResRef = resRef,
            Tag = tag
        };
        utc.FirstName.SetString(0, firstName);
        utc.LastName.SetString(0, lastName);
        return UtcWriter.Write(utc);
    }

    [Fact]
    public async Task RefreshEntryFromDiskAsync_RereadsUpdatedTagAndName()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, BuildUtc("orc01", "OLD_TAG", "Orog", "Stoneskull"));
            var entry = new CreatureBrowserEntry
            {
                Name = "orc01",
                FilePath = tempFile,
                Tag = "OLD_TAG",
                DisplayLabel = "Orog Stoneskull",
                MetadataLoaded = true
            };

            File.WriteAllBytes(tempFile, BuildUtc("orc01", "NEW_TAG", "Grommash", "Hellscream"));

            await CreatureBrowserPanel.RefreshEntryFromDiskAsync(entry);

            Assert.Equal("NEW_TAG", entry.Tag);
            Assert.Equal("Grommash Hellscream", entry.DisplayLabel);
            Assert.True(entry.MetadataLoaded);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RefreshEntryFromDiskAsync_FirstNameOnly_DisplayLabelTrimmed()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, BuildUtc("villager", "TAG", "Bartholomew", string.Empty));
            var entry = new CreatureBrowserEntry
            {
                Name = "villager",
                FilePath = tempFile
            };

            await CreatureBrowserPanel.RefreshEntryFromDiskAsync(entry);

            Assert.Equal("Bartholomew", entry.DisplayLabel);
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
        var entry = new CreatureBrowserEntry
        {
            Name = "hak_creature",
            FilePath = null,
            Tag = "FROM_CACHE",
            DisplayLabel = "From Cache",
            MetadataLoaded = true
        };

        await CreatureBrowserPanel.RefreshEntryFromDiskAsync(entry);

        Assert.Equal("FROM_CACHE", entry.Tag);
        Assert.Equal("From Cache", entry.DisplayLabel);
    }

    [Fact]
    public async Task RefreshEntryFromDiskAsync_MissingFile_NoOp()
    {
        var entry = new CreatureBrowserEntry
        {
            Name = "ghost",
            FilePath = @"C:\nonexistent\ghost.utc",
            Tag = "PRIOR_TAG",
            DisplayLabel = "Prior Name",
            MetadataLoaded = true
        };

        await CreatureBrowserPanel.RefreshEntryFromDiskAsync(entry);

        Assert.Equal("PRIOR_TAG", entry.Tag);
        Assert.Equal("Prior Name", entry.DisplayLabel);
    }

    [Fact]
    public async Task RefreshEntryFromDiskAsync_BicFile_ReadsFirstAndLastName()
    {
        // Vault rows are BIC files. BIC has FileType "BIC " — UtcReader
        // would reject it. Verify the GFF-direct path picks up Tag and the
        // FirstName/LastName CExoLocStrings.
        var tempFile = Path.GetTempFileName();
        try
        {
            var bic = new BicFile { Tag = "PLAYER_TAG" };
            bic.FirstName.SetString(0, "Aragorn");
            bic.LastName.SetString(0, "Elessar");
            File.WriteAllBytes(tempFile, BicWriter.Write(bic));

            var entry = new CreatureBrowserEntry
            {
                Name = "hero",
                FilePath = tempFile,
                IsBic = true
            };

            await CreatureBrowserPanel.RefreshEntryFromDiskAsync(entry);

            Assert.Equal("PLAYER_TAG", entry.Tag);
            Assert.Equal("Aragorn Elessar", entry.DisplayLabel);
            Assert.True(entry.MetadataLoaded);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RefreshEntryFromDiskAsync_CorruptFile_EntryUnchanged()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
            var entry = new CreatureBrowserEntry
            {
                Name = "corrupt",
                FilePath = tempFile,
                Tag = "PRIOR_TAG",
                DisplayLabel = "Prior Name",
                MetadataLoaded = true
            };

            await CreatureBrowserPanel.RefreshEntryFromDiskAsync(entry);

            Assert.Equal("PRIOR_TAG", entry.Tag);
            Assert.Equal("Prior Name", entry.DisplayLabel);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

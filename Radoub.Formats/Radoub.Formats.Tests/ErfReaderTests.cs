using System.Text;
using Radoub.Formats.Erf;
using Xunit;

namespace Radoub.Formats.Tests;

public class ErfReaderTests
{
    [Theory]
    [InlineData("ERF ")]
    [InlineData("HAK ")]
    [InlineData("MOD ")]
    [InlineData("SAV ")]
    [InlineData("NWM ")]
    public void Read_ValidFileTypes_ParsesCorrectly(string fileType)
    {
        var buffer = CreateMinimalErfFile(fileType);

        var result = ErfReader.Read(buffer);

        Assert.Equal(fileType, result.FileType);
        Assert.Equal("V1.0", result.FileVersion);
    }

    [Fact]
    public void Read_MinimalErfFile_ParsesCorrectly()
    {
        var buffer = CreateMinimalErfFile("ERF ");

        var result = ErfReader.Read(buffer);

        Assert.Equal("ERF ", result.FileType);
        Assert.Equal("V1.0", result.FileVersion);
        Assert.Empty(result.LocalizedStrings);
        Assert.Empty(result.Resources);
    }

    [Fact]
    public void Read_ErfWithLocalizedString_ParsesString()
    {
        var buffer = CreateErfWithLocalizedString("Test description", languageId: 0);

        var result = ErfReader.Read(buffer);

        Assert.Single(result.LocalizedStrings);
        Assert.Equal("Test description", result.LocalizedStrings[0].Text);
        Assert.Equal(0u, result.LocalizedStrings[0].LanguageId);
    }

    [Fact]
    public void Read_ErfWithResource_ParsesResource()
    {
        var resourceData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var buffer = CreateErfWithResource("testres", resourceType: 2029, resourceData);

        var result = ErfReader.Read(buffer);

        Assert.Single(result.Resources);
        var res = result.Resources[0];
        Assert.Equal("testres", res.ResRef);
        Assert.Equal(2029, res.ResourceType); // DLG type
        Assert.Equal(4u, res.Size);
    }

    [Fact]
    public void Read_InvalidFileType_ThrowsException()
    {
        var buffer = new byte[160];
        Encoding.ASCII.GetBytes("XXXX").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1.0").CopyTo(buffer, 4);

        var ex = Assert.Throws<InvalidDataException>(() => ErfReader.Read(buffer));
        Assert.Contains("Invalid ERF file type", ex.Message);
    }

    [Fact]
    public void Read_InvalidVersion_ThrowsException()
    {
        var buffer = new byte[160];
        Encoding.ASCII.GetBytes("ERF ").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V2.0").CopyTo(buffer, 4);

        var ex = Assert.Throws<InvalidDataException>(() => ErfReader.Read(buffer));
        Assert.Contains("Unsupported ERF version", ex.Message);
    }

    [Fact]
    public void Read_FileTooSmall_ThrowsException()
    {
        var buffer = new byte[100]; // Too small for header (160 bytes)

        var ex = Assert.Throws<InvalidDataException>(() => ErfReader.Read(buffer));
        Assert.Contains("too small", ex.Message);
    }

    [Fact]
    public void Read_InvalidResourceType_SkipsEntry()
    {
        // Create ERF with one invalid (0xFFFF) and one valid resource
        var buffer = CreateErfWithResources(new[]
        {
            ("invalid", (ushort)0xFFFF, new byte[] { 0x00 }),
            ("valid", (ushort)2029, new byte[] { 0x01 })
        });

        var result = ErfReader.Read(buffer);

        // Should only have the valid resource
        Assert.Single(result.Resources);
        Assert.Equal("valid", result.Resources[0].ResRef);
    }

    [Fact]
    public void FindResource_ExistingResource_ReturnsEntry()
    {
        var erf = new ErfFile();
        erf.Resources.Add(new ErfResourceEntry { ResRef = "test", ResourceType = 2029 });
        erf.Resources.Add(new ErfResourceEntry { ResRef = "other", ResourceType = 2009 });

        var result = erf.FindResource("test", 2029);

        Assert.NotNull(result);
        Assert.Equal("test", result.ResRef);
    }

    [Fact]
    public void FindResource_CaseInsensitive()
    {
        var erf = new ErfFile();
        erf.Resources.Add(new ErfResourceEntry { ResRef = "TestRes", ResourceType = 2029 });

        var result = erf.FindResource("TESTRES", 2029);

        Assert.NotNull(result);
    }

    [Fact]
    public void FindResource_NotFound_ReturnsNull()
    {
        var erf = new ErfFile();
        erf.Resources.Add(new ErfResourceEntry { ResRef = "test", ResourceType = 2029 });

        var result = erf.FindResource("notfound", 2029);

        Assert.Null(result);
    }

    [Fact]
    public void GetResourcesByType_ReturnsMatchingResources()
    {
        var erf = new ErfFile();
        erf.Resources.Add(new ErfResourceEntry { ResRef = "dlg1", ResourceType = 2029 });
        erf.Resources.Add(new ErfResourceEntry { ResRef = "script1", ResourceType = 2009 });
        erf.Resources.Add(new ErfResourceEntry { ResRef = "dlg2", ResourceType = 2029 });

        var dlgResources = erf.GetResourcesByType(2029).ToList();

        Assert.Equal(2, dlgResources.Count);
        Assert.All(dlgResources, r => Assert.Equal((ushort)2029, r.ResourceType));
    }

    [Fact]
    public void IsHak_HakFile_ReturnsTrue()
    {
        var erf = new ErfFile { FileType = "HAK " };
        Assert.True(erf.IsHak);
        Assert.False(erf.IsMod);
    }

    [Fact]
    public void IsMod_ModFile_ReturnsTrue()
    {
        var erf = new ErfFile { FileType = "MOD " };
        Assert.True(erf.IsMod);
        Assert.False(erf.IsHak);
    }

    [Fact]
    public void LocalizedString_LanguageAndGender_DecodeCorrectly()
    {
        // English (0) masculine = 0*2+0 = 0
        var english = new ErfLocalizedString { LanguageId = 0 };
        Assert.Equal(0, english.Language);
        Assert.Equal(0, english.Gender);

        // French (1) feminine = 1*2+1 = 3
        var french = new ErfLocalizedString { LanguageId = 3 };
        Assert.Equal(1, french.Language);
        Assert.Equal(1, french.Gender);

        // German (2) masculine = 2*2+0 = 4
        var german = new ErfLocalizedString { LanguageId = 4 };
        Assert.Equal(2, german.Language);
        Assert.Equal(0, german.Gender);
    }

    [Fact]
    public void ExtractResource_ValidBuffer_ReturnsData()
    {
        var resourceData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var buffer = CreateErfWithResource("test", 2029, resourceData);

        var erf = ErfReader.Read(buffer);
        var extracted = ErfReader.ExtractResource(buffer, erf.Resources[0]);

        Assert.Equal(resourceData, extracted);
    }

    [Fact]
    public void Read_BuildDateFields_ParseCorrectly()
    {
        var buffer = CreateMinimalErfFile("ERF ");
        // Set build year to 125 (2025) and build day to 100
        BitConverter.GetBytes(125u).CopyTo(buffer, 32);
        BitConverter.GetBytes(100u).CopyTo(buffer, 36);

        var result = ErfReader.Read(buffer);

        Assert.Equal(125u, result.BuildYear);
        Assert.Equal(100u, result.BuildDay);
    }

    [Fact]
    public void Read_DescriptionStrRef_ParsesCorrectly()
    {
        var buffer = CreateMinimalErfFile("ERF ");
        BitConverter.GetBytes(12345u).CopyTo(buffer, 40);

        var result = ErfReader.Read(buffer);

        Assert.Equal(12345u, result.DescriptionStrRef);
    }

    #region Test Helpers

    private static byte[] CreateMinimalErfFile(string fileType)
    {
        var buffer = new byte[160];
        Encoding.ASCII.GetBytes(fileType).CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1.0").CopyTo(buffer, 4);
        // All counts/offsets are 0, which is valid for empty ERF
        BitConverter.GetBytes(160u).CopyTo(buffer, 20); // Offset to localized strings (at end of header)
        BitConverter.GetBytes(160u).CopyTo(buffer, 24); // Offset to key list
        BitConverter.GetBytes(160u).CopyTo(buffer, 28); // Offset to resource list
        return buffer;
    }

    private static byte[] CreateErfWithLocalizedString(string text, uint languageId)
    {
        var textBytes = Encoding.UTF8.GetBytes(text);
        var stringEntrySize = 8 + textBytes.Length; // LanguageID (4) + StringSize (4) + text

        var totalSize = 160 + stringEntrySize;
        var buffer = new byte[totalSize];

        // Header
        Encoding.ASCII.GetBytes("ERF ").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1.0").CopyTo(buffer, 4);
        BitConverter.GetBytes(1u).CopyTo(buffer, 8);   // LanguageCount = 1
        BitConverter.GetBytes((uint)stringEntrySize).CopyTo(buffer, 12); // LocalizedStringSize
        BitConverter.GetBytes(0u).CopyTo(buffer, 16);  // EntryCount = 0
        BitConverter.GetBytes(160u).CopyTo(buffer, 20); // Offset to localized strings
        BitConverter.GetBytes((uint)totalSize).CopyTo(buffer, 24); // Offset to key list (at end)
        BitConverter.GetBytes((uint)totalSize).CopyTo(buffer, 28); // Offset to resource list (at end)

        // Localized string entry at offset 160
        BitConverter.GetBytes(languageId).CopyTo(buffer, 160);
        BitConverter.GetBytes((uint)textBytes.Length).CopyTo(buffer, 164);
        textBytes.CopyTo(buffer, 168);

        return buffer;
    }

    private static byte[] CreateErfWithResource(string resRef, ushort resourceType, byte[] resourceData)
    {
        return CreateErfWithResources(new[] { (resRef, resourceType, resourceData) });
    }

    private static byte[] CreateErfWithResources((string resRef, ushort resourceType, byte[] data)[] resources)
    {
        var keyListSize = resources.Length * 24; // 24 bytes per key entry
        var resourceListSize = resources.Length * 8; // 8 bytes per resource entry
        var totalDataSize = resources.Sum(r => r.data.Length);

        var locStrOffset = 160;
        var keyListOffset = locStrOffset; // No localized strings
        var resourceListOffset = keyListOffset + keyListSize;
        var dataOffset = resourceListOffset + resourceListSize;
        var totalSize = dataOffset + totalDataSize;

        var buffer = new byte[totalSize];

        // Header
        Encoding.ASCII.GetBytes("ERF ").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1.0").CopyTo(buffer, 4);
        BitConverter.GetBytes(0u).CopyTo(buffer, 8);   // LanguageCount = 0
        BitConverter.GetBytes(0u).CopyTo(buffer, 12);  // LocalizedStringSize = 0
        BitConverter.GetBytes((uint)resources.Length).CopyTo(buffer, 16);
        BitConverter.GetBytes((uint)locStrOffset).CopyTo(buffer, 20);
        BitConverter.GetBytes((uint)keyListOffset).CopyTo(buffer, 24);
        BitConverter.GetBytes((uint)resourceListOffset).CopyTo(buffer, 28);

        // Write key entries and resource entries
        var currentDataOffset = dataOffset;
        for (int i = 0; i < resources.Length; i++)
        {
            var (resRef, resourceType, data) = resources[i];

            // Key entry (24 bytes)
            var keyOffset = keyListOffset + (i * 24);
            var resRefBytes = Encoding.ASCII.GetBytes(resRef.PadRight(16, '\0'));
            resRefBytes.CopyTo(buffer, keyOffset);
            BitConverter.GetBytes((uint)i).CopyTo(buffer, keyOffset + 16); // ResID
            BitConverter.GetBytes(resourceType).CopyTo(buffer, keyOffset + 20);
            // Bytes 22-23: Unused

            // Resource list entry (8 bytes)
            var resOffset = resourceListOffset + (i * 8);
            BitConverter.GetBytes((uint)currentDataOffset).CopyTo(buffer, resOffset);
            BitConverter.GetBytes((uint)data.Length).CopyTo(buffer, resOffset + 4);

            // Resource data
            data.CopyTo(buffer, currentDataOffset);
            currentDataOffset += data.Length;
        }

        return buffer;
    }

    #endregion
}

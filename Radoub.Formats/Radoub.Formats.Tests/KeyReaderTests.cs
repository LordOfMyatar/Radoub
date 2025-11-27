using System.Text;
using Radoub.Formats.Key;
using Xunit;

namespace Radoub.Formats.Tests;

public class KeyReaderTests
{
    [Fact]
    public void Read_ValidMinimalKeyFile_ParsesCorrectly()
    {
        // Create a minimal valid KEY file with no BIF entries and no key entries
        var buffer = CreateMinimalKeyFile(bifCount: 0, keyCount: 0);

        var result = KeyReader.Read(buffer);

        Assert.Equal("KEY ", result.FileType);
        Assert.Equal("V1  ", result.FileVersion);
        Assert.Empty(result.BifEntries);
        Assert.Empty(result.ResourceEntries);
    }

    [Fact]
    public void Read_KeyFileWithOneBif_ParsesBifEntry()
    {
        // Create KEY file with one BIF entry
        var bifFilename = "data\\test.bif";
        var buffer = CreateKeyFileWithBif(bifFilename, fileSize: 12345);

        var result = KeyReader.Read(buffer);

        Assert.Single(result.BifEntries);
        var bif = result.BifEntries[0];
        Assert.Equal(12345u, bif.FileSize);
        Assert.Contains("test.bif", bif.Filename);
    }

    [Fact]
    public void Read_KeyFileWithResourceEntry_ParsesResourceEntry()
    {
        // Create KEY file with one resource entry
        var buffer = CreateKeyFileWithResource("testres", resourceType: 2029, bifIndex: 0, varIndex: 42);

        var result = KeyReader.Read(buffer);

        Assert.Single(result.ResourceEntries);
        var res = result.ResourceEntries[0];
        Assert.Equal("testres", res.ResRef);
        Assert.Equal(2029, res.ResourceType); // DLG type
        Assert.Equal(0, res.BifIndex);
        Assert.Equal(42, res.VariableTableIndex);
    }

    [Fact]
    public void Read_InvalidFileType_ThrowsException()
    {
        var buffer = new byte[64];
        Encoding.ASCII.GetBytes("XXXX").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1  ").CopyTo(buffer, 4);

        var ex = Assert.Throws<InvalidDataException>(() => KeyReader.Read(buffer));
        Assert.Contains("Invalid KEY file type", ex.Message);
    }

    [Fact]
    public void Read_InvalidVersion_ThrowsException()
    {
        var buffer = new byte[64];
        Encoding.ASCII.GetBytes("KEY ").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V2  ").CopyTo(buffer, 4);

        var ex = Assert.Throws<InvalidDataException>(() => KeyReader.Read(buffer));
        Assert.Contains("Unsupported KEY version", ex.Message);
    }

    [Fact]
    public void Read_FileTooSmall_ThrowsException()
    {
        var buffer = new byte[32]; // Too small for header

        var ex = Assert.Throws<InvalidDataException>(() => KeyReader.Read(buffer));
        Assert.Contains("too small", ex.Message);
    }

    [Fact]
    public void KeyResourceEntry_BifIndex_ExtractsCorrectly()
    {
        // ResId = (bifIndex << 20) | varIndex
        // bifIndex = 5, varIndex = 100
        var resId = (5u << 20) | 100u;

        var entry = new KeyResourceEntry { ResId = resId };

        Assert.Equal(5, entry.BifIndex);
        Assert.Equal(100, entry.VariableTableIndex);
    }

    [Fact]
    public void FindResource_ExistingResource_ReturnsEntry()
    {
        var key = new KeyFile();
        key.ResourceEntries.Add(new KeyResourceEntry { ResRef = "test", ResourceType = 2029 });
        key.ResourceEntries.Add(new KeyResourceEntry { ResRef = "other", ResourceType = 2009 });

        var result = key.FindResource("test", 2029);

        Assert.NotNull(result);
        Assert.Equal("test", result.ResRef);
    }

    [Fact]
    public void FindResource_CaseInsensitive()
    {
        var key = new KeyFile();
        key.ResourceEntries.Add(new KeyResourceEntry { ResRef = "TestRes", ResourceType = 2029 });

        var result = key.FindResource("TESTRES", 2029);

        Assert.NotNull(result);
    }

    [Fact]
    public void GetResourcesByType_ReturnsMatchingResources()
    {
        var key = new KeyFile();
        key.ResourceEntries.Add(new KeyResourceEntry { ResRef = "dlg1", ResourceType = 2029 });
        key.ResourceEntries.Add(new KeyResourceEntry { ResRef = "script1", ResourceType = 2009 });
        key.ResourceEntries.Add(new KeyResourceEntry { ResRef = "dlg2", ResourceType = 2029 });

        var dlgResources = key.GetResourcesByType(2029).ToList();

        Assert.Equal(2, dlgResources.Count);
        Assert.All(dlgResources, r => Assert.Equal((ushort)2029, r.ResourceType));
    }

    #region Test Helpers

    private static byte[] CreateMinimalKeyFile(uint bifCount, uint keyCount)
    {
        var buffer = new byte[64];
        Encoding.ASCII.GetBytes("KEY ").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1  ").CopyTo(buffer, 4);
        BitConverter.GetBytes(bifCount).CopyTo(buffer, 8);
        BitConverter.GetBytes(keyCount).CopyTo(buffer, 12);
        BitConverter.GetBytes(64u).CopyTo(buffer, 16); // File table offset
        BitConverter.GetBytes(64u).CopyTo(buffer, 20); // Key table offset
        return buffer;
    }

    private static byte[] CreateKeyFileWithBif(string filename, uint fileSize)
    {
        var filenameBytes = Encoding.ASCII.GetBytes(filename);

        // Calculate sizes
        var headerSize = 64;
        var bifEntrySize = 12;
        var fileTableOffset = headerSize;
        var filenameTableOffset = headerSize + bifEntrySize;
        var totalSize = filenameTableOffset + filenameBytes.Length + 1; // +1 for null terminator

        var buffer = new byte[totalSize];

        // Header
        Encoding.ASCII.GetBytes("KEY ").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1  ").CopyTo(buffer, 4);
        BitConverter.GetBytes(1u).CopyTo(buffer, 8);  // 1 BIF
        BitConverter.GetBytes(0u).CopyTo(buffer, 12); // 0 keys
        BitConverter.GetBytes((uint)fileTableOffset).CopyTo(buffer, 16);
        BitConverter.GetBytes((uint)totalSize).CopyTo(buffer, 20); // Key table at end

        // BIF entry
        BitConverter.GetBytes(fileSize).CopyTo(buffer, fileTableOffset);
        BitConverter.GetBytes((uint)filenameTableOffset).CopyTo(buffer, fileTableOffset + 4);
        BitConverter.GetBytes((ushort)filenameBytes.Length).CopyTo(buffer, fileTableOffset + 8);
        BitConverter.GetBytes((ushort)1).CopyTo(buffer, fileTableOffset + 10); // Drives

        // Filename
        filenameBytes.CopyTo(buffer, filenameTableOffset);

        return buffer;
    }

    private static byte[] CreateKeyFileWithResource(string resRef, ushort resourceType, int bifIndex, int varIndex)
    {
        var headerSize = 64;
        var keyEntrySize = 22;
        var keyTableOffset = headerSize;
        var totalSize = headerSize + keyEntrySize;

        var buffer = new byte[totalSize];

        // Header
        Encoding.ASCII.GetBytes("KEY ").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1  ").CopyTo(buffer, 4);
        BitConverter.GetBytes(0u).CopyTo(buffer, 8);  // 0 BIFs
        BitConverter.GetBytes(1u).CopyTo(buffer, 12); // 1 key
        BitConverter.GetBytes((uint)headerSize).CopyTo(buffer, 16); // File table at header end
        BitConverter.GetBytes((uint)keyTableOffset).CopyTo(buffer, 20);

        // Key entry
        var resRefBytes = Encoding.ASCII.GetBytes(resRef.PadRight(16, '\0'));
        resRefBytes.CopyTo(buffer, keyTableOffset);
        BitConverter.GetBytes(resourceType).CopyTo(buffer, keyTableOffset + 16);
        var resId = ((uint)bifIndex << 20) | (uint)varIndex;
        BitConverter.GetBytes(resId).CopyTo(buffer, keyTableOffset + 18);

        return buffer;
    }

    #endregion
}

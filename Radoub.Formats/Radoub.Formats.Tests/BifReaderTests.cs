using System.Text;
using Radoub.Formats.Bif;
using Xunit;

namespace Radoub.Formats.Tests;

public class BifReaderTests
{
    [Fact]
    public void Read_ValidMinimalBifFile_ParsesCorrectly()
    {
        var buffer = CreateMinimalBifFile(variableCount: 0, fixedCount: 0);

        var result = BifReader.Read(buffer);

        Assert.Equal("BIFF", result.FileType);
        Assert.Equal("V1  ", result.FileVersion);
        Assert.Empty(result.VariableResources);
        Assert.Empty(result.FixedResources);
    }

    [Fact]
    public void Read_BifWithVariableResource_ParsesResourceEntry()
    {
        var buffer = CreateBifWithVariableResource(
            id: 42,
            offset: 100,
            fileSize: 500,
            resourceType: 2029);

        var result = BifReader.Read(buffer);

        Assert.Single(result.VariableResources);
        var res = result.VariableResources[0];
        Assert.Equal(42u, res.Id);
        Assert.Equal(100u, res.Offset);
        Assert.Equal(500u, res.FileSize);
        Assert.Equal(2029u, res.ResourceType);
    }

    [Fact]
    public void Read_BifWithFixedResource_ParsesResourceEntry()
    {
        var buffer = CreateBifWithFixedResource(
            id: 10,
            offset: 200,
            partCount: 4,
            partSize: 128,
            resourceType: 3);

        var result = BifReader.Read(buffer);

        Assert.Single(result.FixedResources);
        var res = result.FixedResources[0];
        Assert.Equal(10u, res.Id);
        Assert.Equal(200u, res.Offset);
        Assert.Equal(4u, res.PartCount);
        Assert.Equal(128u, res.PartSize);
        Assert.Equal(512u, res.TotalSize); // 4 * 128
        Assert.Equal(3u, res.ResourceType);
    }

    [Fact]
    public void Read_InvalidFileType_ThrowsException()
    {
        var buffer = new byte[20];
        Encoding.ASCII.GetBytes("XXXX").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1  ").CopyTo(buffer, 4);

        var ex = Assert.Throws<InvalidDataException>(() => BifReader.Read(buffer));
        Assert.Contains("Invalid BIF file type", ex.Message);
    }

    [Fact]
    public void Read_InvalidVersion_ThrowsException()
    {
        var buffer = new byte[20];
        Encoding.ASCII.GetBytes("BIFF").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V2  ").CopyTo(buffer, 4);

        var ex = Assert.Throws<InvalidDataException>(() => BifReader.Read(buffer));
        Assert.Contains("Unsupported BIF version", ex.Message);
    }

    [Fact]
    public void Read_FileTooSmall_ThrowsException()
    {
        var buffer = new byte[10]; // Too small for header

        var ex = Assert.Throws<InvalidDataException>(() => BifReader.Read(buffer));
        Assert.Contains("too small", ex.Message);
    }

    [Fact]
    public void ExtractVariableResource_ValidResource_ReturnsData()
    {
        // Create a BIF with actual resource data
        var resourceData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var buffer = CreateBifWithResourceData(resourceData);

        var bif = BifReader.Read(buffer, keepBuffer: true);
        var extracted = bif.ExtractVariableResource(0);

        Assert.NotNull(extracted);
        Assert.Equal(resourceData, extracted);
    }

    [Fact]
    public void ExtractVariableResource_NoBuffer_ReturnsNull()
    {
        var buffer = CreateBifWithVariableResource(id: 1, offset: 20, fileSize: 10, resourceType: 2029);

        var bif = BifReader.Read(buffer, keepBuffer: false);
        var extracted = bif.ExtractVariableResource(0);

        Assert.Null(extracted);
    }

    [Fact]
    public void ExtractVariableResource_InvalidIndex_ReturnsNull()
    {
        var buffer = CreateMinimalBifFile(variableCount: 0, fixedCount: 0);

        var bif = BifReader.Read(buffer, keepBuffer: true);
        var extracted = bif.ExtractVariableResource(99);

        Assert.Null(extracted);
    }

    [Fact]
    public void GetVariableResource_ValidIndex_ReturnsResource()
    {
        var buffer = CreateBifWithVariableResource(id: 42, offset: 100, fileSize: 500, resourceType: 2029);

        var bif = BifReader.Read(buffer);
        var resource = bif.GetVariableResource(0);

        Assert.NotNull(resource);
        Assert.Equal(42u, resource.Id);
    }

    [Fact]
    public void GetVariableResource_InvalidIndex_ReturnsNull()
    {
        var buffer = CreateMinimalBifFile(variableCount: 0, fixedCount: 0);

        var bif = BifReader.Read(buffer);
        var resource = bif.GetVariableResource(0);

        Assert.Null(resource);
    }

    [Fact]
    public void BifVariableResource_VariableTableIndex_ExtractsCorrectly()
    {
        // ID format: top 12 bits = BIF index (ignored in BIF), bottom 20 bits = var index
        var id = (3u << 20) | 42u;

        var resource = new BifVariableResource { Id = id };

        Assert.Equal(42, resource.VariableTableIndex);
    }

    #region Test Helpers

    private static byte[] CreateMinimalBifFile(uint variableCount, uint fixedCount)
    {
        var headerSize = 20;
        var buffer = new byte[headerSize];

        Encoding.ASCII.GetBytes("BIFF").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1  ").CopyTo(buffer, 4);
        BitConverter.GetBytes(variableCount).CopyTo(buffer, 8);
        BitConverter.GetBytes(fixedCount).CopyTo(buffer, 12);
        BitConverter.GetBytes((uint)headerSize).CopyTo(buffer, 16); // Variable table offset

        return buffer;
    }

    private static byte[] CreateBifWithVariableResource(uint id, uint offset, uint fileSize, uint resourceType)
    {
        var headerSize = 20;
        var variableEntrySize = 16;
        var totalSize = headerSize + variableEntrySize;
        var buffer = new byte[totalSize];

        // Header
        Encoding.ASCII.GetBytes("BIFF").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1  ").CopyTo(buffer, 4);
        BitConverter.GetBytes(1u).CopyTo(buffer, 8);  // 1 variable resource
        BitConverter.GetBytes(0u).CopyTo(buffer, 12); // 0 fixed resources
        BitConverter.GetBytes((uint)headerSize).CopyTo(buffer, 16);

        // Variable resource entry
        BitConverter.GetBytes(id).CopyTo(buffer, headerSize);
        BitConverter.GetBytes(offset).CopyTo(buffer, headerSize + 4);
        BitConverter.GetBytes(fileSize).CopyTo(buffer, headerSize + 8);
        BitConverter.GetBytes(resourceType).CopyTo(buffer, headerSize + 12);

        return buffer;
    }

    private static byte[] CreateBifWithFixedResource(uint id, uint offset, uint partCount, uint partSize, uint resourceType)
    {
        var headerSize = 20;
        var fixedEntrySize = 20;
        var totalSize = headerSize + fixedEntrySize;
        var buffer = new byte[totalSize];

        // Header
        Encoding.ASCII.GetBytes("BIFF").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1  ").CopyTo(buffer, 4);
        BitConverter.GetBytes(0u).CopyTo(buffer, 8);  // 0 variable resources
        BitConverter.GetBytes(1u).CopyTo(buffer, 12); // 1 fixed resource
        BitConverter.GetBytes((uint)headerSize).CopyTo(buffer, 16); // Variable table offset (empty)

        // Fixed resource entry (follows variable table)
        BitConverter.GetBytes(id).CopyTo(buffer, headerSize);
        BitConverter.GetBytes(offset).CopyTo(buffer, headerSize + 4);
        BitConverter.GetBytes(partCount).CopyTo(buffer, headerSize + 8);
        BitConverter.GetBytes(partSize).CopyTo(buffer, headerSize + 12);
        BitConverter.GetBytes(resourceType).CopyTo(buffer, headerSize + 16);

        return buffer;
    }

    private static byte[] CreateBifWithResourceData(byte[] resourceData)
    {
        var headerSize = 20;
        var variableEntrySize = 16;
        var dataOffset = headerSize + variableEntrySize;
        var totalSize = dataOffset + resourceData.Length;
        var buffer = new byte[totalSize];

        // Header
        Encoding.ASCII.GetBytes("BIFF").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1  ").CopyTo(buffer, 4);
        BitConverter.GetBytes(1u).CopyTo(buffer, 8);
        BitConverter.GetBytes(0u).CopyTo(buffer, 12);
        BitConverter.GetBytes((uint)headerSize).CopyTo(buffer, 16);

        // Variable resource entry pointing to data
        BitConverter.GetBytes(0u).CopyTo(buffer, headerSize);           // ID
        BitConverter.GetBytes((uint)dataOffset).CopyTo(buffer, headerSize + 4);  // Offset
        BitConverter.GetBytes((uint)resourceData.Length).CopyTo(buffer, headerSize + 8); // Size
        BitConverter.GetBytes(2029u).CopyTo(buffer, headerSize + 12);   // Type (DLG)

        // Resource data
        resourceData.CopyTo(buffer, dataOffset);

        return buffer;
    }

    #endregion
}

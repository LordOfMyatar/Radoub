using System.Text;
using Radoub.Formats.Gff;
using Xunit;

namespace Radoub.Formats.Tests;

public class GffReaderTests
{
    [Fact]
    public void Read_ValidMinimalGffFile_ParsesCorrectly()
    {
        var buffer = CreateMinimalGffFile();

        var result = GffReader.Read(buffer);

        Assert.Equal("TST ", result.FileType);
        Assert.Equal("V3.2", result.FileVersion);
        Assert.NotNull(result.RootStruct);
    }

    [Fact]
    public void Read_GffWithSimpleFields_ParsesAllTypes()
    {
        var buffer = CreateGffWithByteField();

        var result = GffReader.Read(buffer);

        Assert.NotNull(result.RootStruct);
        Assert.Single(result.RootStruct.Fields);

        // Check BYTE field
        var byteField = result.RootStruct.GetField("ByteField");
        Assert.NotNull(byteField);
        Assert.Equal(GffField.BYTE, byteField.Type);
        Assert.Equal((byte)42, byteField.Value);
    }

    [Fact]
    public void Read_GffWithCExoString_ParsesString()
    {
        var buffer = CreateGffWithString("Hello, GFF!");

        var result = GffReader.Read(buffer);

        var stringField = result.RootStruct.GetField("TestString");
        Assert.NotNull(stringField);
        Assert.Equal(GffField.CExoString, stringField.Type);
        Assert.Equal("Hello, GFF!", stringField.Value);
    }

    [Fact]
    public void Read_GffWithCResRef_ParsesResRef()
    {
        var buffer = CreateGffWithResRef("testresref");

        var result = GffReader.Read(buffer);

        var resrefField = result.RootStruct.GetField("TestResRef");
        Assert.NotNull(resrefField);
        Assert.Equal(GffField.CResRef, resrefField.Type);
        Assert.Equal("testresref", resrefField.Value);
    }

    [Fact]
    public void Read_GffWithCExoLocString_ParsesLocalizedString()
    {
        var buffer = CreateGffWithLocString("English text", 0);

        var result = GffReader.Read(buffer);

        var locStringField = result.RootStruct.GetField("TestLocString");
        Assert.NotNull(locStringField);
        Assert.Equal(GffField.CExoLocString, locStringField.Type);

        var locString = locStringField.Value as CExoLocString;
        Assert.NotNull(locString);
        Assert.Equal("English text", locString.GetDefault());
    }

    [Fact]
    public void Read_GffWithList_ParsesListElements()
    {
        var buffer = CreateGffWithList(3);

        var result = GffReader.Read(buffer);

        var listField = result.RootStruct.GetField("TestList");
        Assert.NotNull(listField);
        Assert.Equal(GffField.List, listField.Type);

        var list = listField.Value as GffList;
        Assert.NotNull(list);
        Assert.Equal(3u, list.Count);
        Assert.Equal(3, list.Elements.Count);
    }

    [Fact]
    public void Read_InvalidVersion_ThrowsException()
    {
        var buffer = new byte[56];
        Encoding.ASCII.GetBytes("TST ").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1.0").CopyTo(buffer, 4);

        var ex = Assert.Throws<InvalidDataException>(() => GffReader.Read(buffer));
        Assert.Contains("Unsupported GFF version", ex.Message);
    }

    [Fact]
    public void Read_FileTooSmall_ThrowsException()
    {
        var buffer = new byte[32];

        var ex = Assert.Throws<InvalidDataException>(() => GffReader.Read(buffer));
        Assert.Contains("too small", ex.Message);
    }

    [Fact]
    public void ParseHeader_ValidHeader_ExtractsAllFields()
    {
        var buffer = CreateMinimalGffFile();

        var header = GffReader.ParseHeader(buffer);

        Assert.Equal("TST ", header.FileType);
        Assert.Equal("V3.2", header.FileVersion);
        Assert.Equal(56u, header.StructOffset);
        Assert.True(header.StructCount >= 1);
    }

    [Fact]
    public void GffStruct_GetFieldValue_ReturnsTypedValue()
    {
        var s = new GffStruct();
        s.Fields.Add(new GffField { Label = "TestInt", Type = GffField.DWORD, Value = 999u });

        var value = s.GetFieldValue<uint>("TestInt");

        Assert.Equal(999u, value);
    }

    [Fact]
    public void GffStruct_GetFieldValue_ReturnsDefault_WhenNotFound()
    {
        var s = new GffStruct();

        var value = s.GetFieldValue<uint>("NonExistent", 123u);

        Assert.Equal(123u, value);
    }

    [Fact]
    public void RoundTrip_SimpleGff_PreservesData()
    {
        var buffer = CreateGffWithByteField();

        var gff = GffReader.Read(buffer);
        var written = GffWriter.Write(gff);
        var gff2 = GffReader.Read(written);

        Assert.Equal(gff.FileType, gff2.FileType);
        Assert.Equal(gff.FileVersion, gff2.FileVersion);
        Assert.Equal(gff.RootStruct.Fields.Count, gff2.RootStruct.Fields.Count);
    }

    [Fact]
    public void CExoLocString_GetDefault_ReturnsEnglishFirst()
    {
        var locString = new CExoLocString();
        locString.LocalizedStrings[0] = "English";
        locString.LocalizedStrings[2] = "French";

        Assert.Equal("English", locString.GetDefault());
    }

    [Fact]
    public void CExoLocString_GetDefault_ReturnsFallback_WhenNoEnglish()
    {
        var locString = new CExoLocString();
        locString.LocalizedStrings[2] = "French";

        Assert.Equal("French", locString.GetDefault());
    }

    [Fact]
    public void CExoLocString_IsEmpty_TrueWhenNoData()
    {
        var locString = new CExoLocString();

        Assert.True(locString.IsEmpty);
    }

    [Fact]
    public void GffFieldType_IsSimpleType_IdentifiesCorrectly()
    {
        Assert.True(GffField.BYTE.IsSimpleType());
        Assert.True(GffField.DWORD.IsSimpleType());
        Assert.True(GffField.FLOAT.IsSimpleType());
        Assert.False(GffField.CExoString.IsSimpleType());
        Assert.False(GffField.List.IsSimpleType());
    }

    [Fact]
    public void GffFieldType_GetTypeName_ReturnsCorrectNames()
    {
        Assert.Equal("BYTE", GffField.BYTE.GetTypeName());
        Assert.Equal("CExoString", GffField.CExoString.GetTypeName());
        Assert.Equal("List", GffField.List.GetTypeName());
    }

    #region Test Helpers

    private static byte[] CreateMinimalGffFile()
    {
        // Minimal GFF with one empty root struct
        var buffer = new byte[68]; // 56 header + 12 struct
        var offset = 0;

        // Header
        Encoding.ASCII.GetBytes("TST ").CopyTo(buffer, offset); offset += 4;
        Encoding.ASCII.GetBytes("V3.2").CopyTo(buffer, offset); offset += 4;

        // Struct offset and count
        BitConverter.GetBytes(56u).CopyTo(buffer, offset); offset += 4; // StructOffset
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;  // StructCount

        // Field offset and count (no fields)
        BitConverter.GetBytes(68u).CopyTo(buffer, offset); offset += 4; // FieldOffset
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;  // FieldCount

        // Label offset and count (no labels)
        BitConverter.GetBytes(68u).CopyTo(buffer, offset); offset += 4; // LabelOffset
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;  // LabelCount

        // FieldData offset and count
        BitConverter.GetBytes(68u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        // FieldIndices offset and count
        BitConverter.GetBytes(68u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        // ListIndices offset and count
        BitConverter.GetBytes(68u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        // Root struct (type=0, data=0, fieldcount=0)
        offset = 56;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        return buffer;
    }

    private static byte[] CreateGffWithByteField()
    {
        // GFF with one BYTE field (value = 42)
        var label = "ByteField";
        var labelData = new byte[16];
        Encoding.ASCII.GetBytes(label).CopyTo(labelData, 0);

        uint structOffset = 56;
        uint fieldOffset = structOffset + 12;
        uint labelOffset = fieldOffset + 12;
        uint fieldDataOffset = labelOffset + 16;

        var buffer = new byte[fieldDataOffset];
        var offset = 0;

        // Header
        Encoding.ASCII.GetBytes("TST ").CopyTo(buffer, offset); offset += 4;
        Encoding.ASCII.GetBytes("V3.2").CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(structOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(labelOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        // Root struct (1 field, direct index)
        offset = (int)structOffset;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;  // Field index 0
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        // Field (BYTE=42)
        offset = (int)fieldOffset;
        BitConverter.GetBytes(GffField.BYTE).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(42u).CopyTo(buffer, offset); offset += 4;

        // Label
        offset = (int)labelOffset;
        labelData.CopyTo(buffer, offset);

        return buffer;
    }

    private static byte[] CreateGffWithString(string text)
    {
        var textBytes = Encoding.UTF8.GetBytes(text);
        var label = "TestString";
        var labelData = new byte[16];
        Encoding.ASCII.GetBytes(label).CopyTo(labelData, 0);

        uint structOffset = 56;
        uint fieldOffset = structOffset + 12;
        uint labelOffset = fieldOffset + 12;
        uint fieldDataOffset = labelOffset + 16;
        uint fieldDataCount = 4u + (uint)textBytes.Length;

        var buffer = new byte[fieldDataOffset + fieldDataCount];
        var offset = 0;

        // Header
        Encoding.ASCII.GetBytes("TST ").CopyTo(buffer, offset); offset += 4;
        Encoding.ASCII.GetBytes("V3.2").CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(structOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(labelOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(fieldDataCount).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset + fieldDataCount).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset + fieldDataCount).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        // Struct (1 field, direct index)
        offset = (int)structOffset;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;  // Field index 0
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        // Field (CExoString)
        offset = (int)fieldOffset;
        BitConverter.GetBytes(GffField.CExoString).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;  // Offset into field data

        // Label
        offset = (int)labelOffset;
        labelData.CopyTo(buffer, offset);

        // Field data: length + string
        offset = (int)fieldDataOffset;
        BitConverter.GetBytes((uint)textBytes.Length).CopyTo(buffer, offset); offset += 4;
        textBytes.CopyTo(buffer, offset);

        return buffer;
    }

    private static byte[] CreateGffWithResRef(string resref)
    {
        var resrefBytes = Encoding.ASCII.GetBytes(resref);
        var label = "TestResRef";
        var labelData = new byte[16];
        Encoding.ASCII.GetBytes(label).CopyTo(labelData, 0);

        uint structOffset = 56;
        uint fieldOffset = structOffset + 12;
        uint labelOffset = fieldOffset + 12;
        uint fieldDataOffset = labelOffset + 16;
        uint fieldDataCount = 1u + (uint)resrefBytes.Length;

        var buffer = new byte[fieldDataOffset + fieldDataCount];
        var offset = 0;

        // Header
        Encoding.ASCII.GetBytes("TST ").CopyTo(buffer, offset); offset += 4;
        Encoding.ASCII.GetBytes("V3.2").CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(structOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(labelOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(fieldDataCount).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset + fieldDataCount).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset + fieldDataCount).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        // Struct
        offset = (int)structOffset;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        // Field
        offset = (int)fieldOffset;
        BitConverter.GetBytes(GffField.CResRef).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        // Label
        offset = (int)labelOffset;
        labelData.CopyTo(buffer, offset);

        // Field data: length byte + string
        offset = (int)fieldDataOffset;
        buffer[offset++] = (byte)resrefBytes.Length;
        resrefBytes.CopyTo(buffer, offset);

        return buffer;
    }

    private static byte[] CreateGffWithLocString(string text, uint languageId)
    {
        var textBytes = Encoding.UTF8.GetBytes(text);
        var label = "TestLocString";
        var labelData = new byte[16];
        Encoding.ASCII.GetBytes(label).CopyTo(labelData, 0);

        // CExoLocString: TotalSize(4) + StrRef(4) + SubCount(4) + [LangId(4) + Length(4) + Text]
        var locStringSize = 12 + 8 + textBytes.Length;

        uint structOffset = 56;
        uint fieldOffset = structOffset + 12;
        uint labelOffset = fieldOffset + 12;
        uint fieldDataOffset = labelOffset + 16;
        uint fieldDataCount = (uint)locStringSize;

        var buffer = new byte[fieldDataOffset + fieldDataCount];
        var offset = 0;

        // Header
        Encoding.ASCII.GetBytes("TST ").CopyTo(buffer, offset); offset += 4;
        Encoding.ASCII.GetBytes("V3.2").CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(structOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(labelOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(fieldDataCount).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset + fieldDataCount).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset + fieldDataCount).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        // Struct
        offset = (int)structOffset;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        // Field
        offset = (int)fieldOffset;
        BitConverter.GetBytes(GffField.CExoLocString).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        // Label
        offset = (int)labelOffset;
        labelData.CopyTo(buffer, offset);

        // Field data: CExoLocString
        offset = (int)fieldDataOffset;
        BitConverter.GetBytes((uint)(8 + 8 + textBytes.Length)).CopyTo(buffer, offset); offset += 4; // TotalSize
        BitConverter.GetBytes(0xFFFFFFFF).CopyTo(buffer, offset); offset += 4; // StrRef
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4; // SubStringCount
        BitConverter.GetBytes(languageId).CopyTo(buffer, offset); offset += 4; // LanguageId
        BitConverter.GetBytes((uint)textBytes.Length).CopyTo(buffer, offset); offset += 4; // StringLength
        textBytes.CopyTo(buffer, offset);

        return buffer;
    }

    private static byte[] CreateGffWithList(int elementCount)
    {
        var label = "TestList";
        var labelData = new byte[16];
        Encoding.ASCII.GetBytes(label).CopyTo(labelData, 0);

        // We need: 1 root struct + N child structs
        // Root has 1 List field
        // List has N struct references

        uint structOffset = 56;
        uint structCount = (uint)(1 + elementCount);
        uint fieldOffset = structOffset + (structCount * 12);
        uint labelOffset = fieldOffset + 12;  // 1 field
        uint fieldDataOffset = labelOffset + 16;
        uint fieldIndicesOffset = fieldDataOffset;
        uint listIndicesOffset = fieldIndicesOffset;
        uint listIndicesCount = 4u + ((uint)elementCount * 4);  // Count + N indices

        var buffer = new byte[listIndicesOffset + listIndicesCount];
        var offset = 0;

        // Header
        Encoding.ASCII.GetBytes("TST ").CopyTo(buffer, offset); offset += 4;
        Encoding.ASCII.GetBytes("V3.2").CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(structOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(structCount).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(labelOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldDataOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(fieldIndicesOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;

        BitConverter.GetBytes(listIndicesOffset).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(listIndicesCount).CopyTo(buffer, offset); offset += 4;

        // Structs
        offset = (int)structOffset;
        // Root struct
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;  // Field index 0
        BitConverter.GetBytes(1u).CopyTo(buffer, offset); offset += 4;

        // Child structs (empty)
        for (int i = 0; i < elementCount; i++)
        {
            BitConverter.GetBytes((uint)(i + 1)).CopyTo(buffer, offset); offset += 4;  // Type
            BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;
            BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;  // No fields
        }

        // Field (List)
        offset = (int)fieldOffset;
        BitConverter.GetBytes(GffField.List).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;  // Label index
        BitConverter.GetBytes(0u).CopyTo(buffer, offset); offset += 4;  // Offset into list indices

        // Label
        offset = (int)labelOffset;
        labelData.CopyTo(buffer, offset);

        // List indices
        offset = (int)listIndicesOffset;
        BitConverter.GetBytes((uint)elementCount).CopyTo(buffer, offset); offset += 4;
        for (int i = 0; i < elementCount; i++)
        {
            BitConverter.GetBytes((uint)(i + 1)).CopyTo(buffer, offset); offset += 4;  // Struct indices 1..N
        }

        return buffer;
    }

    #endregion
}

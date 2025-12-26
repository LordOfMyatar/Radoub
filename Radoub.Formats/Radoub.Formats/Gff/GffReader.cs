using System.Text;

namespace Radoub.Formats.Gff;

/// <summary>
/// Reads GFF (Generic File Format) files from binary format.
/// Reference: BioWare Aurora GFF format spec, neverwinter.nim gff.nim
/// </summary>
public static class GffReader
{
    private const int HeaderSize = 56; // 14 uint32_t fields

    /// <summary>
    /// Read a GFF file from a file path.
    /// </summary>
    public static GffFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read a GFF file from a stream.
    /// </summary>
    public static GffFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read a GFF file from a byte buffer.
    /// </summary>
    public static GffFile Read(byte[] buffer)
    {
        if (buffer.Length < HeaderSize)
            throw new InvalidDataException($"GFF file too small: {buffer.Length} bytes, minimum {HeaderSize}");

        var gff = new GffFile();

        // Parse header
        var header = ParseHeader(buffer);
        gff.FileType = header.FileType;
        gff.FileVersion = header.FileVersion;

        // Validate version
        if (header.FileVersion != "V3.2")
            throw new InvalidDataException($"Unsupported GFF version: '{header.FileVersion}', expected 'V3.2'");

        // Parse all sections
        var structs = ParseStructs(buffer, header);
        var fields = ParseFields(buffer, header);
        var labels = ParseLabels(buffer, header);

        // Resolve labels to fields
        ResolveFieldLabels(fields, labels);

        // Resolve field values (complex types need header and structs)
        ResolveFieldValues(fields, structs, buffer, header);

        // Assign fields to structs
        AssignFieldsToStructs(structs, fields, buffer, header);

        // Build result
        gff.Structs = structs.ToList();
        gff.Fields = fields.ToList();
        gff.Labels = labels.ToList();
        gff.RootStruct = structs.Length > 0 ? structs[0] : new GffStruct();

        return gff;
    }

    /// <summary>
    /// Parse GFF header from buffer (exposed for testing).
    /// </summary>
    public static GffHeader ParseHeader(byte[] buffer)
    {
        if (buffer.Length < HeaderSize)
            throw new InvalidDataException($"Buffer too small for GFF header: {buffer.Length} < {HeaderSize}");

        var header = new GffHeader();
        var offset = 0;

        // File signature and version (8 bytes)
        header.FileType = Encoding.ASCII.GetString(buffer, offset, 4).TrimEnd('\0');
        offset += 4;
        header.FileVersion = Encoding.ASCII.GetString(buffer, offset, 4).TrimEnd('\0');
        offset += 4;

        // 12 uint32_t offsets and counts
        header.StructOffset = ReadUInt32(buffer, ref offset);
        header.StructCount = ReadUInt32(buffer, ref offset);
        header.FieldOffset = ReadUInt32(buffer, ref offset);
        header.FieldCount = ReadUInt32(buffer, ref offset);
        header.LabelOffset = ReadUInt32(buffer, ref offset);
        header.LabelCount = ReadUInt32(buffer, ref offset);
        header.FieldDataOffset = ReadUInt32(buffer, ref offset);
        header.FieldDataCount = ReadUInt32(buffer, ref offset);
        header.FieldIndicesOffset = ReadUInt32(buffer, ref offset);
        header.FieldIndicesCount = ReadUInt32(buffer, ref offset);
        header.ListIndicesOffset = ReadUInt32(buffer, ref offset);
        header.ListIndicesCount = ReadUInt32(buffer, ref offset);

        return header;
    }

    private static GffStruct[] ParseStructs(byte[] buffer, GffHeader header)
    {
        var structs = new GffStruct[header.StructCount];
        var offset = (int)header.StructOffset;

        for (uint i = 0; i < header.StructCount; i++)
        {
            ValidateAccess(buffer, offset, 12);

            structs[i] = new GffStruct
            {
                Type = ReadUInt32(buffer, ref offset),
                DataOrDataOffset = ReadUInt32(buffer, ref offset),
                FieldCount = ReadUInt32(buffer, ref offset)
            };
        }

        return structs;
    }

    private static GffField[] ParseFields(byte[] buffer, GffHeader header)
    {
        var fields = new GffField[header.FieldCount];
        var offset = (int)header.FieldOffset;

        for (uint i = 0; i < header.FieldCount; i++)
        {
            ValidateAccess(buffer, offset, 12);

            fields[i] = new GffField
            {
                Type = ReadUInt32(buffer, ref offset),
                LabelIndex = ReadUInt32(buffer, ref offset),
                DataOrDataOffset = ReadUInt32(buffer, ref offset)
            };
        }

        return fields;
    }

    private static GffLabel[] ParseLabels(byte[] buffer, GffHeader header)
    {
        var offset = (int)header.LabelOffset;

        // Auto-detect label format based on section size
        uint expectedSize = header.FieldDataOffset - header.LabelOffset;
        uint nullTerminatedSize = CalculateNullTerminatedSize(buffer, (int)header.LabelOffset, (int)header.LabelCount);
        uint fixedWidthSize = header.LabelCount * 18; // 2-byte length + 16-byte padded
        uint auroraFixedSize = header.LabelCount * 16; // GFF 16-byte fixed format

        // Prioritize exact matches, then use tolerance-based matching
        // Aurora fixed format: exactly 16 bytes per label
        if (expectedSize == auroraFixedSize)
            return ParseAuroraFixedLabels(buffer, header, offset);

        // Length-prefixed format: exactly 18 bytes per label (2-byte length + 16-byte padded)
        if (expectedSize == fixedWidthSize)
            return ParseLengthPrefixedLabels(buffer, header, offset);

        // Null-terminated: check if close to calculated size
        bool isNullTerminated = Math.Abs((int)expectedSize - (int)nullTerminatedSize) < 5;
        if (isNullTerminated)
            return ParseNullTerminatedLabels(buffer, header, offset);

        // Fallback to aurora fixed format (most common for standard GFF files)
        return ParseAuroraFixedLabels(buffer, header, offset);
    }

    private static uint CalculateNullTerminatedSize(byte[] buffer, int startOffset, int labelCount)
    {
        uint size = 0;
        int offset = startOffset;
        int labelsFound = 0;

        while (labelsFound < labelCount && offset < buffer.Length)
        {
            int start = offset;
            while (offset < buffer.Length && buffer[offset] != 0) offset++;
            if (offset < buffer.Length)
            {
                size += (uint)(offset - start + 1);
                offset++;
                labelsFound++;
            }
            else break;
        }
        return size;
    }

    private static GffLabel[] ParseNullTerminatedLabels(byte[] buffer, GffHeader header, int offset)
    {
        var labels = new GffLabel[header.LabelCount];

        for (uint i = 0; i < header.LabelCount; i++)
        {
            ValidateAccess(buffer, offset, 1);

            var startOffset = offset;
            while (offset < buffer.Length && buffer[offset] != 0) offset++;

            if (offset >= buffer.Length)
                throw new InvalidDataException($"Label {i} not properly null-terminated");

            var labelLength = offset - startOffset;
            labels[i] = new GffLabel { Text = Encoding.ASCII.GetString(buffer, startOffset, labelLength) };
            offset++;
        }

        return labels;
    }

    private static GffLabel[] ParseLengthPrefixedLabels(byte[] buffer, GffHeader header, int offset)
    {
        var labels = new GffLabel[header.LabelCount];

        for (uint i = 0; i < header.LabelCount; i++)
        {
            ValidateAccess(buffer, offset, 18);

            ushort length = BitConverter.ToUInt16(buffer, offset);
            offset += 2;

            var labelBytes = new byte[16];
            Array.Copy(buffer, offset, labelBytes, 0, 16);
            offset += 16;

            var actualLength = Math.Min((int)length, 16);
            labels[i] = new GffLabel { Text = Encoding.ASCII.GetString(labelBytes, 0, actualLength) };
        }

        return labels;
    }

    private static GffLabel[] ParseAuroraFixedLabels(byte[] buffer, GffHeader header, int offset)
    {
        var labels = new GffLabel[header.LabelCount];

        for (uint i = 0; i < header.LabelCount; i++)
        {
            ValidateAccess(buffer, offset, 16);

            var labelBytes = new byte[16];
            Array.Copy(buffer, offset, labelBytes, 0, 16);
            offset += 16;

            var nullIndex = Array.IndexOf(labelBytes, (byte)0);
            var labelLength = nullIndex >= 0 ? nullIndex : 16;
            labels[i] = new GffLabel { Text = Encoding.ASCII.GetString(labelBytes, 0, labelLength) };
        }

        return labels;
    }

    private static void ResolveFieldLabels(GffField[] fields, GffLabel[] labels)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            var labelIndex = fields[i].LabelIndex;

            if (labelIndex < labels.Length)
                fields[i].Label = labels[labelIndex].Text;
            else
                fields[i].Label = $"InvalidLabel{labelIndex}";
        }
    }

    private static void ResolveFieldValues(GffField[] fields, GffStruct[] structs, byte[] buffer, GffHeader header)
    {
        foreach (var field in fields)
        {
            try
            {
                field.Value = ReadFieldValue(field, buffer, header, structs);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    $"Failed to read value for field '{field.Label}' of type {field.Type.GetTypeName()}: {ex.Message}", ex);
            }
        }
    }

    private static void AssignFieldsToStructs(GffStruct[] structs, GffField[] fields, byte[] buffer, GffHeader header)
    {
        for (int i = 0; i < structs.Length; i++)
        {
            var s = structs[i];

            if (s.FieldCount == 0)
            {
                // No fields
            }
            else if (s.FieldCount == 1)
            {
                // DataOrDataOffset is direct field index
                if (s.DataOrDataOffset < fields.Length)
                    s.Fields.Add(fields[s.DataOrDataOffset]);
            }
            else
            {
                // DataOrDataOffset is byte offset into FieldIndices array
                var indicesOffset = (int)(header.FieldIndicesOffset + s.DataOrDataOffset);

                for (uint j = 0; j < s.FieldCount; j++)
                {
                    ValidateAccess(buffer, indicesOffset, 4);
                    var fieldIndex = BitConverter.ToUInt32(buffer, indicesOffset);
                    indicesOffset += 4;

                    if (fieldIndex < fields.Length)
                        s.Fields.Add(fields[fieldIndex]);
                }
            }
        }
    }

    private static object? ReadFieldValue(GffField field, byte[] buffer, GffHeader header, GffStruct[] structs)
    {
        if (field.Type.IsSimpleType())
        {
            // Simple types are stored directly in DataOrDataOffset
            return field.Type switch
            {
                GffField.BYTE => (object)(byte)(field.DataOrDataOffset & 0xFF),
                GffField.CHAR => (object)(sbyte)(field.DataOrDataOffset & 0xFF),
                GffField.WORD => (object)(ushort)(field.DataOrDataOffset & 0xFFFF),
                GffField.SHORT => (object)(short)(field.DataOrDataOffset & 0xFFFF),
                GffField.DWORD => (object)field.DataOrDataOffset,
                GffField.INT => (object)(int)field.DataOrDataOffset,
                GffField.DWORD64 => (object)(ulong)field.DataOrDataOffset,
                GffField.INT64 => (object)(long)field.DataOrDataOffset,
                GffField.FLOAT => (object)BitConverter.Int32BitsToSingle((int)field.DataOrDataOffset),
                GffField.DOUBLE => (object)BitConverter.Int64BitsToDouble((long)field.DataOrDataOffset),
                _ => (object)field.DataOrDataOffset
            };
        }
        else
        {
            // Complex types use DataOrDataOffset as an offset into FieldData
            var dataOffset = (int)(header.FieldDataOffset + field.DataOrDataOffset);

            return field.Type switch
            {
                GffField.CExoString => ReadCExoString(buffer, dataOffset),
                GffField.CResRef => ReadCResRef(buffer, dataOffset),
                GffField.CExoLocString => ReadCExoLocString(buffer, dataOffset),
                // Struct fields: DataOrDataOffset IS the struct index directly, not an offset into FieldData
                GffField.Struct => ReadStructByIndex(field.DataOrDataOffset, structs),
                GffField.List => ReadList(buffer, (int)field.DataOrDataOffset, header, structs),
                GffField.VOID => ReadVoid(buffer, dataOffset),
                _ => null
            };
        }
    }

    private static string ReadCExoString(byte[] buffer, int offset)
    {
        ValidateAccess(buffer, offset, 4);
        var length = ReadUInt32(buffer, ref offset);

        if (length == 0) return string.Empty;
        if (length > 65535)
            throw new InvalidDataException($"CExoString length too large: {length}");

        ValidateAccess(buffer, offset, (int)length);

        // Try UTF-8 first, fall back to Windows-1252 if invalid
        try
        {
            var result = Encoding.UTF8.GetString(buffer, offset, (int)length);
            if (!result.Contains('\uFFFD'))
                return result.TrimEnd('\0');
        }
        catch { }

        // Fall back to Windows-1252
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252).GetString(buffer, offset, (int)length).TrimEnd('\0');
    }

    private static string ReadCResRef(byte[] buffer, int offset)
    {
        ValidateAccess(buffer, offset, 1);
        var length = buffer[offset++];

        if (length == 0) return string.Empty;
        if (length > 16)
            throw new InvalidDataException($"CResRef length too large: {length}");

        ValidateAccess(buffer, offset, length);
        return Encoding.ASCII.GetString(buffer, offset, length).TrimEnd('\0');
    }

    private static CExoLocString ReadCExoLocString(byte[] buffer, int offset)
    {
        var locString = new CExoLocString();

        ValidateAccess(buffer, offset, 12);

        // CExoLocString structure:
        // uint32 TotalSize
        // uint32 StrRef
        // uint32 SubStringCount
        var totalSize = ReadUInt32(buffer, ref offset);
        locString.StrRef = ReadUInt32(buffer, ref offset);
        locString.SubStringCount = ReadUInt32(buffer, ref offset);

        for (uint i = 0; i < locString.SubStringCount; i++)
        {
            ValidateAccess(buffer, offset, 8);
            var languageId = ReadUInt32(buffer, ref offset);
            var stringLength = ReadUInt32(buffer, ref offset);

            if (stringLength > 0 && stringLength < 65535 && offset + stringLength <= buffer.Length)
            {
                // Try UTF-8 first, fall back to Windows-1252
                string text;
                try
                {
                    text = Encoding.UTF8.GetString(buffer, offset, (int)stringLength);
                    if (text.Contains('\uFFFD'))
                    {
                        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                        text = Encoding.GetEncoding(1252).GetString(buffer, offset, (int)stringLength);
                    }
                }
                catch
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    text = Encoding.GetEncoding(1252).GetString(buffer, offset, (int)stringLength);
                }

                locString.LocalizedStrings[languageId] = text.TrimEnd('\0');
                offset += (int)stringLength;
            }
            else if (stringLength >= 65535)
            {
                break;
            }
        }

        return locString;
    }

    /// <summary>
    /// Read a struct by direct index (for Struct type fields where DataOrDataOffset is the index).
    /// </summary>
    private static GffStruct? ReadStructByIndex(uint structIndex, GffStruct[] structs)
    {
        if (structIndex >= structs.Length)
            throw new InvalidDataException($"Invalid struct index: {structIndex} >= {structs.Length}");

        return structs[structIndex];
    }

    private static GffList ReadList(byte[] buffer, int dataOffset, GffHeader header, GffStruct[] structs)
    {
        // Lists are stored at ListIndicesOffset + field's DataOrDataOffset
        var listOffset = (int)header.ListIndicesOffset + dataOffset;
        var list = new GffList();

        ValidateAccess(buffer, listOffset, 4);
        var tempOffset = listOffset;
        list.Count = ReadUInt32(buffer, ref tempOffset);

        for (uint i = 0; i < list.Count; i++)
        {
            ValidateAccess(buffer, tempOffset, 4);
            var structIndex = ReadUInt32(buffer, ref tempOffset);

            if (structIndex < structs.Length)
                list.Elements.Add(structs[structIndex]);
        }

        return list;
    }

    private static byte[] ReadVoid(byte[] buffer, int offset)
    {
        ValidateAccess(buffer, offset, 4);
        var length = ReadUInt32(buffer, ref offset);

        if (length == 0) return Array.Empty<byte>();
        if (length > 1024 * 1024) // 1MB limit
            throw new InvalidDataException($"VOID data length too large: {length}");

        ValidateAccess(buffer, offset, (int)length);
        var data = new byte[length];
        Array.Copy(buffer, offset, data, 0, (int)length);
        return data;
    }

    private static uint ReadUInt32(byte[] buffer, ref int offset)
    {
        ValidateAccess(buffer, offset, 4);
        var result = BitConverter.ToUInt32(buffer, offset);
        offset += 4;
        return result;
    }

    private static void ValidateAccess(byte[] buffer, int offset, int length)
    {
        if (offset < 0 || offset + length > buffer.Length)
            throw new InvalidDataException(
                $"Buffer access violation: offset={offset}, length={length}, bufferSize={buffer.Length}");
    }
}

using System.Text;

namespace Radoub.Formats.Gff;

/// <summary>
/// Writes GFF (Generic File Format) files to binary format.
/// Reference: BioWare Aurora GFF format spec, neverwinter.nim gff.nim
/// </summary>
public static class GffWriter
{
    private const int HeaderSize = 56;

    /// <summary>
    /// Write a GFF file to a file path.
    /// </summary>
    public static void Write(GffFile gff, string filePath)
    {
        var buffer = Write(gff);
        File.WriteAllBytes(filePath, buffer);
    }

    /// <summary>
    /// Write a GFF file to a stream.
    /// </summary>
    public static void Write(GffFile gff, Stream stream)
    {
        var buffer = Write(gff);
        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Write a GFF file to a byte buffer.
    /// </summary>
    public static byte[] Write(GffFile gff)
    {
        // Collect all unique items
        var structs = new List<GffStruct>();
        var fields = new List<GffField>();
        var labels = new Dictionary<string, uint>();
        var fieldData = new MemoryStream();
        var fieldIndices = new MemoryStream();
        var listIndices = new MemoryStream();

        // Build struct/field collections from root
        CollectStructs(gff.RootStruct, structs);
        CollectFieldsAndLabels(structs, fields, labels);

        // Pre-calculate field indices (for structs with >1 field)
        var structFieldMap = BuildStructFieldMap(structs, fields);
        BuildFieldIndices(structs, structFieldMap, fieldIndices);

        // Calculate offsets
        var header = new GffHeader
        {
            FileType = gff.FileType.PadRight(4).Substring(0, 4),
            FileVersion = gff.FileVersion.PadRight(4).Substring(0, 4),
            StructOffset = HeaderSize,
            StructCount = (uint)structs.Count,
        };

        header.FieldOffset = header.StructOffset + (header.StructCount * 12);
        header.FieldCount = (uint)fields.Count;

        header.LabelOffset = header.FieldOffset + (header.FieldCount * 12);
        header.LabelCount = (uint)labels.Count;

        // Build label data (16-byte fixed format)
        var labelData = new byte[labels.Count * 16];
        foreach (var kvp in labels)
        {
            var labelBytes = Encoding.ASCII.GetBytes(kvp.Key);
            var destOffset = (int)kvp.Value * 16;
            Array.Copy(labelBytes, 0, labelData, destOffset, Math.Min(labelBytes.Length, 16));
        }

        header.FieldDataOffset = header.LabelOffset + (uint)labelData.Length;

        // Write field data and update field offsets
        WriteFieldData(fields, structs, fieldData, listIndices);

        header.FieldDataCount = (uint)fieldData.Length;
        header.FieldIndicesOffset = header.FieldDataOffset + header.FieldDataCount;
        header.FieldIndicesCount = (uint)fieldIndices.Length;
        header.ListIndicesOffset = header.FieldIndicesOffset + header.FieldIndicesCount;
        header.ListIndicesCount = (uint)listIndices.Length;

        // Build final buffer
        var totalSize = (int)(header.ListIndicesOffset + header.ListIndicesCount);
        var buffer = new byte[totalSize];

        // Write header
        WriteHeader(buffer, header);

        // Write structs
        WriteStructs(buffer, structs, structFieldMap, header);

        // Write fields
        WriteFields(buffer, fields, labels, header);

        // Write labels
        Array.Copy(labelData, 0, buffer, header.LabelOffset, labelData.Length);

        // Write field data
        var fieldDataArray = fieldData.ToArray();
        if (fieldDataArray.Length > 0)
            Array.Copy(fieldDataArray, 0, buffer, header.FieldDataOffset, fieldDataArray.Length);

        // Write field indices
        var fieldIndicesArray = fieldIndices.ToArray();
        if (fieldIndicesArray.Length > 0)
            Array.Copy(fieldIndicesArray, 0, buffer, header.FieldIndicesOffset, fieldIndicesArray.Length);

        // Write list indices
        var listIndicesArray = listIndices.ToArray();
        if (listIndicesArray.Length > 0)
            Array.Copy(listIndicesArray, 0, buffer, header.ListIndicesOffset, listIndicesArray.Length);

        return buffer;
    }

    private static Dictionary<GffStruct, (List<int> FieldIndices, uint IndicesOffset)> BuildStructFieldMap(
        List<GffStruct> structs, List<GffField> fields)
    {
        var result = new Dictionary<GffStruct, (List<int>, uint)>();
        int fieldIndex = 0;

        foreach (var s in structs)
        {
            var indices = new List<int>();
            foreach (var field in s.Fields)
            {
                indices.Add(fieldIndex);
                fieldIndex++;
            }
            result[s] = (indices, 0); // IndicesOffset filled later
        }

        return result;
    }

    private static void BuildFieldIndices(List<GffStruct> structs,
        Dictionary<GffStruct, (List<int> FieldIndices, uint IndicesOffset)> structFieldMap,
        MemoryStream fieldIndices)
    {
        uint currentOffset = 0;

        for (int i = 0; i < structs.Count; i++)
        {
            var s = structs[i];
            var (indices, _) = structFieldMap[s];

            if (indices.Count > 1)
            {
                // Update offset
                structFieldMap[s] = (indices, currentOffset);

                // Write field indices
                foreach (var fi in indices)
                {
                    var bytes = BitConverter.GetBytes((uint)fi);
                    fieldIndices.Write(bytes, 0, 4);
                }
                currentOffset += (uint)(indices.Count * 4);
            }
        }
    }

    private static void CollectStructs(GffStruct root, List<GffStruct> structs)
    {
        var visited = new HashSet<GffStruct>();
        CollectStructsRecursive(root, structs, visited);
    }

    private static void CollectStructsRecursive(GffStruct s, List<GffStruct> structs, HashSet<GffStruct> visited)
    {
        if (s == null || visited.Contains(s))
            return;

        visited.Add(s);
        structs.Add(s);

        foreach (var field in s.Fields)
        {
            if (field.Value is GffStruct childStruct)
            {
                CollectStructsRecursive(childStruct, structs, visited);
            }
            else if (field.Value is GffList list)
            {
                foreach (var element in list.Elements)
                {
                    CollectStructsRecursive(element, structs, visited);
                }
            }
        }
    }

    private static void CollectFieldsAndLabels(List<GffStruct> structs, List<GffField> fields, Dictionary<string, uint> labels)
    {
        foreach (var s in structs)
        {
            foreach (var field in s.Fields)
            {
                fields.Add(field);

                if (!labels.ContainsKey(field.Label))
                {
                    labels[field.Label] = (uint)labels.Count;
                }
            }
        }
    }

    private static void WriteHeader(byte[] buffer, GffHeader header)
    {
        var offset = 0;

        // File type and version
        var typeBytes = Encoding.ASCII.GetBytes(header.FileType);
        Array.Copy(typeBytes, 0, buffer, offset, Math.Min(typeBytes.Length, 4));
        offset += 4;

        var versionBytes = Encoding.ASCII.GetBytes(header.FileVersion);
        Array.Copy(versionBytes, 0, buffer, offset, Math.Min(versionBytes.Length, 4));
        offset += 4;

        // Offsets and counts
        WriteUInt32(buffer, ref offset, header.StructOffset);
        WriteUInt32(buffer, ref offset, header.StructCount);
        WriteUInt32(buffer, ref offset, header.FieldOffset);
        WriteUInt32(buffer, ref offset, header.FieldCount);
        WriteUInt32(buffer, ref offset, header.LabelOffset);
        WriteUInt32(buffer, ref offset, header.LabelCount);
        WriteUInt32(buffer, ref offset, header.FieldDataOffset);
        WriteUInt32(buffer, ref offset, header.FieldDataCount);
        WriteUInt32(buffer, ref offset, header.FieldIndicesOffset);
        WriteUInt32(buffer, ref offset, header.FieldIndicesCount);
        WriteUInt32(buffer, ref offset, header.ListIndicesOffset);
        WriteUInt32(buffer, ref offset, header.ListIndicesCount);
    }

    private static void WriteStructs(byte[] buffer, List<GffStruct> structs,
        Dictionary<GffStruct, (List<int> FieldIndices, uint IndicesOffset)> structFieldMap,
        GffHeader header)
    {
        var offset = (int)header.StructOffset;
        for (int i = 0; i < structs.Count; i++)
        {
            var s = structs[i];
            var (fieldList, indicesOffset) = structFieldMap[s];

            WriteUInt32(buffer, ref offset, s.Type);

            if (fieldList.Count == 0)
            {
                WriteUInt32(buffer, ref offset, 0);
            }
            else if (fieldList.Count == 1)
            {
                WriteUInt32(buffer, ref offset, (uint)fieldList[0]);
            }
            else
            {
                // Write offset into field indices array
                WriteUInt32(buffer, ref offset, indicesOffset);
            }

            WriteUInt32(buffer, ref offset, (uint)fieldList.Count);
        }
    }

    private static void WriteFields(byte[] buffer, List<GffField> fields, Dictionary<string, uint> labels, GffHeader header)
    {
        var offset = (int)header.FieldOffset;

        foreach (var field in fields)
        {
            WriteUInt32(buffer, ref offset, field.Type);
            WriteUInt32(buffer, ref offset, labels[field.Label]);
            WriteUInt32(buffer, ref offset, field.DataOrDataOffset);
        }
    }

    private static void WriteFieldData(List<GffField> fields, List<GffStruct> structs,
        MemoryStream fieldData, MemoryStream listIndices)
    {
        // Map structs to indices
        var structIndices = new Dictionary<GffStruct, uint>();
        for (int i = 0; i < structs.Count; i++)
        {
            structIndices[structs[i]] = (uint)i;
        }

        foreach (var field in fields)
        {
            if (field.Type.IsSimpleType())
            {
                // Simple types store value directly in DataOrDataOffset
                field.DataOrDataOffset = EncodeSimpleValue(field);
            }
            else if (field.Type == GffField.Struct)
            {
                // Struct fields store struct index directly in DataOrDataOffset
                if (field.Value is GffStruct s && structIndices.TryGetValue(s, out var idx))
                {
                    field.DataOrDataOffset = idx;
                }
            }
            else if (field.Type == GffField.List)
            {
                // List fields store offset into ListIndices in DataOrDataOffset
                field.DataOrDataOffset = (uint)listIndices.Position;
                if (field.Value is GffList list)
                {
                    var countBytes = BitConverter.GetBytes(list.Count);
                    listIndices.Write(countBytes, 0, 4);

                    foreach (var element in list.Elements)
                    {
                        if (structIndices.TryGetValue(element, out var elemIdx))
                        {
                            var elemBytes = BitConverter.GetBytes(elemIdx);
                            listIndices.Write(elemBytes, 0, 4);
                        }
                    }
                }
            }
            else
            {
                // Other complex types (CExoString, CResRef, etc.) store offset into FieldData
                field.DataOrDataOffset = (uint)fieldData.Position;
                WriteComplexValue(field, fieldData, listIndices, structIndices);
            }
        }
    }

    private static uint EncodeSimpleValue(GffField field)
    {
        return field.Type switch
        {
            GffField.BYTE => field.Value is byte b ? b : 0u,
            GffField.CHAR => field.Value is sbyte sb ? (uint)(byte)sb : 0u,
            GffField.WORD => field.Value is ushort us ? us : 0u,
            GffField.SHORT => field.Value is short ss ? (uint)(ushort)ss : 0u,
            GffField.DWORD => field.Value is uint ui ? ui : 0u,
            GffField.INT => field.Value is int si ? (uint)si : 0u,
            GffField.FLOAT => field.Value is float f ? (uint)BitConverter.SingleToInt32Bits(f) : 0u,
            _ => 0u
        };
    }

    private static void WriteComplexValue(GffField field, MemoryStream fieldData,
        MemoryStream listIndices, Dictionary<GffStruct, uint> structIndices)
    {
        // Note: Struct and List are handled directly in WriteFieldData, not here
        switch (field.Type)
        {
            case GffField.CExoString:
                WriteCExoString(fieldData, field.Value as string ?? string.Empty);
                break;

            case GffField.CResRef:
                WriteCResRef(fieldData, field.Value as string ?? string.Empty);
                break;

            case GffField.CExoLocString:
                WriteCExoLocString(fieldData, field.Value as CExoLocString ?? new CExoLocString());
                break;

            case GffField.VOID:
                if (field.Value is byte[] data)
                {
                    var lengthBytes = BitConverter.GetBytes((uint)data.Length);
                    fieldData.Write(lengthBytes, 0, 4);
                    fieldData.Write(data, 0, data.Length);
                }
                else
                {
                    var zeroLength = BitConverter.GetBytes(0u);
                    fieldData.Write(zeroLength, 0, 4);
                }
                break;
        }
    }

    private static void WriteCExoString(MemoryStream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var lengthBytes = BitConverter.GetBytes((uint)bytes.Length);
        stream.Write(lengthBytes, 0, 4);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteCResRef(MemoryStream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        var length = (byte)Math.Min(bytes.Length, 16);
        stream.WriteByte(length);
        stream.Write(bytes, 0, length);
    }

    private static void WriteCExoLocString(MemoryStream stream, CExoLocString locString)
    {
        // Determine actual substring count to write.
        // If SubStringCount > LocalizedStrings.Count, we need to write empty padding entries.
        // This is used by the game for "intentionally empty" fields like LastName.
        var actualSubCount = Math.Max(locString.SubStringCount, (uint)locString.LocalizedStrings.Count);
        var paddingCount = actualSubCount - (uint)locString.LocalizedStrings.Count;

        // Calculate total size: 8 bytes header (StrRef + SubStringCount) + substring data
        var substringsSize = 0;
        foreach (var kvp in locString.LocalizedStrings)
        {
            substringsSize += 8 + Encoding.UTF8.GetByteCount(kvp.Value); // langId + length + string
        }
        // Add size for empty padding entries (8 bytes each: langId + length=0)
        substringsSize += (int)paddingCount * 8;

        var totalSize = 8 + substringsSize; // StrRef + SubStringCount + substrings

        var totalSizeBytes = BitConverter.GetBytes((uint)totalSize);
        stream.Write(totalSizeBytes, 0, 4);

        var strRefBytes = BitConverter.GetBytes(locString.StrRef);
        stream.Write(strRefBytes, 0, 4);

        var subCountBytes = BitConverter.GetBytes(actualSubCount);
        stream.Write(subCountBytes, 0, 4);

        // Write actual localized strings
        foreach (var kvp in locString.LocalizedStrings)
        {
            var langIdBytes = BitConverter.GetBytes(kvp.Key);
            stream.Write(langIdBytes, 0, 4);

            var stringBytes = Encoding.UTF8.GetBytes(kvp.Value);
            var stringLengthBytes = BitConverter.GetBytes((uint)stringBytes.Length);
            stream.Write(stringLengthBytes, 0, 4);
            stream.Write(stringBytes, 0, stringBytes.Length);
        }

        // Write empty padding entries for SubStringCount > LocalizedStrings.Count
        // Game uses this pattern for "intentionally empty" fields (e.g., no last name)
        for (uint i = 0; i < paddingCount; i++)
        {
            stream.Write(BitConverter.GetBytes((uint)0), 0, 4); // languageId = 0 (English)
            stream.Write(BitConverter.GetBytes((uint)0), 0, 4); // stringLength = 0
        }
    }

    private static void WriteUInt32(byte[] buffer, ref int offset, uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, buffer, offset, 4);
        offset += 4;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DialogEditor.Services;

namespace DialogEditor.Parsers
{
    public static class GffBinaryReader
    {
        private const int GFF_HEADER_SIZE = 56; // 14 uint32_t fields
        
        public static GffHeader ParseGffHeader(byte[] buffer)
        {
            if (buffer.Length < GFF_HEADER_SIZE)
            {
                throw new InvalidDataException($"Buffer too small for GFF header: {buffer.Length} < {GFF_HEADER_SIZE}");
            }

            var header = new GffHeader();
            var offset = 0;

            // Parse file signature and version (8 bytes total)
            header.FileType = Encoding.ASCII.GetString(buffer, offset, 4).TrimEnd('\0');
            offset += 4;
            header.FileVersion = Encoding.ASCII.GetString(buffer, offset, 4).TrimEnd('\0');
            offset += 4;

            // Parse the 12 uint32_t values
            header.StructOffset = ReadUInt32LE(buffer, ref offset);
            header.StructCount = ReadUInt32LE(buffer, ref offset);
            header.FieldOffset = ReadUInt32LE(buffer, ref offset);
            header.FieldCount = ReadUInt32LE(buffer, ref offset);
            header.LabelOffset = ReadUInt32LE(buffer, ref offset);
            header.LabelCount = ReadUInt32LE(buffer, ref offset);
            header.FieldDataOffset = ReadUInt32LE(buffer, ref offset);
            header.FieldDataCount = ReadUInt32LE(buffer, ref offset);
            header.FieldIndicesOffset = ReadUInt32LE(buffer, ref offset);
            header.FieldIndicesCount = ReadUInt32LE(buffer, ref offset);
            header.ListIndicesOffset = ReadUInt32LE(buffer, ref offset);
            header.ListIndicesCount = ReadUInt32LE(buffer, ref offset);

            UnifiedLogger.LogGff(LogLevel.DEBUG, 
                $"GFF Header: {header.FileType} {header.FileVersion}, " +
                $"Structs: {header.StructCount}, Fields: {header.FieldCount}");
            UnifiedLogger.LogGff(LogLevel.DEBUG, 
                $"GFF Offsets: StructOff={header.StructOffset}, FieldOff={header.FieldOffset}, " +
                $"LabelOff={header.LabelOffset}, FieldDataOff={header.FieldDataOffset}, " +
                $"FieldIndicesOff={header.FieldIndicesOffset}, ListIndicesOff={header.ListIndicesOffset}");

            // REMOVED: Pattern detection was dead code (never used for adaptive behavior)
            // DetectAndLogPattern(header);

            return header;
        }

        public static GffStruct[] ParseStructs(byte[] buffer, GffHeader header)
        {
            var structs = new GffStruct[header.StructCount];
            var offset = (int)header.StructOffset;

            for (uint i = 0; i < header.StructCount; i++)
            {
                ValidateBufferAccess(buffer, offset, 12); // Each struct is 12 bytes
                
                structs[i] = new GffStruct
                {
                    Type = ReadUInt32LE(buffer, ref offset),
                    DataOrDataOffset = ReadUInt32LE(buffer, ref offset),
                    FieldCount = ReadUInt32LE(buffer, ref offset)
                };
            }

            UnifiedLogger.LogGff(LogLevel.DEBUG, $"Parsed {structs.Length} structs");
            return structs;
        }

        public static GffField[] ParseFields(byte[] buffer, GffHeader header)
        {
            UnifiedLogger.LogParser(LogLevel.INFO, $"📖 READ GFF HEADER: {header.FieldCount} fields");
            var fields = new GffField[header.FieldCount];
            var offset = (int)header.FieldOffset;

            for (uint i = 0; i < header.FieldCount; i++)
            {
                ValidateBufferAccess(buffer, offset, 12); // Each field is 12 bytes

                var type = ReadUInt32LE(buffer, ref offset);
                var labelIndex = ReadUInt32LE(buffer, ref offset);
                var dataOrDataOffset = ReadUInt32LE(buffer, ref offset);

                fields[i] = new GffField
                {
                    Type = type,
                    LabelIndex = labelIndex,
                    DataOrDataOffset = dataOrDataOffset
                };

                // Debug Animation fields specifically
                if (i < 10) // Log first 10 fields to avoid spam
                {
                    UnifiedLogger.LogGff(LogLevel.INFO,
                        $"Field[{i}]: Type={type} (0x{type:X}), LabelIdx={labelIndex}, DataOrDataOffset={dataOrDataOffset} (0x{dataOrDataOffset:X})");
                }
            }

            UnifiedLogger.LogGff(LogLevel.DEBUG, $"Parsed {fields.Length} fields");
            return fields;
        }

        public static GffLabel[] ParseLabels(byte[] buffer, GffHeader header)
        {
            var labels = new GffLabel[header.LabelCount];
            var offset = (int)header.LabelOffset;
            
            // Auto-detect format based on label section size
            uint expectedSize = header.FieldDataOffset - header.LabelOffset;
            uint nullTerminatedSize = CalculateNullTerminatedSize(buffer, (int)header.LabelOffset, (int)header.LabelCount);
            uint fixedWidthSize = header.LabelCount * 18; // 2-byte length + 16-byte padded
            uint auroraFixedSize = header.LabelCount * 16; // GFF 16-byte fixed format
            
            bool isNullTerminated = Math.Abs((int)expectedSize - (int)nullTerminatedSize) < 10;
            bool isFixedWidth = Math.Abs((int)expectedSize - (int)fixedWidthSize) < 10;
            bool isAuroraFixed = Math.Abs((int)expectedSize - (int)auroraFixedSize) < 10;

            if (isNullTerminated)
            {
                // New GFF format: consecutive null-terminated strings
                UnifiedLogger.LogGff(LogLevel.DEBUG, "Using null-terminated label format");
                return ParseNullTerminatedLabels(buffer, header, offset);
            }
            else if (isFixedWidth)
            {
                // Old format with length prefixes
                UnifiedLogger.LogGff(LogLevel.DEBUG, "Using length-prefixed label format");
                return ParseLengthPrefixedLabels(buffer, header, offset);
            }
            else if (isAuroraFixed)
            {
                // 16-byte fixed format
                UnifiedLogger.LogGff(LogLevel.DEBUG, "Using 16-byte fixed label format");
                return ParseAuroraFixedLabels(buffer, header, offset);
            }
            else
            {
                // Fallback to 16-byte fixed format
                UnifiedLogger.LogParser(LogLevel.WARN, $"Unknown label format, using 16-byte fixed format. Expected: {expectedSize}, NT: {nullTerminatedSize}, FW: {fixedWidthSize}, AF: {auroraFixedSize}");
                return ParseAuroraFixedLabels(buffer, header, offset);
            }
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
                    size += (uint)(offset - start + 1); // +1 for null terminator
                    offset++; // skip null terminator
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
                ValidateBufferAccess(buffer, offset, 1);
                
                var startOffset = offset;
                while (offset < buffer.Length && buffer[offset] != 0) offset++;
                
                if (offset >= buffer.Length)
                    throw new InvalidDataException($"Label {i} not properly null-terminated");
                
                var labelLength = offset - startOffset;
                labels[i] = new GffLabel { Text = Encoding.ASCII.GetString(buffer, startOffset, labelLength) };
                offset++; // Skip null terminator
            }
            
            return labels;
        }
        
        private static GffLabel[] ParseLengthPrefixedLabels(byte[] buffer, GffHeader header, int offset)
        {
            var labels = new GffLabel[header.LabelCount];
            
            for (uint i = 0; i < header.LabelCount; i++)
            {
                ValidateBufferAccess(buffer, offset, 18);
                
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
                ValidateBufferAccess(buffer, offset, 16);
                
                var labelBytes = new byte[16];
                Array.Copy(buffer, offset, labelBytes, 0, 16);
                offset += 16;
                
                var nullIndex = Array.IndexOf(labelBytes, (byte)0);
                var labelLength = nullIndex >= 0 ? nullIndex : 16;
                labels[i] = new GffLabel { Text = Encoding.ASCII.GetString(labelBytes, 0, labelLength) };
            }
            
            UnifiedLogger.LogGff(LogLevel.DEBUG, $"Parsed {labels.Length} labels");
            return labels;
        }

        public static void ResolveFieldLabels(GffField[] fields, GffLabel[] labels, byte[] buffer, GffHeader header)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                var labelIndex = fields[i].LabelIndex;
                
                if (labelIndex < labels.Length)
                {
                    fields[i].Label = labels[labelIndex].Text;
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, 
                        $"Invalid label index {labelIndex} for field {i}, max: {labels.Length - 1}");
                    fields[i].Label = $"InvalidLabel{labelIndex}";
                }
            }
        }

        public static void ResolveFieldValues(GffField[] fields, GffStruct[] structs, byte[] buffer, GffHeader header)
        {
            foreach (var field in fields)
            {
                try
                {
                    field.Value = ReadFieldValue(field, buffer, header, structs);
                    
                    // Debug Index fields specifically
                    if (field.Label == "Index")
                    {
                        // UnifiedLogger.LogParser(LogLevel.DEBUG,
                        //     $"🔍 INDEX FIELD DEBUG: Label='{field.Label}' Type={field.Type.GetTypeName()} RawData=0x{field.DataOrDataOffset:X8} Value={field.Value}");
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, 
                        $"Failed to read value for field '{field.Label}' of type {field.Type.GetTypeName()}: {ex.Message}");
                    field.Value = null;
                }
            }
        }

        private static object? ReadFieldValue(GffField field, byte[] buffer, GffHeader header, GffStruct[] structs)
        {
            // Debug Animation field type
            if (field.Label == "Animation")
            {
                UnifiedLogger.LogParser(LogLevel.INFO,
                    $"ReadFieldValue Animation: field.Type={field.Type}, IsSimpleType={field.Type.IsSimpleType()}, DWORD constant={GffField.DWORD}, FLOAT constant={GffField.FLOAT}");
            }

            if (field.Type.IsSimpleType())
            {
                // Simple types are stored directly in DataOrDataOffset
                // IMPORTANT: Explicit (object) casts prevent C# from converting all types to common base type
                return field.Type switch
                {
                    GffField.BYTE => (object)(byte)(field.DataOrDataOffset & 0xFF),
                    GffField.CHAR => (object)(sbyte)(field.DataOrDataOffset & 0xFF),
                    GffField.WORD => (object)(ushort)(field.DataOrDataOffset & 0xFFFF),
                    GffField.SHORT => (object)(short)(field.DataOrDataOffset & 0xFFFF),
                    GffField.DWORD => (object)field.DataOrDataOffset,
                    GffField.INT => (object)(int)field.DataOrDataOffset,
                    GffField.FLOAT => (object)ReadFloatFieldDebug(field),
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
                    GffField.Struct => ReadStruct(buffer, dataOffset, structs),
                    GffField.List => ReadList(buffer, (int)field.DataOrDataOffset, header, structs),
                    _ => null
                };
            }
        }

        private static string ReadCExoString(byte[] buffer, int offset)
        {
            ValidateBufferAccess(buffer, offset, 4);
            var length = ReadUInt32LE(buffer, ref offset);

            if (length == 0) return string.Empty;
            if (length > 65535) // Reasonable limit
            {
                throw new InvalidDataException($"CExoString length too large: {length}");
            }

            ValidateBufferAccess(buffer, offset, (int)length);

            // Phase 1 Bug Fix: Try UTF-8 first, fall back to Windows-1252 if invalid
            // GFF Toolset may save files in Windows-1252 encoding
            try
            {
                var result = Encoding.UTF8.GetString(buffer, offset, (int)length);

                // Check if UTF-8 decoding produced replacement characters
                if (!result.Contains('\uFFFD'))
                {
                    return result.TrimEnd('\0');
                }

                // UTF-8 failed, try Windows-1252
                UnifiedLogger.LogParser(LogLevel.DEBUG, $"UTF-8 decode failed, trying Windows-1252 for CExoString");
            }
            catch
            {
                // UTF-8 threw exception, try Windows-1252
            }

            // Fall back to Windows-1252 (Latin-1)
            var latin1Result = Encoding.GetEncoding(1252).GetString(buffer, offset, (int)length);
            return latin1Result.TrimEnd('\0');
        }

        private static string ReadCResRef(byte[] buffer, int offset)
        {
            ValidateBufferAccess(buffer, offset, 1);
            var length = buffer[offset++];
            
            if (length == 0) return string.Empty;
            if (length > 16) // ResRef max length is 16
            {
                throw new InvalidDataException($"CResRef length too large: {length}");
            }
            
            ValidateBufferAccess(buffer, offset, length);
            return Encoding.ASCII.GetString(buffer, offset, length).TrimEnd('\0');
        }

        private static CExoLocString ReadCExoLocString(byte[] buffer, int offset)
        {
            var locString = new CExoLocString();
            
            try
            {
                ValidateBufferAccess(buffer, offset, 12);
                
                // CExoLocString structure:
                // uint32 TotalSize
                // uint32 StrRef  
                // uint32 SubStringCount
                var totalSize = ReadUInt32LE(buffer, ref offset);
                locString.StrRef = ReadUInt32LE(buffer, ref offset);
                locString.SubStringCount = ReadUInt32LE(buffer, ref offset);
                
                UnifiedLogger.LogGff(LogLevel.DEBUG, 
                    $"CExoLocString: TotalSize={totalSize}, StrRef={locString.StrRef}, SubStrings={locString.SubStringCount}");
                
                for (uint i = 0; i < locString.SubStringCount; i++)
                {
                    ValidateBufferAccess(buffer, offset, 8);
                    var languageId = ReadUInt32LE(buffer, ref offset);
                    var stringLength = ReadUInt32LE(buffer, ref offset);
                    
                    if (stringLength > 0 && stringLength < 65535 && offset + stringLength <= buffer.Length)
                    {
                        // Phase 1 Bug Fix: Try UTF-8 first, fall back to Windows-1252
                        string text;
                        try
                        {
                            text = Encoding.UTF8.GetString(buffer, offset, (int)stringLength);

                            // Check if UTF-8 decoding produced replacement characters
                            if (text.Contains('\uFFFD'))
                            {
                                UnifiedLogger.LogParser(LogLevel.DEBUG, $"UTF-8 decode failed for CExoLocString, trying Windows-1252");
                                text = Encoding.GetEncoding(1252).GetString(buffer, offset, (int)stringLength);
                            }
                        }
                        catch
                        {
                            // UTF-8 failed, use Windows-1252
                            text = Encoding.GetEncoding(1252).GetString(buffer, offset, (int)stringLength);
                        }

                        locString.LocalizedStrings[languageId] = text.TrimEnd('\0');
                        offset += (int)stringLength;

                        UnifiedLogger.LogGff(LogLevel.DEBUG, $"  Lang {languageId}: '{text}'");
                    }
                    else if (stringLength >= 65535)
                    {
                        UnifiedLogger.LogParser(LogLevel.WARN, $"Suspicious string length {stringLength} for language {languageId}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Error parsing CExoLocString: {ex.Message}");
            }
            
            return locString;
        }

        private static GffStruct? ReadStruct(byte[] buffer, int offset, GffStruct[] structs)
        {
            ValidateBufferAccess(buffer, offset, 4);
            var structIndex = ReadUInt32LE(buffer, ref offset);
            
            if (structIndex >= structs.Length)
            {
                throw new InvalidDataException($"Invalid struct index: {structIndex} >= {structs.Length}");
            }
            
            return structs[structIndex];
        }

        private static GffList ReadList(byte[] buffer, int dataOffset, GffHeader header, GffStruct[] structs)
        {
            // Lists are stored at ListIndicesOffset + field's DataOrDataOffset
            var listOffset = (int)header.ListIndicesOffset + dataOffset;
            var list = new GffList();
            
            ValidateBufferAccess(buffer, listOffset, 4);
            var tempOffset = listOffset;
            list.Count = ReadUInt32LE(buffer, ref tempOffset);

            UnifiedLogger.LogGff(LogLevel.DEBUG, $"Reading list with {list.Count} elements at offset {listOffset}");
            
            for (uint i = 0; i < list.Count; i++)
            {
                ValidateBufferAccess(buffer, tempOffset, 4);
                var structIndex = ReadUInt32LE(buffer, ref tempOffset);
                
                if (structIndex < structs.Length)
                {
                    list.Elements.Add(structs[structIndex]);
                    
                    // Debug StartingList to see which struct it's pointing to
                    if (listOffset >= header.ListIndicesOffset + 16) // StartingList is typically after EntryList and ReplyList
                    {
                        var targetStruct = structs[structIndex];
                        var indexField = targetStruct.Fields.FirstOrDefault(f => f.Label == "Index");
                        if (indexField != null)
                        {
                            UnifiedLogger.LogParser(LogLevel.INFO, 
                                $"🔍 STARTLIST DEBUG: List element {i} → struct[{structIndex}] Type={targetStruct.Type} IndexValue={indexField.DataOrDataOffset} (0x{indexField.DataOrDataOffset:X8})");
                        }
                    }
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, 
                        $"Invalid struct index {structIndex} in list element {i}, max: {structs.Length - 1}");
                }
            }
            
            return list;
        }

        private static uint ReadUInt32LE(byte[] buffer, ref int offset)
        {
            ValidateBufferAccess(buffer, offset, 4);
            var result = BitConverter.ToUInt32(buffer, offset);
            offset += 4;
            return result;
        }

        private static float ReadFloatFieldDebug(GffField field)
        {
            var result = BitConverter.Int32BitsToSingle((int)field.DataOrDataOffset);
            if (field.Label == "Index")
            {
                UnifiedLogger.LogParser(LogLevel.INFO, 
                    $"🔍 FLOAT DEBUG: Field '{field.Label}' raw data=0x{field.DataOrDataOffset:X8}, converted float={result}");
            }
            return result;
        }

        private static void ValidateBufferAccess(byte[] buffer, int offset, int length)
        {
            if (offset < 0 || offset + length > buffer.Length)
            {
                throw new InvalidDataException(
                    $"Buffer access violation: offset={offset}, length={length}, bufferSize={buffer.Length}");
            }
        }

        // 2025-09-12: GFF pattern detection for adaptive field indices handling
        private static void DetectAndLogPattern(GffHeader header)
        {
            if (header.FieldCount == 0)
            {
                UnifiedLogger.LogParser(LogLevel.WARN, "🔍 AURORA PATTERN: Cannot detect - FieldCount is 0");
                return;
            }

            double fieldIndicesRatio = (double)header.FieldIndicesCount / header.FieldCount;
            
            // Determine GFF pattern type
            string patternType = "UNKNOWN";
            string compatibility = "❓ UNKNOWN";
            
            if (Math.Abs(fieldIndicesRatio - 4.0) < 0.1)
            {
                patternType = "STANDARD_4_1";
                compatibility = "✅ FULL SUPPORT";
            }
            else if (Math.Abs(fieldIndicesRatio - 3.5) < 0.2)  // Allow some tolerance for 3.52
            {
                patternType = "VARIANT_3_5";
                compatibility = "⚠️ ADAPTIVE NEEDED";
            }
            else if (fieldIndicesRatio > 2.0 && fieldIndicesRatio < 6.0)
            {
                patternType = "CUSTOM_RATIO";
                compatibility = "🔄 EXPERIMENTAL";
            }
            else
            {
                patternType = "NON_AURORA";
                compatibility = "❌ UNSUPPORTED";
            }

            UnifiedLogger.LogParser(LogLevel.INFO, 
                $"🔍 AURORA PATTERN DETECTION:");
            UnifiedLogger.LogParser(LogLevel.INFO, 
                $"   Field Count: {header.FieldCount}");
            UnifiedLogger.LogParser(LogLevel.INFO, 
                $"   Field Indices Count: {header.FieldIndicesCount}");
            UnifiedLogger.LogParser(LogLevel.INFO, 
                $"   Ratio: {fieldIndicesRatio:F2}:1");
            UnifiedLogger.LogParser(LogLevel.INFO, 
                $"   Pattern Type: {patternType}");
            UnifiedLogger.LogParser(LogLevel.INFO, 
                $"   Compatibility: {compatibility}");

            // Store pattern info in header for later use
            header.DetectedAuroraPattern = patternType;
            header.FieldIndicesRatio = fieldIndicesRatio;
        }
    }
}
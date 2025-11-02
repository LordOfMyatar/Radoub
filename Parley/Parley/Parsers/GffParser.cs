using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DialogEditor.Services;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Base class for parsing GFF GFF (Generic File Format) files.
    /// Provides common functionality for reading and writing GFF binary format.
    /// Specific file types (DLG, UTC, JRL, etc.) should inherit from this class.
    /// </summary>
    public abstract class GffParser
    {
        protected const int GFF_HEADER_SIZE = 56;

        #region GFF Binary Reading

        /// <summary>
        /// Parse GFF file from binary buffer.
        /// Subclasses should call this to get the root struct, then build their specific data model.
        /// </summary>
        protected GffStruct? ParseGffFromBuffer(byte[] buffer)
        {
            try
            {
                var header = GffBinaryReader.ParseGffHeader(buffer);
                var structs = GffBinaryReader.ParseStructs(buffer, header);
                var fields = GffBinaryReader.ParseFields(buffer, header);
                var labels = GffBinaryReader.ParseLabels(buffer, header);

                UnifiedLogger.LogParser(LogLevel.INFO, $"📖 READ GFF: {structs.Length} structs, {fields.Length} fields, {labels.Length} labels");

                // Resolve field labels and values
                GffBinaryReader.ResolveFieldLabels(fields, labels, buffer, header);
                GffBinaryReader.ResolveFieldValues(fields, structs, buffer, header);

                // Assign fields to their parent structs
                AssignFieldsToStructs(structs, fields, header, buffer);

                return structs.Length > 0 ? structs[0] : null;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to parse GFF: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Assign parsed fields to their parent structs based on field indices.
        /// </summary>
        private void AssignFieldsToStructs(GffStruct[] structs, GffField[] fields, GffHeader header, byte[] buffer)
        {
            for (int structIdx = 0; structIdx < structs.Length; structIdx++)
            {
                var gffStruct = structs[structIdx];
                if (gffStruct.FieldCount == 0)
                {
                    // No fields
                    continue;
                }
                else if (gffStruct.FieldCount == 1)
                {
                    // Single field - DataOrDataOffset is the field index
                    var fieldIndex = gffStruct.DataOrDataOffset;
                    if (fieldIndex < fields.Length)
                    {
                        gffStruct.Fields.Add(fields[fieldIndex]);
                    }
                    else
                    {
                        UnifiedLogger.LogParser(LogLevel.WARN,
                            $"Invalid field index {fieldIndex} for single-field struct, max: {fields.Length - 1}");
                    }
                }
                else
                {
                    // Multiple fields - DataOrDataOffset is a byte offset from FieldIndicesOffset
                    var indicesOffset = (int)(header.FieldIndicesOffset + gffStruct.DataOrDataOffset);

                    if (indicesOffset >= buffer.Length)
                    {
                        UnifiedLogger.LogParser(LogLevel.DEBUG,
                            $"Struct with {gffStruct.FieldCount} fields has invalid indices offset {indicesOffset}, skipping");
                        continue;
                    }

                    for (uint fieldIdx = 0; fieldIdx < gffStruct.FieldCount; fieldIdx++)
                    {
                        var indexPos = indicesOffset + (int)(fieldIdx * 4);
                        if (indexPos + 4 <= buffer.Length)
                        {
                            var fieldIndex = BitConverter.ToUInt32(buffer, indexPos);
                            if (fieldIndex < fields.Length)
                            {
                                gffStruct.Fields.Add(fields[fieldIndex]);
                            }
                            else
                            {
                                UnifiedLogger.LogParser(LogLevel.DEBUG,
                                    $"Invalid field index {fieldIndex} for multi-field struct field {fieldIdx}, max: {fields.Length - 1}");
                            }
                        }
                        else
                        {
                            UnifiedLogger.LogParser(LogLevel.DEBUG,
                                $"Buffer boundary reached at offset {indexPos}, stopping field assignment for this struct");
                            break;
                        }
                    }
                }
            }
        }

        #endregion

        #region GFF Binary Writing

        /// <summary>
        /// Write GFF structure to binary buffer with given signature and version.
        /// </summary>
        protected byte[] WriteGffToBinary(GffStruct rootStruct, string signature, string version)
        {
            var allStructs = new List<GffStruct>();
            var allFields = new List<GffField>();
            var allLabels = new HashSet<string>();
            var fieldData = new List<byte>();

            // Collect all components from the root struct
            CollectGffComponents(rootStruct, allStructs, allFields, allLabels, fieldData);

            // Convert to lists for binary writing
            var labelList = new List<string>(allLabels);

            // Write binary GFF
            return WriteBinaryGff(allStructs, allFields, labelList, fieldData, signature, version);
        }

        /// <summary>
        /// Recursively collect all structs, fields, labels, and field data from GFF structure.
        /// </summary>
        private void CollectGffComponents(GffStruct rootStruct, List<GffStruct> allStructs, List<GffField> allFields,
            HashSet<string> allLabels, List<byte> fieldData)
        {
            var structIndexMap = new Dictionary<GffStruct, uint>();

            // First pass: collect all structs
            CollectAllStructs(rootStruct, allStructs, structIndexMap);

            // Second pass: collect fields and data
            CollectFieldsAndData(allStructs, allFields, allLabels, fieldData, structIndexMap);
        }

        /// <summary>
        /// Recursively collect all structs and build index map.
        /// </summary>
        private void CollectAllStructs(GffStruct gffStruct, List<GffStruct> allStructs, Dictionary<GffStruct, uint> structIndexMap)
        {
            if (structIndexMap.ContainsKey(gffStruct))
                return;

            uint structIndex = (uint)allStructs.Count;
            structIndexMap[gffStruct] = structIndex;
            allStructs.Add(gffStruct);

            foreach (var field in gffStruct.Fields)
            {
                if (field.Type == GffField.Struct && field.Value is GffStruct childStruct)
                {
                    CollectAllStructs(childStruct, allStructs, structIndexMap);
                }
                else if (field.Type == GffField.List && field.Value is GffList list)
                {
                    foreach (var element in list.Elements)
                    {
                        CollectAllStructs(element, allStructs, structIndexMap);
                    }
                }
            }
        }

        /// <summary>
        /// Collect fields, labels, and field data from all structs.
        /// </summary>
        private void CollectFieldsAndData(List<GffStruct> allStructs, List<GffField> allFields, HashSet<string> allLabels,
            List<byte> fieldData, Dictionary<GffStruct, uint> structIndexMap)
        {
            foreach (var gffStruct in allStructs)
            {
                foreach (var field in gffStruct.Fields)
                {
                    allLabels.Add(field.Label);
                    allFields.Add(field);
                    ProcessFieldValue(field, allFields, fieldData, structIndexMap);
                }
            }
        }

        /// <summary>
        /// Process field value and add to field data if needed.
        /// </summary>
        private void ProcessFieldValue(GffField field, List<GffField> allFields, List<byte> fieldData,
            Dictionary<GffStruct, uint> structIndexMap)
        {
            switch (field.Type)
            {
                case GffField.CExoString:
                    if (field.Value is string strValue)
                    {
                        field.DataOrDataOffset = (uint)fieldData.Count;
                        fieldData.AddRange(BuildCExoStringFieldData(strValue));
                    }
                    break;

                case GffField.CResRef:
                    if (field.Value is string resrefValue)
                    {
                        field.DataOrDataOffset = (uint)fieldData.Count;
                        fieldData.AddRange(BuildCResRefFieldData(resrefValue));
                    }
                    break;

                case GffField.CExoLocString:
                    if (field.Value is CExoLocString locString)
                    {
                        field.DataOrDataOffset = (uint)fieldData.Count;
                        fieldData.AddRange(WriteCExoLocString(locString));
                    }
                    break;

                case GffField.Struct:
                    if (field.Value is GffStruct childStruct && structIndexMap.TryGetValue(childStruct, out var structIdx))
                    {
                        field.DataOrDataOffset = structIdx;
                    }
                    break;

                case GffField.List:
                    // List handling would need to be implemented based on specific file format requirements
                    break;
            }
        }

        /// <summary>
        /// Write complete GFF binary format.
        /// </summary>
        private byte[] WriteBinaryGff(List<GffStruct> allStructs, List<GffField> allFields, List<string> allLabels,
            List<byte> fieldData, string signature, string version)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // Calculate offsets
            uint structOffset = GFF_HEADER_SIZE;
            uint structCount = (uint)allStructs.Count;
            uint fieldOffset = structOffset + (structCount * 12); // Each struct is 12 bytes
            uint fieldCount = (uint)allFields.Count;
            uint labelOffset = fieldOffset + (fieldCount * 12); // Each field is 12 bytes
            uint labelCount = (uint)allLabels.Count;
            uint labelSize = CalculateLabelSize(allLabels);
            uint fieldDataOffset = labelOffset + labelSize;
            uint fieldDataCount = (uint)fieldData.Count;
            uint fieldIndicesOffset = fieldDataOffset + fieldDataCount;
            uint fieldIndicesCount = fieldCount * 4; // 4 indices per field (GFF pattern)
            uint listIndicesOffset = fieldIndicesOffset + (fieldIndicesCount * 4);
            uint listIndicesCount = 0; // Would need to be calculated based on lists

            // Write header
            writer.Write(Encoding.ASCII.GetBytes(signature.PadRight(4, '\0').Substring(0, 4)));
            writer.Write(Encoding.ASCII.GetBytes(version.PadRight(4, '\0').Substring(0, 4)));
            writer.Write(structOffset);
            writer.Write(structCount);
            writer.Write(fieldOffset);
            writer.Write(fieldCount);
            writer.Write(labelOffset);
            writer.Write(labelCount);
            writer.Write(fieldDataOffset);
            writer.Write(fieldDataCount);
            writer.Write(fieldIndicesOffset);
            writer.Write(fieldIndicesCount);
            writer.Write(listIndicesOffset);
            writer.Write(listIndicesCount);

            // Write structs
            foreach (var gffStruct in allStructs)
            {
                writer.Write(gffStruct.Type);
                writer.Write(gffStruct.DataOrDataOffset);
                writer.Write(gffStruct.FieldCount);
            }

            // Write fields
            var labelIndexMap = CreateLabelIndexMap(allLabels);
            foreach (var field in allFields)
            {
                writer.Write(field.Type);
                writer.Write(labelIndexMap[field.Label]);
                writer.Write(field.DataOrDataOffset);
            }

            // Write labels (16-byte fixed format for GFF)
            foreach (var label in allLabels)
            {
                var labelBytes = Encoding.ASCII.GetBytes(label);
                var paddedLabel = new byte[16];
                Array.Copy(labelBytes, paddedLabel, Math.Min(labelBytes.Length, 16));
                writer.Write(paddedLabel);
            }

            // Write field data
            writer.Write(fieldData.ToArray());

            // Write field indices (GFF 4:1 pattern)
            for (uint i = 0; i < fieldCount; i++)
            {
                writer.Write(i); // Simple sequential indices for now
                writer.Write(i);
                writer.Write(i);
                writer.Write(i);
            }

            return ms.ToArray();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Build CExoString field data (length + string bytes).
        /// </summary>
        protected byte[] BuildCExoStringFieldData(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var result = new byte[4 + bytes.Length];
            BitConverter.GetBytes((uint)bytes.Length).CopyTo(result, 0);
            bytes.CopyTo(result, 4);
            return result;
        }

        /// <summary>
        /// Build CResRef field data (1-byte length + string bytes, max 16).
        /// </summary>
        protected byte[] BuildCResRefFieldData(string resref)
        {
            var truncated = resref.Length > 16 ? resref.Substring(0, 16) : resref;
            var bytes = Encoding.ASCII.GetBytes(truncated);
            var result = new byte[1 + bytes.Length];
            result[0] = (byte)bytes.Length;
            bytes.CopyTo(result, 1);
            return result;
        }

        /// <summary>
        /// Write CExoLocString to field data.
        /// </summary>
        protected byte[] WriteCExoLocString(CExoLocString locString)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write((uint)locString.LocalizedStrings.Count); // Total size (placeholder)
            writer.Write(locString.StrRef);
            writer.Write((uint)locString.LocalizedStrings.Count); // String count

            foreach (var kvp in locString.LocalizedStrings)
            {
                writer.Write(kvp.Key); // Language ID
                var stringBytes = Encoding.UTF8.GetBytes(kvp.Value);
                writer.Write((uint)stringBytes.Length); // String length
                writer.Write(stringBytes); // String data
            }

            // Update total size at the beginning
            var buffer = ms.ToArray();
            BitConverter.GetBytes((uint)(buffer.Length - 4)).CopyTo(buffer, 0);

            return buffer;
        }

        /// <summary>
        /// Create label to index mapping.
        /// </summary>
        protected Dictionary<string, uint> CreateLabelIndexMap(List<string> allLabels)
        {
            var map = new Dictionary<string, uint>();
            for (int i = 0; i < allLabels.Count; i++)
            {
                map[allLabels[i]] = (uint)i;
            }
            return map;
        }

        /// <summary>
        /// Calculate total size of label section (16 bytes per label for GFF).
        /// </summary>
        protected uint CalculateLabelSize(List<string> allLabels)
        {
            return (uint)(allLabels.Count * 16); // GFF uses 16-byte fixed labels
        }

        #endregion
    }
}

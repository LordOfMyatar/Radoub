using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.Utils;
using Newtonsoft.Json;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Dialog-specific GFF parser for Neverwinter Nights DLG files.
    /// Inherits common GFF functionality from GffParser base class.
    /// Delegates writing to DialogWriter and building to DialogBuilder.
    /// </summary>
    public class DialogParser : GffParser, IDialogParser
    {
        // Support class for building Dialog models from GFF structs
        private readonly DialogBuilder _dialogBuilder = new();

        // Support class for Dialog-to-binary write operations
        private readonly DialogWriter _dialogWriter = new();

        public async Task<Dialog?> ParseFromFileAsync(string filePath)
        {
            try
            {
                // Set per-file logging context
                UnifiedLogger.SetFileContext(filePath);

                UnifiedLogger.LogParser(LogLevel.INFO, "═══════════════════════════════════════════════════════════");
                UnifiedLogger.LogParser(LogLevel.INFO, $"📂 OPENING FILE: {UnifiedLogger.SanitizePath(filePath)}");
                UnifiedLogger.LogParser(LogLevel.INFO, $"   Parley v{DialogEditor.Utils.VersionHelper.FullVersion}");
                UnifiedLogger.LogParser(LogLevel.INFO, "═══════════════════════════════════════════════════════════");

                if (!IsValidDlgFile(filePath))
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"❌ Invalid DLG file: {UnifiedLogger.SanitizePath(filePath)}");
                    return null;
                }

                var buffer = await File.ReadAllBytesAsync(filePath);
                var result = await ParseFromBufferAsync(buffer);

                if (result != null)
                {
                    UnifiedLogger.LogParser(LogLevel.INFO, $"✅ Successfully opened: {UnifiedLogger.SanitizePath(filePath)}");
                    UnifiedLogger.LogParser(LogLevel.INFO, "═══════════════════════════════════════════════════════════");
                }

                return result;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"❌ Failed to open {UnifiedLogger.SanitizePath(filePath)}: {ex.Message}");
                return null;
            }
            finally
            {
                // Clear per-file logging context
                UnifiedLogger.ClearFileContext();
            }
        }

        public async Task<Dialog?> ParseFromStreamAsync(Stream stream)
        {
            try
            {
                UnifiedLogger.LogParser(LogLevel.DEBUG, "Starting to parse DLG from stream");

                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var buffer = memoryStream.ToArray();

                return await ParseFromBufferAsync(buffer);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to parse DLG from stream: {ex.Message}");
                return null;
            }
        }

        public async Task<Dialog?> ParseFromJsonAsync(string jsonContent)
        {
            try
            {
                UnifiedLogger.LogParser(LogLevel.DEBUG, "Starting to parse DLG from JSON");

                return await Task.Run(() =>
                {
                    var dialog = JsonConvert.DeserializeObject<Dialog>(jsonContent);
                    if (dialog != null)
                    {
                        UnifiedLogger.LogParser(LogLevel.DEBUG, "Successfully parsed DLG from JSON");
                    }
                    return dialog;
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to parse DLG from JSON: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> WriteToFileAsync(Dialog dialog, string filePath)
        {
            try
            {
                // Set per-file logging context
                UnifiedLogger.SetFileContext(filePath);

                UnifiedLogger.LogParser(LogLevel.INFO, "═══════════════════════════════════════════════════════════");
                UnifiedLogger.LogParser(LogLevel.INFO, $"📝 SAVING FILE: {UnifiedLogger.SanitizePath(filePath)}");
                UnifiedLogger.LogParser(LogLevel.INFO, $"   Parley v{DialogEditor.Utils.VersionHelper.FullVersion}");
                UnifiedLogger.LogParser(LogLevel.INFO, "═══════════════════════════════════════════════════════════");

                var buffer = CreateDlgBuffer(dialog);
                await File.WriteAllBytesAsync(filePath, buffer);

                UnifiedLogger.LogParser(LogLevel.INFO, $"✅ Successfully saved: {UnifiedLogger.SanitizePath(filePath)}");
                UnifiedLogger.LogParser(LogLevel.INFO, "═══════════════════════════════════════════════════════════");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"❌ Failed to save {UnifiedLogger.SanitizePath(filePath)}: {ex.Message}");
                return false;
            }
            finally
            {
                // Clear per-file logging context
                UnifiedLogger.ClearFileContext();
            }
        }

        public async Task<bool> WriteToStreamAsync(Dialog dialog, Stream stream)
        {
            try
            {
                UnifiedLogger.LogParser(LogLevel.DEBUG, "Starting to write DLG to stream");

                var buffer = CreateDlgBuffer(dialog);
                await stream.WriteAsync(buffer, 0, buffer.Length);

                UnifiedLogger.LogParser(LogLevel.DEBUG, "Successfully wrote DLG to stream");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to write DLG to stream: {ex.Message}");
                return false;
            }
        }

        public async Task<string> WriteToJsonAsync(Dialog dialog)
        {
            try
            {
                UnifiedLogger.LogParser(LogLevel.DEBUG, "Starting to write DLG to JSON");

                return await Task.Run(() =>
                {
                    var json = JsonConvert.SerializeObject(dialog, Formatting.Indented);
                    UnifiedLogger.LogParser(LogLevel.DEBUG, "Successfully wrote DLG to JSON");
                    return json;
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to write DLG to JSON: {ex.Message}");
                return string.Empty;
            }
        }

        public bool IsValidDlgFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                var extension = Path.GetExtension(filePath);
                if (!extension.Equals(".dlg", StringComparison.OrdinalIgnoreCase))
                    return false;

                // Quick check for GFF signature
                using var stream = File.OpenRead(filePath);
                var buffer = new byte[8];
                if (stream.Read(buffer, 0, 8) != 8)
                    return false;

                var signature = System.Text.Encoding.ASCII.GetString(buffer, 0, 4);
                var version = System.Text.Encoding.ASCII.GetString(buffer, 4, 4);

                // DLG files use "DLG " signature with GFF v3.28+ format
                return signature == "DLG " && version == "V3.2";
            }
            catch
            {
                return false;
            }
        }

        public ParserResult ValidateStructure(Dialog dialog)
        {
            var result = ParserResult.CreateSuccess();

            try
            {
                // Basic validation
                if (dialog.Entries.Count == 0 && dialog.Replies.Count == 0)
                {
                    result.AddWarning("Dialog has no entries or replies");
                }

                if (dialog.Starts.Count == 0)
                {
                    result.AddWarning("Dialog has no starting points");
                }

                // Note: Empty nodes (no text, no comment) are valid in NWN dialogs
                // They serve as "[CONTINUE]" nodes for conversation flow

                UnifiedLogger.LogParser(LogLevel.DEBUG,
                    $"Dialog validation completed with {result.Warnings.Count} warnings");

                return result;
            }
            catch (Exception ex)
            {
                return ParserResult.CreateError("Validation failed", ex);
            }
        }

        public async Task<Dialog?> ParseFromBufferAsync(byte[] buffer)
        {
            // Capture file context before Task.Run (ThreadStatic doesn't propagate across threads)
            var fileContext = UnifiedLogger.GetFileContext();

            return await Task.Run(() =>
            {
                try
                {
                    // Restore file context in worker thread
                    if (!string.IsNullOrEmpty(fileContext))
                    {
                        UnifiedLogger.SetFileContext(fileContext);
                    }

                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"Parsing GFF buffer of {buffer.Length} bytes");

                    // Parse GFF structure
                    var header = GffBinaryReader.ParseGffHeader(buffer);

                    if (header.FileType != "DLG " || header.FileVersion != "V3.2")
                    {
                        throw new InvalidDataException($"Invalid DLG format: {header.FileType} {header.FileVersion}");
                    }

                    var structs = GffBinaryReader.ParseStructs(buffer, header);
                    var fields = GffBinaryReader.ParseFields(buffer, header);
                    var labels = GffBinaryReader.ParseLabels(buffer, header);

                    // Resolve field labels and values
                    GffBinaryReader.ResolveFieldLabels(fields, labels, buffer, header);
                    GffBinaryReader.ResolveFieldValues(fields, structs, buffer, header);

                    // Assign fields to their parent structs
                    AssignFieldsToStructs(structs, fields, header, buffer);

                    // Convert GFF root struct to Dialog
                    if (structs.Length == 0)
                    {
                        throw new InvalidDataException("No root struct found in GFF file");
                    }

                    var dialog = _dialogBuilder.BuildDialogFromGffStruct(structs[0]);

                    UnifiedLogger.LogParser(LogLevel.INFO,
                        $"Successfully parsed dialog with {dialog.Entries.Count} entries and {dialog.Replies.Count} replies");

                    return dialog;
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to parse GFF buffer: {ex.Message}");
                    return null;
                }
            });
        }

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

                    // Check if the base offset is reasonable
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
                                var assignedField = fields[fieldIndex];
                                gffStruct.Fields.Add(assignedField);

                                // Debug first struct's fields (Entry or Reply struct 0)
                                if (structIdx < 3 && fieldIdx < 3)
                                {
                                    UnifiedLogger.LogParser(LogLevel.TRACE,
                                        $"🔧 Struct[{structIdx}].Field[{fieldIdx}]: Retrieved fields[{fieldIndex}] - Type={assignedField.Type}, Label={assignedField.Label ?? "unlabeled"}, DataOrDataOffset={assignedField.DataOrDataOffset}");
                                }
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
                            break; // Stop processing this struct's fields
                        }
                    }
                }
            }
        }

        private byte[] CreateDlgBuffer(Dialog dialog)
        {
            return _dialogWriter.CreateDlgBuffer(dialog);
        }
    }
}

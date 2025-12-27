using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Gff;
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

                    // Parse GFF structure using Radoub.Formats.Gff.GffReader
                    var gffFile = GffReader.Read(buffer);

                    if (gffFile.FileType != "DLG ")
                    {
                        throw new InvalidDataException($"Invalid DLG format: {gffFile.FileType} {gffFile.FileVersion}");
                    }

                    // Convert GFF root struct to Dialog
                    var dialog = _dialogBuilder.BuildDialogFromGffStruct(gffFile.RootStruct);

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

        private byte[] CreateDlgBuffer(Dialog dialog)
        {
            return _dialogWriter.CreateDlgBuffer(dialog);
        }
    }
}

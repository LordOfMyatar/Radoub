using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Parsers;
using Radoub.Formats.Dlg;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service facade for dialog file I/O operations.
    /// Provides clean public API and encapsulates DialogParser implementation details.
    /// Phase 4 of parser refactoring - Oct 28, 2025
    /// Phase 5: Migration to Radoub.Formats.Dlg - Dec 27, 2025
    /// </summary>
    public class DialogFileService
    {
        private readonly DialogParser _legacyParser;

        /// <summary>
        /// If true, uses new Radoub.Formats.Dlg parser. If false, uses legacy Parley parser.
        /// Enabled by default as of Dec 27, 2025 (#560).
        /// </summary>
        public bool UseNewParser { get; set; } = true;

        public DialogFileService()
        {
            _legacyParser = new DialogParser();
        }

        /// <summary>
        /// Loads a dialog from a DLG file.
        /// </summary>
        /// <param name="filePath">Path to the DLG file</param>
        /// <returns>Dialog object if successful, null otherwise</returns>
        public async Task<Dialog?> LoadFromFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Dialog file not found", filePath);

            if (UseNewParser)
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        UnifiedLogger.LogParser(LogLevel.INFO, $"[NEW PARSER] Loading: {Path.GetFileName(filePath)}");
                        var dlgFile = DlgReader.Read(filePath);
                        return DlgAdapter.ToDialog(dlgFile);
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogParser(LogLevel.ERROR, $"[NEW PARSER] Load failed: {ex.Message}");
                        return null;
                    }
                });
            }

            return await _legacyParser.ParseFromFileAsync(filePath);
        }

        /// <summary>
        /// Loads a dialog from a stream.
        /// </summary>
        /// <param name="stream">Stream containing DLG data</param>
        /// <returns>Dialog object if successful, null otherwise</returns>
        public async Task<Dialog?> LoadFromStreamAsync(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (UseNewParser)
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        UnifiedLogger.LogParser(LogLevel.INFO, "[NEW PARSER] Loading from stream");
                        var dlgFile = DlgReader.Read(stream);
                        return DlgAdapter.ToDialog(dlgFile);
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogParser(LogLevel.ERROR, $"[NEW PARSER] Load from stream failed: {ex.Message}");
                        return null;
                    }
                });
            }

            return await _legacyParser.ParseFromStreamAsync(stream);
        }

        /// <summary>
        /// Loads a dialog from a byte buffer.
        /// </summary>
        /// <param name="buffer">Byte array containing DLG data</param>
        /// <returns>Dialog object if successful, null otherwise</returns>
        public async Task<Dialog?> LoadFromBufferAsync(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                throw new ArgumentNullException(nameof(buffer));

            if (UseNewParser)
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        UnifiedLogger.LogParser(LogLevel.INFO, "[NEW PARSER] Loading from buffer");
                        var dlgFile = DlgReader.Read(buffer);
                        return DlgAdapter.ToDialog(dlgFile);
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogParser(LogLevel.ERROR, $"[NEW PARSER] Load from buffer failed: {ex.Message}");
                        return null;
                    }
                });
            }

            return await _legacyParser.ParseFromBufferAsync(buffer);
        }

        /// <summary>
        /// Loads a dialog from JSON representation.
        /// JSON parsing always uses legacy parser (no change needed).
        /// </summary>
        /// <param name="json">JSON string containing dialog data</param>
        /// <returns>Dialog object if successful, null otherwise</returns>
        public async Task<Dialog?> LoadFromJsonAsync(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentNullException(nameof(json));

            // JSON parsing stays with legacy parser - it's Parley-specific
            return await _legacyParser.ParseFromJsonAsync(json);
        }

        /// <summary>
        /// Saves a dialog to a DLG file.
        /// </summary>
        /// <param name="dialog">Dialog to save</param>
        /// <param name="filePath">Path where the DLG file should be saved</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> SaveToFileAsync(Dialog dialog, string filePath)
        {
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));

            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            if (UseNewParser)
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        UnifiedLogger.LogParser(LogLevel.INFO, $"[NEW PARSER] Saving: {Path.GetFileName(filePath)}");
                        var dlgFile = DlgAdapter.ToDlgFile(dialog);
                        DlgWriter.Write(dlgFile, filePath);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogParser(LogLevel.ERROR, $"[NEW PARSER] Save failed: {ex.Message}");
                        return false;
                    }
                });
            }

            return await _legacyParser.WriteToFileAsync(dialog, filePath);
        }

        /// <summary>
        /// Saves a dialog to a stream.
        /// </summary>
        /// <param name="dialog">Dialog to save</param>
        /// <param name="stream">Stream to write DLG data to</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> SaveToStreamAsync(Dialog dialog, Stream stream)
        {
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (UseNewParser)
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        UnifiedLogger.LogParser(LogLevel.INFO, "[NEW PARSER] Saving to stream");
                        var dlgFile = DlgAdapter.ToDlgFile(dialog);
                        DlgWriter.Write(dlgFile, stream);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogParser(LogLevel.ERROR, $"[NEW PARSER] Save to stream failed: {ex.Message}");
                        return false;
                    }
                });
            }

            return await _legacyParser.WriteToStreamAsync(dialog, stream);
        }

        /// <summary>
        /// Converts a dialog to JSON representation.
        /// JSON export always uses legacy parser (no change needed).
        /// </summary>
        /// <param name="dialog">Dialog to convert</param>
        /// <returns>JSON string</returns>
        public async Task<string> ConvertToJsonAsync(Dialog dialog)
        {
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));

            // JSON export stays with legacy parser - it's Parley-specific
            return await _legacyParser.WriteToJsonAsync(dialog);
        }

        /// <summary>
        /// Validates that a file is a valid DLG file.
        /// </summary>
        /// <param name="filePath">Path to the file to validate</param>
        /// <returns>True if valid DLG file, false otherwise</returns>
        public bool IsValidDlgFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            if (UseNewParser)
            {
                try
                {
                    var dlgFile = DlgReader.Read(filePath);
                    return dlgFile != null;
                }
                catch
                {
                    return false;
                }
            }

            return _legacyParser.IsValidDlgFile(filePath);
        }

        /// <summary>
        /// Validates the structure of a dialog object.
        /// Validation always uses legacy parser (has validation logic).
        /// </summary>
        /// <param name="dialog">Dialog to validate</param>
        /// <returns>ParserResult with validation details</returns>
        public ParserResult ValidateStructure(Dialog dialog)
        {
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));

            // Validation stays with legacy parser - it has the validation logic
            return _legacyParser.ValidateStructure(dialog);
        }
    }
}

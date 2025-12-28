using System;
using Radoub.Formats.Logging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DialogEditor.Models;
using Newtonsoft.Json;
using Radoub.Formats.Dlg;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service facade for dialog file I/O operations.
    /// Uses Radoub.Formats.Dlg for DLG file parsing/writing and
    /// DlgAdapter for converting to/from Parley's Dialog model.
    /// </summary>
    public class DialogFileService
    {
        private readonly DialogValidator _validator = new();

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

            return await Task.Run(() =>
            {
                try
                {
                    UnifiedLogger.LogParser(LogLevel.INFO, $"Loading: {Path.GetFileName(filePath)}");
                    var dlgFile = DlgReader.Read(filePath);
                    return DlgAdapter.ToDialog(dlgFile);
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"Load failed: {ex.Message}");
                    return null;
                }
            });
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

            return await Task.Run(() =>
            {
                try
                {
                    UnifiedLogger.LogParser(LogLevel.INFO, "Loading from stream");
                    var dlgFile = DlgReader.Read(stream);
                    return DlgAdapter.ToDialog(dlgFile);
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"Load from stream failed: {ex.Message}");
                    return null;
                }
            });
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

            return await Task.Run(() =>
            {
                try
                {
                    UnifiedLogger.LogParser(LogLevel.INFO, "Loading from buffer");
                    var dlgFile = DlgReader.Read(buffer);
                    return DlgAdapter.ToDialog(dlgFile);
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"Load from buffer failed: {ex.Message}");
                    return null;
                }
            });
        }

        /// <summary>
        /// Loads a dialog from JSON representation.
        /// </summary>
        /// <param name="json">JSON string containing dialog data</param>
        /// <returns>Dialog object if successful, null otherwise</returns>
        public async Task<Dialog?> LoadFromJsonAsync(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentNullException(nameof(json));

            return await Task.Run(() =>
            {
                try
                {
                    UnifiedLogger.LogParser(LogLevel.DEBUG, "Parsing dialog from JSON");
                    var dialog = JsonConvert.DeserializeObject<Dialog>(json);
                    if (dialog != null)
                    {
                        UnifiedLogger.LogParser(LogLevel.DEBUG, "Successfully parsed dialog from JSON");
                    }
                    return dialog;
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to parse dialog from JSON: {ex.Message}");
                    return null;
                }
            });
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

            return await Task.Run(() =>
            {
                try
                {
                    UnifiedLogger.LogParser(LogLevel.INFO, $"Saving: {Path.GetFileName(filePath)}");
                    var dlgFile = DlgAdapter.ToDlgFile(dialog);
                    DlgWriter.Write(dlgFile, filePath);
                    return true;
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"Save failed: {ex.Message}");
                    return false;
                }
            });
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

            return await Task.Run(() =>
            {
                try
                {
                    UnifiedLogger.LogParser(LogLevel.INFO, "Saving to stream");
                    var dlgFile = DlgAdapter.ToDlgFile(dialog);
                    DlgWriter.Write(dlgFile, stream);
                    return true;
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"Save to stream failed: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Converts a dialog to JSON representation.
        /// </summary>
        /// <param name="dialog">Dialog to convert</param>
        /// <returns>JSON string</returns>
        public async Task<string> ConvertToJsonAsync(Dialog dialog)
        {
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));

            return await Task.Run(() =>
            {
                try
                {
                    UnifiedLogger.LogParser(LogLevel.DEBUG, "Converting dialog to JSON");
                    var json = JsonConvert.SerializeObject(dialog, Formatting.Indented);
                    UnifiedLogger.LogParser(LogLevel.DEBUG, "Successfully converted dialog to JSON");
                    return json;
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to convert dialog to JSON: {ex.Message}");
                    return string.Empty;
                }
            });
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

            if (!File.Exists(filePath))
                return false;

            var extension = Path.GetExtension(filePath);
            if (!extension.Equals(".dlg", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                // Quick check for GFF/DLG signature
                using var stream = File.OpenRead(filePath);
                var buffer = new byte[8];
                if (stream.Read(buffer, 0, 8) != 8)
                    return false;

                var signature = Encoding.ASCII.GetString(buffer, 0, 4);
                var version = Encoding.ASCII.GetString(buffer, 4, 4);

                // DLG files use "DLG " signature with GFF v3.2 format
                return signature == "DLG " && version == "V3.2";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates the structure of a dialog object.
        /// </summary>
        /// <param name="dialog">Dialog to validate</param>
        /// <returns>ParserResult with validation details</returns>
        public ParserResult ValidateStructure(Dialog dialog)
        {
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));

            return _validator.ValidateStructure(dialog);
        }
    }
}

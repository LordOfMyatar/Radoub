using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Parsers;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service facade for dialog file I/O operations.
    /// Provides clean public API and encapsulates DialogParser implementation details.
    /// Phase 4 of parser refactoring - Oct 28, 2025
    /// </summary>
    public class DialogFileService
    {
        private readonly DialogParser _parser;

        public DialogFileService()
        {
            _parser = new DialogParser();
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

            return await _parser.ParseFromFileAsync(filePath);
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

            return await _parser.ParseFromStreamAsync(stream);
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

            return await _parser.ParseFromBufferAsync(buffer);
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

            return await _parser.ParseFromJsonAsync(json);
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

            return await _parser.WriteToFileAsync(dialog, filePath);
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

            return await _parser.WriteToStreamAsync(dialog, stream);
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

            return await _parser.WriteToJsonAsync(dialog);
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

            return _parser.IsValidDlgFile(filePath);
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

            return _parser.ValidateStructure(dialog);
        }
    }
}

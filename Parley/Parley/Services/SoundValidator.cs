using System;
using System.IO;
using DialogEditor.Utils;

namespace DialogEditor.Services
{
    /// <summary>
    /// Validates sound files against NWN specifications.
    /// Based on: https://nwn.wiki/display/NWN1/Sounds+and+Music
    /// </summary>
    public static class SoundValidator
    {
        public const int MAX_FILENAME_LENGTH = 16; // NWN limitation
        public const int MAX_FILE_SIZE_MB = 15;     // NWSync limitation

        /// <summary>
        /// Validate sound file and return warnings/errors.
        /// </summary>
        /// <param name="filePath">Path to the sound file to validate.</param>
        /// <param name="isVoiceOrSfx">True for voice/SFX (requires mono), false for ambient sounds.</param>
        /// <param name="skipFilenameCheck">True to skip filename length validation (for temp files extracted from HAK).</param>
        public static SoundValidationResult Validate(string filePath, bool isVoiceOrSfx = true, bool skipFilenameCheck = false)
        {
            var result = new SoundValidationResult { IsValid = true };
            var fileName = Path.GetFileName(filePath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // Check filename length (without extension) - skip for temp files from HAK
            if (!skipFilenameCheck && fileNameWithoutExt.Length > MAX_FILENAME_LENGTH)
            {
                result.IsValid = false;
                result.Errors.Add($"Filename too long: {fileNameWithoutExt.Length} chars (max {MAX_FILENAME_LENGTH})");
            }

            // Check file size
            var fileInfo = new FileInfo(filePath);
            var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
            if (fileSizeMB > MAX_FILE_SIZE_MB)
            {
                result.IsValid = false;
                result.Errors.Add($"File too large: {fileSizeMB:F2} MB (max {MAX_FILE_SIZE_MB} MB for NWSync)");
            }

            // Check WAV format details if it's a WAV file
            if (extension == ".wav")
            {
                try
                {
                    var wavInfo = AnalyzeWavFile(filePath);

                    // Check if it's a valid WAV file
                    if (!wavInfo.IsValidWav)
                    {
                        result.IsValidWav = false;
                        result.InvalidWavReason = wavInfo.InvalidReason;
                        result.Errors.Add($"❌ {wavInfo.InvalidReason}");
                        // Don't mark IsValid = false, just warn - user can still try to play
                        result.FormatInfo = "Invalid WAV format";
                        return result;
                    }

                    if (!wavInfo.HasFmtChunk)
                    {
                        result.IsValidWav = false;
                        result.InvalidWavReason = wavInfo.InvalidReason;
                        result.Warnings.Add($"⚠️ {wavInfo.InvalidReason}");
                        result.FormatInfo = "Malformed WAV";
                        return result;
                    }

                    // Sample rate check
                    if (wavInfo.SampleRate != 44100 && wavInfo.SampleRate != 41000)
                    {
                        result.Warnings.Add($"Sample rate {wavInfo.SampleRate} Hz - NWN recommends 44,100 or 41,000 Hz");
                    }

                    // Track channel info
                    result.Channels = wavInfo.Channels;
                    result.IsMono = wavInfo.Channels == 1;

                    // Channel check - stereo is NOT COMPATIBLE with NWN conversations
                    if (isVoiceOrSfx && wavInfo.Channels > 1)
                    {
                        result.Errors.Add("⚠️ Stereo - not compatible with conversations");
                        result.IsValid = false;
                    }

                    // Format check
                    if (wavInfo.AudioFormat == 1) // PCM
                    {
                        result.Warnings.Add("Uncompressed WAV - consider MP3 for smaller file size (5-7x reduction)");
                    }

                    // Add format info
                    result.FormatInfo = $"{wavInfo.SampleRate}Hz, {wavInfo.Channels}ch, {wavInfo.BitsPerSample}-bit, Format:{wavInfo.AudioFormat}";
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Could not analyze WAV format: {ex.Message}");
                }
            }
            else if (extension == ".mp3")
            {
                result.Warnings.Add("MP3 files need 'BMU V1.0' header when used as .WAV - verify file is properly formatted");
            }
            else if (extension == ".bmu")
            {
                // BMU files are typically MP3 with special header
                result.FormatInfo = "BMU music file";
            }
            else
            {
                result.Warnings.Add($"Unusual extension '{extension}' - NWN expects .wav or .bmu");
            }

            return result;
        }

        /// <summary>
        /// Quick check if a WAV file is mono (compatible with NWN conversations).
        /// Returns true if mono or if format cannot be determined.
        /// </summary>
        public static bool IsMonoWav(string filePath)
        {
            try
            {
                var info = AnalyzeWavFile(filePath);
                return info.Channels == 1;
            }
            catch
            {
                // If we can't read it, assume mono (let user decide)
                return true;
            }
        }

        /// <summary>
        /// Get channel count of a WAV file (1=mono, 2=stereo).
        /// Returns 1 if format cannot be determined.
        /// </summary>
        public static int GetWavChannelCount(string filePath)
        {
            try
            {
                var info = AnalyzeWavFile(filePath);
                return info.Channels;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>
        /// Analyze WAV file format details.
        /// </summary>
        private static WavFormatInfo AnalyzeWavFile(string filePath)
        {
            var info = new WavFormatInfo();
            var bytes = File.ReadAllBytes(filePath);

            // Check for RIFF header (bytes 0-3 should be "RIFF")
            if (bytes.Length < 44) // Minimum WAV header size
            {
                info.IsValidWav = false;
                info.InvalidReason = "File too small to be valid WAV";
                return info;
            }

            // Check RIFF signature
            if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F')
            {
                info.IsValidWav = false;
                info.InvalidReason = "No RIFF header - not a standard WAV file";
                return info;
            }

            // Check WAVE format (bytes 8-11 should be "WAVE")
            if (bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E')
            {
                info.IsValidWav = false;
                info.InvalidReason = "No WAVE format marker - not a standard WAV file";
                return info;
            }

            info.IsValidWav = true;

            // Find fmt chunk
            for (int i = 12; i < bytes.Length - 20; i++)
            {
                if (bytes[i] == 'f' && bytes[i + 1] == 'm' && bytes[i + 2] == 't' && bytes[i + 3] == ' ')
                {
                    int fmtStart = i + 8;

                    info.AudioFormat = BitConverter.ToUInt16(bytes, fmtStart);
                    info.Channels = BitConverter.ToUInt16(bytes, fmtStart + 2);
                    info.SampleRate = BitConverter.ToUInt32(bytes, fmtStart + 4);
                    info.BitsPerSample = BitConverter.ToUInt16(bytes, fmtStart + 14);
                    info.HasFmtChunk = true;

                    break;
                }
            }

            if (!info.HasFmtChunk)
            {
                info.InvalidReason = "No fmt chunk found - malformed WAV file";
            }

            return info;
        }

        private class WavFormatInfo
        {
            public ushort AudioFormat { get; set; }
            public ushort Channels { get; set; }
            public uint SampleRate { get; set; }
            public ushort BitsPerSample { get; set; }
            public bool IsValidWav { get; set; } = true;
            public bool HasFmtChunk { get; set; }
            public string InvalidReason { get; set; } = "";
        }
    }

    /// <summary>
    /// Sound validation result with errors and warnings.
    /// </summary>
    public class SoundValidationResult
    {
        public bool IsValid { get; set; }
        public System.Collections.Generic.List<string> Errors { get; set; } = new();
        public System.Collections.Generic.List<string> Warnings { get; set; } = new();
        public string FormatInfo { get; set; } = "";

        /// <summary>
        /// True if this is a mono audio file (or if channel count couldn't be determined).
        /// Stereo files are NOT compatible with NWN conversations.
        /// </summary>
        public bool IsMono { get; set; } = true;

        /// <summary>
        /// Number of audio channels (1=mono, 2=stereo).
        /// </summary>
        public int Channels { get; set; } = 1;

        /// <summary>
        /// True if the file has a valid WAV header (RIFF/WAVE).
        /// False for non-WAV files masquerading as .wav (e.g., MP3 with .wav extension).
        /// </summary>
        public bool IsValidWav { get; set; } = true;

        /// <summary>
        /// If IsValidWav is false, describes why the file is invalid.
        /// </summary>
        public string InvalidWavReason { get; set; } = "";

        public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;
    }
}

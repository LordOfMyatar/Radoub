using System;
using System.IO;
using System.Linq;
using DialogEditor.Models.Sound;
using Radoub.Formats.Bif;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Handles extraction and validation of sounds from HAK and BIF archives.
    /// </summary>
    public class SoundExtractor
    {
        private readonly SoundCache _cache;
        private string? _tempExtractedPath;

        public SoundExtractor()
        {
            _cache = SoundCache.Instance;
        }

        /// <summary>
        /// Validates a HAK sound by extracting temporarily and checking format.
        /// Updates the soundInfo with validation results.
        /// </summary>
        /// <returns>Validation result with format info or error message.</returns>
        public ArchiveSoundValidationResult ValidateHakSound(SoundFileInfo soundInfo)
        {
            if (soundInfo.HakPath == null || soundInfo.ErfEntry == null)
            {
                return new ArchiveSoundValidationResult
                {
                    IsValid = true,
                    Source = soundInfo.Source,
                    IsFromHak = true
                };
            }

            try
            {
                var soundData = ErfReader.ExtractResource(soundInfo.HakPath, soundInfo.ErfEntry);
                var safeResRef = SanitizeForFileName(soundInfo.ErfEntry.ResRef);
                var tempPath = Path.Combine(Path.GetTempPath(), $"pv_{safeResRef}.wav");
                File.WriteAllBytes(tempPath, soundData);

                try
                {
                    var validation = SoundValidator.Validate(tempPath, isVoiceOrSfx: true, skipFilenameCheck: true);
                    soundInfo.IsMono = validation.IsMono;
                    soundInfo.ChannelUnknown = false; // Now verified
                    soundInfo.IsValidWav = validation.IsValidWav;
                    soundInfo.InvalidWavReason = validation.InvalidWavReason;

                    return new ArchiveSoundValidationResult
                    {
                        IsValid = validation.IsValid,
                        IsValidWav = validation.IsValidWav,
                        InvalidWavReason = validation.InvalidWavReason,
                        HasIssues = validation.HasIssues,
                        Errors = validation.Errors.ToArray(),
                        Warnings = validation.Warnings.ToArray(),
                        FormatInfo = validation.FormatInfo,
                        Source = soundInfo.Source,
                        IsFromHak = true
                    };
                }
                finally
                {
                    try { File.Delete(tempPath); }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogApplication(LogLevel.TRACE, $"Could not delete temp file {UnifiedLogger.SanitizePath(tempPath)}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Could not validate HAK sound: {ex.Message}");
                return new ArchiveSoundValidationResult
                {
                    IsValid = true,
                    ValidationUnavailable = true,
                    Source = soundInfo.Source,
                    IsFromHak = true
                };
            }
        }

        /// <summary>
        /// Validates a BIF sound by extracting temporarily and checking format.
        /// Updates the soundInfo with validation results.
        /// </summary>
        public ArchiveSoundValidationResult ValidateBifSound(SoundFileInfo soundInfo)
        {
            if (soundInfo.BifInfo == null)
            {
                return new ArchiveSoundValidationResult
                {
                    IsValid = true,
                    Source = soundInfo.Source,
                    IsFromBif = true
                };
            }

            try
            {
                if (!_cache.TryGetBifFile(soundInfo.BifInfo.BifPath, out var bifFile) || bifFile == null)
                {
                    return new ArchiveSoundValidationResult
                    {
                        IsValid = true,
                        Source = soundInfo.Source,
                        IsFromBif = true
                    };
                }

                var soundData = bifFile.ExtractVariableResource(soundInfo.BifInfo.VariableTableIndex);
                if (soundData == null)
                {
                    return new ArchiveSoundValidationResult
                    {
                        IsValid = false,
                        ExtractionFailed = true,
                        Source = soundInfo.Source,
                        IsFromBif = true
                    };
                }

                var safeResRef = SanitizeForFileName(soundInfo.BifInfo.ResRef);
                var tempPath = Path.Combine(Path.GetTempPath(), $"pv_bif_{safeResRef}.wav");
                File.WriteAllBytes(tempPath, soundData);

                try
                {
                    var validation = SoundValidator.Validate(tempPath, isVoiceOrSfx: true, skipFilenameCheck: true);
                    soundInfo.IsMono = validation.IsMono;
                    soundInfo.ChannelUnknown = false; // Now verified
                    soundInfo.IsValidWav = validation.IsValidWav;
                    soundInfo.InvalidWavReason = validation.InvalidWavReason;

                    return new ArchiveSoundValidationResult
                    {
                        IsValid = validation.IsValid,
                        IsValidWav = validation.IsValidWav,
                        InvalidWavReason = validation.InvalidWavReason,
                        HasIssues = validation.HasIssues,
                        Errors = validation.Errors.ToArray(),
                        Warnings = validation.Warnings.ToArray(),
                        FormatInfo = validation.FormatInfo,
                        Source = soundInfo.Source,
                        IsFromBif = true
                    };
                }
                finally
                {
                    try { File.Delete(tempPath); }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogApplication(LogLevel.TRACE, $"Could not delete temp file {UnifiedLogger.SanitizePath(tempPath)}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Could not validate BIF sound: {ex.Message}");
                return new ArchiveSoundValidationResult
                {
                    IsValid = true,
                    ValidationUnavailable = true,
                    Source = soundInfo.Source,
                    IsFromBif = true
                };
            }
        }

        /// <summary>
        /// Extracts a HAK sound to a temp file for playback.
        /// </summary>
        public string? ExtractHakSoundToTemp(SoundFileInfo soundInfo)
        {
            if (soundInfo.HakPath == null || soundInfo.ErfEntry == null)
                return null;

            try
            {
                var tempDir = Path.GetTempPath();
                var safeResRef = SanitizeForFileName(soundInfo.ErfEntry.ResRef);
                var tempFileName = $"ps_{safeResRef}.wav";
                var tempPath = Path.Combine(tempDir, tempFileName);

                if (_tempExtractedPath == tempPath && File.Exists(tempPath))
                    return tempPath;

                CleanupTempFile();

                var soundData = ErfReader.ExtractResource(soundInfo.HakPath, soundInfo.ErfEntry);
                File.WriteAllBytes(tempPath, soundData);

                _tempExtractedPath = tempPath;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Extracted HAK sound to temp: {tempFileName}");

                return tempPath;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to extract HAK sound: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts a BIF sound to a temp file for playback.
        /// </summary>
        public string? ExtractBifSoundToTemp(SoundFileInfo soundInfo)
        {
            if (soundInfo.BifInfo == null)
                return null;

            try
            {
                var tempDir = Path.GetTempPath();
                var safeResRef = SanitizeForFileName(soundInfo.BifInfo.ResRef);
                var tempFileName = $"ps_bif_{safeResRef}.wav";
                var tempPath = Path.Combine(tempDir, tempFileName);

                if (_tempExtractedPath == tempPath && File.Exists(tempPath))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"BIF sound reusing existing temp file: {tempFileName}");
                    return tempPath;
                }

                CleanupTempFile();

                if (!_cache.TryGetBifFile(soundInfo.BifInfo.BifPath, out var bifFile))
                {
                    bifFile = _cache.GetOrLoadBifFile(soundInfo.BifInfo.BifPath);
                    if (bifFile == null)
                    {
                        UnifiedLogger.LogApplication(LogLevel.ERROR, $"BIF file not loaded: {soundInfo.BifInfo.BifPath}");
                        return null;
                    }
                }

                var soundData = bifFile!.ExtractVariableResource(soundInfo.BifInfo.VariableTableIndex);
                if (soundData == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR,
                        $"Failed to extract BIF sound: resource index {soundInfo.BifInfo.VariableTableIndex} from {Path.GetFileName(soundInfo.BifInfo.BifPath)}");
                    return null;
                }

                File.WriteAllBytes(tempPath, soundData);
                _tempExtractedPath = tempPath;

                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Extracted BIF sound to temp: {tempFileName} ({soundData.Length} bytes)");

                return tempPath;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to extract BIF sound: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cleans up any temporary extracted files.
        /// </summary>
        public void CleanupTempFile()
        {
            if (!string.IsNullOrEmpty(_tempExtractedPath) && File.Exists(_tempExtractedPath))
            {
                try
                {
                    File.Delete(_tempExtractedPath);
                    _tempExtractedPath = null;
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        private static string SanitizeForFileName(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
                return "unknown";

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(resRef.Where(c => !invalidChars.Contains(c) && c != '\0').ToArray());

            if (sanitized.Length > 16)
                sanitized = sanitized.Substring(0, 16);

            return string.IsNullOrEmpty(sanitized) ? "unknown" : sanitized;
        }
    }

    /// <summary>
    /// Result of validating a sound from HAK or BIF with source metadata.
    /// </summary>
    public class ArchiveSoundValidationResult
    {
        public bool IsValid { get; set; }
        public bool IsValidWav { get; set; } = true;
        public string InvalidWavReason { get; set; } = "";
        public bool HasIssues { get; set; }
        public string[] Errors { get; set; } = Array.Empty<string>();
        public string[] Warnings { get; set; } = Array.Empty<string>();
        public string FormatInfo { get; set; } = "";
        public string Source { get; set; } = "";
        public bool IsFromHak { get; set; }
        public bool IsFromBif { get; set; }
        public bool ValidationUnavailable { get; set; }
        public bool ExtractionFailed { get; set; }
    }
}

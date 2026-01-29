using System;
using System.IO;
using DialogEditor.Services;
using Xunit;

namespace DialogEditor.Tests
{
    /// <summary>
    /// Tests for SoundValidator - validates sound files against NWN specifications.
    /// </summary>
    public class SoundValidatorTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly List<string> _createdFiles = new();

        public SoundValidatorTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), "SoundValidatorTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempPath);
        }

        public void Dispose()
        {
            // Clean up temp files
            foreach (var file in _createdFiles)
            {
                try { File.Delete(file); } catch { }
            }
            try { Directory.Delete(_tempPath, true); } catch { }
        }

        #region Mono/Stereo Detection Tests

        [Fact]
        public void ValidateMono_MonoFile_ReturnsTrue()
        {
            // Arrange - create a mono WAV file
            var monoFile = Path.Combine(_tempPath, "mono_test.wav");
            CreateMinimalMonoWav(monoFile);
            _createdFiles.Add(monoFile);

            // Act
            var result = SoundValidator.Validate(monoFile, isVoiceOrSfx: true);

            // Assert
            Assert.True(result.IsMono, "File should be detected as mono");
            Assert.Equal(1, result.Channels);
            Assert.True(result.IsValidWav, "File should be a valid WAV");
        }

        [Fact]
        public void ValidateMono_StereoFile_ReturnsFalse()
        {
            // Arrange - create a stereo WAV file
            var stereoFile = CreateStereoWavFile();

            // Act
            var result = SoundValidator.Validate(stereoFile, isVoiceOrSfx: true);

            // Assert
            Assert.False(result.IsMono, "File should be detected as stereo");
            Assert.Equal(2, result.Channels);
            Assert.False(result.IsValid, "Stereo files should fail validation for voice/SFX");
            Assert.Contains(result.Errors, e => e.Contains("Stereo"));
        }

        [Fact]
        public void IsMonoWav_MonoFile_ReturnsTrue()
        {
            // Arrange - create a mono WAV file
            var monoFile = Path.Combine(_tempPath, "mono_check.wav");
            CreateMinimalMonoWav(monoFile);
            _createdFiles.Add(monoFile);

            // Act
            var isMono = SoundValidator.IsMonoWav(monoFile);

            // Assert
            Assert.True(isMono);
        }

        [Fact]
        public void GetWavChannelCount_StereoFile_Returns2()
        {
            // Arrange
            var stereoFile = CreateStereoWavFile();

            // Act
            var channels = SoundValidator.GetWavChannelCount(stereoFile);

            // Assert
            Assert.Equal(2, channels);
        }

        #endregion

        #region Format Validation Tests

        [Fact]
        public void ValidateFormat_WavFile_ReturnsTrue()
        {
            // Arrange - create a valid WAV file
            var wavFile = Path.Combine(_tempPath, "valid_format.wav");
            CreateMinimalMonoWav(wavFile);
            _createdFiles.Add(wavFile);

            // Act
            var result = SoundValidator.Validate(wavFile);

            // Assert
            Assert.True(result.IsValidWav, "Should recognize as valid WAV");
            Assert.True(string.IsNullOrEmpty(result.InvalidWavReason));
        }

        [Fact]
        public void ValidateFormat_Mp3File_DetectsNonWav()
        {
            // Arrange - create a file with MP3 frame sync header
            var mp3File = CreateMp3HeaderFile();

            // Act
            var result = SoundValidator.Validate(mp3File);

            // Assert
            Assert.False(result.IsValidWav, "Should not be detected as valid WAV");
            Assert.Contains("MP3", result.InvalidWavReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateFormat_BmuFile_DetectsNonWav()
        {
            // Arrange - create a file with BMU V1.0 header
            var bmuFile = CreateBmuHeaderFile();

            // Act
            var result = SoundValidator.Validate(bmuFile);

            // Assert
            Assert.False(result.IsValidWav, "Should not be detected as valid WAV");
            Assert.Contains("BMU", result.InvalidWavReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateFormat_TooSmallFile_ReturnsInvalid()
        {
            // Arrange - create a file that's too small to be a WAV
            var tinyFile = Path.Combine(_tempPath, "tiny.wav");
            File.WriteAllBytes(tinyFile, new byte[10]); // Less than 44 bytes (min WAV header)
            _createdFiles.Add(tinyFile);

            // Act
            var result = SoundValidator.Validate(tinyFile);

            // Assert
            Assert.False(result.IsValidWav, "File too small should be invalid");
            Assert.Contains("too small", result.InvalidWavReason, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Filename Length Validation Tests

        [Fact]
        public void Validate_FilenameTooLong_ReturnsError()
        {
            // Arrange - create a file with name > 16 chars (NWN limit)
            var longName = "this_name_is_way_too_long_for_nwn.wav"; // 33 chars without extension
            var longFile = Path.Combine(_tempPath, longName);
            CreateMinimalMonoWav(longFile);
            _createdFiles.Add(longFile);

            // Act
            var result = SoundValidator.Validate(longFile, skipFilenameCheck: false);

            // Assert
            Assert.False(result.IsValid, "File with long name should fail validation");
            Assert.Contains(result.Errors, e => e.Contains("Filename too long"));
        }

        [Fact]
        public void Validate_FilenameExactlyAtLimit_Passes()
        {
            // Arrange - create a file with exactly 16 chars
            var exactName = "exactly16chars00.wav"; // 16 chars without extension
            var exactFile = Path.Combine(_tempPath, exactName);
            CreateMinimalMonoWav(exactFile);
            _createdFiles.Add(exactFile);

            // Act
            var result = SoundValidator.Validate(exactFile, skipFilenameCheck: false);

            // Assert
            Assert.DoesNotContain(result.Errors, e => e.Contains("Filename too long"));
        }

        [Fact]
        public void Validate_SkipFilenameCheck_IgnoresLongFilename()
        {
            // Arrange
            var longName = "this_is_extracted_from_hak_temp_file.wav";
            var longFile = Path.Combine(_tempPath, longName);
            CreateMinimalMonoWav(longFile);
            _createdFiles.Add(longFile);

            // Act
            var result = SoundValidator.Validate(longFile, skipFilenameCheck: true);

            // Assert
            Assert.DoesNotContain(result.Errors, e => e.Contains("Filename too long"));
        }

        #endregion

        #region Sample Rate Validation Tests

        [Fact]
        public void Validate_NonStandardSampleRate_ReturnsWarning()
        {
            // Arrange - create a WAV file with non-standard sample rate (22050 Hz)
            var wavFile = Path.Combine(_tempPath, "non_standard_rate.wav");
            CreateWavWithSampleRate(wavFile, 22050);
            _createdFiles.Add(wavFile);

            // Act
            var result = SoundValidator.Validate(wavFile);

            // Assert
            // 22050 Hz should trigger a warning about recommended rates (44100 or 41000)
            Assert.Contains(result.Warnings, w => w.Contains("Sample rate") || w.Contains("Hz"));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a minimal mono WAV file (RIFF/WAVE format).
        /// </summary>
        private void CreateMinimalMonoWav(string path)
        {
            CreateWavWithSampleRate(path, 44100);
        }

        /// <summary>
        /// Creates a mono WAV file with specified sample rate.
        /// </summary>
        private void CreateWavWithSampleRate(string path, int sampleRate)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            // RIFF header
            writer.Write("RIFF"u8);
            writer.Write(36 + 100); // File size - 8 (100 bytes of data)
            writer.Write("WAVE"u8);

            // fmt chunk
            writer.Write("fmt "u8);
            writer.Write(16); // Chunk size
            writer.Write((ushort)1); // Audio format (PCM)
            writer.Write((ushort)1); // Channels (mono)
            writer.Write(sampleRate); // Sample rate
            writer.Write(sampleRate * 2); // Byte rate
            writer.Write((ushort)2); // Block align
            writer.Write((ushort)16); // Bits per sample

            // data chunk
            writer.Write("data"u8);
            writer.Write(100); // Data size
            writer.Write(new byte[100]); // Silence
        }

        /// <summary>
        /// Creates a stereo WAV file for testing stereo detection.
        /// </summary>
        private string CreateStereoWavFile()
        {
            var path = Path.Combine(_tempPath, "stereo_test.wav");
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            // RIFF header
            writer.Write("RIFF"u8);
            writer.Write(36 + 100); // File size - 8
            writer.Write("WAVE"u8);

            // fmt chunk
            writer.Write("fmt "u8);
            writer.Write(16); // Chunk size
            writer.Write((ushort)1); // Audio format (PCM)
            writer.Write((ushort)2); // Channels (STEREO)
            writer.Write(44100); // Sample rate
            writer.Write(44100 * 4); // Byte rate (stereo = 4 bytes per sample)
            writer.Write((ushort)4); // Block align
            writer.Write((ushort)16); // Bits per sample

            // data chunk
            writer.Write("data"u8);
            writer.Write(100); // Data size
            writer.Write(new byte[100]); // Silence

            _createdFiles.Add(path);
            return path;
        }

        /// <summary>
        /// Creates a file with MP3 frame sync header for testing format detection.
        /// </summary>
        private string CreateMp3HeaderFile()
        {
            var path = Path.Combine(_tempPath, "fake_mp3.wav");

            // MP3 frame sync: 0xFF 0xFB (MPEG1 Layer III)
            var mp3Header = new byte[] {
                0xFF, 0xFB, // Frame sync + MPEG version/layer
                0x90, 0x00, // Bitrate, sample rate, padding
                // Padding to make file large enough
            };
            var fullData = new byte[100];
            Array.Copy(mp3Header, fullData, mp3Header.Length);

            File.WriteAllBytes(path, fullData);
            _createdFiles.Add(path);
            return path;
        }

        /// <summary>
        /// Creates a file with BMU V1.0 header for testing format detection.
        /// </summary>
        private string CreateBmuHeaderFile()
        {
            var path = Path.Combine(_tempPath, "fake_bmu.wav");

            // BMU V1.0 header
            var bmuHeader = "BMU V1.0"u8.ToArray();
            var fullData = new byte[100];
            Array.Copy(bmuHeader, fullData, bmuHeader.Length);

            File.WriteAllBytes(path, fullData);
            _createdFiles.Add(path);
            return path;
        }

        #endregion
    }
}

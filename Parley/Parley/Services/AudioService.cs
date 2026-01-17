using System;
using System.Linq;
using Radoub.Formats.Logging;
using System.IO;
using System.Runtime.InteropServices;
using DialogEditor.Utils;

namespace DialogEditor.Services
{
    /// <summary>
    /// Cross-platform audio playback service for sound preview.
    /// Uses platform-specific implementations for Windows/macOS/Linux.
    /// </summary>
    public class AudioService : IDisposable
    {
        private readonly IAudioPlayer _player;
        private bool _isPlaying;
        private string? _currentFile;

        public bool IsPlaying => _isPlaying;
        public string? CurrentFile => _currentFile;

        /// <summary>
        /// Event raised when playback stops (either naturally or via Stop()).
        /// </summary>
        public event EventHandler? PlaybackStopped;

        public AudioService()
        {
            // Select platform-specific audio player
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _player = new WindowsAudioPlayer();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _player = new LinuxAudioPlayer();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _player = new MacOSAudioPlayer();
            }
            else
            {
                throw new PlatformNotSupportedException("Audio playback not supported on this platform");
            }

            _player.PlaybackStopped += OnPlaybackStopped;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"AudioService initialized with {_player.GetType().Name}");
        }

        private void OnPlaybackStopped(object? sender, EventArgs e)
        {
            _isPlaying = false;
            _currentFile = null;
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Play a sound file.
        /// </summary>
        public void Play(string filePath)
        {
            if (!File.Exists(filePath))
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Audio file not found: {filePath}");
                throw new FileNotFoundException($"Audio file not found: {filePath}");
            }

            try
            {
                Stop(); // Stop current playback if any

                _player.Play(filePath);
                _isPlaying = true;
                _currentFile = filePath;

                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Playing audio: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error playing audio: {ex.Message}");
                _isPlaying = false;
                _currentFile = null;
                throw;
            }
        }

        /// <summary>
        /// Stop current playback.
        /// </summary>
        public void Stop()
        {
            if (_isPlaying)
            {
                try
                {
                    _player.Stop();
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Stopped audio: {Path.GetFileName(_currentFile ?? "(unknown)")}");
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error stopping audio: {ex.Message}");
                }
                finally
                {
                    _isPlaying = false;
                    _currentFile = null;
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _player?.Dispose();
        }
    }

    /// <summary>
    /// Platform-agnostic audio player interface.
    /// </summary>
    internal interface IAudioPlayer : IDisposable
    {
        void Play(string filePath);
        void Stop();
        event EventHandler? PlaybackStopped;
    }

    /// <summary>
    /// Windows audio player using NAudio.
    /// Supports PCM, ADPCM (including IMA ADPCM), MP3, and other common formats.
    /// Uses WaveFileReader for WAV files (better codec support) and AudioFileReader for others.
    /// </summary>
    internal class WindowsAudioPlayer : IAudioPlayer
    {
        private NAudio.Wave.WaveOutEvent? _outputDevice;
        private NAudio.Wave.WaveStream? _waveStream;

        /// <summary>
        /// Event raised when playback stops (either naturally or via Stop()).
        /// </summary>
        public event EventHandler? PlaybackStopped;

        public void Play(string filePath)
        {
            Stop();

            try
            {
                // Log first few bytes for debugging format issues
                LogFileHeader(filePath);

                // Detect format and use appropriate reader
                var firstBytes = ReadFirstBytes(filePath, 12);

                if (IsRiffWav(firstBytes))
                {
                    // Use WaveFileReader for WAV files - better ADPCM codec support
                    _waveStream = new NAudio.Wave.WaveFileReader(filePath);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Using WaveFileReader for {Path.GetFileName(filePath)} - Format: {_waveStream.WaveFormat}");
                }
                else if (IsBmu(firstBytes))
                {
                    // BMU is MP3 with 8-byte header - skip the header
                    _waveStream = CreateBmuReader(filePath);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Using BMU reader for {Path.GetFileName(filePath)}");
                }
                else if (IsMp3(firstBytes))
                {
                    // MP3 file (ID3 or raw frame sync)
                    _waveStream = new NAudio.Wave.Mp3FileReader(filePath);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Using Mp3FileReader for {Path.GetFileName(filePath)}");
                }
                else
                {
                    // Try AudioFileReader as fallback
                    _waveStream = new NAudio.Wave.AudioFileReader(filePath);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Using AudioFileReader fallback for {Path.GetFileName(filePath)}");
                }

                _outputDevice = new NAudio.Wave.WaveOutEvent();
                _outputDevice.PlaybackStopped += OnPlaybackStopped;
                _outputDevice.Init(_waveStream);
                _outputDevice.Play();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to play {Path.GetFileName(filePath)}: {ex.Message}");
                Stop();
                throw;
            }
        }

        private static void LogFileHeader(string filePath)
        {
            try
            {
                var bytes = ReadFirstBytes(filePath, 16);
                if (bytes.Length >= 16)
                {
                    var hex = BitConverter.ToString(bytes).Replace("-", " ");
                    var ascii = new string(bytes.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"File header [{Path.GetFileName(filePath)}]: {hex} | {ascii}");
                }
            }
            catch { /* Ignore logging errors */ }
        }

        private static byte[] ReadFirstBytes(string filePath, int count)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[Math.Min(count, fs.Length)];
            fs.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        private static bool IsRiffWav(byte[] bytes)
        {
            // RIFF....WAVE
            return bytes.Length >= 12 &&
                   bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F' &&
                   bytes[8] == 'W' && bytes[9] == 'A' && bytes[10] == 'V' && bytes[11] == 'E';
        }

        private static bool IsBmu(byte[] bytes)
        {
            // "BMU V1.0"
            return bytes.Length >= 8 &&
                   bytes[0] == 'B' && bytes[1] == 'M' && bytes[2] == 'U' && bytes[3] == ' ' &&
                   bytes[4] == 'V' && bytes[5] == '1' && bytes[6] == '.' && bytes[7] == '0';
        }

        private static bool IsMp3(byte[] bytes)
        {
            // ID3 tag or MP3 frame sync
            if (bytes.Length >= 3 && bytes[0] == 'I' && bytes[1] == 'D' && bytes[2] == '3')
                return true;
            if (bytes.Length >= 2 && bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0)
                return true;
            return false;
        }

        private static NAudio.Wave.WaveStream CreateBmuReader(string filePath)
        {
            // BMU files are MP3 with an 8-byte "BMU V1.0" header
            // Skip the header and treat as MP3
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            stream.Seek(8, SeekOrigin.Begin); // Skip "BMU V1.0" header
            return new NAudio.Wave.Mp3FileReader(stream);
        }

        private void OnPlaybackStopped(object? sender, NAudio.Wave.StoppedEventArgs e)
        {
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            if (_outputDevice != null)
            {
                _outputDevice.PlaybackStopped -= OnPlaybackStopped;
                _outputDevice.Stop();
                _outputDevice.Dispose();
                _outputDevice = null;
            }

            _waveStream?.Dispose();
            _waveStream = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// Linux audio player using aplay command.
    /// Falls back to paplay (PulseAudio) or ffplay if aplay is unavailable.
    /// </summary>
    internal class LinuxAudioPlayer : IAudioPlayer
    {
        private System.Diagnostics.Process? _process;
        // Note: Clear this cache (set to null) if you change the player preference order
        private static string? _cachedPlayer = null;

        public event EventHandler? PlaybackStopped;

        public void Play(string filePath)
        {
            Stop();

            // Find available player (cache the result)
            var player = GetAvailablePlayer();
            if (player == null)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    "No audio player found. Install aplay (alsa-utils), paplay (pulseaudio-utils), or ffplay (ffmpeg).");
                throw new InvalidOperationException("No audio player available");
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = player,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            // Configure arguments based on player
            if (player == "paplay")
            {
                // PulseAudio player
                startInfo.ArgumentList.Add(filePath);
            }
            else if (player == "ffplay")
            {
                // FFmpeg player - quiet mode, no video
                startInfo.ArgumentList.Add("-nodisp");
                startInfo.ArgumentList.Add("-autoexit");
                startInfo.ArgumentList.Add("-loglevel");
                startInfo.ArgumentList.Add("error");
                startInfo.ArgumentList.Add(filePath);
            }
            else
            {
                // aplay (default)
                startInfo.ArgumentList.Add(filePath);
            }

            _process = new System.Diagnostics.Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            _process.Exited += OnProcessExited;
            _process.ErrorDataReceived += OnErrorDataReceived;

            try
            {
                _process.Start();
                _process.BeginErrorReadLine();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"LinuxAudioPlayer: Started {player} for {Path.GetFileName(filePath)} (PID: {_process.Id})");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"LinuxAudioPlayer: Failed to start {player}: {ex.Message}");
                throw;
            }
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            var exitCode = _process?.ExitCode ?? -1;
            var processId = _process?.Id ?? 0;
            if (exitCode != 0)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"LinuxAudioPlayer: Process (PID: {processId}) exited with code {exitCode}");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"LinuxAudioPlayer: Process (PID: {processId}) finished successfully");
            }
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        private void OnErrorDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"LinuxAudioPlayer stderr: {e.Data}");
            }
        }

        private static string? GetAvailablePlayer()
        {
            if (_cachedPlayer != null)
                return _cachedPlayer;

            // Try players in order of preference
            // ffplay first because it handles more formats (including IMA ADPCM used in NWN)
            // paplay second (PulseAudio, good format support)
            // aplay last (ALSA, PCM only typically)
            string[] players = { "ffplay", "paplay", "aplay" };

            foreach (var player in players)
            {
                if (IsCommandAvailable(player))
                {
                    _cachedPlayer = player;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"LinuxAudioPlayer: Using {player}");
                    return player;
                }
            }

            return null;
        }

        private static bool IsCommandAvailable(string command)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                process?.WaitForExit(1000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public void Stop()
        {
            if (_process != null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process already exited
                }
                finally
                {
                    _process.Dispose();
                    _process = null;
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// macOS audio player using afplay command.
    /// </summary>
    internal class MacOSAudioPlayer : IAudioPlayer
    {
        private System.Diagnostics.Process? _process;

        public event EventHandler? PlaybackStopped;

        public void Play(string filePath)
        {
            Stop();

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "afplay",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(filePath); // Safe argument passing

            _process = new System.Diagnostics.Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            _process.Exited += (s, e) => PlaybackStopped?.Invoke(this, EventArgs.Empty);
            _process.Start();
        }

        public void Stop()
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.Dispose();
                _process = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

using System;
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

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"AudioService initialized with {_player.GetType().Name}");
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
    }

    /// <summary>
    /// Windows audio player using NAudio.
    /// Supports PCM, ADPCM, MP3, and other common formats.
    /// </summary>
    internal class WindowsAudioPlayer : IAudioPlayer
    {
        private NAudio.Wave.WaveOutEvent? _outputDevice;
        private NAudio.Wave.AudioFileReader? _audioFile;

        public void Play(string filePath)
        {
            Stop();

            try
            {
                _audioFile = new NAudio.Wave.AudioFileReader(filePath);
                _outputDevice = new NAudio.Wave.WaveOutEvent();
                _outputDevice.Init(_audioFile);
                _outputDevice.Play();

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Playing {Path.GetFileName(filePath)} - Format: {_audioFile.WaveFormat}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to play {Path.GetFileName(filePath)}: {ex.Message}");
                Stop();
                throw;
            }
        }

        public void Stop()
        {
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            _outputDevice = null;

            _audioFile?.Dispose();
            _audioFile = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// Linux audio player using aplay command.
    /// </summary>
    internal class LinuxAudioPlayer : IAudioPlayer
    {
        private System.Diagnostics.Process? _process;

        public void Play(string filePath)
        {
            Stop();

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "aplay",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(filePath); // Safe argument passing

            _process = new System.Diagnostics.Process
            {
                StartInfo = startInfo
            };

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

    /// <summary>
    /// macOS audio player using afplay command.
    /// </summary>
    internal class MacOSAudioPlayer : IAudioPlayer
    {
        private System.Diagnostics.Process? _process;

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
                StartInfo = startInfo
            };

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

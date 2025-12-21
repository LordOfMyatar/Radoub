using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace DialogEditor.Services
{
    /// <summary>
    /// Linux TTS implementation using espeak-ng.
    /// Issue #479 - TTS Integration Sprint
    /// </summary>
    public class EspeakTtsService : ITtsService
    {
        private readonly List<string> _voiceNames = new();
        private readonly bool _isAvailable;
        private readonly string _unavailableReason = "";
        private Process? _currentProcess;
        private bool _isSpeaking;
        private readonly string _espeakPath;

        public event EventHandler? SpeakCompleted;

        public EspeakTtsService()
        {
            // Try to find espeak-ng
            _espeakPath = FindEspeakPath();

            if (string.IsNullOrEmpty(_espeakPath))
            {
                _isAvailable = false;
                _unavailableReason = "espeak-ng is not installed or not in PATH.";
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    "EspeakTtsService: espeak-ng not found");
                return;
            }

            try
            {
                // Get available voices
                var voiceOutput = RunEspeakCommand("--voices");
                if (!string.IsNullOrEmpty(voiceOutput))
                {
                    ParseVoices(voiceOutput);
                }

                _isAvailable = _voiceNames.Count > 0;
                if (!_isAvailable)
                {
                    _unavailableReason = "No voices found in espeak-ng.";
                }

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"EspeakTtsService: Initialized with {_voiceNames.Count} voices");
            }
            catch (Exception ex)
            {
                _isAvailable = false;
                _unavailableReason = $"Failed to initialize espeak-ng: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"EspeakTtsService: Failed to initialize - {ex.Message}");
            }
        }

        private string FindEspeakPath()
        {
            // Common locations for espeak-ng
            var paths = new[]
            {
                "espeak-ng",  // In PATH
                "/usr/bin/espeak-ng",
                "/usr/local/bin/espeak-ng",
                "/opt/homebrew/bin/espeak-ng"  // macOS Homebrew ARM64
            };

            foreach (var path in paths)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    process?.WaitForExit(1000);

                    if (process?.ExitCode == 0)
                    {
                        return path;
                    }
                }
                catch
                {
                    // Continue to next path
                }
            }

            return "";
        }

        private string RunEspeakCommand(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _espeakPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return "";

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                return output;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"EspeakTtsService: Command failed - {ex.Message}");
                return "";
            }
        }

        private void ParseVoices(string output)
        {
            // espeak-ng --voices output format:
            // Pty Language Age/Gender VoiceName       File          Other Languages
            //  5  af              M  afrikaans       ...
            // We want the VoiceName column

            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("Pty") || line.StartsWith(" Pty")) continue; // Header

                // Parse the line - VoiceName is typically the 4th column
                var parts = Regex.Split(line.Trim(), @"\s{2,}");
                if (parts.Length >= 4)
                {
                    var voiceName = parts[3].Trim();
                    if (!string.IsNullOrEmpty(voiceName) && !_voiceNames.Contains(voiceName))
                    {
                        _voiceNames.Add(voiceName);
                    }
                }
            }

            // Sort voices for better UX
            _voiceNames.Sort();
        }

        public bool IsAvailable => _isAvailable;

        public IReadOnlyList<string> GetVoiceNames() => _voiceNames.AsReadOnly();

        public void Speak(string text, string? voiceName = null, double rate = 1.0)
        {
            if (!_isAvailable) return;

            try
            {
                // Stop any current speech
                Stop();

                // Build arguments
                // -v voice: select voice
                // -s speed: words per minute (default 175, range roughly 80-450)
                // Map rate 0.5-2.0 to speed 90-350
                int speed = (int)(175 * rate);
                speed = Math.Clamp(speed, 80, 450);

                var args = $"-s {speed}";
                if (!string.IsNullOrEmpty(voiceName))
                {
                    args += $" -v \"{voiceName}\"";
                }

                // Escape text for shell
                var escapedText = text.Replace("\"", "\\\"");
                args += $" \"{escapedText}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = _espeakPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _currentProcess = Process.Start(psi);
                _isSpeaking = true;

                // Monitor completion on background thread
                if (_currentProcess != null)
                {
                    _currentProcess.EnableRaisingEvents = true;
                    _currentProcess.Exited += (s, e) =>
                    {
                        _isSpeaking = false;
                        _currentProcess = null;
                        SpeakCompleted?.Invoke(this, EventArgs.Empty);
                    };
                }
            }
            catch (Exception ex)
            {
                _isSpeaking = false;
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"EspeakTtsService: Speak failed - {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    _currentProcess.Kill();
                    _currentProcess = null;
                }
                _isSpeaking = false;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"EspeakTtsService: Stop failed - {ex.Message}");
            }
        }

        public bool IsSpeaking => _isSpeaking;

        public string UnavailableReason => _unavailableReason;

        public string InstallInstructions =>
            "Install espeak-ng for TTS support:\n" +
            "  Ubuntu/Debian: sudo apt install espeak-ng\n" +
            "  Fedora: sudo dnf install espeak-ng\n" +
            "  Arch: sudo pacman -S espeak-ng\n" +
            "  macOS: brew install espeak-ng";
    }
}

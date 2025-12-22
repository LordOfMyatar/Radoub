using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DialogEditor.Services
{
    /// <summary>
    /// Linux TTS implementation using espeak-ng.
    /// Issue #479 - TTS Integration Sprint
    /// Issue #491 - Voice variants with male/female options
    /// </summary>
    public class EspeakTtsService : ITtsService
    {
        private readonly List<string> _voiceNames = new();
        private readonly Dictionary<string, string> _voiceCodeMap = new(); // Display name -> espeak voice code
        private readonly bool _isAvailable;
        private readonly string _unavailableReason = "";
        private Process? _currentProcess;
        private bool _isSpeaking;
        private readonly string _espeakPath;

        // NWN-supported languages with their espeak-ng codes and display names
        private static readonly (string Code, string DisplayName)[] NwnLanguages =
        {
            ("en", "English"),
            ("de", "German"),
            ("fr", "French"),
            ("es", "Spanish"),
            ("it", "Italian"),
            ("pl", "Polish")
        };

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
                // Build voice list for NWN-supported languages with male/female variants
                BuildNwnVoiceList();

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

        /// <summary>
        /// Build voice list with male/female variants for NWN-supported languages.
        /// Uses espeak-ng voice+variant syntax: "en+m3" for male, "en+f3" for female.
        /// Issue #491
        /// </summary>
        private void BuildNwnVoiceList()
        {
            // Get available base voices from espeak-ng to validate
            var voiceOutput = RunEspeakCommand("--voices");
            var availableVoices = ParseAvailableVoices(voiceOutput);

            foreach (var (code, displayName) in NwnLanguages)
            {
                // Check if base language is available in espeak-ng
                if (!availableVoices.Contains(code))
                    continue;

                // Add male variant (using +m3 for a clearer male voice)
                var maleDisplayName = $"{displayName} (Male)";
                var maleCode = $"{code}+m3";
                _voiceNames.Add(maleDisplayName);
                _voiceCodeMap[maleDisplayName] = maleCode;

                // Add female variant (using +f3 for a clearer female voice)
                var femaleDisplayName = $"{displayName} (Female)";
                var femaleCode = $"{code}+f3";
                _voiceNames.Add(femaleDisplayName);
                _voiceCodeMap[femaleDisplayName] = femaleCode;
            }
        }

        /// <summary>
        /// Parse espeak-ng --voices output to get available base language codes.
        /// </summary>
        private HashSet<string> ParseAvailableVoices(string output)
        {
            var voices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(output))
                return voices;

            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("Pty") || line.StartsWith(" Pty")) continue; // Header

                var trimmedLine = line.TrimStart();
                var parts = Regex.Split(trimmedLine, @"\s+");
                if (parts.Length >= 2)
                {
                    var languageCode = parts[1].Trim();
                    if (languageCode == "variant") continue;
                    if (string.IsNullOrEmpty(languageCode)) continue;

                    // Add the full code (e.g., "en-gb") and base code (e.g., "en")
                    voices.Add(languageCode);
                    var baseLang = languageCode.Split('-')[0];
                    voices.Add(baseLang);
                }
            }

            return voices;
        }

        /// <summary>
        /// Translate display name to espeak-ng voice code.
        /// Returns the original name if no mapping exists (backwards compatibility).
        /// </summary>
        public string GetVoiceCode(string displayName)
        {
            return _voiceCodeMap.TryGetValue(displayName, out var code) ? code : displayName;
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
                    // Translate display name to espeak voice code (e.g., "English (Male)" -> "en+m3")
                    var espeakCode = GetVoiceCode(voiceName);
                    args += $" -v \"{espeakCode}\"";
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
                    // Don't redirect stdout/stderr - espeak-ng needs direct audio device access
                    // Redirecting can cause espeak-ng to output audio data to stdout instead of playing
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
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

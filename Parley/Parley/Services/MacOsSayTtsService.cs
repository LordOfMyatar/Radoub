using System;
using Radoub.Formats.Logging;
using System.Collections.Generic;
using System.Diagnostics;

namespace DialogEditor.Services
{
    /// <summary>
    /// macOS TTS implementation using the built-in 'say' command.
    /// Issue #479 - TTS Integration Sprint
    /// </summary>
    public class MacOsSayTtsService : ITtsService
    {
        private readonly List<string> _voiceNames = new();
        private readonly bool _isAvailable;
        private readonly string _unavailableReason = "";
        private Process? _currentProcess;
        private bool _isSpeaking;

        public event EventHandler? SpeakCompleted;

        public MacOsSayTtsService()
        {
            try
            {
                // Check if 'say' command exists (should always be present on macOS)
                var testOutput = RunCommand("say", "--voice=?");
                if (string.IsNullOrEmpty(testOutput))
                {
                    _isAvailable = false;
                    _unavailableReason = "The 'say' command is not available.";
                    return;
                }

                // Parse available voices
                ParseVoices(testOutput);

                _isAvailable = _voiceNames.Count > 0;
                if (!_isAvailable)
                {
                    _unavailableReason = "No voices found.";
                }

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"MacOsSayTtsService: Initialized with {_voiceNames.Count} voices");
            }
            catch (Exception ex)
            {
                _isAvailable = false;
                _unavailableReason = $"Failed to initialize macOS TTS: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"MacOsSayTtsService: Failed to initialize - {ex.Message}");
            }
        }

        private string RunCommand(string command, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = command,
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
            catch
            {
                return "";
            }
        }

        private void ParseVoices(string output)
        {
            // 'say --voice=?' output format:
            // Alex                en_US    # Most people recognize me by my voice.
            // Alice               it_IT    # Salve, mi chiamo Alice e sono una voce italiana.
            // We want the first column (voice name)

            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Voice name is at the start of the line, before whitespace
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1)
                {
                    var voiceName = parts[0].Trim();
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
                // -r rate: words per minute (default ~175, range roughly 1-500)
                // Map rate 0.5-2.0 to speed 90-350
                int speed = (int)(175 * rate);
                speed = Math.Clamp(speed, 1, 500);

                var args = $"-r {speed}";
                if (!string.IsNullOrEmpty(voiceName))
                {
                    args += $" -v \"{voiceName}\"";
                }

                // Escape text for shell - replace quotes
                var escapedText = text.Replace("\"", "\\\"");
                args += $" \"{escapedText}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = "say",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _currentProcess = Process.Start(psi);
                _isSpeaking = true;

                // Monitor completion
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
                    $"MacOsSayTtsService: Speak failed - {ex.Message}");
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
                    $"MacOsSayTtsService: Stop failed - {ex.Message}");
            }
        }

        public bool IsSpeaking => _isSpeaking;

        public string UnavailableReason => _unavailableReason;

        public string InstallInstructions =>
            "macOS TTS should be available by default using the 'say' command.\n" +
            "If not working, check System Settings > Accessibility > Spoken Content.";
    }
}

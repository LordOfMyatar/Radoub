using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DialogEditor.Services
{
    /// <summary>
    /// Linux/macOS TTS implementation using Piper neural TTS.
    /// Piper provides high-quality neural voice synthesis.
    /// Issue #491 - Piper TTS integration
    /// </summary>
    public class PiperTtsService : ITtsService
    {
        private readonly List<string> _voiceNames = new();
        private readonly Dictionary<string, string> _voiceModelMap = new(); // Display name -> model name
        private readonly bool _isAvailable;
        private readonly string _unavailableReason = "";
        private Process? _currentProcess;
        private bool _isSpeaking;
        private readonly string _piperPath;

        // NWN-supported languages with Piper voice models
        // Format: (model name, display name, gender)
        // Using medium quality models for balance of quality and speed
        private static readonly (string Model, string DisplayName)[] NwnVoices =
        {
            // English voices
            ("en_US-lessac-medium", "English US - Lessac (Male)"),
            ("en_US-amy-medium", "English US - Amy (Female)"),
            ("en_GB-alan-medium", "English GB - Alan (Male)"),
            ("en_GB-alba-medium", "English GB - Alba (Female)"),
            // German voices
            ("de_DE-thorsten-medium", "German - Thorsten (Male)"),
            ("de_DE-eva_k-x_low", "German - Eva (Female)"),
            // French voices
            ("fr_FR-upmc-medium", "French - UPMC (Male)"),
            ("fr_FR-siwis-medium", "French - Siwis (Female)"),
            // Spanish voices
            ("es_ES-sharvard-medium", "Spanish - Sharvard (Male)"),
            ("es_ES-carlfm-x_low", "Spanish - Carlfm (Male)"),
            // Italian voices
            ("it_IT-riccardo-x_low", "Italian - Riccardo (Male)"),
            // Polish voices
            ("pl_PL-gosia-medium", "Polish - Gosia (Female)"),
            ("pl_PL-darkman-medium", "Polish - Darkman (Male)")
        };

        public event EventHandler? SpeakCompleted;

        public PiperTtsService()
        {
            // Try to find piper
            _piperPath = FindPiperPath();

            if (string.IsNullOrEmpty(_piperPath))
            {
                _isAvailable = false;
                _unavailableReason = "Piper TTS is not installed.";
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    "PiperTtsService: piper not found");
                return;
            }

            try
            {
                // Build voice list
                BuildVoiceList();

                _isAvailable = _voiceNames.Count > 0;
                if (!_isAvailable)
                {
                    _unavailableReason = "No Piper voices configured.";
                }

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"PiperTtsService: Initialized with {_voiceNames.Count} voices");
            }
            catch (Exception ex)
            {
                _isAvailable = false;
                _unavailableReason = $"Failed to initialize Piper: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"PiperTtsService: Failed to initialize - {ex.Message}");
            }
        }

        private string FindPiperPath()
        {
            // Common locations for piper
            var paths = new[]
            {
                "piper",  // In PATH (pip install piper-tts)
                "/usr/bin/piper",
                "/usr/local/bin/piper",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/bin/piper")
            };

            foreach (var path in paths)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "--help",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    process?.WaitForExit(2000);

                    // piper --help returns 0
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

        private void BuildVoiceList()
        {
            // Add all configured NWN voices
            // Piper will auto-download models on first use
            foreach (var (model, displayName) in NwnVoices)
            {
                _voiceNames.Add(displayName);
                _voiceModelMap[displayName] = model;
            }
        }

        /// <summary>
        /// Get the Piper model name for a display name.
        /// </summary>
        public string GetModelName(string displayName)
        {
            return _voiceModelMap.TryGetValue(displayName, out var model) ? model : displayName;
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

                // Get model name
                var modelName = string.IsNullOrEmpty(voiceName)
                    ? NwnVoices[0].Model  // Default to first voice
                    : GetModelName(voiceName);

                // Calculate length_scale (inverse of rate: 0.5 = faster, 2.0 = slower)
                // rate 0.5 -> length_scale 2.0, rate 2.0 -> length_scale 0.5
                var lengthScale = 1.0 / rate;
                lengthScale = Math.Clamp(lengthScale, 0.5, 2.0);

                // Create temp file for audio output
                var tempWavFile = Path.Combine(Path.GetTempPath(), $"piper_tts_{Guid.NewGuid()}.wav");

                // Build piper command
                // piper --model <model> --output_file <file> --length_scale <scale>
                var args = $"--model {modelName} --output_file \"{tempWavFile}\" --length_scale {lengthScale:F2}";

                var psi = new ProcessStartInfo
                {
                    FileName = _piperPath,
                    Arguments = args,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _currentProcess = Process.Start(psi);
                if (_currentProcess == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR,
                        "PiperTtsService: Failed to start piper process");
                    return;
                }

                _isSpeaking = true;

                // Write text to stdin
                _currentProcess.StandardInput.WriteLine(text);
                _currentProcess.StandardInput.Close();

                // Handle completion asynchronously
                Task.Run(() =>
                {
                    try
                    {
                        _currentProcess.WaitForExit();

                        if (_currentProcess.ExitCode == 0 && File.Exists(tempWavFile))
                        {
                            // Play the generated WAV file using aplay (Linux) or afplay (macOS)
                            PlayWavFile(tempWavFile);
                        }
                        else
                        {
                            var stderr = _currentProcess.StandardError.ReadToEnd();
                            UnifiedLogger.LogApplication(LogLevel.ERROR,
                                $"PiperTtsService: piper failed - {stderr}");
                            _isSpeaking = false;
                            SpeakCompleted?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogApplication(LogLevel.ERROR,
                            $"PiperTtsService: Error during synthesis - {ex.Message}");
                        _isSpeaking = false;
                        SpeakCompleted?.Invoke(this, EventArgs.Empty);
                    }
                    finally
                    {
                        // Clean up temp file
                        try { if (File.Exists(tempWavFile)) File.Delete(tempWavFile); }
                        catch { /* ignore cleanup errors */ }
                    }
                });
            }
            catch (Exception ex)
            {
                _isSpeaking = false;
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"PiperTtsService: Speak failed - {ex.Message}");
            }
        }

        private void PlayWavFile(string wavFile)
        {
            try
            {
                // Use aplay on Linux, afplay on macOS
                var playerPath = File.Exists("/usr/bin/aplay") ? "aplay" :
                                 File.Exists("/usr/bin/afplay") ? "afplay" :
                                 "aplay";  // Default to aplay

                var psi = new ProcessStartInfo
                {
                    FileName = playerPath,
                    Arguments = $"\"{wavFile}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                var playProcess = Process.Start(psi);
                if (playProcess != null)
                {
                    playProcess.EnableRaisingEvents = true;
                    playProcess.Exited += (s, e) =>
                    {
                        _isSpeaking = false;
                        SpeakCompleted?.Invoke(this, EventArgs.Empty);
                    };
                }
                else
                {
                    _isSpeaking = false;
                    SpeakCompleted?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"PiperTtsService: Failed to play audio - {ex.Message}");
                _isSpeaking = false;
                SpeakCompleted?.Invoke(this, EventArgs.Empty);
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
                    $"PiperTtsService: Stop failed - {ex.Message}");
            }
        }

        public bool IsSpeaking => _isSpeaking;

        public string UnavailableReason => _unavailableReason;

        public string InstallInstructions =>
            "Install Piper TTS for high-quality neural voices:\n" +
            "  pip install piper-tts\n\n" +
            "Voice models are downloaded automatically on first use.\n" +
            "For more voices, see: https://rhasspy.github.io/piper-samples/";
    }
}

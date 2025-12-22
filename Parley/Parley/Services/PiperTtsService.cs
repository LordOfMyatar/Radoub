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
        private readonly Dictionary<string, string> _voiceModelMap = new(); // Display name -> full path to ONNX
        private readonly bool _isAvailable;
        private readonly string _unavailableReason = "";
        private Process? _currentProcess;
        private Process? _playbackProcess;
        private string? _currentTempFile;
        private bool _isSpeaking;
        private readonly string _piperPath;
        private readonly string _voicesDir;

        // NWN-supported languages with Piper voice models
        // Format: (model base name, display name)
        // Users must download these models to ~/.local/share/piper-voices/
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
            // Set up voices directory
            _voicesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "piper-voices");

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
                // Build voice list from installed models
                BuildVoiceList();

                _isAvailable = _voiceNames.Count > 0;
                if (!_isAvailable)
                {
                    _unavailableReason = $"No Piper voice models found in {_voicesDir}";
                }

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"PiperTtsService: Initialized with {_voiceNames.Count} voices from {_voicesDir}");
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
            // Only add voices that have downloaded ONNX models
            if (!Directory.Exists(_voicesDir))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"PiperTtsService: Voices directory does not exist: {_voicesDir}");
                return;
            }

            foreach (var (model, displayName) in NwnVoices)
            {
                var onnxPath = Path.Combine(_voicesDir, $"{model}.onnx");
                var jsonPath = Path.Combine(_voicesDir, $"{model}.onnx.json");

                // Only add if both model and config exist
                if (File.Exists(onnxPath) && File.Exists(jsonPath))
                {
                    _voiceNames.Add(displayName);
                    _voiceModelMap[displayName] = onnxPath;  // Store full path
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"PiperTtsService: Found voice model: {model}");
                }
            }
        }

        /// <summary>
        /// Get the full path to the ONNX model for a display name.
        /// </summary>
        public string GetModelPath(string displayName)
        {
            return _voiceModelMap.TryGetValue(displayName, out var path) ? path : "";
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

                // Get model path (full path to ONNX file)
                string modelPath;
                if (string.IsNullOrEmpty(voiceName) || !_voiceModelMap.ContainsKey(voiceName))
                {
                    // Use first available voice
                    modelPath = _voiceModelMap.Values.FirstOrDefault() ?? "";
                }
                else
                {
                    modelPath = GetModelPath(voiceName);
                }

                if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR,
                        $"PiperTtsService: Voice model not found: {voiceName}");
                    return;
                }

                // Calculate length_scale (inverse of rate: 0.5 = faster, 2.0 = slower)
                // rate 0.5 -> length_scale 2.0, rate 2.0 -> length_scale 0.5
                var lengthScale = 1.0 / rate;
                lengthScale = Math.Clamp(lengthScale, 0.5, 2.0);

                // Create temp file for audio output and track it
                var tempWavFile = Path.Combine(Path.GetTempPath(), $"piper_tts_{Guid.NewGuid()}.wav");
                _currentTempFile = tempWavFile;

                // Build piper command
                // piper --model <path> --output_file <file> --length_scale <scale>
                var args = $"--model \"{modelPath}\" --output_file \"{tempWavFile}\" --length_scale {lengthScale:F2}";

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
                    string? stderrOutput = null;
                    try
                    {
                        // Read stderr while process is running (before WaitForExit)
                        stderrOutput = _currentProcess.StandardError.ReadToEnd();
                        _currentProcess.WaitForExit();

                        if (_currentProcess.ExitCode == 0 && File.Exists(tempWavFile))
                        {
                            // Play the generated WAV file using aplay (Linux) or afplay (macOS)
                            // Note: PlayWavFile handles cleanup of temp file after playback
                            PlayWavFile(tempWavFile);
                        }
                        else
                        {
                            UnifiedLogger.LogApplication(LogLevel.ERROR,
                                $"PiperTtsService: piper failed (exit {_currentProcess.ExitCode}) - {stderrOutput}");
                            _isSpeaking = false;
                            CleanupTempFile(tempWavFile);
                            SpeakCompleted?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogApplication(LogLevel.ERROR,
                            $"PiperTtsService: Error during synthesis - {ex.Message}");
                        _isSpeaking = false;
                        CleanupTempFile(tempWavFile);
                        SpeakCompleted?.Invoke(this, EventArgs.Empty);
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

                _playbackProcess = Process.Start(psi);
                if (_playbackProcess != null)
                {
                    _playbackProcess.EnableRaisingEvents = true;
                    _playbackProcess.Exited += (s, e) =>
                    {
                        _isSpeaking = false;
                        _playbackProcess = null;
                        if (_currentTempFile == wavFile)
                        {
                            CleanupTempFile(wavFile);  // Clean up after playback
                            _currentTempFile = null;
                        }
                        SpeakCompleted?.Invoke(this, EventArgs.Empty);
                    };
                }
                else
                {
                    _isSpeaking = false;
                    CleanupTempFile(wavFile);
                    if (_currentTempFile == wavFile) _currentTempFile = null;
                    SpeakCompleted?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"PiperTtsService: Failed to play audio - {ex.Message}");
                _isSpeaking = false;
                CleanupTempFile(wavFile);
                SpeakCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        private static void CleanupTempFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        public void Stop()
        {
            try
            {
                // Kill piper process if running
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    _currentProcess.Kill();
                    _currentProcess = null;
                }

                // Kill playback process if running
                if (_playbackProcess != null && !_playbackProcess.HasExited)
                {
                    _playbackProcess.Kill();
                    _playbackProcess = null;
                }

                // Clean up temp file from previous operation
                if (_currentTempFile != null)
                {
                    CleanupTempFile(_currentTempFile);
                    _currentTempFile = null;
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
            "Install Piper TTS for high-quality neural voices:\n\n" +
            "1. Install piper:\n" +
            "   pipx install piper-tts  (or: pip install piper-tts)\n\n" +
            "2. Download voice models to ~/.local/share/piper-voices/\n" +
            "   Example for English:\n" +
            "   mkdir -p ~/.local/share/piper-voices\n" +
            "   cd ~/.local/share/piper-voices\n" +
            "   wget https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/en/en_US/lessac/medium/en_US-lessac-medium.onnx\n" +
            "   wget https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/en/en_US/lessac/medium/en_US-lessac-medium.onnx.json\n\n" +
            "For more voices, see: https://rhasspy.github.io/piper-samples/";
    }
}

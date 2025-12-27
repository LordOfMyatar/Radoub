using System;
using Radoub.Formats.Logging;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#if WINDOWS
using System.Speech.Synthesis;
#endif

namespace DialogEditor.Services
{
    /// <summary>
    /// Windows TTS implementation using System.Speech.Synthesis.
    /// Issue #479 - TTS Integration Sprint
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class WindowsTtsService : ITtsService
    {
#if WINDOWS
        private SpeechSynthesizer? _synthesizer;
        private List<string> _voiceNames = new();
        private bool _isAvailable;
        private string _unavailableReason = "";
#endif
        private bool _isSpeaking;

        public event EventHandler? SpeakCompleted;

        public WindowsTtsService()
        {
#if WINDOWS
            try
            {
                _synthesizer = new SpeechSynthesizer();
                _synthesizer.SpeakCompleted += (s, e) =>
                {
                    _isSpeaking = false;
                    SpeakCompleted?.Invoke(this, EventArgs.Empty);
                };

                // Get available voices
                foreach (var voice in _synthesizer.GetInstalledVoices())
                {
                    if (voice.Enabled)
                    {
                        _voiceNames.Add(voice.VoiceInfo.Name);
                    }
                }

                _isAvailable = _voiceNames.Count > 0;
                if (!_isAvailable)
                {
                    _unavailableReason = "No TTS voices are installed on this system.";
                }

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"WindowsTtsService: Initialized with {_voiceNames.Count} voices");
            }
            catch (Exception ex)
            {
                _isAvailable = false;
                _unavailableReason = $"Failed to initialize Windows TTS: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"WindowsTtsService: Failed to initialize - {ex.Message}");
            }
#endif
        }

        public bool IsAvailable
        {
            get
            {
#if WINDOWS
                return _isAvailable;
#else
                return false;
#endif
            }
        }

        public IReadOnlyList<string> GetVoiceNames()
        {
#if WINDOWS
            return _voiceNames.AsReadOnly();
#else
            return Array.Empty<string>();
#endif
        }

        public void Speak(string text, string? voiceName = null, double rate = 1.0)
        {
#if WINDOWS
            if (_synthesizer == null || !_isAvailable) return;

            try
            {
                // Stop any current speech
                Stop();

                // Set voice if specified
                if (!string.IsNullOrEmpty(voiceName) && _voiceNames.Contains(voiceName))
                {
                    _synthesizer.SelectVoice(voiceName);
                }

                // Set rate (-10 to 10, where 0 is normal)
                // Map our 0.5-2.0 range to -5 to 5
                int rateValue = (int)((rate - 1.0) * 5);
                rateValue = Math.Clamp(rateValue, -10, 10);
                _synthesizer.Rate = rateValue;

                _isSpeaking = true;
                _synthesizer.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                _isSpeaking = false;
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"WindowsTtsService: Speak failed - {ex.Message}");
            }
#endif
        }

        public void Stop()
        {
#if WINDOWS
            if (_synthesizer == null) return;

            try
            {
                _synthesizer.SpeakAsyncCancelAll();
                _isSpeaking = false;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"WindowsTtsService: Stop failed - {ex.Message}");
            }
#endif
        }

        public bool IsSpeaking => _isSpeaking;

        public string UnavailableReason
        {
            get
            {
#if WINDOWS
                return _unavailableReason;
#else
                return "Windows TTS is only available on Windows.";
#endif
            }
        }

        public string InstallInstructions => "Windows TTS should be available by default. " +
            "If no voices are found, check Settings > Time & Language > Speech.";
    }
}

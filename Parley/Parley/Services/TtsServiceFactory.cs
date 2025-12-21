using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DialogEditor.Services
{
    /// <summary>
    /// Factory for creating platform-appropriate TTS service.
    /// Issue #479 - TTS Integration Sprint
    /// </summary>
    public static class TtsServiceFactory
    {
        private static ITtsService? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// Gets the singleton TTS service instance for the current platform.
        /// </summary>
        public static ITtsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= Create();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Creates a new TTS service for the current platform.
        /// </summary>
        public static ITtsService Create()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    "TtsServiceFactory: Creating WindowsTtsService");
                return new WindowsTtsService();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // On macOS, try the native 'say' command first
                var sayService = new MacOsSayTtsService();
                if (sayService.IsAvailable)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        "TtsServiceFactory: Creating MacOsSayTtsService");
                    return sayService;
                }

                // Fall back to espeak-ng if installed
                var espeakService = new EspeakTtsService();
                if (espeakService.IsAvailable)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        "TtsServiceFactory: Creating EspeakTtsService (macOS fallback)");
                    return espeakService;
                }

                // Return the 'say' service even if unavailable (for error message)
                return sayService;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    "TtsServiceFactory: Creating EspeakTtsService");
                return new EspeakTtsService();
            }
            else
            {
                // Unknown platform - return a null implementation
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"TtsServiceFactory: Unknown platform {RuntimeInformation.OSDescription}");
                return new NullTtsService();
            }
        }

        /// <summary>
        /// Null implementation for unsupported platforms.
        /// </summary>
        private class NullTtsService : ITtsService
        {
            public bool IsAvailable => false;
            public bool IsSpeaking => false;
            public string UnavailableReason => "TTS is not supported on this platform.";
            public string InstallInstructions => "TTS is not available for this operating system.";

            public IReadOnlyList<string> GetVoiceNames() => Array.Empty<string>();
            public void Speak(string text, string? voiceName = null, double rate = 1.0) { }
            public void Stop() { }
        }
    }
}

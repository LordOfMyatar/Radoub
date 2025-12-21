using System;
using System.Collections.Generic;

namespace DialogEditor.Services
{
    /// <summary>
    /// Interface for platform-agnostic text-to-speech services.
    /// Issue #479 - TTS Integration Sprint
    /// </summary>
    public interface ITtsService
    {
        /// <summary>
        /// Whether TTS is available on this platform.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Gets the list of available voice names.
        /// </summary>
        IReadOnlyList<string> GetVoiceNames();

        /// <summary>
        /// Speaks the given text using the specified voice.
        /// </summary>
        /// <param name="text">Text to speak.</param>
        /// <param name="voiceName">Voice name to use, or null for default.</param>
        /// <param name="rate">Speech rate multiplier (0.5 = half speed, 2.0 = double speed).</param>
        void Speak(string text, string? voiceName = null, double rate = 1.0);

        /// <summary>
        /// Stops any currently playing speech.
        /// </summary>
        void Stop();

        /// <summary>
        /// Whether speech is currently playing.
        /// </summary>
        bool IsSpeaking { get; }

        /// <summary>
        /// Gets a user-friendly message about why TTS is not available.
        /// Only valid when IsAvailable is false.
        /// </summary>
        string UnavailableReason { get; }

        /// <summary>
        /// Gets installation instructions for making TTS available.
        /// Only valid when IsAvailable is false.
        /// </summary>
        string InstallInstructions { get; }

        /// <summary>
        /// Event raised when speech completes (including when stopped).
        /// </summary>
        event EventHandler? SpeakCompleted;
    }
}

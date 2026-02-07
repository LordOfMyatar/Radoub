using DialogEditor.Services;

namespace Parley.Tests.Mocks
{
    /// <summary>
    /// Mock ITtsService for unit testing.
    /// Simulates TTS availability and speech operations without platform dependencies.
    /// </summary>
    public class MockTtsService : ITtsService
    {
        private readonly List<string> _voiceNames = new() { "TestVoice1", "TestVoice2" };
        private readonly List<string> _spokenTexts = new();

        public bool IsAvailable { get; set; } = true;
        public bool IsSpeaking { get; set; } = false;
        public string UnavailableReason { get; set; } = "TTS not available in test environment";
        public string InstallInstructions { get; set; } = "No installation needed for tests";

        public event EventHandler? SpeakCompleted;

        public IReadOnlyList<string> GetVoiceNames() => _voiceNames.AsReadOnly();

        public void Speak(string text, string? voiceName = null, double rate = 1.0)
        {
            _spokenTexts.Add(text);
            IsSpeaking = true;
        }

        public void Stop()
        {
            IsSpeaking = false;
            SpeakCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Complete the current speech (simulates natural completion).
        /// </summary>
        public void CompleteSpeech()
        {
            IsSpeaking = false;
            SpeakCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Get all texts that were passed to Speak().
        /// </summary>
        public IReadOnlyList<string> SpokenTexts => _spokenTexts.AsReadOnly();

        /// <summary>
        /// Add a voice name for testing.
        /// </summary>
        public void AddVoice(string voiceName) => _voiceNames.Add(voiceName);

        /// <summary>
        /// Clear spoken text history.
        /// </summary>
        public void Reset()
        {
            _spokenTexts.Clear();
            IsSpeaking = false;
        }
    }
}

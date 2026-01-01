using Radoub.Formats.Erf;

namespace DialogEditor.Models.Sound
{
    /// <summary>
    /// Sound info with path/source and mono status for filtering.
    /// </summary>
    public class SoundFileInfo
    {
        public string FileName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsMono { get; set; } = true;

        /// <summary>
        /// Source of the sound (e.g., "Override", "customsounds.hak", "Base Game").
        /// </summary>
        public string Source { get; set; } = "";

        /// <summary>
        /// If from HAK, the path to the HAK file.
        /// </summary>
        public string? HakPath { get; set; }

        /// <summary>
        /// If from HAK, the ERF resource entry for extraction.
        /// </summary>
        public ErfResourceEntry? ErfEntry { get; set; }

        /// <summary>
        /// True if this sound comes from a HAK file (requires extraction for playback).
        /// </summary>
        public bool IsFromHak => HakPath != null && ErfEntry != null;

        /// <summary>
        /// If from BIF, the BIF sound info for extraction.
        /// </summary>
        public BifSoundInfo? BifInfo { get; set; }

        /// <summary>
        /// True if this sound comes from a BIF file (requires extraction for playback).
        /// </summary>
        public bool IsFromBif => BifInfo != null;

        /// <summary>
        /// True if the file has a valid WAV header. False for invalid/corrupt files.
        /// </summary>
        public bool IsValidWav { get; set; } = true;

        /// <summary>
        /// If IsValidWav is false, describes why.
        /// </summary>
        public string InvalidWavReason { get; set; } = "";
    }
}

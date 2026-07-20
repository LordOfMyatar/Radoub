namespace Radoub.Formats.Ssf;

/// <summary>
/// Represents a single entry in a Sound Set File (SSF).
/// Each entry maps to a specific sound event (attack, hello, etc).
/// </summary>
public class SsfEntry
{
    /// <summary>ResRef of the sound file to play (up to 16 characters, without .wav extension).</summary>
    public string ResRef { get; set; } = string.Empty;

    /// <summary>
    /// StringRef to dialog.tlk for the text to display when this sound plays.
    /// 0xFFFFFFFF (-1) means no text.
    /// </summary>
    public uint StringRef { get; set; } = uint.MaxValue;

    /// <summary>Valid sound reference; "****" is the empty-slot sentinel.</summary>
    public bool HasSound => !string.IsNullOrEmpty(ResRef) && ResRef != "****";

    public bool HasText => StringRef != uint.MaxValue;
}

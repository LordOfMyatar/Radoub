namespace Radoub.Formats.Tlk;

/// <summary>
/// Represents a TLK (Talk Table) file used for string localization in Aurora Engine games.
/// Reference: neverwinter.nim tlk.nim
/// </summary>
public class TlkFile
{
    /// <summary>
    /// File signature - "TLK "
    /// </summary>
    public string FileType { get; set; } = "TLK ";

    /// <summary>
    /// File version - "V3.0"
    /// </summary>
    public string FileVersion { get; set; } = "V3.0";

    /// <summary>
    /// Language ID for this TLK file.
    /// </summary>
    public uint LanguageId { get; set; }

    /// <summary>
    /// String entries indexed by StrRef.
    /// </summary>
    public List<TlkEntry> Entries { get; set; } = new();

    /// <summary>
    /// Get a string by StrRef.
    /// Returns null if StrRef is out of range or entry has no text.
    /// </summary>
    public string? GetString(uint strRef)
    {
        if (strRef >= Entries.Count)
            return null;

        var entry = Entries[(int)strRef];
        return entry.HasText ? entry.Text : null;
    }

    /// <summary>
    /// Get an entry by StrRef.
    /// Returns null if StrRef is out of range.
    /// </summary>
    public TlkEntry? GetEntry(uint strRef)
    {
        if (strRef >= Entries.Count)
            return null;

        return Entries[(int)strRef];
    }

    /// <summary>
    /// Total number of string entries.
    /// </summary>
    public int Count => Entries.Count;
}

/// <summary>
/// A single entry in a TLK file.
/// </summary>
public class TlkEntry
{
    /// <summary>
    /// Entry flags. Bit 0x1 = has text, Bit 0x2 = has sound, Bit 0x4 = has sound length.
    /// </summary>
    public uint Flags { get; set; }

    /// <summary>
    /// The text content. May be empty if HasText is false.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Sound resource reference (ResRef for associated audio).
    /// </summary>
    public string SoundResRef { get; set; } = string.Empty;

    /// <summary>
    /// Volume variance (unused by most games).
    /// </summary>
    public uint VolumeVariance { get; set; }

    /// <summary>
    /// Pitch variance (unused by most games).
    /// </summary>
    public uint PitchVariance { get; set; }

    /// <summary>
    /// Sound duration in seconds.
    /// </summary>
    public float SoundLength { get; set; }

    /// <summary>
    /// Whether this entry contains text (flag bit 0x1).
    /// </summary>
    public bool HasText => (Flags & 0x1) != 0;

    /// <summary>
    /// Whether this entry has an associated sound (flag bit 0x2).
    /// </summary>
    public bool HasSound => (Flags & 0x2) != 0;

    /// <summary>
    /// Whether this entry has a sound length value (flag bit 0x4).
    /// </summary>
    public bool HasSoundLength => (Flags & 0x4) != 0;
}

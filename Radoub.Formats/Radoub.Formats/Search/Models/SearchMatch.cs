namespace Radoub.Formats.Search;

/// <summary>
/// A single search match within a GFF file.
/// </summary>
public class SearchMatch
{
    /// <summary>Which field definition matched</summary>
    public required FieldDefinition Field { get; init; }

    /// <summary>The text that matched the pattern</summary>
    public required string MatchedText { get; init; }

    /// <summary>Complete value of the field (for context preview)</summary>
    public required string FullFieldValue { get; init; }

    /// <summary>Character offset of match within FullFieldValue</summary>
    public int MatchOffset { get; init; }

    /// <summary>Length of the match</summary>
    public int MatchLength { get; init; }

    /// <summary>
    /// Provider-specific location within the file.
    /// DLG: DlgMatchLocation. GFF: string field path. UTC: field name.
    /// </summary>
    public object? Location { get; init; }

    /// <summary>
    /// For LocString matches: which language variant matched (null for non-LocString).
    /// Language ID encoding: LanguageEnum * 2 + Gender (0=male, 1=female).
    /// </summary>
    public uint? LanguageId { get; init; }

    public override string ToString() => $"{Field.Name}: \"{MatchedText}\" at offset {MatchOffset}";
}

/// <summary>
/// Location within a DLG file tree.
/// </summary>
public class DlgMatchLocation
{
    public required DlgNodeType NodeType { get; init; }
    public int? NodeIndex { get; init; }
    public bool IsOnLink { get; init; }
    public int? LinkIndex { get; init; }
    public required string DisplayPath { get; init; }
}

public enum DlgNodeType
{
    Entry,
    Reply,
    StartingLink
}

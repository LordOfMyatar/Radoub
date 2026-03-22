namespace Radoub.Formats.Search;

/// <summary>
/// A single replace instruction for a matched field.
/// </summary>
public class ReplaceOperation
{
    /// <summary>The match to replace</summary>
    public required SearchMatch Match { get; init; }

    /// <summary>Replacement text</summary>
    public required string ReplacementText { get; init; }

    /// <summary>Use regex substitution (capture groups)</summary>
    public bool IsRegex { get; init; }
}

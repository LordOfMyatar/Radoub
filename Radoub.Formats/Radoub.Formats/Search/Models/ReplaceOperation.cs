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

    /// <summary>
    /// When true, ResRef fields with IsReplaceable=false are still replaced.
    /// Default false. Set by ResRefRenameOrchestrator (Chunk 3) when applying
    /// reference updates as part of a rename operation.
    /// </summary>
    public bool AllowResRefReplace { get; init; }

    /// <summary>
    /// When true, the replacement adopts the matched span's case style (#2180):
    /// louis/Louis/LOUIS → lewie/Lewie/LEWIE. Default false (verbatim insert).
    /// Ignored for ResRef-typed fields (and the filename virtual field, also
    /// ResRef-typed), which stay lowercase. Set true by Marlinspike; other
    /// consumers leave it false to preserve verbatim behavior.
    /// </summary>
    public bool PreserveCase { get; init; }
}

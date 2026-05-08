namespace Radoub.Formats.Search.Rename;

/// <summary>
/// One reference to a ResRef discovered by the scanner.
/// Each reference becomes a row in the rename preview UI;
/// users can untick rows to skip individual updates.
/// </summary>
public class ResRefReference
{
    /// <summary>Absolute path to the file containing this reference.</summary>
    public required string FilePath { get; init; }

    /// <summary>Resource type of the file (used for provider dispatch).
    /// For .nss matches this is set to ResourceTypes.Nss (added in registry if not already present).</summary>
    public required ushort ResourceType { get; init; }

    /// <summary>Field where this reference lives (registered FieldDefinition).
    /// Null for .nss matches and DLG script-param substring matches.</summary>
    public FieldDefinition? Field { get; init; }

    /// <summary>Human-readable location within the file
    /// (e.g., "Creature List > Item 3 > TemplateResRef" or "Line 42 (script source)").</summary>
    public required string Location { get; init; }

    /// <summary>Current value at this reference site (the old ResRef, possibly mixed-case).</summary>
    public required string OldValue { get; init; }

    /// <summary>The new value to write (lowercased target ResRef).</summary>
    public required string NewValue { get; init; }

    /// <summary>Where this reference was found and how confident the match is.</summary>
    public required ResRefScopeTier ScopeTier { get; init; }

    /// <summary>True for any non-typed-GFF match (.nss source or DLG script params).
    /// Convenience flag derived from ScopeTier; orchestrator branches on this.</summary>
    public bool IsTextMatch => ScopeTier != ResRefScopeTier.TypedGffField;

    /// <summary>For .nss/DLG-param matches: byte offset of the match in the source.
    /// For typed GFF fields: 0 (entire field value is replaced).</summary>
    public int MatchOffset { get; init; }

    /// <summary>For .nss/DLG-param matches: length of the matched text.
    /// For typed GFF fields: full field length.</summary>
    public int MatchLength { get; init; }

    /// <summary>User-controllable: whether this reference will be applied during execute.
    /// Default true; users untick false positives in the preview UI.</summary>
    public bool IsSelected { get; set; } = true;
}

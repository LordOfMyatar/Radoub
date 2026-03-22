namespace Radoub.Formats.Search;

/// <summary>
/// Describes a single searchable field within a GFF file type.
/// </summary>
public class FieldDefinition
{
    /// <summary>Display name shown in UI (e.g., "Speaker", "First Name")</summary>
    public required string Name { get; init; }

    /// <summary>GFF field label or path (e.g., "Text", "FirstName", "Script")</summary>
    public required string GffPath { get; init; }

    /// <summary>Field data type — determines matching behavior</summary>
    public required SearchFieldType FieldType { get; init; }

    /// <summary>Logical category for UI grouping</summary>
    public required SearchFieldCategory Category { get; init; }

    /// <summary>Whether this field supports replace operations</summary>
    public bool IsReplaceable { get; init; } = true;

    /// <summary>Tooltip description for UI</summary>
    public string Description { get; init; } = string.Empty;

    public override string ToString() => $"{Name} ({FieldType})";
}

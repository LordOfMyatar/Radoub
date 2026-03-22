namespace Radoub.Formats.Search;

/// <summary>
/// Result of a single replace operation.
/// </summary>
public class ReplaceResult
{
    public required bool Success { get; init; }
    public required FieldDefinition Field { get; init; }
    public required string OldValue { get; init; }
    public required string NewValue { get; init; }
    public bool Skipped { get; init; }
    public string? SkipReason { get; init; }
    public string? Warning { get; init; }
}

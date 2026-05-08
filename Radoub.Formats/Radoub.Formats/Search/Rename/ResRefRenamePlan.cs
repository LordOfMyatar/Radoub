namespace Radoub.Formats.Search.Rename;

/// <summary>
/// Captures one rename operation: old name, new name (validated), and all
/// references that will be updated. The orchestrator consumes this directly.
/// </summary>
public class ResRefRenamePlan
{
    /// <summary>Original ResRef being renamed (lowercased for matching).</summary>
    public required string OldName { get; init; }

    /// <summary>Validated new ResRef name.</summary>
    public required string NewName { get; init; }

    /// <summary>Resource type / extension of the file being renamed.</summary>
    public required ushort ResourceType { get; init; }

    /// <summary>Validation result for the new name (collisions, warnings).</summary>
    public required ResRefValidationResult Validation { get; init; }

    /// <summary>All discovered references to the old name. Each row has IsSelected.</summary>
    public List<ResRefReference> References { get; init; } = new();

    /// <summary>Source file path being renamed (the file whose name is changing).</summary>
    public required string SourceFilePath { get; init; }

    /// <summary>Target file path after rename (computed: directory + NewName + extension).</summary>
    public required string TargetFilePath { get; init; }

    /// <summary>True if the user has not unticked this rename in the preview.
    /// When false, the file rename is skipped but the user may still apply
    /// individual reference updates from References.</summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>References user has chosen to apply (IsSelected == true).</summary>
    public IEnumerable<ResRefReference> SelectedReferences =>
        References.Where(r => r.IsSelected);
}

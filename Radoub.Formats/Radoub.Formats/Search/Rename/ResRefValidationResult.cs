namespace Radoub.Formats.Search.Rename;

/// <summary>
/// Outcome of validating and normalizing a proposed ResRef name.
/// </summary>
public record ResRefValidationResult
{
    /// <summary>True if the name is acceptable (possibly with a warning or auto-suffix).</summary>
    public required bool IsValid { get; init; }

    /// <summary>The normalized, validated name. Lowercase, extension stripped,
    /// possibly suffixed for collision resolution. Empty when IsValid is false.</summary>
    public required string NormalizedName { get; init; }

    /// <summary>Hard-fail error message when IsValid is false. Null when IsValid is true.</summary>
    public string? Error { get; init; }

    /// <summary>Non-blocking advisory (e.g., "starts with a digit"). Null if no warning.</summary>
    public string? Warning { get; init; }

    /// <summary>True when collision resolution applied an auto-suffix (e.g., _2, _3).
    /// UI must surface a confirmation dialog before proceeding.</summary>
    public bool AutoSuffixApplied { get; init; }

    public static ResRefValidationResult Fail(string error) => new()
    {
        IsValid = false, NormalizedName = string.Empty, Error = error
    };

    public static ResRefValidationResult Ok(string name, string? warning = null, bool autoSuffix = false) => new()
    {
        IsValid = true, NormalizedName = name, Warning = warning, AutoSuffixApplied = autoSuffix
    };
}

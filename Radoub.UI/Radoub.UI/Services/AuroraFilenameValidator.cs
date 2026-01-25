namespace Radoub.UI.Services;

/// <summary>
/// Validates filenames for Aurora Engine (Neverwinter Nights) compatibility.
/// Aurora Engine has strict filename constraints: max 16 characters, lowercase,
/// alphanumeric and underscore only.
/// </summary>
public static class AuroraFilenameValidator
{
    /// <summary>
    /// Maximum filename length for Aurora Engine (excluding extension).
    /// </summary>
    public const int MaxFilenameLength = 16;

    /// <summary>
    /// Validates a filename for Aurora Engine compatibility.
    /// </summary>
    /// <param name="filename">Filename without extension</param>
    /// <returns>Validation result with any errors</returns>
    public static ValidationResult Validate(string filename)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(filename))
        {
            errors.Add("Filename cannot be empty.");
            return new ValidationResult(false, errors);
        }

        // Check length
        if (filename.Length > MaxFilenameLength)
        {
            errors.Add($"Filename is too long ({filename.Length} characters). Maximum is {MaxFilenameLength} characters.");
        }

        // Check for uppercase
        if (filename.Any(char.IsUpper))
        {
            errors.Add($"Filename contains uppercase letters. Suggested: \"{filename.ToLowerInvariant()}\"");
        }

        // Check for invalid characters (only alphanumeric and underscore allowed)
        var invalidChars = filename.Where(c => !char.IsLetterOrDigit(c) && c != '_').Distinct().ToList();
        if (invalidChars.Count > 0)
        {
            var invalidStr = string.Join("", invalidChars);
            errors.Add($"Filename contains invalid characters: \"{invalidStr}\". Only letters, numbers, and underscores are allowed.");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Suggests a valid filename based on the input.
    /// </summary>
    /// <param name="filename">Original filename</param>
    /// <returns>Sanitized filename that passes validation (may be truncated)</returns>
    public static string Sanitize(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return "unnamed";

        // Lowercase
        var sanitized = filename.ToLowerInvariant();

        // Replace invalid characters with underscore
        sanitized = new string(sanitized.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());

        // Remove consecutive underscores
        while (sanitized.Contains("__"))
            sanitized = sanitized.Replace("__", "_");

        // Trim underscores from start/end
        sanitized = sanitized.Trim('_');

        // Truncate to max length
        if (sanitized.Length > MaxFilenameLength)
            sanitized = sanitized[..MaxFilenameLength];

        // If empty after sanitization, use default
        if (string.IsNullOrEmpty(sanitized))
            sanitized = "unnamed";

        return sanitized;
    }
}

/// <summary>
/// Result of filename validation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Errors { get; }

    public ValidationResult(bool isValid, IEnumerable<string> errors)
    {
        IsValid = isValid;
        Errors = errors.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets all errors as a single string, one per line.
    /// </summary>
    public string GetErrorMessage() => string.Join("\n", Errors);
}

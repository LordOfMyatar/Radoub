using System.Linq;

namespace Radoub.Formats.Search.Rename;

/// <summary>
/// Validates and normalizes proposed ResRef names. Pure logic — no I/O.
/// See spec Section 6 (NonPublic/Trebuchet/2026-05-03-resref-rename-design.md).
/// </summary>
public class ResRefValidator
{
    private const int MaxLength = 16;

    /// <summary>
    /// Validates and normalizes a proposed ResRef name.
    /// </summary>
    /// <param name="proposedName">Raw user input. May include extension; will be trimmed and lowercased.</param>
    /// <param name="existingNames">Existing ResRefs in the target scope, used for collision detection.
    /// Caller must EXCLUDE the name being renamed — otherwise renaming "louis" → "louis" would falsely
    /// trigger an auto-suffix.</param>
    /// <param name="extension">Target file extension (e.g., ".dlg"). Used to detect and strip a user-typed
    /// extension from <paramref name="proposedName"/>.</param>
    /// <returns>A <see cref="ResRefValidationResult"/>. On collision, auto-suffixes _2.._99 are tried;
    /// if all are taken, returns a Fail result.</returns>
    public ResRefValidationResult Validate(
        string proposedName,
        IReadOnlySet<string> existingNames,
        string extension)
    {
        var trimmed = (proposedName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            return ResRefValidationResult.Fail("ResRef cannot be empty");

        // Strip user-typed extension (any case) if it matches the target extension
        var ext = extension.TrimStart('.');
        if (trimmed.EndsWith($".{ext}", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^(ext.Length + 1)];

        var normalized = trimmed.ToLowerInvariant();

        if (normalized.Length > MaxLength)
            return ResRefValidationResult.Fail(
                $"ResRef '{normalized}' is {normalized.Length} characters ({MaxLength} max). " +
                $"Try '{normalized[..MaxLength]}'.");

        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[a-z0-9_]+$"))
        {
            var badChars = normalized
                .Where(c => !((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                .Distinct()
                .Select(c => c == ' ' ? "space" : $"'{c}'");
            return ResRefValidationResult.Fail(
                $"ResRef can only contain lowercase letters, digits, and underscores. " +
                $"Remove: {string.Join(", ", badChars)}");
        }

        string? warning = null;
        if (char.IsDigit(normalized[0]))
            warning = "ResRef starts with a digit — confirm this is intentional";

        return ResolveCollision(normalized, warning, existingNames);
    }

    private static ResRefValidationResult ResolveCollision(
        string baseName,
        string? warning,
        IReadOnlySet<string> existing)
    {
        if (!existing.Contains(baseName))
            return ResRefValidationResult.Ok(baseName, warning);

        for (int suffix = 2; suffix <= 99; suffix++)
        {
            var candidate = BuildCandidate(baseName, suffix);
            if (!existing.Contains(candidate))
                return ResRefValidationResult.Ok(candidate, warning, autoSuffix: true);
        }

        return ResRefValidationResult.Fail(
            $"Cannot find available filename — {baseName} and all suffixes _2 through _99 are taken");
    }

    private static string BuildCandidate(string baseName, int suffix)
    {
        var suffixStr = $"_{suffix}";
        var maxBaseLen = MaxLength - suffixStr.Length;
        var truncatedBase = baseName.Length > maxBaseLen ? baseName[..maxBaseLen] : baseName;
        return truncatedBase + suffixStr;
    }
}

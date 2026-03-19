using System;
using System.Text.RegularExpressions;

namespace ItemEditor.Services;

/// <summary>
/// Pure logic service for NWN item Tag and ResRef generation, validation, and conflict resolution.
/// ResRef: filename-like (16 char max, lowercase, [a-z0-9_]).
/// Tag: script identifier (32 char max, typically UPPERCASE, [a-zA-Z0-9_]).
/// </summary>
public static partial class ItemNamingService
{
    private const int MaxResRefLength = 16;
    private const int MaxTagLength = 32;

    [GeneratedRegex(@"[^a-zA-Z0-9 ]")]
    private static partial Regex NonAlphanumericOrSpaceRegex();

    [GeneratedRegex(@"^[a-z0-9_]+$")]
    private static partial Regex ValidResRefRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
    private static partial Regex ValidTagRegex();

    /// <summary>
    /// Generates a NWN Tag from a display name.
    /// Strip non-alphanumeric (keep spaces), replace spaces with '_', UPPERCASE, truncate at 32, trim trailing '_'.
    /// </summary>
    public static string GenerateTag(string name)
    {
        var sanitized = Sanitize(name);
        if (sanitized.Length == 0)
            return string.Empty;

        var tag = sanitized.ToUpperInvariant();
        return TruncateAndTrim(tag, MaxTagLength);
    }

    /// <summary>
    /// Generates a NWN ResRef from a display name.
    /// Strip non-alphanumeric (keep spaces), replace spaces with '_', lowercase, truncate at 16, trim trailing '_'.
    /// </summary>
    public static string GenerateResRef(string name)
    {
        var sanitized = Sanitize(name);
        if (sanitized.Length == 0)
            return string.Empty;

        var resref = sanitized.ToLowerInvariant();
        return TruncateAndTrim(resref, MaxResRefLength);
    }

    /// <summary>
    /// Returns true if the resRef is valid: [a-z0-9_], 1–16 chars.
    /// </summary>
    public static bool IsValidResRef(string resRef)
    {
        if (string.IsNullOrEmpty(resRef) || resRef.Length > MaxResRefLength)
            return false;
        return ValidResRefRegex().IsMatch(resRef);
    }

    /// <summary>
    /// Returns true if the tag is valid: [a-zA-Z0-9_], 1–32 chars.
    /// </summary>
    public static bool IsValidTag(string tag)
    {
        if (string.IsNullOrEmpty(tag) || tag.Length > MaxTagLength)
            return false;
        return ValidTagRegex().IsMatch(tag);
    }

    /// <summary>
    /// BioWare-style ResRef conflict resolution.
    /// If resRef does not exist, returns it unchanged.
    /// If it exists, chops the last 3 chars (or uses the first char for short resRefs), appends "001", "002", etc.
    /// Result always stays within MaxResRefLength.
    /// Tags do NOT need conflict resolution in NWN.
    /// </summary>
    public static string ResolveResRefConflict(string resRef, Func<string, bool> exists)
    {
        if (!exists(resRef))
            return resRef;

        // BioWare style: determine base (chop last 3 chars, or use first char for short refs)
        string baseRef = resRef.Length >= 4
            ? resRef[..^3]
            : resRef[..1];

        // Ensure base fits within 13 chars so appending "NNN" stays <= 16
        if (baseRef.Length > MaxResRefLength - 3)
            baseRef = baseRef[..(MaxResRefLength - 3)];

        for (int i = 1; i <= 999; i++)
        {
            var candidate = baseRef + i.ToString("D3");
            if (!exists(candidate))
                return candidate;
        }

        // Fallback: should never reach here in practice
        return baseRef + "999";
    }

    private static string Sanitize(string name)
    {
        // Strip non-alphanumeric (keep spaces), collapse multiple spaces, trim
        var stripped = NonAlphanumericOrSpaceRegex().Replace(name, string.Empty);
        var trimmed = stripped.Trim();
        // Replace internal spaces with '_'
        return trimmed.Replace(' ', '_');
    }

    private static string TruncateAndTrim(string value, int maxLength)
    {
        if (value.Length > maxLength)
            value = value[..maxLength];
        return value.TrimEnd('_');
    }
}

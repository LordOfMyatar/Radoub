using System.Text.RegularExpressions;

namespace PlaceableEditor.Services;

/// <summary>
/// Tag/ResRef generation from a display name (#2372), mirroring Relique's ItemNamingService.
/// ResRef: filename-like (16 char max, lowercase, [a-z0-9_]). Tag: script identifier (32 char max,
/// UPPERCASE, [a-zA-Z0-9_]). Used by the Identity panel's "sync with name" checkboxes.
/// </summary>
public static partial class PlaceableNamingService
{
    private const int MaxResRefLength = 16;
    private const int MaxTagLength = 32;

    [GeneratedRegex(@"[^a-zA-Z0-9 ]")]
    private static partial Regex NonAlphanumericOrSpaceRegex();

    [GeneratedRegex(@" +")]
    private static partial Regex MultiSpaceRegex();

    /// <summary>Tag from a name: sanitize, '_' for spaces, UPPERCASE, ≤32, trim trailing '_'.</summary>
    public static string GenerateTag(string name)
    {
        var sanitized = Sanitize(name);
        return sanitized.Length == 0 ? string.Empty
            : TruncateAndTrim(sanitized.ToUpperInvariant(), MaxTagLength);
    }

    /// <summary>ResRef from a name: sanitize, '_' for spaces, lowercase, ≤16, trim trailing '_'.</summary>
    public static string GenerateResRef(string name)
    {
        var sanitized = Sanitize(name);
        return sanitized.Length == 0 ? string.Empty
            : TruncateAndTrim(sanitized.ToLowerInvariant(), MaxResRefLength);
    }

    private static string Sanitize(string name)
    {
        var stripped = NonAlphanumericOrSpaceRegex().Replace(name, string.Empty);
        var collapsed = MultiSpaceRegex().Replace(stripped, " ");
        return collapsed.Trim().Replace(' ', '_');
    }

    private static string TruncateAndTrim(string value, int maxLength)
    {
        if (value.Length > maxLength)
            value = value[..maxLength];
        return value.TrimEnd('_');
    }
}

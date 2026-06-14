using System.Linq;

namespace Radoub.Formats.Search;

/// <summary>Case style of a matched string.</summary>
public enum CaseKind { AllLower, AllUpper, TitleCase, Mixed }

/// <summary>
/// Pure: detect a matched string's case style and reapply it to a replacement.
/// Used by content replace to preserve casing per match (#2180). Mixed/ambiguous
/// styles (internal capitals, no letters) fall back to the replacement verbatim.
/// No I/O.
/// </summary>
public static class CaseStyle
{
    public static CaseKind Detect(string matched)
    {
        if (string.IsNullOrEmpty(matched)) return CaseKind.Mixed;

        var letters = matched.Where(char.IsLetter).ToArray();
        if (letters.Length == 0) return CaseKind.Mixed;

        if (letters.All(char.IsUpper)) return CaseKind.AllUpper;
        if (letters.All(char.IsLower)) return CaseKind.AllLower;

        // TitleCase: first letter upper, all remaining letters lower.
        if (char.IsUpper(letters[0]) && letters.Skip(1).All(char.IsLower))
            return CaseKind.TitleCase;

        return CaseKind.Mixed;
    }

    public static string Apply(CaseKind kind, string replacement)
    {
        if (string.IsNullOrEmpty(replacement)) return replacement;
        return kind switch
        {
            CaseKind.AllUpper => replacement.ToUpperInvariant(),
            CaseKind.AllLower => replacement.ToLowerInvariant(),
            CaseKind.TitleCase => char.ToUpperInvariant(replacement[0]) + replacement[1..].ToLowerInvariant(),
            _ => replacement // Mixed → verbatim
        };
    }
}

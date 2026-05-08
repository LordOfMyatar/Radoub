using System.Text.RegularExpressions;
using Radoub.Formats.Common;
using Radoub.Formats.Search.Rename;

namespace Radoub.UI.Services.Search;

/// <summary>
/// Scans .nss script source for ResRef substring matches.
/// Quoted matches ("target_resref") are high confidence; bare substring matches
/// (target_resref) are low confidence and visually flagged in the preview.
/// See spec Section 4 Tier 3.
/// </summary>
public class NssReferenceScanner
{
    public IReadOnlyList<ResRefReference> Scan(string nssFilePath, string oldResRef)
    {
        if (string.IsNullOrEmpty(oldResRef)) return Array.Empty<ResRefReference>();
        if (!File.Exists(nssFilePath)) return Array.Empty<ResRefReference>();

        var source = File.ReadAllText(nssFilePath);
        var results = new List<ResRefReference>();

        // Pass 1: quoted matches (high confidence) — pattern: "<oldResRef>"
        var quotedPattern = $"\"{Regex.Escape(oldResRef)}\"";
        foreach (Match m in Regex.Matches(source, quotedPattern, RegexOptions.IgnoreCase))
        {
            var inner = m.Index + 1;
            var innerLen = m.Length - 2;
            results.Add(new ResRefReference
            {
                FilePath = nssFilePath,
                ResourceType = ResourceTypes.Nss,
                Field = null,
                Location = $"Line {LineOf(source, m.Index)} (quoted)",
                OldValue = source.Substring(inner, innerLen),
                NewValue = string.Empty,
                ScopeTier = ResRefScopeTier.NssQuotedString,
                MatchOffset = inner,
                MatchLength = innerLen
            });
        }

        return results;
    }

    private static int LineOf(string text, int byteIndex)
    {
        int line = 1;
        for (int i = 0; i < byteIndex && i < text.Length; i++)
            if (text[i] == '\n') line++;
        return line;
    }
}

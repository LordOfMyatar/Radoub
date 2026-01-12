using System.Text.RegularExpressions;

namespace Radoub.Formats.Tokens;

/// <summary>
/// Parses text containing Aurora Engine tokens into segments.
/// Supports standard tokens, highlight tokens, color tokens, and user-defined color tokens.
/// </summary>
public class TokenParser
{
    private readonly UserColorConfig? _userColorConfig;

    // Regex patterns for token detection
    private static readonly Regex StandardTokenRegex = new(
        @"<([A-Za-z][A-Za-z0-9/]*)>",
        RegexOptions.Compiled);

    private static readonly Regex HighlightTokenRegex = new(
        @"<(StartAction|StartCheck|StartHighlight)>(.*?)(</(Start|End)>|<End>)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex ColorTokenRegex = new(
        @"<c(.{3,})>(.*?)</c>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Create a parser without user color configuration.
    /// </summary>
    public TokenParser()
    {
    }

    /// <summary>
    /// Create a parser with user color configuration for custom token support.
    /// </summary>
    /// <param name="userColorConfig">User-defined color token mappings</param>
    public TokenParser(UserColorConfig userColorConfig)
    {
        _userColorConfig = userColorConfig;
    }

    /// <summary>
    /// Parse text into a list of token segments.
    /// </summary>
    /// <param name="text">Text to parse</param>
    /// <returns>List of token segments in order</returns>
    public List<TokenSegment> Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<TokenSegment>();

        var segments = new List<TokenSegment>();
        var processed = new bool[text.Length];

        // Process in order of precedence:
        // 1. Highlight tokens (longest match, most specific)
        // 2. Color tokens
        // 3. User color tokens (if configured)
        // 4. Standard/Custom tokens

        ProcessHighlightTokens(text, segments, processed);
        ProcessColorTokens(text, segments, processed);

        if (_userColorConfig != null)
        {
            ProcessUserColorTokens(text, segments, processed);
        }

        ProcessStandardTokens(text, segments, processed);

        // Fill in plain text gaps
        FillPlainTextGaps(text, segments, processed);

        // Sort by start index
        segments.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));

        return segments;
    }

    /// <summary>
    /// Parse text and return only the display text (tokens replaced with their content).
    /// </summary>
    /// <param name="text">Text to parse</param>
    /// <returns>Display text with tokens expanded</returns>
    public string GetDisplayText(string text)
    {
        var segments = Parse(text);
        return string.Concat(segments.Select(s => s.DisplayText));
    }

    private void ProcessHighlightTokens(string text, List<TokenSegment> segments, bool[] processed)
    {
        foreach (Match match in HighlightTokenRegex.Matches(text))
        {
            if (IsRangeProcessed(processed, match.Index, match.Length))
                continue;

            var typeStr = match.Groups[1].Value;
            var content = match.Groups[2].Value;
            var closeTagFull = match.Groups[3].Value; // Now captures full close tag

            var type = typeStr.ToLowerInvariant() switch
            {
                "startaction" => HighlightType.Action,
                "startcheck" => HighlightType.Check,
                "starthighlight" => HighlightType.Highlight,
                _ => HighlightType.Action
            };

            var openTag = $"<{typeStr}>";

            segments.Add(new HighlightToken(
                match.Value, type, content, openTag, closeTagFull, match.Index));

            MarkProcessed(processed, match.Index, match.Length);
        }
    }

    private void ProcessColorTokens(string text, List<TokenSegment> segments, bool[] processed)
    {
        foreach (Match match in ColorTokenRegex.Matches(text))
        {
            if (IsRangeProcessed(processed, match.Index, match.Length))
                continue;

            var rgbContent = match.Groups[1].Value;
            var textContent = match.Groups[2].Value;

            // Try to decode RGB values
            if (ColorTokenEncoder.DecodeRgbWithEscapes(rgbContent, out byte r, out byte g, out byte b))
            {
                var openTag = $"<c{rgbContent}>";
                segments.Add(new ColorToken(
                    match.Value, r, g, b, textContent, openTag, "</c>", match.Index));

                MarkProcessed(processed, match.Index, match.Length);
            }
        }
    }

    private void ProcessUserColorTokens(string text, List<TokenSegment> segments, bool[] processed)
    {
        if (_userColorConfig == null || string.IsNullOrEmpty(_userColorConfig.CloseToken))
            return;

        foreach (var colorDef in _userColorConfig.Colors)
        {
            var openToken = colorDef.Value;
            var closeToken = _userColorConfig.CloseToken;

            // Find all occurrences of this color token pair
            int searchStart = 0;
            while (searchStart < text.Length)
            {
                int openIndex = text.IndexOf(openToken, searchStart, StringComparison.Ordinal);
                if (openIndex < 0)
                    break;

                int contentStart = openIndex + openToken.Length;
                int closeIndex = text.IndexOf(closeToken, contentStart, StringComparison.Ordinal);
                if (closeIndex < 0)
                    break;

                int totalLength = closeIndex + closeToken.Length - openIndex;

                if (!IsRangeProcessed(processed, openIndex, totalLength))
                {
                    var content = text.Substring(contentStart, closeIndex - contentStart);
                    var rawText = text.Substring(openIndex, totalLength);

                    segments.Add(new UserColorToken(
                        rawText, colorDef.Key, content, colorDef.Key,
                        openToken, closeToken, openIndex));

                    MarkProcessed(processed, openIndex, totalLength);
                }

                searchStart = closeIndex + closeToken.Length;
            }
        }
    }

    private void ProcessStandardTokens(string text, List<TokenSegment> segments, bool[] processed)
    {
        foreach (Match match in StandardTokenRegex.Matches(text))
        {
            if (IsRangeProcessed(processed, match.Index, match.Length))
                continue;

            var tokenName = match.Groups[1].Value;

            // Check if it's a standard token
            if (TokenDefinitions.IsStandardToken(tokenName))
            {
                segments.Add(new StandardToken(match.Value, tokenName, match.Index));
                MarkProcessed(processed, match.Index, match.Length);
            }
            // Check if it's a CUSTOM token
            else if (TokenDefinitions.IsCustomToken(tokenName, out int tokenNumber))
            {
                segments.Add(new CustomToken(match.Value, tokenNumber, match.Index));
                MarkProcessed(processed, match.Index, match.Length);
            }
            // Skip highlight open/close tags that weren't matched as pairs
            else if (tokenName.StartsWith("Start", StringComparison.OrdinalIgnoreCase) ||
                     tokenName.Equals("Start", StringComparison.OrdinalIgnoreCase) ||
                     tokenName.Equals("End", StringComparison.OrdinalIgnoreCase))
            {
                // Leave as plain text - orphaned highlight tags
            }
        }
    }

    private void FillPlainTextGaps(string text, List<TokenSegment> segments, bool[] processed)
    {
        int gapStart = -1;

        for (int i = 0; i <= text.Length; i++)
        {
            bool isProcessed = i < text.Length && processed[i];
            bool atEnd = i == text.Length;

            if (!isProcessed && !atEnd)
            {
                if (gapStart < 0)
                    gapStart = i;
            }
            else if (gapStart >= 0)
            {
                var plainText = text.Substring(gapStart, i - gapStart);
                segments.Add(new PlainTextSegment(plainText, gapStart));
                gapStart = -1;
            }
        }
    }

    private static bool IsRangeProcessed(bool[] processed, int start, int length)
    {
        for (int i = start; i < start + length && i < processed.Length; i++)
        {
            if (processed[i])
                return true;
        }
        return false;
    }

    private static void MarkProcessed(bool[] processed, int start, int length)
    {
        for (int i = start; i < start + length && i < processed.Length; i++)
        {
            processed[i] = true;
        }
    }
}

/// <summary>
/// Configuration for user-defined color tokens using CUSTOM token numbers.
/// </summary>
public class UserColorConfig
{
    /// <summary>
    /// The close token that ends all colored text (e.g., "&lt;CUSTOM1000&gt;").
    /// </summary>
    public string CloseToken { get; set; } = "";

    /// <summary>
    /// Map of color names to their open tokens and hex values.
    /// Key: Color name (e.g., "Red")
    /// Value: Open token (e.g., "&lt;CUSTOM1001&gt;")
    /// </summary>
    public Dictionary<string, string> Colors { get; set; } = new();

    /// <summary>
    /// Map of color names to their hex display values for UI.
    /// Key: Color name (e.g., "Red")
    /// Value: Hex color (e.g., "#FF0000")
    /// </summary>
    public Dictionary<string, string> ColorHexValues { get; set; } = new();

    /// <summary>
    /// Get the hex color for a named color.
    /// </summary>
    public string? GetHexColor(string colorName)
    {
        return ColorHexValues.TryGetValue(colorName, out var hex) ? hex : null;
    }

    /// <summary>
    /// Create a default configuration with common colors.
    /// Uses CUSTOM1000 as close tag and CUSTOM1001+ for colors.
    /// </summary>
    public static UserColorConfig CreateDefault()
    {
        return new UserColorConfig
        {
            CloseToken = "<CUSTOM1000>",
            Colors = new Dictionary<string, string>
            {
                ["Red"] = "<CUSTOM1001>",
                ["Green"] = "<CUSTOM1002>",
                ["Blue"] = "<CUSTOM1003>",
                ["Yellow"] = "<CUSTOM1004>",
                ["Orange"] = "<CUSTOM1005>",
                ["Purple"] = "<CUSTOM1006>",
                ["Cyan"] = "<CUSTOM1007>",
                ["Gold"] = "<CUSTOM1008>"
            },
            ColorHexValues = new Dictionary<string, string>
            {
                ["Red"] = "#FF0000",
                ["Green"] = "#00FF00",
                ["Blue"] = "#0000FF",
                ["Yellow"] = "#FFFF00",
                ["Orange"] = "#FFA500",
                ["Purple"] = "#800080",
                ["Cyan"] = "#00FFFF",
                ["Gold"] = "#FFD700"
            }
        };
    }
}

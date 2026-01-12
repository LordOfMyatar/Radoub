namespace Radoub.Formats.Tokens;

/// <summary>
/// Base class for text segments that may contain tokens.
/// A parsed string is decomposed into a sequence of TokenSegments.
/// </summary>
public abstract class TokenSegment
{
    /// <summary>
    /// The raw text of this segment (including token markers for tokens).
    /// </summary>
    public string RawText { get; }

    /// <summary>
    /// The display text (token content or plain text).
    /// </summary>
    public abstract string DisplayText { get; }

    /// <summary>
    /// Starting index in the original string.
    /// </summary>
    public int StartIndex { get; }

    /// <summary>
    /// Length in the original string.
    /// </summary>
    public int Length => RawText.Length;

    protected TokenSegment(string rawText, int startIndex)
    {
        RawText = rawText;
        StartIndex = startIndex;
    }
}

/// <summary>
/// Plain text segment with no token formatting.
/// </summary>
public class PlainTextSegment : TokenSegment
{
    public override string DisplayText => RawText;

    public PlainTextSegment(string text, int startIndex)
        : base(text, startIndex)
    {
    }
}

/// <summary>
/// Standard game variable token (e.g., &lt;FirstName&gt;, &lt;Boy/Girl&gt;).
/// These expand to player/game values at runtime.
/// </summary>
public class StandardToken : TokenSegment
{
    /// <summary>
    /// The token name without angle brackets (e.g., "FirstName", "Boy/Girl").
    /// </summary>
    public string TokenName { get; }

    public override string DisplayText => TokenName;

    public StandardToken(string rawText, string tokenName, int startIndex)
        : base(rawText, startIndex)
    {
        TokenName = tokenName;
    }
}

/// <summary>
/// Custom token placeholder (e.g., &lt;CUSTOM0&gt; through &lt;CUSTOM9&gt;).
/// Values are set via SetCustomToken() in NWScript.
/// </summary>
public class CustomToken : TokenSegment
{
    /// <summary>
    /// The custom token number (0-9 for standard, higher for module-defined).
    /// </summary>
    public int TokenNumber { get; }

    /// <summary>
    /// The token name (e.g., "CUSTOM0", "CUSTOM1001").
    /// </summary>
    public string TokenName { get; }

    public override string DisplayText => TokenName;

    public CustomToken(string rawText, int tokenNumber, int startIndex)
        : base(rawText, startIndex)
    {
        TokenNumber = tokenNumber;
        TokenName = $"CUSTOM{tokenNumber}";
    }
}

/// <summary>
/// Highlight token for action/check/highlight text.
/// Format: &lt;StartAction&gt;[text]&lt;/Start&gt;
/// </summary>
public class HighlightToken : TokenSegment
{
    /// <summary>
    /// Type of highlight (Action, Check, Highlight).
    /// </summary>
    public HighlightType Type { get; }

    /// <summary>
    /// The text content inside the highlight tags.
    /// </summary>
    public string Content { get; }

    public override string DisplayText => Content;

    /// <summary>
    /// The opening tag (e.g., "&lt;StartAction&gt;").
    /// </summary>
    public string OpenTag { get; }

    /// <summary>
    /// The closing tag ("&lt;/Start&gt;").
    /// </summary>
    public string CloseTag { get; }

    public HighlightToken(string rawText, HighlightType type, string content,
        string openTag, string closeTag, int startIndex)
        : base(rawText, startIndex)
    {
        Type = type;
        Content = content;
        OpenTag = openTag;
        CloseTag = closeTag;
    }
}

/// <summary>
/// Types of highlight tokens in Aurora Engine.
/// </summary>
public enum HighlightType
{
    /// <summary>
    /// Action text (green) - describes character actions.
    /// Example: [Chef waves]
    /// </summary>
    Action,

    /// <summary>
    /// Check text (red) - indicates skill/ability checks.
    /// Example: [Lore]
    /// </summary>
    Check,

    /// <summary>
    /// Highlight text (blue) - emphasized text.
    /// Example: [You are surprised]
    /// </summary>
    Highlight
}

/// <summary>
/// Color token for custom colored text.
/// Format: &lt;cRGB&gt;text&lt;/c&gt; where RGB are ASCII-encoded values.
/// </summary>
public class ColorToken : TokenSegment
{
    /// <summary>
    /// Red component (0-255).
    /// </summary>
    public byte Red { get; }

    /// <summary>
    /// Green component (0-255).
    /// </summary>
    public byte Green { get; }

    /// <summary>
    /// Blue component (0-255).
    /// </summary>
    public byte Blue { get; }

    /// <summary>
    /// The text content inside the color tags.
    /// </summary>
    public string Content { get; }

    public override string DisplayText => Content;

    /// <summary>
    /// The opening tag (e.g., "&lt;cRGB&gt;").
    /// </summary>
    public string OpenTag { get; }

    /// <summary>
    /// The closing tag ("&lt;/c&gt;").
    /// </summary>
    public string CloseTag { get; }

    public ColorToken(string rawText, byte red, byte green, byte blue,
        string content, string openTag, string closeTag, int startIndex)
        : base(rawText, startIndex)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Content = content;
        OpenTag = openTag;
        CloseTag = closeTag;
    }

    /// <summary>
    /// Get the color as a hex string (e.g., "#FF0000").
    /// </summary>
    public string ToHexColor() => $"#{Red:X2}{Green:X2}{Blue:X2}";
}

/// <summary>
/// User-defined color token using CUSTOM token numbers.
/// Format: &lt;CUSTOM###&gt;text&lt;CUSTOM###&gt; (open and close tokens).
/// </summary>
public class UserColorToken : TokenSegment
{
    /// <summary>
    /// The color name from user configuration (e.g., "Red", "Gold").
    /// </summary>
    public string ColorName { get; }

    /// <summary>
    /// The text content between the color tokens.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// The hex color value from configuration (e.g., "#FF0000").
    /// </summary>
    public string HexColor { get; }

    public override string DisplayText => Content;

    /// <summary>
    /// The opening token (e.g., "&lt;CUSTOM1001&gt;").
    /// </summary>
    public string OpenToken { get; }

    /// <summary>
    /// The closing token (e.g., "&lt;CUSTOM1000&gt;").
    /// </summary>
    public string CloseToken { get; }

    public UserColorToken(string rawText, string colorName, string content,
        string hexColor, string openToken, string closeToken, int startIndex)
        : base(rawText, startIndex)
    {
        ColorName = colorName;
        Content = content;
        HexColor = hexColor;
        OpenToken = openToken;
        CloseToken = closeToken;
    }
}

namespace Radoub.Formats.Tokens;

/// <summary>
/// Encoder/decoder for NWN color tokens.
/// NWN uses a non-standard encoding where RGB values are represented as ASCII characters.
/// </summary>
public static class ColorTokenEncoder
{
    /// <summary>
    /// The full 256-character color token string used by NWN.
    /// Position N in this string represents color value N (0-255).
    /// Characters 0-31 are control characters, so practical range starts at 32.
    /// </summary>
    /// <remarks>
    /// This is the standard COLOR_TOKEN constant used in NWScript.
    /// The first 32 characters are ASCII control codes and may not display correctly.
    /// </remarks>
    private const string ColorTokenString =
        "                                " + // 0-31 (control chars, spaces as placeholder)
        " !\"#$%&'()*+,-./0123456789:;<=>?" + // 32-63
        "@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_" + // 64-95
        "`abcdefghijklmnopqrstuvwxyz{|}~\u007F" + // 96-127
        "\u0080\u0081\u0082\u0083\u0084\u0085\u0086\u0087\u0088\u0089\u008A\u008B\u008C\u008D\u008E\u008F" + // 128-143
        "\u0090\u0091\u0092\u0093\u0094\u0095\u0096\u0097\u0098\u0099\u009A\u009B\u009C\u009D\u009E\u009F" + // 144-159
        "\u00A0\u00A1\u00A2\u00A3\u00A4\u00A5\u00A6\u00A7\u00A8\u00A9\u00AA\u00AB\u00AC\u00AD\u00AE\u00AF" + // 160-175
        "\u00B0\u00B1\u00B2\u00B3\u00B4\u00B5\u00B6\u00B7\u00B8\u00B9\u00BA\u00BB\u00BC\u00BD\u00BE\u00BF" + // 176-191
        "\u00C0\u00C1\u00C2\u00C3\u00C4\u00C5\u00C6\u00C7\u00C8\u00C9\u00CA\u00CB\u00CC\u00CD\u00CE\u00CF" + // 192-207
        "\u00D0\u00D1\u00D2\u00D3\u00D4\u00D5\u00D6\u00D7\u00D8\u00D9\u00DA\u00DB\u00DC\u00DD\u00DE\u00DF" + // 208-223
        "\u00E0\u00E1\u00E2\u00E3\u00E4\u00E5\u00E6\u00E7\u00E8\u00E9\u00EA\u00EB\u00EC\u00ED\u00EE\u00EF" + // 224-239
        "\u00F0\u00F1\u00F2\u00F3\u00F4\u00F5\u00F6\u00F7\u00F8\u00F9\u00FA\u00FB\u00FC\u00FD\u00FE\u00FF";  // 240-255

    /// <summary>
    /// Encode RGB values to a NWN color tag.
    /// </summary>
    /// <param name="red">Red component (0-255)</param>
    /// <param name="green">Green component (0-255)</param>
    /// <param name="blue">Blue component (0-255)</param>
    /// <returns>Color tag string (e.g., "&lt;c###&gt;")</returns>
    public static string EncodeColorTag(byte red, byte green, byte blue)
    {
        // Use ASCII values directly - the character at position N represents value N
        char r = (char)red;
        char g = (char)green;
        char b = (char)blue;
        return $"<c{r}{g}{b}>";
    }

    /// <summary>
    /// Encode RGB values to a NWN color tag using safe printable characters.
    /// Values below 32 are shifted to avoid control characters.
    /// </summary>
    /// <param name="red">Red component (0-255)</param>
    /// <param name="green">Green component (0-255)</param>
    /// <param name="blue">Blue component (0-255)</param>
    /// <returns>Color tag string with printable characters</returns>
    public static string EncodeColorTagSafe(byte red, byte green, byte blue)
    {
        // For values < 32, we use the escape sequence format (NWN:EE)
        // This is more reliable across different text editors
        return $"<c{EscapeColorByte(red)}{EscapeColorByte(green)}{EscapeColorByte(blue)}>";
    }

    /// <summary>
    /// Escape a color byte value for NWN:EE format.
    /// </summary>
    private static string EscapeColorByte(byte value)
    {
        if (value < 32)
        {
            // Use \x## hex escape for control characters
            return $"\\x{value:X2}";
        }
        return ((char)value).ToString();
    }

    /// <summary>
    /// Decode a 3-character RGB sequence from a color tag.
    /// </summary>
    /// <param name="rgbChars">The 3 characters after "&lt;c"</param>
    /// <param name="red">Decoded red component</param>
    /// <param name="green">Decoded green component</param>
    /// <param name="blue">Decoded blue component</param>
    /// <returns>True if successfully decoded</returns>
    public static bool DecodeRgb(ReadOnlySpan<char> rgbChars, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;

        if (rgbChars.Length < 3)
            return false;

        // Simple case: direct ASCII value encoding
        red = (byte)rgbChars[0];
        green = (byte)rgbChars[1];
        blue = (byte)rgbChars[2];
        return true;
    }

    /// <summary>
    /// Decode a color tag string that may contain escape sequences.
    /// Supports both direct ASCII and \x## hex escapes (NWN:EE format).
    /// </summary>
    /// <param name="tagContent">Content between &lt;c and &gt;</param>
    /// <param name="red">Decoded red component</param>
    /// <param name="green">Decoded green component</param>
    /// <param name="blue">Decoded blue component</param>
    /// <returns>True if successfully decoded</returns>
    public static bool DecodeRgbWithEscapes(string tagContent, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;

        if (string.IsNullOrEmpty(tagContent))
            return false;

        var values = new List<byte>(3);
        int i = 0;

        while (i < tagContent.Length && values.Count < 3)
        {
            if (i + 3 < tagContent.Length &&
                tagContent[i] == '\\' &&
                tagContent[i + 1] == 'x')
            {
                // Hex escape: \x##
                if (byte.TryParse(tagContent.AsSpan(i + 2, 2),
                    System.Globalization.NumberStyles.HexNumber, null, out byte value))
                {
                    values.Add(value);
                    i += 4;
                    continue;
                }
            }

            // Direct character value
            values.Add((byte)tagContent[i]);
            i++;
        }

        if (values.Count >= 3)
        {
            red = values[0];
            green = values[1];
            blue = values[2];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get the close tag for color tokens.
    /// </summary>
    public static string GetCloseTag() => "</c>";

    /// <summary>
    /// Create a complete colored text string.
    /// </summary>
    /// <param name="text">Text to colorize</param>
    /// <param name="red">Red component (0-255)</param>
    /// <param name="green">Green component (0-255)</param>
    /// <param name="blue">Blue component (0-255)</param>
    /// <returns>Text wrapped in color tags</returns>
    public static string Colorize(string text, byte red, byte green, byte blue)
    {
        return $"{EncodeColorTag(red, green, blue)}{text}</c>";
    }

    /// <summary>
    /// Parse a hex color string to RGB components.
    /// </summary>
    /// <param name="hex">Hex color (e.g., "#FF0000" or "FF0000")</param>
    /// <param name="red">Parsed red component</param>
    /// <param name="green">Parsed green component</param>
    /// <param name="blue">Parsed blue component</param>
    /// <returns>True if successfully parsed</returns>
    public static bool ParseHexColor(string hex, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;

        if (string.IsNullOrEmpty(hex))
            return false;

        // Remove # prefix if present
        if (hex.StartsWith('#'))
            hex = hex[1..];

        if (hex.Length != 6)
            return false;

        try
        {
            red = Convert.ToByte(hex[0..2], 16);
            green = Convert.ToByte(hex[2..4], 16);
            blue = Convert.ToByte(hex[4..6], 16);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Common predefined colors for NWN color tokens.
    /// Values use 254 instead of 255 and 32 instead of 0 to avoid ASCII control characters.
    /// </summary>
    public static class Colors
    {
        // Primary colors
        public static (byte R, byte G, byte B) Red => (254, 32, 32);
        public static (byte R, byte G, byte B) Green => (32, 254, 32);
        public static (byte R, byte G, byte B) Blue => (32, 32, 254);
        public static (byte R, byte G, byte B) Cyan => (32, 254, 254);
        public static (byte R, byte G, byte B) Magenta => (254, 32, 254);
        public static (byte R, byte G, byte B) Yellow => (254, 254, 32);

        // Dark variants (using 165 for mid-tone)
        public static (byte R, byte G, byte B) DarkRed => (165, 32, 32);
        public static (byte R, byte G, byte B) DarkGreen => (32, 165, 32);
        public static (byte R, byte G, byte B) DarkBlue => (32, 32, 165);
        public static (byte R, byte G, byte B) DarkCyan => (32, 165, 165);
        public static (byte R, byte G, byte B) DarkMagenta => (165, 32, 165);
        public static (byte R, byte G, byte B) DarkYellow => (165, 165, 32);

        // Neutrals
        public static (byte R, byte G, byte B) Black => (32, 32, 32);
        public static (byte R, byte G, byte B) DarkGrey => (140, 140, 140);
        public static (byte R, byte G, byte B) Grey => (165, 165, 165);
        public static (byte R, byte G, byte B) White => (254, 254, 254);

        // Special colors
        public static (byte R, byte G, byte B) Orange => (254, 165, 32);
        public static (byte R, byte G, byte B) DarkOrange => (254, 140, 32);
        public static (byte R, byte G, byte B) Brown => (218, 165, 35);
        public static (byte R, byte G, byte B) DarkBrown => (194, 134, 32);
    }
}

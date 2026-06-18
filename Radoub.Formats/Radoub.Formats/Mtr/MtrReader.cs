using System.Globalization;
using System.Text;

namespace Radoub.Formats.Mtr;

/// <summary>
/// Reads NWN:EE MTR material files (resource type 3007) from the ASCII text format.
/// The text format is whitespace-delimited with <c>//</c> line comments; blank lines
/// are ignored. A binary <c>MTR V1.x</c> container exists in NWN:EE but is not yet
/// supported here.
/// </summary>
public static class MtrReader
{
    static MtrReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>Read an MTR file from a file path.</summary>
    public static MtrFile Read(string filePath) => Read(File.ReadAllBytes(filePath));

    /// <summary>Read an MTR file from a stream.</summary>
    public static MtrFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read an MTR file from a byte buffer. NWN1 text is Windows-1252; a UTF-8 BOM
    /// (EF BB BF) opts into UTF-8 (matches <c>TwoDAReader</c>).
    /// </summary>
    public static MtrFile Read(byte[] buffer)
    {
        string text;
        if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            text = Encoding.UTF8.GetString(buffer, 3, buffer.Length - 3);
        else
            text = Encoding.GetEncoding(1252).GetString(buffer);
        return Parse(text);
    }

    /// <summary>Parse MTR text content into an <see cref="MtrFile"/>.</summary>
    public static MtrFile Parse(string text)
    {
        var mtr = new MtrFile();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                continue;

            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                continue;

            var key = tokens[0].ToLowerInvariant();
            switch (key)
            {
                case "customshadervs":
                    mtr.CustomShaderVs = ValueOrNull(tokens, 1);
                    break;
                case "customshaderfs":
                    mtr.CustomShaderFs = ValueOrNull(tokens, 1);
                    break;
                case "renderhint":
                    mtr.RenderHint = ValueOrNull(tokens, 1);
                    break;
                case "parameter":
                    ParseParameter(tokens, mtr);
                    break;
                default:
                    if (key.StartsWith("texture", StringComparison.Ordinal))
                        ParseTexture(key, tokens, mtr);
                    break;
            }
        }

        return mtr;
    }

    private static void ParseTexture(string key, string[] tokens, MtrFile mtr)
    {
        // key is "texture<N>"
        if (!int.TryParse(key.AsSpan("texture".Length), out var slot)
            || slot < 0 || slot >= mtr.Textures.Length)
            return;

        var value = ValueOrNull(tokens, 1);
        // A literal "null" sampler is an empty slot.
        if (value is not null && value.Equals("null", StringComparison.OrdinalIgnoreCase))
            value = null;
        mtr.Textures[slot] = value;
    }

    private static void ParseParameter(string[] tokens, MtrFile mtr)
    {
        // parameter <type> <name> <v0> [v1 ...]
        if (tokens.Length < 4)
            return;

        var name = tokens[2];
        var values = new List<float>(tokens.Length - 3);
        for (var i = 3; i < tokens.Length; i++)
        {
            if (float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                values.Add(f);
        }

        if (values.Count > 0)
            mtr.Parameters[name] = values.ToArray();
    }

    private static string? ValueOrNull(string[] tokens, int index) =>
        index < tokens.Length ? tokens[index] : null;
}

// MDL ASCII Reader - Utility methods
// Partial class for tokenization and parsing helpers

using System.Globalization;
using System.Numerics;

namespace Radoub.Formats.Mdl;

public partial class MdlAsciiReader
{
    private static string[] Tokenize(string line)
    {
        // Handle quoted strings and regular tokens
        var result = new List<string>();
        var i = 0;

        while (i < line.Length)
        {
            // Skip whitespace
            while (i < line.Length && char.IsWhiteSpace(line[i]))
                i++;

            if (i >= line.Length) break;

            if (line[i] == '"')
            {
                // Quoted string
                var end = line.IndexOf('"', i + 1);
                if (end < 0) end = line.Length;
                result.Add(line.Substring(i + 1, end - i - 1));
                i = end + 1;
            }
            else
            {
                // Regular token
                var start = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i]))
                    i++;
                result.Add(line.Substring(start, i - start));
            }
        }

        return result.ToArray();
    }

    private static MdlClassification ParseClassification(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "effect" => MdlClassification.Effect,
            "tile" => MdlClassification.Tile,
            "character" => MdlClassification.Character,
            "door" => MdlClassification.Door,
            _ => MdlClassification.None
        };
    }

    private static float ParseFloat(string value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        return 0f;
    }

    private static int ParseInt(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        return 0;
    }

    private static Quaternion QuaternionFromAxisAngle(float x, float y, float z, float angle)
    {
        var axis = new Vector3(x, y, z);
        if (axis.LengthSquared() < 0.0001f)
            return Quaternion.Identity;

        axis = Vector3.Normalize(axis);
        return Quaternion.CreateFromAxisAngle(axis, angle);
    }
}

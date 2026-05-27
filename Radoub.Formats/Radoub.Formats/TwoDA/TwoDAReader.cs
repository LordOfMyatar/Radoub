using System.Text;

namespace Radoub.Formats.TwoDA;

/// <summary>
/// Reads 2DA (Two-Dimensional Array) files from text format.
/// Reference: neverwinter.nim twoda.nim
/// </summary>
public static class TwoDAReader
{
    private const string ExpectedSignature = "2DA V2.0";
    private const string EmptyCell = "****";
    private const int MaxColumns = 1024;

    private static readonly Encoding NwnEncoding;

    static TwoDAReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        NwnEncoding = Encoding.GetEncoding(1252);
    }

    /// <summary>
    /// Read a 2DA file from a file path.
    /// </summary>
    public static TwoDAFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read a 2DA file from a stream.
    /// </summary>
    public static TwoDAFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read a 2DA file from a byte buffer. NWN1 2DAs are Windows-1252 (#2242,
    /// matches neverwinter.nim twoda.nim:118). A UTF-8 BOM (EF BB BF) opts the
    /// reader into UTF-8 to keep hand-edited / tool-exported UTF-8 2DAs working.
    /// </summary>
    public static TwoDAFile Read(byte[] buffer)
    {
        string text;
        if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            text = Encoding.UTF8.GetString(buffer, 3, buffer.Length - 3);
        }
        else
        {
            text = NwnEncoding.GetString(buffer);
        }
        return Parse(text);
    }

    /// <summary>
    /// Parse 2DA content from a string.
    /// </summary>
    public static TwoDAFile Parse(string content)
    {
        var twoDA = new TwoDAFile();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var lineIndex = 0;

        // Skip empty lines at start
        while (lineIndex < lines.Length && string.IsNullOrWhiteSpace(lines[lineIndex]))
            lineIndex++;

        if (lineIndex >= lines.Length)
            throw new InvalidDataException("2DA file is empty");

        // Read signature line
        var signatureLine = lines[lineIndex].Trim();
        if (!signatureLine.StartsWith("2DA", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Invalid 2DA signature: expected '2DA', got '{signatureLine}'");

        // Extract version
        var signatureParts = signatureLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (signatureParts.Length >= 1)
            twoDA.FileType = signatureParts[0];
        if (signatureParts.Length >= 2)
            twoDA.FileVersion = signatureParts[1];

        lineIndex++;

        // Skip empty lines after signature
        while (lineIndex < lines.Length && string.IsNullOrWhiteSpace(lines[lineIndex]))
            lineIndex++;

        if (lineIndex >= lines.Length)
            return twoDA; // Empty 2DA with just signature

        // Check for DEFAULT line
        var currentLine = lines[lineIndex].Trim();
        if (currentLine.StartsWith("DEFAULT:", StringComparison.OrdinalIgnoreCase))
        {
            var defaultPart = currentLine.Substring(8).Trim();
            if (!string.IsNullOrEmpty(defaultPart))
            {
                twoDA.DefaultValue = ParseField(defaultPart);
            }
            lineIndex++;

            // Skip empty lines after DEFAULT
            while (lineIndex < lines.Length && string.IsNullOrWhiteSpace(lines[lineIndex]))
                lineIndex++;
        }

        if (lineIndex >= lines.Length)
            return twoDA; // 2DA with signature and optional default but no columns

        // Read column headers
        var headerLine = lines[lineIndex];
        twoDA.Columns = ParseFields(headerLine);

        if (twoDA.Columns.Count == 0)
            throw new InvalidDataException("2DA file has no columns");

        if (twoDA.Columns.Count > MaxColumns)
            throw new InvalidDataException($"2DA file has too many columns: {twoDA.Columns.Count} > {MaxColumns}");

        // Validate no empty column headers
        for (int i = 0; i < twoDA.Columns.Count; i++)
        {
            if (string.IsNullOrEmpty(twoDA.Columns[i]))
                throw new InvalidDataException($"Empty column header at position {i}");
        }

        lineIndex++;

        // Read data rows
        while (lineIndex < lines.Length)
        {
            var rowLine = lines[lineIndex];
            lineIndex++;

            if (string.IsNullOrWhiteSpace(rowLine))
                continue;

            var fields = ParseFields(rowLine);
            if (fields.Count == 0)
                continue;

            var row = new TwoDARow();

            // First field is the row label/index
            row.Label = fields[0];

            // Remaining fields are cell values
            for (int i = 1; i < fields.Count && row.Values.Count < twoDA.Columns.Count; i++)
            {
                var value = fields[i];
                // **** represents empty cell
                if (value == EmptyCell)
                    row.Values.Add(null);
                else
                    row.Values.Add(value);
            }

            // Pad with nulls if row is shorter than column count
            while (row.Values.Count < twoDA.Columns.Count)
                row.Values.Add(null);

            twoDA.Rows.Add(row);
        }

        return twoDA;
    }

    /// <summary>
    /// Parse fields from a line, handling quoted strings.
    /// </summary>
    private static List<string> ParseFields(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < line.Length)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Check for escaped quote
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i += 2;
                        continue;
                    }
                    // End of quoted string
                    inQuotes = false;
                    i++;
                    continue;
                }
                current.Append(c);
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                    continue;
                }
                if (c == ' ' || c == '\t')
                {
                    // End of field
                    if (current.Length > 0)
                    {
                        fields.Add(current.ToString());
                        current.Clear();
                    }
                    i++;
                    continue;
                }
                current.Append(c);
            }
            i++;
        }

        // Add final field
        if (current.Length > 0)
            fields.Add(current.ToString());

        return fields;
    }

    /// <summary>
    /// Parse a single field value, removing quotes if present.
    /// </summary>
    private static string ParseField(string value)
    {
        value = value.Trim();
        if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
        {
            value = value.Substring(1, value.Length - 2);
            value = value.Replace("\"\"", "\""); // Unescape quotes
        }
        return value;
    }
}
